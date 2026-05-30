//Missile V18
// =============================================================
// MISSILE PB — Clean Up Naming, Prove Setting Applications,
//              Provide Setting Change Redundancies
// =============================================================

/*****
Chronological mission outline
0) IDLE → LAUNCH_START
1) SETTLE: attitude hold using _refFwd/_refUp, thruster OFF
2) ACTIVE/CRUISE (pre-lock): warmup delay after SETTLE→ACTIVE, then cruise to target speed
   - Gyros hold attitude; no PN
   - Coast into ~2.5 km AI lock range
3) ACTIVE/GUIDE (post-lock): APN guidance using AI waypoint feed
   - Thrust gated by ψ and LOS error; clamp thrust to 0.5–1.0 (Rdav style)
4) TERMINAL/FUSE: arm inside Fuse+20 m, detonate at Fuse
5) LOSS/Safe: if AI feed invalid/stale → thrust 0, hold attitude, then SAFE

This stencil compiles and runs as a no-op for cruise/guide, with rich TODOs.
*****/

// ------------------------------
// Block name tags (keep stable)
// ------------------------------
const string NAME_GYRO_PRE = "M-Gyro";
const string NAME_THR_PRE  = "M-Thrust";
const string NAME_WARH1    = "M-Warhead1";    // optional
const string NAME_WARH2    = "M-Warhead2";    // optional
const string NAME_DECOY    = "M-Decoy";       // optional
const string NAME_MASS     = "M-Mass";        // optional
const string NAME_MERGE    = "M-Merge";       // optional
const string NAME_BATT_PRE = "M-Batt";        // optional
// AI blocks on the MISSILE grid
const string NAME_AI_OFF   = "M-AI-Offense";
const string NAME_AI_FLT   = "M-AI-Flight";

// ---------------------------------------
// Config (CustomData:[Config]) — minimal + profiles
// ---------------------------------------
const string SEC_CFG = "MissileProfile";
const string K_LaunchSettle   = "LaunchSettle";   // seconds
const string K_FuseStandoff   = "FuseStandoff";   // meters
// Profile placeholders (rack can override via CustomData later)
const string K_ProfileIntent  = "ProfileIntent";  // e.g., "Intercept", "Wild Weasel"
const string K_TargetNeutrals = "TargetNeutrals"; // bool; AI Offense + nose sensor follow this
const string K_EnableDecoy    = "EnableDecoy";    // bool; enable decoy at launch

const float  D_LaunchSettle   = 2.5f;
const float  D_FuseStandoff   = 6.0f;            // Rdav default; tune later (e.g., 23 for LG)
// User-facing switches (defaults if CustomData not set)
const string CFG_PROFILE_INTENT        = "INTERCEPT";
const bool   CFG_TARGET_NEUTRALS       = false;   // true to allow targeting neutrals; nose sensor matches
const bool   CFG_ENABLE_DECOY_AT_LAUNCH= false;   // true to start with decoy enabled

// ---------------------------------
// Lifecycle delays and clamps
// ---------------------------------

const int    CRUISE_WARMUP_TICKS = 10;   // ~10 ticks after SETTLE→ACTIVE to allow detach
const int    GUIDEDELAY_TICKS    = 50;   // ~50 ticks before enabling PN (optional)
const double THRUST_MIN_CLAMP    = 0.5;  // Rdav thrust clamp lower bound
const double THRUST_MAX_CLAMP    = 1.0;  // upper bound (LG often forces 1.0)

// Convert old 6 Hz tick gates to seconds for Update1
const double CRUISE_WARMUP_SEC = CRUISE_WARMUP_TICKS / 6.0; // ≈1.67 s
const double GUIDEDELAY_SEC    = GUIDEDELAY_TICKS  / 6.0;   // ≈8.33 s

// RDAV-style accel shaping
const double DRIFT_CANCEL_GAIN = 0.5; // weight for cancelling lateral velocity (unitless blend)

// Cruise targets (40 ± 5 m/s)
const double CRUISE_SET_MS   = 40.0;   // desired cruise speed
const double CRUISE_BAND_MS  = 5.0;    // hysteresis band
const double CRUISE_TIMEOUTS = 12.0;   // max seconds to attempt cruise if no lock

// PN guidance tuning
// RDAV-style per-axis P control at 60 Hz
const double CTRL_GAIN        = 18.0;  // gyro P gain (per-axis), high because Update1

// Desired-accel mix (unitless weights)
const double A_LAT            = 1.0;   // lateral toward LOS
const double A_FWD            = 0.8;   // forward close-in component
const double A_DAMP           = 0.5;   // oppose lateral drift

// Weasel orbit bias (PB-side; AI Offense pattern unused for steering)
const double ORBIT_ENTER_M    = 600.0; // start bias within this range
const double ORBIT_FRAC       = 0.20;  // extra lateral share of available accel


// -----------------
// Global state
// -----------------
enum MState { IDLE, SETTLE, ACTIVE, ARMED, SAFE }
MState _st = MState.IDLE;

enum ActPhase { Cruise, Guide }
ActPhase _phase; // set only inside state transitions

// Timekeeping
double _t = 0, _dt = 0;                // seconds since PB start, delta-time
//int    _tick10 = 0;                    // counts Update10 ticks since phase entry
double _phaseTime = 0;                // seconds since state/phase entry

// Block refs
List<IMyGyro> _gyros = new List<IMyGyro>();
List<IMyThrust> _thr = new List<IMyThrust>();
List<IMyBatteryBlock> _batts = new List<IMyBatteryBlock>();
IMyWarhead _wh1, _wh2; IMySensorBlock _sensor; IMyDecoy _decoy; IMyArtificialMassBlock _mass; IMyShipMergeBlock _merge;
IMyFunctionalBlock _aiOff, _aiFlt;     // AI providers; we only READ them
// Cached targeting from rack (preserve and reassert)
string _cfgTargetGroup = null, _cfgTargetPriority = null;

// Attitude hold reference (captured at SETTLE)
Vector3D _refFwd = Vector3D.Zero, _refUp = Vector3D.Zero;
Vector3D _omegaHoldWorld = Vector3D.Zero;
const double HOLD_KP = 6.0, HOLD_MAX_OMEGA = 0.35, HOLD_SMOOTH = 0.50, HOLD_DEADBAND_DEG = 1.5;

// LOS/PN working
Vector3D _tPos = Vector3D.Zero;        // current target position (world) — from AI feed
Vector3D _tVel = Vector3D.Zero;        // target velocity estimate — from AI feed diff
Vector3D _losDir = Vector3D.Zero, _losDirPrev = Vector3D.Zero;
double   _losAng = 999, _losAngPrev = 999, _psiDeg = 180;

// AI feed cache
Vector3D _wpPrev = Vector3D.Zero, _wpCurr = Vector3D.Zero;
bool _wpPrevValid = false, _wpCurrValid = false;
int  _lockConsec = 0;
bool _hasLock = false;
double _wpLastTime = -1;

// Housekeeping
Vector3D _lastPos = Vector3D.Zero; bool _haveLastPos = false;
Vector3D _vMisCached = Vector3D.Zero;
double   _accelAvail = 0.0;
double _gridMass = 0.0; // cached missile grid mass for thrust/mass accel
double _tl_range,_tl_vr,_tl_vc,_tl_losR,_tl_duty,_tl_aLat,_tl_psi,_tl_gyro,_tl_vlat;

// =========================
// Program entry / scheduler
// =========================
Program() {
    Runtime.UpdateFrequency = UpdateFrequency.Update1; // was 10
    FindBlocks();
    SafePrelaunchPosture();
    _lastPos = Me.GetPosition(); _haveLastPos = true;
    Echo("MISSILE PB — APN Roadmap Stencil v1.1");
}
void Save() {}

void Main(string arg, UpdateType ut) {
    _dt = Runtime.TimeSinceLastRun.TotalSeconds; if (_dt <= 0) _dt = 1.0/60.0; _t += _dt; _phaseTime += _dt;
    Vector3D __posNow = Me.GetPosition();
    double   __dt     = Math.Max(_dt, 1e-3);
    _vMisCached = _haveLastPos ? ((__posNow - _lastPos) / __dt) : Vector3D.Zero;
    _lastPos = __posNow; _haveLastPos = true;
    // === AIOFF ARG HOOK (safe) ===
if (!string.IsNullOrWhiteSpace(arg)) {
    string a = arg.Trim();
    // List available action IDs into PB Custom Data
    if (string.Equals(a, "LIST_AIOFF_ACTIONS", StringComparison.OrdinalIgnoreCase)) {
        var ai = FindAiOffenseOnThisGrid();
        if (ai == null) { Me.CustomData = "AIOFF: none found"; Echo("AIOFF list → CustomData (none)"); return; }
        var acts = new List<ITerminalAction>();
        ai.GetActions(acts, x => x != null);
        var sb = new StringBuilder(2048);
        sb.AppendLine("[AIOFF ACTIONS]");
        for (int i = 0; i < acts.Count; i++) {
            var id = acts[i].Id;
            if (id.StartsWith("SetTargetingGroup_", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("SetTargetPriority_", StringComparison.OrdinalIgnoreCase)) {
                sb.AppendLine(id);
            }
        }
        Me.CustomData = sb.ToString();
        Echo("AIOFF list → CustomData (" + acts.Count + ")");
        return;
    }
    Echo("ARG:" + a);

    // 1) List available AI Offensive actions
    if (string.Equals(a, "LIST_AIOFF_ACTIONS", StringComparison.OrdinalIgnoreCase)) {
        var ai = FindAiOffenseOnThisGrid();
        if (ai == null) { Echo("AIOFF: none found"); return; }
        var acts = new List<ITerminalAction>();
        ai.GetActions(acts, x => x != null);
        var sb = new StringBuilder();
        sb.AppendLine("AIOFF Actions:");
        for (int i = 0; i < acts.Count; i++) {
            var id = acts[i].Id;
            if (id.StartsWith("SetTargetingGroup_", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("SetTargetPriority_", StringComparison.OrdinalIgnoreCase)) {
                sb.AppendLine(id);
            }
        }
        Echo(sb.ToString());
        return;
    }

    // 2) Apply a specific targeting/priority action ID
    bool aiAction =
        a.StartsWith("SetTargetingGroup_", StringComparison.OrdinalIgnoreCase) ||
        a.StartsWith("SetTargetPriority_", StringComparison.OrdinalIgnoreCase);

    if (aiAction) {
        var ai = FindAiOffenseOnThisGrid();
        if (ai != null) { ai.ApplyAction(a); Echo("AIOFF:" + a + ":APPLIED"); }
        else            { Echo("AIOFF:" + a + ":NO AI-OFF"); }
        return; // handled
    }

    // Fall back to normal arg handling
    HandleArg(a);
}
    Tick();

    // ======================
    // ECHO STATUS
    // ======================
    string phaseText = "";
    if (_st == MState.ACTIVE)
        phaseText = $" Phase:{_phase} lock:{(_hasLock?1:0)}";
    double wpAge = (_wpLastTime >= 0) ? Math.Max(0, _t - _wpLastTime) : -1;
    Echo($"State:{_st}{phaseText} t:{_t:0.0} ψ:{_psiDeg:0.0} LOS:{_losAng:0.0} wp:{wpAge:0.0}s");
    Echo($"R:{_tl_range:0} VR:{_tl_vr:0.0} VC:{_tl_vc:0.0} LSR:{_tl_losR:0.00} PSI:{_tl_psi:0.0}");
    Echo($"Aav:{_accelAvail:0.0} AL:{_tl_aLat:0.0} Duty:{_tl_duty:0.00} Gy:{_tl_gyro:0} VLat:{_tl_vlat:0.0}");
}

// -----------------
// Argument handler
// -----------------
void HandleArg(string a){
    if (a.Equals("LAUNCH_START", StringComparison.OrdinalIgnoreCase)) {
        var ini = new MyIni(); MyIniParseResult res;
        ini.TryParse(Me.CustomData, out res);
        ini.Set("Diag","LaunchStartSeen","true");
        Me.CustomData = ini.ToString();
        BeginSettle();
        return;
    }
    if (a.Equals("SAFE",          StringComparison.OrdinalIgnoreCase)) { EnterSafe();   return; }
}

// =====================
// Chronological control
// =====================
void Tick(){
    switch (_st){
        case MState.IDLE:
            break; // waiting for LAUNCH_START

        case MState.SETTLE:
        {
            // STEP 1 — SETTLE: Attitude hold; thruster OFF
            ApplyGyroHold(true);
            SetThrustOverride(0f);
            if (_phaseTime >= LoadFloatOrDefault(K_LaunchSettle, D_LaunchSettle))
                ActivateSystems();
            break;
        }

        case MState.ACTIVE:
            if (_phase == ActPhase.Cruise){ Step_CruisePreLock(); break; }
            if (_phase == ActPhase.Guide){  Step_GuidePostLock(); break; }
            break;

        case MState.ARMED:
        case MState.SAFE:
            ApplyThrustDuty(0.0); ApplyGyroPN(false); ApplyGyroHold(false);
            break;
    }
}

// ======================
// ACTIVE / CRUISE — pre-lock
// ======================
void Step_CruisePreLock(){
    // Prevent AI Flight from injecting torque during Cruise
    if (_aiFlt != null) _aiFlt.Enabled = false;
    // Cruise warmup delay to allow clean detach (seconds)
    if (_phaseTime < CRUISE_WARMUP_SEC)
    {
        if (_mass != null) _mass.Enabled = false;
        ApplyThrustDuty(0.0);
        ApplyGyroPN(false);
        ApplyGyroHold(true);
        return;
    }

    // Bang-bang speed control around CRUISE_SET_MS ± CRUISE_BAND_MS
    double v = _vMisCached.Length();
    double vLo = CRUISE_SET_MS - CRUISE_BAND_MS;
    double vHi = CRUISE_SET_MS + CRUISE_BAND_MS;
    if (v <= vLo)       ApplyThrustDuty(1.0);
    else if (v >= vHi)  ApplyThrustDuty(0.0);
    else                ApplyThrustDuty(0.25);

    // Attitude hold only; no PN in Cruise
    ApplyGyroPN(false);
    ApplyGyroHold(true);

    // TODO[P2]: AI lock detection — Read AI Flight CurrentWaypoint; require 2 consecutive valid non-zero samples.
    //           On first valid pair AND after warmup+guide delay, set _phase=ActPhase.Guide.
    if (Placeholder_HasAiLock() && _phaseTime >= (CRUISE_WARMUP_SEC + GUIDEDELAY_SEC)){
    _phase = ActPhase.Guide;
    _phaseTime = 0; // reset for Guide
    _losDirPrev = _losDir = ComputeThrustAxisWorld();

    // Reassert user-selected AI-Offense targeting once at Guide start
    var tbO = _aiOff as IMyTerminalBlock;
    if (tbO != null && _cfgTargetGroup != null){
        ApplyAiOffenseTargetGroup(tbO,    _cfgTargetGroup);
        ApplyAiOffenseTargetPriority(tbO, _cfgTargetPriority ?? "Largest");
    }
    return;
}

    // TODO[P1]: Cruise timeout — If time_since_activate ≥ 12 s with no lock, doctrine TBD (SAFE or keep Cruise).
}

// --------------------------------------
// STEP 3 — GUIDE (post-lock, APN on AI)
// --------------------------------------
void Step_GuidePostLock(){
    // Lock feed must be valid per tick
    bool ok = Placeholder_ReadAiTarget(out _tPos, out _tVel);
    if (!ok){ ApplyThrustDuty(0.0); ApplyGyroPN(false); ApplyGyroHold(true); return; }

    // LOS + rates
    UpdateLOSFromTarget(_tPos);

    // Velocity, LOS, ψ (telemetry only)
    Vector3D v = _vMisCached; double sp = v.Length();
    Vector3D u = _losDir;
    double vdot = (sp > 1e-3) ? (v / sp).Dot(u) : -1.0;
    _psiDeg = Math.Acos(MathHelper.Clamp(vdot, -1.0, 1.0)) * 57.2957795;
    _tl_psi = _psiDeg; //telemetry
    // Gyro control (RDAV-style)
    ApplyGyroHold(false);
    ApplyGyroPN(true);

    // Desired accel 'a' (same mix as in ApplyGyroPN)
    Vector3D vhat = (sp > 1e-3) ? v / sp : ComputeThrustAxisWorld();
    Vector3D lat  = Vector3D.Cross(Vector3D.Cross(vhat, u), vhat);
    Vector3D vlat = v - u * v.Dot(u);
    Vector3D cmd  = (A_LAT * lat) + (A_FWD * u);
    double vlat2  = vlat.LengthSquared();
    _tl_vlat = (vlat2 > 0.0) ? Math.Sqrt(vlat2) : 0.0; //telemetry
    if (vlat2 > 1e-6) cmd += (-A_DAMP) * (vlat / Math.Sqrt(vlat2));
    double c2     = cmd.LengthSquared();
    Vector3D a    = (c2 > 1e-10) ? (cmd / Math.Sqrt(c2)) : u;

    // Align to real thrust axis and keep thrust on (RDAV-style clamp)
    // Vector3D thrustAxisNow = ComputeThrustAxisWorld(); // Potential Duplicate function unused
    Vector3D thrustAxis = -ComputeThrustAxisWorld();
    double cosA = MathHelper.Clamp(thrustAxis.Dot(a), -1.0, 1.0);

    // Duty: 0.5..1.0 by alignment; never 0 in GUIDE
    double duty = 0.5 + 0.5 * Math.Max(0.0, cosA);
    if (Me.CubeGrid.GridSizeEnum == MyCubeSize.Large) duty = 1.0;

    ApplyThrustDuty(duty);
    _tl_duty = duty; //telemetry
    // _vectoring no longer used in GUIDE

    // Terminal / fuse
    double range = Vector3D.Distance(_tPos, Me.CubeGrid.WorldVolume.Center);
    _tl_range = range; //telemetry
    // Backup contact trip: arm then detonate on sensor hit
    if (_sensor != null && _sensor.IsActive) { ArmWarheads(true); Detonate(); }
    if (range < LoadFloatOrDefault(K_FuseStandoff, D_FuseStandoff) + 20.0) ArmWarheads(true);
    if (range < LoadFloatOrDefault(K_FuseStandoff, D_FuseStandoff)) Detonate();
}

// =============================
// Phase transitions / lifecycle
// =============================
// ======================
// SETTLE — entry
// ======================

void BeginSettle(){
    // Hold the NOSE direction as the reference to avoid 180° flip in Cruise
    Vector3D noseRef = -ComputeThrustAxisWorld();             // nose = -thrust
    Vector3D u0      = Me.WorldMatrix.Up;                     // seed "up"
    Vector3D uRef    = u0 - noseRef * Vector3D.Dot(u0, noseRef); // project Up onto plane ⟂ nose
    if (uRef.LengthSquared() < 1e-6)
        uRef = Vector3D.Normalize(Vector3D.Cross(Me.WorldMatrix.Right, noseRef));
    else
        uRef = Vector3D.Normalize(uRef);
    _refFwd = Vector3D.Normalize(noseRef);
    _refUp  = uRef;
    // Gyros on for attitude hold during SETTLE
    for (int i = 0; i < _gyros.Count; i++) { _gyros[i].Enabled = true; _gyros[i].GyroPower = 1f; }
    // Ensure AI Flight cannot steer during SETTLE
    if (_aiFlt != null) _aiFlt.Enabled = false;

    SetThrustOverride(0f);
    _st = MState.SETTLE; _phaseTime = 0;
}
// ======================
// ACTIVATION
// ======================
void ActivateSystems(){
    ConfigureForLaunch();
    _phase = ActPhase.Cruise;
    _phaseTime = 0;
    _losDirPrev = _losDir = ComputeThrustAxisWorld();
    _st = MState.ACTIVE;
}
void EnterSafe(){ ApplyThrustDuty(0.0); ArmWarheads(false); _st = MState.SAFE; }

// ======================
// AUTO-CONFIG (launch-time)
// ======================
void ConfigureForLaunch(){
    FindBlocks(); // refresh references; some blocks were welded after Program()
    // Resolve switches from CustomData (or defaults)
    string profile = LoadStringOrDefault(K_ProfileIntent,  CFG_PROFILE_INTENT);
    bool   tgtNeu  = LoadBoolOrDefault  (K_TargetNeutrals, CFG_TARGET_NEUTRALS);
    bool   useDec  = LoadBoolOrDefault  (K_EnableDecoy,    CFG_ENABLE_DECOY_AT_LAUNCH);

    // AI Flight: RDAV-style feed only (power OFF, behavior ON, limits set)
    var tbF = _aiFlt as IMyTerminalBlock;
    if (tbF != null){
    var fbF = _aiFlt as IMyFunctionalBlock; if (fbF != null) fbF.Enabled = false; // power OFF
    var act = tbF.GetActionWithName("ActivateBehavior_On"); if (act != null) act.Apply(tbF);

    // Disable interference
    act = tbF.GetActionWithName("CollisionAvoidance_Off"); if (act != null) act.Apply(tbF); else TrySetBool(tbF, "CollisionAvoidance", false);
    act = tbF.GetActionWithName("DockingMode_Off");        if (act != null) act.Apply(tbF); else TrySetBool(tbF, "DockingMode", false);
    act = tbF.GetActionWithName("AlignToGravity_Off");     if (act != null) act.Apply(tbF); else TrySetBool(tbF, "AlignToGravity", false);

    // RDAV limits
    TrySetFloat(tbF, "MinHeightAboveTerrain", 10f);
    // Set very high; terminal clamps to slider max (~100 m/s). Not exposed to user.
    TrySetFloat(tbF, "SpeedLimit", 1000000f);
    }
    // AI Offensive: RDAV-style targeting + feed stability
    var tbO = _aiOff as IMyTerminalBlock;
    if (tbO != null){
    var fbO = _aiOff as IMyFunctionalBlock; if (fbO != null) fbO.Enabled = true;

    // Behavior ON keeps target updates alive
    var act = tbO.GetActionWithName("ActivateBehavior_On"); if (act != null) act.Apply(tbO);

    // Backup contact sensor setup (primary detonation trip)
    if (_sensor != null) {
    _sensor.Enabled = true;
    _sensor.LeftExtend = _sensor.RightExtend = _sensor.TopExtend =
        _sensor.BottomExtend = _sensor.BackExtend = 0f;

    float sfront = LoadFloatOrDefault("SensorFront", 3f); // from [Config], rack sets it
    if (sfront < 2f) sfront = 2f; else if (sfront > 12f) sfront = 12f;
    _sensor.FrontExtend = sfront;

    // Detection flags: neutrals follow toggle; everything else strictly filtered
    _sensor.DetectEnemy = true;
    _sensor.DetectNeutral = tgtNeu;
    _sensor.DetectFriendly = false;
    _sensor.DetectPlayers = false;
    _sensor.DetectFloatingObjects = false;
    _sensor.DetectAsteroids = false;
    _sensor.DetectSubgrids = false;

    // Ship/station classes on
    _sensor.DetectSmallShips = true;
    _sensor.DetectLargeShips = true;
    _sensor.DetectStations  = true;
    }

    // Targeting policy
    var tgtId = tgtNeu ? "SetAttackMode_EnemiesAndNeutrals" : "SetAttackMode_EnemiesOnly";
    act = tbO.GetActionWithName(tgtId); if (act != null) act.Apply(tbO);

    // Override collision avoidance inside the combat block
    TrySetBool(tbO, "OffensiveCombatIntercept_OverrideCollisionAvoidance", true);

    // RDAV cadence only (pattern is irrelevant when PB steers)
    TrySetFloat(tbO, "UpdateTargetInterval", 4f);   // ticks

    // Preferred subsystem group and priority from [Config] — cache for later reassert
    _cfgTargetGroup    = LoadStringOrDefault("TargetGroup",    "Weapons");
    _cfgTargetPriority = LoadStringOrDefault("TargetPriority", "Largest");
    if (string.Equals(_cfgTargetPriority, "Default", StringComparison.OrdinalIgnoreCase))
    _cfgTargetPriority = "Largest";
    ApplyAiOffenseTargetGroup(tbO,    _cfgTargetGroup);
    ApplyAiOffenseTargetPriority(tbO, _cfgTargetPriority);
}

    // Decoy per config
    if (_decoy != null) _decoy.Enabled = useDec;

    // Battery: force Auto so PB keeps power after separation
    if (_batts != null){
        for (int i = 0; i < _batts.Count; i++){
            var bt = _batts[i];
            if (bt != null) bt.ChargeMode = Sandbox.ModAPI.Ingame.ChargeMode.Auto;
            }
    }

    // Ordnance safe
    ArmWarheads(false);
}

Vector3D ComputeThrustAxisWorld()
{
    if (_thr == null || _thr.Count == 0) return Me.WorldMatrix.Forward;
    Vector3D sum = Vector3D.Zero;
    for (int i = 0; i < _thr.Count; i++)
    {
        var t = _thr[i];
        if (t == null || !t.IsFunctional) continue;
        // Push direction is WorldMatrix.Backward
        sum += t.WorldMatrix.Backward;
    }
    double n2 = sum.LengthSquared();
    return (n2 > 1e-6) ? Vector3D.Normalize(sum) : Me.WorldMatrix.Forward;
}

void TrySetBool(IMyTerminalBlock b, string name, bool v){
    if (b!=null && b.GetProperty(name)!=null) b.SetValueBool(name, v);
}


void TrySetFloat(IMyTerminalBlock b, string name, float v){
    if (b!=null && b.GetProperty(name)!=null) b.SetValueFloat(name, v);
}

void ApplyAiOffenseTargetGroup(IMyTerminalBlock b, string grp){
    if (b == null) return;
    string u = (grp ?? "Weapons").Trim().ToUpperInvariant();
string id = (u=="WEAPONS") ? "SetTargetingGroup_Weapons"
         : (u=="PROPULSION") ? "SetTargetingGroup_Propulsion"
         : (u=="POWER" || u=="POWERSYSTEMS" || u=="POWER SYSTEMS") ? "SetTargetingGroup_PowerSystems"
         : null;
        var a = (id!=null) ? b.GetActionWithName(id) : null;
        if (a != null) a.Apply(b);
}

void ApplyAiOffenseTargetPriority(IMyTerminalBlock b, string pr){
    if (b == null) return;
    string u = (pr ?? "Largest").Trim().ToUpperInvariant();
    string id = (u=="LARGEST")  ? "SetTargetPriority_Largest" :
                (u=="SMALLEST") ? "SetTargetPriority_Smallest" :
                (u=="NEAREST"||u=="CLOSEST") ? "SetTargetPriority_Closest" : null;
    var a = (id!=null) ? b.GetActionWithName(id) : null;
    if (a != null) a.Apply(b);
}

// ======================
// Guidance primitives
// ======================
void UpdateLOSFromTarget(Vector3D tgtPos){
    Vector3D m = Me.GetPosition();                 // precise grid origin
    Vector3D u = tgtPos - m; double d = u.Length(); if (d < 1e-3) d = 1e-3; u /= d;
    _losDirPrev = _losDir; _losDir = u;
    _losAngPrev = _losAng;
    _losAng = Math.Acos(MathHelper.Clamp(Me.WorldMatrix.Forward.Dot(u), -1.0, 1.0)) * 57.2957795;
}

// Gyro hold
void ApplyGyroHold(bool enabled){
    if (!enabled || _gyros.Count==0){
        if (_st==MState.SETTLE){
            for (int i=0;i<_gyros.Count;i++){ var g=_gyros[i]; g.GyroOverride=false; g.Pitch=g.Yaw=g.Roll=0f; }
            _omegaHoldWorld=Vector3D.Zero;
        }
        return;
    }
    Vector3D f = -ComputeThrustAxisWorld(), u=Me.WorldMatrix.Up; Vector3D fRef=Vector3D.Normalize(_refFwd), uRef=Vector3D.Normalize(_refUp);
    Vector3D axisF=Vector3D.Cross(f,fRef); double errF=Math.Acos(MathHelper.Clamp(f.Dot(fRef),-1,1));
    Vector3D axisU=Vector3D.Cross(u,uRef); double errU=Math.Acos(MathHelper.Clamp(u.Dot(uRef),-1,1));
    double errFDeg=errF*57.2957795, errUDeg=errU*57.2957795;
    if (errFDeg<HOLD_DEADBAND_DEG && errUDeg<HOLD_DEADBAND_DEG) _omegaHoldWorld=Vector3D.Zero; else {
        Vector3D omega=Vector3D.Zero; if (errF>1e-3) omega+=axisF*(HOLD_KP*errF); if (errU>1e-3) omega+=axisU*(0.1*HOLD_KP*errU);
        if (omega.LengthSquared()>HOLD_MAX_OMEGA*HOLD_MAX_OMEGA) omega=Vector3D.Normalize(omega)*HOLD_MAX_OMEGA;
        double a=Math.Exp(-_dt/Math.Max(1e-3,HOLD_SMOOTH)); _omegaHoldWorld=a*_omegaHoldWorld+(1-a)*omega;
    }
    for (int i=0;i<_gyros.Count;i++){
        var g=_gyros[i];
        Vector3D cmdLocal=Vector3D.TransformNormal(_omegaHoldWorld, MatrixD.Transpose(g.WorldMatrix));
        g.Enabled=true; g.GyroOverride=true; g.Pitch=(float)cmdLocal.X; g.Yaw=(float)cmdLocal.Y; g.Roll=(float)cmdLocal.Z;
    }
}

// ProNav (rate form)
// RDAV-style: align thrust axis (Backward) to composite desired-acceleration.
void ApplyGyroPN(bool enabled){
    if (!enabled || _gyros.Count==0){
        for (int i=0;i<_gyros.Count;i++){ var g=_gyros[i]; g.GyroOverride=false; g.Pitch=g.Yaw=g.Roll=0f; }
        return;
    }

    const double DAMPING_GAIN = 0.30;   // RDAV damping
    const double PN_GAIN      = 18.0;   // RDAV PNGain

    // Live axes and kinematics
    Vector3D thrustAxisNow = ComputeThrustAxisWorld();
    Vector3D u_old = _losDirPrev;
    Vector3D u_new = _losDir;
    Vector3D v_mis = _vMisCached;
    Vector3D v_tgt = _tVel;

    // update available-accel from thrust/mass (instantaneous) — no Physics access in PB
    if (_gridMass <= 0.0) {
        var _all = new List<IMyTerminalBlock>();
        GridTerminalSystem.GetBlocksOfType(_all, b => b.CubeGrid == Me.CubeGrid);
        double m = 0.0;
        for (int i = 0; i < _all.Count; i++) { m += _all[i].Mass; }
        _gridMass = (m > 1.0) ? m : 1.0; // avoid div-by-zero
    }
    double tsum = 0.0;
    for (int i = 0; i < _thr.Count; i++) {
        var t = _thr[i];
        if (t != null && t.IsFunctional) tsum += t.MaxEffectiveThrust;
    }
    _accelAvail = tsum / _gridMass;

    // LOS rate and closing speed
    double   dt           = Math.Max(_dt, 1e-3);
    Vector3D los_delta    = u_new - u_old;
    Vector3D los_rate_vec = los_delta / dt;
    // orthogonalize LOS-rate to current LOS (rate form PN needs perpendicular component)
    Vector3D los_rate_perp = los_rate_vec - u_new * Vector3D.Dot(los_rate_vec, u_new);
    double   los_rate      = los_rate_perp.Length();
    _tl_losR = los_rate; //telemetry
    Vector3D rel_vel   = v_tgt - v_mis;

    double   vclosing  = -Vector3D.Dot(rel_vel, u_new);    // clamp opening to zero
    if (vclosing < 0) vclosing = 0;
    Vector3D rel_dir   = (rel_vel.LengthSquared() > 1e-6) ? Vector3D.Normalize(rel_vel) : u_new;
    _tl_vr = rel_vel.Length(); //telemetry
    _tl_vc = Math.Max(0.0, -Vector3D.Dot(rel_vel, u_new)); //telemetry
    // RDAV lateral direction
    Vector3D a_dir = los_rate_perp;
    double a_dir_n2 = a_dir.LengthSquared();
    if (a_dir_n2 > 1e-12) a_dir /= Math.Sqrt(a_dir_n2);
    else a_dir = Vector3D.Zero;
    double vr = rel_vel.Length();
    double a_lat_gain = PN_GAIN * vr * los_rate;  // PN uses |v_rel| for bite at 90°
    Vector3D a_lat = a_dir * a_lat_gain;
    _tl_aLat = a_lat.Length(); //telemetry
// === Allocation using measured available accel ===
    double   a_lat_mag  = a_lat.Length();
Vector3D a_lat_unit = (a_lat_mag > 1e-12) ? (a_lat / a_lat_mag) : Vector3D.Zero;

// total accel (unchanged structure)
double   a_total    = _accelAvail;

double   a_lat_cap  = (a_lat_mag > 0.0 && a_total > 0.0) ? Math.Min(a_lat_mag, a_total) : 0.0;

// Weasel: increase lateral share inside orbit window
bool     weasel = LoadStringOrDefault(K_ProfileIntent,  CFG_PROFILE_INTENT).Equals("WILD WEASEL", StringComparison.OrdinalIgnoreCase);
double   range_orbit = Vector3D.Distance(_tPos, Me.CubeGrid.WorldVolume.Center);
if (weasel && range_orbit < ORBIT_ENTER_M && a_total > 0.0) {
    double extra = ORBIT_FRAC * a_total;
    a_lat_cap = Math.Min(a_total, a_lat_cap + extra);
}

double   a_fwd_mag  = (a_total > 0.0) ? Math.Sqrt(Math.Max(0.0, a_total*a_total - a_lat_cap*a_lat_cap)) : 0.0;

// Final desired-accel vector: capped lateral + forward toward LOS
Vector3D a_vec = a_lat_unit * a_lat_cap + u_new * a_fwd_mag;

// Over-demand drift cancel based on RELATIVE velocity
if (a_lat_mag > a_total) {
    Vector3D vrel = v_tgt - v_mis;
    double   vpar = Vector3D.Dot(vrel, u_new);
    Vector3D vlat = vrel - u_new * vpar;
    double   n2   = vlat.LengthSquared();
    if (n2 > 1e-6) {
        Vector3D dc = -vlat / Math.Sqrt(n2);
        double   over = Math.Min(a_lat_mag - a_total, a_total);
        a_vec += dc * over * DRIFT_CANCEL_GAIN;
    }
}

    // Normalize for steering
    double   a2 = a_vec.LengthSquared();
    Vector3D a  = (a2 > 1e-12) ? (a_vec / Math.Sqrt(a2)) : u_new;

    // Align thrust axis to 'a'
    double c = MathHelper.Clamp(thrustAxisNow.Dot(a), -1.0, 1.0);
    double ang = Math.Acos(c);
    Vector3D axis = Vector3D.Cross(thrustAxisNow, a);
    double ax2 = axis.LengthSquared();
    if (ax2 > 1e-12) axis /= Math.Sqrt(ax2); else axis = Vector3D.Zero;

    // World angular command (keep roll; RDAV-style)
    Vector3D w = axis * (ang * CTRL_GAIN);

    // Per-gyro local mapping with damping and RDAV sign
    _tl_gyro = 0.0; //telemetry
    for (int i=0;i<_gyros.Count;i++){
        var g=_gyros[i];
        Vector3D cmdLocal = -Vector3D.TransformNormal(w, MatrixD.Transpose(g.WorldMatrix));
        double p = (1.0 - DAMPING_GAIN) * cmdLocal.X + DAMPING_GAIN * g.Pitch;
        double y = (1.0 - DAMPING_GAIN) * cmdLocal.Y + DAMPING_GAIN * g.Yaw;
        double r = (1.0 - DAMPING_GAIN) * cmdLocal.Z + DAMPING_GAIN * g.Roll;
        _tl_gyro = Math.Max(_tl_gyro, Math.Max(Math.Max(Math.Abs(p), Math.Abs(y)), Math.Abs(r))); //telemetry
        // per-axis clamp like RDAV
        p = MathHelper.Clamp(p, -1000, 1000);
        y = MathHelper.Clamp(y, -1000, 1000);
        r = MathHelper.Clamp(r, -1000, 1000);

        g.Enabled = true; g.GyroOverride = true;
        g.Pitch = (float)p; g.Yaw = (float)y; g.Roll = (float)r;
    }
}

// ======================
// AI FEED — lock + target from AI Flight
// ======================
// Lock = two consecutive valid waypoints (no distance threshold).
bool Placeholder_HasAiLock(){
    UpdateAiWaypointCache();
    return _hasLock;
}
bool Placeholder_ReadAiTarget(out Vector3D pos, out Vector3D vel){
    UpdateAiWaypointCache();
    if (_wpCurrValid && _wpPrevValid){
        pos = _wpCurr;
        double dt = Math.Max(_dt, 1e-3);
        vel = (_wpCurr - _wpPrev) / dt;   // stationary target → ~Zero
        return true;
    }
    pos = _tPos; vel = _tVel;            // keep previous values for telemetry
    return false;
}

// One-pass cache and lock test; no allocations in tick
// One-pass cache and lock test; no allocations in tick
void UpdateAiWaypointCache(){
    var fmb = _aiFlt as Sandbox.ModAPI.Ingame.IMyFlightMovementBlock;
    if (fmb == null){ _wpCurrValid = false; _wpPrevValid = false; _lockConsec = 0; _hasLock = false; return; }

    var wpObj = fmb.CurrentWaypoint; // IMyAutopilotWaypoint
    if (wpObj == null){ _wpCurrValid = false; _wpPrevValid = false; _lockConsec = 0; _hasLock = false; return; }

    Vector3D wp = wpObj.Matrix.Translation; // waypoint world position
    if (!IsFinite(wp)){ _wpCurrValid = false; _wpPrevValid = false; _lockConsec = 0; _hasLock = false; return; }

    // Shift cache
    _wpPrev = _wpCurr; _wpPrevValid = _wpCurrValid;
    _wpCurr = wp;      _wpCurrValid = true; _wpLastTime = _t;

    // Lock rule: two consecutive valid samples, even if identical
    if (_wpPrevValid){
        if (_lockConsec < 2) _lockConsec++;
    } else {
        _lockConsec = 1; // first valid sample in a new run
    }
    _hasLock = (_lockConsec >= 2);
}

// Terminal property readers

bool IsFinite(Vector3D x){
    return !double.IsNaN(x.X) && !double.IsNaN(x.Y) && !double.IsNaN(x.Z)
        && !double.IsInfinity(x.X) && !double.IsInfinity(x.Y) && !double.IsInfinity(x.Z);
}

// ======================
// Utilities / blocks IO
// ======================
// ======================
// DISCOVERY — missile blocks by prefix/type with index auto-assign (C#6-safe)
// ======================
void FindBlocks()
{

    // Lists by prefix
    GridTerminalSystem.GetBlocksOfType(_gyros, g =>
        g.CubeGrid == Me.CubeGrid &&
        g.CustomName != null &&
        g.CustomName.IndexOf(NAME_GYRO_PRE, StringComparison.OrdinalIgnoreCase) >= 0);

    GridTerminalSystem.GetBlocksOfType(_thr, t =>
        t.CubeGrid == Me.CubeGrid &&
        t.CustomName != null &&
        t.CustomName.IndexOf(NAME_THR_PRE, StringComparison.OrdinalIgnoreCase) >= 0);

    GridTerminalSystem.GetBlocksOfType(_batts, b =>
        b.CubeGrid == Me.CubeGrid &&
        b.CustomName != null &&
        b.CustomName.IndexOf(NAME_BATT_PRE, StringComparison.OrdinalIgnoreCase) >= 0);

    // Canonicalize names for robustness across variants
    if (_thr != null && _thr.Count > 0)
    {
        var thrMap = new Dictionary<int, IMyThrust>();
        AssignTypeIndexed<IMyThrust>(_thr, "Thrust", thrMap);       // M-Thrust1..n
    }
    if (_gyros != null && _gyros.Count > 0)
    {
        var gyroMap = new Dictionary<int, IMyGyro>();
        AssignTypeIndexed<IMyGyro>(_gyros, "Gyro", gyroMap);        // M-Gyro1..n
    }
    if (_batts != null && _batts.Count > 0)
    {
        var battMap = new Dictionary<int, IMyBatteryBlock>();
        AssignTypeIndexed<IMyBatteryBlock>(_batts, "Batt", battMap);// M-Batt1..n
    }

    // Warhead(s)
    var war = new List<IMyWarhead>();
    GridTerminalSystem.GetBlocksOfType(war, w => w.CubeGrid == Me.CubeGrid && w.CustomName.StartsWith("M-", StringComparison.OrdinalIgnoreCase) && w.CustomName.IndexOf("Warhead", StringComparison.OrdinalIgnoreCase) >= 0);
    var mapWar = new Dictionary<int, IMyWarhead>();

    // Sensor(s)
    var sens = new List<IMySensorBlock>();
    GridTerminalSystem.GetBlocksOfType(sens, s =>
        s.CubeGrid == Me.CubeGrid &&
        s.CustomName != null &&
        s.CustomName.StartsWith("M-", StringComparison.OrdinalIgnoreCase));
    var mapSens = new Dictionary<int, IMySensorBlock>();
    AssignTypeIndexed<IMySensorBlock>(sens, "Sensor", mapSens); // M-Sensor1..n
    _sensor = mapSens.ContainsKey(1) ? mapSens[1] : null;

    // Thrusters: canonicalize names M-Thrust1..n for robustness across builds
    if (_thr != null && _thr.Count > 0)
    {
        var thrMap = new Dictionary<int, IMyThrust>();
        AssignTypeIndexed<IMyThrust>(_thr, "Thrust", thrMap);
    }
    AssignTypeIndexed<IMyWarhead>(war, "Warhead", mapWar);
    _wh1 = mapWar.ContainsKey(1) ? mapWar[1] : null;
    _wh2 = mapWar.ContainsKey(2) ? mapWar[2] : null;

    // Merge
    var merges = new List<IMyShipMergeBlock>();
    GridTerminalSystem.GetBlocksOfType(merges, m =>
    m.CubeGrid == Me.CubeGrid &&
    m.CustomName != null &&
    m.CustomName.StartsWith(NAME_MERGE, StringComparison.OrdinalIgnoreCase));
    _merge = merges.Count > 0 ? merges[0] : null;   // no renaming or indexing

    // Decoy
    var decs = new List<IMyDecoy>();
    GridTerminalSystem.GetBlocksOfType(decs, d => d.CubeGrid == Me.CubeGrid && d.CustomName.StartsWith("M-", StringComparison.OrdinalIgnoreCase) && d.CustomName.IndexOf("Decoy", StringComparison.OrdinalIgnoreCase) >= 0);
    var mapDec = new Dictionary<int, IMyDecoy>();
    AssignTypeIndexed<IMyDecoy>(decs, "Decoy", mapDec);
    _decoy = mapDec.ContainsKey(1) ? mapDec[1] : null;

    // Artificial Mass
    var masses = new List<IMyArtificialMassBlock>();
    GridTerminalSystem.GetBlocksOfType(masses, m => m.CubeGrid == Me.CubeGrid && m.CustomName.StartsWith("M-", StringComparison.OrdinalIgnoreCase) && m.CustomName.IndexOf("Mass", StringComparison.OrdinalIgnoreCase) >= 0);
    var mapMass = new Dictionary<int, IMyArtificialMassBlock>();
    AssignTypeIndexed<IMyArtificialMassBlock>(masses, "Mass", mapMass);
    _mass = mapMass.ContainsKey(1) ? mapMass[1] : null;

    // AI Offensive
    var aos = new List<IMyFunctionalBlock>();
    GridTerminalSystem.GetBlocksOfType(aos, f =>
    f.CubeGrid == Me.CubeGrid &&
    f.CustomName != null &&
    f.CustomName.StartsWith(NAME_AI_OFF, StringComparison.OrdinalIgnoreCase));
    _aiOff = aos.Count > 0 ? aos[0] : null;   // no renaming or indexing

    // AI Flight
    var afs = new List<IMyFunctionalBlock>();
    GridTerminalSystem.GetBlocksOfType(afs, f =>
    f.CubeGrid == Me.CubeGrid &&
    f.CustomName != null &&
    f.CustomName.StartsWith(NAME_AI_FLT, StringComparison.OrdinalIgnoreCase));
    _aiFlt = afs.Count > 0 ? afs[0] : null;   // no renaming or indexing

    // --- Naming enforcement: singular blocks use fixed, non-indexed names ---
    // Intent: prevent blueprint drift from breaking hotbar/scripts.
    EnsureCanonicalName(_aiOff as IMyTerminalBlock, NAME_AI_OFF);   // "M-AI-Offense"
    EnsureCanonicalName(_aiFlt as IMyTerminalBlock, NAME_AI_FLT);   // "M-AI-Flight"
    EnsureCanonicalName(_merge as IMyTerminalBlock, NAME_MERGE);    // "M-Merge"
    EnsureCanonicalName(Me as IMyTerminalBlock, "M-ProgBlock"); // PB has no constant
}

T GetExact<T>(string name) where T:class, IMyTerminalBlock{
    var list=new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType(list, b=>b.CustomName.Equals(name,StringComparison.OrdinalIgnoreCase));
    return list.Count>0 ? (T)list[0] : null;
}

// --- Naming enforcement helper ---
// Ensures singular blocks use our fixed non-indexed canonical names.
// Used to reduce drift from blueprint cloning or manual edits.
void EnsureCanonicalName(IMyTerminalBlock b, string canonical){
    if (b == null) return;

    // Do NOT rename these singulars: honor blueprint names
    if (canonical == NAME_AI_OFF) return;           // e.g., "M-AI-Offense"
    if (canonical == NAME_AI_FLT) return;           // e.g., "M-AI-Flight"
    if (canonical == NAME_MERGE) return;            // e.g., "M-Merge"
    if (canonical == "M-ProgBlock") return;         // missile PB

    // Other cases may still enforce canonical naming
    if (b.CustomName != canonical) b.CustomName = canonical;
}

// ======================
// HELPERS — index parsing and canonical rename (C#6-safe)
// ======================

IMyTerminalBlock FindAiOffenseOnThisGrid() {
    var list = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType(list, b =>
        b != null &&
        b.CubeGrid == Me.CubeGrid &&
        b.CustomName != null &&
        b.CustomName.StartsWith("M-AI-Offense", StringComparison.OrdinalIgnoreCase));
    return list.Count > 0 ? list[0] : null;
}

void AssignTypeIndexed<T>(List<T> src, string typeToken, Dictionary<int,T> outMap) where T: class, IMyTerminalBlock {
    if (src==null || src.Count==0) return;

    // Stable order: name asc, then EntityId
    src.Sort(delegate(T a, T b){
        int c = string.Compare(a.CustomName, b.CustomName, StringComparison.OrdinalIgnoreCase);
        if (c!=0) return c;
        long ea = (a as IMyEntity).EntityId, eb = (b as IMyEntity).EntityId;
        if (ea<eb) return -1; if (ea>eb) return 1; return 0;
    });

    int[] used = new int[128];
    int count = src.Count;
    int i;
    int[] assigned = new int[count];

    // First pass: keep valid explicit indices
    for (i=0;i<count;i++){
        int idx,s,e; bool has = TryParseIndex(src[i].CustomName, typeToken, out idx, out s, out e);
        if (has && idx>=1 && idx<used.Length && used[idx]==0){ assigned[i]=idx; used[idx]=1; }
        else assigned[i]=0;
    }
    // Second pass: assign remaining indices consecutively
    for (i=0;i<count;i++){
        if (assigned[i]==0){ assigned[i] = NextFreeIndex(used, 1); }
    }
    // Apply canonical names and fill map
    for (i=0;i<count;i++){
        T b = src[i];
        int idx = assigned[i];
        string canonical = "M-" + typeToken + idx.ToString();
        b.CustomName = canonical;
        if (!outMap.ContainsKey(idx)) outMap[idx] = b;
    }
}

int NextFreeIndex(int[] used, int startAt){
    int k = startAt; if (k<1) k=1;
    while (k < used.Length && used[k]!=0) k++;
    if (k >= used.Length) k = used.Length-1;
    used[k]=1;
    return k;
}

bool TryParseIndex(string name, string typeToken, out int index, out int numStart, out int numEnd){
    index=0; numStart=-1; numEnd=-1;
    if (string.IsNullOrEmpty(name)) return false;
    int tpos = name.IndexOf(typeToken, StringComparison.OrdinalIgnoreCase);
    if (tpos < 0) return false;

    int i = tpos + typeToken.Length;
    while (i < name.Length && !char.IsDigit(name[i])) i++;
    int j = i; while (j < name.Length && char.IsDigit(name[j])) j++;
    if (j > i){
        int val; if (int.TryParse(name.Substring(i, j-i), out val)){ index=val; numStart=i; numEnd=j; return true; }
    }
    // Fallback: last integer anywhere
    for (int k=name.Length-1; k>=0; k--){
        if (char.IsDigit(name[k])){
            int end=k+1; while (k>=0 && char.IsDigit(name[k])) k--; int start=k+1;
            int val; if (int.TryParse(name.Substring(start, end-start), out val)){ index=val; numStart=start; numEnd=end; return true; }
            break;
        }
    }
    return false;
}

// Prelaunch posture (safe defaults)
void SafePrelaunchPosture(){
    if (_mass!=null) _mass.Enabled=true;
    ArmWarheads(false);
    // Keep thrusters OFF pre-release; avoid plume while pistons move.
    for (int i=0;i<_thr.Count;i++){ var t=_thr[i]; if (t!=null){ t.Enabled=false; t.ThrustOverridePercentage=0f; } }
    // sensor comment stale; configured later in ConfigureForLaunch (if present)
}

// Thrust helpers
void EnableAllThrusters(){ for (int i=0;i<_thr.Count;i++) _thr[i].Enabled=true; }
void SetThrustOverride(float pct){ pct=MathHelper.Clamp(pct,0f,1f); for (int i=0;i<_thr.Count;i++) _thr[i].ThrustOverridePercentage=pct; }
void ApplyThrustDuty(double duty){ float pct=(float)MathHelper.Clamp(duty,0.0,1.0); for (int i=0;i<_thr.Count;i++){ var t=_thr[i]; t.Enabled=true; t.ThrustOverridePercentage=pct; } }

// Fuse helpers
void ArmWarheads(bool armed){ if (_wh1!=null) _wh1.IsArmed=armed; if (_wh2!=null) _wh2.IsArmed=armed; }
void Detonate(){ if (_wh1!=null && _wh1.IsArmed) _wh1.Detonate(); if (_wh2!=null && _wh2.IsArmed) _wh2.Detonate(); }

// Math/metrics

// (helpers removed: GetMissileVelocity, SecondsToTicks)

// Config I/O
float  LoadFloatOrDefault (string key, float  defVal){ var ini=new MyIni(); MyIniParseResult r; ini.TryParse(Me.CustomData, out r); return (float)ini.Get(SEC_CFG, key).ToSingle(defVal); }
bool   LoadBoolOrDefault  (string key, bool   defVal){ var ini=new MyIni(); MyIniParseResult r; ini.TryParse(Me.CustomData, out r); return ini.Get(SEC_CFG, key).ToBoolean(defVal); }
string LoadStringOrDefault(string key, string defVal){ var ini=new MyIni(); MyIniParseResult r; ini.TryParse(Me.CustomData, out r); var v=ini.Get(SEC_CFG, key).ToString(); return string.IsNullOrEmpty(v)?defVal:v; }

/*
R — range to target
Units: m. Formula: |p_tgt − p_mis|.
Healthy: falls steadily to 0.
Red flag: stalls or grows while PSI > 0° → no closure.

VR — relative speed magnitude
Units: m/s. Formula: |v_rel|.
Healthy: non-zero in flight.
Red flag: 0 while moving → velocity feed broken.

VC — closing speed along LOS
Units: m/s. Formula: max(0, −v_rel·u_LOS).
Healthy: rises from ~0 at big off-angle, then stays >0.
Red flag: stays 0 while PSI is large → lateral authority or direction bug.

LSR — LOS rotation rate
Units: deg/s. Formula: |(Δu_LOS/Δt)_⊥| converted to deg/s.
Healthy: high early, trends to ~0 near impact.
Red flag: ~0 while PSI is large → LOS-rate sampling/timing wrong.

PSI — velocity-to-LOS angle
Units: deg. Formula: acos( v̂·u_LOS ).
Healthy: decays quickly toward 0°.
Red flag: flat or growing → lateral demand/cap or sign inversion issue.

Aav — available acceleration
Units: m/s². Formula: Σ MaxEffectiveThrust / mass.
Healthy: stable, plausible (e.g., 3–20 m/s² for small missiles).
Red flag: unrealistically high/low → bad mass or thruster list.

AL — PN lateral demand magnitude (pre-allocation)
Units: m/s². From PN: PN_GAIN * |v_rel| * LSR.
Healthy: sizable early, then declines.
Red flag: near zero while LSR is non-zero → PN magnitude or direction wrong.

Duty — thrust duty
Units: 0.50–1.00 (fraction).
Healthy: ~1.00 during chase, may modulate late.
Red flag: pinned low with big PSI/LSR → throttle logic fighting guidance.

Gy — max |gyro command| this tick
Units: arbitrary, clamped ±1000.
Healthy: >0 when turning; trends to 0 near impact.
Red flag: 0 while PSI/LSR high → allocator starved or wrong w vector; 1000 for many ticks → gain/sign issues.

VLat — lateral component of relative velocity
Units: m/s. Formula: |v_rel − u_LOS (v_rel·u_LOS)|.
Healthy: decreases over time.
Red flag: flat/high while Gy>0 and AL>0 → drift-cancel not engaging or lateral cap too tight.

Quick reads
VR=0 & VC=0 with movement → fix missile velocity feed.
LSR≈0 & PSI large → LOS-rate computation/timing bug.
AL>0 & Gy=0 → lateral cap/starvation or wrong accel→gyro mapping.
VLat not dropping while AL,Gy>0 → check drift-cancel trigger and lateral cap vs total accel.
Aav nonsense → mass/thrust estimator wrong, breaks allocation everywhere.

Quick checks:

2000 m: LSR 0.2–2.0, PSI large, VC ~0→small+, AL ~30–80% of Aav, Gy > 0.
1200 m: LSR falling, PSI about half start, VC > 10 m/s, VLat decreasing.
600 m: LSR < 0.3, PSI < 10°, VC 15–40 m/s, AL trending down.
200 m: LSR < 0.1, PSI < 2°, VC ≈ VR, AL → 0, Gy → 0.

*/