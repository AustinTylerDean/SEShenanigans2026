// LDC1 ARGOS V037 / DpsBirthSeamIgnore / From LDC1 V036
// Space Engineers Programmable Block script
// C# 6-compatible. No LINQ in hot paths.
//
// ARGOS role:
//   - Entity identity seed: infer entity tag from this PB name or clean Custom Data.
//   - Self-tag: add [ARG] only to this ARGOS PB, just after the entity tag.
//   - ADS: cached door/gate/hangar close automation.
//   - Tagging: DRYRUN/TAG first, then ACCEPT stores scope and enables cautious AutoTag maintenance.
//
// PB Budget Gate:
//   - Update10 path closes cached managed doors only and advances one bounded task step.
//   - No broad GridTerminalSystem scan in the door close loop.
//   - Broad scans are command/task driven and phased with instruction checks.
//   - TAG is DRYRUN-gated, uses the frozen dry-run ledger, and still rechecks safety.
//   - AutoTag may run with connected merge seams only after seam classification is clean; far-side and ambiguous candidates are skipped.
//   - V004 resumes TAG/AutoTag after merge-firewall classification binds; far-side and ambiguous merge candidates remain blocked.
//   - STATUS is safe refresh/report only: no tags, no acceptance, no user config overwrite.
//   - Managed Custom Data report sections are replaced by marker; user config remains the source of truth.
//
// Safety model:
//   - Seed is this ARGOS PB grid.
//   - Ownership walks mechanical links only: rotors, hinges, pistons.
//   - Connectors are never traversal links.
//   - Connected merge blocks are treated as boundary seams.
//   - Any connected merge pair is a hard ownership boundary.
//   - The merge block on the ARGOS/PB side may be local; the opposite merge block is not local.
//   - Blocks beyond the merge seam are skipped; ambiguous geometry is skipped.
//   - Existing leading non-entity bracket tags are protected and never modified.
//   - ARGOS never strips ID/entity tags and never stacks this entity tag onto another leading tag.
//
// Commands:
//   HELP / STATUS      Safe refresh/report. Rebuilds managed Custom Data report only; no rename/config mutation.
//   AUDIT              Deeper scope/classification report; no rename.
//   DRYRUN             Preview untagged safe-local blocks that would receive the entity tag.
//   TAG                Apply entity tag only to safe-local untagged blocks after dry-run.
//   ADS_SCAN           Rebuild cached managed door list.
//   ADS_ON / ADS_OFF
//   ADS_CLOSE_NOW      Close cached managed doors immediately.
//   PAIR_SCAN          Rebuild normal-door interlock groups from cached managed doors.
//   PAIR_ON / PAIR_OFF Toggle normal-door interlock enforcement.
//   ACCEPT             Store current clean scope and enable AutoTag maintenance.
//   CLEAR_SCOPE        Forget accepted scope and disable AutoTag.
//   AUTOTAG_OFF        Disable accepted-scope AutoTag maintenance. AUTOTAG_ON remains as an alias.
//
// TEMP V004 DIAGNOSTIC LAYER - REMOVE/COMPACT AFTER MERGE FIREWALL LEDGER V3:
//   - Connected merge seams are traced in managed Custom Data reports.
//   - TAG/AutoTag may continue only for blocks classified LOCAL.
//   - FAR/AMBIGUOUS merge-side candidates are skipped, never renamed.
//   - V037 keeps seam-safe mechanical scope and ignores DPS bay birth seams connected to untagged or DCS-serial drone merges.

const string ARG_TAG = "[ARG]";
const string DEFAULT_NAME = "UNNAMED";
const string DEFAULT_TAG = "";
const string SCRIPT_NAME = "ARGOS V037 LDC1";
const int INSTRUCTION_MARGIN = 22000;
const string ARGOS_STATUS_BEGIN = "# <ARGOS_STATUS_BEGIN>";
const string ARGOS_STATUS_END = "# <ARGOS_STATUS_END>";
const int SAMPLE_LIMIT = 8;
const int ECHO_HOLD_TICKS = 80;
const double DEFAULT_CLOSE_DELAY = 0.5;
const double ADS_OPEN_RATIO = 0.95;
const double ADS_CLOSED_RATIO = 0.05;
const double AUTOTAG_AUDIT_SECONDS = 30.0;
const double ADS_RESCAN_SECONDS = 60.0;
const double DOOR_PAIR_REPAIR_SECONDS = 5.0;
const double DEFAULT_INTERLOCK_MAX_GAP_METERS = 5.0;
const double DEFAULT_INTERLOCK_MAX_LATERAL_METERS = 2.0;
const double DEFAULT_INTERLOCK_AUTO_OPEN_DELAY_SECONDS = 0.5;

string _entityName = DEFAULT_NAME;
string _entityTag = DEFAULT_TAG;
string _entityPrefix = "";
bool _identityFault = false;
string _identityFaultText = "";

bool _adsEnabled = true;
bool _manageDoors = true;
bool _manageHangarDoors = true;
bool _manageGates = true;
bool _manageUntaggedLocalDoors = false;
string _manualManagedTag = "[ADS]";
string _excludeTag = "[NOADS]";
double _closeDelay = DEFAULT_CLOSE_DELAY;
bool _doorInterlockEnabled = true;
double _doorInterlockMaxGapMeters = DEFAULT_INTERLOCK_MAX_GAP_METERS;
double _doorInterlockMaxLateralMeters = DEFAULT_INTERLOCK_MAX_LATERAL_METERS;
int _doorPairCandidateCount = 0;
int _doorPairUnpairedCount = 0;
int _doorPairAmbiguousCount = 0;
StringBuilder _doorPairDiag = new StringBuilder();
string _doorPairDiagBrief = "";
string _doorInterlockExcludeTag = "[NOPAIR]";
bool _doorInterlockPowerLock = true;
bool _doorInterlockAutoCycle = true;
double _doorInterlockAutoOpenDelaySeconds = DEFAULT_INTERLOCK_AUTO_OPEN_DELAY_SECONDS;

bool _autoTagEnabled = false;
bool _tagNewLocalBlocks = false;
bool _autoTagAllowCleanExpansion = true;
bool _autoTagIgnoreFarSideMergeSeams = true;
bool _mergeFirewallTraceEnabled = true;

List<IMyTerminalBlock> _blocks = new List<IMyTerminalBlock>();
List<IMyDoor> _allDoors = new List<IMyDoor>();
List<IMyDoor> _managedDoors = new List<IMyDoor>();
List<DoorGroup> _doorGroups = new List<DoorGroup>();
Dictionary<long, int> _doorOpenTick = new Dictionary<long, int>();
Dictionary<long, bool> _doorPowerLocked = new Dictionary<long, bool>();
Dictionary<long, bool> _ownedGridIds = new Dictionary<long, bool>();
Dictionary<long, double> _openSeconds = new Dictionary<long, double>();
List<IMyShipMergeBlock> _connectedMerges = new List<IMyShipMergeBlock>();
List<MergePair> _mergePairs = new List<MergePair>();
Dictionary<long, bool> _pairedMergeIds = new Dictionary<long, bool>();
List<MechLink> _mechLinks = new List<MechLink>();
Dictionary<long, bool> _mechLinkIds = new Dictionary<long, bool>();
StringBuilder _report = new StringBuilder();
StringBuilder _sample = new StringBuilder();
StringBuilder _protectedSample = new StringBuilder();
StringBuilder _foreignSample = new StringBuilder();
StringBuilder _mergeFarSample = new StringBuilder();
StringBuilder _ambiguousSample = new StringBuilder();
StringBuilder _mergeFirewallTrace = new StringBuilder();
StringBuilder _echoHold = new StringBuilder();
List<string> _idTags = new List<string>();
List<int> _idCounts = new List<int>();
List<string> _localIdTags = new List<string>();
List<int> _localIdCounts = new List<int>();
List<string> _nonLocalIdTags = new List<string>();
List<int> _nonLocalIdCounts = new List<int>();
Dictionary<long, bool> _dryCandidateIds = new Dictionary<long, bool>();

bool _scopeAccepted = false;
string _acceptedSignature = "";
bool _acceptedScopeV2 = false;
int _acceptedOwnedGridMax = -1;
int _acceptedMechanicalLinkMax = -1;
List<string> _acceptedSeamKeys = new List<string>();
string _lastDrySignature = "";
int _lastDrySafe = -1;
int _lastDryAmbPairs = 0;
int _lastDryAmbBlocks = 0;
string _autoTagStopReason = "";

TaskKind _task = TaskKind.None;
TaskPhase _phase = TaskPhase.None;
bool _taskApply = false;
int _taskIndex = 0;
int _scopePass = 0;
bool _scopeChanged = false;
int _pairI = 0;

int _safeUntagged = 0;
int _safeAlreadyTagged = 0;
int _protectedTagged = 0;
int _skippedForeignGrid = 0;
int _skippedMergeFar = 0;
int _skippedMergeAmbiguous = 0;
int _untaggedAll = 0;
int _unknownLeadingTags = 0;
int _changed = 0;
int _failed = 0;
int _scanned = 0;
int _mergeConnected = 0;
int _mergePairCount = 0;
int _mergeAmbiguousPairs = 0;
int _farSideSeamsIgnored = 0;
int _localDynamicSeamsIgnored = 0;
int _mechanicalLinks = 0;
int _ownedGridCount = 0;
int _mechanicalLinksBlocked = 0;
int _mechanicalLinksAmbiguous = 0;

int _doorManaged = 0;
int _doorClosedThisRun = 0;
int _doorCloseNowCount = 0;
int _doorIgnored = 0;
int _doorExplicit = 0;
int _doorEntityTagged = 0;
int _doorGroupsBuilt = 0;
int _doorInterlockClosed = 0;
int _doorInterlockLocked = 0;
int _doorInterlockUnlocked = 0;
int _doorInterlockCycled = 0;
int _doorInterlockTick = 0;

double _autoTagTimer = 0.0;
double _adsScanTimer = ADS_RESCAN_SECONDS;
double _pairRepairTimer = DOOR_PAIR_REPAIR_SECONDS;
bool _dryRunCompleted = false;
int _echoHoldTicks = 0;
string _lastCommand = "NONE";
string _lastStatus = "Booting.";
int _hiInstr = 0;
string _hiWhere = "BOOT";
int _budgetStops = 0;

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    LoadConfig();
    EnsureArgSelfTag();
    WriteCleanConfig(false);
    _lastStatus = _identityFault ? _identityFaultText : "Ready. Run DRYRUN before TAG.";
    LoadAcceptedScope();
    if (_autoTagEnabled && _tagNewLocalBlocks && (!_scopeAccepted || !_acceptedScopeV2))
    {
        _autoTagEnabled = false;
        _tagNewLocalBlocks = false;
        WriteCleanConfig(true);
        _lastStatus = "AutoTag disabled: accepted scope required. Run DRYRUN, TAG if needed, then ACCEPT.";
    }
    if (!_identityFault && _adsEnabled)
    {
        StartTask(TaskKind.AdsScan, false);
        if (!_lastStatus.StartsWith("AutoTag disabled")) _lastStatus = "Startup ADS scan queued.";
    }
}

public void Save()
{
    // Storage is written immediately by ACCEPT/CLEAR_SCOPE.
}

public void Main(string argument, UpdateType updateSource)
{
    int startInstr = Runtime.CurrentInstructionCount;
    try
    {
        MainImpl(argument, updateSource);
    }
    catch (Exception e)
    {
        _lastStatus = "ERROR: " + e.Message;
        EchoStatus();
        Echo(e.ToString());
    }
    int used = Runtime.CurrentInstructionCount - startInstr;
    if (used > _hiInstr)
    {
        _hiInstr = used;
        _hiWhere = _lastCommand + "/" + _phase.ToString();
    }
}

void MainImpl(string argument, UpdateType updateSource)
{
    string raw = argument == null ? "" : argument.Trim();
    bool periodic = (updateSource & (UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100)) != 0;
    double dt = Runtime.TimeSinceLastRun.TotalSeconds;
    if (dt < 0 || dt > 10) dt = 0;

    if (raw.Length > 0)
    {
        HandleCommand(raw);
        return;
    }

    if (periodic)
    {
        if (_adsEnabled) TickAds(dt);
        if (_adsEnabled) TickAdsAutoScan(dt);
        if (_autoTagEnabled && _autoTagStopReason.Length == 0) TickAutoTagAudit(dt);
        StepTask();
        if (_echoHoldTicks > 0 && _echoHold.Length > 0)
        {
            Echo(_echoHold.ToString());
            _echoHoldTicks--;
        }
        else EchoStatus();
    }
}

void HandleCommand(string raw)
{
    string cmd = raw.ToUpperInvariant();
    _lastCommand = cmd;

    if (cmd == "HELP" || cmd == "?") { ShowHelp(); return; }
    if (cmd == "STATUS") { StartStatusReport(); return; }

    if (cmd == "AUDIT") { StartTask(TaskKind.Audit, false); StepTaskBudgeted(); EchoStatus(); return; }
    if (cmd == "DRYRUN" || cmd == "TAG_DRYRUN" || cmd == "PREVIEW") { StartTask(TaskKind.TagDryRun, false); StepTaskBudgeted(); EchoStatus(); return; }
    if (cmd == "TAG" || cmd == "TAG_NOW")
    {
        if (!_dryRunCompleted || _dryCandidateIds.Count == 0)
        {
            _lastStatus = "TAG blocked. Run DRYRUN first and review the report.";
            EchoStatus();
            return;
        }
        StartTask(TaskKind.TagNow, true); StepTaskBudgeted(); EchoStatus(); return;
    }
    if (cmd == "ADS_SCAN") { StartTask(TaskKind.AdsScan, false); StepTaskBudgeted(); EchoStatus(); return; }

    if (cmd == "ACCEPT" || cmd == "ACCEPT_SCOPE") { AcceptScopeCommand(); EchoStatus(); return; }
    if (cmd == "CLEAR_SCOPE") { ClearAcceptedScope(); _autoTagEnabled = false; _tagNewLocalBlocks = false; WriteCleanConfig(true); _lastStatus = "Accepted scope cleared. AutoTag disabled."; EchoStatus(); return; }

    if (cmd == "ADS_ON") { _adsEnabled = true; _adsScanTimer = ADS_RESCAN_SECONDS; if (_task == TaskKind.None) StartTask(TaskKind.AdsScan, false); _lastStatus = "ADS enabled. Scan queued."; EchoStatus(); return; }
    if (cmd == "ADS_OFF") { _adsEnabled = false; _openSeconds.Clear(); ReleaseAllInterlockLocks(); _lastStatus = "ADS disabled."; EchoStatus(); return; }
    if (cmd == "ADS_CLOSE_NOW") { CloseManagedDoorsNow(); _lastStatus = "ADS_CLOSE_NOW complete."; EchoStatus(); return; }
    if (cmd == "PAIR_SCAN") { BuildDoorInterlockGroups(); _lastStatus = "PAIR_SCAN complete. " + _doorPairDiagBrief; EchoStatus(); return; }
    if (cmd == "PAIR_REPORT") { HoldEcho(_doorPairDiag.Length > 0 ? _doorPairDiag.ToString() : "No pair diagnostic yet. ADS scan will build it."); return; }
    if (cmd == "PAIR_ON") { _doorInterlockEnabled = true; WriteCleanConfig(true); BuildDoorInterlockGroups(); _lastStatus = "Door interlock enabled. " + _doorPairDiagBrief; EchoStatus(); return; }
    if (cmd == "PAIR_OFF") { _doorInterlockEnabled = false; ReleaseAllInterlockLocks(); WriteCleanConfig(true); _doorGroups.Clear(); _doorGroupsBuilt = 0; _lastStatus = "Door interlock disabled."; EchoStatus(); return; }

    if (cmd == "AUTOTAG_ON")
    {
        if (!_scopeAccepted || !_acceptedScopeV2)
        {
            _lastStatus = "AUTOTAG_ON blocked. Run DRYRUN, TAG if needed, then ACCEPT.";
            EchoStatus();
            return;
        }
        _autoTagEnabled = true;
        _tagNewLocalBlocks = true;
        _autoTagStopReason = "";
        WriteCleanConfig(true);
        _lastStatus = "AutoTag maintenance enabled for accepted scope.";
        EchoStatus();
        return;
    }
    if (cmd == "AUTOTAG_OFF") { _autoTagEnabled = false; _tagNewLocalBlocks = false; WriteCleanConfig(true); _lastStatus = "AutoTag disabled."; EchoStatus(); return; }

    _lastStatus = "Unknown command: " + raw;
    EchoStatus();
}

void StartStatusReport()
{
    // PB-wide standard: STATUS is safe refresh/report only.
    // It must not mutate config, accepted scope, block names, or ledgers.
    StartTask(TaskKind.Audit, false);
    _lastStatus = "STATUS report queued. Wait for ARGOS_STATUS in Custom Data.";
    StepTaskBudgeted();
    if (_task == TaskKind.None) _lastStatus = "STATUS report complete. Managed Custom Data report updated.";
    EchoStatus();
}

void StepTaskBudgeted()
{
    int guard = 0;
    while (_task != TaskKind.None && guard < 24 && !NearLimit())
    {
        StepTask();
        guard++;
    }
}

void TickAds(double dt)
{
    _doorClosedThisRun = 0;
    _doorInterlockClosed = 0;
    _doorInterlockLocked = 0;
    _doorInterlockUnlocked = 0;
    _doorInterlockCycled = 0;
    _doorInterlockTick++;
    // ARGOS V011: ADS_SCAN is command-driven for now.
    // Do not let a periodic ADS rescan reset tag dry-run context or stack broad work
    // on top of the identity classifier. Cached doors still close when present.
    for (int i = 0; i < _managedDoors.Count; i++)
    {
        IMyDoor d = _managedDoors[i];
        if (d == null) continue;
        double ratio = d.OpenRatio;
        long id = d.EntityId;
        if (ratio >= ADS_OPEN_RATIO)
        {
            double seconds;
            if (!_openSeconds.TryGetValue(id, out seconds)) seconds = 0;
            seconds += dt;
            _openSeconds[id] = seconds;
            if (seconds >= _closeDelay)
            {
                d.CloseDoor();
                _openSeconds[id] = 0;
                _doorClosedThisRun++;
            }
        }
        else if (ratio <= ADS_CLOSED_RATIO)
        {
            if (_openSeconds.ContainsKey(id)) _openSeconds.Remove(id);
        }
        if (NearLimit()) { _budgetStops++; break; }
    }
    if (_doorInterlockEnabled && _doorGroups.Count > 0 && !NearLimit()) TickDoorInterlock(dt);
}

void TickAutoTagAudit(double dt)
{
    _autoTagTimer += dt;
    if (_autoTagTimer < AUTOTAG_AUDIT_SECONDS) return;
    if (_task != TaskKind.None) return;
    _autoTagTimer = 0;

    if (_tagNewLocalBlocks)
    {
        if (!_scopeAccepted)
        {
            SuspendAutoTag("no accepted scope");
            return;
        }
        StartTask(TaskKind.AutoTagMaintain, true);
    }
    else
    {
        StartTask(TaskKind.TagDryRun, false);
    }
}

void TickAdsAutoScan(double dt)
{
    if (_task != TaskKind.None) return;

    bool needInitialScan = _managedDoors.Count == 0;
    if (needInitialScan)
    {
        StartTask(TaskKind.AdsScan, false);
        _adsScanTimer = 0;
        _pairRepairTimer = 0;
        _lastStatus = "Auto ADS scan queued.";
        return;
    }

    // V025: interlock pairing is self-maintaining. If the cache has managed doors but
    // no usable pairs, queue a bounded ADS/pair rescan automatically instead of
    // requiring PAIR_SCAN after a rebuild/recompile.
    if (_doorInterlockEnabled && _managedDoors.Count >= 2 && _doorGroups.Count == 0)
    {
        _pairRepairTimer += dt;
        if (_pairRepairTimer >= DOOR_PAIR_REPAIR_SECONDS)
        {
            StartTask(TaskKind.AdsScan, false);
            _pairRepairTimer = 0;
            _adsScanTimer = 0;
            _lastStatus = "Auto pair scan queued.";
            return;
        }
    }
    else _pairRepairTimer = DOOR_PAIR_REPAIR_SECONDS;

    _adsScanTimer += dt;
    if (_adsScanTimer >= ADS_RESCAN_SECONDS)
    {
        StartTask(TaskKind.AdsScan, false);
        _adsScanTimer = 0;
        _pairRepairTimer = 0;
        _lastStatus = "Periodic ADS scan queued.";
    }
}

void StartTask(TaskKind kind, bool apply)
{
    if (_identityFault && (kind == TaskKind.TagNow || kind == TaskKind.TagDryRun || kind == TaskKind.AdsScan || kind == TaskKind.Audit))
    {
        _lastStatus = _identityFaultText;
        return;
    }
    if (kind == TaskKind.TagDryRun)
    {
        _dryCandidateIds.Clear();
        _dryRunCompleted = false;
    }

    _task = kind;
    _taskApply = apply;
    _phase = TaskPhase.RefreshBlocks;
    _taskIndex = 0;
    _scopePass = 0;
    _scopeChanged = false;
    _pairI = 0;
    ResetCounters();
    _sample.Clear();
    _protectedSample.Clear();
    _foreignSample.Clear();
    _mergeFarSample.Clear();
    _ambiguousSample.Clear();
    _mergeFirewallTrace.Clear();
    _report.Clear();
    _lastStatus = "Task queued: " + kind.ToString();
}

void StepTask()
{
    if (_task == TaskKind.None) return;

    if (_phase == TaskPhase.RefreshBlocks)
    {
        _blocks.Clear();
        GridTerminalSystem.GetBlocks(_blocks);
        _ownedGridIds.Clear();
        if (Me != null && Me.CubeGrid != null) _ownedGridIds[Me.CubeGrid.EntityId] = true;
        _connectedMerges.Clear();
        _mergePairs.Clear();
        _pairedMergeIds.Clear();
        _mechLinks.Clear();
        _mechLinkIds.Clear();
        _scopePass = 0;
        _scopeChanged = true;
        _taskIndex = 0;
        _phase = TaskPhase.BuildMechanicalScope;
        if (NearLimit()) { _budgetStops++; return; }
    }

    if (_phase == TaskPhase.BuildMechanicalScope) BuildMechanicalScopeStep();
    if (NearLimit()) { _budgetStops++; return; }

    if (_phase == TaskPhase.CollectMerges) CollectMergesStep();
    if (NearLimit()) { _budgetStops++; return; }

    if (_phase == TaskPhase.BuildMergePairs) BuildMergePairsStep();
    if (NearLimit()) { _budgetStops++; return; }

    if (_phase == TaskPhase.FilterMechanicalScope) FilterMechanicalScopeStep();
    if (NearLimit()) { _budgetStops++; return; }

    if (_task == TaskKind.AutoTagMaintain && _phase == TaskPhase.ProcessBlocks)
    {
        string scopeProblem = AcceptedScopeProblem();
        if (scopeProblem.Length > 0)
        {
            SuspendAutoTag(scopeProblem);
            _phase = TaskPhase.Done;
        }
    }

    if (_phase == TaskPhase.ProcessBlocks) ProcessBlocksStep();
    if (NearLimit()) { _budgetStops++; return; }

    if (_phase == TaskPhase.Done) FinishTask();
}

void BuildMechanicalScopeStep()
{
    while (_scopePass < 24)
    {
        if (_taskIndex == 0) _scopeChanged = false;

        while (_taskIndex < _blocks.Count)
        {
            IMyTerminalBlock b = _blocks[_taskIndex++];
            if (b == null || b.CubeGrid == null) { if (NearLimit()) return; else continue; }

            IMyMotorStator rotor = b as IMyMotorStator;
            if (rotor != null)
            {
                AddMechanicalLink(rotor, rotor.TopGrid);
                if (AddMechanicalPair(rotor.CubeGrid, rotor.TopGrid)) _scopeChanged = true;
            }
            else
            {
                IMyPistonBase piston = b as IMyPistonBase;
                if (piston != null)
                {
                    AddMechanicalLink(piston, piston.TopGrid);
                    if (AddMechanicalPair(piston.CubeGrid, piston.TopGrid)) _scopeChanged = true;
                }
            }
            if (NearLimit()) return;
        }

        _scopePass++;
        if (!_scopeChanged) break;
        _taskIndex = 0;
    }

    _ownedGridCount = _ownedGridIds.Count;
    _taskIndex = 0;
    _phase = TaskPhase.CollectMerges;
}

void AddMechanicalLink(IMyTerminalBlock baseBlock, IMyCubeGrid topGrid)
{
    if (baseBlock == null || baseBlock.CubeGrid == null || topGrid == null) return;
    if (_mechLinkIds.ContainsKey(baseBlock.EntityId)) return;
    MechLink ml = new MechLink();
    ml.Base = baseBlock;
    ml.BaseGridId = baseBlock.CubeGrid.EntityId;
    ml.TopGridId = topGrid.EntityId;
    ml.BasePos = baseBlock.Position;
    _mechLinks.Add(ml);
    _mechLinkIds[baseBlock.EntityId] = true;
}

bool AddMechanicalPair(IMyCubeGrid a, IMyCubeGrid b)
{
    if (a == null || b == null) return false;
    bool ao = IsOwnedGrid(a);
    bool bo = IsOwnedGrid(b);
    if (ao == bo) return false;
    if (ao) _ownedGridIds[b.EntityId] = true;
    else _ownedGridIds[a.EntityId] = true;
    _mechanicalLinks++;
    return true;
}

void CollectMergesStep()
{
    while (_taskIndex < _blocks.Count)
    {
        IMyShipMergeBlock mb = _blocks[_taskIndex++] as IMyShipMergeBlock;
        if (mb != null && mb.CubeGrid != null && IsOwnedGrid(mb.CubeGrid) && IsMergeConnected(mb))
        {
            _connectedMerges.Add(mb);
        }
        if (NearLimit()) return;
    }
    _mergeConnected = _connectedMerges.Count;
    _pairI = 0;
    _phase = TaskPhase.BuildMergePairs;
}

void BuildMergePairsStep()
{
    // Greedy, concise merge seam pairing.
    // If two connected merge blocks are in the mechanically owned candidate set,
    // the nearest unpaired connected merge is treated as its seam partner.
    // This intentionally avoids brittle assumptions about Forward being the functional face.
    while (_pairI < _connectedMerges.Count)
    {
        IMyShipMergeBlock a = _connectedMerges[_pairI++];
        if (a == null || a.CubeGrid == null) { if (NearLimit()) return; else continue; }
        if (_pairedMergeIds.ContainsKey(a.EntityId)) { if (NearLimit()) return; else continue; }

        IMyShipMergeBlock best = null;
        int bestDist = int.MaxValue;
        for (int j = 0; j < _connectedMerges.Count; j++)
        {
            IMyShipMergeBlock b = _connectedMerges[j];
            if (b == null || b.CubeGrid == null) continue;
            if (b.EntityId == a.EntityId) continue;
            if (_pairedMergeIds.ContainsKey(b.EntityId)) continue;
            if (b.CubeGrid.EntityId != a.CubeGrid.EntityId) continue;
            Vector3I delta = b.Position - a.Position;
            int dist = Abs(delta.X) + Abs(delta.Y) + Abs(delta.Z);
            if (dist <= 0) continue;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = b;
            }
        }

        if (best == null)
        {
            _mergeAmbiguousPairs++;
            if (NearLimit()) return;
            continue;
        }

        AddMergePairFromSeam(a, best);
        _pairedMergeIds[a.EntityId] = true;
        _pairedMergeIds[best.EntityId] = true;
        if (NearLimit()) return;
    }

    _mergePairCount = _mergePairs.Count;
    _taskIndex = 0;
    _scopePass = -1;
    _phase = TaskPhase.FilterMechanicalScope;
}

void FilterMechanicalScopeStep()
{
    if (_scopePass < 0)
    {
        _ownedGridIds.Clear();
        if (Me != null && Me.CubeGrid != null) _ownedGridIds[Me.CubeGrid.EntityId] = true;
        _mechanicalLinks = 0;
        _mechanicalLinksBlocked = 0;
        _mechanicalLinksAmbiguous = 0;
        _scopePass = 0;
        _taskIndex = 0;
        _scopeChanged = true;
    }

    while (_scopePass < 24)
    {
        if (_taskIndex == 0) _scopeChanged = false;

        while (_taskIndex < _mechLinks.Count)
        {
            MechLink ml = _mechLinks[_taskIndex++];
            bool baseOwned = IsOwnedGridId(ml.BaseGridId);
            bool topOwned = IsOwnedGridId(ml.TopGridId);
            if (baseOwned == topOwned) { if (NearLimit()) return; else continue; }

            LocalState gate = MechanicalLinkGateState(ml);
            if (gate != LocalState.Local) { if (NearLimit()) return; else continue; }

            if (baseOwned) _ownedGridIds[ml.TopGridId] = true;
            else _ownedGridIds[ml.BaseGridId] = true;
            _mechanicalLinks++;
            _scopeChanged = true;
            if (NearLimit()) return;
        }

        _scopePass++;
        if (!_scopeChanged) break;
        _taskIndex = 0;
    }

    _ownedGridCount = _ownedGridIds.Count;
    CountBlockedMechanicalLinks();
    _taskIndex = 0;
    if (_task == TaskKind.AdsScan)
    {
        _allDoors.Clear();
        GridTerminalSystem.GetBlocksOfType<IMyDoor>(_allDoors);
    }
    _phase = TaskPhase.ProcessBlocks;
}

LocalState MechanicalLinkGateState(MechLink ml)
{
    if (ml == null || ml.Base == null || ml.Base.CubeGrid == null) return LocalState.Ambiguous;
    MergeSide side = MergeSideForBlock(ml.Base);
    if (side == MergeSide.Local) return LocalState.Local;
    if (side == MergeSide.Far) return LocalState.MergeFar;
    return LocalState.Ambiguous;
}

void CountBlockedMechanicalLinks()
{
    _mechanicalLinksBlocked = 0;
    _mechanicalLinksAmbiguous = 0;
    for (int i = 0; i < _mechLinks.Count; i++)
    {
        MechLink ml = _mechLinks[i];
        bool baseOwned = IsOwnedGridId(ml.BaseGridId);
        bool topOwned = IsOwnedGridId(ml.TopGridId);
        if (baseOwned == topOwned) continue;
        LocalState gate = MechanicalLinkGateState(ml);
        if (gate == LocalState.MergeFar) _mechanicalLinksBlocked++;
        else if (gate == LocalState.Ambiguous) _mechanicalLinksAmbiguous++;
    }
}

void AddMergePairFromSeam(IMyShipMergeBlock a, IMyShipMergeBlock b)
{
    if (a == null || b == null || a.CubeGrid == null || b.CubeGrid == null) return;
    Vector3I raw = b.Position - a.Position;
    Vector3I dir = PrimaryAxis(raw);
    if (dir.X == 0 && dir.Y == 0 && dir.Z == 0)
    {
        _mergeAmbiguousPairs++;
        return;
    }

    int seedSide = MergePairSeedSide(a.CubeGrid, a.Position, b.Position, dir);
    if (seedSide == 0)
    {
        // If the exact midpoint is unlucky, choose the nearer merge block as the ARGOS side.
        Vector3I seed = FindLocalSeed(a.CubeGrid);
        int da = DistSq(seed, a.Position);
        int db = DistSq(seed, b.Position);
        if (da == db)
        {
            _mergeAmbiguousPairs++;
            return;
        }
        seedSide = da < db ? Sign(Dot(a.Position - b.Position, dir))
                           : Sign(Dot(b.Position - a.Position, dir));
    }

    if (seedSide == 0)
    {
        _mergeAmbiguousPairs++;
        return;
    }

    MergePair mp = new MergePair();
    mp.A = a;
    mp.B = b;
    mp.Dir = dir;
    mp.SeedSide = seedSide;
    _mergePairs.Add(mp);
}

void ProcessBlocksStep()
{
    if (_task == TaskKind.AdsScan)
    {
        ProcessDoorScanStep();
        return;
    }

    while (_taskIndex < _blocks.Count)
    {
        IMyTerminalBlock b = _blocks[_taskIndex++];
        ProcessOneBlockForTagTask(b);
        if (NearLimit()) return;
    }
    _phase = TaskPhase.Done;
}

void ProcessDoorScanStep()
{
    if (_taskIndex == 0)
    {
        _managedDoors.Clear();
        _doorIgnored = 0;
        _doorExplicit = 0;
        _doorEntityTagged = 0;
    }

    while (_taskIndex < _allDoors.Count)
    {
        IMyDoor d = _allDoors[_taskIndex++];
        if (d == null) { if (NearLimit()) return; else continue; }

        string name = d.CustomName == null ? "" : d.CustomName;
        if (HasTag(name, _excludeTag))
        {
            _doorIgnored++;
            if (NearLimit()) return;
            continue;
        }

        string lead = LeadingBracketTag(name);
        bool manual = HasTag(name, _manualManagedTag);
        bool entityTagged = StartsWithEntityTag(name);

        // ADS trust order:
        //   [NOADS] always wins above.
        //   [ADS] force-manages the door/gate/hangar block.
        //   [ENTITY] manages the door even if the geometry classifier is confused.
        //   Foreign/unknown leading tags are ignored.
        //   Untagged doors use the mechanical/merge classifier.
        if (manual || entityTagged)
        {
            if (manual) _doorExplicit++;
            if (entityTagged) _doorEntityTagged++;
            if (DoorCategoryAllowed(d, manual)) _managedDoors.Add(d);
            else _doorIgnored++;
            if (NearLimit()) return;
            continue;
        }

        if (lead.Length > 0)
        {
            _doorIgnored++;
            if (NearLimit()) return;
            continue;
        }

        if (!_manageUntaggedLocalDoors)
        {
            _doorIgnored++;
            if (NearLimit()) return;
            continue;
        }

        LocalState state = ClassifyLocality(d);
        if (state != LocalState.Local)
        {
            _doorIgnored++;
            if (NearLimit()) return;
            continue;
        }

        if (DoorCategoryAllowed(d, false)) _managedDoors.Add(d);
        else _doorIgnored++;
        if (NearLimit()) return;
    }
    _doorManaged = _managedDoors.Count;
    TrimOpenTracker();
    _phase = TaskPhase.Done;
}

void ProcessOneBlockForTagTask(IMyTerminalBlock b)
{
    if (b == null || b.CubeGrid == null) return;
    _scanned++;
    string name = b.CustomName == null ? "" : b.CustomName;
    string lead = LeadingBracketTag(name);
    LocalState state = ClassifyLocality(b);

    if (lead.Length > 0)
    {
        AddIdCount(_idTags, _idCounts, lead);
        if (state == LocalState.Local) AddIdCount(_localIdTags, _localIdCounts, lead);
        else AddIdCount(_nonLocalIdTags, _nonLocalIdCounts, lead);
    }
    else
    {
        _untaggedAll++;
    }

    if (state == LocalState.ForeignGrid)
    {
        _skippedForeignGrid++;
        AddSample(_foreignSample, name, "foreign grid/mechanical scope");
        return;
    }
    if (state == LocalState.MergeFar)
    {
        _skippedMergeFar++;
        AddSample(_mergeFarSample, name, "past merge seam");
        return;
    }
    if (state == LocalState.Ambiguous)
    {
        _skippedMergeAmbiguous++;
        AddSample(_ambiguousSample, name, "ambiguous merge side");
        return;
    }

    if (StartsWithEntityTag(name))
    {
        _safeAlreadyTagged++;
        return;
    }
    if (lead.Length > 0)
    {
        _protectedTagged++;
        AddSample(_protectedSample, name, "protected leading tag " + lead);
        return;
    }

    if (_task == TaskKind.TagNow && !_dryCandidateIds.ContainsKey(b.EntityId)) return;


    _safeUntagged++;
    if (_task == TaskKind.TagDryRun) _dryCandidateIds[b.EntityId] = true;

    string newName = _entityPrefix + name;
    AddRenameSample(_sample, name, newName);

    if (!_taskApply) return;
    try
    {
        b.CustomName = newName;
        _changed++;
    }
    catch
    {
        _failed++;
    }
}

void FinishTask()
{
    if (_task == TaskKind.AdsScan)
    {
        BuildDoorInterlockGroups();
        _pairRepairTimer = DOOR_PAIR_REPAIR_SECONDS;
        _lastStatus = "ADS scan complete. Managed " + _doorManaged + " " + _doorPairDiagBrief;
    }
    else
    {
        if (_task == TaskKind.AutoTagMaintain && _autoTagStopReason.Length > 0)
            _lastStatus = "AutoTag suspended: " + _autoTagStopReason;
        else
            _lastStatus = _task.ToString() + " complete. Safe untagged " + _safeUntagged + (_taskApply ? " changed " + _changed : "");
        if (_task == TaskKind.TagDryRun)
        {
            _dryRunCompleted = true;
            _lastDrySafe = _safeUntagged;
            _lastDryAmbPairs = _mergeAmbiguousPairs;
            _lastDryAmbBlocks = _skippedMergeAmbiguous;
            _lastDrySignature = BuildScopeSignature();
        }
        if (_task == TaskKind.TagNow)
        {
            if (_failed == 0)
            {
                _dryRunCompleted = true;
                _lastDrySafe = 0;
                _lastDryAmbPairs = _mergeAmbiguousPairs;
                _lastDryAmbBlocks = _skippedMergeAmbiguous;
                _lastDrySignature = BuildScopeSignature();
            }
            else
            {
                _dryRunCompleted = false;
            }
            _adsScanTimer = ADS_RESCAN_SECONDS;
        }
        if (_task == TaskKind.AutoTagMaintain && _changed > 0) _adsScanTimer = ADS_RESCAN_SECONDS;
        BuildTagReport();
        WriteManagedStatusReport(_report.ToString());
        HoldEcho(_report.ToString());
    }
    _task = TaskKind.None;
    _phase = TaskPhase.None;
}

void BuildTagReport()
{
    _report.Clear();
    AppendHeader(_report);
    if (_task == TaskKind.AutoTagMaintain) _report.AppendLine("MODE: AUTOTAG MAINTENANCE");
    else _report.AppendLine(_taskApply ? "MODE: TAG" : "MODE: DRY RUN / AUDIT");
    _report.AppendLine("Entity: " + _entityTag + "  Name: " + _entityName);
    _report.AppendLine("Scope: mechanical only; connectors ignored; merge seams classified");
    _report.AppendLine("");
    _report.AppendLine("IDENTITY CONTEXT");
    _report.Append("All visible leading tags: "); AppendTagCounts(_report, _idTags, _idCounts);
    _report.Append("Entity tag seen in ARGOS-side scan: "); AppendEntityFilterTagCounts(_report, _localIdTags, _localIdCounts, true);
    _report.Append("Protected other tags seen in ARGOS-side scan: "); AppendEntityFilterTagCounts(_report, _localIdTags, _localIdCounts, false);
    _report.Append("Tags outside ARGOS-side scan: "); AppendTagCounts(_report, _nonLocalIdTags, _nonLocalIdCounts);
    _report.AppendLine("Untagged seen: " + _untaggedAll);
    _report.AppendLine("Note: ARGOS-side scan context is not permission to rename tagged blocks.");
    _report.AppendLine("Only untagged safe candidates enter the frozen TAG ledger.");
    _report.AppendLine("Accepted scope: " + (_scopeAccepted ? (_acceptedScopeV2 ? "YES V2" : "YES legacy") : "NO") + (_scopeAccepted ? "" : " (AutoTag maintenance blocked)"));
    if (_acceptedScopeV2) _report.AppendLine("Accepted limits: grids<= " + _acceptedOwnedGridMax + " mech<= " + _acceptedMechanicalLinkMax + " known seams " + _acceptedSeamKeys.Count);
    if (_autoTagStopReason.Length > 0) _report.AppendLine("AutoTag stop: " + _autoTagStopReason);
    _report.AppendLine("");
    _report.AppendLine("Owned grids: " + _ownedGridCount + "  Mechanical links: " + _mechanicalLinks);
    _report.AppendLine("Seam-safe mech blocks: far=" + _mechanicalLinksBlocked + " amb=" + _mechanicalLinksAmbiguous);
    _report.AppendLine("Connected merges: " + _mergeConnected + "  Seam pairs: " + _mergePairCount + "  Ambiguous seams: " + _mergeAmbiguousPairs);
    AppendMergeFirewallTrace(_report);
    AppendMergeEndpointAudit(_report);
    _report.AppendLine("Scanned terminal blocks: " + _scanned);
    _report.AppendLine("Safe already tagged: " + _safeAlreadyTagged);
    _report.AppendLine("Safe untagged candidates: " + _safeUntagged);
    _report.AppendLine("Frozen TAG ledger: " + _dryCandidateIds.Count);
    _report.AppendLine("Protected leading bracket tags: " + _protectedTagged);
    _report.AppendLine("Skipped foreign grid: " + _skippedForeignGrid);
    _report.AppendLine("Skipped past merge seam: " + _skippedMergeFar);
    _report.AppendLine("Skipped ambiguous merge: " + _skippedMergeAmbiguous);
    if (_mergeFirewallTraceEnabled && _mergeConnected > 0) _report.AppendLine("Firewall mode: LOCAL-side AutoTag allowed; FAR/AMBIG skipped.");
    if (_taskApply)
    {
        _report.AppendLine("Changed: " + _changed);
        _report.AppendLine("Failed: " + _failed);
    }
    _report.AppendLine("");
    _report.AppendLine("Sample safe candidates:");
    if (_sample.Length > 0) _report.Append(_sample.ToString());
    else _report.AppendLine("No safe rename candidates.");
    AppendSampleSection(_report, "Sample protected leading tags:", _protectedSample);
    AppendSampleSection(_report, "Sample skipped foreign/mechanical scope:", _foreignSample);
    AppendSampleSection(_report, "Sample skipped past merge seam:", _mergeFarSample);
    AppendSampleSection(_report, "Sample skipped ambiguous merge:", _ambiguousSample);
    _report.AppendLine("");
    if (_mergeAmbiguousPairs > 0 || _skippedMergeAmbiguous > 0)
        _report.AppendLine("WARNING: Ambiguous merge geometry exists. Skipped uncertain blocks.");
    if (!_taskApply) _report.AppendLine("Run TAG only if this report is boring and correct.");
    else if (_task == TaskKind.AutoTagMaintain) _report.AppendLine("AutoTag maintenance only acts when accepted scope still matches and safety rechecks pass.");
    else _report.AppendLine("TAG applied only blocks from the last completed dry-run ledger and rechecked safety.");
}


void AppendMergeFirewallTrace(StringBuilder r)
{
    if (!_mergeFirewallTraceEnabled) return;
    r.AppendLine("[TEMP ARGOS MERGE FIREWALL TRACE - REMOVE AFTER LEDGER V3]");
    r.AppendLine("Rule: connected merge seam present => bind firewall, allow LOCAL candidates only, skip FAR/AMBIG.");
    r.AppendLine("Connected merge blocks seen: " + _connectedMerges.Count + "  seam pairs: " + _mergePairs.Count);
    r.AppendLine("Seam filters: localDynamic=" + _localDynamicSeamsIgnored + " farSide=" + _farSideSeamsIgnored + " mechBlocked=" + _mechanicalLinksBlocked);
    int limit = _connectedMerges.Count < SAMPLE_LIMIT ? _connectedMerges.Count : SAMPLE_LIMIT;
    for (int i = 0; i < limit; i++)
    {
        IMyShipMergeBlock mb = _connectedMerges[i];
        if (mb == null) continue;
        string grid = mb.CubeGrid == null ? "G--" : ("G" + ShortId(mb.CubeGrid.EntityId));
        r.AppendLine("  M" + (i + 1).ToString("00") + " " + grid + " pos " + PosText(mb.Position) + " " + ShortName(mb));
    }
    if (_connectedMerges.Count > limit) r.AppendLine("  ... " + (_connectedMerges.Count - limit) + " more connected merge blocks");
    if (_mergeConnected > 0) r.AppendLine("Firewall action: TAG/AUTOTAG RESUMED for LOCAL side only; far/ambiguous side remains blocked.");
    else r.AppendLine("Firewall action: no connected seams; normal TAG/AutoTag gates apply.");
}

void AppendMergeEndpointAudit(StringBuilder r)
{
    if (_mergePairs.Count == 0)
    {
        r.AppendLine("Merge endpoint audit: no connected merge seam pairs detected.");
        return;
    }
    int local = 0;
    int far = 0;
    int amb = 0;
    int samples = 0;
    StringBuilder sb = new StringBuilder();
    for (int i = 0; i < _mergePairs.Count; i++)
    {
        MergePair mp = _mergePairs[i];
        CountMergeEndpoint(mp.A, ref local, ref far, ref amb, sb, ref samples);
        CountMergeEndpoint(mp.B, ref local, ref far, ref amb, sb, ref samples);
    }
    r.AppendLine("Merge endpoint audit: local=" + local + " farSkipped=" + far + " ambiguous=" + amb);
    if (sb.Length > 0)
    {
        r.AppendLine("Merge endpoint samples:");
        r.Append(sb.ToString());
    }
}

void CountMergeEndpoint(IMyShipMergeBlock mb, ref int local, ref int far, ref int amb, StringBuilder sb, ref int samples)
{
    if (mb == null) return;
    MergeSide side = MergeSideForBlock(mb);
    if (side == MergeSide.Local) local++;
    else if (side == MergeSide.Far) far++;
    else amb++;
    if (samples >= SAMPLE_LIMIT) return;
    string label = side == MergeSide.Local ? "LOCAL" : (side == MergeSide.Far ? "FAR-SKIP" : "AMBIG");
    sb.AppendLine("  " + label + " " + ShortName(mb));
    samples++;
}

LocalState ClassifyLocality(IMyTerminalBlock b)
{
    if (b == null || b.CubeGrid == null) return LocalState.ForeignGrid;
    if (!IsOwnedGrid(b.CubeGrid)) return LocalState.ForeignGrid;
    MergeSide side = MergeSideForBlock(b);
    if (side == MergeSide.Far) return LocalState.MergeFar;
    if (side == MergeSide.Ambiguous) return LocalState.Ambiguous;
    return LocalState.Local;
}

MergeSide MergeSideForBlock(IMyTerminalBlock b)
{
    if (b == null || b.CubeGrid == null) return MergeSide.Ambiguous;
    if (_mergeConnected == 0) return MergeSide.Local;
    bool relevantMergeGrid = false;
    for (int i = 0; i < _connectedMerges.Count; i++)
    {
        IMyShipMergeBlock mb = _connectedMerges[i];
        if (mb != null && mb.CubeGrid != null && mb.CubeGrid.EntityId == b.CubeGrid.EntityId)
        {
            relevantMergeGrid = true;
            break;
        }
    }
    if (!relevantMergeGrid) return MergeSide.Local;

    bool hadPairOnGrid = false;
    for (int i = 0; i < _mergePairs.Count; i++)
    {
        MergePair mp = _mergePairs[i];
        if (mp.A == null || mp.A.CubeGrid == null) continue;
        if (mp.A.CubeGrid.EntityId != b.CubeGrid.EntityId) continue;
        hadPairOnGrid = true;
        int side = Sign(Dot(b.Position + b.Position - mp.A.Position - mp.B.Position, mp.Dir));
        if (side == 0) return MergeSide.Ambiguous;
        if (side != mp.SeedSide) return MergeSide.Far;
    }
    if (!hadPairOnGrid) return MergeSide.Ambiguous;
    return MergeSide.Local;
}

int MergePairSeedSide(IMyCubeGrid grid, Vector3I a, Vector3I b, Vector3I dir)
{
    Vector3I seed = FindLocalSeed(grid);
    return Sign(Dot(seed + seed - a - b, dir));
}

Vector3I FindLocalSeed(IMyCubeGrid grid)
{
    if (grid == null) return new Vector3I(0, 0, 0);
    if (Me != null && Me.CubeGrid != null && Me.CubeGrid.EntityId == grid.EntityId) return Me.Position;
    for (int i = 0; i < _blocks.Count; i++)
    {
        IMyTerminalBlock b = _blocks[i];
        if (b != null && b.CubeGrid != null && b.CubeGrid.EntityId == grid.EntityId && StartsWithEntityTag(b.CustomName)) return b.Position;
    }
    return new Vector3I(0, 0, 0);
}

bool DoorCategoryAllowed(IMyDoor d, bool manual)
{
    if (manual) return true;
    bool specialHangar = IsHangarDoor(d);
    bool specialGate = IsGateDoor(d);
    if (specialHangar) return _manageHangarDoors;
    if (specialGate) return _manageGates;
    return _manageDoors;
}

bool IsHangarDoor(IMyTerminalBlock b)
{
    string def = DoorDefText(b);
    return def.IndexOf("hangar") >= 0 || def.IndexOf("shutter") >= 0;
}

bool IsGateDoor(IMyTerminalBlock b)
{
    string def = DoorDefText(b);
    return def.IndexOf("gate") >= 0;
}

string DoorDefText(IMyTerminalBlock b)
{
    if (b == null) return "";
    return SafeLower(b.DefinitionDisplayNameText) + " " + SafeLower(b.BlockDefinition.SubtypeName) + " " + SafeLower(b.BlockDefinition.TypeIdString);
}


void BuildDoorInterlockGroups()
{
    ReleaseAllInterlockLocks();
    _doorGroups.Clear();
    _doorGroupsBuilt = 0;
    _doorPairCandidateCount = 0;
    _doorPairUnpairedCount = 0;
    _doorPairAmbiguousCount = 0;
    _doorPairDiag.Clear();
    _doorPairDiagBrief = "";
    if (!_doorInterlockEnabled) return;

    List<IMyDoor> candidates = new List<IMyDoor>();
    for (int i = 0; i < _managedDoors.Count; i++)
    {
        IMyDoor d = _managedDoors[i];
        if (!IsInterlockCandidate(d)) continue;
        candidates.Add(d);
        if (NearLimit()) { _budgetStops++; return; }
    }

    int n = candidates.Count;
    if (n < 2)
    {
        _doorPairUnpairedCount = n;
        BuildDoorPairDiag(candidates, new List<DoorPairCandidate>(), new bool[n]);
        return;
    }

    List<DoorPairCandidate> pairs = new List<DoorPairCandidate>();
    int[] plausible = new int[n];
    for (int i = 0; i < n; i++)
    {
        IMyDoor a = candidates[i];
        for (int j = i + 1; j < n; j++)
        {
            IMyDoor b = candidates[j];
            DoorPairCandidate pc;
            if (TryDoorPairCandidate(a, b, i, j, out pc))
            {
                pairs.Add(pc);
                plausible[i]++;
                plausible[j]++;
            }
            if (NearLimit()) { _budgetStops++; break; }
        }
        if (NearLimit()) break;
    }

    _doorPairCandidateCount = pairs.Count;
    for (int i = 0; i < plausible.Length; i++) if (plausible[i] > 1) _doorPairAmbiguousCount++;

    pairs.Sort(delegate(DoorPairCandidate x, DoorPairCandidate y)
    {
        int c = x.Score.CompareTo(y.Score);
        if (c != 0) return c;
        c = x.Distance.CompareTo(y.Distance);
        if (c != 0) return c;
        c = x.A.CompareTo(y.A);
        if (c != 0) return c;
        return x.B.CompareTo(y.B);
    });

    bool[] used = new bool[n];
    List<DoorPairCandidate> chosen = new List<DoorPairCandidate>();
    for (int i = 0; i < pairs.Count; i++)
    {
        DoorPairCandidate pc = pairs[i];
        if (used[pc.A] || used[pc.B]) continue;
        used[pc.A] = true;
        used[pc.B] = true;
        DoorGroup g = new DoorGroup();
        g.Root = _doorGroups.Count;
        g.Doors.Add(candidates[pc.A]);
        g.Doors.Add(candidates[pc.B]);
        _doorGroups.Add(g);
        chosen.Add(pc);
        if (NearLimit()) { _budgetStops++; break; }
    }

    for (int i = 0; i < used.Length; i++) if (!used[i]) _doorPairUnpairedCount++;
    _doorGroupsBuilt = _doorGroups.Count;
    BuildDoorPairDiag(candidates, chosen, used);
}

bool IsInterlockCandidate(IMyDoor d)
{
    if (d == null) return false;
    string name = d.CustomName == null ? "" : d.CustomName;
    if (HasTag(name, _excludeTag) || HasTag(name, _doorInterlockExcludeTag)) return false;
    if (IsHangarDoor(d) || IsGateDoor(d)) return false;
    return true;
}

bool TryDoorPairCandidate(IMyDoor a, IMyDoor b, int ai, int bi, out DoorPairCandidate pc)
{
    pc = null;
    double distanceMeters = 999999.0;
    if (a == null || b == null || a.CubeGrid == null || b.CubeGrid == null) return false;
    if (a.CubeGrid.EntityId != b.CubeGrid.EntityId) return false;

    Vector3D ap = a.GetPosition();
    Vector3D bp = b.GetPosition();
    Vector3D delta = bp - ap;
    distanceMeters = delta.Length();
    if (distanceMeters <= 0.01 || distanceMeters > _doorInterlockMaxGapMeters + 0.05) return false;

    Vector3D af = Normalized(a.WorldMatrix.Forward);
    Vector3D bf = Normalized(b.WorldMatrix.Forward);
    if (af.LengthSquared() < 0.001 || bf.LengthSquared() < 0.001) return false;

    // Doors may be visually centered/half/biased but still represent the same airlock passage.
    // Keep the rule permissive: favor nearby, similarly oriented doors with low lateral offset.
    double faceDot = Math.Abs(Vector3D.Dot(af, bf));
    if (faceDot < 0.45) return false;

    Vector3D dir = delta / distanceMeters;
    double axial = Math.Abs(Vector3D.Dot(dir, af));
    double lateral = Math.Sqrt(Math.Max(0.0, distanceMeters * distanceMeters * (1.0 - axial * axial)));
    if (lateral > _doorInterlockMaxLateralMeters + 0.05) return false;

    // Score low = better. Distance dominates; lateral and poor axial/face agreement are tie breakers.
    pc = new DoorPairCandidate();
    pc.A = ai;
    pc.B = bi;
    pc.Distance = distanceMeters;
    pc.Lateral = lateral;
    pc.Axial = axial;
    pc.FaceDot = faceDot;
    pc.Score = distanceMeters + lateral * 1.25 + (1.0 - axial) * 1.5 + (1.0 - faceDot) * 0.5;
    return true;
}

Vector3D Normalized(Vector3D v)
{
    double l = v.Length();
    if (l < 0.0001) return new Vector3D(0, 0, 0);
    return v / l;
}

void BuildDoorPairDiag(List<IMyDoor> candidates, List<DoorPairCandidate> chosen, bool[] used)
{
    _doorPairDiag.Clear();
    _doorPairDiag.AppendLine("[ADS PAIR DIAGNOSTIC]");
    _doorPairDiag.AppendLine("Doors=" + candidates.Count + " Pairs=" + _doorGroupsBuilt + " Cand=" + _doorPairCandidateCount + " Unpaired=" + _doorPairUnpairedCount + " Amb=" + _doorPairAmbiguousCount);
    _doorPairDiag.AppendLine("MaxDist=" + _doorInterlockMaxGapMeters.ToString("0.0") + "m MaxLat=" + _doorInterlockMaxLateralMeters.ToString("0.0") + "m");
    for (int i = 0; i < chosen.Count && i < 6; i++)
    {
        DoorPairCandidate pc = chosen[i];
        string an = ShortName(candidates[pc.A]);
        string bn = ShortName(candidates[pc.B]);
        _doorPairDiag.AppendLine("PAIR " + (i + 1) + ": " + an + " <-> " + bn + " d" + pc.Distance.ToString("0.0") + " lat" + pc.Lateral.ToString("0.0"));
    }
    int listed = 0;
    for (int i = 0; i < candidates.Count && listed < 6; i++)
    {
        if (used != null && i < used.Length && used[i]) continue;
        _doorPairDiag.AppendLine("UNPAIRED: " + ShortName(candidates[i]));
        listed++;
    }
    _doorPairDiagBrief = "pairs " + _doorGroupsBuilt + " unp " + _doorPairUnpairedCount + " cand " + _doorPairCandidateCount + " amb " + _doorPairAmbiguousCount;
}

string ShortName(IMyTerminalBlock b)
{
    if (b == null || b.CustomName == null) return "<null>";
    string n = b.CustomName;
    if (n.Length <= 28) return n;
    return n.Substring(0, 25) + "...";
}

string ShortId(long id)
{
    long v = id;
    if (v < 0) v = -v;
    return (v % 1000000).ToString();
}

bool DoorsAreInterlockAdjacent(IMyDoor a, IMyDoor b, out double distanceMeters)
{
    DoorPairCandidate pc;
    bool ok = TryDoorPairCandidate(a, b, 0, 1, out pc);
    distanceMeters = pc == null ? 999999.0 : pc.Distance;
    return ok;
}

bool IsSingleAxis(Vector3I delta, Vector3I axis)
{
    if (axis.X != 0) return delta.Y == 0 && delta.Z == 0;
    if (axis.Y != 0) return delta.X == 0 && delta.Z == 0;
    if (axis.Z != 0) return delta.X == 0 && delta.Y == 0;
    return false;
}

Vector3I DoorForwardAxis(IMyDoor d)
{
    if (d == null) return new Vector3I(0, 0, 0);
    return Base6Directions.GetIntVector(d.Orientation.Forward);
}

bool Parallel(Vector3I a, Vector3I b)
{
    if (a.X == 0 && a.Y == 0 && a.Z == 0) return false;
    if (b.X == 0 && b.Y == 0 && b.Z == 0) return false;
    return (a.X == b.X && a.Y == b.Y && a.Z == b.Z) ||
           (a.X == -b.X && a.Y == -b.Y && a.Z == -b.Z);
}

int Find(int[] parent, int i)
{
    while (parent[i] != i)
    {
        parent[i] = parent[parent[i]];
        i = parent[i];
    }
    return i;
}

void Union(int[] parent, int a, int b)
{
    int ra = Find(parent, a);
    int rb = Find(parent, b);
    if (ra != rb) parent[rb] = ra;
}

void TickDoorInterlock(double dt)
{
    // V020: two-door groups can auto-cycle like a simple airlock.
    // Geometry discovery is scan-time only; this fast path uses cached groups/door refs.
    for (int i = 0; i < _doorGroups.Count; i++)
    {
        DoorGroup g = _doorGroups[i];
        if (g == null) continue;
        if (_doorInterlockAutoCycle && g.Doors.Count == 2)
        {
            TickTwoDoorAutoCycle(g, dt);
        }
        else
        {
            TickDoorHardInterlock(g);
        }
        if (NearLimit()) { _budgetStops++; return; }
    }
}

void TickDoorHardInterlock(DoorGroup g)
{
    // Hard interlock: when any door in a cached normal-door group is active,
    // all other doors in that group are commanded closed and power-disabled.
    if (g == null) return;
    IMyDoor keep = null;
    int keepTick = int.MaxValue;
    int activeCount = 0;

    for (int j = 0; j < g.Doors.Count; j++)
    {
        IMyDoor d = g.Doors[j];
        if (d == null) continue;
        bool active = d.OpenRatio > ADS_CLOSED_RATIO;
        long id = d.EntityId;
        if (active)
        {
            activeCount++;
            int t;
            if (!_doorOpenTick.TryGetValue(id, out t))
            {
                t = _doorInterlockTick;
                _doorOpenTick[id] = t;
            }
            if (t < keepTick)
            {
                keepTick = t;
                keep = d;
            }
        }
        else if (_doorOpenTick.ContainsKey(id)) _doorOpenTick.Remove(id);
        if (NearLimit()) { _budgetStops++; return; }
    }

    if (activeCount <= 0 || keep == null)
    {
        for (int j = 0; j < g.Doors.Count; j++) UnlockInterlockDoor(g.Doors[j]);
        g.CycleState = 0;
        g.EntryId = 0;
        g.ExitId = 0;
        g.ExitStarted = false;
        return;
    }

    UnlockInterlockDoor(keep);
    for (int j = 0; j < g.Doors.Count; j++)
    {
        IMyDoor d = g.Doors[j];
        if (d == null || d.EntityId == keep.EntityId) continue;
        LockInterlockDoor(d);
        if (NearLimit()) { _budgetStops++; return; }
    }
}

void TickTwoDoorAutoCycle(DoorGroup g, double dt)
{
    if (g == null || g.Doors.Count != 2) return;
    IMyDoor a = g.Doors[0];
    IMyDoor b = g.Doors[1];
    if (a == null || b == null) return;

    if (g.CycleState == 0)
    {
        bool aActive = a.OpenRatio > ADS_CLOSED_RATIO;
        bool bActive = b.OpenRatio > ADS_CLOSED_RATIO;
        if (!aActive && !bActive)
        {
            UnlockInterlockDoor(a);
            UnlockInterlockDoor(b);
            return;
        }
        if (aActive) StartDoorCycle(g, a, b);
        else StartDoorCycle(g, b, a);
        return;
    }

    IMyDoor entry = FindDoorInGroup(g, g.EntryId);
    IMyDoor exit = FindDoorInGroup(g, g.ExitId);
    if (entry == null || exit == null)
    {
        ReleaseDoorGroup(g);
        return;
    }

    if (g.CycleState == 1)
    {
        // Entry door is the door the player opened. Keep exit locked until entry fully closes.
        UnlockInterlockDoor(entry);
        LockInterlockDoor(exit);
        if (entry.OpenRatio <= ADS_CLOSED_RATIO)
        {
            LockInterlockDoor(entry);
            LockInterlockDoor(exit);
            g.ExitStarted = false;
            g.ExitDelayLeft = _doorInterlockAutoOpenDelaySeconds;
            g.CycleState = 2;
            _doorInterlockCycled++;
        }
        return;
    }

    if (g.CycleState == 2)
    {
        // Exit door opens after the configured delay; keep entry locked until exit closes again.
        LockInterlockDoor(entry);
        if (!g.ExitStarted)
        {
            if (g.ExitDelayLeft > 0)
            {
                LockInterlockDoor(exit);
                g.ExitDelayLeft -= dt;
                return;
            }
            UnlockInterlockDoor(exit);
            exit.OpenDoor();
            if (exit.OpenRatio > ADS_CLOSED_RATIO) g.ExitStarted = true;
            return;
        }
        UnlockInterlockDoor(exit);
        if (exit.OpenRatio > ADS_CLOSED_RATIO) g.ExitStarted = true;
        if (g.ExitStarted && exit.OpenRatio <= ADS_CLOSED_RATIO)
        {
            ReleaseDoorGroup(g);
        }
    }
}

void StartDoorCycle(DoorGroup g, IMyDoor entry, IMyDoor exit)
{
    if (g == null || entry == null || exit == null) return;
    g.EntryId = entry.EntityId;
    g.ExitId = exit.EntityId;
    g.CycleState = 1;
    g.ExitStarted = false;
    g.ExitDelayLeft = 0;
    UnlockInterlockDoor(entry);
    LockInterlockDoor(exit);
}

IMyDoor FindDoorInGroup(DoorGroup g, long id)
{
    if (g == null) return null;
    for (int i = 0; i < g.Doors.Count; i++)
    {
        IMyDoor d = g.Doors[i];
        if (d != null && d.EntityId == id) return d;
    }
    return null;
}

void ReleaseDoorGroup(DoorGroup g)
{
    if (g == null) return;
    for (int i = 0; i < g.Doors.Count; i++) UnlockInterlockDoor(g.Doors[i]);
    g.CycleState = 0;
    g.EntryId = 0;
    g.ExitId = 0;
    g.ExitStarted = false;
    g.ExitDelayLeft = 0;
}

void LockInterlockDoor(IMyDoor d)
{
    if (d == null) return;
    d.Enabled = true;
    if (d.OpenRatio > ADS_CLOSED_RATIO)
    {
        d.CloseDoor();
        _doorInterlockClosed++;
    }
    if (_doorInterlockPowerLock)
    {
        if (d.Enabled) d.Enabled = false;
        if (!_doorPowerLocked.ContainsKey(d.EntityId))
        {
            _doorPowerLocked[d.EntityId] = true;
            _doorInterlockLocked++;
        }
    }
}

void UnlockInterlockDoor(IMyDoor d)
{
    if (d == null) return;
    if (!_doorPowerLocked.ContainsKey(d.EntityId)) return;
    d.Enabled = true;
    _doorPowerLocked.Remove(d.EntityId);
    _doorInterlockUnlocked++;
}

void ReleaseAllInterlockLocks()
{
    if (_doorPowerLocked.Count == 0) return;
    List<long> ids = new List<long>(_doorPowerLocked.Keys);
    for (int i = 0; i < ids.Count; i++)
    {
        long id = ids[i];
        for (int j = 0; j < _managedDoors.Count; j++)
        {
            IMyDoor d = _managedDoors[j];
            if (d != null && d.EntityId == id)
            {
                d.Enabled = true;
                break;
            }
        }
        _doorPowerLocked.Remove(id);
        if (NearLimit()) { _budgetStops++; break; }
    }
}

void CloseManagedDoorsNow()
{
    _doorCloseNowCount = 0;
    for (int i = 0; i < _managedDoors.Count; i++)
    {
        IMyDoor d = _managedDoors[i];
        if (d == null) continue;
        if (d.OpenRatio > ADS_CLOSED_RATIO)
        {
            d.CloseDoor();
            _doorCloseNowCount++;
        }
        if (NearLimit()) { _budgetStops++; break; }
    }
}

void TrimOpenTracker()
{
    if (_openSeconds.Count == 0) return;
    List<long> remove = new List<long>();
    foreach (KeyValuePair<long, double> kv in _openSeconds)
    {
        bool found = false;
        for (int i = 0; i < _managedDoors.Count; i++)
        {
            IMyDoor d = _managedDoors[i];
            if (d != null && d.EntityId == kv.Key) { found = true; break; }
        }
        if (!found) remove.Add(kv.Key);
    }
    for (int i = 0; i < remove.Count; i++) _openSeconds.Remove(remove[i]);
}

void LoadConfig()
{
    string cd = Me.CustomData == null ? "" : Me.CustomData;
    string name = ReadIni(cd, "ENTITY", "Name", "");
    string tag = ReadIni(cd, "ENTITY", "Tag", "");
    string nameTag = InferEntityTagFromPbName();

    if (tag.Length == 0 && nameTag.Length > 0) tag = nameTag;
    if (tag.Length == 0) tag = DEFAULT_TAG;
    if (tag.Length > 0 && !IsBracketTag(tag)) tag = "[" + StripBrackets(tag) + "]";
    if (tag.Length == 0)
    {
        _identityFault = true;
        _identityFaultText = "IDENTITY FAULT: name this PB with an entity tag, e.g. [LDC1] ARGOS PB";
    }

    if (nameTag.Length > 0 && tag.Length > 0 && nameTag != tag)
    {
        _identityFault = true;
        _identityFaultText = "IDENTITY FAULT: PB name " + nameTag + " conflicts with Custom Data " + tag;
    }

    _entityTag = tag;
    _entityPrefix = _entityTag.Length > 0 ? _entityTag + " " : "";
    _entityName = name.Length > 0 ? name : (tag.Length > 0 ? StripBrackets(_entityTag) : DEFAULT_NAME);

    _autoTagEnabled = ReadBool(cd, "AUTOTAG", "Enabled", false);
    _tagNewLocalBlocks = ReadBool(cd, "AUTOTAG", "TagNewLocalBlocks", false);
    _autoTagAllowCleanExpansion = ReadBool(cd, "AUTOTAG", "AllowCleanScopeExpansion", true);
    _autoTagIgnoreFarSideMergeSeams = ReadBool(cd, "AUTOTAG", "IgnoreFarSideMergeSeams", true);
    if (_tagNewLocalBlocks) _autoTagEnabled = true;

    _adsEnabled = ReadBool(cd, "ADS", "Enabled", true);
    _closeDelay = ReadDouble(cd, "ADS", "CloseDelaySeconds", DEFAULT_CLOSE_DELAY);
    _manageDoors = ReadBool(cd, "ADS", "ManageDoors", true);
    _manageHangarDoors = ReadBool(cd, "ADS", "ManageHangarDoors", true);
    _manageGates = ReadBool(cd, "ADS", "ManageGates", true);
    _manageUntaggedLocalDoors = ReadBool(cd, "ADS", "ManageUntaggedLocalDoors", false);
    _manualManagedTag = ReadIni(cd, "ADS", "ManualManagedTag", "[ADS]");
    _excludeTag = ReadIni(cd, "ADS", "ExcludeTag", "[NOADS]");
    _doorInterlockEnabled = ReadBool(cd, "ADS", "DoorInterlockEnabled", true);
    _doorInterlockMaxGapMeters = ReadDouble(cd, "ADS", "DoorInterlockMaxGapMeters", DEFAULT_INTERLOCK_MAX_GAP_METERS);
    if (_doorInterlockMaxGapMeters < 0.5) _doorInterlockMaxGapMeters = 0.5;
    if (_doorInterlockMaxGapMeters > 10.0) _doorInterlockMaxGapMeters = 10.0;
    _doorInterlockMaxLateralMeters = ReadDouble(cd, "ADS", "DoorInterlockMaxLateralMeters", DEFAULT_INTERLOCK_MAX_LATERAL_METERS);
    if (_doorInterlockMaxLateralMeters < 0.25) _doorInterlockMaxLateralMeters = 0.25;
    if (_doorInterlockMaxLateralMeters > 5.0) _doorInterlockMaxLateralMeters = 5.0;
    _doorInterlockExcludeTag = ReadIni(cd, "ADS", "DoorInterlockExcludeTag", "[NOPAIR]");
    _doorInterlockPowerLock = ReadBool(cd, "ADS", "DoorInterlockPowerLock", true);
    _doorInterlockAutoCycle = ReadBool(cd, "ADS", "DoorInterlockAutoCycle", true);
    _doorInterlockAutoOpenDelaySeconds = ReadDouble(cd, "ADS", "DoorInterlockAutoOpenDelaySeconds", DEFAULT_INTERLOCK_AUTO_OPEN_DELAY_SECONDS);
    if (_doorInterlockAutoOpenDelaySeconds < 0) _doorInterlockAutoOpenDelaySeconds = 0;
    if (_doorInterlockAutoOpenDelaySeconds > 5.0) _doorInterlockAutoOpenDelaySeconds = 5.0;
}

void WriteCleanConfig(bool force)
{
    StringBuilder c = new StringBuilder();
    c.AppendLine("[ENTITY]");
    c.AppendLine("Name=" + _entityName);
    c.AppendLine("Tag=" + _entityTag);
    c.AppendLine("");
    c.AppendLine("[AUTOTAG]");
    c.AppendLine("Enabled=" + (_autoTagEnabled ? "true" : "false"));
    c.AppendLine("TagNewLocalBlocks=" + (_tagNewLocalBlocks ? "true" : "false"));
    c.AppendLine("AllowCleanScopeExpansion=" + (_autoTagAllowCleanExpansion ? "true" : "false"));
    c.AppendLine("IgnoreFarSideMergeSeams=" + (_autoTagIgnoreFarSideMergeSeams ? "true" : "false"));
    c.AppendLine("");
    c.AppendLine("[ADS]");
    c.AppendLine("Enabled=" + (_adsEnabled ? "true" : "false"));
    c.AppendLine("CloseDelaySeconds=" + _closeDelay.ToString("0.###"));
    c.AppendLine("ManageDoors=" + (_manageDoors ? "true" : "false"));
    c.AppendLine("ManageHangarDoors=" + (_manageHangarDoors ? "true" : "false"));
    c.AppendLine("ManageGates=" + (_manageGates ? "true" : "false"));
    c.AppendLine("ManageUntaggedLocalDoors=" + (_manageUntaggedLocalDoors ? "true" : "false"));
    c.AppendLine("ManualManagedTag=" + _manualManagedTag);
    c.AppendLine("ExcludeTag=" + _excludeTag);
    c.AppendLine("DoorInterlockEnabled=" + (_doorInterlockEnabled ? "true" : "false"));
    c.AppendLine("DoorInterlockMaxGapMeters=" + _doorInterlockMaxGapMeters.ToString("0.###"));
    c.AppendLine("DoorInterlockMaxLateralMeters=" + _doorInterlockMaxLateralMeters.ToString("0.###"));
    c.AppendLine("DoorInterlockExcludeTag=" + _doorInterlockExcludeTag);
    c.AppendLine("DoorInterlockPowerLock=" + (_doorInterlockPowerLock ? "true" : "false"));
    c.AppendLine("DoorInterlockAutoCycle=" + (_doorInterlockAutoCycle ? "true" : "false"));
    c.AppendLine("DoorInterlockAutoOpenDelaySeconds=" + _doorInterlockAutoOpenDelaySeconds.ToString("0.###"));
    string newCd = c.ToString();

    // Do not rewrite Custom Data on recompile when user/config sections already exist.
    // Mutating commands may still write clean config, but managed report sections are preserved.
    string old = Me.CustomData == null ? "" : Me.CustomData;
    if (!force && old.Trim().Length > 0) return;
    if (force) newCd = PreserveManagedSections(old, newCd);
    if (force || old.Trim().Length == 0) Me.CustomData = newCd;
}

string PreserveManagedSections(string oldCd, string baseCd)
{
    string status = ExtractMarkedSection(oldCd, ARGOS_STATUS_BEGIN, ARGOS_STATUS_END);
    if (status.Length == 0) return baseCd;
    StringBuilder sb = new StringBuilder();
    sb.Append(baseCd.TrimEnd());
    sb.AppendLine();
    sb.AppendLine();
    sb.Append(status.TrimEnd());
    sb.AppendLine();
    return sb.ToString();
}

void WriteManagedStatusReport(string body)
{
    string old = Me.CustomData == null ? "" : Me.CustomData;
    string block = ARGOS_STATUS_BEGIN + "\n" + body.TrimEnd() + "\n" + ARGOS_STATUS_END + "\n";
    Me.CustomData = ReplaceMarkedSection(old, ARGOS_STATUS_BEGIN, ARGOS_STATUS_END, block);
}

string ReplaceMarkedSection(string text, string begin, string end, string block)
{
    if (text == null) text = "";
    int a = text.IndexOf(begin);
    if (a >= 0)
    {
        int b = text.IndexOf(end, a + begin.Length);
        if (b >= 0)
        {
            b += end.Length;
            while (b < text.Length && (text[b] == '\r' || text[b] == '\n')) b++;
            string pre = text.Substring(0, a).TrimEnd();
            string post = text.Substring(b).TrimStart('\r', '\n');
            StringBuilder sb = new StringBuilder();
            if (pre.Length > 0) { sb.AppendLine(pre); sb.AppendLine(); }
            sb.Append(block);
            if (post.Length > 0) { sb.AppendLine(); sb.Append(post); }
            return sb.ToString();
        }
    }
    StringBuilder add = new StringBuilder();
    if (text.Trim().Length > 0) { add.AppendLine(text.TrimEnd()); add.AppendLine(); }
    add.Append(block);
    return add.ToString();
}

string ExtractMarkedSection(string text, string begin, string end)
{
    if (text == null) return "";
    int a = text.IndexOf(begin);
    if (a < 0) return "";
    int b = text.IndexOf(end, a + begin.Length);
    if (b < 0) return "";
    b += end.Length;
    return text.Substring(a, b - a);
}

void EnsureArgSelfTag()
{
    if (Me == null) return;
    string n = Me.CustomName == null ? "" : Me.CustomName;
    if (!StartsWithEntityTag(n)) return;
    if (HasTag(n, ARG_TAG)) return;
    string rest = n.Substring(_entityTag.Length).TrimStart();
    Me.CustomName = _entityPrefix + ARG_TAG + " " + rest;
}

string InferEntityTagFromPbName()
{
    string n = Me == null || Me.CustomName == null ? "" : Me.CustomName.Trim();
    if (!n.StartsWith("[")) return "";
    int end = n.IndexOf(']');
    if (end <= 0) return "";
    string tag = n.Substring(0, end + 1);
    if (tag == ARG_TAG) return "";
    return tag;
}

string ReadIni(string text, string section, string key, string fallback)
{
    string cur = "";
    string[] lines = text.Replace("\r", "").Split('\n');
    string target = "[" + section + "]";
    for (int i = 0; i < lines.Length; i++)
    {
        string line = lines[i].Trim();
        if (line.Length == 0 || line.StartsWith(";")) continue;
        if (line.StartsWith("[") && line.EndsWith("]")) { cur = line; continue; }
        if (cur != target) continue;
        int eq = line.IndexOf('=');
        if (eq <= 0) continue;
        string k = line.Substring(0, eq).Trim();
        if (!k.Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
        return line.Substring(eq + 1).Trim();
    }
    return fallback;
}

bool ReadBool(string text, string section, string key, bool fallback)
{
    string v = ReadIni(text, section, key, fallback ? "true" : "false").ToLowerInvariant();
    return v == "true" || v == "yes" || v == "1" || v == "on";
}

double ReadDouble(string text, string section, string key, double fallback)
{
    double v;
    if (double.TryParse(ReadIni(text, section, key, fallback.ToString()), out v)) return v;
    return fallback;
}

bool StartsWithEntityTag(string name)
{
    if (name == null) return false;
    return name.StartsWith(_entityPrefix) || name == _entityTag || name.StartsWith(_entityTag);
}

string LeadingBracketTag(string name)
{
    if (name == null) return "";
    name = name.TrimStart();
    if (!name.StartsWith("[")) return "";
    int end = name.IndexOf(']');
    if (end <= 0) return "";
    return name.Substring(0, end + 1);
}

void AddIdCount(List<string> tags, List<int> counts, string tag)
{
    if (tag == null || tag.Length == 0) return;
    for (int i = 0; i < tags.Count; i++)
    {
        if (tags[i] == tag)
        {
            counts[i] = counts[i] + 1;
            return;
        }
    }
    if (tags.Count < 16)
    {
        tags.Add(tag);
        counts.Add(1);
    }
    else _unknownLeadingTags++;
}

string CompactTagCounts(List<string> tags, List<int> counts)
{
    if (tags.Count == 0) return "none";
    StringBuilder sb = new StringBuilder();
    int limit = tags.Count < 4 ? tags.Count : 4;
    for (int i = 0; i < limit; i++)
    {
        if (i > 0) sb.Append(' ');
        sb.Append(tags[i]).Append('=').Append(counts[i]);
    }
    if (tags.Count > limit) sb.Append(" +").Append(tags.Count - limit);
    return sb.ToString();
}

void AppendTagCounts(StringBuilder r, List<string> tags, List<int> counts)
{
    if (tags.Count == 0)
    {
        r.AppendLine("none");
        return;
    }
    for (int i = 0; i < tags.Count; i++)
    {
        if (i > 0) r.Append("  ");
        r.Append(tags[i]).Append('=').Append(counts[i]);
    }
    if (_unknownLeadingTags > 0) r.Append("  +overflow=").Append(_unknownLeadingTags);
    r.AppendLine();
}

void AppendEntityFilterTagCounts(StringBuilder r, List<string> tags, List<int> counts, bool entityOnly)
{
    bool any = false;
    for (int i = 0; i < tags.Count; i++)
    {
        bool isEntity = tags[i] == _entityTag;
        if (entityOnly != isEntity) continue;
        if (any) r.Append("  ");
        r.Append(tags[i]).Append('=').Append(counts[i]);
        any = true;
    }
    if (!any) r.Append("none");
    r.AppendLine();
}

bool HasTag(string name, string tag)
{
    if (name == null || tag == null || tag.Length == 0) return false;
    return name.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0;
}

bool IsBracketTag(string tag)
{
    return tag != null && tag.StartsWith("[") && tag.EndsWith("]") && tag.Length > 2;
}

string StripBrackets(string tag)
{
    if (tag == null) return "";
    tag = tag.Trim();
    if (tag.StartsWith("[") && tag.EndsWith("]") && tag.Length > 1) return tag.Substring(1, tag.Length - 2);
    return tag;
}

bool IsOwnedGridId(long gridId)
{
    return _ownedGridIds.ContainsKey(gridId);
}

bool IsOwnedGrid(IMyCubeGrid grid)
{
    return grid != null && _ownedGridIds.ContainsKey(grid.EntityId);
}

bool IsMergeConnected(IMyShipMergeBlock mb)
{
    if (mb == null) return false;
    try { return mb.IsConnected; } catch { return false; }
}

bool NearLimit()
{
    return Runtime.CurrentInstructionCount > Runtime.MaxInstructionCount - INSTRUCTION_MARGIN;
}

int Abs(int v)
{
    return v < 0 ? -v : v;
}

int DistSq(Vector3I a, Vector3I b)
{
    int x = a.X - b.X;
    int y = a.Y - b.Y;
    int z = a.Z - b.Z;
    return x * x + y * y + z * z;
}

Vector3I PrimaryAxis(Vector3I v)
{
    int ax = Abs(v.X);
    int ay = Abs(v.Y);
    int az = Abs(v.Z);
    if (ax == 0 && ay == 0 && az == 0) return new Vector3I(0, 0, 0);
    if (ax >= ay && ax >= az) return new Vector3I(Sign(v.X), 0, 0);
    if (ay >= ax && ay >= az) return new Vector3I(0, Sign(v.Y), 0);
    return new Vector3I(0, 0, Sign(v.Z));
}

int Dot(Vector3I a, Vector3I b)
{
    return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
}

int Sign(int v)
{
    if (v > 0) return 1;
    if (v < 0) return -1;
    return 0;
}

string SafeLower(string s)
{
    return s == null ? "" : s.ToLowerInvariant();
}

int CountSampleLines(StringBuilder sb)
{
    int count = 0;
    for (int i = 0; i < sb.Length; i++) if (sb[i] == '\n') count++;
    return count;
}

void AddRenameSample(StringBuilder sb, string oldName, string newName)
{
    if (sb == null) return;
    if (sb.Length >= 600 || CountSampleLines(sb) >= SAMPLE_LIMIT) return;
    sb.Append("- ").Append(TrimForReport(oldName)).Append(" -> ").Append(TrimForReport(newName)).Append('\n');
}

void AddSample(StringBuilder sb, string name, string reason)
{
    if (sb == null) return;
    if (sb.Length >= 500 || CountSampleLines(sb) >= SAMPLE_LIMIT) return;
    sb.Append("- ").Append(TrimForReport(name));
    if (reason != null && reason.Length > 0) sb.Append(" (").Append(reason).Append(")");
    sb.Append('\n');
}

void AppendSampleSection(StringBuilder r, string title, StringBuilder sb)
{
    if (r == null || sb == null || sb.Length == 0) return;
    r.AppendLine("");
    r.AppendLine(title);
    r.Append(sb.ToString());
}

string TrimForReport(string s)
{
    if (s == null) return "";
    if (s.Length <= 70) return s;
    return s.Substring(0, 67) + "...";
}

void ResetCounters()
{
    _safeUntagged = 0;
    _safeAlreadyTagged = 0;
    _protectedTagged = 0;
    _skippedForeignGrid = 0;
    _skippedMergeFar = 0;
    _skippedMergeAmbiguous = 0;
    _untaggedAll = 0;
    _idTags.Clear(); _idCounts.Clear();
    _localIdTags.Clear(); _localIdCounts.Clear();
    _nonLocalIdTags.Clear(); _nonLocalIdCounts.Clear();
    _changed = 0;
    _failed = 0;
    _scanned = 0;
    _mergeConnected = 0;
    _mergePairCount = 0;
    _mergeAmbiguousPairs = 0;
    _farSideSeamsIgnored = 0;
    _localDynamicSeamsIgnored = 0;
    _mechanicalLinks = 0;
    _ownedGridCount = 0;
    _mechanicalLinksBlocked = 0;
    _mechanicalLinksAmbiguous = 0;
}

void AcceptScopeCommand()
{
    if (!_dryRunCompleted)
    {
        _lastStatus = "ACCEPT blocked. Run DRYRUN first.";
        return;
    }
    if (_lastDrySafe != 0)
    {
        _lastStatus = "ACCEPT blocked. Safe untagged candidates remain: " + _lastDrySafe + ". Run TAG, then ACCEPT.";
        return;
    }
    if (_lastDryAmbPairs > 0 || _lastDryAmbBlocks > 0)
    {
        _lastStatus = "ACCEPT blocked. Ambiguous merge data exists.";
        return;
    }
    if (_lastDrySignature.Length == 0)
    {
        _lastStatus = "ACCEPT blocked. No dry-run scope signature.";
        return;
    }

    _acceptedSignature = _lastDrySignature;
    _acceptedOwnedGridMax = _ownedGridCount;
    _acceptedMechanicalLinkMax = _mechanicalLinks;
    List<string> newAcceptedSeams = new List<string>();
    BuildCurrentSeamKeys(newAcceptedSeams);
    _acceptedSeamKeys.Clear();
    for (int i = 0; i < newAcceptedSeams.Count; i++) _acceptedSeamKeys.Add(newAcceptedSeams[i]);
    _scopeAccepted = true;
    _acceptedScopeV2 = true;
    _autoTagStopReason = "";
    WriteScopeStorage();
    _autoTagEnabled = true;
    _tagNewLocalBlocks = true;
    WriteCleanConfig(true);
    _lastStatus = "Accepted ARGOS scope. AutoTag maintenance enabled.";
}

void LoadAcceptedScope()
{
    _scopeAccepted = false;
    _acceptedScopeV2 = false;
    _acceptedSignature = "";
    _acceptedOwnedGridMax = -1;
    _acceptedMechanicalLinkMax = -1;
    _acceptedSeamKeys.Clear();
    _autoTagStopReason = "";
    string st = Storage == null ? "" : Storage;
    string tag = ReadStorageLine(st, "Tag", "");
    if (tag != _entityTag) return;

    if (st.IndexOf("ARGOS_SCOPE_V2") >= 0)
    {
        string sig = ReadStorageLine(st, "Signature", "");
        int grids;
        int links;
        if (sig.Length == 0) return;
        if (!int.TryParse(ReadStorageLine(st, "OwnedGridMax", "-1"), out grids)) grids = -1;
        if (!int.TryParse(ReadStorageLine(st, "MechanicalLinkMax", "-1"), out links)) links = -1;
        _acceptedSignature = sig;
        _acceptedOwnedGridMax = grids;
        _acceptedMechanicalLinkMax = links;
        ParseList(ReadStorageLine(st, "KnownSeams", ""), _acceptedSeamKeys);
        _scopeAccepted = true;
        _acceptedScopeV2 = true;
        return;
    }

    // Legacy V1 accepted scopes remain readable, but exact-match behavior is retained.
    // Run DRYRUN then ACCEPT again to upgrade to the safer V2 known-seam model.
    if (st.IndexOf("ARGOS_SCOPE_V1") >= 0)
    {
        string sig = ReadStorageLine(st, "Signature", "");
        if (sig.Length > 0)
        {
            _acceptedSignature = sig;
            _scopeAccepted = true;
            _acceptedScopeV2 = false;
        }
    }
}

void WriteScopeStorage()
{
    StringBuilder s = new StringBuilder();
    s.AppendLine("ARGOS_SCOPE_V2");
    s.AppendLine("Tag=" + _entityTag);
    s.AppendLine("Signature=" + _acceptedSignature);
    s.AppendLine("OwnedGridMax=" + _acceptedOwnedGridMax);
    s.AppendLine("MechanicalLinkMax=" + _acceptedMechanicalLinkMax);
    s.Append("KnownSeams=");
    for (int i = 0; i < _acceptedSeamKeys.Count; i++)
    {
        if (i > 0) s.Append('|');
        s.Append(_acceptedSeamKeys[i]);
    }
    s.AppendLine();
    Storage = s.ToString();
}

void ClearAcceptedScope()
{
    _scopeAccepted = false;
    _acceptedScopeV2 = false;
    _acceptedSignature = "";
    _acceptedOwnedGridMax = -1;
    _acceptedMechanicalLinkMax = -1;
    _acceptedSeamKeys.Clear();
    _autoTagStopReason = "";
    Storage = "";
}

string ReadStorageLine(string text, string key, string fallback)
{
    string[] lines = text.Replace("\r", "").Split('\n');
    string prefix = key + "=";
    for (int i = 0; i < lines.Length; i++)
    {
        string line = lines[i].Trim();
        if (line.StartsWith(prefix)) return line.Substring(prefix.Length).Trim();
    }
    return fallback;
}

void ParseList(string text, List<string> list)
{
    list.Clear();
    if (text == null || text.Length == 0) return;
    string[] parts = text.Split('|');
    for (int i = 0; i < parts.Length; i++)
    {
        string v = parts[i].Trim();
        if (v.Length > 0) list.Add(v);
    }
}

string BuildScopeSignature()
{
    StringBuilder s = new StringBuilder();
    s.Append(_entityTag).Append("|");
    if (Me != null) s.Append("PB").Append(Me.EntityId).Append("|");
    s.Append("G").Append(_ownedGridCount).Append("L").Append(_mechanicalLinks);
    s.Append("M").Append(_mergeConnected).Append("P").Append(_mergePairCount).Append("A").Append(_mergeAmbiguousPairs).Append("|");

    List<string> seams = new List<string>();
    BuildCurrentSeamKeys(seams);
    for (int i = 0; i < seams.Count; i++) s.Append(seams[i]).Append(';');
    return s.ToString();
}

void BuildCurrentSeamKeys(List<string> keys)
{
    keys.Clear();
    _farSideSeamsIgnored = 0;
    _localDynamicSeamsIgnored = 0;
    for (int i = 0; i < _mergePairs.Count; i++)
    {
        MergePair mp = _mergePairs[i];
        if (ShouldIgnoreFarSideSeam(mp))
        {
            _farSideSeamsIgnored++;
            continue;
        }
        if (ShouldIgnoreLocalDynamicSeam(mp))
        {
            _localDynamicSeamsIgnored++;
            continue;
        }
        string key = SeamKey(mp);
        if (key.Length > 0) keys.Add(key);
    }
    keys.Sort();
}

bool ShouldIgnoreLocalDynamicSeam(MergePair candidate)
{
    if (candidate == null || candidate.A == null || candidate.B == null) return false;
    bool aBirth = IsDpsBirthMerge(candidate.A);
    bool bBirth = IsDpsBirthMerge(candidate.B);
    if (aBirth == bBirth) return false;
    IMyShipMergeBlock other = aBirth ? candidate.B : candidate.A;
    string lead = LeadingBracketTag(other.CustomName);
    if (lead.Length == 0) return true;
    if (lead == _entityTag) return false;
    if (IsDcsSerialTag(lead)) return true;
    return false;
}

bool IsDpsBirthMerge(IMyShipMergeBlock b)
{
    if (b == null) return false;
    string n = b.CustomName;
    return StartsWithEntityTag(n) && HasTag(n, "[DPS]") && HasTag(n, "[BAY");
}

bool IsDcsSerialTag(string tag)
{
    if (tag == null || tag.Length < 4 || _entityTag.Length < 3) return false;
    if (!_entityTag.StartsWith("[") || !_entityTag.EndsWith("]")) return false;
    string p = _entityTag.Substring(0, _entityTag.Length - 1) + "-";
    return tag.StartsWith(p, StringComparison.OrdinalIgnoreCase) && tag.EndsWith("]");
}

bool ShouldIgnoreFarSideSeam(MergePair candidate)
{
    if (!_autoTagIgnoreFarSideMergeSeams) return false;
    if (candidate == null || candidate.A == null || candidate.B == null) return false;
    if (_acceptedSeamKeys.Count == 0) return false;

    for (int i = 0; i < _mergePairs.Count; i++)
    {
        MergePair accepted = _mergePairs[i];
        if (accepted == null || accepted == candidate) continue;
        string acceptedKey = SeamKey(accepted);
        if (acceptedKey.Length == 0) continue;
        if (!ContainsString(_acceptedSeamKeys, acceptedKey)) continue;

        int aSide = SideOfMergePair(candidate.A.Position, accepted);
        int bSide = SideOfMergePair(candidate.B.Position, accepted);
        if (aSide == 0 || bSide == 0) continue;
        if (aSide != accepted.SeedSide && bSide != accepted.SeedSide) return true;
    }
    return false;
}

int SideOfMergePair(Vector3I pos, MergePair seam)
{
    if (seam == null || seam.A == null || seam.B == null) return 0;
    return Sign(Dot(pos + pos - seam.A.Position - seam.B.Position, seam.Dir));
}

string SeamKey(MergePair mp)
{
    if (mp == null || mp.A == null || mp.B == null) return "";
    string a = PosText(mp.A.Position);
    string b = PosText(mp.B.Position);
    string first = String.CompareOrdinal(a, b) <= 0 ? a : b;
    string second = String.CompareOrdinal(a, b) <= 0 ? b : a;
    return first + "<>" + second;
}

string PosText(Vector3I p)
{
    return p.X + "/" + p.Y + "/" + p.Z;
}

string AcceptedScopeProblem()
{
    if (!_scopeAccepted || _acceptedSignature.Length == 0) return "no accepted scope";
    if (_mergeAmbiguousPairs > 0) return "ambiguous merge seam count " + _mergeAmbiguousPairs;

    if (!_acceptedScopeV2)
    {
        // Legacy accepted scope: exact signature only. Upgrade by running ACCEPT again.
        string legacy = BuildScopeSignature();
        if (legacy == _acceptedSignature) return "";
        return "legacy accepted scope exact mismatch; run ACCEPT to upgrade";
    }

    List<string> current = new List<string>();
    BuildCurrentSeamKeys(current);
    for (int i = 0; i < current.Count; i++)
    {
        if (!ContainsString(_acceptedSeamKeys, current[i]))
            return "unknown merge seam " + current[i];
    }

    bool gridExpanded = _acceptedOwnedGridMax >= 0 && _ownedGridCount > _acceptedOwnedGridMax;
    bool linkExpanded = _acceptedMechanicalLinkMax >= 0 && _mechanicalLinks > _acceptedMechanicalLinkMax;
    if (gridExpanded || linkExpanded)
    {
        if (_autoTagAllowCleanExpansion)
        {
            // Clean construction expansion is common while building an accepted entity.
            // If there are no ambiguous/unknown merge seams, refresh only the numeric maxima.
            // Known seam list is still strict: new seam = suspension above.
            if (gridExpanded) _acceptedOwnedGridMax = _ownedGridCount;
            if (linkExpanded) _acceptedMechanicalLinkMax = _mechanicalLinks;
            WriteScopeStorage();
            _lastStatus = "AutoTag accepted clean scope expansion grids " + _ownedGridCount + " mech " + _mechanicalLinks;
            return "";
        }
        return "mechanical expansion grids " + _ownedGridCount + ">" + _acceptedOwnedGridMax + " mech " + _mechanicalLinks + ">" + _acceptedMechanicalLinkMax;
    }

    return "";
}

bool AcceptedScopeMatchesCurrent()
{
    return AcceptedScopeProblem().Length == 0;
}

bool ContainsString(List<string> list, string value)
{
    for (int i = 0; i < list.Count; i++) if (list[i] == value) return true;
    return false;
}

void SuspendAutoTag(string reason)
{
    _autoTagStopReason = reason;
    // Do not rewrite the user's AutoTag config booleans here.
    // Suspension is runtime state; AUTOTAG_ON or ACCEPT clears it deliberately.
    _lastStatus = "AutoTag suspended: " + reason;
}

void ShowHelp()
{
    Echo(SCRIPT_NAME);
    Echo("Commands:");
    Echo("  AUDIT        scope/classify report");
    Echo("  DRYRUN       preview safe untagged local blocks");
    Echo("  TAG          apply after reviewed dry run");
    Echo("  ACCEPT       store clean scope and enable AutoTag");
    Echo("  CLEAR_SCOPE  forget accepted scope and disable AutoTag");
    Echo("  ADS_SCAN     rebuild cached door list");
    Echo("  PAIR_REPORT  show door pair diagnostic");
    Echo("  ADS_ON/OFF   toggle door automation");
    Echo("  ADS_CLOSE_NOW close cached managed doors");
    Echo("  PAIR_SCAN    rebuild normal-door interlock groups");
    Echo("  PAIR_ON/OFF  toggle normal-door interlock");
    Echo("  Interlock power-locks blocked doors; 2-door groups auto-cycle");
    Echo("  AUTOTAG_ON   clear suspension / enable maintenance");
    Echo("  AUTOTAG_OFF  disable accepted-scope maintenance");
    Echo("");
    Echo("Rules: no connectors, merge seams are firewalls, foreign leading tags protected.");
    Echo("TAG uses the frozen DRYRUN ledger and rechecks safety.");
}


string EchoClip(string s, int max)
{
    if (s == null) return "";
    if (s.Length <= max) return s;
    if (max <= 3) return s.Substring(0, max);
    return s.Substring(0, max - 3) + "...";
}

void EchoStatus()
{
    Echo(SCRIPT_NAME);
    Echo("Entity: " + _entityTag + " " + _entityName);
    Echo("Command: " + _lastCommand);
    Echo("Status: " + EchoClip(_lastStatus, 62));
    Echo("Instr: " + Runtime.CurrentInstructionCount + "/" + Runtime.MaxInstructionCount + " hi " + _hiInstr + " " + EchoClip(_hiWhere, 26) + " stops " + _budgetStops);
    if (_task == TaskKind.None) Echo("Task: None");
    else Echo("Task: " + _task.ToString() + " / " + _phase.ToString() + " idx " + _taskIndex + "/" + _blocks.Count);
    Echo("ADS: " + (_adsEnabled ? "ON" : "OFF") + " managed " + _doorManaged + " ignored " + _doorIgnored + " explicit " + _doorExplicit + " ent " + _doorEntityTagged);
    Echo("Door action: closed " + _doorClosedThisRun + " closeNow " + _doorCloseNowCount + " pairClose " + _doorInterlockClosed);
    Echo("Door locks " + _doorInterlockLocked + "/" + _doorInterlockUnlocked + " cycles " + _doorInterlockCycled);
    Echo("Door pairs: " + (_doorInterlockEnabled ? "ON" : "OFF") + " groups " + _doorGroupsBuilt + " unp " + _doorPairUnpairedCount + " cand " + _doorPairCandidateCount + " amb " + _doorPairAmbiguousCount);
    string tagState = !_autoTagEnabled ? "OFF" : (_autoTagStopReason.Length > 0 ? "SUSP" : "READY");
    Echo("Tag: " + tagState + " auto " + (_autoTagEnabled ? "ON" : "OFF") + " tagNew " + (_tagNewLocalBlocks ? "ON" : "OFF") + " dryrunOK " + (_dryRunCompleted ? "YES" : "NO") + " ledger " + _dryCandidateIds.Count);
    Echo("ScopeAccept: " + (_scopeAccepted ? (_acceptedScopeV2 ? "YES V2" : "YES legacy") : "NO"));
    if (_autoTagStopReason.Length > 0) Echo("Tag stop: " + EchoClip(_autoTagStopReason, 58));
    Echo("Scope: grids " + _ownedGridCount + "/" + _acceptedOwnedGridMax + " mech " + _mechanicalLinks + "/" + _acceptedMechanicalLinkMax + " merges " + _mergeConnected + "/" + _mergePairCount + " amb " + _mergeAmbiguousPairs + " blk " + _mechanicalLinksBlocked + " dyn " + _localDynamicSeamsIgnored);
    Echo("Firewall: " + (_mergeFirewallTraceEnabled ? "TRACE" : "OFF") + " far " + _skippedMergeFar + " amb " + _skippedMergeAmbiguous);
    Echo("IDs: " + EchoClip(CompactTagCounts(_idTags, _idCounts), 58) + " untag " + _untaggedAll);
    Echo("Dry: safe " + _safeUntagged + " already " + _safeAlreadyTagged + " protected " + _protectedTagged);
    Echo("Skip: grid " + _skippedForeignGrid + " mergeFar " + _skippedMergeFar + " amb " + _skippedMergeAmbiguous);
    if (_identityFault) Echo(EchoClip(_identityFaultText, 62));
}

void AppendHeader(StringBuilder r)
{
    r.AppendLine(SCRIPT_NAME);
    r.AppendLine("Entity: " + _entityTag + " " + _entityName);
    r.AppendLine("Command: " + _lastCommand);
    r.AppendLine("Status: " + _lastStatus);
    r.AppendLine("Instr: " + Runtime.CurrentInstructionCount + "/" + Runtime.MaxInstructionCount + " hi " + _hiInstr + " " + _hiWhere);
    r.AppendLine("--------------------------------");
}

void HoldEcho(string text)
{
    _echoHold.Clear();
    _echoHold.Append(text);
    _echoHoldTicks = ECHO_HOLD_TICKS;
    Echo(text);
}

class DoorPairCandidate
{
    public int A;
    public int B;
    public double Distance;
    public double Lateral;
    public double Axial;
    public double FaceDot;
    public double Score;
}

class DoorGroup
{
    public int Root;
    public List<IMyDoor> Doors = new List<IMyDoor>();
    public int CycleState;
    public long EntryId;
    public long ExitId;
    public bool ExitStarted;
    public double ExitDelayLeft;
}

class MechLink
{
    public IMyTerminalBlock Base;
    public long BaseGridId;
    public long TopGridId;
    public Vector3I BasePos;
}

class MergePair
{
    public IMyShipMergeBlock A;
    public IMyShipMergeBlock B;
    public Vector3I Dir;
    public int SeedSide;
}

enum TaskKind
{
    None,
    Audit,
    TagDryRun,
    TagNow,
    AutoTagMaintain,
    AdsScan
}

enum TaskPhase
{
    None,
    RefreshBlocks,
    BuildMechanicalScope,
    CollectMerges,
    BuildMergePairs,
    FilterMechanicalScope,
    ProcessBlocks,
    Done
}

enum LocalState
{
    Local,
    ForeignGrid,
    MergeFar,
    Ambiguous
}

enum MergeSide
{
    Local,
    Far,
    Ambiguous
}
