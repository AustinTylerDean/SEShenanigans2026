
const string INSTALL_TAG = "[OB1]";
const string IMS_TAG = "[IMS]";
const string PB1_TAG = "[PB1]";
const string PB2_TAG = "[PB2]";
const string UNLOAD_TAG = "[UNLOAD]";
const string TAG_ICE = "[ICE]";
const string TAG_ORE = "[ORE]";
const string TAG_INGOT = "[INGOT]";
const string TAG_REST = "[REST]";
const string TAG_COMP = "[COMP]";
const string TAG_AMMO = "[AMMO]";
const string TAG_ARMORY = "[ARMORY]";
const string PB1_EXPORT_BEGIN = "# IMS_PB1_EXPORT_BEGIN";
const string PB1_EXPORT_END = "# IMS_PB1_EXPORT_END";
const string WORKER_STATUS_BEGIN = "# IMS_WORKER_STATUS_BEGIN";
const string WORKER_STATUS_END = "# IMS_WORKER_STATUS_END";
const string DOCKED_BEGIN = "# OB1_IMS_DOCKED_VESSELS_BEGIN";
const string DOCKED_END = "# OB1_IMS_DOCKED_VESSELS_END";
const string OLD_PLAN_BEGIN = "# OB1_IMS_PB2_SETTINGS_PLAN_BEGIN";
const string OLD_PLAN_END = "# OB1_IMS_PB2_SETTINGS_PLAN_END";
const string GROUP_LEDGER_BEGIN = "# OB1_IMS_GROUP_ASSIGNMENTS_BEGIN";
const string GROUP_LEDGER_END = "# OB1_IMS_GROUP_ASSIGNMENTS_END";

const int SCAN_CHUNK = 80;
const int REF_OUTPUT_CLEAR_PASSES = 4;
const int ORE_UNLOAD_BLOCKS_PER_RUN = 28;
const int ORE_UNLOAD_MOVES_PER_RUN = 3;
const int DOCK_BUFFER_PORTS_PER_RUN = 3;
const int DOCK_BUFFER_MOVES_PER_RUN = 2;
const int MINEABLE_SWEEP_EVERY_TICKS = 60;
const int MINEABLE_SWEEP_BLOCKS_PER_PASS = 6;
const int MINEABLE_SWEEP_MOVES_PER_PASS = 2;
const int GENERAL_SWEEP_EVERY_TICKS = 30;
const int GENERAL_SWEEP_BLOCKS_PER_PASS = 6;
const int GENERAL_SWEEP_MOVES_PER_PASS = 2;
const int GROUP_CLEAN_EVERY_TICKS_DEFAULT = 60;
const int GROUP_CLEAN_CONTAINERS_DEFAULT = 8;
const int GROUP_CLEAN_MOVES_DEFAULT = 4;
const int SOFT_INSTR_LIMIT = 35000;
const int MODE_COMMIT_TICKS = 12;
const int ECHO_PERIOD_TICKS = 12;
const int MODE_OFF = 0;
const int MODE_AUTO = 1;
const int MODE_OPT = 2;
const int ICE_MODE_OFF = 0;
const int ICE_MODE_MIN = 1;
const int ICE_MODE_AUTO = 2;
const int ICE_FEED_EVERY_TICKS = 6;
const int ICE_FEED_GENS_PER_PASS = 2;
const int ICE_FEED_CARGOS_PER_PASS = 4;
const int OPT_REF_FEED_EVERY_TICKS = 6;
const int REF_TOPOFF_QTY = 3000;
const double ICE_FEED_AMOUNT = 5000.0;
const double ICE_MIN_HYSTERESIS = 2.0;
const double MANAGED_CARGO_MIN_L = 100000.0;

string[] ORE_NAMES = new string[] { "Iron", "Nickel", "Cobalt", "Silicon", "Magnesium", "Silver", "Gold", "Platinum", "Uranium", "Stone" };
string[] HV_ORES = new string[] { "Uranium", "Platinum", "Gold", "Silver" };

List<IMyTerminalBlock> scanBlocks = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> oreSourceBlocks = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> mineableSweepBlocks = new List<IMyTerminalBlock>();
List<IMyRefinery> refineries = new List<IMyRefinery>();
List<IMyAssembler> assemblers = new List<IMyAssembler>();
List<IMyGasGenerator> gasGens = new List<IMyGasGenerator>();
List<IMyCargoContainer> cargos = new List<IMyCargoContainer>();
List<IMyCargoContainer> managedCargo = new List<IMyCargoContainer>();
List<IMyShipConnector> unloadPorts = new List<IMyShipConnector>();
List<DockRow> dockRows = new List<DockRow>();
Dictionary<string, int> unloadAuth = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
Dictionary<string, double> pb1Nums = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
Dictionary<long, bool> approvedSourceGrids = new Dictionary<long, bool>();
List<MyInventoryItem> itemScratch = new List<MyInventoryItem>();
List<MyInventoryItem> itemScratch2 = new List<MyInventoryItem>();
Dictionary<long, string> groupLedger = new Dictionary<long, string>();

IMyProgrammableBlock pb1 = null;
int scanIndex = 0;
bool scanning = false;
int scanSeq = 0;
string fault = "NONE";
string managedCargoMode = "NONE";
string managedCargoTier = "NONE";
int managedCargoCandidates = 0;

bool pb1Found = false;
bool pb1PacketSeen = false;
int pb1Seq = -1;
int lastPb1Seq = -1;
int pb1AgeTicks = 999999;
bool consoleEditHold = false;

int requestedRefiningMode = MODE_OPT;
int pendingRefiningMode = MODE_OPT;
int committedRefiningMode = MODE_OPT;
int modeStableTicks = 0;

int oreUnloadMode = MODE_OFF;
int iceProcessingMode = MODE_OFF;
int ingotClearingMode = MODE_OFF;
int distributionMode = 1;
int componentStorageMode = 0;
int autoGroupLabels = 0;
string[] autoGroupPlan = new string[0];
string groupPlanText = "NONE";
int groupPlanTick = 999;
bool groupPlanDirty = true;
bool groupForceReplan = false;
int groupRegroupSeq = 0;
int lastGroupRegroupSeq = 0;
bool groupRegroupPrimed = false;
int groupRegroupAckSeq = 0;
string lastGroupRegroup = "NONE";
int autoGroupRestored = 0;
bool groupLedgerDirty = false;
int overflowIce = 0, overflowOre = 0, overflowIngot = 0, overflowRest = 0, overflowComp = 0, overflowEmerg = 0;
double currentH2 = 100.0;
double currentO2 = 100.0;
double targetH2Min = 0.0;
double targetO2Min = 0.0;
bool gasMinDemand = false;
string gasState = "OFF";
string lastIceProcessing = "NONE";
int iceFeedTick = 0;
int iceFeedGenCursor = 0;
int iceFeedCargoCursor = 0;
int iceFeedMoves = 0;

int connectedUnloadPorts = 0;
int lastConnectedUnloadPorts = -1;
int approvedUnloadDocks = 0;
int lastApprovedUnloadDocks = -1;
int riskyUnloadDocks = 0;
int blockedUnloadDocks = 0;
string unloadRiskSummary = "NONE";
string firstRiskLine = "";

string effectiveRefining = "OPTIMIZED";
bool wouldEnableRefineries = true;
bool wouldUseConveyors = false;
bool optimizedWorkersAllowed = true;
int outputRefCursor = 0;
int oreSourceCursor = 0;
int oreSourceBuildScanSeq = -1;
string oreSourceBuildSig = "";
int oreSourceGridCount = 0;
int oreUnloadMoves = 0;
int dockBufferCursor = 0;
int dockBufferMoves = 0;
int mineableSweepCursor = 0;
int mineableSweepTick = 0;
int mineableSweepMoves = 0;
int generalSweepCursor = 0;
int generalSweepTick = 0;
int generalSweepMoves = 0;
string generalSweepState = "OFF";
string lastGeneralSweep = "NONE";
string oreUnloadState = "OFF";
string lastOreUnload = "NONE";
int ingotClearMoves = 0;
string lastIngotClear = "NONE";
string ingotClearState = "OFF";
int optRefTick = 0;
int optRefCursor = 0;
int optRefMoves = 0;
int optRefAttempts = 0;
string optRefState = "OFF";
string lastOptRefinery = "NONE";
int groupCleanTick = 0;
int groupCleanCursor = 0;
int groupCleanMoves = 0;
string groupCleanState = "OFF";
string lastGroupClean = "NONE";

int lastInstr = 0;
int highInstr = 0;
int echoTick = 0;


class DockRow
{
    public string Key;
    public string Name;
    public string Dock;
    public string State;
    public IMyCubeGrid Grid;
}

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    BeginScan("COMPILE");
}

void Save() {}

void Main(string argument, UpdateType updateSource)
{
    string arg = (argument == null ? "" : argument.Trim()).ToUpperInvariant();
    if (arg.Length > 0)
    {
        if (arg == "SCAN" || arg == "DISCOVER") BeginScan("CMD");
        else if (arg == "RESET HIWATER" || arg == "RESET HIGHWATER" || arg == "HIWATER RESET") ResetHighwater();
        else if (arg == "STATUS") echoTick = ECHO_PERIOD_TICKS;
        else if (arg == "HELP") { echoTick = ECHO_PERIOD_TICKS; }
        else if (arg == "REGROUP" || arg == "REPLAN GROUPS") { groupForceReplan = true; groupPlanDirty = true; }
        else if (arg == "RESET AUTO GROUPS" || arg == "CLEAR GROUP LEDGER" || arg == "CLEAR_GROUP_LEDGER") { groupLedger.Clear(); groupLedgerDirty = true; groupPlanDirty = true; lastGroupRegroup = "LEDGER RESET"; }
    }

    if (scanning) StepScan();
    MarkInstr();
    ReadPb1Export();
    UpdateModeCommit();
    MarkInstr();
    EvaluateUnloadPorts();
    MaybeRefreshScanForOreUnload();
    MaybeRebuildOreSourceBlocks();
    MarkInstr();
    EvaluateRefineryPlan();
    ApplyRefinerySettings();
    ApplyIceProcessing();
    MarkInstr();
    ApplyOreUnload();
    MarkInstr();
    ApplyGeneralSweep();
    MarkInstr();
    ApplyIngotClearing();
    MarkInstr();
    ApplyOptimizedRefineryFeed();
    MarkInstr();
    ApplyGroupedCleanup();
    MaybeApplyGroupPlan();
    MarkInstr();
    WriteStatus();
    MarkInstr();
    MaybeEcho();
}

void MarkInstr()
{
    lastInstr = Runtime.CurrentInstructionCount;
    if (lastInstr > highInstr) highInstr = lastInstr;
}

void ResetHighwater()
{
    highInstr = Runtime.CurrentInstructionCount;
}

void BeginScan(string why)
{
    scanBlocks.Clear();
    refineries.Clear();
    assemblers.Clear();
    cargos.Clear();
    oreSourceBlocks.Clear();
    mineableSweepBlocks.Clear();
    mineableSweepCursor = 0;
    generalSweepCursor = 0;
    approvedSourceGrids.Clear();
    oreSourceBuildScanSeq = -1;
    oreSourceBuildSig = "";
    oreSourceGridCount = 0;
    managedCargo.Clear();
    unloadPorts.Clear();
    pb1 = null;
    pb1Found = false;
    scanIndex = 0;
    scanSeq++;
    scanning = true;
    groupPlanDirty = true;
    GridTerminalSystem.GetBlocks(scanBlocks);
}

bool BlockHasInstallTag(IMyTerminalBlock b)
{
    return b != null && HasTag(b.CustomName, INSTALL_TAG);
}

void StepScan()
{
    int end = scanIndex + SCAN_CHUNK;
    if (end > scanBlocks.Count) end = scanBlocks.Count;
    for (int i = scanIndex; i < end; i++) ClassifyBlock(scanBlocks[i]);
    scanIndex = end;
    if (scanIndex >= scanBlocks.Count)
    {
        scanning = false;
        SelectManagedCargo();
        ApplyAutoGroupLabels();
        PruneManagedMineableSweepBlocks();
    }
}

void ClassifyBlock(IMyTerminalBlock b)
{
    if (b == null) return;
    IMyProgrammableBlock p = b as IMyProgrammableBlock;
    bool own = BlockHasInstallTag(b);
    if (p != null && own)
    {
        if (p != Me && HasTag(p.CustomName, IMS_TAG) && HasTag(p.CustomName, PB1_TAG))
        {
            if (pb1 == null) pb1 = p;
            pb1Found = true;
        }
    }

    IMyRefinery r = b as IMyRefinery;
    if (r != null && own) refineries.Add(r);

    IMyAssembler asm = b as IMyAssembler;
    if (asm != null && own) assemblers.Add(asm);

    IMyGasGenerator gen = b as IMyGasGenerator;
    if (gen != null && own) gasGens.Add(gen);

    IMyCargoContainer box = b as IMyCargoContainer;
    if (box != null && own) cargos.Add(box);

    IMyShipConnector c = b as IMyShipConnector;
    if (c != null && own && HasTag(b.CustomName, UNLOAD_TAG)) unloadPorts.Add(c);

    if (own && IsMineableSweepCandidate(b)) mineableSweepBlocks.Add(b);

}

void ReadPb1Export()
{
    if (pb1 == null)
    {
        pb1Found = false;
        pb1PacketSeen = false;
        pb1AgeTicks++;
        return;
    }
    pb1Found = true;
    string block = ExtractDataBlock(Me.CustomData, PB1_EXPORT_BEGIN, PB1_EXPORT_END);
    if (block.Length == 0)
    {
        block = ExtractDataBlock(pb1.CustomData, PB1_EXPORT_BEGIN, PB1_EXPORT_END);
    }
    if (block.Length == 0)
    {
        pb1PacketSeen = false;
        pb1AgeTicks++;
        return;
    }

    pb1PacketSeen = true;
    unloadAuth.Clear();
    pb1Nums.Clear();
    bool sawSeq = false;
    string[] lines = block.Replace("\r", "").Split('\n');
    for (int i = 0; i < lines.Length; i++)
    {
        string line = lines[i].Trim();
        if (line.Length == 0 || line.StartsWith("#")) continue;
        if (line.StartsWith("AUTH|", StringComparison.OrdinalIgnoreCase)) { ParseAuthLine(line); continue; }
        int eq = line.IndexOf('=');
        if (eq < 0) continue;
        string key = line.Substring(0, eq).Trim();
        string val = line.Substring(eq + 1).Trim();
        if (key.StartsWith("Current.", StringComparison.OrdinalIgnoreCase) || key.StartsWith("Target.", StringComparison.OrdinalIgnoreCase)) pb1Nums[key] = ParseDouble(val, 0);
        int n;
        if (key.Equals("Seq", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out n)) { pb1Seq = n; sawSeq = true; }
        else if (key.Equals("Console.EditHold", StringComparison.OrdinalIgnoreCase)) consoleEditHold = (val == "1" || val.Equals("TRUE", StringComparison.OrdinalIgnoreCase));
        else if (key.Equals("Mode.REFINERY.REFINING", StringComparison.OrdinalIgnoreCase)) requestedRefiningMode = ParseModeNumber(val, requestedRefiningMode);
        else if (key.Equals("Mode.REFINERY.ORE_UNLOAD", StringComparison.OrdinalIgnoreCase)) oreUnloadMode = ParseModeNumber(val, oreUnloadMode);
        else if (key.Equals("Mode.REFINERY.ICE_PROCESSING", StringComparison.OrdinalIgnoreCase)) iceProcessingMode = ParseModeNumber(val, iceProcessingMode);
        else if (key.Equals("Mode.REFINERY.INGOT_CLEARING", StringComparison.OrdinalIgnoreCase)) ingotClearingMode = ParseModeNumber(val, ingotClearingMode);
        else if (key.Equals("Mode.SYSTEM.DISTRIBUTION_METHOD", StringComparison.OrdinalIgnoreCase)) { int old = distributionMode; distributionMode = ClampInt(ParseModeNumber(val, distributionMode), 0, 1); if (old != distributionMode) groupPlanDirty = true; }
        else if (key.Equals("Mode.SYSTEM.COMPONENT_STORAGE", StringComparison.OrdinalIgnoreCase)) { int old = componentStorageMode; componentStorageMode = ClampInt(ParseModeNumber(val, componentStorageMode), 0, 1); if (old != componentStorageMode) groupPlanDirty = true; }
        else if (key.Equals("Cmd.SYSTEM.GROUP_REGROUP_SEQ", StringComparison.OrdinalIgnoreCase)) { int cmdSeq; if (int.TryParse(val, out cmdSeq)) groupRegroupSeq = cmdSeq; }
        else if (key.Equals("Current.SUPPLY.H2", StringComparison.OrdinalIgnoreCase)) currentH2 = ParseDouble(val, currentH2);
        else if (key.Equals("Current.SUPPLY.O2", StringComparison.OrdinalIgnoreCase)) currentO2 = ParseDouble(val, currentO2);
        else if (key.Equals("Target.SUPPLY.H2.Min", StringComparison.OrdinalIgnoreCase)) targetH2Min = ParseDouble(val, targetH2Min);
        else if (key.Equals("Target.SUPPLY.O2.Min", StringComparison.OrdinalIgnoreCase)) targetO2Min = ParseDouble(val, targetO2Min);
    }
    if (sawSeq && pb1Seq != lastPb1Seq)
    {
        lastPb1Seq = pb1Seq;
        pb1AgeTicks = 0;
    }
    else pb1AgeTicks++;
    if (!groupRegroupPrimed)
    {
        lastGroupRegroupSeq = groupRegroupSeq;
        groupRegroupPrimed = true;
    }
    else if (groupRegroupSeq > 0 && groupRegroupSeq != lastGroupRegroupSeq)
    {
        lastGroupRegroupSeq = groupRegroupSeq;
        groupForceReplan = true;
        groupPlanDirty = true;
        groupRegroupAckSeq = groupRegroupSeq;
        lastGroupRegroup = "ACK " + groupRegroupSeq.ToString();
    }
}

int ParseModeNumber(string text, int fallback)
{
    int n;
    if (int.TryParse(text, out n)) return ClampInt(n, 0, 9);
    string v = (text == null ? "" : text.Trim().ToUpperInvariant());
    if (v == "OFF") return MODE_OFF;
    if (v == "AUTO") return MODE_AUTO;
    if (v == "OPTIMIZED" || v == "OPT") return MODE_OPT;
    if (v == "ON") return 2;
    return fallback;
}

double ParseDouble(string text, double fallback)
{
    double d;
    if (double.TryParse(text, out d)) return d;
    return fallback;
}

void UpdateModeCommit()
{
    requestedRefiningMode = ClampInt(requestedRefiningMode, MODE_OFF, MODE_OPT);
    if (requestedRefiningMode != pendingRefiningMode)
    {
        pendingRefiningMode = requestedRefiningMode;
        modeStableTicks = 0;
    }
    else if (!consoleEditHold && modeStableTicks < MODE_COMMIT_TICKS) modeStableTicks++;

    if (!consoleEditHold && modeStableTicks >= MODE_COMMIT_TICKS) committedRefiningMode = pendingRefiningMode;
}

void EvaluateUnloadPorts()
{
    connectedUnloadPorts = 0;
    approvedUnloadDocks = 0;
    riskyUnloadDocks = 0;
    blockedUnloadDocks = 0;
    unloadRiskSummary = "NONE";
    firstRiskLine = "";
    dockRows.Clear();

    for (int i = 0; i < unloadPorts.Count; i++)
    {
        IMyShipConnector c = unloadPorts[i];
        if (c == null) continue;
        if (c.Status != MyShipConnectorStatus.Connected) continue;
        IMyShipConnector other = c.OtherConnector;
        if (other == null) continue;
        connectedUnloadPorts++;

        DockRow row = BuildDockRow(c, other);
        dockRows.Add(row);

        bool hardBlock = row.State.StartsWith("BLOCK");
        bool auth = row.State == "AUTH";
        if (auth) approvedUnloadDocks++;
        else if (hardBlock) { blockedUnloadDocks++; riskyUnloadDocks++; }
        else riskyUnloadDocks++;

        if (!auth && firstRiskLine.Length == 0) firstRiskLine = "Dock " + row.Dock + " " + row.Name + " -> " + row.State;
    }
    if (riskyUnloadDocks > 0) unloadRiskSummary = "RISK " + riskyUnloadDocks.ToString() + "/" + connectedUnloadPorts.ToString();
}

DockRow BuildDockRow(IMyShipConnector own, IMyShipConnector other)
{
    DockRow r = new DockRow();
    r.Grid = other == null ? null : other.CubeGrid;
    r.Key = GridKey(r.Grid);
    r.Name = VesselDisplayName(other);
    r.Dock = DockNumber(own);
    r.State = DockAuthState(other, r.Key);
    return r;
}

string DockAuthState(IMyShipConnector other, string key)
{
    int st;
    if (key != null && unloadAuth.TryGetValue(key, out st)) return st > 0 ? "AUTH" : "DENY";
    return "PENDING";
}

void ParseAuthLine(string line)
{
    string[] p = line.Split('|');
    if (p.Length < 3) return;
    string key = p[1].Trim();
    string state = p[2].Trim().ToUpperInvariant();
    if (key.Length == 0) return;
    if (state == "AUTH") unloadAuth[key] = 1;
    else if (state == "DENY") unloadAuth[key] = -1;
}

string GridKey(IMyCubeGrid g)
{
    if (g == null) return "0";
    return g.EntityId.ToString();
}

string VesselDisplayName(IMyShipConnector other)
{
    string n = "";
    if (other != null && other.CubeGrid != null) n = Safe(other.CubeGrid.CustomName).Trim();
    if (n.Length == 0 && other != null) n = Safe(other.CustomName).Trim();
    if (n.Length == 0) n = "UNKNOWN";
    n = StripTags(n).Trim();
    string h = ShortHash(GridKey(other == null ? null : other.CubeGrid));
    if (h.Length > 0) n = n + "-" + h;
    if (n.Length > 24) n = n.Substring(0, 24);
    return n;
}

string StripTags(string text)
{
    if (text == null) return "";
    StringBuilder b = new StringBuilder();
    bool inTag = false;
    for (int i = 0; i < text.Length; i++)
    {
        char ch = text[i];
        if (ch == '[') { inTag = true; continue; }
        if (ch == ']') { inTag = false; continue; }
        if (!inTag) b.Append(ch);
    }
    return b.ToString().Trim();
}

string ShortHash(string key)
{
    if (key == null) return "";
    string k = key.Trim();
    if (k.Length <= 4) return k;
    return k.Substring(k.Length - 4);
}

string DockNumber(IMyTerminalBlock b)
{
    string n = b == null ? "" : Safe(b.CustomName);
    StringBuilder digits = new StringBuilder();
    bool inTag = false;
    bool started = false;
    for (int i = 0; i < n.Length; i++)
    {
        char c = n[i];
        if (c == '[') { inTag = true; continue; }
        if (c == ']') { inTag = false; continue; }
        if (inTag) continue;
        if (c >= '0' && c <= '9') { digits.Append(c); started = true; }
        else if (started) break;
    }
    return digits.Length > 0 ? digits.ToString() : "?";
}

bool IsMineable(MyInventoryItem item)
{
    string type = item.Type.TypeId.ToString();
    string sub = item.Type.SubtypeId.ToString();
    if (type.IndexOf("Ore", StringComparison.OrdinalIgnoreCase) >= 0) return true;
    if (sub.Equals("Stone", StringComparison.OrdinalIgnoreCase)) return true;
    if (sub.Equals("Ice", StringComparison.OrdinalIgnoreCase)) return true;
    return false;
}

void MaybeRefreshScanForOreUnload()
{
    if (scanning) return;
    if (oreUnloadMode <= 0)
    {
        lastConnectedUnloadPorts = connectedUnloadPorts;
        lastApprovedUnloadDocks = approvedUnloadDocks;
        return;
    }
    if (connectedUnloadPorts != lastConnectedUnloadPorts || approvedUnloadDocks != lastApprovedUnloadDocks)
    {
        lastConnectedUnloadPorts = connectedUnloadPorts;
        lastApprovedUnloadDocks = approvedUnloadDocks;
        BeginScan("DOCK_CHANGE");
    }
}

string OreSourceSignature()
{
    StringBuilder b = new StringBuilder();
    for (int i = 0; i < dockRows.Count; i++)
    {
        DockRow r = dockRows[i];
        if (r == null) continue;
        b.Append(r.Key).Append(':').Append(r.State).Append(';');
    }
    return b.ToString();
}

void MaybeRebuildOreSourceBlocks()
{
    if (scanning) return;
    if (oreUnloadMode <= 0) return;
    string sig = OreSourceSignature();
    if (oreSourceBuildScanSeq == scanSeq && oreSourceBuildSig == sig) return;
    RebuildOreSourceBlocks(sig);
}

void RebuildOreSourceBlocks(string sig)
{
    oreSourceBlocks.Clear();
    approvedSourceGrids.Clear();
    oreSourceGridCount = 0;

    for (int i = 0; i < dockRows.Count; i++)
    {
        DockRow r = dockRows[i];
        if (r == null || r.State != "AUTH" || r.Grid == null) continue;
        AddApprovedSourceGrid(r.Grid);
    }

    bool changed = true;
    int guard = 0;
    while (changed && guard < 24)
    {
        changed = false;
        guard++;
        for (int i = 0; i < scanBlocks.Count; i++)
        {
            IMyTerminalBlock b = scanBlocks[i];
            if (b == null || BlockHasInstallTag(b)) continue;

            IMyMotorStator rotor = b as IMyMotorStator;
            if (rotor != null)
            {
                if (AddSourceMechanicalPair(rotor.CubeGrid, rotor.TopGrid)) changed = true;
                continue;
            }

            IMyPistonBase piston = b as IMyPistonBase;
            if (piston != null)
            {
                if (AddSourceMechanicalPair(piston.CubeGrid, piston.TopGrid)) changed = true;
                continue;
            }
        }
    }

    for (int i = 0; i < scanBlocks.Count; i++)
    {
        IMyTerminalBlock b = scanBlocks[i];
        if (b == null || BlockHasInstallTag(b)) continue;
        if (b.InventoryCount < 1) continue;
        if (IsApprovedSourceGrid(b.CubeGrid)) oreSourceBlocks.Add(b);
    }

    oreSourceGridCount = approvedSourceGrids.Count;
    oreSourceCursor = 0;
    oreSourceBuildScanSeq = scanSeq;
    oreSourceBuildSig = sig;
}

bool AddApprovedSourceGrid(IMyCubeGrid g)
{
    if (g == null) return false;
    long id = g.EntityId;
    if (approvedSourceGrids.ContainsKey(id)) return false;
    approvedSourceGrids[id] = true;
    return true;
}

bool AddSourceMechanicalPair(IMyCubeGrid a, IMyCubeGrid b)
{
    if (a == null || b == null) return false;
    bool aa = IsApprovedSourceGrid(a);
    bool bb = IsApprovedSourceGrid(b);
    if (aa == bb) return false;
    return AddApprovedSourceGrid(aa ? b : a);
}

bool IsApprovedSourceGrid(IMyCubeGrid g)
{
    return g != null && approvedSourceGrids.ContainsKey(g.EntityId);
}

void EvaluateRefineryPlan()
{
    optimizedWorkersAllowed = false;
    wouldEnableRefineries = false;
    wouldUseConveyors = false;
    effectiveRefining = ModeName(committedRefiningMode);

    if (committedRefiningMode == MODE_OFF)
    {
        wouldEnableRefineries = false;
        wouldUseConveyors = false;
    }
    else if (committedRefiningMode == MODE_AUTO)
    {
        wouldEnableRefineries = true;
        if (riskyUnloadDocks > 0)
        {
            wouldUseConveyors = false;
            effectiveRefining = "AUTO_BLOCKED_UNLOAD_RISK";
        }
        else wouldUseConveyors = true;
    }
    else
    {
        wouldEnableRefineries = true;
        wouldUseConveyors = false;
        optimizedWorkersAllowed = true;
    }
}


void ApplyRefinerySettings()
{
    if (scanning) return;
    for (int i = 0; i < refineries.Count; i++)
    {
        IMyRefinery r = refineries[i];
        if (r == null) continue;
        if (r.Enabled != wouldEnableRefineries) r.Enabled = wouldEnableRefineries;
        if (r.UseConveyorSystem != wouldUseConveyors) r.UseConveyorSystem = wouldUseConveyors;
    }
}

void ApplyIceProcessing()
{
    if (scanning) return;
    iceProcessingMode = ClampInt(iceProcessingMode, 0, 2);
    if (gasGens.Count < 1) { gasState = "NO_GENS"; lastIceProcessing = "NO H2O2 GENS"; return; }

    if (iceProcessingMode == ICE_MODE_MIN)
    {
        if (!gasMinDemand && (currentH2 < targetH2Min || currentO2 < targetO2Min)) gasMinDemand = true;
        else if (gasMinDemand && currentH2 >= targetH2Min + ICE_MIN_HYSTERESIS && currentO2 >= targetO2Min + ICE_MIN_HYSTERESIS) gasMinDemand = false;
    }
    else gasMinDemand = iceProcessingMode == ICE_MODE_AUTO;

    bool wantOn = iceProcessingMode == ICE_MODE_AUTO || (iceProcessingMode == ICE_MODE_MIN && gasMinDemand);
    bool wantConv = iceProcessingMode == ICE_MODE_AUTO;
    int on = 0, conv = 0;
    for (int i = 0; i < gasGens.Count; i++)
    {
        IMyGasGenerator g = gasGens[i];
        if (g == null) continue;
        IMyFunctionalBlock f = g as IMyFunctionalBlock;
        if (f != null && f.Enabled != wantOn) f.Enabled = wantOn;
        if (g.UseConveyorSystem != wantConv) g.UseConveyorSystem = wantConv;
        if (f != null && f.Enabled) on++;
        if (g.UseConveyorSystem) conv++;
    }

    gasState = IceModeName(iceProcessingMode) + " " + (wantOn ? "ON" : "OFF") + " " + on.ToString() + "/" + gasGens.Count.ToString() + " CONV " + conv.ToString();
    if (iceProcessingMode == ICE_MODE_MIN) gasState += " H2 " + currentH2.ToString("0") + "/" + targetH2Min.ToString("0") + " O2 " + currentO2.ToString("0") + "/" + targetO2Min.ToString("0");

    if (iceProcessingMode == ICE_MODE_MIN && gasMinDemand && managedCargo.Count > 0)
    {
        int moved = ExecuteManagedIceFeedPass();
        if (moved > 0) lastIceProcessing = "ICE FEED x" + moved.ToString();
        else if (lastIceProcessing.Length == 0 || lastIceProcessing == "NONE") lastIceProcessing = "ICE FEED WAIT";
    }
    else if (iceProcessingMode == ICE_MODE_MIN && !gasMinDemand) lastIceProcessing = "MIN SATISFIED";
    else if (iceProcessingMode == ICE_MODE_AUTO) lastIceProcessing = "AUTO KEEN FEED";
    else if (iceProcessingMode == ICE_MODE_OFF) lastIceProcessing = "OFF";
}

int ExecuteManagedIceFeedPass()
{
    if (++iceFeedTick < ICE_FEED_EVERY_TICKS) return 0;
    iceFeedTick = 0;
    int moved = 0;
    int genCount = gasGens.Count;
    if (genCount < 1 || managedCargo.Count < 1) return 0;
    if (iceFeedGenCursor < 0 || iceFeedGenCursor >= genCount) iceFeedGenCursor = 0;
    int checkedGens = 0;
    while (checkedGens < genCount && checkedGens < ICE_FEED_GENS_PER_PASS && moved < 1)
    {
        if (SoftBudgetHit()) { lastIceProcessing = "YIELD ICE"; break; }
        IMyGasGenerator g = gasGens[iceFeedGenCursor];
        iceFeedGenCursor++;
        if (iceFeedGenCursor >= genCount) iceFeedGenCursor = 0;
        checkedGens++;
        IMyFunctionalBlock f = g as IMyFunctionalBlock;
        if (g == null || f == null || !f.Enabled || g.InventoryCount < 1) continue;
        IMyInventory dst = g.GetInventory(0);
        if (dst == null) continue;
        if (TryFeedIceToGenerator(dst, g)) moved++;
    }
    return moved;
}

bool TryFeedIceToGenerator(IMyInventory dst, IMyTerminalBlock gen)
{
    int cargoCount = managedCargo.Count;
    if (cargoCount < 1) return false;
    if (iceFeedCargoCursor < 0 || iceFeedCargoCursor >= cargoCount) iceFeedCargoCursor = 0;
    int checkedCargo = 0;
    while (checkedCargo < cargoCount && checkedCargo < ICE_FEED_CARGOS_PER_PASS)
    {
        IMyCargoContainer c = managedCargo[iceFeedCargoCursor];
        iceFeedCargoCursor++;
        if (iceFeedCargoCursor >= cargoCount) iceFeedCargoCursor = 0;
        checkedCargo++;
        if (c == null || c.InventoryCount < 1) continue;
        IMyInventory src = c.GetInventory(0);
        if (src == null) continue;
        itemScratch.Clear();
        src.GetItems(itemScratch);
        for (int i = 0; i < itemScratch.Count; i++)
        {
            MyInventoryItem it = itemScratch[i];
            if (!IsIce(it)) continue;
            double qty = (double)it.Amount;
            if (qty <= 0.0) continue;
            MyFixedPoint amt = (MyFixedPoint)Math.Min(qty, ICE_FEED_AMOUNT);
            if (!dst.CanItemsBeAdded(amt, it.Type))
            {
                amt = (MyFixedPoint)Math.Min(qty, 1000.0);
                if (!dst.CanItemsBeAdded(amt, it.Type))
                {
                    amt = (MyFixedPoint)Math.Min(qty, 100.0);
                    if (!dst.CanItemsBeAdded(amt, it.Type)) continue;
                }
            }
            bool ok = src.TransferItemTo(dst, it, amt);
            if (ok)
            {
                iceFeedMoves++;
                lastIceProcessing = "ICE " + ShortName(c) + " -> " + ShortName(gen) + " " + FormatCompact((double)amt);
                return true;
            }
            lastIceProcessing = "ICE TRANSFER FAIL " + ShortName(c) + " -> " + ShortName(gen);
            return false;
        }
    }
    lastIceProcessing = "NO ICE IN MANAGED CARGO";
    return false;
}

bool IsIce(MyInventoryItem item)
{
    string sub = item.Type.SubtypeId.ToString();
    return sub.Equals("Ice", StringComparison.OrdinalIgnoreCase);
}

void ApplyOreUnload()
{
    oreUnloadState = oreUnloadMode > 0 ? "ON" : "OFF";
    if (scanning) return;
    if (oreUnloadMode <= 0) { lastOreUnload = "OFF"; return; }
    if (managedCargo.Count < 1) { oreUnloadState = "FAULT_NO_MANAGED_CARGO"; lastOreUnload = "NO MANAGED CARGO"; return; }
    if (approvedUnloadDocks < 1) { oreUnloadState = "NO_AUTH_DOCK"; lastOreUnload = "NO AUTH DOCK"; return; }
    int moved = ExecuteOreUnloadPass();
    int bufferMoved = ExecuteDockBufferSweepPass();
    int sweepMoved = ExecuteMineableSweepPass();
    if (moved + bufferMoved + sweepMoved > 0) oreUnloadState = "MOVED";
}

int ExecuteOreUnloadPass()
{
    int moved = 0;
    int n = oreSourceBlocks.Count;
    if (n < 1) { lastOreUnload = "NO AUTH SOURCE BLOCKS"; return 0; }
    if (oreSourceCursor < 0 || oreSourceCursor >= n) oreSourceCursor = 0;
    int checkedBlocks = 0;
    while (checkedBlocks < n && checkedBlocks < ORE_UNLOAD_BLOCKS_PER_RUN && moved < ORE_UNLOAD_MOVES_PER_RUN)
    {
        if (SoftBudgetHit()) { oreUnloadState = "YIELD"; lastOreUnload = "YIELD ORE"; break; }
        IMyTerminalBlock b = oreSourceBlocks[oreSourceCursor];
        oreSourceCursor++;
        if (oreSourceCursor >= n) oreSourceCursor = 0;
        checkedBlocks++;
        if (b == null) continue;
        if (!IsApprovedSourceGrid(b.CubeGrid)) continue;
        if (TryUnloadMineablesFromBlock(b)) moved++;
    }
    if (moved < 1 && lastOreUnload != "YIELD ORE") lastOreUnload = "SRC " + checkedBlocks.ToString() + "/" + n.ToString() + " NO MINEABLE MOVE";
    return moved;
}

int ExecuteDockBufferSweepPass()
{
    int moved = 0;
    int n = unloadPorts.Count;
    if (n < 1 || approvedUnloadDocks < 1) return 0;
    if (dockBufferCursor < 0 || dockBufferCursor >= n) dockBufferCursor = 0;
    int checkedPorts = 0;
    while (checkedPorts < n && checkedPorts < DOCK_BUFFER_PORTS_PER_RUN && moved < DOCK_BUFFER_MOVES_PER_RUN)
    {
        if (SoftBudgetHit()) { oreUnloadState = "YIELD"; lastOreUnload = "YIELD BUFFER"; break; }
        IMyShipConnector c = unloadPorts[dockBufferCursor];
        dockBufferCursor++;
        if (dockBufferCursor >= n) dockBufferCursor = 0;
        checkedPorts++;
        if (c == null || c.Status != MyShipConnectorStatus.Connected) continue;
        if (!IsAuthorizedUnloadPort(c)) continue;
        if (TrySweepDockBuffer(c)) moved++;
    }
    return moved;
}

bool IsAuthorizedUnloadPort(IMyShipConnector own)
{
    if (own == null || own.Status != MyShipConnectorStatus.Connected || own.OtherConnector == null) return false;
    string key = GridKey(own.OtherConnector.CubeGrid);
    for (int i = 0; i < dockRows.Count; i++)
    {
        DockRow r = dockRows[i];
        if (r != null && r.Key == key && r.State == "AUTH") return true;
    }
    return false;
}

int ExecuteMineableSweepPass()
{
    if (oreUnloadMode <= 0 || approvedUnloadDocks < 1) return 0;
    if (++mineableSweepTick < MINEABLE_SWEEP_EVERY_TICKS) return 0;
    mineableSweepTick = 0;
    int n = mineableSweepBlocks.Count;
    if (n < 1) return 0;
    if (mineableSweepCursor < 0 || mineableSweepCursor >= n) mineableSweepCursor = 0;
    int checkedBlocks = 0, moved = 0;
    while (checkedBlocks < n && checkedBlocks < MINEABLE_SWEEP_BLOCKS_PER_PASS && moved < MINEABLE_SWEEP_MOVES_PER_PASS)
    {
        if (SoftBudgetHit()) { oreUnloadState = "YIELD"; lastOreUnload = "YIELD SWEEP"; break; }
        IMyTerminalBlock b = mineableSweepBlocks[mineableSweepCursor++];
        if (mineableSweepCursor >= n) mineableSweepCursor = 0;
        checkedBlocks++;
        if (b == null || !IsMineableSweepCandidate(b)) continue;
        if (TrySweepMineablesFromBlock(b, "SWEEP")) moved++;
    }
    return moved;
}

bool TrySweepDockBuffer(IMyTerminalBlock b)
{
    return TryMoveMineablesToCargo(b, "BUFFER", 1, 1);
}

bool TrySweepMineablesFromBlock(IMyTerminalBlock b, string label)
{
    return TryMoveMineablesToCargo(b, label, b == null ? 0 : b.InventoryCount, 2);
}

bool TryUnloadMineablesFromBlock(IMyTerminalBlock b)
{
    return TryMoveMineablesToCargo(b, "", b == null ? 0 : b.InventoryCount, 0);
}

bool TryMoveMineablesToCargo(IMyTerminalBlock b, string label, int invLimit, int counter)
{
    if (b == null || b.InventoryCount < 1 || invLimit < 1) return false;
    int lim = invLimit < b.InventoryCount ? invLimit : b.InventoryCount;
    for (int invIndex = 0; invIndex < lim; invIndex++)
    {
        IMyInventory src = b.GetInventory(invIndex);
        if (src == null) continue;
        itemScratch.Clear();
        src.GetItems(itemScratch);
        for (int i = 0; i < itemScratch.Count; i++)
        {
            MyInventoryItem it = itemScratch[i];
            if (!IsMineable(it)) continue;
            double qty = (double)it.Amount;
            if (qty <= 0.0) continue;
            int cargoIndex = ChooseCargoForItem(it, it.Amount);
            string tag = label == "BUFFER" ? "BUFFER " + DockNumber(b) : (label.Length > 0 ? label + " " + ShortName(b) : ShortName(b));
            if (cargoIndex < 0)
            {
                oreUnloadState = "FAULT_NO_SPACE";
                lastOreUnload = (label.Length > 0 ? label + " " : "") + "NO SPACE " + ItemShort(it) + " " + FormatCompact(qty);
                return false;
            }
            IMyInventory dst = managedCargo[cargoIndex].GetInventory(0);
            bool ok = src.TransferItemTo(dst, it, (MyFixedPoint?)null);
            if (ok)
            {
                oreUnloadMoves++;
                if (counter == 1) dockBufferMoves++;
                else if (counter == 2) mineableSweepMoves++;
                lastOreUnload = tag + " " + ItemShort(it) + " -> " + ShortName(managedCargo[cargoIndex]) + " " + FormatCompact(qty);
                return true;
            }
            oreUnloadState = "TRANSFER_FAIL";
            lastOreUnload = tag + " " + ItemShort(it) + " transfer rejected";
            return false;
        }
    }
    return false;
}

void ApplyGeneralSweep()
{
    generalSweepState = "ON";
    if (scanning) return;
    if (managedCargo.Count < 1) { generalSweepState = "NO_CARGO"; lastGeneralSweep = "NO MANAGED"; return; }
    int moved = ExecuteGeneralSweepPass();
    if (moved > 0) generalSweepState = "MOVED";
}

int ExecuteGeneralSweepPass()
{
    if (++generalSweepTick < GENERAL_SWEEP_EVERY_TICKS) return 0;
    generalSweepTick = 0;
    int n = mineableSweepBlocks.Count;
    if (n < 1) return 0;
    if (generalSweepCursor < 0 || generalSweepCursor >= n) generalSweepCursor = 0;
    int checkedBlocks = 0, moved = 0;
    while (checkedBlocks < n && checkedBlocks < GENERAL_SWEEP_BLOCKS_PER_PASS && moved < GENERAL_SWEEP_MOVES_PER_PASS)
    {
        if (SoftBudgetHit()) { generalSweepState = "YIELD"; lastGeneralSweep = "YIELD"; break; }
        IMyTerminalBlock b = mineableSweepBlocks[generalSweepCursor++];
        if (generalSweepCursor >= n) generalSweepCursor = 0;
        checkedBlocks++;
        if (!IsGeneralSweepCandidate(b)) continue;
        if (TryMoveGeneralItemsToCargo(b)) moved++;
    }
    if (moved < 1 && lastGeneralSweep == "NONE") lastGeneralSweep = "NO MOVE";
    return moved;
}

bool IsGeneralSweepCandidate(IMyTerminalBlock b)
{
    if (!IsMineableSweepCandidate(b)) return false;
    return b is IMyCargoContainer || b is IMyShipConnector;
}

bool IsGeneralSweepItem(MyInventoryItem it)
{
    string t = Safe(it.Type.TypeId.ToString());
    return t.IndexOf("Component", StringComparison.OrdinalIgnoreCase) >= 0;
}

bool TryMoveGeneralItemsToCargo(IMyTerminalBlock b)
{
    if (b == null || b.InventoryCount < 1) return false;
    int lim = b.InventoryCount;
    for (int invIndex = 0; invIndex < lim; invIndex++)
    {
        IMyInventory src = b.GetInventory(invIndex);
        if (src == null) continue;
        itemScratch.Clear();
        src.GetItems(itemScratch);
        for (int i = 0; i < itemScratch.Count; i++)
        {
            MyInventoryItem it = itemScratch[i];
            if (!IsGeneralSweepItem(it)) continue;
            int dstIndex = ChooseCargoForItem(it, it.Amount);
            if (dstIndex < 0) { generalSweepState = "NO_SPACE"; lastGeneralSweep = "NO SPACE " + ItemShort(it); return false; }
            IMyInventory dst = managedCargo[dstIndex].GetInventory(0);
            if (dst == null) continue;
            bool ok = src.TransferItemTo(dst, it, (MyFixedPoint?)null);
            if (ok)
            {
                generalSweepMoves++;
                lastGeneralSweep = "COMP " + ItemShort(it) + " " + ShortName(b) + " -> " + ShortName(managedCargo[dstIndex]) + " " + FormatCompact((double)it.Amount);
                return true;
            }
            generalSweepState = "TRANSFER_FAIL";
            lastGeneralSweep = "COMP " + ItemShort(it) + " transfer rejected";
            return false;
        }
    }
    return false;
}



void ApplyGroupedCleanup()
{
    groupCleanState = distributionMode > 0 ? "GROUPED" : "REDUNDANT";
    if (scanning) return;
    if (managedCargo.Count < 2) { groupCleanState = "NO_CARGO"; return; }
    if (++groupCleanTick < GROUP_CLEAN_EVERY_TICKS_DEFAULT) return;
    groupCleanTick = 0;
    int moved = distributionMode > 0 ? ExecuteGroupedCleanupPass() : ExecuteRedundantCleanupPass();
    if (moved < 1 && lastGroupClean == "NONE") lastGroupClean = distributionMode > 0 ? "NO MISPLACED" : "NO SPREAD";
}

int ExecuteGroupedCleanupPass()
{
    int startInstr = Runtime.CurrentInstructionCount;
    int n = managedCargo.Count;
    if (n < 2) return 0;
    if (groupCleanCursor < 0 || groupCleanCursor >= n) groupCleanCursor = 0;
    int checkedBlocks = 0, moved = 0;
    lastGroupClean = "NONE";
    groupCleanState = "IDLE";
    while (checkedBlocks < n && checkedBlocks < GROUP_CLEAN_CONTAINERS_DEFAULT && moved < GROUP_CLEAN_MOVES_DEFAULT)
    {
        if (SoftBudgetHit()) { groupCleanState = "YIELD"; lastGroupClean = "YIELD GROUP"; break; }
        IMyCargoContainer c = managedCargo[groupCleanCursor++];
        if (groupCleanCursor >= n) groupCleanCursor = 0;
        checkedBlocks++;
        if (c == null || c.InventoryCount < 1) continue;
        if (TryMoveMisplacedGroupedItem(c)) moved++;
    }
    if (moved > 0) groupCleanState = "MOVED";
    return moved;
}

int ExecuteRedundantCleanupPass()
{
    int startInstr = Runtime.CurrentInstructionCount;
    int n = managedCargo.Count;
    if (n < 2) return 0;
    if (groupCleanCursor < 0 || groupCleanCursor >= n) groupCleanCursor = 0;
    int checkedBlocks = 0, moved = 0;
    lastGroupClean = "NONE";
    groupCleanState = "RED IDLE";
    while (checkedBlocks < n && checkedBlocks < GROUP_CLEAN_CONTAINERS_DEFAULT && moved < GROUP_CLEAN_MOVES_DEFAULT)
    {
        if (SoftBudgetHit()) { groupCleanState = "YIELD"; lastGroupClean = "YIELD RED"; break; }
        IMyCargoContainer c = managedCargo[groupCleanCursor++];
        if (groupCleanCursor >= n) groupCleanCursor = 0;
        checkedBlocks++;
        if (c == null || c.InventoryCount < 1) continue;
        if (TrySpreadRedundantItem(c)) moved++;
    }
    if (moved > 0) groupCleanState = "RED MOVED";
    return moved;
}

bool TrySpreadRedundantItem(IMyCargoContainer c)
{
    int srcIndex = ManagedCargoIndex(c);
    if (srcIndex < 0) return false;
    IMyInventory src = c.GetInventory(0);
    if (src == null) return false;
    itemScratch.Clear();
    src.GetItems(itemScratch);
    for (int pri = 5; pri >= 1; pri--)
    {
        for (int i = 0; i < itemScratch.Count; i++)
        {
            MyInventoryItem it = itemScratch[i];
            if (RedundantPriority(it) != pri) continue;
            double srcAmt = ItemAmountIn(src, it);
            if (srcAmt <= 0.0) continue;
            int dstIndex = ChooseRedundantDest(it, srcIndex, srcAmt);
            if (dstIndex < 0) continue;
            IMyInventory dst = managedCargo[dstIndex].GetInventory(0);
            if (dst == null) continue;
            double dstAmt = ItemAmountIn(dst, it);
            double move = Math.Min((double)it.Amount, Math.Max(1.0, (srcAmt - dstAmt) * 0.5));
            if (IsDiscreteItem(it)) move = Math.Floor(move);
            if (move <= 0.0) continue;
            bool ok = src.TransferItemTo(dst, it, (MyFixedPoint)move);
            if (ok)
            {
                groupCleanMoves++;
                lastGroupClean = "RED " + ItemShort(it) + " " + ShortName(c) + " -> " + ShortName(managedCargo[dstIndex]) + " " + FormatCompact(move);
                return true;
            }
            groupCleanState = "TRANSFER_FAIL";
            lastGroupClean = "RED " + ItemShort(it) + " transfer rejected";
            return false;
        }
    }
    return false;
}

int ChooseRedundantDest(MyInventoryItem it, int srcIndex, double srcAmt)
{
    int best = -1;
    double bestAmt = srcAmt;
    for (int pass = 0; pass < 2; pass++)
    {
        for (int i = 0; i < managedCargo.Count; i++)
        {
            if (i == srcIndex) continue;
            IMyCargoContainer c = managedCargo[i];
            if (c == null || c.InventoryCount < 1) continue;
            IMyInventory inv = c.GetInventory(0);
            if (inv == null || !inv.CanItemsBeAdded((MyFixedPoint)1, it.Type)) continue;
            double amt = ItemAmountIn(inv, it);
            if (pass == 0 && amt > 0.0) continue;
            if (amt < bestAmt) { bestAmt = amt; best = i; }
        }
        if (best >= 0 && srcAmt > bestAmt * 1.25 + 1.0) return best;
    }
    return -1;
}

int RedundantPriority(MyInventoryItem it)
{
    string type = Safe(it.Type.TypeId.ToString());
    string sub = Safe(it.Type.SubtypeId.ToString());
    if (type.IndexOf("AmmoMagazine", StringComparison.OrdinalIgnoreCase) >= 0) return 5;
    if (type.IndexOf("Component", StringComparison.OrdinalIgnoreCase) >= 0) return 4;
    if (type.IndexOf("Ingot", StringComparison.OrdinalIgnoreCase) >= 0) return 3;
    if (sub.Equals("Ice", StringComparison.OrdinalIgnoreCase) || sub.Equals("Uranium", StringComparison.OrdinalIgnoreCase)) return 3;
    if (type.IndexOf("PhysicalGunObject", StringComparison.OrdinalIgnoreCase) >= 0) return 3;
    if (type.IndexOf("Ore", StringComparison.OrdinalIgnoreCase) >= 0) return sub.Equals("Stone", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
    return 1;
}

bool IsDiscreteItem(MyInventoryItem it)
{
    string t = Safe(it.Type.TypeId.ToString());
    return t.IndexOf("Component", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("AmmoMagazine", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("PhysicalGunObject", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("ContainerObject", StringComparison.OrdinalIgnoreCase) >= 0;
}

bool TryMoveMisplacedGroupedItem(IMyCargoContainer c)
{
    int srcIndex = ManagedCargoIndex(c);
    if (srcIndex < 0) return false;
    IMyInventory src = c.GetInventory(0);
    if (src == null) return false;
    itemScratch.Clear();
    src.GetItems(itemScratch);
    for (int pass = 0; pass < 2; pass++)
    {
        for (int i = 0; i < itemScratch.Count; i++)
        {
            MyInventoryItem it = itemScratch[i];
            string group = EffectiveGroup(ItemGroup(it));
            bool correct = CargoHasAnyGroup(c, group);
            int dstIndex = -1;
            string action = "";
            if (pass == 0)
            {
                if (correct) continue;
                dstIndex = ChooseGroupedCargoExcept(it, it.Amount, group, srcIndex);
                action = group;
            }
            else
            {
                if (!correct) continue;
                dstIndex = ChooseConsolidationDest(it, group, srcIndex, src);
                action = "JOIN " + group;
            }
            if (dstIndex < 0) continue;
            IMyInventory dst = managedCargo[dstIndex].GetInventory(0);
            if (dst == null) continue;
            bool ok = src.TransferItemTo(dst, it, (MyFixedPoint?)null);
            if (ok)
            {
                groupCleanMoves++;
                lastGroupClean = action + " " + ItemShort(it) + " " + ShortName(c) + " -> " + ShortName(managedCargo[dstIndex]);
                return true;
            }
            groupCleanState = "TRANSFER_FAIL";
            lastGroupClean = action + " " + ItemShort(it) + " transfer rejected";
            return false;
        }
    }
    return false;
}

int ManagedCargoIndex(IMyCargoContainer c)
{
    for (int i = 0; i < managedCargo.Count; i++) if (managedCargo[i] == c) return i;
    return -1;
}

string EffectiveGroup(string group)
{
    if ((group == "AMMO" || group == "ARMORY") && !HasGroupPool(group)) return "REST";
    return group;
}

bool HasGroupPool(string group)
{
    for (int i = 0; i < managedCargo.Count; i++) if (CargoHasAnyGroup(managedCargo[i], group)) return true;
    return false;
}

bool CargoHasAnyGroup(IMyCargoContainer c, string group)
{
    return c != null && (HasTag(c.CustomName, GroupTag(group)) || HasTag(c.CustomName, AutoGroupTag(group)));
}

string AutoGroupTag(string group)
{
    return "[" + group + "*]";
}

int ChooseGroupedCargoExcept(MyInventoryItem it, MyFixedPoint amount, string group, int excludeIndex)
{
    return ChooseGroupedCargoCore(it, amount, group, excludeIndex, true);
}

int ChooseGroupedCargoCore(MyInventoryItem it, MyFixedPoint amount, string group, int excludeIndex, bool useEffective)
{
    string g = useEffective ? EffectiveGroup(group) : group;
    bool manual = HasGroupTag(g);
    int best = -1;
    double bestAmt = -1.0;
    for (int pass = 0; pass < 2; pass++)
    {
        double bestFree = -1.0;
        for (int i = 0; i < managedCargo.Count; i++)
        {
            if (i == excludeIndex) continue;
            IMyCargoContainer c = managedCargo[i];
            if (c == null || c.InventoryCount < 1) continue;
            if (manual) { if (!CargoHasGroup(c, g)) continue; }
            else if (!CargoHasAnyGroup(c, g)) continue;
            IMyInventory inv = c.GetInventory(0);
            if (inv == null || !inv.CanItemsBeAdded(amount, it.Type)) continue;
            double same = ItemAmountIn(inv, it);
            if (pass == 0)
            {
                if (same <= 0.0) continue;
                if (same > bestAmt) { bestAmt = same; best = i; }
            }
            else
            {
                double free = ((double)inv.MaxVolume - (double)inv.CurrentVolume) * 1000.0;
                if (free > bestFree) { bestFree = free; best = i; }
            }
        }
        if (best >= 0) return best;
    }
    return -1;
}

int ChooseConsolidationDest(MyInventoryItem it, string group, int srcIndex, IMyInventory src)
{
    double srcAmt = ItemAmountIn(src, it);
    int best = -1;
    double bestAmt = srcAmt;
    string g = EffectiveGroup(group);
    bool manual = HasGroupTag(g);
    for (int i = 0; i < managedCargo.Count; i++)
    {
        if (i == srcIndex) continue;
        IMyCargoContainer c = managedCargo[i];
        if (c == null || c.InventoryCount < 1) continue;
        if (manual) { if (!CargoHasGroup(c, g)) continue; }
        else if (!CargoHasAnyGroup(c, g)) continue;
        IMyInventory inv = c.GetInventory(0);
        if (inv == null || !inv.CanItemsBeAdded(it.Amount, it.Type)) continue;
        double amt = ItemAmountIn(inv, it);
        if (amt > bestAmt) { bestAmt = amt; best = i; }
    }
    return best;
}

double ItemAmountIn(IMyInventory inv, MyInventoryItem sample)
{
    if (inv == null) return 0.0;
    itemScratch2.Clear();
    inv.GetItems(itemScratch2);
    double total = 0.0;
    for (int i = 0; i < itemScratch2.Count; i++)
        if (SameItem(itemScratch2[i], sample)) total += (double)itemScratch2[i].Amount;
    return total;
}

bool SameItem(MyInventoryItem a, MyInventoryItem b)
{
    return a.Type.TypeId.Equals(b.Type.TypeId) && a.Type.SubtypeId.Equals(b.Type.SubtypeId);
}

void ApplyOptimizedRefineryFeed()
{
    optRefState = optimizedWorkersAllowed ? "ON" : "OFF";
    if (scanning) return;
    if (!optimizedWorkersAllowed) { lastOptRefinery = "SKIP: NOT OPTIMIZED"; return; }
    if (managedCargo.Count < 1) { optRefState = "FAULT_NO_MANAGED_CARGO"; lastOptRefinery = "NO MANAGED CARGO"; return; }
    if (refineries.Count < 1) { optRefState = "NO_REFINERIES"; lastOptRefinery = "NO REFINERIES"; return; }
    if (++optRefTick < OPT_REF_FEED_EVERY_TICKS) return;
    optRefTick = 0;
    if (SoftBudgetHit()) { optRefState = "YIELD"; lastOptRefinery = "YIELD OPTREF"; return; }
    if (TryOptimizedRefineryFeedOnce()) optRefState = "MOVED";
    else if (lastOptRefinery == "NONE") lastOptRefinery = "SKIP: NO ORE NEED";
}

bool TryOptimizedRefineryFeedOnce()
{
    lastOptRefinery = "NONE";
    int n = refineries.Count;
    if (optRefCursor < 0 || optRefCursor >= n) optRefCursor = 0;
    bool yieldBank = HasYieldBank();
    bool highDemand = HasHighValueDemand();
    for (int step = 0; step < n; step++)
    {
        if (SoftBudgetHit()) { optRefState = "YIELD"; lastOptRefinery = "YIELD OPTREF"; return false; }
        int idx = (optRefCursor + step) % n;
        IMyRefinery r = refineries[idx];
        if (r == null || r.InventoryCount < 1) continue;
        IMyInventory rin = r.GetInventory(0);
        if (rin == null) continue;
        bool isYield = IsYieldRefinery(r);
        string curOre;
        double curQty;
        ReadFirstRefineryOre(rin, out curOre, out curQty);
        if (curOre.Length > 0 && !OreAllowedForRefinery(curOre, isYield, yieldBank, highDemand))
        {
            if (ReturnFirstRefineryOreToCargo(rin, curOre))
            {
                optRefCursor = (idx + 1) % n;
                optRefState = "CLASSCLEAN";
                lastOptRefinery = "R" + (idx + 1).ToString() + " return " + curOre;
                return true;
            }
            return false;
        }
        if (curOre.Length > 0)
        {
            bool curNeeded = OreNeedScore(curOre) > 0.0;
            bool betterNeeded = HasOtherNeededOreForRefinery(isYield, yieldBank, highDemand, curOre);
            if (curNeeded || !betterNeeded)
            {
                if (curQty < REF_TOPOFF_QTY)
                {
                    double topMoved;
                    if (MoveOreFromCargoToRefinery(curOre, rin, out topMoved))
                    {
                        optRefMoves++;
                        optRefCursor = (idx + 1) % n;
                        lastOptRefinery = "R" + (idx + 1).ToString() + " topoff " + curOre + " +" + FormatCompact(topMoved);
                        return true;
                    }
                }
                lastOptRefinery = "R" + (idx + 1).ToString() + " keep " + curOre;
                continue;
            }
            if (ReturnFirstRefineryOreToCargo(rin, curOre))
            {
                optRefCursor = (idx + 1) % n;
                optRefState = "RELEASE";
                lastOptRefinery = "R" + (idx + 1).ToString() + " release " + curOre;
                return true;
            }
            return false;
        }
        string wanted;
        double wantedScore;
        if (!FindBestOreForRefinery(isYield, yieldBank, highDemand, out wanted, out wantedScore)) continue;
        double moved;
        if (MoveOreFromCargoToRefinery(wanted, rin, out moved))
        {
            optRefMoves++;
            optRefCursor = (idx + 1) % n;
            lastOptRefinery = "R" + (idx + 1).ToString() + " " + wanted + " +" + FormatCompact(moved);
            return true;
        }
        lastOptRefinery = "FAIL NO " + wanted + " MOVE";
        return false;
    }
    optRefCursor = 0;
    return false;
}

bool HasYieldBank()
{
    for (int i = 0; i < refineries.Count; i++) if (IsYieldRefinery(refineries[i])) return true;
    return false;
}

bool IsYieldRefinery(IMyRefinery r)
{
    if (r == null) return false;
    string info = Safe(r.DetailedInfo);
    double y = ParsePercentAfter(info, "Yield Rate");
    return y >= 175.0;
}

double ParsePercentAfter(string text, string label)
{
    if (text == null) return 0;
    int p = text.IndexOf(label, StringComparison.OrdinalIgnoreCase);
    if (p < 0) return 0;
    p += label.Length;
    while (p < text.Length && !(text[p] >= '0' && text[p] <= '9') && text[p] != '.') p++;
    int s = p;
    while (p < text.Length && ((text[p] >= '0' && text[p] <= '9') || text[p] == '.')) p++;
    double d;
    if (p > s && double.TryParse(text.Substring(s, p - s), out d)) return d;
    return 0;
}

bool HasHighValueDemand()
{
    for (int i = 0; i < HV_ORES.Length; i++)
        if (OreNeedScore(HV_ORES[i]) > 0.0 && CountOreInManagedCargo(HV_ORES[i]) >= 1.0) return true;
    return false;
}

bool FindBestOreForRefinery(bool isYield, bool yieldBank, bool highDemand, out string bestName, out double bestScore)
{
    bestName = "";
    bestScore = 0.0;
    for (int i = 0; i < ORE_NAMES.Length; i++)
    {
        string ore = ORE_NAMES[i];
        if (!OreAllowedForRefinery(ore, isYield, yieldBank, highDemand)) continue;
        double avail = CountOreInManagedCargo(ore);
        if (avail < 1.0) continue;
        double score = AdjustedOreScore(ore, isYield);
        if (score <= 0.0 && isYield && !highDemand && ore == "Magnesium") score = 0.5 / (1 + ActiveOreSlots(ore, isYield));
        if (score <= 0.0 && isYield && !highDemand && ore == "Stone") score = 0.1 / (1 + ActiveOreSlots(ore, isYield));
        if (score > bestScore) { bestScore = score; bestName = ore; }
    }
    return bestName.Length > 0;
}

double AdjustedOreScore(string ore, bool isYield)
{
    double score = OreNeedScore(ore) * OreScoreMult(ore);
    if (score <= 0.0) return 0.0;
    return score / (1 + ActiveOreSlots(ore, isYield));
}

int ActiveOreSlots(string ore, bool isYield)
{
    int count = 0;
    for (int i = 0; i < refineries.Count; i++)
    {
        IMyRefinery r = refineries[i];
        if (r == null || r.InventoryCount < 1) continue;
        if (IsYieldRefinery(r) != isYield) continue;
        string cur;
        double q;
        ReadFirstRefineryOre(r.GetInventory(0), out cur, out q);
        if (cur.Equals(ore, StringComparison.OrdinalIgnoreCase)) count++;
    }
    return count;
}

bool HasOtherNeededOreForRefinery(bool isYield, bool yieldBank, bool highDemand, string currentOre)
{
    bool currentHigh = IsHighValueOre(currentOre);
    for (int i = 0; i < ORE_NAMES.Length - 1; i++)
    {
        string ore = ORE_NAMES[i];
        if (ore.Equals(currentOre, StringComparison.OrdinalIgnoreCase)) continue;
        if (isYield && currentHigh && !IsHighValueOre(ore)) continue;
        if (!OreAllowedForRefinery(ore, isYield, yieldBank, highDemand)) continue;
        if (OreNeedScore(ore) <= 0.0) continue;
        if (CountOreInManagedCargo(ore) < 1.0) continue;
        return true;
    }
    return false;
}

bool OreAllowedForRefinery(string ore, bool isYield, bool yieldBank, bool highDemand)
{
    if (ore == null) return false;
    bool high = IsHighValueOre(ore);
    if (!yieldBank) return true;
    if (isYield)
    {
        if (highDemand) return high;
        return true;
    }
    if (high) return false;
    if (ore.Equals("Magnesium", StringComparison.OrdinalIgnoreCase) && !MagnesiumBelowMin()) return false;
    return true;
}

bool IsHighValueOre(string ore)
{
    for (int i = 0; i < HV_ORES.Length; i++) if (ore.Equals(HV_ORES[i], StringComparison.OrdinalIgnoreCase)) return true;
    return false;
}

bool MagnesiumBelowMin()
{
    return RefinedCurrent("Magnesium") < RefinedMin("Magnesium");
}

double OreNeedScore(string ore)
{
    if (ore.Equals("Stone", StringComparison.OrdinalIgnoreCase)) return 0.0;
    double min = RefinedMin(ore);
    if (min <= 0.0) return 0.0;
    double cur = RefinedCurrent(ore);
    double pressure = (min - cur) / min;
    if (pressure <= 0.0) return 0.0;
    if (cur <= 0.0) pressure *= 2.0;
    return pressure;
}

double OreScoreMult(string ore)
{
    if (ore == "Iron") return 0.8;
    if (ore == "Silicon") return 1.0;
    if (ore == "Nickel") return 1.2;
    if (ore == "Cobalt") return 2.4;
    if (ore == "Magnesium") return 3.0;
    if (ore == "Silver") return 2.8;
    if (ore == "Gold") return 3.5;
    if (ore == "Platinum") return 4.0;
    if (ore == "Uranium") return 4.0;
    if (ore == "Stone") return 0.25;
    return 1.0;
}

double RefinedCurrent(string ore)
{
    if (ore.Equals("Uranium", StringComparison.OrdinalIgnoreCase)) return GetPb1Double("Current.SUPPLY.URANIUM", 0.0);
    return GetPb1Double("Current.REFINED." + ore.ToUpperInvariant(), 0.0);
}

double RefinedMin(string ore)
{
    if (ore.Equals("Uranium", StringComparison.OrdinalIgnoreCase)) return GetPb1Double("Target.SUPPLY.URANIUM.Min", 0.0);
    return GetPb1Double("Target.REFINED." + ore.ToUpperInvariant() + ".Min", 0.0);
}

double GetPb1Double(string key, double fallback)
{
    double v;
    if (pb1Nums.TryGetValue(key, out v)) return v;
    return fallback;
}

void ReadFirstRefineryOre(IMyInventory inv, out string ore, out double qty)
{
    ore = "";
    qty = 0.0;
    if (inv == null) return;
    itemScratch.Clear();
    inv.GetItems(itemScratch);
    for (int j = 0; j < itemScratch.Count; j++)
    {
        MyInventoryItem it = itemScratch[j];
        string type = Safe(it.Type.TypeId.ToString());
        if (type.IndexOf("Ore", StringComparison.OrdinalIgnoreCase) < 0) continue;
        ore = OreName(it);
        qty = (double)it.Amount;
        return;
    }
}

bool ReturnFirstRefineryOreToCargo(IMyInventory rin, string expectedOre)
{
    itemScratch.Clear();
    rin.GetItems(itemScratch);
    for (int i = 0; i < itemScratch.Count; i++)
    {
        MyInventoryItem it = itemScratch[i];
        string type = Safe(it.Type.TypeId.ToString());
        if (type.IndexOf("Ore", StringComparison.OrdinalIgnoreCase) < 0) continue;
        if (!OreName(it).Equals(expectedOre, StringComparison.OrdinalIgnoreCase)) continue;
        int cargoIndex = ChooseCargoForItem(it, it.Amount);
        if (cargoIndex < 0) { lastOptRefinery = "FAIL NO CARGO OLD"; return false; }
        IMyInventory dst = managedCargo[cargoIndex].GetInventory(0);
        optRefAttempts++;
        bool ok = rin.TransferItemTo(dst, it, (MyFixedPoint?)null);
        if (!ok) lastOptRefinery = "FAIL RETURN " + expectedOre;
        return ok;
    }
    return true;
}

bool MoveOreFromCargoToRefinery(string wanted, IMyInventory rin, out double moved)
{
    moved = 0.0;
    for (int lane = 0; lane < managedCargo.Count; lane++)
    {
        if (SoftBudgetHit()) { lastOptRefinery = "YIELD CARGO"; return false; }
        IMyCargoContainer c = managedCargo[lane];
        if (c == null || c.InventoryCount < 1) continue;
        IMyInventory src = c.GetInventory(0);
        if (src == null) continue;
        itemScratch.Clear();
        src.GetItems(itemScratch);
        for (int i = 0; i < itemScratch.Count; i++)
        {
            MyInventoryItem it = itemScratch[i];
            string type = Safe(it.Type.TypeId.ToString());
            if (type.IndexOf("Ore", StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (!OreName(it).Equals(wanted, StringComparison.OrdinalIgnoreCase)) continue;
            double q = Math.Floor((double)it.Amount);
            if (q < 1.0) continue;
            optRefAttempts++;
            return TryTransferMax(src, rin, it, q, out moved);
        }
    }
    return false;
}

bool TryTransferMax(IMyInventory src, IMyInventory dst, MyInventoryItem it, double maxQty, out double moved)
{
    moved = 0.0;
    if (maxQty <= 0.0001) return false;
    MyFixedPoint exact = (MyFixedPoint)maxQty;
    if (dst.CanItemsBeAdded(exact, it.Type))
    {
        bool okExact = src.TransferItemTo(dst, it, exact);
        if (okExact) moved = maxQty;
        return okExact;
    }
    int hi = (int)Math.Floor(maxQty);
    if (hi < 1) return false;
    int lo = 1;
    int best = 0;
    while (lo <= hi)
    {
        int mid = (lo + hi) / 2;
        MyFixedPoint amt = (MyFixedPoint)mid;
        if (dst.CanItemsBeAdded(amt, it.Type)) { best = mid; lo = mid + 1; }
        else hi = mid - 1;
    }
    if (best < 1) return false;
    MyFixedPoint amount = (MyFixedPoint)best;
    bool ok = src.TransferItemTo(dst, it, amount);
    if (ok) moved = best;
    return ok;
}

double CountOreInManagedCargo(string ore)
{
    double total = 0.0;
    for (int i = 0; i < managedCargo.Count; i++)
    {
        if (SoftBudgetHit()) return total;
        IMyCargoContainer c = managedCargo[i];
        if (c == null || c.InventoryCount < 1) continue;
        IMyInventory inv = c.GetInventory(0);
        if (inv == null) continue;
        itemScratch.Clear();
        inv.GetItems(itemScratch);
        for (int j = 0; j < itemScratch.Count; j++)
        {
            MyInventoryItem it = itemScratch[j];
            string type = Safe(it.Type.TypeId.ToString());
            if (type.IndexOf("Ore", StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (OreName(it).Equals(ore, StringComparison.OrdinalIgnoreCase)) total += Math.Floor((double)it.Amount);
        }
    }
    return total;
}

string OreName(MyInventoryItem it)
{
    string s = Safe(it.Type.SubtypeId.ToString());
    if (s.EndsWith("Ore", StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - 3);
    return s;
}

void ApplyIngotClearing()
{
    ingotClearState = ingotClearingMode > 0 ? "ON" : "OFF";
    if (scanning) return;
    if (ingotClearingMode <= 0) { lastIngotClear = "OFF"; return; }
    if (managedCargo.Count < 1) { ingotClearState = "FAULT_NO_MANAGED_CARGO"; lastIngotClear = "NO MANAGED CARGO"; return; }
    int moved = ExecuteRefineryOutputClearPasses(REF_OUTPUT_CLEAR_PASSES);
    if (moved > 1) lastIngotClear = "REF OUTPUT CLEAR x" + moved.ToString();
}

int ExecuteRefineryOutputClearPasses(int passes)
{
    int moved = 0;
    lastIngotClear = "NONE";
    ingotClearState = "IDLE";
    for (int i = 0; i < passes; i++)
    {
        if (SoftBudgetHit()) { ingotClearState = "YIELD"; lastIngotClear = "YIELD REF"; break; }
        if (!TryClearRefineryOutputsMb1Style()) break;
        moved++;
    }
    if (moved > 0) ingotClearState = "MOVED";
    else if (lastIngotClear == "NONE") lastIngotClear = "SKIP: NO OUTPUTS";
    return moved;
}

bool TryClearRefineryOutputsMb1Style()
{
    int n = refineries.Count;
    if (n < 1) { ingotClearState = "NO_REFINERIES"; lastIngotClear = "NO REFINERIES"; return false; }
    if (outputRefCursor < 0 || outputRefCursor >= n) outputRefCursor = 0;
    for (int step = 0; step < n; step++)
    {
        if (SoftBudgetHit()) { ingotClearState = "YIELD"; lastIngotClear = "YIELD REF"; return false; }
        int i = (outputRefCursor + step) % n;
        IMyTerminalBlock b = refineries[i] as IMyTerminalBlock;
        if (TryClearOutputFromBlockMb1Style(b, "REF"))
        {
            outputRefCursor = (i + 1) % n;
            return true;
        }
    }
    outputRefCursor = 0;
    return false;
}

bool TryClearOutputFromBlockMb1Style(IMyTerminalBlock b, string kind)
{
    if (b == null || b.InventoryCount < 2) return false;
    IMyInventory src = b.GetInventory(1);
    if (src == null) return false;
    itemScratch.Clear();
    src.GetItems(itemScratch);
    for (int i = 0; i < itemScratch.Count; i++)
    {
        MyInventoryItem it = itemScratch[i];
        if (!IsRefineryOutputItem(it)) continue;
        double qty = (double)it.Amount;
        if (qty <= 0.0) continue;
        int cargoIndex = ChooseCargoForItem(it, it.Amount);
        if (cargoIndex < 0)
        {
            ingotClearState = "FAULT_NO_SPACE";
            lastIngotClear = kind + " " + ItemShort(it) + " -> NO SPACE " + FormatCompact(qty);
            return false;
        }
        IMyInventory dst = managedCargo[cargoIndex].GetInventory(0);
        lastIngotClear = kind + " " + ItemShort(it) + " -> " + ShortName(managedCargo[cargoIndex]) + " " + FormatCompact(qty);
        bool ok = src.TransferItemTo(dst, it, (MyFixedPoint?)null);
        ingotClearState = ok ? "MOVED" : "TRANSFER_FAIL";
        if (ok) ingotClearMoves++;
        else lastIngotClear = kind + " " + ItemShort(it) + " transfer rejected";
        return ok;
    }
    return false;
}

bool SoftBudgetHit()
{
    return Runtime.CurrentInstructionCount >= SOFT_INSTR_LIMIT;
}

bool IsRefineryOutputItem(MyInventoryItem it)
{
    string type = it.Type.TypeId.ToString();
    string sub = it.Type.SubtypeId.ToString();
    if (type.IndexOf("Ingot", StringComparison.OrdinalIgnoreCase) >= 0) return true;
    if (sub.Equals("Stone", StringComparison.OrdinalIgnoreCase)) return true;
    if (sub.Equals("Gravel", StringComparison.OrdinalIgnoreCase)) return true;
    return false;
}

int ChooseCargoForItem(MyInventoryItem it, MyFixedPoint amount)
{
    if (distributionMode > 0)
    {
        string group = ItemGroup(it);
        int g = ChooseGroupedCargo(it, amount, group, false);
        if (g >= 0) return g;
        AddOverflow(group);
        g = ChooseGroupedCargo(it, amount, "REST", true);
        if (g >= 0) return g;
        AddOverflow("EMERG");
    }
    return ChooseManagedCargoMb1Style(it, amount);
}

string ItemGroup(MyInventoryItem it)
{
    string type = Safe(it.Type.TypeId.ToString());
    string sub = Safe(it.Type.SubtypeId.ToString());
    if (sub.Equals("Ice", StringComparison.OrdinalIgnoreCase)) return "ICE";
    if (type.IndexOf("Ore", StringComparison.OrdinalIgnoreCase) >= 0 || sub.Equals("Stone", StringComparison.OrdinalIgnoreCase)) return "ORE";
    if (type.IndexOf("Ingot", StringComparison.OrdinalIgnoreCase) >= 0) return "INGOT";
    if (type.IndexOf("AmmoMagazine", StringComparison.OrdinalIgnoreCase) >= 0) return "AMMO";
    if (type.IndexOf("PhysicalGunObject", StringComparison.OrdinalIgnoreCase) >= 0) return "ARMORY";
    if (type.IndexOf("Component", StringComparison.OrdinalIgnoreCase) >= 0) return componentStorageMode > 0 ? "COMP" : "REST";
    return "REST";
}

int ChooseGroupedCargo(MyInventoryItem it, MyFixedPoint amount, string group, bool fallback)
{
    return ChooseGroupedCargoCore(it, amount, fallback ? "REST" : group, -1, true);
}

bool HasGroupTag(string group)
{
    string tag = GroupTag(group);
    if (tag.Length < 1) return false;
    for (int i = 0; i < managedCargo.Count; i++)
        if (HasTag(managedCargo[i].CustomName, tag)) return true;
    return false;
}

bool CargoHasGroup(IMyCargoContainer c, string group)
{
    return c != null && HasTag(c.CustomName, GroupTag(group));
}

string GroupTag(string group)
{
    if (group == "ICE") return TAG_ICE;
    if (group == "ORE") return TAG_ORE;
    if (group == "INGOT") return TAG_INGOT;
    if (group == "COMP") return TAG_COMP;
    if (group == "AMMO") return TAG_AMMO;
    if (group == "ARMORY") return TAG_ARMORY;
    return TAG_REST;
}



int ChooseManagedCargoMb1Style(MyInventoryItem it, MyFixedPoint amount)
{
    int best = -1;
    double bestFree = -1.0;
    for (int i = 0; i < managedCargo.Count; i++)
    {
        IMyCargoContainer c = managedCargo[i];
        if (c == null || c.InventoryCount < 1) continue;
        IMyInventory inv = c.GetInventory(0);
        if (inv == null) continue;
        if (!inv.CanItemsBeAdded(amount, it.Type)) continue;
        double free = ((double)inv.MaxVolume - (double)inv.CurrentVolume) * 1000.0;
        if (free > bestFree) { bestFree = free; best = i; }
    }
    return best;
}

void PruneManagedMineableSweepBlocks()
{
    for (int i = mineableSweepBlocks.Count - 1; i >= 0; i--)
        if (IsManagedCargoBlock(mineableSweepBlocks[i])) mineableSweepBlocks.RemoveAt(i);
}

bool IsMineableSweepCandidate(IMyTerminalBlock b)
{
    if (b == null) return false;
    if (!BlockHasInstallTag(b)) return false;
    if (b.InventoryCount < 1) return false;
    if (IsManagedCargoBlock(b)) return false;
    if (b is IMyRefinery) return false;
    if (b is IMyAssembler) return false;
    if (b is IMyGasGenerator) return false;
    if (b is IMyGasTank) return false;
    if (b is IMyProgrammableBlock) return false;
    if (b is IMyTextPanel) return false;
    if (b is IMyShipController) return false;
    return true;
}

bool IsManagedCargoBlock(IMyTerminalBlock b)
{
    if (b == null) return false;
    for (int i = 0; i < managedCargo.Count; i++)
        if (managedCargo[i] == b) return true;
    return false;
}

void SelectManagedCargo()
{
    managedCargo.Clear();
    managedCargoMode = "NONE";
    managedCargoTier = "NONE";
    managedCargoCandidates = 0;

    List<double> tierCap = new List<double>();
    List<double> tierTotal = new List<double>();
    List<int> tierCount = new List<int>();

    for (int i = 0; i < cargos.Count; i++)
    {
        IMyCargoContainer c = cargos[i];
        if (c == null || c.InventoryCount < 1) continue;
        double cap = InventoryCapacityL(c);
        if (cap <= 0.0) continue;
        managedCargoCandidates++;

        int tier = -1;
        for (int t = 0; t < tierCap.Count; t++)
        {
            if (SameCargoTier(tierCap[t], cap)) { tier = t; break; }
        }
        if (tier < 0)
        {
            tierCap.Add(cap);
            tierTotal.Add(cap);
            tierCount.Add(1);
        }
        else
        {
            double n = tierCount[tier] + 1;
            tierCap[tier] = ((tierCap[tier] * tierCount[tier]) + cap) / n;
            tierTotal[tier] += cap;
            tierCount[tier]++;
        }
    }

    int bestAny = BestCargoTier(tierCap, tierTotal, tierCount, false);
    int bestLarge = BestCargoTier(tierCap, tierTotal, tierCount, true);
    int best = bestLarge >= 0 ? bestLarge : bestAny;
    if (best < 0) return;

    double targetCap = tierCap[best];
    for (int i = 0; i < cargos.Count; i++)
    {
        IMyCargoContainer c = cargos[i];
        if (c == null || c.InventoryCount < 1) continue;
        double cap = InventoryCapacityL(c);
        if (cap > 0.0 && SameCargoTier(targetCap, cap)) managedCargo.Add(c);
    }

    managedCargoMode = bestLarge >= 0 ? "TIER" : "TIER_FALLBACK";
    managedCargoTier = managedCargo.Count.ToString() + "x" + FormatCompact(targetCap) + "L";
}

int BestCargoTier(List<double> tierCap, List<double> tierTotal, List<int> tierCount, bool requireFloor)
{
    int best = -1;
    for (int i = 0; i < tierCap.Count; i++)
    {
        if (requireFloor && tierCap[i] < MANAGED_CARGO_MIN_L) continue;
        if (best < 0 || tierTotal[i] > tierTotal[best] ||
            (Math.Abs(tierTotal[i] - tierTotal[best]) < 0.1 && tierCount[i] > tierCount[best]) ||
            (Math.Abs(tierTotal[i] - tierTotal[best]) < 0.1 && tierCount[i] == tierCount[best] && tierCap[i] > tierCap[best]))
            best = i;
    }
    return best;
}

bool SameCargoTier(double a, double b)
{
    if (a <= 0.0 || b <= 0.0) return false;
    double m = Math.Max(a, b);
    return Math.Abs(a - b) <= m * 0.02;
}

void ApplyAutoGroupLabels()
{
    autoGroupLabels = 0;
    if (distributionMode > 0) BuildAutoGroupPlan();
    else groupPlanText = "REDUNDANT";
    for (int i = 0; i < managedCargo.Count; i++)
    {
        IMyCargoContainer c = managedCargo[i];
        if (c == null) continue;
        string oldName = c.CustomName;
        string clean = StripAutoGroupTags(oldName);
        bool manual = HasAnyManualGroupTag(clean);
        string add = (distributionMode > 0 && !manual) ? AutoTagsForCargoIndex(i) : "";
        string planned = (distributionMode > 0 && !manual && autoGroupPlan != null && i >= 0 && i < autoGroupPlan.Length) ? autoGroupPlan[i] : "";
        if (manual) { if (groupLedger.Remove(c.EntityId)) groupLedgerDirty = true; }
        else if (distributionMode > 0 && planned.Length > 0)
        {
            string oldLedger;
            if (!groupLedger.TryGetValue(c.EntityId, out oldLedger) || oldLedger != planned) { groupLedger[c.EntityId] = planned; groupLedgerDirty = true; }
        }
        string nn = CleanSpaces(clean + (add.Length > 0 ? " " + add : ""));
        if (nn != oldName)
        {
            c.CustomName = nn;
            autoGroupLabels++;
        }
    }
    DecayOverflow();
    groupPlanDirty = false;
}

void MaybeApplyGroupPlan()
{
    if (++groupPlanTick < 120 && !groupPlanDirty) return;
    if (groupPlanTick < 12 && groupPlanDirty) return;
    groupPlanTick = 0;
    if (groupPlanDirty) ApplyAutoGroupLabels();
}

void BuildAutoGroupPlan()
{
    ReadGroupLedger();
    int n = managedCargo.Count;
    if (autoGroupPlan == null || autoGroupPlan.Length != n) autoGroupPlan = new string[n];
    for (int i = 0; i < n; i++) autoGroupPlan[i] = "";
    autoGroupRestored = 0;
    if (n < 1) { groupPlanText = "NONE"; groupForceReplan = false; return; }

    double ice = 0, ore = 0, ing = 0, rest = 0, comp = 0;
    for (int i = 0; i < n; i++)
    {
        IMyCargoContainer c = managedCargo[i];
        if (c == null || c.InventoryCount < 1) continue;
        IMyInventory inv = c.GetInventory(0);
        if (inv == null) continue;
        itemScratch.Clear();
        inv.GetItems(itemScratch);
        for (int k = 0; k < itemScratch.Count; k++)
        {
            MyInventoryItem it = itemScratch[k];
            double q = (double)it.Amount;
            string g = ItemGroup(it);
            if (g == "ICE") ice += q;
            else if (g == "ORE") ore += q;
            else if (g == "INGOT") ing += q;
            else if (g == "COMP") comp += q;
            else rest += q;
        }
    }

    double sIce = 1.2 + gasGens.Count * 0.08 + Math.Min(3.0, Math.Max(0.0, GetPb1Double("Target.SUPPLY.ICE.Max", 0) / 1000000.0)) + Math.Min(3.0, ice / 1000000.0) + overflowIce * 0.5;
    double sOre = 1.3 + refineries.Count * 0.12 + Math.Min(4.0, ore / 1000000.0) + overflowOre * 0.5;
    double sIng = 1.8 + refineries.Count * 0.05 + assemblers.Count * 0.04 + Math.Min(4.0, ing / 1000000.0) + overflowIngot * 0.5;
    double sRest = 1.0 + Math.Min(4.0, rest / 500000.0) + overflowRest * 0.5 + overflowEmerg * 0.8;
    double sComp = componentStorageMode > 0 ? 1.0 + Math.Min(4.0, comp / 250000.0) + overflowComp * 0.5 : 0.0;

    int mIce = CountManualGroup("ICE"), mOre = CountManualGroup("ORE"), mIng = CountManualGroup("INGOT"), mRest = CountManualGroup("REST"), mComp = CountManualGroup("COMP");
    int cIce = mIce, cOre = mOre, cIng = mIng, cRest = mRest, cComp = mComp;

    for (int i = 0; i < n; i++)
    {
        IMyCargoContainer c = managedCargo[i];
        if (c == null) continue;
        string clean = StripAutoGroupTags(c.CustomName);
        if (HasAnyManualGroupTag(clean)) { if (groupLedger.Remove(c.EntityId)) groupLedgerDirty = true; continue; }
        string ag = ExistingAutoGroup(c.CustomName);
        bool restored = false;
        if (ag.Length < 1 || !IsAllowedAutoGroup(ag))
        {
            if (groupLedger.TryGetValue(c.EntityId, out ag) && IsAllowedAutoGroup(ag)) restored = true;
            else ag = "";
        }
        if (ag.Length > 0)
        {
            autoGroupPlan[i] = ag;
            if (restored) autoGroupRestored++;
            if (ag == "ICE") cIce++; else if (ag == "ORE") cOre++; else if (ag == "INGOT") cIng++; else if (ag == "COMP") cComp++; else cRest++;
        }
    }

    if (groupForceReplan)
    {
        int autoSlots = AutoCargoCount();
        int dIce = mIce, dOre = mOre, dIng = mIng, dRest = mRest, dComp = mComp;
        BuildDesiredGroupCounts(autoSlots, sIce, sOre, sIng, sRest, sComp, ref dIce, ref dOre, ref dIng, ref dRest, ref dComp);
        AdjustAutoPlanToDesired(dIce, dOre, dIng, dRest, dComp, ref cIce, ref cOre, ref cIng, ref cRest, ref cComp);
    }

    int[] idx = AutoCargoIndexesStable();
    string[] core = componentStorageMode > 0 ? new string[] { "ICE", "ORE", "INGOT", "COMP", "REST" } : new string[] { "ICE", "ORE", "INGOT", "REST" };
    for (int p = 0; p < idx.Length; p++)
    {
        int ci = idx[p];
        if (ci < 0 || ci >= n) continue;
        if (autoGroupPlan[ci].Length > 0) continue;
        IMyCargoContainer c = managedCargo[ci];
        if (c == null) continue;
        string clean = StripAutoGroupTags(c.CustomName);
        if (HasAnyManualGroupTag(clean)) continue;
        string g = "";
        for (int pass = 0; pass < 2 && g.Length < 1; pass++)
        {
            double best = -1;
            for (int k = 0; k < core.Length; k++)
            {
                string cg = core[k];
                int cur = GroupCount(cg, cIce, cOre, cIng, cRest, cComp);
                if (pass == 0 && cur > 0) continue;
                double sc = GroupScore(cg, sIce, sOre, sIng, sRest, sComp) / (1.0 + cur);
                if (sc > best) { best = sc; g = cg; }
            }
        }
        if (g.Length < 1) g = "REST";
        autoGroupPlan[ci] = g;
        if (g == "ICE") cIce++; else if (g == "ORE") cOre++; else if (g == "INGOT") cIng++; else if (g == "COMP") cComp++; else cRest++;
    }
    groupPlanText = (groupForceReplan ? "REGROUP " : "STICKY ") + "ICE:" + cIce + " ORE:" + cOre + " INGOT:" + cIng + (componentStorageMode > 0 ? " COMP:" + cComp : "") + " REST:" + cRest;
    groupForceReplan = false;
}


int AutoCargoCount()
{
    int n = 0;
    for (int i = 0; i < managedCargo.Count; i++)
    {
        IMyCargoContainer c = managedCargo[i];
        if (c != null && !HasAnyManualGroupTag(StripAutoGroupTags(c.CustomName))) n++;
    }
    return n;
}

void BuildDesiredGroupCounts(int slots, double sIce, double sOre, double sIng, double sRest, double sComp, ref int cIce, ref int cOre, ref int cIng, ref int cRest, ref int cComp)
{
    string[] core = componentStorageMode > 0 ? new string[] { "ICE", "ORE", "INGOT", "COMP", "REST" } : new string[] { "ICE", "ORE", "INGOT", "REST" };
    for (int p = 0; p < slots; p++)
    {
        string g = "";
        for (int pass = 0; pass < 2 && g.Length < 1; pass++)
        {
            double best = -1;
            for (int k = 0; k < core.Length; k++)
            {
                string cg = core[k];
                int cur = GroupCount(cg, cIce, cOre, cIng, cRest, cComp);
                if (pass == 0 && cur > 0) continue;
                double sc = GroupScore(cg, sIce, sOre, sIng, sRest, sComp) / (1.0 + cur);
                if (sc > best) { best = sc; g = cg; }
            }
        }
        if (g.Length < 1) g = "REST";
        if (g == "ICE") cIce++; else if (g == "ORE") cOre++; else if (g == "INGOT") cIng++; else if (g == "COMP") cComp++; else cRest++;
    }
}

void AdjustAutoPlanToDesired(int dIce, int dOre, int dIng, int dRest, int dComp, ref int cIce, ref int cOre, ref int cIng, ref int cRest, ref int cComp)
{
    int guard = managedCargo.Count + 2;
    while (guard-- > 0)
    {
        string need = DeficitGroup(dIce, dOre, dIng, dRest, dComp, cIce, cOre, cIng, cRest, cComp);
        if (need.Length < 1) break;
        string donor = SurplusGroup(dIce, dOre, dIng, dRest, dComp, cIce, cOre, cIng, cRest, cComp);
        if (donor.Length < 1) break;
        int ix = FindAutoPlanIndex(donor);
        if (ix < 0) break;
        autoGroupPlan[ix] = need;
        DecGroup(donor, ref cIce, ref cOre, ref cIng, ref cRest, ref cComp);
        IncGroup(need, ref cIce, ref cOre, ref cIng, ref cRest, ref cComp);
    }
}

string DeficitGroup(int dIce, int dOre, int dIng, int dRest, int dComp, int cIce, int cOre, int cIng, int cRest, int cComp)
{
    if (cOre < dOre) return "ORE";
    if (cIng < dIng) return "INGOT";
    if (cIce < dIce) return "ICE";
    if (componentStorageMode > 0 && cComp < dComp) return "COMP";
    if (cRest < dRest) return "REST";
    return "";
}

string SurplusGroup(int dIce, int dOre, int dIng, int dRest, int dComp, int cIce, int cOre, int cIng, int cRest, int cComp)
{
    if (componentStorageMode > 0 && cComp > dComp && cComp > 1) return "COMP";
    if (cIng > dIng && cIng > 1) return "INGOT";
    if (cOre > dOre && cOre > 1) return "ORE";
    if (cIce > dIce && cIce > 1) return "ICE";
    if (cRest > dRest && cRest > 1) return "REST";
    return "";
}

void IncGroup(string g, ref int cIce, ref int cOre, ref int cIng, ref int cRest, ref int cComp)
{
    if (g == "ICE") cIce++; else if (g == "ORE") cOre++; else if (g == "INGOT") cIng++; else if (g == "COMP") cComp++; else cRest++;
}

void DecGroup(string g, ref int cIce, ref int cOre, ref int cIng, ref int cRest, ref int cComp)
{
    if (g == "ICE") cIce--; else if (g == "ORE") cOre--; else if (g == "INGOT") cIng--; else if (g == "COMP") cComp--; else cRest--;
}

int FindAutoPlanIndex(string g)
{
    int[] idx = AutoCargoIndexesStable();
    for (int p = idx.Length - 1; p >= 0; p--)
    {
        int i = idx[p];
        if (i >= 0 && i < autoGroupPlan.Length && autoGroupPlan[i] == g) return i;
    }
    return -1;
}

int[] AutoCargoIndexesStable()
{
    int n = 0;
    for (int i = 0; i < managedCargo.Count; i++) if (!HasAnyManualGroupTag(StripAutoGroupTags(managedCargo[i].CustomName))) n++;
    int[] a = new int[n];
    int p = 0;
    for (int i = 0; i < managedCargo.Count; i++) if (!HasAnyManualGroupTag(StripAutoGroupTags(managedCargo[i].CustomName))) a[p++] = i;
    for (int i = 0; i < n - 1; i++)
    {
        int best = i;
        for (int j = i + 1; j < n; j++)
        {
            if (CargoSortBefore(managedCargo[a[j]], managedCargo[a[best]])) best = j;
        }
        if (best != i) { int t = a[i]; a[i] = a[best]; a[best] = t; }
    }
    return a;
}

bool CargoSortBefore(IMyCargoContainer a, IMyCargoContainer b)
{
    double ca = InventoryCapacityL(a), cb = InventoryCapacityL(b);
    if (Math.Abs(ca - cb) > Math.Max(ca, cb) * 0.02) return ca > cb;
    string na = StripAutoGroupTags(a == null ? "" : a.CustomName);
    string nb = StripAutoGroupTags(b == null ? "" : b.CustomName);
    int c = String.Compare(na, nb, StringComparison.OrdinalIgnoreCase);
    if (c != 0) return c < 0;
    long ea = a == null ? 0 : a.EntityId;
    long eb = b == null ? 0 : b.EntityId;
    return ea < eb;
}

int CountManualGroup(string g)
{
    int c = 0;
    for (int i = 0; i < managedCargo.Count; i++) if (CargoHasGroup(managedCargo[i], g)) c++;
    return c;
}

int GroupCount(string g, int ice, int ore, int ing, int rest, int comp)
{
    if (g == "ICE") return ice;
    if (g == "ORE") return ore;
    if (g == "INGOT") return ing;
    if (g == "COMP") return comp;
    return rest;
}

double GroupScore(string g, double ice, double ore, double ing, double rest, double comp)
{
    if (g == "ICE") return ice;
    if (g == "ORE") return ore;
    if (g == "INGOT") return ing;
    if (g == "COMP") return comp;
    return rest;
}



void AddOverflow(string group)
{
    if (group == "ICE") overflowIce++;
    else if (group == "ORE") overflowOre++;
    else if (group == "INGOT") overflowIngot++;
    else if (group == "COMP") overflowComp++;
    else if (group == "EMERG") overflowEmerg++;
    else overflowRest++;
    groupPlanDirty = true;
}


void DecayOverflow()
{
    if (overflowIce > 0) overflowIce--;
    if (overflowOre > 0) overflowOre--;
    if (overflowIngot > 0) overflowIngot--;
    if (overflowRest > 0) overflowRest--;
    if (overflowComp > 0) overflowComp--;
    if (overflowEmerg > 0) overflowEmerg--;
}

string AutoTagsForCargoIndex(int i)
{
    if (autoGroupPlan == null || i < 0 || i >= autoGroupPlan.Length || autoGroupPlan[i].Length < 1) return "";
    return "[" + autoGroupPlan[i] + "*]";
}

string ExistingAutoGroup(string name)
{
    if (HasTag(name, "[ICE*]")) return "ICE";
    if (HasTag(name, "[ORE*]")) return "ORE";
    if (HasTag(name, "[INGOT*]")) return "INGOT";
    if (HasTag(name, "[COMP*]")) return "COMP";
    if (HasTag(name, "[REST*]")) return "REST";
    if (HasTag(name, "[AMMO*]")) return "AMMO";
    if (HasTag(name, "[ARMORY*]")) return "ARMORY";
    return "";
}

bool IsAllowedAutoGroup(string g)
{
    if (g == "ICE" || g == "ORE" || g == "INGOT" || g == "REST") return true;
    if (g == "COMP") return componentStorageMode > 0;
    return false;
}

bool HasAnyManualGroupTag(string name)
{
    return HasTag(name, TAG_ICE) || HasTag(name, TAG_ORE) || HasTag(name, TAG_INGOT) || HasTag(name, TAG_REST) || HasTag(name, TAG_COMP) || HasTag(name, TAG_AMMO) || HasTag(name, TAG_ARMORY);
}

string StripAutoGroupTags(string name)
{
    string s = Safe(name);
    string[] a = new string[] { "[ICE*]", "[ORE*]", "[INGOT*]", "[REST*]", "[COMP*]", "[AMMO*]", "[ARMORY*]" };
    for (int i = 0; i < a.Length; i++) s = ReplaceCI(s, a[i], "");
    return CleanSpaces(s);
}

string ReplaceCI(string src, string find, string repl)
{
    if (src == null || find == null || find.Length == 0) return src;
    StringBuilder b = new StringBuilder();
    int pos = 0;
    while (true)
    {
        int ix = src.IndexOf(find, pos, StringComparison.OrdinalIgnoreCase);
        if (ix < 0) { b.Append(src.Substring(pos)); break; }
        b.Append(src.Substring(pos, ix - pos));
        b.Append(repl);
        pos = ix + find.Length;
    }
    return b.ToString();
}

string CleanSpaces(string s)
{
    if (s == null) return "";
    StringBuilder b = new StringBuilder();
    bool space = true;
    for (int i = 0; i < s.Length; i++)
    {
        char c = s[i];
        bool sp = c == ' ' || c == '\t';
        if (sp)
        {
            if (!space) b.Append(' ');
            space = true;
        }
        else
        {
            b.Append(c);
            space = false;
        }
    }
    return b.ToString().Trim();
}

double InventoryCapacityL(IMyTerminalBlock b)
{
    if (b == null || b.InventoryCount < 1) return 0.0;
    double liters = 0.0;
    for (int i = 0; i < b.InventoryCount; i++)
    {
        IMyInventory inv = b.GetInventory(i);
        if (inv != null) liters += (double)inv.MaxVolume * 1000.0;
    }
    return liters;
}

string ItemShort(MyInventoryItem it)
{
    string sub = it.Type.SubtypeId.ToString();
    if (sub.Length > 18) sub = sub.Substring(0, 18);
    return sub;
}

string FormatCompact(double v)
{
    if (v >= 1000000.0) return Math.Round(v / 1000000.0, 1).ToString("0.#") + "M";
    if (v >= 1000.0) return Math.Round(v / 1000.0, 1).ToString("0.#") + "k";
    return Math.Round(v).ToString("0");
}

void KV(StringBuilder b, string k, object v)
{
    b.Append(k).Append('=').AppendLine(v == null ? "" : v.ToString());
}


void ReadGroupLedger()
{
    groupLedger.Clear();
    string block = ExtractDataBlock(Me.CustomData, GROUP_LEDGER_BEGIN, GROUP_LEDGER_END);
    if (block.Length == 0) return;
    string[] lines = block.Replace("\r", "").Split('\n');
    for (int i = 0; i < lines.Length; i++)
    {
        string line = lines[i].Trim();
        if (line.Length == 0 || line.StartsWith("#")) continue;
        int eq = line.IndexOf('=');
        if (eq < 1) continue;
        long id;
        if (!long.TryParse(line.Substring(0, eq).Trim(), out id)) continue;
        string g = line.Substring(eq + 1).Trim().ToUpperInvariant();
        if (IsAllowedAutoGroup(g)) groupLedger[id] = g;
    }
}

string BuildGroupLedgerBlock()
{
    StringBuilder b = new StringBuilder();
    b.AppendLine(GROUP_LEDGER_BEGIN);
    for (int i = 0; i < managedCargo.Count; i++)
    {
        IMyCargoContainer c = managedCargo[i];
        if (c == null) continue;
        string g;
        if (groupLedger.TryGetValue(c.EntityId, out g) && IsAllowedAutoGroup(g)) b.AppendLine(c.EntityId.ToString() + "=" + g);
    }
    b.AppendLine(GROUP_LEDGER_END);
    return b.ToString();
}

void WriteStatus()
{
    StringBuilder b = new StringBuilder();
    b.AppendLine(WORKER_STATUS_BEGIN);
    b.AppendLine("WorkerVersion=OB1_PB2_V069_COMPONENT_SWEEP");
    KV(b, "State", GetWorkerState());
    KV(b, "Fault", fault);
    KV(b, "InstrLast", lastInstr.ToString());
    KV(b, "InstrHigh", highInstr.ToString());
    KV(b, "PB1Packet", (pb1PacketSeen ? "OK" : "MISSING"));
    KV(b, "PB1AgeTicks", pb1AgeTicks.ToString());
    KV(b, "RefiningEffective", effectiveRefining);
    KV(b, "DistributionMode", distributionMode > 0 ? "GROUPED" : "REDUNDANT");
    KV(b, "AutoGroupLedger", groupLedger.Count.ToString());
    KV(b, "AutoGroupRestored", autoGroupRestored.ToString());
    KV(b, "GroupRegroupSeq", groupRegroupSeq.ToString());
    KV(b, "GroupRegroupAckSeq", groupRegroupAckSeq.ToString());
    KV(b, "LastGroupRegroup", lastGroupRegroup);
    KV(b, "GroupPlan", groupPlanText);
    KV(b, "GroupCleanState", groupCleanState);
    KV(b, "GroupCleanCfg", GROUP_CLEAN_EVERY_TICKS_DEFAULT.ToString() + "/" + GROUP_CLEAN_CONTAINERS_DEFAULT.ToString() + "/" + GROUP_CLEAN_MOVES_DEFAULT.ToString());
    KV(b, "LastGroupClean", lastGroupClean);
    KV(b, "GeneralSweep", generalSweepState);
    KV(b, "LastGeneralSweep", lastGeneralSweep);
    KV(b, "ManagedCargo", managedCargo.Count.ToString());
    KV(b, "ManagedCargoMode", managedCargoMode);
    KV(b, "ManagedCargoTier", managedCargoTier);
    b.AppendLine(WORKER_STATUS_END);

    StringBuilder d = new StringBuilder();
    d.AppendLine(DOCKED_BEGIN);
    for (int i = 0; i < dockRows.Count; i++) d.AppendLine("DOCK|" + dockRows[i].Key + "|" + dockRows[i].Name + "|" + dockRows[i].Dock + "|" + dockRows[i].State);
    d.AppendLine(DOCKED_END);

    string cd = Me.CustomData;
    cd = RemoveDataBlockFast(cd, OLD_PLAN_BEGIN, OLD_PLAN_END);
    cd = RemoveDataBlockFast(cd, "# OB1_IMS_PB2_TUNING_BEGIN", "# OB1_IMS_PB2_TUNING_END");
    if (groupLedgerDirty) { cd = ReplaceDataBlockFast(cd, GROUP_LEDGER_BEGIN, GROUP_LEDGER_END, BuildGroupLedgerBlock()); groupLedgerDirty = false; }
    cd = ReplaceDataBlockFast(cd, WORKER_STATUS_BEGIN, WORKER_STATUS_END, b.ToString());
    cd = ReplaceDataBlockFast(cd, DOCKED_BEGIN, DOCKED_END, d.ToString());
    Me.CustomData = cd;
}


string GetWorkerState()
{
    if (fault != "NONE") return "FAULT";
    if (scanning) return "SCANNING";
    if (!pb1Found) return "NO_PB1";
    if (!pb1PacketSeen) return "NO_PB1_PACKET";
    if (effectiveRefining == "AUTO_BLOCKED_UNLOAD_RISK") return "AUTO_BLOCKED";
    return "OK";
}

void MaybeEcho()
{
    if (++echoTick < ECHO_PERIOD_TICKS) return;
    echoTick = 0;
    Echo(BuildEcho());
}

string BuildEcho()
{
    StringBuilder b = new StringBuilder();
    b.AppendLine("OB1 IMS PB2 V069");
    b.AppendLine(GetWorkerState() + " Fault " + fault + " PB1 " + (pb1PacketSeen ? "OK" : "MISS") + " age " + pb1AgeTicks.ToString());
    b.AppendLine("Instr " + lastInstr.ToString() + " HW " + highInstr.ToString());
    b.AppendLine("Cargo " + managedCargo.Count.ToString() + " " + managedCargoMode + " " + managedCargoTier + " cand=" + managedCargoCandidates.ToString());
    b.AppendLine("Dist " + (distributionMode > 0 ? "GROUPED" : "REDUNDANT") + " " + groupPlanText);
    b.AppendLine("Ledger " + groupLedger.Count.ToString() + "/" + autoGroupRestored.ToString() + " cmd RESET_AUTO_GROUPS");
    b.AppendLine("Clean " + groupCleanState + " moves=" + groupCleanMoves.ToString() + " cfg=" + GROUP_CLEAN_EVERY_TICKS_DEFAULT.ToString() + "/" + GROUP_CLEAN_CONTAINERS_DEFAULT.ToString() + "/" + GROUP_CLEAN_MOVES_DEFAULT.ToString());
    b.AppendLine("LastGroup " + lastGroupClean);
    b.AppendLine("Sweep " + generalSweepState + " " + lastGeneralSweep);
    b.AppendLine("O " + oreUnloadState + " " + lastOreUnload);
    b.AppendLine("I " + ingotClearState + " " + lastIngotClear);
    b.AppendLine("R " + optRefState + " " + lastOptRefinery);
    b.AppendLine("Ref " + effectiveRefining + " Ice " + IceModeName(iceProcessingMode) + " Gas " + gasState);
    return b.ToString();
}


string IceModeName(int mode)
{
    if (mode <= 0) return "OFF";
    if (mode == 1) return "MIN";
    return "AUTO";
}

string RemoveDataBlockFast(string e, string begin, string end)
{
    if (e == null) return "";
    int a = e.IndexOf(begin, StringComparison.OrdinalIgnoreCase), b = e.IndexOf(end, StringComparison.OrdinalIgnoreCase);
    return (a >= 0 && b > a) ? e.Remove(a, b + end.Length - a).Trim() : e;
}

string ReplaceDataBlockFast(string existing, string begin, string end, string blockWithMarkers)
{
    if (existing == null) existing = "";
    string clean = blockWithMarkers.Trim();
    int a = existing.IndexOf(begin, StringComparison.OrdinalIgnoreCase);
    int b = existing.IndexOf(end, StringComparison.OrdinalIgnoreCase);
    if (a >= 0 && b > a) return existing.Remove(a, b + end.Length - a).Insert(a, clean);
    if (existing.Trim().Length == 0) return clean;
    return existing.TrimEnd() + "\n\n" + clean;
}

string ExtractDataBlock(string text, string begin, string end)
{
    if (text == null) return "";
    int a = text.IndexOf(begin, StringComparison.OrdinalIgnoreCase);
    if (a < 0) return "";
    int b = text.IndexOf(end, a + begin.Length, StringComparison.OrdinalIgnoreCase);
    if (b < 0) return "";
    return text.Substring(a + begin.Length, b - a - begin.Length).Trim();
}

bool HasTag(string text, string tag)
{
    if (text == null || tag == null) return false;
    return text.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0;
}

string Safe(string s)
{
    return s == null ? "" : s;
}

string ShortName(IMyTerminalBlock b)
{
    string n = b == null ? "" : Safe(b.CustomName);
    if (n.Length > 34) return n.Substring(0, 34);
    return n;
}

int ClampInt(int v, int min, int max)
{
    if (v < min) return min;
    if (v > max) return max;
    return v;
}


string ModeName(int mode)
{
    if (mode <= MODE_OFF) return "OFF";
    if (mode == MODE_AUTO) return "AUTO";
    return "OPTIMIZED";
}

