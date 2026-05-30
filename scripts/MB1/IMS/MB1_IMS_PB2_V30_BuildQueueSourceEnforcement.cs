const string INSTALL_TAG = "[MB1]";
const string WORKER_TAG = "[IMSWORKER]";
const string NO_UNLOAD_TAG = "[NO UNLOAD]";
const string PB1_EXPORT_BEGIN = "# IMS_PB1_EXPORT_BEGIN";
const string PB1_EXPORT_END = "# IMS_PB1_EXPORT_END";
const string WORKER_STATUS_BEGIN = "# IMS_WORKER_STATUS_BEGIN";
const string WORKER_STATUS_END = "# IMS_WORKER_STATUS_END";
const string HIWATER_BEGIN = "# IMS_WORKER_HIWATER_BEGIN";
const string HIWATER_END = "# IMS_WORKER_HIWATER_END";
const string WORKER_CONFIG_BEGIN = "# IMS_WORKER_CONFIG_BEGIN";
const string WORKER_CONFIG_END = "# IMS_WORKER_CONFIG_END";
const string BQS_DISABLED_BEGIN = "# IMS_WORKER_BQS_DISABLED_BEGIN";
const string BQS_DISABLED_END = "# IMS_WORKER_BQS_DISABLED_END";
const string BQS_COOP_BEGIN = "# IMS_WORKER_BQS_COOP_GUARD_BEGIN";
const string BQS_COOP_END = "# IMS_WORKER_BQS_COOP_GUARD_END";
const string WSO_MAGCFG_BEGIN = "# WSO_MAGCFG_BEGIN";
const string WSO_MAGCFG_END = "# WSO_MAGCFG_END";
const int WORKER_CADENCE_DEFAULT=1;
const int WORKER_CADENCE_MIN = 1;
const int WORKER_CADENCE_MAX = 60;
const int WORKER_CADENCE_STEP = 1;
const int SOFT_INSTR_LIMIT = 32000;
const int PB1_STALE_TICKS = 18;
const int AUTO_OUTPUT_CLEAR_PASSES = 1;
const int REF_OUTPUT_CLEAR_PASSES = 4;
const int AUTO_DISTRIBUTE_PASSES = 1;
const int AUTO_QUEUE_PASSES = 1;
const int AUTO_ORE_PASSES = 1;
const int AUTO_UNLOAD_BULK_PASSES = 4;
const int AUTO_UNLOAD_SOURCE_SCAN = 16;
const int AUTO_UNLOAD_SOURCE_SCAN_FAST = 20;
const int AUTO_UNLOAD_FAST_PASSES = 3;
const double MANAGED_CARGO_MIN_L = 300000.0;
int PB_INSTRUCTION_LIMIT = 50000;
List<IMyTerminalBlock> allBlocks = new List<IMyTerminalBlock>();
List<IMyCargoContainer> cargos = new List<IMyCargoContainer>();
List<IMyCargoContainer> laneCargos = new List<IMyCargoContainer>();
List<IMyAssembler> assemblers = new List<IMyAssembler>();
List<IMyAssembler> survivalLike = new List<IMyAssembler>();
List<IMyAssembler> assemblerQueueTargets = new List<IMyAssembler>();
List<IMyAssembler> assemblerCoopHelpers = new List<IMyAssembler>();
List<IMyAssembler> basicFallbackTargets = new List<IMyAssembler>();
List<IMyAssembler> coopRecoveryHelpers = new List<IMyAssembler>();
List<long> bqsDisabled = new List<long>();
List<long> bqsCoopGuard = new List<long>();
int coopBlockIdx = 0;
double ledgerPendingCache = -1;
int ledgerPendingCacheTick = -999;
int localOutputAsmCursor = 0;
int localOutputRefCursor = 0;
int orePriorityRefineryCursor = 0;
int magReqCursor = 0;
const int COOP_PROBE_BUDGET = 6;
const int COOP_RECOVER_BUDGET = 4;
List<IMyRefinery> refineries = new List<IMyRefinery>();
List<IMyGasGenerator> gasGens = new List<IMyGasGenerator>();
const int ICE_MODE_OFF = 0, ICE_MODE_MIN = 1, ICE_MODE_AUTO = 2;
const double GAS_MIN_HYSTERESIS = 3.0;
bool gasMinDemand = false;
string lastGasResult = "NONE";
List<IMyTextSurface> workerSurfaces = new List<IMyTextSurface>();
Dictionary<string, string> pb1 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
Dictionary<string, SpreadItem> spread = new Dictionary<string, SpreadItem>(StringComparer.OrdinalIgnoreCase);
List<SpreadItem> spreadList = new List<SpreadItem>();
List<MyInventoryItem> itemScratch = new List<MyInventoryItem>();
List<MyProductionItem> queueScratch = new List<MyProductionItem>();
List<IMyShipConnector> unloadConnectors = new List<IMyShipConnector>();
List<long> unloadAllowedGridIds = new List<long>();
List<IMyTerminalBlock> unloadSources = new List<IMyTerminalBlock>();
List<IMyProgrammableBlock> pbScratch = new List<IMyProgrammableBlock>();
List<MagRequest> magRequests = new List<MagRequest>();
List<IMyCargoContainer> magBoxes = new List<IMyCargoContainer>();
int tick;
int workerCadenceTicks = WORKER_CADENCE_DEFAULT;
int workPhase;
int packetAgeTicks;
int lastSeq = -1;
int seq = 0;
int statusSeq = 0;
int perfSeq = 0;
int perfRunningPhase = -1;
string perfRunningLabel = "INIT";
string perfLastComplete = "NONE";
int perfLastInstr = 0;
string perfMaxLabel = "NONE";
int perfMaxInstr = 0;
int hiInstr = 0;
string hiLabel = "NONE";
int hiSeq = 0;
int hiPhase = -1;
int hiTick = 0;
bool pb1Found;
bool bqsLoaded = false;
bool bqsCoopLoaded = false;
string bqsClusterTag = "AUTO";
string bqsClusterSrc = "MB1";
string bqsLocalState = "ACTIVE";
string bqsEnforce = "NONE";
string lastCommand = "INIT";
string fault = "NONE";
int distUneven;
double distWorstScore;
string distWorstLabel = "NONE";
string lastMoveResult = "NONE";
string[] recentMovePairs = new string[8];
int recentMoveIndex = 0;
int lastBatchMoves = 0;
string lastOutputClear = "NONE";
string lastOutputResult = "NONE";
bool autoClearing = false;
double selectedCargoCapL = 0;
string queuePlanText = "NONE";
string ammoPlanText = "AMMO ?";
string lastQueueResult = "NONE";
string ledgerLabel = "";
string ledgerToken = "";
string ledgerBaseKey = "";
double ledgerRequested = 0;
double ledgerStartStock = 0;
double ledgerStartQueue = 0;
double ledgerStartTrusted = 0;
string lastCoopRecover = "NONE";
int autoQueueHoldSeq = -1;
int queueTargetCursor = 0;
int queueCategoryCursor = 0;
string asmPolicyText = "ASM POLICY NONE";
string queueAddTrace = "";
double lastLedgerPending = -1;
int ledgerStale = 0;
int ledgerSeenSeq = -1;
bool ledgerFault = false;
string staleBaseKey = "";
string staleLabel = "";
string orePlanText = "NONE";
string oreStatusText = "NONE";
string lastOreApply = "NONE";
string lastUnloadResult = "NONE";
string lastMagResult = "NONE";
bool autoUnloading = false;
int unloadScanIndex = 0;
int unloadConnSig = -1;
bool unloadCacheReady = false;
int unloadFastPasses = 0;
public Program()
{
ReadWorkerConfig();
ReadHighWater();
Runtime.UpdateFrequency = UpdateFrequency.Update10;
Discover();
ReadPb1Export();
EnforceBuildQueueSource();
AnalyzeDistribution();
AnalyzeQueuePlan();
AnalyzeOrePriority();
UpdateFault();
WriteStatus();
DrawWorkerScreen();
Echo(BuildEchoStatus());
}
public void Save()
{
WriteStatus();
}
public void Main(string argument, UpdateType updateSource)
{
tick++;
if (argument != null && argument.Trim().Length > 0)
{
HandleCommand(argument.Trim());
return;
}
if (tick % workerCadenceTicks == 0)
{
RunWorkerPhase();
}
Echo(BuildEchoStatus());
}
void RunWorkerPhase()
{
string phaseName = PhaseName(workPhase);
PerfBegin(phaseName); MarkInstr(phaseName + ":BEGIN");
if (workPhase == 0 || packetAgeTicks > PB1_STALE_TICKS) { ReadPb1Export(); MarkInstr(phaseName + ":PB1"); }
EnforceBuildQueueSource(); MarkInstr(phaseName + ":BQS");
UpdateFault(); MarkInstr(phaseName + ":FAULT");
if (pb1Found) { ExecuteGasGeneratorControl(); MarkInstr(phaseName + ":GAS"); }
if (fault == "NONE")
{
if (workPhase == 0) { if(IsBuildQueueOffline()){ lastCoopRecover="SKIP BQS OFFLINE"; } else ExecuteCoopRecoverSlice(); MarkInstr(phaseName + ":COOP"); }
else if (workPhase == 1) { ExecuteAutoOutputClear(); MarkInstr(phaseName + ":LOCALOUT"); }
else if (workPhase == 2) { ExecuteAutoOreUnload(); MarkInstr(phaseName + ":UNLOAD"); }
else if (workPhase == 3) { AnalyzeDistribution(); MarkInstr(phaseName + ":ANALYZE"); }
else if (workPhase == 4) { ExecuteAutoDistribution(); MarkInstr(phaseName + ":MOVE"); }
else if (workPhase == 5) { if(IsBuildQueueOffline()){ queuePlanText="BQS OFFLINE"; } else AnalyzeQueuePlan(); MarkInstr(phaseName + ":PLAN"); }
else if (workPhase == 6) { if(IsBuildQueueOffline()){ lastQueueResult="SKIP BQS OFFLINE"; } else ExecuteAutoQueue(); MarkInstr(phaseName + ":QUEUE"); }
else if (workPhase == 7) { if(IsBuildQueueOffline()){ lastMagResult="SKIP BQS OFFLINE"; } else ExecuteMagazineStocking(); MarkInstr(phaseName + ":MAG"); }
else if (workPhase == 8) { AnalyzeOrePriority(); MarkInstr(phaseName + ":OREPLAN"); }
else if (workPhase == 9) { ExecuteAutoOrePriority(); MarkInstr(phaseName + ":OREMOVE"); }
else if (workPhase == 10) { WriteStatus(); MarkInstr(phaseName + ":WRITESTATUS"); }
else { DrawWorkerScreen(); MarkInstr(phaseName + ":SCREEN"); }
}
else if (workPhase == 10 || workPhase == 11)
{
WriteStatus(); MarkInstr(phaseName + ":WRITESTATUS");
DrawWorkerScreen(); MarkInstr(phaseName + ":SCREEN");
}
PerfEnd(phaseName);
workPhase++;
if (workPhase > 11) workPhase = 0;
}
string PhaseName(int p)
{
if (p == 0) return "P0_COOP_RECOVER";
if (p == 1) return "P1_LOCAL_OUTPUT";
if (p == 2) return "P2_ORE_UNLOAD";
if (p == 3) return "P3_DIST_ANALYZE";
if (p == 4) return "P4_DIST_MOVE";
if (p == 5) return "P5_QUEUE_PLAN";
if (p == 6) return "P6_QUEUE_ADD";
if (p == 7) return "P7_MAG_STOCK";
if (p == 8) return "P8_ORE_PLAN";
if (p == 9) return "P9_ORE_APPLY";
if (p == 10) return "P10_STATUS_WRITE";
return "P11_SCREEN";
}
bool SoftBudgetHit()
{
return Runtime.CurrentInstructionCount >= SOFT_INSTR_LIMIT;
}
void InvalidateLedgerPendingCache()
{
ledgerPendingCacheTick = -999;
ledgerPendingCache = -1;
}
void PerfBegin(string label)
{
perfSeq++;
perfRunningPhase = workPhase;
perfRunningLabel = label;
}
void PerfEnd(string label)
{
int instr = Runtime.CurrentInstructionCount;
perfLastComplete = label;
perfLastInstr = instr;
if (instr > perfMaxInstr)
{
perfMaxInstr = instr;
perfMaxLabel = label;
}
}
void WritePerfTrace()
{
WriteHighWater();
}
void MarkInstr(string label)
{
int i = Runtime.CurrentInstructionCount;
if (i <= hiInstr) return;
hiInstr = i; hiLabel = label; hiSeq = perfSeq; hiPhase = workPhase; hiTick = tick;
WriteHighWater();
}
void ReadHighWater()
{
string block = ExtractDataBlock(Me.CustomData, HIWATER_BEGIN, HIWATER_END);
if (block.Length == 0) return;
string[] lines = block.Split('\n');
for (int i = 0; i < lines.Length; i++)
{
string line = lines[i].Trim(); int eq = line.IndexOf('='); if (eq <= 0) continue;
string k = line.Substring(0, eq).Trim(); string v = line.Substring(eq + 1).Trim(); int n;
if (k == "HiInstr" && int.TryParse(v, out n)) hiInstr = n;
else if (k == "HiSeq" && int.TryParse(v, out n)) hiSeq = n;
else if (k == "HiPhase" && int.TryParse(v, out n)) hiPhase = n;
else if (k == "HiTick" && int.TryParse(v, out n)) hiTick = n;
else if (k == "HiLabel") hiLabel = v;
}
}
void WriteHighWater()
{
StringBuilder sb = new StringBuilder();
sb.AppendLine(HIWATER_BEGIN);
sb.AppendLine("WorkerVersion=PB2_V30_BUILD_QUEUE_SOURCE_ENFORCEMENT");
sb.AppendLine("HiInstr=" + hiInstr.ToString());
sb.AppendLine("HiPct=" + InstrPct(hiInstr));
sb.AppendLine("HiLabel=" + hiLabel);
sb.AppendLine("HiPhase=" + hiPhase.ToString());
sb.AppendLine("HiSeq=" + hiSeq.ToString());
sb.AppendLine("HiTick=" + hiTick.ToString());
sb.AppendLine("LastComplete=" + perfLastComplete);
sb.AppendLine("Running=" + perfRunningLabel);
sb.AppendLine("Cadence=" + workerCadenceTicks.ToString());
sb.AppendLine("Queue=" + lastQueueResult);
sb.AppendLine("Ammo=" + ammoPlanText);
sb.AppendLine("Mag=" + lastMagResult);
sb.AppendLine("Dist=" + lastMoveResult);
sb.AppendLine("Unload=" + lastUnloadResult);
sb.AppendLine("Output=" + lastOutputResult);
sb.AppendLine("Ledger=" + (ledgerRequested > 0 ? Shorten(ledgerLabel, 12) + " req=" + FormatQty(ledgerRequested, true) : (ledgerFault ? "STALE " + Shorten(staleLabel, 12) : "NONE")));
sb.AppendLine("Note=FastCustomDataNoP5Replan");
sb.AppendLine(HIWATER_END);
Me.CustomData = ReplaceDataBlockFast(Me.CustomData, HIWATER_BEGIN, HIWATER_END, sb.ToString());
}
int ClampInt(int v, int min, int max)
{
if (v < min) return min;
if (v > max) return max;
return v;
}
void ReadWorkerConfig()
{
string block = ExtractDataBlock(Me.CustomData, WORKER_CONFIG_BEGIN, WORKER_CONFIG_END);
if (block.Length == 0) return;
string[] lines = block.Split('\n');
for (int i = 0; i < lines.Length; i++)
{
string line = lines[i].Trim();
if (line.Length == 0 || line[0] == '#') continue;
int eq = line.IndexOf('=');
if (eq <= 0) continue;
string key = line.Substring(0, eq).Trim();
string val = line.Substring(eq + 1).Trim();
int n;
if (key.Equals("WorkerCadence", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out n))
workerCadenceTicks = ClampInt(n, WORKER_CADENCE_MIN, WORKER_CADENCE_MAX);
}
}
void SaveWorkerConfig()
{
StringBuilder sb = new StringBuilder();
sb.AppendLine(WORKER_CONFIG_BEGIN);
sb.AppendLine("WorkerCadence=" + workerCadenceTicks.ToString());
sb.AppendLine(WORKER_CONFIG_END);
Me.CustomData = ReplaceDataBlockFast(Me.CustomData, WORKER_CONFIG_BEGIN, WORKER_CONFIG_END, sb.ToString());
}
void AdjustWorkerCadence(int dir)
{
workerCadenceTicks += dir * WORKER_CADENCE_STEP;
workerCadenceTicks = ClampInt(workerCadenceTicks, WORKER_CADENCE_MIN, WORKER_CADENCE_MAX);
tick = 0;
SaveWorkerConfig();
lastCommand = "WORKER CADENCE " + workerCadenceTicks.ToString();
}
string InstrPct(int instr)
{
if (PB_INSTRUCTION_LIMIT <= 0) return "0%";
return ((double)instr * 100.0 / PB_INSTRUCTION_LIMIT).ToString("0.0") + "%";
}
void HandleCommand(string arg)
{
string a = arg.ToUpperInvariant();
lastCommand = a;
if (a == "TRACE" || a == "PERF") { WritePerfTrace(); WriteHighWater(); Echo(BuildEchoStatus()); return; }
if (a == "TRACE RESET" || a == "PERF RESET") { hiInstr = 0; hiLabel = "RESET"; hiSeq = perfSeq; hiPhase = workPhase; hiTick = tick; WriteHighWater(); Echo(BuildEchoStatus()); return; }
if (a == "CLEAN DATA" || a == "CLEAN CUSTOM DATA") { Me.CustomData = CompactCustomData(Me.CustomData); Echo(BuildEchoStatus()); return; }
if (a == "WORKER FASTER" || a == "CADENCE FASTER" || a == "PHASE FASTER")
{
AdjustWorkerCadence(-1);
WriteStatus(); DrawWorkerScreen(); Echo(BuildEchoStatus()); return;
}
if (a == "WORKER SLOWER" || a == "CADENCE SLOWER" || a == "PHASE SLOWER")
{
AdjustWorkerCadence(1);
WriteStatus(); DrawWorkerScreen(); Echo(BuildEchoStatus()); return;
}
if (a == "WORKER DEFAULT" || a == "CADENCE DEFAULT" || a == "PHASE DEFAULT")
{
workerCadenceTicks = WORKER_CADENCE_DEFAULT;
tick = 0; SaveWorkerConfig();
WriteStatus(); DrawWorkerScreen(); Echo(BuildEchoStatus()); return;
}
if (a == "CLEAR STALE" || a == "QUEUE CLEAR STALE")
{
ledgerFault = false; staleBaseKey = ""; staleLabel = "";
lastQueueResult = "STALE CLEARED"; lastCoopRecover = "STALE CLEARED";
autoQueueHoldSeq = seq;
ReadPb1Export(); AnalyzeDistribution(); AnalyzeQueuePlan(); AnalyzeOrePriority(); UpdateFault();
WriteStatus(); DrawWorkerScreen(); Echo(BuildEchoStatus()); return;
}
if (a == "RESCAN" || a == "DISCOVER") { Discover(); unloadCacheReady = false; }
ReadPb1Export();
EnforceBuildQueueSource();
AnalyzeDistribution();
AnalyzeQueuePlan();
AnalyzeOrePriority();
UpdateFault();
if (a == "ORE APPLY ONCE" || a == "ORE APPLY")
{
ExecuteOreApplyOnce();
AnalyzeOrePriority();
AnalyzeDistribution();
AnalyzeQueuePlan();
UpdateFault();
WriteStatus();
DrawWorkerScreen();
Echo(BuildEchoStatus());
return;
}
if (a == "SETTINGS APPLY" || a == "APPLY SETTINGS")
{
ExecuteSettingsApply();
AnalyzeQueuePlan();
AnalyzeOrePriority();
UpdateFault();
WriteStatus();
DrawWorkerScreen();
Echo(BuildEchoStatus());
return;
}
if (a == "QUEUE ONCE" || a == "QUEUE")
{
ExecuteQueueOnce();
AnalyzeQueuePlan();
AnalyzeOrePriority();
UpdateFault();
WriteStatus();
DrawWorkerScreen();
Echo(BuildEchoStatus());
return;
}
if (a == "QUEUE AMMO ONCE" || a == "QUEUE AMMO")
{
ExecuteAmmoQueueOnce();
AnalyzeQueuePlan();
AnalyzeOrePriority();
UpdateFault();
WriteStatus();
DrawWorkerScreen();
Echo(BuildEchoStatus());
return;
}
WriteStatus();
DrawWorkerScreen();
Echo(BuildEchoStatus());
}
void Discover()
{
allBlocks.Clear();
cargos.Clear();
laneCargos.Clear();
assemblers.Clear();
survivalLike.Clear();
refineries.Clear();
gasGens.Clear();
workerSurfaces.Clear();
assemblerQueueTargets.Clear();
assemblerCoopHelpers.Clear();
basicFallbackTargets.Clear();
coopRecoveryHelpers.Clear();
GridTerminalSystem.GetBlocks(allBlocks);
for (int i = 0; i < allBlocks.Count; i++)
{
IMyTerminalBlock b = allBlocks[i];
if (b == null || !HasTag(b, INSTALL_TAG)) continue;
IMyCargoContainer c = b as IMyCargoContainer;
if (c != null) cargos.Add(c);
IMyAssembler a = b as IMyAssembler;
if (a != null)
{
if (IsSurvivalOrBasic(a)) survivalLike.Add(a);
else assemblers.Add(a);
}
IMyRefinery r = b as IMyRefinery;
if (r != null) refineries.Add(r);
IMyGasGenerator g = b as IMyGasGenerator;
if (g != null) gasGens.Add(g);
if (HasTag(b, WORKER_TAG) && !(b is IMyProgrammableBlock)) AddSurfacesFromBlock(b);
}
SelectCargoLanes();
DiscoverCoopRecoveryHelpers();
ClassifyAssemblerPolicy();
}
void DiscoverCoopRecoveryHelpers()
{
coopRecoveryHelpers.Clear();
for (int i = 0; i < allBlocks.Count; i++)
{
IMyAssembler a = allBlocks[i] as IMyAssembler;
if (a == null || HasTag(a, INSTALL_TAG)) continue;
if (!a.IsFunctional) continue;
IMyFunctionalBlock fb = a as IMyFunctionalBlock;
if (fb != null && !fb.Enabled) continue;
if (!a.CooperativeMode) continue;
if (IsLinkedToCargoLane(a)) coopRecoveryHelpers.Add(a);
}
}
void ClassifyAssemblerPolicy()
{
assemblerQueueTargets.Clear();
assemblerCoopHelpers.Clear();
basicFallbackTargets.Clear();
int skipDead = 0, fixedConv = 0, fixedMode = 0, skipOff = 0;
for (int i = 0; i < assemblers.Count; i++)
{
IMyAssembler a = assemblers[i];
if (a == null) continue;
if (!a.IsFunctional) { skipDead++; continue; }
IMyFunctionalBlock fb = a as IMyFunctionalBlock;
if (fb != null && !fb.Enabled) { skipOff++; continue; }
if (!a.UseConveyorSystem)
{
a.UseConveyorSystem = true;
fixedConv++;
}
if (a.CooperativeMode)
{
assemblerCoopHelpers.Add(a);
}
else
{
try
{
if (a.Mode != MyAssemblerMode.Assembly)
{
a.Mode = MyAssemblerMode.Assembly;
fixedMode++;
}
}
catch { }
assemblerQueueTargets.Add(a);
}
}
for (int i = 0; i < survivalLike.Count; i++)
{
IMyAssembler a = survivalLike[i];
if (a == null || !a.IsFunctional) continue;
IMyFunctionalBlock fb = a as IMyFunctionalBlock;
if (fb != null && !fb.Enabled) continue;
if (!a.UseConveyorSystem)
{
a.UseConveyorSystem = true;
fixedConv++;
}
if (IsLinkedToCargoLane(a)) basicFallbackTargets.Add(a);
}
asmPolicyText = "ASM POLICY q=" + assemblerQueueTargets.Count + " coop=" + assemblerCoopHelpers.Count + " basic=" + basicFallbackTargets.Count + " skipOff=" + skipOff + " dead=" + skipDead + " fixConv=" + fixedConv + " fixMode=" + fixedMode;
}
bool IsLinkedToCargoLane(IMyTerminalBlock b)
{
if (b == null || b.InventoryCount <= 0) return false;
for (int bi = 0; bi < b.InventoryCount; bi++)
{
IMyInventory src = b.GetInventory(bi);
if (src == null) continue;
for (int li = 0; li < laneCargos.Count; li++)
{
if (laneCargos[li] == null || laneCargos[li].InventoryCount <= 0) continue;
IMyInventory dst = laneCargos[li].GetInventory(0);
if (dst != null && src.IsConnectedTo(dst)) return true;
}
}
return false;
}
void SelectCargoLanes()
{
laneCargos.Clear();
selectedCargoCapL = 0;
if (cargos.Count == 0) return;
for (int i = 0; i < cargos.Count; i++)
{
IMyCargoContainer c = cargos[i];
if (c == null || c.InventoryCount <= 0) continue;
double cap = InventoryCapacityL(c);
if (cap >= MANAGED_CARGO_MIN_L)
{
laneCargos.Add(c);
selectedCargoCapL += cap;
}
}
}
int FindCargoGroup(List<double> caps, double cap)
{
for (int i = 0; i < caps.Count; i++) if (SameCargoTier(cap, caps[i])) return i;
return -1;
}
bool SameCargoTier(double a, double b)
{
double m = Math.Max(Math.Abs(a), Math.Abs(b));
double tol = Math.Max(1.0, m * 0.02);
return Math.Abs(a - b) <= tol;
}
string CargoGroupText()
{
if (laneCargos.Count == 0) return "NONE";
return laneCargos.Count.ToString() + "x LARGE+ " + FormatQty(selectedCargoCapL) + "L";
}
void UpdateFault()
{
fault = "NONE";
if (!pb1Found) fault = "PB1 EXPORT MISSING";
else if (packetAgeTicks > PB1_STALE_TICKS) fault = "PB1 EXPORT STALE";
else if (laneCargos.Count < 1) fault = "NO CARGO GROUP";
}
bool HasTag(IMyTerminalBlock b, string tag)
{
return b.CustomName != null && b.CustomName.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0;
}
double InventoryCapacityL(IMyTerminalBlock b)
{
if (b == null || b.InventoryCount <= 0) return 0;
double liters = 0;
for (int i = 0; i < b.InventoryCount; i++)
{
IMyInventory inv = b.GetInventory(i);
if (inv != null) liters += (double)inv.MaxVolume * 1000.0;
}
return liters;
}
bool IsSurvivalOrBasic(IMyAssembler a)
{
string n = Safe(a.CustomName).ToUpperInvariant();
string sub = Safe(a.BlockDefinition.SubtypeName).ToUpperInvariant();
string type = Safe(a.BlockDefinition.TypeIdString).ToUpperInvariant();
if (n.IndexOf("SURVIVAL") >= 0 || sub.IndexOf("SURVIVAL") >= 0 || type.IndexOf("SURVIVAL") >= 0) return true;
if (n.IndexOf("BASIC") >= 0 || sub.IndexOf("BASIC") >= 0) return true;
return false;
}
void AddSurfacesFromBlock(IMyTerminalBlock b)
{
IMyTextPanel p = b as IMyTextPanel;
if (p != null)
{
PrepareSurface(p);
workerSurfaces.Add(p);
return;
}
IMyTextSurfaceProvider sp = b as IMyTextSurfaceProvider;
if (sp == null) return;
for (int i = 0; i < sp.SurfaceCount; i++)
{
IMyTextSurface s = sp.GetSurface(i);
if (s == null) continue;
PrepareSurface(s);
workerSurfaces.Add(s);
}
}
void PrepareSurface(IMyTextSurface s)
{
s.ContentType = ContentType.TEXT_AND_IMAGE;
s.Font = "Monospace";
s.FontSize = 0.78f;
s.Alignment = TextAlignment.LEFT;
s.TextPadding = 3f;
}
void ReadPb1Export()
{
pb1.Clear();
string block = ExtractDataBlock(Me.CustomData, PB1_EXPORT_BEGIN, PB1_EXPORT_END);
pb1Found = block.Length > 0;
if (!pb1Found)
{
packetAgeTicks++;
return;
}
string[] lines = block.Split('\n');
for (int i = 0; i < lines.Length; i++)
{
string line = lines[i].Trim();
if (line.Length == 0 || line[0] == '#') continue;
int eq = line.IndexOf('=');
if (eq <= 0) continue;
string k = line.Substring(0, eq).Trim();
string v = line.Substring(eq + 1).Trim();
if (pb1.ContainsKey(k)) pb1[k] = v;
else pb1.Add(k, v);
}
seq = GetInt("Seq", seq);
if (seq != lastSeq)
{
lastSeq = seq;
packetAgeTicks = 0;
}
else packetAgeTicks++;
bqsClusterTag = GetStr("BuildQueue.ClusterTag", GetStr("BuildQueue.SourceTag", "AUTO"));
bqsClusterSrc = GetStr("BuildQueue.ClusterSource", "MB1");
UpdateBuildQueueState();
}
string ExtractDataBlock(string text, string begin, string end)
{
if (text == null) return "";
int start = text.IndexOf(begin, StringComparison.OrdinalIgnoreCase);
if (start < 0) return "";
start += begin.Length;
int stop = text.IndexOf(end, start, StringComparison.OrdinalIgnoreCase);
if (stop < 0) return "";
return text.Substring(start, stop - start).Trim();
}

void UpdateBuildQueueState()
{
string t = SafeStr(bqsClusterTag).ToUpperInvariant();
if (t.Length == 0 || t == "AUTO" || t == "MB1" || t == "LOCAL") bqsLocalState = "ACTIVE";
else bqsLocalState = "OFFLINE";
}
bool IsBuildQueueOffline()
{
return bqsLocalState == "OFFLINE";
}
void LoadBqsDisabled()
{
if (bqsLoaded) return; bqsLoaded = true; bqsDisabled.Clear();
string block = ExtractDataBlock(Me.CustomData, BQS_DISABLED_BEGIN, BQS_DISABLED_END);
if (block.Length == 0) return;
string[] lines = block.Split('\n');
for (int i = 0; i < lines.Length; i++)
{
long id; string l = lines[i].Trim();
if (long.TryParse(l, out id) && !HasBqsId(id)) bqsDisabled.Add(id);
}
}
bool HasBqsId(long id)
{
for (int i = 0; i < bqsDisabled.Count; i++) if (bqsDisabled[i] == id) return true;
return false;
}
void SaveBqsDisabled()
{
StringBuilder sb = new StringBuilder();
sb.AppendLine(BQS_DISABLED_BEGIN);
for (int i = 0; i < bqsDisabled.Count; i++) sb.AppendLine(bqsDisabled[i].ToString());
sb.AppendLine(BQS_DISABLED_END);
Me.CustomData = ReplaceDataBlockFast(Me.CustomData, BQS_DISABLED_BEGIN, BQS_DISABLED_END, sb.ToString());
}

void LoadBqsCoopGuard()
{
if (bqsCoopLoaded) return; bqsCoopLoaded = true; bqsCoopGuard.Clear();
string block = ExtractDataBlock(Me.CustomData, BQS_COOP_BEGIN, BQS_COOP_END);
if (block.Length == 0) return;
string[] lines = block.Split('\n');
for (int i = 0; i < lines.Length; i++)
{
long id; string l = lines[i].Trim();
if (long.TryParse(l, out id) && !HasBqsCoopId(id)) bqsCoopGuard.Add(id);
}
}
bool HasBqsCoopId(long id)
{
for (int i = 0; i < bqsCoopGuard.Count; i++) if (bqsCoopGuard[i] == id) return true;
return false;
}
void SaveBqsCoopGuard()
{
StringBuilder sb = new StringBuilder();
sb.AppendLine(BQS_COOP_BEGIN);
for (int i = 0; i < bqsCoopGuard.Count; i++) sb.AppendLine(bqsCoopGuard[i].ToString());
sb.AppendLine(BQS_COOP_END);
Me.CustomData = ReplaceDataBlockFast(Me.CustomData, BQS_COOP_BEGIN, BQS_COOP_END, sb.ToString());
}
IMyAssembler FindAnyBqsAssembler(long id)
{
for (int i = 0; i < allBlocks.Count; i++)
{
IMyAssembler a = allBlocks[i] as IMyAssembler;
if (a != null && a.EntityId == id) return a;
}
return null;
}
bool BqsProtected()
{
string t = SafeStr(bqsClusterTag).ToUpperInvariant();
return t.Length > 0 && t != "AUTO";
}
bool IsSelectedSourceAsm(IMyAssembler a)
{
string t = SafeStr(bqsClusterTag).ToUpperInvariant();
if (t.Length == 0 || t == "AUTO") return false;
return HasTag(a, "[" + t + "]");
}
int ApplyCoopGuard()
{
LoadBqsCoopGuard(); int changed = 0;
for (int i = 0; i < allBlocks.Count; i++)
{
IMyAssembler a = allBlocks[i] as IMyAssembler;
if (a == null) continue;
if (HasTag(a, INSTALL_TAG) || IsSelectedSourceAsm(a)) continue;
if (a.CooperativeMode)
{
if (!HasBqsCoopId(a.EntityId)) bqsCoopGuard.Add(a.EntityId);
try { a.CooperativeMode = false; changed++; } catch { }
}
}
if (changed > 0) SaveBqsCoopGuard();
return changed;
}
int RestoreCoopGuard()
{
LoadBqsCoopGuard(); int restored = 0;
for (int i = bqsCoopGuard.Count - 1; i >= 0; i--)
{
IMyAssembler a = FindAnyBqsAssembler(bqsCoopGuard[i]);
if (a == null) continue;
try { if (!a.CooperativeMode) { a.CooperativeMode = true; restored++; } bqsCoopGuard.RemoveAt(i); } catch { }
}
SaveBqsCoopGuard();
return restored;
}
IMyAssembler FindBqsAssembler(long id)
{
for (int i = 0; i < assemblers.Count; i++) if (assemblers[i] != null && assemblers[i].EntityId == id) return assemblers[i];
for (int i = 0; i < survivalLike.Count; i++) if (survivalLike[i] != null && survivalLike[i].EntityId == id) return survivalLike[i];
return null;
}
void EnforceBuildQueueSource()
{
LoadBqsDisabled(); LoadBqsCoopGuard(); int changed = 0, restored = 0, coopChanged = 0, coopRestored = 0;
if (BqsProtected()) coopChanged = ApplyCoopGuard(); else coopRestored = RestoreCoopGuard();
if (IsBuildQueueOffline())
{
for (int i = 0; i < assemblers.Count; i++)
{
IMyFunctionalBlock fb = assemblers[i] as IMyFunctionalBlock;
if (fb != null && fb.Enabled)
{
if (!HasBqsId(assemblers[i].EntityId)) bqsDisabled.Add(assemblers[i].EntityId);
try { fb.Enabled = false; changed++; } catch { }
}
}
for (int i = 0; i < survivalLike.Count; i++)
{
IMyFunctionalBlock fb = survivalLike[i] as IMyFunctionalBlock;
if (fb != null && fb.Enabled)
{
if (!HasBqsId(survivalLike[i].EntityId)) bqsDisabled.Add(survivalLike[i].EntityId);
try { fb.Enabled = false; changed++; } catch { }
}
}
if (changed > 0) SaveBqsDisabled();
bqsEnforce = "OFFLINE disabled " + changed.ToString() + " held " + bqsDisabled.Count.ToString() + " coopOff " + coopChanged.ToString();
return;
}
if (bqsDisabled.Count > 0)
{
for (int i = bqsDisabled.Count - 1; i >= 0; i--)
{
IMyAssembler a = FindBqsAssembler(bqsDisabled[i]);
IMyFunctionalBlock fb = a as IMyFunctionalBlock;
if (fb != null)
{
try { if (!fb.Enabled) { fb.Enabled = true; restored++; } } catch { }
}
bqsDisabled.RemoveAt(i);
}
SaveBqsDisabled();
}
bqsEnforce = "ACTIVE restored " + restored.ToString() + " coopOff " + coopChanged.ToString() + " coopRest " + coopRestored.ToString() + " coopHeld " + bqsCoopGuard.Count.ToString();
}
string SafeStr(string s)
{
return s == null ? "" : s;
}

void WriteStatus()
{
statusSeq++;
StringBuilder sb = new StringBuilder();
sb.AppendLine(WORKER_STATUS_BEGIN);
sb.AppendLine("WorkerVersion=PB2_V30_BUILD_QUEUE_SOURCE_ENFORCEMENT");
sb.AppendLine("StatusSeq=" + statusSeq.ToString());
sb.AppendLine("State=" + GetWorkerState());
sb.AppendLine("Mode=DIAG");
sb.AppendLine("Fault=" + fault);
sb.AppendLine("Last=" + lastCommand);
sb.AppendLine("BuildQueueCluster=" + bqsClusterTag);
sb.AppendLine("BuildQueueLocalState=" + bqsLocalState);
sb.AppendLine("BuildQueueEnforcement=" + bqsEnforce);
sb.AppendLine("CoopGuardHeld=" + bqsCoopGuard.Count.ToString());
sb.AppendLine(WORKER_STATUS_END);
Me.CustomData = ReplaceDataBlockFast(Me.CustomData, WORKER_STATUS_BEGIN, WORKER_STATUS_END, sb.ToString());
}
string ReplaceDataBlockFast(string existing, string begin, string end, string block)
{
if (existing == null || existing.Length == 0) return block.Trim();
int a = existing.IndexOf(begin, StringComparison.OrdinalIgnoreCase);
int b = existing.IndexOf(end, StringComparison.OrdinalIgnoreCase);
string clean = block.Trim();
if (a >= 0 && b > a) return existing.Remove(a, b + end.Length - a).Insert(a, clean);
return existing.TrimEnd() + "\n" + clean;
}
string ReplaceDataBlock(string existing, string begin, string end, string block)
{
if (existing == null) existing = "";
existing = CompactCustomData(RemoveDataBlock(existing, begin, end));
if (existing.Length == 0) return block.Trim();
return existing + "\n" + block.Trim();
}
string CompactCustomData(string s)
{
if (s == null || s.Length == 0) return "";
StringBuilder b = new StringBuilder(); bool blank = false;
string[] lines = s.Replace("\r", "").Split('\n');
for (int i = 0; i < lines.Length; i++)
{
string line = lines[i].TrimEnd(); bool isBlank = line.Trim().Length == 0;
if (isBlank) { if (blank) continue; blank = true; } else blank = false;
b.AppendLine(line);
}
return b.ToString().Trim();
}
string RemoveDataBlock(string s, string begin, string end)
{
int a = s.IndexOf(begin, StringComparison.OrdinalIgnoreCase);
int b = s.IndexOf(end, StringComparison.OrdinalIgnoreCase);
if (a >= 0 && b > a) return s.Remove(a, b + end.Length - a);
return s;
}
string GetWorkerState()
{
if (!pb1Found) return "OFFLINE_PB1";
if (fault != "NONE") return "FAULT";
return "OK";
}
void DrawWorkerScreen()
{
if (workerSurfaces.Count == 0) return;
string txt = BuildScreenText();
for (int i = 0; i < workerSurfaces.Count; i++) workerSurfaces[i].WriteText(txt, false);
}
string BuildScreenText()
{
StringBuilder sb = new StringBuilder();
sb.AppendLine("IMS WORKER V31 BQS COOP");
sb.AppendLine("STATE " + GetWorkerState() + "  FAULT " + fault);
sb.AppendLine("PB1 " + (pb1Found ? "OK" : "MISSING") + "  SEQ " + seq + "  AGE " + packetAgeTicks);
sb.AppendLine("INSTR " + perfLastInstr + "/" + PB_INSTRUCTION_LIMIT + " " + InstrPct(perfLastInstr) + "  MAX " + perfMaxInstr + " " + InstrPct(perfMaxInstr));
sb.AppendLine("MODE " + FormatWorkerMode() + (ConsoleEditHold() ? " EDIT" : ""));
sb.AppendLine("CARGO " + CargoGroupText() + " lanes " + laneCargos.Count + "/" + cargos.Count);
sb.AppendLine("BQS " + bqsClusterTag + " LOCAL " + bqsLocalState);
sb.AppendLine("BQS ENF " + bqsEnforce);
sb.AppendLine("ASM " + assemblers.Count + " q " + assemblerQueueTargets.Count + " coop " + assemblerCoopHelpers.Count + " dock " + coopRecoveryHelpers.Count);
sb.AppendLine("REF " + refineries.Count + " GEN " + gasGens.Count + " ICE " + FormatIceProcessingMode(IceProcessingMode()) + " " + lastGasResult);
sb.AppendLine("Queue " + queuePlanText + " last " + lastQueueResult);
sb.AppendLine(ammoPlanText);
sb.AppendLine("Mag " + lastMagResult);
if (queueAddTrace.Length > 0) sb.AppendLine(queueAddTrace);
sb.AppendLine("Ledger " + LedgerStatus());
sb.AppendLine("Ore " + oreStatusText + " plan " + orePlanText + " last " + lastOreApply);
sb.AppendLine("Dist uneven " + distUneven + " worst " + distWorstLabel);
return sb.ToString();
}
string BuildEchoStatus()
{
return "IMS WORKER V30\n" +
"State: " + GetWorkerState() + " Fault: " + fault + "\n" +
"PB1: " + (pb1Found ? "OK" : "MISSING") + " Seq: " + seq + " Age: " + packetAgeTicks + " Phase: " + workPhase + " Cadence: " + workerCadenceTicks + "\n" +
"Trace: run " + perfRunningLabel + " last " + perfLastComplete + " instr " + perfLastInstr + "/" + PB_INSTRUCTION_LIMIT + " " + InstrPct(perfLastInstr) + " max " + perfMaxInstr + " " + InstrPct(perfMaxInstr) + " " + perfMaxLabel + "\n" +
"HiWater: " + hiInstr + "/" + PB_INSTRUCTION_LIMIT + " " + InstrPct(hiInstr) + " " + hiLabel + "\n" +
"Cargo: " + CargoGroupText() + " lanes " + laneCargos.Count + "/" + cargos.Count + "\n" +
"Asm/Q/Coop/Dock: " + assemblers.Count + "/" + assemblerQueueTargets.Count + "/" + assemblerCoopHelpers.Count + "/" + coopRecoveryHelpers.Count + "\n" +
"BQS: " + bqsClusterTag + " LocalASM " + bqsLocalState + " " + bqsEnforce + "\n" +
"Gas: ICE " + FormatIceProcessingMode(IceProcessingMode()) + " " + lastGasResult + "\n" +
asmPolicyText + "\n" +
"Queue: " + lastQueueResult + " Ledger: " + LedgerStatus() + "\n" +
ammoPlanText + "\n" +
(queueAddTrace.Length > 0 ? queueAddTrace + "\n" : "") +
"Mag: " + lastMagResult + "\n" +
"Ore: " + lastOreApply + " Unload: " + lastUnloadResult + "\n" +
"Dist: " + lastMoveResult + " Out: " + lastOutputResult + "\n" +
"Cmds: QUEUE ONCE | QUEUE AMMO ONCE | WORKER FASTER/SLOWER | CLEAR STALE | RESCAN | TRACE | CLEAN DATA";
}
void ExecuteSettingsApply()
{
if (!IsBuildQueueOffline()) for (int i = 0; i < assemblers.Count; i++) EnsureMachineReady(assemblers[i]);
for (int i = 0; i < refineries.Count; i++) EnsureMachineReady(refineries[i]);
ExecuteGasGeneratorControl();
}
int ExecuteGasGeneratorControl()
{
int mode = IceProcessingMode();
if (gasGens.Count < 1) { lastGasResult = "NO GEN"; return 0; }
double h2 = GetDouble("Current.SUPPLY.H2", 100);
double o2 = GetDouble("Current.SUPPLY.O2", 100);
double h2Min = GetDouble("Target.SUPPLY.H2.Min", 0);
double o2Min = GetDouble("Target.SUPPLY.O2.Min", 0);
if (mode == ICE_MODE_MIN)
{
    if (!gasMinDemand && (h2 < h2Min || o2 < o2Min)) gasMinDemand = true;
    else if (gasMinDemand && h2 >= h2Min + GAS_MIN_HYSTERESIS && o2 >= o2Min + GAS_MIN_HYSTERESIS) gasMinDemand = false;
}
else gasMinDemand = mode == ICE_MODE_AUTO;
bool wantOn = mode == ICE_MODE_AUTO || (mode == ICE_MODE_MIN && gasMinDemand);
if (mode == ICE_MODE_OFF) wantOn = false;
int changed = 0, on = 0, off = 0, conv = 0;
for (int i = 0; i < gasGens.Count; i++)
{
    IMyGasGenerator g = gasGens[i];
    if (g == null || !g.IsFunctional) continue;
    if (g.UseConveyorSystem == false) { g.UseConveyorSystem = true; changed++; conv++; }
    IMyFunctionalBlock f = g as IMyFunctionalBlock;
    if (f != null && f.Enabled != wantOn) { f.Enabled = wantOn; changed++; }
    if (f != null && f.Enabled) on++; else off++;
}
lastGasResult = (wantOn ? "ON" : "OFF") + " " + on.ToString() + "/" + gasGens.Count.ToString() + (conv > 0 ? " CONV+" + conv.ToString() : "") + (mode == ICE_MODE_MIN ? " H2 " + h2.ToString("0") + "/" + h2Min.ToString("0") + " O2 " + o2.ToString("0") + "/" + o2Min.ToString("0") : "");
return changed;
}
int IceProcessingMode()
{
int v;
if (!int.TryParse(GetStr("Mode.REFINERY.ICE_PROCESSING", "2"), out v)) v = 2;
if (v < 0) v = 0;
if (v > 2) v = 2;
return v;
}
string FormatIceProcessingMode(int v)
{
if (v <= 0) return "OFF";
if (v == 1) return "MIN";
return "AUTO";
}
int EnsureMachineReady(IMyTerminalBlock b)
{
int changed = 0;
IMyFunctionalBlock f = b as IMyFunctionalBlock;
if (f != null && !f.Enabled)
{
f.Enabled = true;
changed++;
}
IMyAssembler a = b as IMyAssembler;
if (a != null && !a.UseConveyorSystem)
{
a.UseConveyorSystem = true;
changed++;
}
IMyRefinery r = b as IMyRefinery;
if (r != null && !r.UseConveyorSystem)
{
r.UseConveyorSystem = true;
changed++;
}
IMyGasGenerator g = b as IMyGasGenerator;
if (g != null && !g.UseConveyorSystem)
{
g.UseConveyorSystem = true;
changed++;
}
return changed;
}
int ExecuteDistributePasses(int count)
{
lastBatchMoves = 0;
lastMoveResult = "NONE";
for (int i = 0; i < count; i++)
{
AnalyzeDistribution();
AnalyzeQueuePlan();
AnalyzeOrePriority();
UpdateFault();
if (fault != "NONE")
{
if (lastBatchMoves == 0) lastMoveResult = "SKIP: " + fault;
break;
}
if (SelectDistributionTarget() == null)
{
if (lastBatchMoves == 0) lastMoveResult = "SKIP: NOTHING UNEVEN";
break;
}
if (!ExecuteDistributeOnce()) break;
lastBatchMoves++;
}
if (lastBatchMoves > 0) lastMoveResult = "OK: " + lastBatchMoves.ToString() + " MOVE(S)";
return lastBatchMoves;
}
bool ExecuteDistributeOnce()
{
lastMoveResult = "NONE";
if (fault != "NONE")
{
lastMoveResult = "SKIP: " + fault;
return false;
}
if (laneCargos.Count < 2)
{
lastMoveResult = "SKIP: NEED 2+ CARGO";
return false;
}
SpreadItem target = SelectDistributionTarget();
if (target == null)
{
lastMoveResult = "SKIP: NOTHING UNEVEN";
return false;
}
IMyInventory src = laneCargos[target.SourceLane].GetInventory(0);
IMyInventory dst = laneCargos[target.DestLane].GetInventory(0);
if (src == null || dst == null)
{
lastMoveResult = "FAIL: INVENTORY MISSING";
return false;
}
itemScratch.Clear();
src.GetItems(itemScratch);
for (int i = 0; i < itemScratch.Count; i++)
{
MyInventoryItem it = itemScratch[i];
if (ItemKey(it) != target.Key) continue;
double available = Math.Floor((double)it.Amount);
double qty = Math.Floor(target.PlanQty);
if (qty > available) qty = available;
if (qty < 1.0) continue;
MyFixedPoint amount = (MyFixedPoint)qty;
bool ok = src.TransferItemTo(dst, it, amount);
lastMoveResult = ok ? "OK" : "FAIL: TRANSFER REJECTED";
if (ok) RememberMovePair(target);
return ok;
}
lastMoveResult = "FAIL: SOURCE ITEM NOT FOUND";
return false;
}
SpreadItem SelectDistributionTarget()
{
SpreadItem fallback = null;
for (int i = 0; i < spreadList.Count; i++)
{
SpreadItem si = spreadList[i];
if (!IsValidDistributionTarget(si)) continue;
if (fallback == null) fallback = si;
if (!IsRecentMovePair(si)) return si;
}
return fallback;
}
bool IsValidDistributionTarget(SpreadItem si)
{
if (si == null || si.State != "UNEVEN") return false;
if (si.SourceLane < 0 || si.SourceLane >= laneCargos.Count || si.DestLane < 0 || si.DestLane >= laneCargos.Count) return false;
if (si.PlanQty < 1.0) return false;
return true;
}
string MovePairKey(SpreadItem si, bool reverse)
{
if (si == null) return "";
int a = reverse ? si.DestLane : si.SourceLane;
int b = reverse ? si.SourceLane : si.DestLane;
return si.Key + "|" + a.ToString() + ">" + b.ToString();
}
bool IsRecentMovePair(SpreadItem si)
{
string direct = MovePairKey(si, false);
string reverse = MovePairKey(si, true);
for (int i = 0; i < recentMovePairs.Length; i++)
{
string r = recentMovePairs[i];
if (r == null || r.Length == 0) continue;
if (r == direct || r == reverse) return true;
}
return false;
}
void RememberMovePair(SpreadItem si)
{
if (si == null) return;
recentMovePairs[recentMoveIndex] = MovePairKey(si, false);
recentMoveIndex++;
if (recentMoveIndex >= recentMovePairs.Length) recentMoveIndex = 0;
}
void ExecuteAutoOrePriority()
{
if (fault != "NONE" || WorkerPaused()) return;
int rm;
if (!int.TryParse(GetModeNewOld("Mode.REFINERY.REFINING", "Mode.REFINERY.AUTO_REFINING"), out rm) || rm <= 0) return;
for (int i = 0; i < AUTO_ORE_PASSES; i++)
{
if (!ExecuteOreApplyOnce()) return;
AnalyzeOrePriority();
}
}
void ExecuteAutoQueue()
{
if (fault != "NONE" || WorkerPaused() || ConsoleEditHold()) return;
if (autoQueueHoldSeq >= 0 && seq <= autoQueueHoldSeq) return;
int asmMode;
if (!int.TryParse(GetModeNewOld("Mode.ASSEMBLY.ASSEMBLY", "Mode.ASSEMBLY.AUTO_ASSEMBLY"), out asmMode) || asmMode <= 0) return;
if (ExecuteManualQueueBalance()) return;
if (LedgerPending() >= 1) return;
for (int i = 0; i < AUTO_QUEUE_PASSES; i++)
{
QueueCandidate q = FindQueueCandidate();
if (q == null) return;
ExecuteQueueOnce();
if (!lastQueueResult.StartsWith("OK")) return;
if (LedgerPending() >= 1) return;
AnalyzeQueuePlan();
}
}
void ExecuteAutoDistribution()
{
if (fault != "NONE" || WorkerPaused() || !DistributionEven() || laneCargos.Count < 2 || distUneven <= 0) return;
lastBatchMoves = 0;
if (ExecuteDistributeOnce())
{
lastBatchMoves = 1;
lastMoveResult = "OK: 1 MOVE(S)";
}
}
void ExecuteCoopRecoverSlice()
{
lastCoopRecover = "NONE";
if (WorkerPaused() || fault != "NONE" || laneCargos.Count < 1) return;
if (ledgerRequested < 1 || coopRecoveryHelpers.Count < 1) { lastCoopRecover = ledgerRequested < 1 ? "NO LEDGER" : "NO HELPERS"; return; }
autoClearing = true;
bool rec = TryRecoverCoopLedgerOutputs();
autoClearing = false;
if (rec) { lastOutputClear = "COOP RECOVER"; lastOutputResult = "OK"; }
}
void ExecuteAutoOutputClear()
{
if (fault != "NONE" || laneCargos.Count < 1) return;
if (WorkerPaused()) return;
autoClearing = true;
int moved = 0;
for (int i = 0; i < AUTO_OUTPUT_CLEAR_PASSES; i++) if (TryClearAssemblerOutputs()) moved++;
for (int i = 0; i < REF_OUTPUT_CLEAR_PASSES; i++)
{
if (SoftBudgetHit()) break;
if (!TryClearRefineryOutputs()) break;
moved++;
}
autoClearing = false;
if (moved > 0)
{
lastOutputClear = "AUTO OUTPUT CLEAR";
lastOutputResult = "OK +" + moved.ToString();
}
else if (lastOutputResult == "NONE") lastOutputResult = "SKIP: NO OUTPUTS";
}
void ExecuteAutoOreUnload()
{
if (WorkerPaused() || ConsoleEditHold() || !OreUnloadOn()) return;
if (fault != "NONE" || laneCargos.Count < 1) return;
autoUnloading = true;
ExecuteOreUnloadPasses(AUTO_UNLOAD_BULK_PASSES);
autoUnloading = false;
}
int ExecuteOreUnloadPasses(int passes)
{
int moved = 0;
lastUnloadResult = "NONE";
for (int i = 0; i < passes; i++)
{
if (!ExecuteOreUnloadOnce()) break;
moved++;
}
if (moved > 1) lastUnloadResult = "OK BULK x" + moved.ToString();
return moved;
}
bool ExecuteOreUnloadOnce()
{
lastUnloadResult = "NONE";
if (fault != "NONE") { lastUnloadResult = "SKIP: " + fault; return false; }
if (laneCargos.Count < 1) { lastUnloadResult = "SKIP: NO CARGO"; return false; }
EnsureUnloadCache(!autoUnloading);
int n = unloadSources.Count;
if (n < 1) { lastUnloadResult = "SKIP: NO SRC"; return false; }
int scanBudget = (unloadFastPasses > 0) ? AUTO_UNLOAD_SOURCE_SCAN_FAST : AUTO_UNLOAD_SOURCE_SCAN;
int budget = autoUnloading ? Math.Min(scanBudget, n) : n;
if (unloadScanIndex < 0 || unloadScanIndex >= n) unloadScanIndex = 0;
for (int step = 0; step < budget; step++)
{
int i = autoUnloading ? ((unloadScanIndex + step) % n) : step;
IMyTerminalBlock b = unloadSources[i];
if (b == null || b.InventoryCount <= 0 || HasTag(b, NO_UNLOAD_TAG)) continue;
for (int invI = 0; invI < b.InventoryCount; invI++)
{
IMyInventory src = b.GetInventory(invI);
if (src == null) continue;
itemScratch.Clear();
src.GetItems(itemScratch);
for (int j = 0; j < itemScratch.Count; j++)
{
MyInventoryItem it = itemScratch[j];
if (!IsOreUnloadItem(it)) continue;
double q = (double)it.Amount;
if (q <= 0.0001) continue;
int lane = ChooseUnloadLane(it);
if (lane < 0) { lastUnloadResult = "FAIL NO CARGO SPACE"; return false; }
IMyInventory dst = laneCargos[lane].GetInventory(0);
double moved;
bool ok = TryTransferMax(src, dst, it, q, out moved);
lastUnloadResult = ok ? "OK" : "FAIL TRANSFER";
if (autoUnloading)
{
unloadScanIndex = ok ? i : ((i + 1) % n);
if (ok) unloadFastPasses = 0;
}
return ok;
}
}
}
if (autoUnloading)
{
unloadScanIndex = (unloadScanIndex + budget) % n;
if (unloadFastPasses > 0) unloadFastPasses--;
}
lastUnloadResult = autoUnloading ? "SCAN " + budget.ToString() + "/" + n.ToString() : "SKIP: NO ORE/ICE";
return false;
}
void EnsureUnloadCache(bool force)
{
int sig = GetUnloadConnectorSig();
if (force || !unloadCacheReady || sig != unloadConnSig) BuildUnloadSourceCache(sig);
}
int GetUnloadConnectorSig()
{
int sig = 0;
for (int i = 0; i < allBlocks.Count; i++)
{
IMyShipConnector c = allBlocks[i] as IMyShipConnector;
if (c == null || c.Status != MyShipConnectorStatus.Connected || c.OtherConnector == null) continue;
bool selfMb1 = HasTag(c, INSTALL_TAG);
bool otherMb1 = HasTag(c.OtherConnector, INSTALL_TAG);
if (selfMb1 == otherMb1) continue;
IMyShipConnector f = selfMb1 ? c.OtherConnector : c;
if (!IsDirectShipUnloadConnector(f)) continue;
unchecked { sig = sig * 31 + (int)(f.EntityId & 0x7fffffff); }
}
return sig;
}
void BuildUnloadSourceCache(int sig)
{
unloadSources.Clear();
BuildUnloadConnectorList();
int priorityCount = 0;
for (int i = 0; i < allBlocks.Count; i++)
{
IMyTerminalBlock b = allBlocks[i];
if (b == null || b.InventoryCount <= 0) continue;
if (HasTag(b, INSTALL_TAG) || HasTag(b, NO_UNLOAD_TAG)) continue;
if (b is IMyRefinery || b is IMyAssembler || b is IMyGasGenerator || b is IMyGasTank) continue;
if (!IsOnUnloadSourceGrid(b)) continue;
if (b is IMyCargoContainer || b is IMyShipDrill || b is IMyShipConnector)
{
unloadSources.Insert(priorityCount, b);
priorityCount++;
}
else unloadSources.Add(b);
}
unloadConnSig = sig;
unloadCacheReady = true;
unloadScanIndex = 0;
unloadFastPasses = AUTO_UNLOAD_FAST_PASSES;
}
void BuildUnloadConnectorList()
{
unloadConnectors.Clear();
unloadAllowedGridIds.Clear();
for (int i = 0; i < allBlocks.Count; i++)
{
IMyShipConnector c = allBlocks[i] as IMyShipConnector;
if (c == null || c.Status != MyShipConnectorStatus.Connected || c.OtherConnector == null) continue;
bool selfMb1 = HasTag(c, INSTALL_TAG);
bool otherMb1 = HasTag(c.OtherConnector, INSTALL_TAG);
if (selfMb1 == otherMb1) continue;
IMyShipConnector f = selfMb1 ? c.OtherConnector : c;
if (!IsDirectShipUnloadConnector(f)) continue;
bool seen = false;
for (int j = 0; j < unloadConnectors.Count; j++) if (unloadConnectors[j].EntityId == f.EntityId) { seen = true; break; }
if (!seen)
{
unloadConnectors.Add(f);
AddUnloadGrid(f.CubeGrid.EntityId);
}
}
ExpandUnloadMechanicalGrids();
}
bool IsDirectShipUnloadConnector(IMyShipConnector f)
{
if (f == null || HasTag(f, NO_UNLOAD_TAG)) return false;
if (f.CubeGrid == null || f.CubeGrid.IsStatic) return false;
if (HasStationRoleTag(f)) return false;
return true;
}
bool HasStationRoleTag(IMyTerminalBlock b)
{
if (b == null) return false;
string n = b.CustomName;
return n.IndexOf("[STATION]", StringComparison.OrdinalIgnoreCase) >= 0 ||
n.IndexOf("[BASE]", StringComparison.OrdinalIgnoreCase) >= 0 ||
n.IndexOf("[HOME]", StringComparison.OrdinalIgnoreCase) >= 0;
}
void AddUnloadGrid(long id)
{
for (int i = 0; i < unloadAllowedGridIds.Count; i++) if (unloadAllowedGridIds[i] == id) return;
unloadAllowedGridIds.Add(id);
}
bool HasUnloadGrid(long id)
{
for (int i = 0; i < unloadAllowedGridIds.Count; i++) if (unloadAllowedGridIds[i] == id) return true;
return false;
}
void ExpandUnloadMechanicalGrids()
{
bool changed = true;
int guard = 0;
while (changed && guard++ < 8)
{
changed = false;
for (int i = 0; i < allBlocks.Count; i++)
{
IMyMechanicalConnectionBlock m = allBlocks[i] as IMyMechanicalConnectionBlock;
if (m == null || m.CubeGrid == null || m.TopGrid == null) continue;
long baseId = m.CubeGrid.EntityId;
long topId = m.TopGrid.EntityId;
if (HasUnloadGrid(baseId) && !m.TopGrid.IsStatic && !HasUnloadGrid(topId))
{
AddUnloadGrid(topId);
changed = true;
}
else if (HasUnloadGrid(topId) && !m.CubeGrid.IsStatic && !HasUnloadGrid(baseId))
{
AddUnloadGrid(baseId);
changed = true;
}
}
}
}
bool IsOnUnloadSourceGrid(IMyTerminalBlock b)
{
if (b == null || b.CubeGrid == null) return false;
return HasUnloadGrid(b.CubeGrid.EntityId);
}
int ChooseUnloadLane(MyInventoryItem it)
{
int best = -1;
double bestFree = -1;
MyFixedPoint one = (MyFixedPoint)1;
for (int i = 0; i < laneCargos.Count; i++)
{
if (laneCargos[i] == null || laneCargos[i].InventoryCount <= 0) continue;
IMyInventory inv = laneCargos[i].GetInventory(0);
if (inv == null || !inv.CanItemsBeAdded(one, it.Type)) continue;
double free = ((double)inv.MaxVolume - (double)inv.CurrentVolume) * 1000.0;
if (free > bestFree) { bestFree = free; best = i; }
}
return best;
}
bool IsOreUnloadItem(MyInventoryItem it)
{
string type = Safe(it.Type.TypeId.ToString());
string sub = Safe(it.Type.SubtypeId.ToString());
if (type.IndexOf("Ore", StringComparison.OrdinalIgnoreCase) >= 0) return true;
if (sub.Equals("Ice", StringComparison.OrdinalIgnoreCase)) return true;
if (sub.Equals("Stone", StringComparison.OrdinalIgnoreCase)) return true;
return false;
}
int ExecuteOutputClearPasses(int passes)
{
int moved = 0;
lastOutputClear = "NONE";
lastOutputResult = "NONE";
for (int i = 0; i < passes; i++)
{
if (!ExecuteOutputClearOnce()) break;
moved++;
}
if (moved > 1) lastOutputResult = "OK x" + moved.ToString();
return moved;
}
bool ExecuteOutputClearOnce()
{
lastOutputClear = "NONE";
lastOutputResult = "NONE";
if (fault != "NONE")
{
lastOutputResult = "SKIP: " + fault;
return false;
}
if (laneCargos.Count < 1)
{
lastOutputResult = "SKIP: NO CARGO GROUP";
return false;
}
if (TryClearAssemblerOutputs()) return true;
if (TryClearRefineryOutputs()) return true;
lastOutputResult = "SKIP: NO OUTPUTS";
return false;
}
bool TryClearAssemblerOutputs()
{
int n = assemblers.Count;
if (n < 1) return false;
if (localOutputAsmCursor < 0 || localOutputAsmCursor >= n) localOutputAsmCursor = 0;
for (int step = 0; step < n; step++)
{
if (SoftBudgetHit()) { lastOutputResult = "YIELD ASM"; return false; }
int i = (localOutputAsmCursor + step) % n;
IMyTerminalBlock b = assemblers[i] as IMyTerminalBlock;
if (TryClearOutputFromBlock(b, "ASM")) { localOutputAsmCursor = (i + 1) % n; return true; }
}
localOutputAsmCursor = 0;
return false;
}
bool TryClearRefineryOutputs()
{
if (autoClearing && !IsIngotClearOn()) return false;
int n = refineries.Count;
if (n < 1) return false;
if (localOutputRefCursor < 0 || localOutputRefCursor >= n) localOutputRefCursor = 0;
for (int step = 0; step < n; step++)
{
if (SoftBudgetHit()) { lastOutputResult = "YIELD REF"; return false; }
int i = (localOutputRefCursor + step) % n;
IMyTerminalBlock b = refineries[i] as IMyTerminalBlock;
if (TryClearOutputFromBlock(b, "REF")) { localOutputRefCursor = (i + 1) % n; return true; }
}
localOutputRefCursor = 0;
return false;
}
bool TryClearOutputFromBlock(IMyTerminalBlock b, string kind)
{
if (b == null || b.InventoryCount < 2) return false;
IMyInventory src = b.GetInventory(1);
if (src == null) return false;
itemScratch.Clear();
src.GetItems(itemScratch);
for (int i = 0; i < itemScratch.Count; i++)
{
MyInventoryItem it = itemScratch[i];
double q = Math.Floor((double)it.Amount);
if (q < 1.0) continue;
MyFixedPoint amount = (MyFixedPoint)q;
int lane = ChooseOutputClearLane(it, amount);
if (lane < 0)
{
lastOutputClear = kind + " " + ItemLabel(it) + " -> NONE (" + FormatQty(q, true) + ")";
lastOutputResult = "FAULT: NO CARGO SPACE";
return false;
}
IMyInventory dst = laneCargos[lane].GetInventory(0);
lastOutputClear = kind + " " + ItemLabel(it) + " -> " + LaneName(lane) + " (" + FormatQty(q, true) + ")";
bool ok = src.TransferItemTo(dst, it, amount);
lastOutputResult = ok ? "OK" : "FAIL: TRANSFER REJECTED";
return ok;
}
return false;
}
int ChooseOutputClearLane(MyInventoryItem it, MyFixedPoint amount)
{
int best = -1;
double bestFree = -1;
for (int i = 0; i < laneCargos.Count; i++)
{
if (laneCargos[i] == null || laneCargos[i].InventoryCount <= 0) continue;
IMyInventory inv = laneCargos[i].GetInventory(0);
if (inv == null) continue;
if (!inv.CanItemsBeAdded(amount, it.Type)) continue;
double free = ((double)inv.MaxVolume - (double)inv.CurrentVolume) * 1000.0;
if (free > bestFree)
{
bestFree = free;
best = i;
}
}
return best;
}
double CountQueuedByBlueprintToken(string blueprintToken)
{
double v = VisibleQueuedByBlueprintToken(blueprintToken);
double l = LedgerPendingForToken(blueprintToken);
return v > l ? v : l;
}
double PendingAssemblerOutputByToken(string itemToken)
{
if (itemToken == null) itemToken = "";
double total = 0;
for (int i = 0; i < assemblerQueueTargets.Count; i++) total += CountAssemblerOutput(assemblerQueueTargets[i], itemToken);
for (int i = 0; i < assemblerCoopHelpers.Count; i++) total += CountAssemblerOutput(assemblerCoopHelpers[i], itemToken);
if (assemblerQueueTargets.Count == 0 && assemblers.Count == 0)
for (int i = 0; i < basicFallbackTargets.Count; i++) total += CountAssemblerOutput(basicFallbackTargets[i], itemToken);
return total;
}
double CountAssemblerOutput(IMyAssembler a, string itemToken)
{
if (a == null || a.InventoryCount < 2) return 0;
IMyInventory inv = a.GetInventory(1);
if (inv == null) return 0;
itemScratch.Clear();
inv.GetItems(itemScratch);
double total = 0;
for (int i = 0; i < itemScratch.Count; i++)
{
MyInventoryItem it = itemScratch[i];
string id = it.Type.SubtypeId;
if (itemToken.Length == 0 || id.IndexOf(itemToken, StringComparison.OrdinalIgnoreCase) >= 0) total += (double)it.Amount;
}
return total;
}
double VisibleQueuedByBlueprintToken(string blueprintToken)
{
if (blueprintToken == null) blueprintToken = "";
double total = 0;
for (int i = 0; i < assemblerQueueTargets.Count; i++) total += CountQueueOnAssembler(assemblerQueueTargets[i], blueprintToken);
for (int i = 0; i < assemblerCoopHelpers.Count; i++) total += CountQueueOnAssembler(assemblerCoopHelpers[i], blueprintToken);
if (assemblerQueueTargets.Count == 0 && assemblers.Count == 0)
for (int i = 0; i < basicFallbackTargets.Count; i++) total += CountQueueOnAssembler(basicFallbackTargets[i], blueprintToken);
return total;
}
double CountQueueOnAssembler(IMyAssembler a, string blueprintToken)
{
if (a == null) return 0;
queueScratch.Clear();
a.GetQueue(queueScratch);
double total = 0;
for (int i = 0; i < queueScratch.Count; i++)
{
MyProductionItem q = queueScratch[i];
string id = q.BlueprintId.ToString();
if (blueprintToken.Length == 0 || id.IndexOf(blueprintToken, StringComparison.OrdinalIgnoreCase) >= 0) total += (double)q.Amount;
}
return total;
}
void ExecuteMagazineStocking()
{
lastMagResult = "NONE";
if (laneCargos.Count < 1) { lastMagResult = "SKIP NO CARGO"; return; }
ReadWsoMagConfig();
if (magRequests.Count == 0) { lastMagResult = "NO CFG"; return; }
int low = 0, noBox = 0, needCount = 0;
string firstLow = "", firstNoBox = "";
if (magReqCursor < 0 || magReqCursor >= magRequests.Count) magReqCursor = 0;
for (int step = 0; step < magRequests.Count; step++)
{
if (SoftBudgetHit()) { lastMagResult = "YIELD"; return; }
int i = (magReqCursor + step) % magRequests.Count;
MagRequest r = magRequests[i];
if (r == null || r.Wpn == null || r.Wpn.Length == 0 || r.AmmoSub == null || r.AmmoSub.Length == 0) continue;
if (r.Min <= 0 && r.Max <= 0) continue;
int target = r.Max > 0 ? r.Max : r.Min;
if (target <= 0) continue;
magBoxes.Clear();
FindMagazineBoxes(r.Wpn, magBoxes);
if (magBoxes.Count == 0)
{
noBox++;
if (firstNoBox.Length == 0) firstNoBox = r.Wpn;
continue;
}
double cur = CountAmmoInMagBoxes(magBoxes, r.AmmoSub);
if (cur >= r.Min) continue;
double need = Math.Floor(target - cur);
if (need < 1) continue;
needCount++;
double moved;
if (MoveAmmoFromCargoToBoxes(r.AmmoSub, magBoxes, need, out moved))
{
lastMagResult = "OK " + r.Wpn + " +" + FormatQty(moved, true);
magReqCursor = (i + 1) % Math.Max(1, magRequests.Count);
return;
}
low++;
if (firstLow.Length == 0) firstLow = r.Wpn;
}
if (low > 0) { lastMagResult = "SUPPLY LOW " + firstLow + (low > 1 ? "+" + (low - 1).ToString() : ""); return; }
if (noBox > 0) { lastMagResult = firstNoBox + " NO BOX" + (noBox > 1 ? "+" + (noBox - 1).ToString() : ""); return; }
if (needCount == 0) { lastMagResult = "OK ALL"; return; }
}
void ReadWsoMagConfig()
{
magRequests.Clear();
pbScratch.Clear();
GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>(pbScratch);
for (int i = 0; i < pbScratch.Count; i++)
{
IMyProgrammableBlock p = pbScratch[i];
if (p == null) continue;
string n = p.CustomName;
if (n == null || n.IndexOf(INSTALL_TAG, StringComparison.OrdinalIgnoreCase) < 0) continue;
if (n.IndexOf("[WSO-PB1]", StringComparison.OrdinalIgnoreCase) < 0 && n.IndexOf("[WSO]", StringComparison.OrdinalIgnoreCase) < 0) continue;
string block = ExtractDataBlock(p.CustomData, WSO_MAGCFG_BEGIN, WSO_MAGCFG_END);
if (block.Length > 0) { ParseMagConfig(block); return; }
}
}
void ParseMagConfig(string block)
{
string[] lines = block.Split('\n');
for (int i = 0; i < lines.Length; i++)
{
string line = lines[i].Trim();
if (!line.StartsWith("MAGCFG|", StringComparison.OrdinalIgnoreCase)) continue;
string[] p = line.Split('|');
if (p.Length < 3) continue;
MagRequest r = new MagRequest();
r.Wpn = p[1];
r.AmmoSub = p[2];
r.Min = ToInt(MagField(p, "MIN"), 0);
r.Max = ToInt(MagField(p, "MAX"), r.Min);
magRequests.Add(r);
}
}
string MagField(string[] p, string key)
{
for (int i = 3; i + 1 < p.Length; i++) if (p[i].Equals(key, StringComparison.OrdinalIgnoreCase)) return p[i + 1];
return "";
}
int ToInt(string s, int fallback)
{
int v;
if (int.TryParse(s, out v)) return v;
double d;
if (double.TryParse(s, out d)) return (int)Math.Round(d);
return fallback;
}
void FindMagazineBoxes(string wpn, List<IMyCargoContainer> result)
{
result.Clear();
for (int i = 0; i < cargos.Count; i++)
{
IMyCargoContainer c = cargos[i];
if (c == null || c.InventoryCount < 1) continue;
string n = c.CustomName;
if (n == null || n.IndexOf(INSTALL_TAG, StringComparison.OrdinalIgnoreCase) < 0 || n.IndexOf("[" + wpn + "]", StringComparison.OrdinalIgnoreCase) < 0) continue;
if (IsLaneCargo(c)) continue;
result.Add(c);
}
}
bool IsLaneCargo(IMyCargoContainer c)
{
for (int i = 0; i < laneCargos.Count; i++) if (laneCargos[i] == c) return true;
return false;
}
double CountAmmoInMagBoxes(List<IMyCargoContainer> boxes, string ammoSub)
{
double total = 0;
for (int i = 0; i < boxes.Count; i++)
{
IMyCargoContainer c = boxes[i];
if (c == null || c.InventoryCount < 1) continue;
IMyInventory inv = c.GetInventory(0);
if (inv == null) continue;
itemScratch.Clear();
inv.GetItems(itemScratch);
for (int j = 0; j < itemScratch.Count; j++)
{
MyInventoryItem it = itemScratch[j];
if (ItemSubtypeMatches(it, ammoSub)) total += (double)it.Amount;
}
}
return total;
}
bool MoveAmmoFromCargoToBoxes(string ammoSub, List<IMyCargoContainer> boxes, double need, out double moved)
{
moved = 0;
for (int lane = 0; lane < laneCargos.Count && moved < need; lane++)
{
IMyCargoContainer srcC = laneCargos[lane];
if (srcC == null || srcC.InventoryCount < 1) continue;
IMyInventory src = srcC.GetInventory(0);
if (src == null) continue;
itemScratch.Clear();
src.GetItems(itemScratch);
for (int j = 0; j < itemScratch.Count && moved < need; j++)
{
MyInventoryItem it = itemScratch[j];
if (!ItemSubtypeMatches(it, ammoSub)) continue;
double available = (double)it.Amount;
double want = Math.Min(available, need - moved);
for (int b = 0; b < boxes.Count && want > 0.0001; b++)
{
IMyCargoContainer box = boxes[b];
if (box == null || box.InventoryCount < 1) continue;
IMyInventory dst = box.GetInventory(0);
if (dst == null) continue;
double xfer;
if (TryTransferMax(src, dst, it, want, out xfer))
{
moved += xfer;
return true;
}
}
}
}
return moved > 0.0001;
}
bool ItemSubtypeMatches(MyInventoryItem it, string sub)
{
if (sub == null || sub.Length == 0) return false;
string s = Safe(it.Type.SubtypeId.ToString());
return s.Equals(sub, StringComparison.OrdinalIgnoreCase) || s.IndexOf(sub, StringComparison.OrdinalIgnoreCase) >= 0;
}
bool ExecuteManualQueueBalance()
{
ClassifyAssemblerPolicy();
int targets=ActiveQueueTargetCount();
if(targets<2)return false;
int[] loads=new int[targets];
int[] entries=new int[targets];
int src=-1,srcLoad=0;
for(int i=0;i<targets;i++)
{
IMyAssembler a=ActiveQueueTarget(i);
if(a==null||!a.IsFunctional||!a.Enabled||a.Mode!=MyAssemblerMode.Assembly)continue;
queueScratch.Clear();a.GetQueue(queueScratch);
entries[i]=queueScratch.Count;
int load=0;
for(int q=0;q<queueScratch.Count;q++)load+=(int)Math.Ceiling((double)queueScratch[q].Amount);
loads[i]=load;
if(load>srcLoad){srcLoad=load;src=i;}
}
if(src<0||srcLoad<1)return false;
int moved=0,fail=0;
string labs="";
for(int pass=0;pass<8;pass++)
{
if(Runtime.CurrentInstructionCount>32000)break;
int dst=LeastQueueTarget(loads,src);
if(dst<0)break;
if(loads[src]<=loads[dst]+1&&entries[src]<=entries[dst]+1)break;
IMyAssembler sa=ActiveQueueTarget(src),da=ActiveQueueTarget(dst);
if(sa==null||da==null)break;
queueScratch.Clear();sa.GetQueue(queueScratch);
if(queueScratch.Count<1)break;
int qi=PickQueueMoveIndex(queueScratch);
if(qi<0)break;
MyProductionItem pi=queueScratch[qi];
int amt=(int)Math.Floor((double)pi.Amount);
if(amt<1)break;
int take=amt;
int gap=loads[src]-loads[dst];
if(amt>1&&gap>2)take=Math.Max(1,Math.Min(amt,gap/2));
try
{
if(!da.CanUseBlueprint(pi.BlueprintId)){fail++;break;}
sa.RemoveQueueItem(qi,(MyFixedPoint)take);
if(da.Mode!=MyAssemblerMode.Assembly)da.Mode=MyAssemblerMode.Assembly;
da.AddQueueItem(pi.BlueprintId,(MyFixedPoint)take);
loads[src]-=take;loads[dst]+=take;entries[dst]++;if(take>=amt)entries[src]=Math.Max(0,entries[src]-1);
moved+=take;
if(labs.Length<46){if(labs.Length>0)labs+=",";labs+=ShortBlueprintLabel(pi.BlueprintId.ToString());}
}
catch{fail++;break;}
}
if(moved<1)return false;
autoQueueHoldSeq=seq;
lastQueueResult="OK SPREAD +"+moved.ToString()+" "+labs+(fail>0?" fail "+fail.ToString():"");
return true;
}
int LeastQueueTarget(int[] loads,int src)
{
int best=-1,bestLoad=999999999;
for(int i=0;i<loads.Length;i++)
{
if(i==src)continue;
IMyAssembler a=ActiveQueueTarget(i);
if(a==null||!a.IsFunctional||!a.Enabled)continue;
if(loads[i]<bestLoad){bestLoad=loads[i];best=i;}
}
return best;
}
int PickQueueMoveIndex(List<MyProductionItem> q)
{
int best=-1,bestAmt=0;
for(int i=q.Count-1;i>=0;i--)
{
int a=(int)Math.Floor((double)q[i].Amount);
if(a>bestAmt){bestAmt=a;best=i;}
}
return best;
}
string ShortBlueprintLabel(string id)
{
int slash = id.LastIndexOf('/');
if (slash >= 0 && slash + 1 < id.Length) id = id.Substring(slash + 1);
if (id.Length > 16) id = id.Substring(0, 16);
return id;
}
void AnalyzeQueuePlan()
{
UpdateQueueLedger();
ammoPlanText = BuildAmmoQueueDebug();
queuePlanText = "NONE";
QueueCandidate best = FindQueueCandidate();
ammoPlanText = BuildAmmoQueueDebug();
if (best == null) return;
queuePlanText = best.Category + " " + best.Label + " +" + FormatQty(best.Need, true);
}
string BuildAmmoQueueDebug()
{
int asmMode;
int ammoMode;
string asmTxt = GetModeNewOld("Mode.ASSEMBLY.ASSEMBLY", "Mode.ASSEMBLY.AUTO_ASSEMBLY");
if (!int.TryParse(asmTxt, out asmMode)) asmMode = -1;
string ammoTxt = GetStr("Mode.ASSEMBLY.AMMO", "?");
if (!int.TryParse(ammoTxt, out ammoMode)) ammoMode = -1;
string head = "AMMO asm=" + asmMode.ToString() + " mode=" + ammoMode.ToString();
if (!pb1Found) return head + " PB1?";
if (asmMode <= 0) return head + " ASM_OFF";
if (ammoMode <= 0) return head + " OFF";
string[] keys = new string[] { "GATLING_BOX", "RIFLE_MAG", "MISSILES", "ARTILLERY", "ASSAULT", "AUTOCANNON", "RAILGUN" };
string[] labels = new string[] { "Gat", "Rifle", "Miss", "Art", "Aslt", "Auto", "Rail" };
string[] tokens = new string[] { "NATO_25x184mm", "RapidFireAutomaticRifleGun_Mag_50rd", "Missile", "LargeCalibre", "MediumCalibre", "Autocannon", "Railgun" };
double bestNeed = 0;
string best = "NONE";
string why = "";
for (int i = 0; i < keys.Length; i++)
{
string baseKey = "AMMO." + keys[i];
double cur = GetDouble("Current." + baseKey, 0);
double target = GetDouble("Target." + baseKey + (ammoMode == 1 ? ".Min" : ".Max"), 0);
double queued = CountQueuedByBlueprintToken(tokens[i]);
double pendingOut = PendingAssemblerOutputByToken(tokens[i]);
double need = Math.Floor(target - cur - queued - pendingOut);
if (need > bestNeed)
{
bestNeed = need;
best = labels[i] + " +" + FormatQty(need, true);
why = " cur=" + FormatQty(cur, true) + " tgt=" + FormatQty(target, true) + " q=" + FormatQty(queued, true) + " out=" + FormatQty(pendingOut, true);
}
}
if (bestNeed < 1) return head + " NONE";
return head + " " + best + why;
}
QueueCandidate FindQueueCandidate()
{
ClassifyAssemblerPolicy();
int asmMode;
if (!int.TryParse(GetModeNewOld("Mode.ASSEMBLY.ASSEMBLY", "Mode.ASSEMBLY.AUTO_ASSEMBLY"), out asmMode) || asmMode <= 0)
{
queuePlanText = "ASM AUTO";
return null;
}
if (ActiveQueueTargetCount() == 0)
{
queuePlanText = "NO TARGET";
return null;
}
string[] cats = new string[] { "COMPONENTS", "AMMO", "TOOLS" };
string[] modes = new string[] { "Mode.ASSEMBLY.COMPONENTS", "Mode.ASSEMBLY.AMMO", "Mode.ASSEMBLY.TOOLS" };
bool[] isComp = new bool[] { true, false, false };
if (queueCategoryCursor < 0 || queueCategoryCursor >= cats.Length) queueCategoryCursor = 0;
for (int pass = 0; pass < cats.Length; pass++)
{
int ci = (queueCategoryCursor + pass) % cats.Length;
QueueCandidate catBest = null;
CheckQueueCategory(cats[ci], modes[ci], isComp[ci], ref catBest);
if (catBest != null)
{
catBest.CategoryIndex = ci;
queuePlanText = catBest.Category + " " + catBest.Label + " +" + FormatQty(catBest.Need, true);
return catBest;
}
}
queuePlanText = "NONE";
return null;
}
int ActiveQueueTargetCount()
{
if (assemblerQueueTargets.Count > 0) return assemblerQueueTargets.Count;
if (assemblers.Count == 0 && basicFallbackTargets.Count > 0) return basicFallbackTargets.Count;
return 0;
}
IMyAssembler ActiveQueueTarget(int i)
{
if (assemblerQueueTargets.Count > 0) return assemblerQueueTargets[i];
return basicFallbackTargets[i];
}
void ExecuteAmmoQueueOnce()
{
queueAddTrace = "";
ClassifyAssemblerPolicy();
int asmMode;
if (!int.TryParse(GetModeNewOld("Mode.ASSEMBLY.ASSEMBLY", "Mode.ASSEMBLY.AUTO_ASSEMBLY"), out asmMode) || asmMode <= 0)
{
lastQueueResult = "SKIP AMMO: ASM OFF";
return;
}
int targets = ActiveQueueTargetCount();
if (targets <= 0)
{
lastQueueResult = "SKIP AMMO: NO TARGET";
return;
}
QueueCandidate q = null;
CheckQueueCategory("AMMO", "Mode.ASSEMBLY.AMMO", false, ref q);
if (q == null)
{
lastQueueResult = "SKIP AMMO: " + BuildAmmoQueueDebug();
return;
}
QueueCandidate old = q;
int need = (int)Math.Floor(old.Need);
if (need < 1)
{
lastQueueResult = "SKIP AMMO: SMALL";
return;
}
MyDefinitionId bp;
try
{
bp = MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/" + old.Blueprint);
}
catch
{
lastQueueResult = "FAIL AMMO BP " + old.Blueprint;
return;
}
int queued = 0, used = 0, failed = 0;
int per = need / targets;
int rem = need % targets;
int start = queueTargetCursor;
if (start < 0 || start >= targets) start = 0;
string failNames = "";
for (int i = 0; i < targets; i++)
{
int amt = per + (i < rem ? 1 : 0);
if (amt < 1) continue;
int ti = (start + i) % targets;
IMyAssembler a = ActiveQueueTarget(ti);
try
{
if (a.Mode != MyAssemblerMode.Assembly) a.Mode = MyAssemblerMode.Assembly;
a.AddQueueItem(bp, (MyFixedPoint)amt);
queued += amt;
used++;
}
catch
{
failed++;
if (failNames.Length < 38)
{
if (failNames.Length > 0) failNames += ",";
failNames += ShortName(a);
}
}
}
if (queued > 0)
{
StartQueueLedger(old, queued);
autoQueueHoldSeq = seq;
queueTargetCursor = (start + Math.Max(1, used)) % targets;
lastQueueResult = "OK AMMO " + old.Label + " +" + queued.ToString() + " used " + used + "/" + targets + (failed > 0 ? " fail " + failed : "");
if (failed > 0) queueAddTrace = "ADD FAIL " + failNames;
}
else
{
lastQueueResult = "FAIL AMMO ADD " + old.Label + " targets " + targets + " fail " + failed;
queueAddTrace = "ADD FAIL " + failNames;
}
}
void ExecuteQueueOnce()
{
queueAddTrace = "";
if (ExecuteManualQueueBalance()) return;
QueueCandidate q = FindQueueCandidate();
if (q == null)
{
lastQueueResult = "SKIP: " + queuePlanText;
return;
}
int need = (int)Math.Floor(q.Need);
if (need < 1)
{
lastQueueResult = "SKIP: SMALL";
return;
}
MyDefinitionId bp;
try
{
bp = MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/" + q.Blueprint);
}
catch
{
lastQueueResult = "FAIL BP " + q.Blueprint;
return;
}
int targets = ActiveQueueTargetCount();
if (targets <= 0)
{
lastQueueResult = "FAIL NO TARGET";
return;
}
int queued = 0, used = 0, failed = 0;
int per = need / targets;
int rem = need % targets;
int start = queueTargetCursor;
if (start < 0 || start >= targets) start = 0;
string failNames = "";
for (int i = 0; i < targets; i++)
{
int amt = per + (i < rem ? 1 : 0);
if (amt < 1) continue;
int ti = (start + i) % targets;
IMyAssembler a = ActiveQueueTarget(ti);
try
{
if (a.Mode != MyAssemblerMode.Assembly) a.Mode = MyAssemblerMode.Assembly;
a.AddQueueItem(bp, (MyFixedPoint)amt);
queued += amt;
used++;
}
catch
{
failed++;
if (failNames.Length < 38)
{
if (failNames.Length > 0) failNames += ",";
failNames += ShortName(a);
}
}
}
if (queued > 0)
{
StartQueueLedger(q, queued);
autoQueueHoldSeq = seq;
queueTargetCursor = (start + Math.Max(1, used)) % targets;
if (q.CategoryIndex >= 0) queueCategoryCursor = (q.CategoryIndex + 1) % 3;
lastQueueResult = "OK " + q.Label + " +" + queued.ToString() + " used " + used + "/" + targets + (failed > 0 ? " fail " + failed : "");
if (failed > 0) queueAddTrace = "ADD FAIL " + failNames;
}
else
{
lastQueueResult = "FAIL ADD " + q.Label + " targets " + targets + " fail " + failed;
queueAddTrace = "ADD FAIL " + failNames;
}
}
string ShortName(IMyTerminalBlock b)
{
if (b == null) return "NULL";
string n = b.CustomName;
if (n == null) return "?";
n = n.Replace(INSTALL_TAG, "").Trim();
if (n.Length > 18) n = n.Substring(0, 18);
return n;
}
void CheckQueueCategory(string category, string modeKey, bool components, ref QueueCandidate best)
{
int m;
if (!int.TryParse(GetStr(modeKey, "0"), out m) || m <= 0) return;
string[] keys, labels, tokens, bps;
if (components)
{
keys = new string[] { "STEEL_PLATE", "INT__PLATE", "CONST__COMP", "MOTOR", "COMPUTER", "DISPLAY", "GIRDER", "SMALL_TUBE", "LARGE_TUBE", "METAL_GRID", "B__GLASS", "MEDICAL", "DETECTOR", "RADIO_COMM", "POWER_CELL", "REACTOR_COMP", "SUPERCOND_", "THRUSTER_COMP" };
labels = new string[] { "Steel Plate", "Int Plate", "Const Comp", "Motor", "Computer", "Display", "Girder", "Small Tube", "Large Tube", "Metal Grid", "B Glass", "Medical", "Detector", "Radio", "Power Cell", "Reactor", "Supercond", "Thruster" };
tokens = new string[] { "SteelPlate", "InteriorPlate", "Construction", "Motor", "Computer", "Display", "Girder", "SmallTube", "LargeTube", "MetalGrid", "BulletproofGlass", "Medical", "Detector", "RadioCommunication", "PowerCell", "Reactor", "Superconductor", "Thrust" };
bps = new string[] { "SteelPlate", "InteriorPlate", "ConstructionComponent", "MotorComponent", "ComputerComponent", "Display", "GirderComponent", "SmallTube", "LargeTube", "MetalGrid", "BulletproofGlass", "MedicalComponent", "DetectorComponent", "RadioCommunicationComponent", "PowerCell", "ReactorComponent", "Superconductor", "ThrustComponent" };
}
else if (category == "AMMO")
{
keys = new string[] { "GATLING_BOX", "RIFLE_MAG", "MISSILES", "ARTILLERY", "ASSAULT", "AUTOCANNON", "RAILGUN" };
labels = new string[] { "Gatling", "Rifle Mag", "Missile", "Artillery", "Assault", "Autocannon", "Railgun" };
tokens = new string[] { "NATO_25x184mm", "RapidFireAutomaticRifleGun_Mag_50rd", "Missile", "LargeCalibre", "MediumCalibre", "Autocannon", "Railgun" };
bps = new string[] { "Position0080_NATO_25x184mmMagazine", "Position0050_RapidFireAutomaticRifleGun_Mag_50rd", "Position0100_Missile200mm", "Position0120_LargeCalibreAmmo", "Position0110_MediumCalibreAmmo", "Position0090_AutocannonClip", "Position0130_SmallRailgunAmmo" };
}
else
{
return;
}
for (int i = 0; i < keys.Length; i++)
{
string baseKey = category + "." + keys[i];
if (ledgerFault && baseKey.Equals(staleBaseKey, StringComparison.OrdinalIgnoreCase)) continue;
double cur = GetDouble("Current." + baseKey, 0);
double target = GetDouble("Target." + baseKey + (m == 1 ? ".Min" : ".Max"), 0);
double queued = CountQueuedByBlueprintToken(tokens[i]);
double pendingOut = PendingAssemblerOutputByToken(tokens[i]);
double accounted = queued + pendingOut;
double need = Math.Floor(target - cur - accounted);
if (need < 1) continue;
if (best == null || need > best.Need)
{
best = new QueueCandidate();
best.Category = category;
best.Label = labels[i];
best.Token = tokens[i];
best.Blueprint = bps[i];
best.BaseKey = baseKey;
best.Current = cur;
best.Target = target;
best.Queued = accounted;
best.Need = need;
}
}
}
void StartQueueLedger(QueueCandidate q, double amount)
{
ledgerLabel = q.Label;
ledgerToken = q.Token;
ledgerBaseKey = q.BaseKey;
ledgerRequested = Math.Floor(amount);
ledgerStartStock = q.Current;
ledgerStale = 0; lastLedgerPending = -1; ledgerSeenSeq = seq; ledgerFault = false; staleBaseKey = ""; staleLabel = "";
ledgerStartQueue = VisibleQueuedByBlueprintToken(ledgerToken);
ledgerStartTrusted = LedgerTrustedSeen();
lastCoopRecover = "PENDING " + ledgerLabel + " " + FormatQty(ledgerRequested, true);
}
void UpdateQueueLedger()
{
if (ledgerRequested < 1 || ledgerBaseKey == null || ledgerBaseKey.Length == 0) return;
double p = LedgerPending();
if (p < 1) { ClearLedger("COMPLETE"); return; }
if (seq != ledgerSeenSeq)
{
double visibleQ = VisibleQueuedByBlueprintToken(ledgerToken);
if (visibleQ > 0 || p < lastLedgerPending || lastLedgerPending < 0) ledgerStale = 0;
else ledgerStale++;
ledgerSeenSeq = seq;
lastLedgerPending = p;
if (ledgerStale > 8) FaultLedgerStale();
}
}
void FaultLedgerStale()
{
string l = ledgerLabel; string k = ledgerBaseKey;
ClearLedger("STALE " + l);
ledgerFault = true; staleLabel = l; staleBaseKey = k;
}
void ClearLedger(string msg)
{
ledgerRequested = 0; ledgerLabel = ""; ledgerToken = ""; ledgerBaseKey = ""; ledgerStartStock = 0; ledgerStartQueue = 0; ledgerStartTrusted = 0;
InvalidateLedgerPendingCache();
ledgerStale = 0; lastLedgerPending = -1; ledgerSeenSeq = seq; autoQueueHoldSeq = seq; lastCoopRecover = msg;
}
double LedgerProgress()
{
if (ledgerRequested < 1) return 0;
double vq = VisibleQueuedByBlueprintToken(ledgerToken);
double byQueue = ledgerStartQueue - vq; if (byQueue < 0) byQueue = 0;
double bySink = LedgerTrustedSeen() - ledgerStartTrusted; if (bySink < 0) bySink = 0;
return Math.Floor(byQueue > bySink ? byQueue : bySink);
}
double LedgerTrustedSeen()
{
if (ledgerToken == null || ledgerToken.Length == 0) return 0;
double t = GetDouble("Current." + ledgerBaseKey, ledgerStartStock);
t += PendingAssemblerOutputByToken(ledgerToken);
t += CountMagBoxesByToken(ledgerToken);
return Math.Floor(t);
}
double CountMagBoxesByToken(string token)
{
if (token == null || token.Length == 0) return 0;
double total = 0;
for (int i = 0; i < magRequests.Count; i++)
{
if (SoftBudgetHit()) break;
MagRequest r = magRequests[i];
if (r == null || r.Wpn == null || r.AmmoSub == null) continue;
if (!TokenMatch(r.AmmoSub, token)) continue;
magBoxes.Clear();
FindMagazineBoxes(r.Wpn, magBoxes);
total += CountAmmoInMagBoxes(magBoxes, r.AmmoSub);
}
return total;
}
bool TokenMatch(string a, string b)
{
if (a == null || b == null) return false;
return a.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0 || b.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0;
}
double LedgerPending()
{
if (ledgerRequested < 1 || ledgerBaseKey == null || ledgerBaseKey.Length == 0) return 0;
if (ledgerPendingCacheTick == tick) return ledgerPendingCache;
double p = ledgerRequested - LedgerProgress();
ledgerPendingCache = p > 0 ? Math.Floor(p) : 0;
ledgerPendingCacheTick = tick;
return ledgerPendingCache;
}
double LedgerPendingForToken(string token)
{
if (token == null) token = "";
if (ledgerToken == null || ledgerToken.Length == 0) return 0;
if (TokenMatch(ledgerToken, token)) return LedgerPending();
return 0;
}
string LedgerStatus()
{
if (ledgerFault) return "STALE " + Shorten(staleLabel, 12);
double p = LedgerPending();
if (p < 1) return "NONE";
return Shorten(ledgerLabel, 12) + " " + FormatQty(p, true);
}
bool TryRecoverCoopLedgerOutputs()
{
if (ledgerRequested < 1) return false;
if (coopRecoveryHelpers.Count < 1) DiscoverCoopRecoveryHelpers();
int n = coopRecoveryHelpers.Count;
if (n < 1) { lastCoopRecover = "NO HELPERS"; return false; }
if (coopBlockIdx < 0 || coopBlockIdx >= n) coopBlockIdx = 0;
int checkedCount = 0;
int budget = Math.Min(COOP_RECOVER_BUDGET, n);
while (checkedCount < budget)
{
if (SoftBudgetHit()) { lastCoopRecover = "YIELD"; return false; }
int i = (coopBlockIdx + checkedCount) % n;
IMyAssembler a = coopRecoveryHelpers[i];
if (a != null && !HasTag(a, INSTALL_TAG) && a.IsFunctional && a.CooperativeMode)
{
IMyFunctionalBlock fb = a as IMyFunctionalBlock;
if ((fb == null || fb.Enabled) && a.InventoryCount > 1)
{
if (TryRecoverLedgerFromInv(a.GetInventory(1), "XASM", a.CustomName, 1))
{
coopBlockIdx = (i + 1) % n;
return true;
}
}
}
checkedCount++;
}
coopBlockIdx = (coopBlockIdx + checkedCount) % n;
lastCoopRecover = "SCAN " + checkedCount.ToString() + "/" + n.ToString() + " ASMOUT";
return false;
}
bool TryRecoverLedgerFromInv(IMyInventory src, string kind, string srcName, int invIndex)
{
if (src == null || ledgerRequested < 1) return false;
itemScratch.Clear();
src.GetItems(itemScratch);
for (int j = 0; j < itemScratch.Count; j++)
{
MyInventoryItem it = itemScratch[j];
if (!ItemMatchesLedger(it)) continue;
double q = Math.Floor((double)it.Amount);
if (q < 1) continue;
if (q > ledgerRequested) q = ledgerRequested;
MyFixedPoint amount = (MyFixedPoint)q;
int lane = ChooseOutputClearLane(it, amount);
if (lane < 0)
{
lastCoopRecover = "FAULT NO CARGO";
lastOutputResult = "FAULT: NO CARGO SPACE";
return false;
}
IMyInventory dst = laneCargos[lane].GetInventory(0);
bool ok = false;
if (dst != null && dst.CanItemsBeAdded(amount, it.Type) && src.IsConnectedTo(dst))
ok = src.TransferItemTo(dst, it, amount);
lastOutputClear = kind + " " + ItemLabel(it) + " -> " + LaneName(lane) + " (" + FormatQty(q, true) + ")";
if (ok)
{
lastCoopRecover = ItemLabel(it) + " +" + FormatQty(q, true);
lastOutputResult = "OK";
ledgerRequested -= q;
InvalidateLedgerPendingCache();
if (ledgerRequested < 1) ClearLedger("COMPLETE");
return true;
}
lastCoopRecover = "MISS " + ItemLabel(it);
lastOutputResult = "MISS: TRY NEXT";
}
return false;
}

bool ItemMatchesLedger(MyInventoryItem it)
{
if (ledgerToken == null || ledgerToken.Length == 0) return false;
string sub = Safe(it.Type.SubtypeId.ToString());
string lab = ItemLabel(it);
return TokenMatch(sub, ledgerToken) || lab.IndexOf(ledgerLabel, StringComparison.OrdinalIgnoreCase) >= 0;
}
class MagRequest
{
public string Wpn;
public string AmmoSub;
public int Min;
public int Max;
}
class QueueCandidate
{
public int CategoryIndex = -1;
public string Category;
public string Label;
public string Token;
public string Blueprint;
public string BaseKey;
public double Current;
public double Target;
public double Queued;
public double Need;
}
double OreScoreMult(string name)
{
if (name == "Iron") return 1.35;
if (name == "Nickel" || name == "Cobalt" || name == "Silicon") return 1.15;
if (name == "Gold" || name == "Platinum") return 0.75;
return 1.0;
}
void AnalyzeOrePriority()
{
oreStatusText = "AUTO";
orePlanText = "NONE";
int rm;
if (!int.TryParse(GetModeNewOld("Mode.REFINERY.REFINING", "Mode.REFINERY.AUTO_REFINING"), out rm) || rm <= 0) return;
oreStatusText = "ON";
string[] names = new string[] { "Iron", "Nickel", "Cobalt", "Silicon", "Magnesium", "Silver", "Gold", "Platinum", "Uranium" };
string[] keys = new string[] { "REFINED.IRON", "REFINED.NICKEL", "REFINED.COBALT", "REFINED.SILICON", "REFINED.MAGNESIUM", "REFINED.SILVER", "REFINED.GOLD", "REFINED.PLATINUM", "SUPPLY.URANIUM" };
double bestScore = 0;
int best = -1;
double bestAvail = 0;
for (int i = 0; i < names.Length; i++)
{
double cur = GetDouble("Current." + keys[i], 0);
double min = GetDouble("Target." + keys[i] + ".Min", 0);
double shortage = min - cur;
if (shortage <= 0) continue;
double avail = CountOreInCargo(names[i]);
if (avail < 1) continue;
double score = shortage * OreScoreMult(names[i]);
if (score > bestScore) { bestScore = score; best = i; bestAvail = avail; }
}
if (best < 0)
{
oreStatusText = "ON OK";
orePlanText = "no shortage ore";
return;
}
IMyRefinery r;
string curOre;
double curQty;
int ri = FindRefineryForOrePlan(names[best], out r, out curOre, out curQty);
if (r == null)
{
orePlanText = "no refinery input";
return;
}
string rn = "R" + (ri + 1).ToString();
if (curOre.Length == 0) orePlanText = rn + " fill " + names[best] + " <= " + FormatQty(bestAvail, true);
else if (curOre.Equals(names[best], StringComparison.OrdinalIgnoreCase))
{
if (curQty < 3000 && bestAvail >= 1) orePlanText = rn + " top " + names[best] + " " + FormatQty(curQty, true);
else orePlanText = rn + " keep " + names[best];
}
else orePlanText = rn + " swap " + curOre + " -> " + names[best];
}
int FindRefineryForOrePlan(string wanted, out IMyRefinery rr, out string curOre, out double curQty)
{
rr = null;
curOre = "";
curQty = 0;
if (refineries.Count <= 0) return -1;
if (orePriorityRefineryCursor < 0 || orePriorityRefineryCursor >= refineries.Count) orePriorityRefineryCursor = 0;
int keepIdx = -1;
string keepOre = "";
double keepQty = 0;
for (int pass = 0; pass < refineries.Count; pass++)
{
int i = (orePriorityRefineryCursor + pass) % refineries.Count;
IMyRefinery r = refineries[i];
if (r == null || r.InventoryCount < 1) continue;
IMyInventory inv = r.GetInventory(0);
if (inv == null) continue;
string ore;
double qty;
ReadFirstRefineryOre(inv, out ore, out qty);
if (ore.Length == 0) { rr = r; curOre = ""; curQty = 0; return i; }
if (ore.Equals(wanted, StringComparison.OrdinalIgnoreCase))
{
if (qty < 3000) { rr = r; curOre = ore; curQty = qty; return i; }
if (keepIdx < 0) { keepIdx = i; keepOre = ore; keepQty = qty; }
continue;
}
rr = r;
curOre = ore;
curQty = qty;
return i;
}
if (keepIdx >= 0)
{
rr = refineries[keepIdx];
curOre = keepOre;
curQty = keepQty;
return keepIdx;
}
return -1;
}
void ReadFirstRefineryOre(IMyInventory inv, out string ore, out double qty)
{
ore = "";
qty = 0;
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
bool ExecuteOreApplyOnce()
{
lastOreApply = "NONE";
int rm;
if (!int.TryParse(GetModeNewOld("Mode.REFINERY.REFINING", "Mode.REFINERY.AUTO_REFINING"), out rm) || rm <= 0)
{
lastOreApply = "SKIP AUTO";
return false;
}
string wanted;
if (!FindBestPriorityOre(out wanted))
{
lastOreApply = "SKIP NO ORE NEED";
return false;
}
IMyRefinery r;
string curOre;
double curQty;
int ri = FindRefineryForOrePlan(wanted, out r, out curOre, out curQty);
if (r == null || r.InventoryCount < 1)
{
lastOreApply = "SKIP NO REFINERY";
return false;
}
IMyInventory rin = r.GetInventory(0);
if (rin == null)
{
lastOreApply = "SKIP NO INPUT";
return false;
}
if (refineries.Count > 0) orePriorityRefineryCursor = (ri + 1) % refineries.Count;
if (curOre.Length > 0 && curOre.Equals(wanted, StringComparison.OrdinalIgnoreCase) && curQty >= 3000)
{
lastOreApply = "SKIP KEEP " + wanted;
return false;
}
if (curOre.Length > 0 && !curOre.Equals(wanted, StringComparison.OrdinalIgnoreCase))
{
if (!ReturnFirstRefineryOreToCargo(rin, curOre)) return false;
}
double moved;
bool ok = MoveOreFromCargoToRefinery(wanted, rin, out moved);
if (ok) lastOreApply = "R" + (ri + 1).ToString() + " " + wanted + " +" + FormatQty(moved, true);
else lastOreApply = "FAIL NO " + wanted + " MOVE";
return ok;
}
bool FindBestPriorityOre(out string bestName)
{
bestName = "";
string[] names = new string[] { "Iron", "Nickel", "Cobalt", "Silicon", "Magnesium", "Silver", "Gold", "Platinum", "Uranium" };
string[] keys = new string[] { "REFINED.IRON", "REFINED.NICKEL", "REFINED.COBALT", "REFINED.SILICON", "REFINED.MAGNESIUM", "REFINED.SILVER", "REFINED.GOLD", "REFINED.PLATINUM", "SUPPLY.URANIUM" };
double bestScore = 0;
for (int i = 0; i < names.Length; i++)
{
double cur = GetDouble("Current." + keys[i], 0);
double min = GetDouble("Target." + keys[i] + ".Min", 0);
double shortage = min - cur;
if (shortage <= 0) continue;
double avail = CountOreInCargo(names[i]);
if (avail < 1) continue;
double score = shortage * OreScoreMult(names[i]);
if (score > bestScore) { bestScore = score; bestName = names[i]; }
}
return bestName.Length > 0;
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
double q = Math.Floor((double)it.Amount);
if (q < 1) { lastOreApply = "FAIL OLD ORE <1"; return false; }
MyFixedPoint amount = (MyFixedPoint)q;
int lane = ChooseOutputClearLane(it, amount);
if (lane < 0) { lastOreApply = "FAIL NO CARGO OLD"; return false; }
IMyInventory dst = laneCargos[lane].GetInventory(0);
bool ok = rin.TransferItemTo(dst, it, amount);
if (!ok) { lastOreApply = "FAIL RETURN " + expectedOre; return false; }
return true;
}
return true;
}
bool MoveOreFromCargoToRefinery(string wanted, IMyInventory rin, out double moved)
{
moved = 0;
for (int lane = 0; lane < laneCargos.Count; lane++)
{
IMyCargoContainer c = laneCargos[lane];
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
if (q < 1) continue;
return TryTransferMax(src, rin, it, q, out moved);
}
}
return false;
}
bool TryTransferMax(IMyInventory src, IMyInventory dst, MyInventoryItem it, double maxQty, out double moved)
{
moved = 0;
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
double CountOreInCargo(string ore)
{
double total = 0;
for (int i = 0; i < laneCargos.Count; i++)
{
IMyCargoContainer c = laneCargos[i];
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
void AnalyzeDistribution()
{
spread.Clear();
spreadList.Clear();
distUneven = 0;
distWorstScore = -1;
distWorstLabel = "NONE";
int lanes = laneCargos.Count;
if (lanes <= 0) return;
for (int lane = 0; lane < lanes; lane++)
{
if (SoftBudgetHit()) break;
IMyCargoContainer c = laneCargos[lane];
if (c == null || c.InventoryCount <= 0) continue;
IMyInventory inv = c.GetInventory(0);
if (inv == null) continue;
itemScratch.Clear();
inv.GetItems(itemScratch);
for (int i = 0; i < itemScratch.Count; i++)
{
MyInventoryItem it = itemScratch[i];
string key = ItemKey(it);
if (key.Length == 0) continue;
SpreadItem si;
if (!spread.TryGetValue(key, out si))
{
si = new SpreadItem();
si.Key = key;
si.Label = ItemLabel(it);
si.Qty = new double[lanes];
spread.Add(key, si);
spreadList.Add(si);
}
si.Qty[lane] += (double)it.Amount;
}
}
double tol = GetDouble("Policy.TolerancePercent", 5.0);
if (tol < 1) tol = 1;
if (tol > 24) tol = 24;
double ideal = 100.0 / Math.Max(1, lanes);
for (int i = 0; i < spreadList.Count; i++)
{
SpreadItem si = spreadList[i];
si.Total = 0;
si.WorstScore = 0;
si.SourceLane = -1;
si.DestLane = -1;
double maxQ = -1;
double minQ = double.MaxValue;
for (int l = 0; l < lanes; l++)
{
si.Total += si.Qty[l];
if (si.Qty[l] > maxQ) { maxQ = si.Qty[l]; si.SourceLane = l; }
if (si.Qty[l] < minQ) { minQ = si.Qty[l]; si.DestLane = l; }
}
if (si.Total <= 0) continue;
if (lanes < 2 || si.Total < (double)lanes)
{
si.State = "SMALL";
si.Plan = "too little to split cleanly";
continue;
}
bool ok = true;
for (int l = 0; l < lanes; l++)
{
double share = si.Qty[l] / si.Total * 100.0;
double score = Math.Abs(share - ideal);
if (score > si.WorstScore) si.WorstScore = score;
if (share < ideal - tol || share > ideal + tol) ok = false;
}
if (ok)
{
si.State = "OK";
si.Plan = "within tolerance";
}
else
{
si.State = "UNEVEN";
double idealQty = si.Total / (double)lanes;
double sourceExcess = si.Qty[si.SourceLane] - idealQty;
double destNeed = idealQty - si.Qty[si.DestLane];
double moveQty = Math.Min(sourceExcess, destNeed);
if (moveQty <= 0) moveQty = idealQty;
if (moveQty > si.Qty[si.SourceLane]) moveQty = si.Qty[si.SourceLane];
moveQty = Math.Floor(moveQty);
si.PlanQty = moveQty;
si.PlanPct = si.Total > 0 ? moveQty / si.Total * 100.0 : 0;
if (si.PlanQty < 1.0)
{
si.State = "SMALL";
si.Plan = "too little to split cleanly";
continue;
}
si.Plan = si.Label + " " + LaneName(si.SourceLane) + " -> " + LaneName(si.DestLane) + " " + FormatMovePct(si.PlanPct) + " (" + FormatQty(si.PlanQty, true) + ")";
distUneven++;
if (si.WorstScore > distWorstScore)
{
distWorstScore = si.WorstScore;
distWorstLabel = si.Label;
}
}
}
SortSpreadWorstFirst();
}
void SortSpreadWorstFirst()
{
for (int i = 0; i < spreadList.Count - 1; i++)
{
int best = i;
for (int j = i + 1; j < spreadList.Count; j++)
{
if (SpreadSortScore(spreadList[j]) > SpreadSortScore(spreadList[best])) best = j;
}
if (best != i)
{
SpreadItem tmp = spreadList[i];
spreadList[i] = spreadList[best];
spreadList[best] = tmp;
}
}
}
double SpreadSortScore(SpreadItem si)
{
if (si == null) return -1;
if (si.State == "UNEVEN") return 1000.0 + si.WorstScore;
if (si.State == "SMALL") return 10.0;
return si.WorstScore;
}
string FormatMovePct(double pct)
{
if (pct < 0) pct = 0;
return pct.ToString("0.0") + "%";
}
string FormatQty(double q)
{
return FormatQty(q, false);
}
string FormatQty(double q, bool whole)
{
if (whole) q = Math.Floor(q);
if (q >= 1000000) return (q / 1000000.0).ToString(whole ? "0" : "0.##") + "M";
if (q >= 1000) return (q / 1000.0).ToString(whole ? "0" : "0.##") + "k";
if (whole || q >= 10) return q.ToString("0");
return q.ToString("0.##");
}
string LaneName(int lane)
{
if (lane < 0) return "?";
if (lane < 26) return ((char)('A' + lane)).ToString();
return (lane + 1).ToString();
}
string ItemKey(MyInventoryItem it)
{
return Safe(it.Type.TypeId.ToString()) + "/" + Safe(it.Type.SubtypeId.ToString());
}
string ItemLabel(MyInventoryItem it)
{
string sub = Safe(it.Type.SubtypeId.ToString());
if (sub.Length == 0) sub = it.Type.ToString();
return CleanItemName(sub);
}
string CleanItemName(string s)
{
if (s == null) return "?";
s = s.Replace("Component", " Comp");
s = s.Replace("Ingot", " Ingot");
s = s.Replace("Ore", " Ore");
s = s.Replace("SteelPlate", "Steel Plate");
s = s.Replace("InteriorPlate", "Interior Plate");
s = s.Replace("Construction", "Construction");
s = s.Replace("NATO_25x184mm", "Gatling Ammo");
return s;
}
string Shorten(string s, int max)
{
if (s == null) return "";
if (s.Length <= max) return s;
if (max <= 1) return s.Substring(0, max);
return s.Substring(0, max - 1) + ".";
}
double GetDouble(string key, double fallback)
{
string v;
if (!pb1.TryGetValue(key, out v)) return fallback;
double x;
if (double.TryParse(v, out x)) return x;
return fallback;
}
class SpreadItem
{
public string Key;
public string Label;
public double[] Qty;
public double Total;
public double WorstScore;
public int SourceLane;
public int DestLane;
public double PlanQty;
public double PlanPct;
public string State;
public string Plan;
}
bool WorkerPaused()
{
int v;
return int.TryParse(GetStr("Mode.SYSTEM.WORKER_MODE", "0"), out v) && v > 0;
}
bool ConsoleEditHold()
{
int v;
return int.TryParse(GetStr("Console.EditHold", "0"), out v) && v > 0;
}
bool DistributionEven()
{
int v;
return !int.TryParse(GetStr("Mode.SYSTEM.DISTRIBUTION_METHOD", "0"), out v) || v == 0;
}
string FormatWorkerMode()
{
return WorkerPaused() ? "PAUSE" : "AUTO";
}
bool IsIngotClearOn()
{
int v;
return int.TryParse(GetStr("Mode.REFINERY.INGOT_CLEARING", "0"), out v) && v > 0;
}
bool OreUnloadOn()
{
int v;
return int.TryParse(GetModeNewOld("Mode.REFINERY.ORE_UNLOAD", "Mode.REFINERY.ORE_FEEDING"), out v) && v > 0;
}
string GetModeNewOld(string newKey, string oldKey)
{
string v;
if (pb1.TryGetValue(newKey, out v)) return v;
if (pb1.TryGetValue(oldKey, out v)) return v;
return "?";
}
string GetStr(string key, string fallback)
{
string v;
if (pb1.TryGetValue(key, out v)) return v;
return fallback;
}
int GetInt(string key, int fallback)
{
string v;
if (!pb1.TryGetValue(key, out v)) return fallback;
int x;
if (int.TryParse(v, out x)) return x;
return fallback;
}
string Safe(string s)
{
return s == null ? "" : s;
}
