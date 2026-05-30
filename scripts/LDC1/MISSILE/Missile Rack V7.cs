// ==========================================
// LIGHTNING MISSILE RACK CONTROLLER — V17
// Prefix+Type discovery (no exact names). Pistons resolved by digits 1/2.
// DIAG + SPLIT LCD + EXPLICIT BUILD ARM preserved.
// ==========================================
// Ops Args: PREP | FIRE L | FIRE R | DISARM | STATUS | BUILD L | BUILD R | BUILD ALL | BUILD OFF
// Diag Args per rack: STEP <L|R> START|NEXT|PREV|GOTO n|EXIT|AUTO ON|AUTO OFF|RESET
// Alert groups: "Lightning Alerts", "Lightning Alert Light"
// Missile is fully pre-configured in its blueprint; rack does not modify missile blocks.
// ==========================================

// ===== PROJECTOR ORIENTATION =====
// Rotation values are in quarter-turns (90° steps). (Pitch,Yaw,Roll)
// Offset values are in block counts. (Horiz,Vert,Forward)
static readonly Vector3I PROJ_ROT  = new Vector3I(0, 2, 0);  // 0°,180°,0°
static readonly Vector3I PROJ_OFF  = new Vector3I(0, 0, 2);  // H=0,V=0,F=+2

// ===== MOTION SPEEDS =====
const float PISTON_SPEED = 0.3f;   // piston extend/retract velocity

const float DROP_DEBOUNCE_SECONDS = 0.2f;
const float STALL_SECONDS = 20.0f;
const int   N_CLEAR_TICKS = 10;

const string LCD_GROUP_L = "Lightning Rack Status L";
const string LCD_GROUP_R = "Lightning Rack Status R";
const string ALERT_LCD_GROUP  = "Lightning Alerts";
const string ALERT_LIGHT_GRP  = "Lightning Alert Light";

// Right rack piston travel
const float BUILD1_POS_R  = 0.4f;
const float LAUNCH1_POS_R = 2.0f;
const float BUILD2_POS_R  = 0.0f;
const float LAUNCH2_POS_R = 2.0f;

// Left rack piston travel
const float BUILD1_POS_L  = 0.4f;
const float LAUNCH1_POS_L = 2.0f;
const float BUILD2_POS_L  = 0.0f;
const float LAUNCH2_POS_L = 2.0f;

const float PISTON_EPS = 0.10f;
// Bay proximity radius squared (4 m)^2 for in-bay missile selection
const double BAY_R2 = 16.0;

const UpdateFrequency TICK_RATE = UpdateFrequency.Update10; // ~6 Hz
const double TICK_SECONDS = 1.0 / 6.0;

// ===== MISSILE SIMPLE PROFILE CONSTANTS =====
const float  NOSE_STANDOFF_M   = 5.0f;   // nose fuse distance ahead of missile tip

enum RackState { BUILDING, PRIMED, EJECTING, RELOADING, DISARMED }
enum FaultType { NONE, STALL, MATERIALS_LOW, STUCK, DOOR_FAIL, PISTON_MISSING, PISTON_DUP }
enum DoorGoal  { Open, Close }

class Diag {
    public bool Active = false;
    public int Step = 0;              // 0..14
    public bool Auto = false;
    public double Clock = 0.0;
    public int LastRem = -1;
    public string Why = "";
    public string Status = "IDLE";    // WAIT|PASS|FAIL|IDLE
}

class Rack {
    public string Tag;    // "L" or "R"
    public string Prefix; // "LR-L " or "LR-R "
    public RackState State = RackState.DISARMED;
    public FaultType Fault = FaultType.NONE;

    public List<IMyAirtightHangarDoor> Doors = new List<IMyAirtightHangarDoor>();
    public IMyShipMergeBlock Merge;
    public IMyProjector Projector;
    public IMyPistonBase Piston1, Piston2;
    public List<IMySensorBlock> Sensors = new List<IMySensorBlock>();
    public List<IMyShipWelder> Welders = new List<IMyShipWelder>();
    public List<IMyTextPanel> Lcds = new List<IMyTextPanel>();

    public DoorGoal Goal = DoorGoal.Close;

    public double StallClock = 0.0;
    public double DebounceClock = 0.0;
    public int    ClearTicks = 0;
    public int    LastRemaining = -1;

    public bool BuildArmed = false;   // explicit build arm
    public Diag Diag = new Diag();
}

class RackObs {
    public bool Merge, Bay;
    public int Remaining;             // -1 if no projector
    public bool Projecting;
    public bool AllWeldersOn;
    public bool AtBuild, AtLaunch;
    public string Doors;              // OPEN/CLOSED/FAULT
}

// === ADDED: read-only missile assessment container
class MissileStatus {
    public bool FoundMissileGrid;
    public int BatteryCount;
    public int ThrusterCount;
    public bool HasNoseSensor;
    public bool HasMassBlock;
    public bool HasArmTimer;
    public string BatteryModes; // "AUTO:x, RECHARGE:y, DISCHARGE:z"
}

// ===== Missile fuse/sensor presets (edit here) =====
const float PRESET_STANDOFF_SMALL = 2.0f;  // m (center-distance fallback)
const float PRESET_STANDOFF_LARGE = 7.5f;  // m
const float PRESET_SENSOR_IMPACT  = 3.0f;  // m (contact-ish)
const float PRESET_SENSOR_BURST   = 9.0f;  // m (pre-contact)

// ===== Missile profile toggles (defaults on new/STATUS) =====
const string DEF_GridSize = "SMALL GRID";   // or "LARGE GRID"
const string DEF_FuseMode = "IMPACT";       // or "BURST"

List<Rack> Racks = new List<Rack>();
List<IMyTextPanel> AlertLcds = new List<IMyTextPanel>();
List<IMyTextPanel> HelmLcds  = new List<IMyTextPanel>();
List<IMyLightingBlock> AlertLights = new List<IMyLightingBlock>();
bool Disarmed = false;

Program() {
    Runtime.UpdateFrequency = TICK_RATE;
    DiscoverAll();
    AutoConfigureSensors();
    RecoverStates();
    EnsureProfileDefaults();
    Echo("INIT complete");
}

void DiscoverAll() {
    Racks.Clear();
    string[] prefixes = { "LR-L ", "LR-R " };

    for (int i = 0; i < prefixes.Length; i++) {
        string p = prefixes[i];
        var r = new Rack();
        r.Prefix = p;
        r.Tag = p.IndexOf("LR-L") >= 0 ? "L" : "R";

        // Doors via group to preserve builder intent
        var groups = new List<IMyBlockGroup>();
        GridTerminalSystem.GetBlockGroups(groups, g => g.Name == p + "DoorGrp");
        if (groups.Count > 0) groups[0].GetBlocksOfType(r.Doors);

        // Type+prefix discovery on same construct
        r.Merge     = FindOneByPrefix<IMyShipMergeBlock>(p);
        r.Projector = FindOneByPrefix<IMyProjector>(p);
        ResolvePistonsByDigit(r); // sets Piston1 / Piston2 or faults
        r.Sensors   = FindAllByPrefix<IMySensorBlock>(p);
        r.Welders   = FindAllByPrefix<IMyShipWelder>(p);

        Racks.Add(r);
    }

    // Per-rack status LCDs by prefix (e.g., "LR-L...", "LR-R...")
    for (int i = 0; i < Racks.Count; i++) {
        Racks[i].Lcds = FindAllByPrefix<IMyTextPanel>(Racks[i].Prefix);
    }

    // Alert LCDs: global “LR-Alerts...” prefix
    AlertLcds = FindAllByPrefix<IMyTextPanel>("LR-Alerts");

    // Helm LCDs: global “LR-Helm...” prefix
    HelmLcds  = FindAllByPrefix<IMyTextPanel>("LR-Helm");

    // Keep alert lights as-is for now (grouped) until we migrate them
    AlertLights = FindAll<IMyLightingBlock>(l => InGroup(l, ALERT_LIGHT_GRP));
}

void AutoConfigureSensors() {
    for (int i = 0; i < Racks.Count; i++) {
        var r = Racks[i];
        for (int j = 0; j < r.Sensors.Count; j++) {
            var s = r.Sensors[j];
            if (!s.IsSameConstructAs(Me)) continue;
            s.FrontExtend  = 6.0f; s.BackExtend = 0.1f; s.LeftExtend = 0.1f;
            s.RightExtend  = 0.1f; s.TopExtend  = 0.1f; s.BottomExtend = 0.1f;
            s.DetectSubgrids = true;
            s.DetectSmallShips = true; s.DetectLargeShips = false; s.DetectStations = false;
            s.DetectEnemy = false; s.DetectNeutral = false; s.DetectFriendly = false;
            s.DetectPlayers = false; s.DetectFloatingObjects = false; s.DetectAsteroids = false;
        }
        if (r.Projector != null) {
            r.Projector.ShowOnlyBuildable = false;
            var tbp = r.Projector as IMyTerminalBlock;
            if (tbp != null) tbp.SetValueBool("KeepProjection", true);

            if (r.Projector.ProjectionRotation != PROJ_ROT) r.Projector.ProjectionRotation = PROJ_ROT;
            if (r.Projector.ProjectionOffset   != PROJ_OFF) r.Projector.ProjectionOffset   = PROJ_OFF;
        }
    }
}

// Prefix+type finder for missile parts (no exact names, suffix tolerant).
T FindOnMissileByToken<T>(string token) where T : class, IMyTerminalBlock {
    string prefix = "M-" + token;
    var list = new List<T>();
    GridTerminalSystem.GetBlocksOfType(list, b =>
        b != null &&
        b.IsSameConstructAs(Me) &&
        b.CustomName != null &&
        b.CustomName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    return list.Count > 0 ? list[0] : null;
}

// removed: use FindMissileGrid(Rack r)

List<IMyThrust> FindMissileThrusters(Rack r)
{
    var thr = new List<IMyThrust>();
    GridTerminalSystem.GetBlocksOfType(thr, t => InBay(t, r)); // M- prefix enforced by InBay
    return thr;
}



void SetMissileMassEnabled(Rack r, bool on)
{
    var masses = new List<IMyArtificialMassBlock>();
    GridTerminalSystem.GetBlocksOfType(masses, m => InBay(m, r) && HasMissileToken(m, "Mass"));
    for (int i = 0; i < masses.Count; i++) { try { masses[i].Enabled = on; } catch {} }
}

void PreconfigureMissile(Rack r)
{
    if (!IsMerged(r)) return;        // only while merged
    EnableMissileMerge(r);           // keep missile merge enabled until eject

    // 3) Batteries — set to RECHARGE while merged (AUTO at eject gate)
    {
        var bats = new List<IMyBatteryBlock>();
        GridTerminalSystem.GetBlocksOfType(bats, b => InBay(b, r) && HasMissileToken(b, "Batt"));
        for (int i = 0; i < bats.Count; i++) { try { bats[i].Enabled = true; bats[i].ChargeMode = ChargeMode.Recharge; } catch {} }
    }

    // 4) Thrusters — zero override and keep disabled (blueprints sometimes bake overrides)
    {
        var thr = new List<IMyThrust>();
        GridTerminalSystem.GetBlocksOfType(thr, t => InBay(t, r)); // M- prefix required by InBay
        for (int i = 0; i < thr.Count; i++) { try { thr[i].ThrustOverridePercentage = 0f; thr[i].Enabled = false; } catch {} }
    }

    // 4b) Gyros — ensure On, no override, full power (blueprints may spawn Off)
    {
        var gy = new List<IMyGyro>();
        GridTerminalSystem.GetBlocksOfType(gy, g => InBay(g, r));
        for (int i = 0; i < gy.Count; i++) { try { gy[i].Enabled = true; gy[i].GyroOverride = false; gy[i].GyroPower = 1f; } catch {} }
    }

    // 5) Artificial mass — keep OFF here; enabled at eject gate right before detach
    {
        var masses = new List<IMyArtificialMassBlock>();
        GridTerminalSystem.GetBlocksOfType(masses, m => InBay(m, r) && HasMissileToken(m, "Mass"));
        for (int i = 0; i < masses.Count; i++) { try { masses[i].Enabled = false; } catch {} }
    }
}


// === ADDED: read-only missile assessment (runs after build completes)
bool AssessMissile(Rack r, out MissileStatus ms) {
    ms = new MissileStatus();
    if (!IsMerged(r)) return false;
    ms.FoundMissileGrid = true; // scope is bay, not grid

    var thr    = new List<IMyThrust>();
    var bats   = new List<IMyBatteryBlock>();
    var sens   = new List<IMySensorBlock>();
    var mass   = new List<IMyArtificialMassBlock>();
    var timers = new List<IMyTimerBlock>();

    GridTerminalSystem.GetBlocksOfType(thr,    b => InBay(b, r));
    GridTerminalSystem.GetBlocksOfType(bats,   b => InBay(b, r) && HasMissileToken(b, "Batt"));
    GridTerminalSystem.GetBlocksOfType(sens,   b => InBay(b, r) && HasMissileToken(b, "Sensor"));
    GridTerminalSystem.GetBlocksOfType(mass,   b => InBay(b, r) && HasMissileToken(b, "Mass"));
    GridTerminalSystem.GetBlocksOfType(timers, b => InBay(b, r) && HasMissileToken(b, "Arm"));

    ms.ThrusterCount = thr.Count;
    ms.BatteryCount  = bats.Count;
    ms.HasNoseSensor = sens.Count > 0;
    ms.HasMassBlock  = mass.Count > 0;
    ms.HasArmTimer   = timers.Count > 0;

    int auto=0, rech=0, dis=0;
    for (int i = 0; i < bats.Count; i++) {
        var m = bats[i].ChargeMode;
        if (m == ChargeMode.Auto) auto++;
        else if (m == ChargeMode.Recharge) rech++;
        else if (m == ChargeMode.Discharge) dis++;
    }
    ms.BatteryModes = "AUTO:" + auto + ", RECHARGE:" + rech + ", DISCHARGE:" + dis;
    return true;
}

// ===== OBSERVE =====
RackObs Observe(Rack r) {
    var o = new RackObs();
    o.Merge = IsMerged(r);
    o.Bay = AnySensorActive(r);
    o.Remaining = (r.Projector != null) ? r.Projector.RemainingBlocks : -1;
    o.Projecting = (r.Projector != null) && r.Projector.IsProjecting;
    o.AllWeldersOn = AllWeldersOn(r);
    o.AtBuild = AtBuild(r);
    o.AtLaunch = AtLaunch(r);
    o.Doors = DoorsStr(r.Doors, r.Goal);
    return o;
}

string BuildStatus(Rack r, RackObs o) {
    if (r.Projector == null) return "UNKNOWN";
    if (o.Remaining == 0) return "COMPLETE";
    if (o.Remaining > 0 && o.Projecting && o.AllWeldersOn) return "ACTIVE";
    if (o.Remaining > 0 && o.Projecting && !o.AllWeldersOn) return "NEEDS WELDERS";
    if (o.Remaining > 0 && !o.Projecting) return "NEEDS PROJECTION";
    return "UNKNOWN";
}

bool MissileBuilt(Rack r) {
    return r.Projector != null && r.Projector.RemainingBlocks == 0;
}

// Build gating: continue welding regardless of sensor/merge once started.
// Only the START of BUILD requires bay clear.
bool CanBuildNow(Rack r) {
    if (r.Projector == null) return false;
    if (!AtBuild(r)) return false;
    if (DoorsStr(r.Doors, DoorGoal.Close) != "CLOSED") return false;
    if (!r.Projector.IsProjecting) return false;
    if (r.Projector.RemainingBlocks <= 0) return false;
    if (r.Fault == FaultType.PISTON_MISSING || r.Fault == FaultType.PISTON_DUP) return false;
    return true;
}

string CanBuildDiag(Rack r) {
    if (r.Projector == null) return "No projector";
    if (!AtBuild(r)) return "Pistons not at BUILD";
    if (DoorsStr(r.Doors, DoorGoal.Close) != "CLOSED") return "Doors not closed";
    if (!r.Projector.IsProjecting) return "Projector idle";
    if (r.Projector.RemainingBlocks <= 0) return "No remaining";
    if (r.Fault == FaultType.PISTON_MISSING) return "Pistons missing";
    if (r.Fault == FaultType.PISTON_DUP) return "Piston number duplicate";
    return "OK";
}

// ===== RECOVER =====
void RecoverStates() {
    for (int i = 0; i < Racks.Count; i++) {
        var r = Racks[i];
        if (Disarmed) { r.State = RackState.DISARMED; continue; }

        if (IsMerged(r) && MissileBuilt(r)) {
            r.State = RackState.PRIMED; continue;
        }

        // Default idle posture: no auto-build
        r.State = RackState.RELOADING;
        StopProjector(r); SetAllWelders(r, false);
        r.BuildArmed = false; // unarmed after compile/STATUS
    }
}

// ===== MAIN =====
void Main(string arg, UpdateType ut) {
    if ((ut & (UpdateType.Trigger | UpdateType.Terminal)) != 0 && !string.IsNullOrWhiteSpace(arg)) {
        HandleCommand(arg.Trim());
    }

    for (int i = 0; i < Racks.Count; i++) {
        var r = Racks[i];

        if (r.Diag.Active) { TickDiag(r); continue; }

        if (Disarmed) r.State = RackState.DISARMED;
        if (r.State == RackState.DISARMED) { TickDisarmed(r); continue; }
        if (r.State == RackState.BUILDING) { TickBuilding(r); continue; }
        if (r.State == RackState.PRIMED)   { TickPrimed(r);   continue; }
        if (r.State == RackState.EJECTING) { TickEjecting(r); continue; }
        if (r.State == RackState.RELOADING){ TickReloading(r);continue; }
    }
    ShowDiagEcho();
    DrawLcdSplit();
    DrawHelm();
}

// ===== COMMANDS =====
void HandleCommand(string a)
{
    if (EqualsCI(a, "DISARM"))
    {
        Disarmed = true;
        for (int i = 0; i < Racks.Count; i++)
        {
            var r = Racks[i];
            r.State = RackState.DISARMED;
            r.BuildArmed = false;
            CloseDoors(r);
            StopProjector(r);
            SetAllWelders(r, false);
        }
        return;
    }
    if (EqualsCI(a, "STATUS")) { DiscoverAll(); AutoConfigureSensors(); RecoverStates(); EnsureProfileDefaults(); return; }
    if (EqualsCI(a, "PREP")) { Disarmed = false; for (int i = 0; i < Racks.Count; i++) PrepRack(Racks[i]); return; }
    if (EqualsCI(a, "FIRE L")) { Disarmed = false; var rl = GetRack("L"); if (rl != null) FireRack(rl); return; }
    if (EqualsCI(a, "FIRE R")) { Disarmed = false; var rr = GetRack("R"); if (rr != null) FireRack(rr); return; }

    // Grid diagnostics to sustained echo
    if (EqualsCI(a, "GRID CHECK L")) { var rl = GetRack("L"); SetDiagEcho(RenderGridCheck(rl)); return; }
    if (EqualsCI(a, "GRID CHECK R")) { var rr = GetRack("R"); SetDiagEcho(RenderGridCheck(rr)); return; }
    if (EqualsCI(a, "GRID CHECK ALL")) {
        var sb = new StringBuilder(2048);
        var rl = GetRack("L"); if (rl != null) sb.AppendLine(RenderGridCheck(rl));
        var rr = GetRack("R"); if (rr != null) sb.AppendLine(RenderGridCheck(rr));
        SetDiagEcho(sb.ToString()); return;
    }
    if (EqualsCI(a, "CLEAR CHECK")) { ClearDiagEcho(); return; }

    // Battery diagnostics (persistent echo; reads only)
    if (EqualsCI(a, "BATS CHECK R")) { var rackR = GetRack("R"); SetDiagEcho(RenderBatteryDiag(rackR)); return; }
    if (EqualsCI(a, "BATS CHECK L")) { var rackL = GetRack("L"); SetDiagEcho(RenderBatteryDiag(rackL)); return; }

    // Profile controls (hotbar)
    // Toggle between INTERCEPT and WILD WEASEL
    if (EqualsCI(a, "TOGGLE PROFILE"))
    {
        var cur = LoadProfile("Mode", "INTERCEPT");
        var next = cur.Equals("WILD WEASEL", StringComparison.OrdinalIgnoreCase) ? "INTERCEPT" : "WILD WEASEL";
        SaveProfile("Mode", next);
        return;
    }
    if (EqualsCI(a, "TOGGLE NEUTRALS")) { SaveProfile("Neutrals", LoadProfile("Neutrals", "true").Equals("true", StringComparison.OrdinalIgnoreCase) ? "false" : "true"); return; }
    if (EqualsCI(a, "TOGGLE SUBSYS")) { SaveProfile("TargetGroup", Cycle(new string[] { "Weapons", "Propulsion", "PowerSystems" }, LoadProfile("TargetGroup", "Weapons"))); return; }
    if (EqualsCI(a, "TOGGLE PRIORITY")) { SaveProfile("TargetPriority", Cycle(new string[] { "Closest", "Largest", "Smallest" }, LoadProfile("TargetPriority", "Largest"))); return; }
    // Grid size preset (fallback center-distance fuse)
    if (EqualsCI(a, "TOGGLE GRIDSIZE"))
    {
        var cur = LoadProfile("GridSize", DEF_GridSize);
        SaveProfile("GridSize", cur.Equals("SMALL GRID", StringComparison.OrdinalIgnoreCase) ? "LARGE GRID" : "SMALL GRID");
        return;
    }
    // Fuse mode preset (sensor front bubble)
    if (EqualsCI(a, "TOGGLE FUSEMODE"))
    {
        var cur = LoadProfile("FuseMode", DEF_FuseMode);
        SaveProfile("FuseMode", cur.Equals("IMPACT", StringComparison.OrdinalIgnoreCase) ? "BURST" : "IMPACT");
        return;
    }
    // Decoy toggle
    if (EqualsCI(a, "TOGGLE DECOY"))
    {
        var cur = LoadProfile("EnableDecoy", "true");
        SaveProfile("EnableDecoy", cur.Equals("true", StringComparison.OrdinalIgnoreCase) ? "false" : "true");
        return;
    }
    // Explicit build arm
    if (EqualsCI(a, "BUILD L")) { var rl = GetRack("L"); if (rl != null) ArmBuild(rl); return; }
    if (EqualsCI(a, "BUILD R")) { var rr = GetRack("R"); if (rr != null) ArmBuild(rr); return; }
    if (EqualsCI(a, "BUILD ALL")) { for (int i = 0; i < Racks.Count; i++) ArmBuild(Racks[i]); return; }
    if (EqualsCI(a, "BUILD OFF")) { for (int i = 0; i < Racks.Count; i++) DisarmBuild(Racks[i]); return; }

    if (a.StartsWith("STEP ", StringComparison.OrdinalIgnoreCase)) { HandleStepCommand(a); return; }
    // TEST: Artificial Mass control per bay
    if (EqualsCI(a, "MASS ON L"))  { var rl = GetRack("L"); if (rl != null) MassOn_Bay(rl);  return; }
    if (EqualsCI(a, "MASS OFF L")) { var rl = GetRack("L"); if (rl != null) MassOff_Bay(rl); return; }
    if (EqualsCI(a, "MASS ON R"))  { var rr = GetRack("R"); if (rr != null) MassOn_Bay(rr);  return; }
    if (EqualsCI(a, "MASS OFF R")) { var rr = GetRack("R"); if (rr != null) MassOff_Bay(rr); return; }

    if (a.StartsWith("STEP ", StringComparison.OrdinalIgnoreCase)) { HandleStepCommand(a); return; }
}

void HandleStepCommand(string a) {
    // Expected: STEP <L|R> START|NEXT|PREV|GOTO n|EXIT|AUTO ON|AUTO OFF|RESET
    var parts = a.Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length < 3) return;

    string side = parts[1].ToUpper();
    var r = GetRack(side);
    if (r == null) return;

    string cmd = parts[2].ToUpper();

    if (cmd == "START") {
        r.Diag.Active = true;
        r.Diag.Step = 0;
        r.Diag.Auto = false;
        r.Diag.Clock = 0;
        r.Diag.LastRem = -1;
        r.Diag.Why = "";
        r.Diag.Status = "WAIT";
        return;
    }
    if (!r.Diag.Active && cmd != "START") return;

    if (cmd == "NEXT") { r.Diag.Step = Math.Min(14, r.Diag.Step + 1); r.Diag.Clock = 0; r.Diag.Status = "WAIT"; return; }
    if (cmd == "PREV") { r.Diag.Step = Math.Max(0, r.Diag.Step - 1);  r.Diag.Clock = 0; r.Diag.Status = "WAIT"; return; }
    if (cmd == "EXIT") { r.Diag.Active = false; r.Diag.Status = "IDLE"; RecoverStates(); return; }
    if (cmd == "RESET"){ r.Diag.Clock = 0; r.Diag.LastRem = -1; r.Diag.Status = "WAIT"; r.Diag.Why = ""; return; }
    if (cmd == "AUTO" && parts.Length >= 4) { r.Diag.Auto = parts[3].Equals("ON", StringComparison.OrdinalIgnoreCase); return; }
    if (cmd == "GOTO" && parts.Length >= 4) {
        int n;
        if (int.TryParse(parts[3], out n)) {
            r.Diag.Step = Math.Max(0, Math.Min(14, n));
            r.Diag.Clock = 0;
            r.Diag.Status = "WAIT";
        }
        return;
    }
}

void ArmBuild(Rack r) {
    if (r == null) return;
    r.BuildArmed = true;
    r.State = RackState.RELOADING;     // enter reload path; start only when safe
    if (r.Projector != null) r.Projector.Enabled = true; // show projection only; welders OFF until BUILDING
    SetAllWelders(r, false);
}

void DisarmBuild(Rack r) {
    if (r == null) return;
    r.BuildArmed = false;
    StopProjector(r);
    SetAllWelders(r, false);
    if (IsMerged(r) && MissileBuilt(r)) r.State = RackState.PRIMED;
    else r.State = RackState.RELOADING;
}

// ===== NORMAL TICKS =====
void TickDisarmed(Rack r) {
    CloseDoors(r);
    ToBuild(r);
    StopProjector(r);
    SetAllWelders(r, false);
    r.StallClock = 0; r.DebounceClock = 0; r.ClearTicks = 0;
}

void TickBuilding(Rack r) {
    if (!r.BuildArmed) { SetAllWelders(r, false); StopProjector(r); r.State = RackState.RELOADING; return; }

    CloseDoors(r);
    ToBuild(r);

    if (r.Projector != null) r.Projector.Enabled = true;

    if (!CanBuildNow(r)) {
        SetAllWelders(r, false);
        if (r.Projector != null && r.Projector.RemainingBlocks <= 0) {
            StopProjector(r);
            r.State = IsMerged(r) ? RackState.PRIMED : RackState.RELOADING;
        }
        return;
    }

    SetAllWelders(r, true);

    int rem = r.Projector != null ? r.Projector.RemainingBlocks : 0;
    if (r.LastRemaining < 0) r.LastRemaining = rem;

    if (rem == 0) {
        SetAllWelders(r, false);
        StopProjector(r);
        r.StallClock = 0; r.LastRemaining = -1;
        r.State = IsMerged(r) ? RackState.PRIMED : RackState.RELOADING;

        // === ADDED: run missile scan once, right after build completes
        MissileStatus _ms;
        if (AssessMissile(r, out _ms)) {
            Echo("SLS: " + r.Prefix.Trim() +
                 " missile scan OK thr=" + _ms.ThrusterCount +
                 " bat=" + _ms.BatteryCount +
                 " nose=" + _ms.HasNoseSensor +
                 " mass=" + _ms.HasMassBlock +
                 " armT=" + _ms.HasArmTimer +
                 " bats[" + _ms.BatteryModes + "]");
        } else {
            Echo("SLS: " + r.Prefix.Trim() + " missile scan FAILED (no missile grid)");
        }
        // === END ADDED

        return;
    }

    if (rem < r.LastRemaining) { r.StallClock = 0; r.LastRemaining = rem; }
    else {
        r.StallClock += TICK_SECONDS;
        if (r.StallClock >= STALL_SECONDS) r.Fault = FaultType.STALL;
    }

    if (r.Projector != null && !r.Projector.IsProjecting && rem > 0) {
        r.Fault = FaultType.MATERIALS_LOW;
    }
}

void TickPrimed(Rack r) {
    CloseDoors(r);
    EnsureMergeOn(r);
    PreconfigureMissile(r); // configure thrusters/mass while still merged
    StopProjector(r);
    SetAllWelders(r, false);
    ToBuild(r);
    if (!IsMerged(r) || !MissileBuilt(r)) r.State = RackState.RELOADING;
}

void TickEjecting(Rack r) {
    OpenDoors(r);
    StopProjector(r);
    SetAllWelders(r, false);

    // 1) Wait for doors fully OPEN before moving pistons
    if (!DoorsFullyOpen(r)) {
        ToBuild(r); // hold retracted
    } else {
        // 2) Doors open: extend pistons to launch
        ToLaunch(r);

        // 3) While still merged and at launch, prep battery, hand off, then release
        if (AtLaunch(r) && IsMerged(r)) {
            SetMissileMassEnabled(r, true); // enable simulated gravity right before detach
            SetMissileBatteryAuto(r);  // last safe moment
            SendLaunchStart(r);        // deliver while powered/merged
            DisableMissileMerge(r);         // detach by missile-side merge OFF
        }
    }

    // 4) Begin clear-bay debounce after release
    bool released = !IsMerged(r);
    if (released && !AnySensorActive(r)) {
        r.DebounceClock += TICK_SECONDS;
        if (r.DebounceClock >= DROP_DEBOUNCE_SECONDS) r.ClearTicks++;
    } else {
        r.DebounceClock = 0;
        r.ClearTicks = 0;
    }

    // 5) Transition back to RELOADING after missile clear
    if (r.ClearTicks >= N_CLEAR_TICKS) {
        ToBuild(r);
        EnsureMergeOn(r); // keep rack-side merge ON by doctrine
        r.State = RackState.RELOADING;
        r.DebounceClock = 0;
        r.ClearTicks = 0;
        r.StallClock = 0;
        return;
    }

    // 6) Fault if stuck too long
    r.StallClock += TICK_SECONDS;
    if (r.StallClock >= STALL_SECONDS && r.Fault == FaultType.NONE)
        r.Fault = FaultType.STUCK;
}

void TickReloading(Rack r) {
    CloseDoors(r);
    EnsureMergeOn(r);
    ToBuild(r);
    SetAllWelders(r, false);

    // Only show projection if armed
    if (r.Projector != null) r.Projector.Enabled = r.BuildArmed;

    int rem = (r.Projector != null) ? r.Projector.RemainingBlocks : -1;
    bool needsBuild = (r.Projector != null && rem > 0);

    // Start BUILDING only when armed AND bay is clear
    if (r.BuildArmed && needsBuild && CanBuildNow(r) && !AnySensorActive(r)) {
        r.State = RackState.BUILDING;
        r.StallClock = 0; r.LastRemaining = rem;
        return;
    }

    // If fully built and merged, become PRIMED
    if (IsMerged(r) && MissileBuilt(r)) {
        StopProjector(r);
        r.State = RackState.PRIMED;
    }
}

// ===== DIAG TICK =====
void TickDiag(Rack r) {
    var d = r.Diag;
    var o = Observe(r);
    d.Clock += TICK_SECONDS;
    d.Why = ""; d.Status = "WAIT";

    // 0 Safe idle
    if (d.Step == 0) {
        CloseDoors(r); ToBuild(r); StopProjector(r); SetAllWelders(r, false); EnsureMergeOn(r);
        d.Status = "PASS";
    }
    // 1 Doors CLOSED
    else if (d.Step == 1) {
        CloseDoors(r);
        d.Status = (o.Doors == "CLOSED") ? "PASS" : "WAIT";
        if (d.Status != "PASS") d.Why = "Doors not CLOSED";
    }
    // 2 Pistons to BUILD
    else if (d.Step == 2) {
        ToBuild(r);
        d.Status = o.AtBuild ? "PASS" : "WAIT";
        if (d.Status != "PASS") d.Why = "Pistons not RETRACTED";
    }
    // 3 Projector ON
    else if (d.Step == 3) {
        if (r.Projector != null) r.Projector.Enabled = true;
        SetAllWelders(r, false);
        d.Status = (r.Projector != null && o.Projecting) ? "PASS" : "WAIT";
        if (r.Projector == null) { d.Status = "FAIL"; d.Why = "No projector"; }
        else if (!o.Projecting) d.Why = "Projector idle";
    }
    // 4 Welders ON when safe; sensor ignored here
    else if (d.Step == 4) {
        if (CanBuildNow(r)) {
            if (r.Projector != null) r.Projector.Enabled = true;
            SetAllWelders(r, true);
            d.Status = AllWeldersOn(r) ? "PASS" : "WAIT";
            if (!AllWeldersOn(r)) d.Why = "Welders off";
        } else {
            SetAllWelders(r, false);
            d.Status = "WAIT";
            d.Why = CanBuildDiag(r);
        }
    }
    // 5 Build ACTIVE (remaining > 0 and decreasing)
    else if (d.Step == 5) {
        if (!CanBuildNow(r)) { SetAllWelders(r, false); d.Status = "WAIT"; d.Why = CanBuildDiag(r); }
        else {
            if (r.Projector != null) r.Projector.Enabled = true;
            SetAllWelders(r, true);
            if (r.Projector == null) { d.Status = "FAIL"; d.Why = "No projector"; }
            else if (o.Remaining <= 0) { d.Status = "WAIT"; d.Why = "Nothing to weld"; }
            else {
                if (d.LastRem < 0) d.LastRem = o.Remaining;
                if (o.Remaining < d.LastRem) { d.Status = "PASS"; d.LastRem = o.Remaining; }
                else { d.Status = "WAIT"; d.Why = "Not decreasing"; }
            }
        }
    }
    // 6 Build COMPLETE
    else if (d.Step == 6) {
        SetAllWelders(r, false);
        d.Status = (o.Remaining == 0) ? "PASS" : "WAIT";
        if (d.Status != "PASS") d.Why = "Remaining > 0";
    }
    // 7 Merge ENABLED
    else if (d.Step == 7) {
        EnsureMergeOn(r);
        d.Status = (r.Merge != null && r.Merge.Enabled) ? "PASS" : "WAIT";
        if (r.Merge == null) { d.Status = "FAIL"; d.Why = "No merge block"; }
        else if (!r.Merge.Enabled) d.Why = "Merge disabled";
    }
    // 8 Merge CONNECTED
    else if (d.Step == 8) {
        EnsureMergeOn(r);           // rack merge ON
        EnableMissileMerge(r);       // NEW: missile merge ON
        d.Status = o.Merge ? "PASS" : "WAIT";
        if (!o.Merge) d.Why = "Not connected";
    }
    // 9 Doors OPEN
    else if (d.Step == 9) {
        OpenDoors(r);
        d.Status = (o.Doors == "OPEN") ? "PASS" : "WAIT";
        if (d.Status != "PASS") d.Why = "Doors not OPEN";
    }
    // 10 Pistons LAUNCH
    else if (d.Step == 10) {
        ToLaunch(r);
        d.Status = o.AtLaunch ? "PASS" : "WAIT";
        if (!o.AtLaunch) d.Why = "Pistons not EXTENDED";
    }
    // 11 Launch
    else if (d.Step == 11) {
        SendLaunchStart(r); // send while still merged/powered
        if (r.Merge != null) r.Merge.Enabled = false;
        if (!o.Bay) { d.Status = "PASS"; }
        else { d.Status = "WAIT"; d.Why = "Bay still detected"; }
        SetAllWelders(r, false);
        StopProjector(r);
    }

    // 12 Pistons back to BUILD
    else if (d.Step == 12) {
        ToBuild(r);
        d.Status = o.AtBuild ? "PASS" : "WAIT";
        if (!o.AtBuild) d.Why = "Pistons not RETRACTED";
    }
    // 13 Doors CLOSE
    else if (d.Step == 13) {
        CloseDoors(r);
        d.Status = (o.Doors == "CLOSED") ? "PASS" : "WAIT";
        if (d.Status != "PASS") d.Why = "Doors not CLOSED";
    }
    // 14 Primed conditions
    else if (d.Step == 14) {
        EnsureMergeOn(r);
        d.Status = (o.Merge && o.Remaining == 0) ? "PASS" : "WAIT";
        if (!o.Merge) d.Why = "Merge not connected";
        else if (o.Remaining != 0) d.Why = "Build not complete";
        SetAllWelders(r, false);
        StopProjector(r);
    }

    if (d.Auto && d.Status == "PASS" && d.Step < 14) { d.Step++; d.Clock = 0; d.Status = "WAIT"; }
}

// ===== ACTIONS =====
void PrepRack(Rack r) {
    if (r == null) return;
    CloseDoors(r); ToBuild(r);
    StopProjector(r); SetAllWelders(r, false);
    r.BuildArmed = false;

    if (IsMerged(r) && MissileBuilt(r)) { r.State = RackState.PRIMED; return; }
    r.State = RackState.RELOADING;
}

void FireRack(Rack r) {
    if (r == null || r.State == RackState.DISARMED) return;

    r.BuildArmed = false;
    StopProjector(r);
    SetAllWelders(r, false);
    EnsureMergeOn(r);

    OpenDoors(r);
    ToBuild(r);

    r.State = RackState.EJECTING;
    r.DebounceClock = 0;
    r.ClearTicks = 0;
    r.StallClock = 0;
}

// ===== MISSILE SIGNAL =====
// Always pre-enable missile PBs so they are ready when the gate opens.
// Send LAUNCH_START only after rack pistons are at launch extension.
void SendLaunchStart(Rack r)
{
    // 0) Pre-enable all missile PBs in this bay named "M-..."
    var pbs = new List<IMyProgrammableBlock>();
    GridTerminalSystem.GetBlocksOfType(pbs, pb =>
    pb != null && pb.IsFunctional && InBay(pb, r));
    for (int i = 0; i < pbs.Count; i++) if (!pbs[i].Enabled) pbs[i].Enabled = true;

    // 1) Hard gate: must be at launch with merge engaged (use same tolerance as AtLaunch)
    if (r == null || !AtLaunch(r) || !IsMerged(r)) return;

    // 2) Read desired settings from THIS PB → [MissileProfile]
    var cfg = new MyIni(); MyIniParseResult res;
    string mode="INTERCEPT", neutrals="true", group="Weapons", prio="Largest";
    string gridSize=DEF_GridSize, fuseMode=DEF_FuseMode, decoy="true";
    if (cfg.TryParse(Me.CustomData, out res)) {
        mode      = cfg.Get("MissileProfile","Mode").ToString(mode);
        neutrals  = cfg.Get("MissileProfile","Neutrals").ToString(neutrals);
        group     = cfg.Get("MissileProfile","TargetGroup").ToString(group);
        prio      = cfg.Get("MissileProfile","TargetPriority").ToString(prio);
        gridSize  = cfg.Get("MissileProfile","GridSize").ToString(gridSize);
        fuseMode  = cfg.Get("MissileProfile","FuseMode").ToString(fuseMode);
        decoy     = cfg.Get("MissileProfile","EnableDecoy").ToString(decoy);
    }
    float standoff = gridSize.Equals("LARGE GRID", StringComparison.OrdinalIgnoreCase) ? PRESET_STANDOFF_LARGE : PRESET_STANDOFF_SMALL;
    float sfront   = fuseMode.Equals("BURST", StringComparison.OrdinalIgnoreCase)       ? PRESET_SENSOR_BURST   : PRESET_SENSOR_IMPACT;

    // 3) Handoff to missile PBs and trigger
    for (int i = 0; i < pbs.Count; i++) {
        try {
            var pb = pbs[i];
            var ini = new MyIni(); MyIniParseResult r2; ini.TryParse(pb.CustomData, out r2);
            ini.Set("Config", "ProfileIntent",  mode);
            ini.Set("Config", "TargetNeutrals", neutrals);
            ini.Set("Config", "TargetGroup",    group);
            ini.Set("Config", "TargetPriority", prio);
            ini.Set("Config", "FuseStandoff",   standoff.ToString(System.Globalization.CultureInfo.InvariantCulture));
            ini.Set("Config", "SensorFront",    sfront.ToString(System.Globalization.CultureInfo.InvariantCulture));
            ini.Set("Config", "EnableDecoy",    decoy);
            ini.Set("Config", "LaunchSettle",   "2.5");
            pb.CustomData = ini.ToString();
            pb.TryRun("LAUNCH_START");
        } catch {}
    }
}

// ===== DOORS =====
void OpenDoors(Rack r) { r.Goal = DoorGoal.Open;  for (int i = 0; i < r.Doors.Count; i++) r.Doors[i].OpenDoor(); }
void CloseDoors(Rack r){ r.Goal = DoorGoal.Close; for (int i = 0; i < r.Doors.Count; i++) r.Doors[i].CloseDoor(); }

// ===== PROJECTOR / WELDERS =====
void StopProjector(Rack r) { if (r.Projector != null) r.Projector.Enabled = false; }

void SetAllWelders(Rack r, bool on) {
    var ws = r.Welders;
    for (int i = 0; i < ws.Count; i++) ws[i].Enabled = on;
}

bool AllWeldersOn(Rack r) {
    var ws = r.Welders;
    if (ws.Count == 0) return false;
    for (int i = 0; i < ws.Count; i++) if (!ws[i].Enabled) return false;
    return true;
}

// ===== SENSORS / MERGE =====
bool AnySensorActive(Rack r) {
    for (int i = 0; i < r.Sensors.Count; i++) if (r.Sensors[i].IsActive) return true;
    return false;
}
bool IsMerged(Rack r) { return r.Merge != null && r.Merge.IsConnected; }
void EnsureMergeOn(Rack r) { if (r.Merge != null) r.Merge.Enabled = true; }

// ===== PISTONS =====
bool AtBuild(Rack r) {
    if (r.Piston1 == null || r.Piston2 == null) return false;
    if (r.Tag=="L") return AtPos(r.Piston1, BUILD1_POS_L) && AtPos(r.Piston2, BUILD2_POS_L);
    else            return AtPos(r.Piston1, BUILD1_POS_R) && AtPos(r.Piston2, BUILD2_POS_R);
}
bool AtLaunch(Rack r) {
    if (r.Piston1 == null || r.Piston2 == null) return false;
    if (r.Tag=="L") return AtPos(r.Piston1, LAUNCH1_POS_L) && AtPos(r.Piston2, LAUNCH2_POS_L);
    else            return AtPos(r.Piston1, LAUNCH1_POS_R) && AtPos(r.Piston2, LAUNCH2_POS_R);
}
void ToBuild(Rack r) {
    if (r.Piston1 == null || r.Piston2 == null) return;
    if (r.Tag=="L") { ToPos(r.Piston1, BUILD1_POS_L); ToPos(r.Piston2, BUILD2_POS_L); }
    else            { ToPos(r.Piston1, BUILD1_POS_R); ToPos(r.Piston2, BUILD2_POS_R); }
}
void ToLaunch(Rack r) {
    if (r.Piston1 == null || r.Piston2 == null) return;
    if (r.Tag=="L") { ToPos(r.Piston1, LAUNCH1_POS_L); ToPos(r.Piston2, LAUNCH2_POS_L); }
    else            { ToPos(r.Piston1, LAUNCH1_POS_R); ToPos(r.Piston2, LAUNCH2_POS_R); }
}
bool AtPos(IMyPistonBase p, float pos) {
    return p != null && Math.Abs(p.CurrentPosition - pos) <= PISTON_EPS;
}
void ToPos(IMyPistonBase p, float pos) {
    if (p == null) return;
    if (Math.Abs(p.CurrentPosition - pos) <= PISTON_EPS) {
        p.Velocity = 0f;
        p.MinLimit = pos;
        p.MaxLimit = pos;
        return;
    }
    float lo = Math.Min(p.CurrentPosition, pos);
    float hi = Math.Max(p.CurrentPosition, pos);
    p.MinLimit = lo;
    p.MaxLimit = hi;
    p.Velocity = (p.CurrentPosition < pos) ? PISTON_SPEED : -PISTON_SPEED;
}

// ===== LCD (SPLIT) =====
void DrawLcdSplit() {
    var rl = GetRack("L");
    var rr = GetRack("R");

    string left  = (rl != null) ? BuildRackText(rl) : "";
    string right = (rr != null) ? BuildRackText(rr) : "";

    if (rl != null) {
        for (int i = 0; i < rl.Lcds.Count; i++) {
            rl.Lcds[i].ContentType = ContentType.TEXT_AND_IMAGE;
            rl.Lcds[i].WriteText(left);
        }
    }
    if (rr != null) {
        for (int i = 0; i < rr.Lcds.Count; i++) {
            rr.Lcds[i].ContentType = ContentType.TEXT_AND_IMAGE;
            rr.Lcds[i].WriteText(right);
        }
    }

    var both = new StringBuilder();
    if (!string.IsNullOrEmpty(left)) both.Append(left).AppendLine();
    if (!string.IsNullOrEmpty(right)) both.Append(right);
    for (int i = 0; i < AlertLcds.Count; i++) { AlertLcds[i].ContentType = ContentType.TEXT_AND_IMAGE; AlertLcds[i].WriteText(both.ToString()); }

    bool anyFault = false; for (int i = 0; i < Racks.Count; i++) if (Racks[i].Fault != FaultType.NONE) anyFault = true;
    for (int i = 0; i < AlertLights.Count; i++) AlertLights[i].Enabled = anyFault;
}

void DrawHelm() {
    var ini = new MyIni(); MyIniParseResult res;
    string mode="INTERCEPT", neutrals="true", targetGroup="Weapons", targetPriority="Largest";
    string gridSize=DEF_GridSize, fuseMode=DEF_FuseMode, decoy="true";

    if (ini.TryParse(Me.CustomData, out res)) {
        mode           = ini.Get("MissileProfile","Mode").ToString(mode);
        neutrals       = ini.Get("MissileProfile","Neutrals").ToString(neutrals);
        targetGroup    = ini.Get("MissileProfile","TargetGroup").ToString(targetGroup);
        targetPriority = ini.Get("MissileProfile","TargetPriority").ToString(targetPriority);
        gridSize       = ini.Get("MissileProfile","GridSize").ToString(gridSize);
        fuseMode       = ini.Get("MissileProfile","FuseMode").ToString(fuseMode);
        decoy          = ini.Get("MissileProfile","EnableDecoy").ToString(decoy);
    }

    var sb = new StringBuilder();
    sb.AppendLine("=== Missile Pre-Launch ===");
    sb.AppendLine("Mode: " + mode);
    sb.AppendLine("Neutrals: " + neutrals);
    sb.AppendLine("Subsystem: " + targetGroup);
    sb.AppendLine("Priority: " + targetPriority);
    sb.AppendLine("Decoy: " + decoy);
    sb.AppendLine("FuseMode: " + fuseMode);
    sb.AppendLine("GridSize: " + gridSize);

    for (int i = 0; i < HelmLcds.Count; i++) {
        var p = HelmLcds[i];
        p.ContentType = ContentType.TEXT_AND_IMAGE;
        p.WriteText(sb.ToString());
    }
}

string BuildRackText(Rack r) {
    if (r == null) return "";
    var o = Observe(r);
    var sb = new StringBuilder();

    // Read desired settings from THIS PB's CustomData → [MissileProfile]
    var ini = new MyIni(); MyIniParseResult res;
    string mode = "INTERCEPT";
    string neutrals = "true";
    string group = "Weapons";
    string prio  = "Largest";
    if (ini.TryParse(Me.CustomData, out res)) {
        mode     = ini.Get("MissileProfile","Mode").ToString(mode);
        neutrals = ini.Get("MissileProfile","Neutrals").ToString(neutrals);
        group    = ini.Get("MissileProfile","TargetGroup").ToString(group);
        prio     = ini.Get("MissileProfile","TargetPriority").ToString(prio);
    }
    sb.AppendLine("Profile:   " + mode + "  Neutrals: " + neutrals);
    sb.AppendLine("Subsystem: " + group + "  Priority: " + prio);

    if (r.Diag.Active) {
        sb.AppendLine("=== Rack " + r.Tag + " DIAG ===");
        sb.AppendLine("Step: " + r.Diag.Step + "/14  Auto:" + (r.Diag.Auto ? "ON" : "OFF"));
        sb.AppendLine("Status: " + r.Diag.Status + (string.IsNullOrEmpty(r.Diag.Why) ? "" : ("  WHY: " + r.Diag.Why)));
    } else {
        sb.AppendLine("=== Rack " + r.Tag + " ===");
    }

    sb.AppendLine("State:     " + StateStr(r.State));
    sb.AppendLine("BuildArm:  " + (r.BuildArmed ? "ON" : "OFF"));
    sb.AppendLine("Doors:     " + o.Doors);
    sb.AppendLine("Merge:     " + (o.Merge ? "CONNECTED" : "DISCONNECTED"));
    sb.AppendLine("Bay:       " + (o.Bay ? "Detected" : "Clear"));
    sb.AppendLine("Build:     " + BuildStatus(r, o));
    sb.AppendLine("CanBuild:  " + (CanBuildNow(r) ? "YES" : "NO") + " (" + CanBuildDiag(r) + ")");
    sb.AppendLine("ProjRem:   " + (o.Remaining >= 0 ? o.Remaining.ToString() : "N/A"));
    sb.AppendLine("Projector: " + (o.Projecting ? "Projecting" : "Idle/Off"));
    sb.AppendLine("Welders:   " + (o.AllWeldersOn ? "All On" : "Not All On"));
    sb.AppendLine("Pistons:   " + PistonState(r));
    sb.AppendLine("Fault:     " + FaultStr(r.Fault));
    return sb.ToString();
}

string StateStr(RackState s) {
    if (s == RackState.BUILDING) return "BUILDING";
    if (s == RackState.PRIMED)    return "PRIMED";
    if (s == RackState.EJECTING) return "EJECTING";
    if (s == RackState.RELOADING)return "HOLDING";
    if (s == RackState.DISARMED) return "DISARMED";
    return "UNKNOWN";
}
string DoorsStr(List<IMyAirtightHangarDoor> doors, DoorGoal goal) {
    if (doors == null || doors.Count == 0) return "FAULT";
    bool allOpen = true, allClosed = true;
    for (int i = 0; i < doors.Count; i++) {
        var st = doors[i].Status;
        if (st != DoorStatus.Open)   allOpen = false;    // strict
        if (st != DoorStatus.Closed) allClosed = false;  // strict
    }
    if (goal == DoorGoal.Open  && allOpen)  return "OPEN";
    if (goal == DoorGoal.Close && allClosed) return "CLOSED";
    return "MOVING";
}
string PistonState(Rack r) {
    if (r.Piston1 == null || r.Piston2 == null) return "FAULT";
    if (AtBuild(r))  return "RETRACTED";
    if (AtLaunch(r)) return "EXTENDED";
    return "TRANSIT";
}
string FaultStr(FaultType f) {
    if (f == FaultType.NONE) return "NONE";
    if (f == FaultType.STALL) return "STALL";
    if (f == FaultType.MATERIALS_LOW) return "MATERIALS LOW";
    if (f == FaultType.STUCK) return "STUCK";
    if (f == FaultType.DOOR_FAIL) return "DOOR FAIL";
    if (f == FaultType.PISTON_MISSING) return "PISTON MISSING";
    if (f == FaultType.PISTON_DUP) return "PISTON DUPLICATE";
    return "NONE";
}

// ===== HELPERS =====

Vector3D BayAnchor(Rack r) {
    return (r != null && r.Merge != null) ? r.Merge.GetPosition() : Me.GetPosition();
}
bool InBay(IMyTerminalBlock b, Rack r) {
    if (b == null || r == null || r.Merge == null) return false;
    if (!b.IsSameConstructAs(Me)) return false;
    if (b.CustomName == null || !b.CustomName.StartsWith("M-", StringComparison.OrdinalIgnoreCase)) return false;
    Vector3D p = BayAnchor(r);
    return Vector3D.DistanceSquared(b.GetPosition(), p) <= BAY_R2;
}
bool HasMissileToken(IMyTerminalBlock b, string token) {
    if (b == null || b.CustomName == null) return false;
    // "M-" + token (case-insensitive), e.g., Batt, Mass, Merge, Sensor, Arm, ProgBlock
    return b.CustomName.StartsWith("M-" + token, StringComparison.OrdinalIgnoreCase);
}

void EnsureProfileDefaults() {
    var ini = new MyIni(); MyIniParseResult res;
    if (!ini.TryParse(Me.CustomData, out res)) ini.Clear();
    bool dirty = false;

    // Core (keep)
    dirty |= EnsureKey(ref ini, "MissileProfile", "Mode",           "INTERCEPT");
    dirty |= EnsureKey(ref ini, "MissileProfile", "Neutrals",       "true");
    dirty |= EnsureKey(ref ini, "MissileProfile", "TargetGroup",    "Weapons");
    dirty |= EnsureKey(ref ini, "MissileProfile", "TargetPriority", "Largest");

    // New toggles
    dirty |= EnsureKey(ref ini, "MissileProfile", "GridSize", DEF_GridSize); // SMALL GRID | LARGE GRID
    dirty |= EnsureKey(ref ini, "MissileProfile", "FuseMode", DEF_FuseMode); // IMPACT | BURST
    dirty |= EnsureKey(ref ini, "MissileProfile", "EnableDecoy", "true");

    // Remove deprecated/misleading keys
    if (ini.ContainsSection("MissileProfile")) {
        ini.Delete("MissileProfile", "Subsystem");
        ini.Delete("MissileProfile", "Priority");
        ini.Delete("MissileProfile", "LaunchSettle");
        ini.Delete("MissileProfile", "FuseStandoff");
        ini.Delete("MissileProfile", "SensorFront");
    }

    if (dirty) Me.CustomData = ini.ToString();
}

bool EnsureKey(ref MyIni ini, string section, string key, string defVal)
{
    if (!ini.ContainsSection(section) || !ini.ContainsKey(section, key)) { ini.Set(section, key, defVal); return true; }
    return false;
}

string LoadProfile(string key, string defVal)
{
    var ini = new MyIni(); MyIniParseResult res; if (!ini.TryParse(Me.CustomData, out res)) return defVal;
    return ini.Get("MissileProfile", key).ToString(defVal);
}

void SaveProfile(string key, string val){
    var ini = new MyIni(); MyIniParseResult res;
    if (!ini.TryParse(Me.CustomData, out res)) Me.CustomData = "";
    ini.Clear(); ini.TryParse(Me.CustomData, out res);
    ini.Set("MissileProfile", key, val);
    Me.CustomData = ini.ToString();
}

void SaveProfileFloat(string key, float value)
{
    var ini = new MyIni(); MyIniParseResult res;
    ini.TryParse(Me.CustomData, out res);
    ini.Set("MissileProfile", key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    Me.CustomData = ini.ToString();
}

float LoadProfileFloat(string key, float defVal)
{
    var s = LoadProfile(key, defVal.ToString(System.Globalization.CultureInfo.InvariantCulture));
    float v; return float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : defVal;
}

void AdjustProfileFloat(string key, float delta, float min, float max, float defVal){
    var v = LoadProfileFloat(key, defVal) + delta;
    if (v < min) v = min; if (v > max) v = max;
    SaveProfileFloat(key, v);
}

string Cycle(string[] vals, string cur)
{
    if (vals == null || vals.Length == 0) return cur;
    int i = 0; for (; i < vals.Length; i++) if (vals[i].Equals(cur, StringComparison.OrdinalIgnoreCase)) break;
    int j = (i + 1) % vals.Length; return vals[j];
}

void SetMissileBatteryAuto(Rack r)
{
    if (!IsMerged(r)) return; // rack cannot touch post-detach
    var bats = new List<IMyBatteryBlock>();
    GridTerminalSystem.GetBlocksOfType(bats, b => InBay(b, r) && HasMissileToken(b, "Batt"));
    for (int i = 0; i < bats.Count; i++) { try { bats[i].Enabled = true; bats[i].ChargeMode = ChargeMode.Auto; } catch {} }
}

void SetMissileBatteryRecharge(Rack r)
{
    if (!IsMerged(r)) return;
    var bats = new List<IMyBatteryBlock>();
    GridTerminalSystem.GetBlocksOfType(bats, b => InBay(b, r) && HasMissileToken(b, "Batt"));
    for (int i = 0; i < bats.Count; i++) { try { bats[i].Enabled = true; bats[i].ChargeMode = ChargeMode.Recharge; } catch {} }
}

void EnableMissileMerge(Rack r)
{
    var merges = new List<IMyShipMergeBlock>();
    GridTerminalSystem.GetBlocksOfType(merges, m => InBay(m, r) && HasMissileToken(m, "Merge"));
    for (int i = 0; i < merges.Count; i++) { try { merges[i].Enabled = true; } catch { } }
}

void DisableMissileMerge(Rack r)
{
    var merges = new List<IMyShipMergeBlock>();
    GridTerminalSystem.GetBlocksOfType(merges, m => InBay(m, r) && HasMissileToken(m, "Merge"));
    for (int i = 0; i < merges.Count; i++) { try { merges[i].Enabled = false; } catch {} }
}

// === Artificial Mass test helpers (bay-scoped; type+grid+prefix) ===
void MassOn_Bay(Rack r)
{
    var masses = new List<IMyArtificialMassBlock>();
    GridTerminalSystem.GetBlocksOfType(masses, m => InBay(m, r) && HasMissileToken(m, "Mass"));
    for (int i = 0; i < masses.Count; i++) { try { masses[i].Enabled = true; } catch {} }
}
void MassOff_Bay(Rack r)
{
    var masses = new List<IMyArtificialMassBlock>();
    GridTerminalSystem.GetBlocksOfType(masses, m => InBay(m, r) && HasMissileToken(m, "Mass"));
    for (int i = 0; i < masses.Count; i++) { try { masses[i].Enabled = false; } catch {} }
}

// == Ad-hoc persistent diagnostics ==
string _diagEcho = null;
void SetDiagEcho(string s){ _diagEcho = s; }
void ClearDiagEcho(){ _diagEcho = null; }

string RenderBatteryDiag(Rack r)
{
    var sb = new StringBuilder(1024);
    sb.AppendLine("== BATS CHECK (BAY) ==");
    if (r == null) { sb.AppendLine("FAIL:RACK_MISSING"); return sb.ToString(); }

    bool mergeConnected = IsMerged(r);
    sb.AppendLine("MergeConnected: " + (mergeConnected ? "Y" : "N"));

    var bats = new List<IMyBatteryBlock>();
    GridTerminalSystem.GetBlocksOfType(bats, b => InBay(b, r) && HasMissileToken(b, "Batt"));

    int nB = bats.Count, nAuto = 0, nRech = 0, nDis = 0;
    for (int i = 0; i < bats.Count; i++) {
        try {
            var m = bats[i].ChargeMode;
            if (m == ChargeMode.Auto) nAuto++;
            else if (m == ChargeMode.Recharge) nRech++;
            else if (m == ChargeMode.Discharge) nDis++;
        } catch {}
    }
    sb.Append("Bats: ").Append(nB)
      .Append("   Modes: AUTO:").Append(nAuto)
      .Append(" RECHARGE:").Append(nRech)
      .Append(" DISCHARGE:").Append(nDis).AppendLine();

    // Missile PB presence in bay
    var pbs = new List<IMyProgrammableBlock>();
    GridTerminalSystem.GetBlocksOfType(pbs, pb => InBay(pb, r));
    bool missilePbOnGrid = false;
    for (int i = 0; i < pbs.Count; i++) if (HasMissileToken(pbs[i], "ProgBlock")) { missilePbOnGrid = true; break; }
    sb.AppendLine("MissilePBInBay: " + (missilePbOnGrid ? "Y" : "N"));

    if (!mergeConnected) { sb.AppendLine("Conclusion: FAIL:GATE_ORDER (check before detach)"); return sb.ToString(); }
    if (nB == 0) { sb.AppendLine("Conclusion: FAIL:NO_BATTERIES_IN_BAY"); return sb.ToString(); }
    if (nAuto == 0 && (nRech > 0 || nDis > 0)) { sb.AppendLine("Conclusion: FAIL:BATTERY_MODE (not AUTO at gate)"); return sb.ToString(); }

    sb.AppendLine("Conclusion: OK");
    sb.AppendLine("Hint: Run at eject gate: doors OPEN, pistons LAUNCH, merge CONNECTED.");
    return sb.ToString();
}


string RenderGridCheck(Rack r){
    var sb = new StringBuilder(1024);
    sb.AppendLine("== GRID CHECK ==");
    if (r == null){ sb.AppendLine("Rack:<null>"); return sb.ToString(); }

    // Rack merge + merged state
    sb.Append("RackMerge: ");
    if (r.Merge == null) sb.AppendLine("<null>");
    else sb.Append((r.Merge.Enabled ? "EN " : "DIS ") + "GridId=" + r.Merge.CubeGrid.EntityId).AppendLine();
    sb.AppendLine("IsMerged: " + (IsMerged(r) ? "Y" : "N"));

    // Bay-scoped: Artificial Mass in the bay (≤4 m, same construct, M-*)
    {
        var vm = new List<IMyArtificialMassBlock>();
        GridTerminalSystem.GetBlocksOfType(vm, m => InBay(m, r) && HasMissileToken(m, "Mass"));
        sb.AppendLine("Mass in bay (≤4m): " + vm.Count);
        for (int i = 0; i < vm.Count && i < 3; i++)
        {
            var tb = (IMyTerminalBlock)vm[i];
            var fb = (IMyFunctionalBlock)vm[i];
            sb.Append(" - ").Append(tb.CustomName ?? "<noname>")
              .Append(" Enabled=").Append(fb.Enabled ? "Y" : "N").AppendLine();
        }
    }

    return sb.ToString();
}

// Add to helpers section
List<T> FindAll<T>(Func<T, bool> pred) where T : class, IMyTerminalBlock {
    var list = new List<T>();
    GridTerminalSystem.GetBlocksOfType(list, b => b != null && pred(b));
    return list;
}
bool DoorsFullyOpen(Rack r) {
    if (r.Doors == null || r.Doors.Count == 0) return false;
    for (int i = 0; i < r.Doors.Count; i++) {
        if (r.Doors[i].Status != DoorStatus.Open) return false; // Opening is not enough
    }
    return true;
}

T FindOneByPrefix<T>(string prefix) where T : class, IMyTerminalBlock {
    var list = new List<T>();
    GridTerminalSystem.GetBlocksOfType(list, b =>
        b != null &&
        b.IsSameConstructAs(Me) &&
        b.CustomName != null &&
        b.CustomName.StartsWith(prefix, StringComparison.Ordinal));
    if (list.Count == 0) return null;
    list.Sort((a,b)=>string.Compare(a.CustomName,b.CustomName,StringComparison.Ordinal));
    return list[0];
}

List<T> FindAllByPrefix<T>(string prefix) where T : class, IMyTerminalBlock {
    var list = new List<T>();
    GridTerminalSystem.GetBlocksOfType(list, b =>
        b != null &&
        b.IsSameConstructAs(Me) &&
        b.CustomName != null &&
        b.CustomName.StartsWith(prefix, StringComparison.Ordinal));
    list.Sort((a,b)=>string.Compare(a.CustomName,b.CustomName,StringComparison.Ordinal));
    return list;
}

IMyProgrammableBlock FindMissilePB(Rack r) {
    var pbs = new List<IMyProgrammableBlock>();
    GridTerminalSystem.GetBlocksOfType(pbs, pb =>
        pb != null && pb.IsFunctional && InBay(pb, r));
    for (int i = 0; i < pbs.Count; i++) if (HasMissileToken(pbs[i], "ProgBlock")) return pbs[i];
    return pbs.Count > 0 ? pbs[0] : null;
}

bool InGroup(IMyTerminalBlock b, string groupName) {
    var groups = new List<IMyBlockGroup>();
    GridTerminalSystem.GetBlockGroups(groups, g => g.Name == groupName);
    if (groups.Count == 0) return false;
    var tmp = new List<IMyTerminalBlock>();
    groups[0].GetBlocks(tmp);
    for (int i = 0; i < tmp.Count; i++) if (tmp[i] == b) return true;
    return false;
}

Rack GetRack(string tag) {
    for (int i = 0; i < Racks.Count; i++) if (Racks[i].Tag == tag) return Racks[i];
    return null;
}
bool EqualsCI(string a, string b) {
    return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}

// Resolve pistons by digits 1 and 2 that appear AFTER the prefix; ignore word content.
void ResolvePistonsByDigit(Rack r) {
    r.Piston1 = null; r.Piston2 = null;
    r.Fault = FaultType.NONE;

    var all = FindAllByPrefix<IMyPistonBase>(r.Prefix);
    var candidates1 = new List<IMyPistonBase>();
    var candidates2 = new List<IMyPistonBase>();

    for (int i = 0; i < all.Count; i++) {
        var name = all[i].CustomName ?? "";
        int digit = ExtractSingleDigitAfterPrefix(name, r.Prefix);
        if (digit == 1) candidates1.Add(all[i]);
        else if (digit == 2) candidates2.Add(all[i]);
    }

    if (candidates1.Count == 0 || candidates2.Count == 0) {
        r.Fault = FaultType.PISTON_MISSING;
    } else if (candidates1.Count > 1 || candidates2.Count > 1) {
        r.Fault = FaultType.PISTON_DUP;
        // pick stable firsts to allow partial operation and LCD visibility
        candidates1.Sort((a,b)=>string.Compare(a.CustomName,b.CustomName,StringComparison.Ordinal));
        candidates2.Sort((a,b)=>string.Compare(a.CustomName,b.CustomName,StringComparison.Ordinal));
        r.Piston1 = candidates1[0];
        r.Piston2 = candidates2[0];
    } else {
        r.Piston1 = candidates1[0];
        r.Piston2 = candidates2[0];
    }
}

// Returns 1 or 2 if a single standalone digit is found after prefix; else -1.
// Standalone means not adjacent to another digit (rejects 10, 21, etc.).
int ExtractSingleDigitAfterPrefix(string name, string prefix) {
    if (name == null) return -1;
    int start = (name.StartsWith(prefix, StringComparison.Ordinal) ? prefix.Length : 0);
    for (int i = start; i < name.Length; i++) {
        char c = name[i];
        if (c >= '0' && c <= '9') {
            // reject if neighbor is also a digit
            bool leftDigit  = (i-1 >= start) && char.IsDigit(name[i-1]);
            bool rightDigit = (i+1 <  name.Length) && char.IsDigit(name[i+1]);
            if (leftDigit || rightDigit) continue;
            if (c == '1') return 1;
            if (c == '2') return 2;
        }
    }
    return -1;
}

string BuildDiagEcho(Rack r) {
    var d = r.Diag;
    var sb = new StringBuilder(512);
    sb.AppendLine("=== LIGHTNING RACK DIAG ===");
    sb.Append("Rack: (").Append(r.Tag).Append(")   Step: ")
      .Append(d.Step).Append("/14   Auto: ").Append(d.Auto ? "ON" : "OFF").AppendLine();
    sb.Append("Status: ").Append(d.Status)
      .Append("    Why: ").Append(string.IsNullOrEmpty(d.Why) ? "-" : d.Why).AppendLine();
    sb.Append("Elapsed: ").Append(d.Clock.ToString("0.0")).Append("s  LastRem: ")
      .Append(d.LastRem < 0 ? "-" : d.LastRem.ToString()).AppendLine();

    // Short hint for the current step
    string[] hints = {
        "Idle safe","Doors CLOSED","Pistons BUILD","Projector ON","Welders ON",
        "Build ACTIVE","Build DONE","Merge ENABLED","Merge CONNECT","Doors OPEN",
        "Pistons LAUNCH","Launch","Pistons BUILD","Doors CLOSE","Primed checks"
    };
    if (d.Step >= 0 && d.Step < hints.Length) {
        sb.Append("Next target: ").Append(hints[d.Step]).AppendLine();
    }

    sb.AppendLine();
    sb.AppendLine("Commands:");
    sb.AppendLine("STEP " + r.Tag + " NEXT");
    sb.AppendLine("STEP " + r.Tag + " PREV");
    sb.AppendLine("STEP " + r.Tag + " GOTO <n>");
    sb.AppendLine("STEP " + r.Tag + " AUTO ON");
    sb.AppendLine("STEP " + r.Tag + " AUTO OFF");
    sb.AppendLine("STEP " + r.Tag + " RESET");
    sb.AppendLine("STEP " + r.Tag + " EXIT");
    sb.AppendLine();
    sb.AppendLine("Step map:");
    sb.AppendLine("0 Idle safe    1 Doors CLOSED   2 Pistons BUILD   3 Projector ON");
    sb.AppendLine("4 Welders ON   5 Build ACTIVE   6 Build DONE      7 Merge ENABLED");
    sb.AppendLine("8 Merge CONNECT 9 Doors OPEN   10 Pistons LAUNCH 11 Launch");
    sb.AppendLine("12 Pistons BUILD 13 Doors CLOSE 14 Primed checks");
    return sb.ToString();
}

void ShowDiagEcho() {
    // Stepper echo has priority
    for (int i = 0; i < Racks.Count; i++) {
        if (Racks[i].Diag.Active) { Echo(BuildDiagEcho(Racks[i])); return; }
    }
    // Ad-hoc persistent diagnostics
    if (!string.IsNullOrEmpty(_diagEcho)) { Echo(_diagEcho); return; }

    Echo("OK");
}
