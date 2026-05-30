// OB1_IMS_PB1_V034_BuildQueueSourceLocalDisplay
// Station-side IMS console for OB1.
// V031: mirrors BUILD QUEUE SOURCE with MB1; newest cluster sequence wins; no enforcement yet.
// V030: adds MB1 as selectable BUILD QUEUE SOURCE peer; config/export only.
// V028: battery percent uses functional batteries only and shows compact DAMAGED indicator for incomplete/nonfunctional batteries.
// V027: production edit hold reduced to 3 seconds; V026 dynamic STEP banks preserved.
// V025: production-affecting console edits enter an export hold indicator; PB3 respects Console.EditHold before queueing.
// V022: adds SYSTEM-page momentary GROUP REGROUP command for PB2 sticky grouped auto assignments.
// V021: aligns distribution method language to REDUNDANT/GROUPED and adds COMPONENT STORAGE REST/SEPARATE.
// V017: reads PB2 refinery auto-block status and blinks BLOCKED above AUTO on the graph/status surface.
// V016: changes ORE UNLOAD to OFF/ON, keeps REFINING OFF/AUTO/OPTIMIZED,
// preserves target values and mode selections without preserving stale mode bounds.
// Safe behavior: read/report/export only. No inventory movement, no queue edits, no block setting changes.

const string INSTALL_TAG = "[OB1]";
const string DISPLAY_TAG = "[IMS]";
const string WORKER_TAG = "[IMSWORKER]";
const string PB2_TAG = "[PB2]";
const string PROD_TAG = "[IMSPROD]";
const string PB3_TAG = "[PB3]";
const string MB1_TAG = "[MB1]";
const int SCAN_TICKS = 12; // Update10 * 12 ~= 2 seconds
const int EDIT_HOLD_TICKS = 18;
const double LARGE_CARGO_MIN_L = 100000.0;
const string PB1_EXPORT_BEGIN = "# IMS_PB1_EXPORT_BEGIN";
const string PB1_EXPORT_END = "# IMS_PB1_EXPORT_END";
const string WORKER_STATUS_BEGIN = "# IMS_WORKER_STATUS_BEGIN";
const string WORKER_STATUS_END = "# IMS_WORKER_STATUS_END";
const string MB1_SERVICE_BEGIN = "# MB1_IMS_SERVICE_TARGETS_BEGIN";
const string MB1_SERVICE_END = "# MB1_IMS_SERVICE_TARGETS_END";
const string OB1_STATUS_BEGIN = "# OB1_IMS_STATION_STATUS_BEGIN";
const string OB1_STATUS_END = "# OB1_IMS_STATION_STATUS_END";
const string OFFICER_DATA_BEGIN = "# OB1_IMS_OFFICER_DATA_BEGIN";
const string OFFICER_DATA_END = "# OB1_IMS_OFFICER_DATA_END";
const string DOCKED_BEGIN = "# OB1_IMS_DOCKED_VESSELS_BEGIN";
const string DOCKED_END = "# OB1_IMS_DOCKED_VESSELS_END";
const string UNLOAD_AUTH_BEGIN = "# OB1_IMS_UNLOAD_AUTH_BEGIN";
const string UNLOAD_AUTH_END = "# OB1_IMS_UNLOAD_AUTH_END";
const string DETAIL_TAG_PREFIX = "[IMS-DETAIL";
const string IMS_ENTITY_TAG = "OB1";
const string IMS_ENTITY_LABEL = "OB1";

Color C_BG = new Color(2, 5, 7);
Color C_PANEL = new Color(9, 20, 27);
Color C_TEXT = new Color(218, 245, 240);
Color C_MUTED = new Color(110, 165, 170);
Color C_DIM = new Color(40, 72, 82);
Color C_CYAN = new Color(70, 245, 220);
Color C_GREEN = new Color(85, 230, 145);
Color C_YELLOW = new Color(245, 190, 70);
Color C_RED = new Color(245, 75, 65);
const TextAlignment LEF = TextAlignment.LEFT;
const TextAlignment CEN = TextAlignment.CENTER;
const TextAlignment RIG = TextAlignment.RIGHT;
const int TARGET_PAGE = 0;
const int MODE_PAGE = 1;
const int READONLY_PAGE = 2;
const int AUTH_PAGE = 3;
const int CONSOLE_ROWS = 5;

List<IMyTerminalBlock> tagged = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> inventoryBlocks = new List<IMyTerminalBlock>();
List<IMyCargoContainer> managedCargo = new List<IMyCargoContainer>();
List<IMyGasTank> gasTanks = new List<IMyGasTank>();
List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
List<IMyTextSurface> auxSurfaces = new List<IMyTextSurface>(); // explicit [IMS-DETAIL#] targets only.
List<MyInventoryItem> itemScratch = new List<MyInventoryItem>();
Dictionary<string, double> ingots = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
Dictionary<string, double> ores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
Dictionary<string, double> comps = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
Dictionary<string, double> ammo = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
Dictionary<string, string> mb1Modes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
Dictionary<string, int> unloadAuth = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
List<DockedVessel> dockedVessels = new List<DockedVessel>();
List<ServiceLine> mb1Needs = new List<ServiceLine>();
List<ServiceLine> mb1Surplus = new List<ServiceLine>();

class ConsoleRig
{
    public IMyTerminalBlock Block;
    public List<IMyTextSurface> Surfaces = new List<IMyTextSurface>();
    public int LastDrawTick = -999999;
    public bool WasOccupied = false;
}

List<ConsoleRig> consoleRigs = new List<ConsoleRig>();
IMyProgrammableBlock workerPB = null;
IMyProgrammableBlock prodPB = null;
IMyProgrammableBlock mb1PB1 = null;
string workerState = "OFFLINE";
string workerFault = "";
bool workerRefineryAutoBlocked = false;
string workerRefineryAutoBlockReason = "";
string workerRefiningEffective = "";
string mb1PacketState = "OFFLINE";
int mb1Seq = -1;
string mb1BqsTag = "AUTO";
int mb1BqsSeq = -1;
string mb1BqsSrc = "MB1";
int bqsLocalSeq = 0;
int bqsClusterSeq = 0;
string bqsClusterTag = "AUTO";
string bqsClusterSrc = "OB1";
int tick = 0;
int seq = 0;
int editHold = 0;
int page = 0;
int field = 0;
int stepIndex = 1;
bool discovered = false;
int renderTick = 0;
int activeConsoleIndex = -1;
bool consoleDirty = true;
int focusIndex = -1; // -1 = console; >=0 = explicit [IMS-DETAIL#] surface index.
string consoleLine = "Console ?";
bool legacyOfficerData = false;
int groupRegroupSeq = 0;
int groupRegroupAckSeq = 0;
int groupRegroupSentTick = -9999;
int groupRegroupAckTick = -9999;
bool batteryDamaged = false;
int batteryGoodCount = 0;
int batteryBadCount = 0;

// Item-aware STEP banks. Discrete items are whole counts; bulk materials use compact k/M formatting; percent fields always show %.
double[] stepPct = new double[] { 1, 5, 10 };
double[] stepAmmo = new double[] { 10, 100, 1000 };
double[] stepIce = new double[] { 10000, 100000, 1000000 };
double[] stepUranium = new double[] { 10, 100, 1000 };
double[] stepSteel = new double[] { 100, 1000, 10000 };
double[] stepCommon = new double[] { 50, 250, 1000 };
double[] stepLowComp = new double[] { 10, 100, 1000 };
double[] stepIron = new double[] { 1000, 10000, 100000 };
double[] stepMidRef = new double[] { 500, 5000, 50000 };
double[] stepMgSilver = new double[] { 100, 1000, 10000 };
double[] stepGoldPlat = new double[] { 50, 500, 5000 };
double[] stepGravel = new double[] { 1000, 10000, 100000 };
double[] stepLcd = new double[] { 1, 5, 10 };

string[] pages = new string[] { "SUPPLY", "AMMO", "COMPONENTS", "REFINED", "ASSEMBLY", "REFINERY", "DOCKED VESSELS", "SYSTEM", "MB1 SERVICE" };
int[] pageKind = new int[] { TARGET_PAGE, TARGET_PAGE, TARGET_PAGE, TARGET_PAGE, MODE_PAGE, MODE_PAGE, AUTH_PAGE, MODE_PAGE, READONLY_PAGE };
string[][] items = new string[][]
{
    new string[] { "ICE", "URANIUM", "BATTERY", "H2", "O2" },
    new string[] { "GATLING BOX", "RIFLE MAG", "MISSILES", "ARTILLERY", "ASSAULT", "AUTOCANNON", "RAILGUN" },
    new string[] { "STEEL PLATE", "INT. PLATE", "CONST. COMP", "MOTOR", "COMPUTER", "DISPLAY", "GIRDER", "SMALL TUBE", "LARGE TUBE", "METAL GRID", "B. GLASS", "MEDICAL", "DETECTOR", "RADIO-COMM", "POWER CELL", "REACTOR COMP", "SUPERCOND.", "THRUSTER COMP" },
    new string[] { "IRON", "NICKEL", "COBALT", "SILICON", "MAGNESIUM", "SILVER", "GOLD", "PLATINUM", "GRAVEL" },
    new string[] { "ASSEMBLY", "COMPONENTS", "AMMO", "TOOLS" },
    new string[] { "REFINING", "ICE PROCESSING", "INGOT CLEARING", "ORE UNLOAD" },
    new string[] { "DOCKED" },
    new string[] { "DISTRIBUTION METHOD", "REGROUP AUTO", "COMPONENT STORAGE", "WORKER MODE", "BUILD QUEUE SOURCE", "SHOW GRAVEL", "LCD ROTATION", "CORE EXPORT" },
    new string[] { "MB1 NEEDS", "MB1 SURPLUS", "MB1 MODES", "PACKET" }
};

double[][] mins = new double[][]
{
    new double[] { 250000, 100, 30, 70, 40 },
    new double[] { 25, 250, 25, 50, 50, 50, 10 },
    new double[] { 500, 500, 500, 200, 200, 50, 200, 500, 500, 200, 100, 25, 10, 10, 100, 25, 25, 50 },
    new double[] { 500000, 75000, 50000, 75000, 25000, 10000, 5000, 1000, 50000 },
    new double[] { 0, 0, 0, 0 },
    new double[] { 0, 0, 0, 0 },
    new double[] { 0 },
    new double[] { 0, 0, 0, 0, 0, 1, 5, 1 },
    new double[] { 0, 0, 0, 0 }
};

double[][] maxs = new double[][]
{
    new double[] { 1000000, 1000, 100, 100, 100 },
    new double[] { 500, 2000, 500, 500, 500, 500, 100 },
    new double[] { 2000, 2000, 2000, 1000, 1000, 500, 1000, 2000, 2000, 1000, 500, 100, 50, 50, 500, 100, 100, 250 },
    new double[] { 2000000, 300000, 200000, 300000, 100000, 50000, 25000, 10000, 500000 },
    new double[] { 1, 2, 2, 2 },
    new double[] { 2, 2, 1, 1 },
    new double[] { 0 },
    new double[] { 1, 1, 1, 1, 2, 1, 600, 1 },
    new double[] { 0, 0, 0, 0 }
};

double[][] cur = new double[][]
{
    new double[] { 0, 0, 0, 0, 0 },
    new double[] { 0, 0, 0, 0, 0, 0, 0 },
    new double[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
    new double[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 },
    new double[] { 0, 0, 0, 0 },
    new double[] { 2, 2, 1, 1 },
    new double[] { 0 },
    new double[] { 1, 0, 0, 0, 0, 1, 5, 1 },
    new double[] { 0, 0, 0, 0 }
};

string lastScan = "INIT";
string cargoLine = "cargo ?";
string gasLine = "gas ?";
string mb1Line = "MB1 packet offline";


class DockedVessel
{
    public string Key;
    public string Name;
    public string Dock;
    public string WorkerState;
}

class ServiceLine
{
    public string Page;
    public string Item;
    public double Min;
    public double Max;
    public double Cur;
    public double Need;
    public double Surplus;
}

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    LoadAuthLedger();
    LoadOfficerData();
    Discover();
    ScanLive();
    consoleDirty = true;
    DrawAll();
    ExportPackets();
}

public void Main(string argument, UpdateType updateSource)
{
    string arg = argument == null ? "" : argument.Trim().ToUpper();
    if (arg == "RESET" || arg == "SCAN" || arg == "DISCOVER")
    {
        Discover();
        ScanLive();
        consoleDirty = true;
        DrawAll();
        ExportPackets();
        return;
    }
    if (arg == "STATUS")
    {
        ScanLive();
        consoleDirty = true;
        DrawAll();
        ExportPackets();
        EchoStatus();
        return;
    }
    if (arg == "PB2 LABEL" || arg == "LABEL PB2" || arg == "RESTORE PB2 FACE")
    {
        Echo("PB2 LABEL disabled in V011: PB1 no longer writes programmable-block faces.");
        return;
    }
    if (HandleCommand(arg))
    {
        ScanLive();
        consoleDirty = true;
        DrawAll();
        ExportPackets();
        return;
    }
    tick++;
    renderTick++;
    if (editHold > 0) editHold--;
    if (!discovered || tick >= SCAN_TICKS)
    {
        tick = 0;
        Discover();
        ScanLive();
        consoleDirty = true;
        DrawAll();
        ExportPackets();
    }
    else if ((renderTick % 3) == 0)
    {
        DrawConsoles(false);
    }
}

bool HandleCommand(string arg)
{
    if (arg.StartsWith("IMS ")) arg = arg.Substring(4).Trim();
    if (arg == "FOCUS" || arg == "LCD")
    {
        CycleFocusTarget();
        SaveOfficerData();
        return true;
    }
    if (arg == "PAGE" || arg == "PAGE NEXT" || arg == "PAGENEXT")
    {
        page++;
        if (page >= pages.Length) page = 0;
        ClampField();
        SaveOfficerData();
        return true;
    }
    if (arg == "PAGE PREV" || arg == "PAGEPREV")
    {
        page--;
        if (page < 0) page = pages.Length - 1;
        ClampField();
        SaveOfficerData();
        return true;
    }
    if (arg == "NEXT" || arg == "RIGHT")
    {
        field++;
        if (field >= FieldCount()) field = 0;
        SaveOfficerData();
        return true;
    }
    if (arg == "PREV" || arg == "LEFT")
    {
        field--;
        if (field < 0) field = FieldCount() - 1;
        SaveOfficerData();
        return true;
    }
    if (arg == "STEP" || arg == "SCALE")
    {
        stepIndex++;
        if (stepIndex >= ActiveStepCount()) stepIndex = 0;
        SaveOfficerData();
        return true;
    }
    if (arg == "INC" || arg == "UP" || arg == "+")
    {
        Adjust(1);
        SaveOfficerData();
        return true;
    }
    if (arg == "DEC" || arg == "DOWN" || arg == "-")
    {
        Adjust(-1);
        SaveOfficerData();
        return true;
    }
    return false;
}

void CycleFocusTarget()
{
    if (auxSurfaces.Count <= 0)
    {
        focusIndex = -1;
        return;
    }
    focusIndex++;
    if (focusIndex >= auxSurfaces.Count) focusIndex = -1;
}

string FocusName()
{
    if (focusIndex < 0) return "CONSOLE";
    return "DETAIL " + (focusIndex + 1).ToString("0");
}

void Adjust(int dir)
{
    if (pageKind[page] == READONLY_PAGE) return;
    if (pageKind[page] == AUTH_PAGE) { AdjustDockAuth(); return; }
    ClampField();
    int item = SelectedItem();
    if (pageKind[page] == MODE_PAGE)
    {
        if (pages[page] == "SYSTEM" && items[page][item] == "REGROUP AUTO")
        {
            groupRegroupSeq++;
            if (groupRegroupSeq > 999999) groupRegroupSeq = 1;
            groupRegroupSentTick = renderTick;
            groupRegroupAckTick = -9999;
            return;
        }
        if (pages[page] == "SYSTEM" && items[page][item] == "LCD ROTATION")
        {
            cur[page][item] += CurrentStep() * dir;
            int hi = ModeMax(page, item);
            if (cur[page][item] > hi) cur[page][item] = 0;
            if (cur[page][item] < 0) cur[page][item] = hi;
        }
        else
        {
            cur[page][item] += dir;
            int hi = ModeMax(page, item);
            if (cur[page][item] > hi) cur[page][item] = 0;
            if (cur[page][item] < 0) cur[page][item] = hi;
        }
        if (pages[page] == "SYSTEM" && items[page][item] == "BUILD QUEUE SOURCE") BqsLocalChanged();
        MarkProductionEditHold(page, item);
        return;
    }
    double step = CurrentStep();
    if (SelectedIsMax()) maxs[page][item] += step * dir;
    else mins[page][item] += step * dir;
    ClampTargetPair(page, item);
    MarkProductionEditHold(page, item);
}

void MarkProductionEditHold(int p, int item)
{
    if (p < 0 || p >= pages.Length) return;
    string pg = pages[p];
    if (pg == "COMPONENTS" || pg == "AMMO" || pg == "ASSEMBLY") editHold = EDIT_HOLD_TICKS;
    else if (pg == "SYSTEM" && item >= 0 && item < items[p].Length && (items[p][item] == "WORKER MODE" || items[p][item] == "BUILD QUEUE SOURCE")) editHold = EDIT_HOLD_TICKS;
}

string EditHoldGlyph()
{
    if (editHold <= 0) return "";
    string[] g = new string[] { "|", "/", "-", "\\" };
    int sec = (editHold + 5) / 6;
    if (sec < 1) sec = 1;
    return g[(renderTick / 2) % g.Length] + sec.ToString("0");
}

int ActiveStepCount()
{
    return StepBank(page, SelectedItem()).Length;
}

double CurrentStep()
{
    double[] a = StepBank(page, SelectedItem());
    int i = stepIndex;
    if (i < 0) i = 0;
    if (i >= a.Length) i = a.Length - 1;
    return a[i];
}

double[] StepBank(int p, int item)
{
    if (IsPercentField(p, item)) return stepPct;
    if (p < 0 || p >= pages.Length || item < 0 || item >= items[p].Length) return stepCommon;
    string pg = pages[p];
    string n = items[p][item];
    if (pg == "SUPPLY")
    {
        if (n == "ICE") return stepIce;
        if (n == "URANIUM") return stepUranium;
    }
    if (pg == "AMMO") return stepAmmo;
    if (pg == "COMPONENTS")
    {
        if (n == "STEEL PLATE") return stepSteel;
        if (n == "MEDICAL" || n == "DETECTOR" || n == "RADIO-COMM" || n == "REACTOR COMP" || n == "SUPERCOND." || n == "THRUSTER COMP") return stepLowComp;
        return stepCommon;
    }
    if (pg == "REFINED")
    {
        if (n == "IRON") return stepIron;
        if (n == "NICKEL" || n == "COBALT" || n == "SILICON") return stepMidRef;
        if (n == "MAGNESIUM" || n == "SILVER") return stepMgSilver;
        if (n == "GOLD" || n == "PLATINUM") return stepGoldPlat;
        if (n == "GRAVEL") return stepGravel;
    }
    if (pg == "SYSTEM" && n == "LCD ROTATION") return stepLcd;
    return stepCommon;
}

int ModeMax(int p, int item)
{
    if (p >= 0 && p < pages.Length && pages[p] == "SYSTEM" && item >= 0 && item < items[p].Length && items[p][item] == "REGROUP AUTO") return 0;
    if (p < 0 || p >= maxs.Length || item < 0 || item >= maxs[p].Length) return 0;
    int hi = (int)Math.Round(maxs[p][item]);
    if (hi < 0) hi = 0;
    return hi;
}

bool IsPercentField(int p, int item)
{
    if (p != 0 || item < 0 || item >= items[p].Length) return false;
    string n = items[p][item];
    return n == "BATTERY" || n == "H2" || n == "O2";
}

void ClampTargetPair(int p, int item)
{
    if (p < 0 || p >= mins.Length || item < 0 || item >= mins[p].Length) return;
    if (mins[p][item] < 0) mins[p][item] = 0;
    if (maxs[p][item] < 0) maxs[p][item] = 0;
    if (IsPercentField(p, item))
    {
        if (mins[p][item] > 100) mins[p][item] = 100;
        if (maxs[p][item] > 100) maxs[p][item] = 100;
    }
    if (mins[p][item] > maxs[p][item])
    {
        if (SelectedIsMax()) mins[p][item] = maxs[p][item];
        else maxs[p][item] = mins[p][item];
    }
}

void NormalizeTargetPair(int p, int item)
{
    if (p < 0 || p >= mins.Length || item < 0 || item >= mins[p].Length) return;
    if (mins[p][item] < 0) mins[p][item] = 0;
    if (maxs[p][item] < 0) maxs[p][item] = 0;
    if (IsPercentField(p, item))
    {
        if (mins[p][item] > 100) mins[p][item] = 100;
        if (maxs[p][item] > 100) maxs[p][item] = 100;
    }
    if (mins[p][item] > maxs[p][item]) maxs[p][item] = mins[p][item];
}

void NormalizeAllTargets()
{
    for (int p = 0; p < pages.Length; p++)
        if (pageKind[p] == TARGET_PAGE)
            for (int i = 0; i < items[p].Length; i++) NormalizeTargetPair(p, i);
}

int FieldCount()
{
    if (page < 0 || page >= items.Length) return 1;
    if (pageKind[page] == AUTH_PAGE) return dockedVessels.Count > 0 ? dockedVessels.Count * 2 : 1;
    if (pageKind[page] == TARGET_PAGE) return items[page].Length * 2;
    return items[page].Length;
}

void ClampField()
{
    int max = FieldCount();
    if (max <= 0) { field = 0; return; }
    if (field < 0) field = 0;
    if (field >= max) field = max - 1;
}

int SelectedItem()
{
    if (pageKind[page] == TARGET_PAGE || pageKind[page] == AUTH_PAGE) return field / 2;
    return field;
}

bool SelectedIsMax()
{
    return pageKind[page] == TARGET_PAGE && (field % 2) == 1;
}

void Discover()
{
    tagged.Clear(); inventoryBlocks.Clear(); managedCargo.Clear(); gasTanks.Clear(); batteries.Clear(); auxSurfaces.Clear(); consoleRigs.Clear();
    workerPB = null; prodPB = null; mb1PB1 = null; activeConsoleIndex = -1;
    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(tagged, b => b != null && HasTag(b, INSTALL_TAG));
    for (int i = 0; i < tagged.Count; i++)
    {
        IMyTerminalBlock b = tagged[i];
        if (b == null) continue;
        if (b.HasInventory) inventoryBlocks.Add(b);
        IMyCargoContainer cargo = b as IMyCargoContainer;
        if (cargo != null && CargoCapacityL(cargo) >= LARGE_CARGO_MIN_L) managedCargo.Add(cargo);
        IMyGasTank tank = b as IMyGasTank;
        if (tank != null) gasTanks.Add(tank);
        IMyBatteryBlock bat = b as IMyBatteryBlock;
        if (bat != null) batteries.Add(bat);
        IMyProgrammableBlock pb = b as IMyProgrammableBlock;
        if (pb != null && pb != Me)
        {
            if ((HasTag(pb, WORKER_TAG) || HasTag(pb, PB2_TAG)) && workerPB == null) workerPB = pb;
            if ((HasTag(pb, PROD_TAG) || HasTag(pb, PB3_TAG)) && prodPB == null) prodPB = pb;
        }
        if (IsConsoleCandidate(b)) AddConsoleRig(b);
        // Strict ownership: do not claim generic [IMS] displays here.
        // Detail LCDs must use explicit role tags such as [IMS-DETAIL1]. PB faces are never display targets.
        if (!(b is IMyProgrammableBlock) && HasDetailTag(b))
        {
            IMyTextPanel tp = b as IMyTextPanel;
            if (tp != null) { Prepare(tp); auxSurfaces.Add(tp); }
            else
            {
                IMyTextSurfaceProvider sp = b as IMyTextSurfaceProvider;
                if (sp != null)
                {
                    for (int si = 0; si < sp.SurfaceCount; si++)
                    {
                        IMyTextSurface ds = sp.GetSurface(si);
                        if (ds == null) continue;
                        Prepare(ds);
                        auxSurfaces.Add(ds);
                    }
                }
            }
        }
    }
    List<IMyProgrammableBlock> pbs = new List<IMyProgrammableBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>(pbs);
    for (int i = 0; i < pbs.Count; i++)
    {
        IMyProgrammableBlock pb = pbs[i];
        if (pb == null || pb == Me) continue;
        if (pb.CustomData != null && pb.CustomData.IndexOf(MB1_SERVICE_BEGIN, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            mb1PB1 = pb;
            break;
        }
    }
    PickActiveConsole();
    consoleDirty = true;
    discovered = true;
}

bool IsConsoleCandidate(IMyTerminalBlock b)
{
    if (b == null) return false;
    if (!(b is IMyShipController)) return false;
    if (!HasTag(b, DISPLAY_TAG)) return false;
    IMyTextSurfaceProvider p = b as IMyTextSurfaceProvider;
    if (p == null || p.SurfaceCount <= 0) return false;
    return true;
}

void AddConsoleRig(IMyTerminalBlock b)
{
    for (int i = 0; i < consoleRigs.Count; i++)
        if (consoleRigs[i].Block == b) return;
    ConsoleRig rig = new ConsoleRig();
    rig.Block = b;
    AddSurfaces(b, rig.Surfaces);
    if (rig.Surfaces.Count > 0) consoleRigs.Add(rig);
}

bool IsOccupied(IMyTerminalBlock b)
{
    IMyShipController c = b as IMyShipController;
    return c != null && c.IsUnderControl;
}

void PickActiveConsole()
{
    int occupied = -1;
    int occupiedCount = 0;
    for (int i = 0; i < consoleRigs.Count; i++)
    {
        if (IsOccupied(consoleRigs[i].Block))
        {
            if (occupied < 0) occupied = i;
            occupiedCount++;
        }
    }
    if (occupied >= 0) activeConsoleIndex = occupied;
    else if (activeConsoleIndex < 0 || activeConsoleIndex >= consoleRigs.Count) activeConsoleIndex = consoleRigs.Count > 0 ? 0 : -1;

    if (consoleRigs.Count == 0) consoleLine = "Console FAULT: no [OB1] [IMS] control seats";
    else consoleLine = "Consoles " + consoleRigs.Count + " active=" + (activeConsoleIndex + 1) + (occupiedCount > 1 ? " MULTI-OCCUPIED" : (occupiedCount == 1 ? " occupied" : " standby"));
}

void AddSurfaces(IMyTerminalBlock b, List<IMyTextSurface> list)
{
    IMyTextSurfaceProvider p = b as IMyTextSurfaceProvider;
    if (p == null) return;
    for (int i = 0; i < p.SurfaceCount; i++)
    {
        IMyTextSurface s = p.GetSurface(i);
        if (s == null) continue;
        Prepare(s);
        list.Add(s);
    }
}

void Prepare(IMyTextSurface s)
{
    // User-facing LCD setup required for cockpit/seat custom sprite UI:
    // Content = Apps, App = None. In PB API terms this is SCRIPT content
    // with an empty selected script/app, then DrawFrame supplies our sprites.
    s.ContentType = ContentType.SCRIPT;
    s.Script = "";
    s.ScriptBackgroundColor = C_BG;
    s.ScriptForegroundColor = C_TEXT;
}

void ScanLive()
{
    ingots.Clear(); ores.Clear(); comps.Clear(); ammo.Clear();
    double cargoCur = 0, cargoMax = 0;
    for (int i = 0; i < managedCargo.Count; i++)
    {
        IMyCargoContainer c = managedCargo[i];
        if (c == null || c.Closed) continue;
        IMyInventory inv = c.GetInventory(0);
        if (inv == null) continue;
        cargoCur += (double)inv.CurrentVolume * 1000.0;
        cargoMax += (double)inv.MaxVolume * 1000.0;
    }
    for (int i = 0; i < inventoryBlocks.Count; i++)
    {
        IMyTerminalBlock b = inventoryBlocks[i];
        if (b == null || b.Closed) continue;
        for (int n = 0; n < b.InventoryCount; n++)
        {
            IMyInventory inv = b.GetInventory(n);
            if (inv == null) continue;
            itemScratch.Clear();
            inv.GetItems(itemScratch);
            for (int j = 0; j < itemScratch.Count; j++) AddItem(itemScratch[j]);
        }
    }
    double h2 = 0, h2n = 0, o2 = 0, o2n = 0;
    for (int i = 0; i < gasTanks.Count; i++)
    {
        IMyGasTank t = gasTanks[i];
        if (t == null || t.Closed) continue;
        string name = t.BlockDefinition.SubtypeName;
        if (name.IndexOf("Hydrogen", StringComparison.OrdinalIgnoreCase) >= 0) { h2 += t.FilledRatio; h2n++; }
        else { o2 += t.FilledRatio; o2n++; }
    }
    double battCur = 0, battMax = 0;
    batteryGoodCount = 0;
    batteryBadCount = 0;
    for (int i = 0; i < batteries.Count; i++)
    {
        IMyBatteryBlock b = batteries[i];
        if (b == null || b.Closed) continue;
        if (b.IsFunctional && b.MaxStoredPower > 0)
        {
            battCur += b.CurrentStoredPower;
            battMax += b.MaxStoredPower;
            batteryGoodCount++;
        }
        else batteryBadCount++;
    }
    batteryDamaged = batteryBadCount > 0;
    cur[0][0] = Map(ores, "Ice");
    cur[0][1] = Map(ingots, "Uranium");
    cur[0][2] = battMax > 0 ? (battCur / battMax) * 100.0 : 0;
    cur[0][3] = h2n > 0 ? (h2 / h2n) * 100.0 : 0;
    cur[0][4] = o2n > 0 ? (o2 / o2n) * 100.0 : 0;
    cur[1][0] = Map(ammo, "NATO_25x184mm"); cur[1][1] = Map(ammo, "RapidFireAutomaticRifleGun_Mag_50rd"); cur[1][2] = Map(ammo, "Missile200mm");
    cur[1][3] = Map(ammo, "LargeCalibreAmmo"); cur[1][4] = Map(ammo, "MediumCalibreAmmo"); cur[1][5] = Map(ammo, "AutocannonClip"); cur[1][6] = Map(ammo, "LargeRailgunAmmo") + Map(ammo, "SmallRailgunAmmo");
    cur[2][0] = Map(comps, "SteelPlate"); cur[2][1] = Map(comps, "InteriorPlate"); cur[2][2] = Map(comps, "Construction"); cur[2][3] = Map(comps, "Motor");
    cur[2][4] = Map(comps, "Computer"); cur[2][5] = Map(comps, "Display"); cur[2][6] = Map(comps, "Girder"); cur[2][7] = Map(comps, "SmallTube");
    cur[2][8] = Map(comps, "LargeTube"); cur[2][9] = Map(comps, "MetalGrid"); cur[2][10] = Map(comps, "BulletproofGlass"); cur[2][11] = Map(comps, "Medical");
    cur[2][12] = Map(comps, "Detector"); cur[2][13] = Map(comps, "RadioCommunication"); cur[2][14] = Map(comps, "PowerCell"); cur[2][15] = Map(comps, "Reactor");
    cur[2][16] = Map(comps, "Superconductor"); cur[2][17] = Map(comps, "Thrust");
    cur[3][0] = Map(ingots, "Iron"); cur[3][1] = Map(ingots, "Nickel"); cur[3][2] = Map(ingots, "Cobalt");
    cur[3][3] = Map(ingots, "Silicon"); cur[3][4] = Map(ingots, "Magnesium"); cur[3][5] = Map(ingots, "Silver");
    cur[3][6] = Map(ingots, "Gold"); cur[3][7] = Map(ingots, "Platinum"); cur[3][8] = Map(ingots, "Stone");
    cargoLine = "Managed cargo " + managedCargo.Count + "x " + Format(cargoCur) + "L / " + Format(cargoMax) + "L " + (cargoMax > 0 ? ((cargoCur / cargoMax) * 100.0).ToString("0.0") : "0.0") + "%";
    gasLine = "H2 " + cur[0][3].ToString("0.0") + "%  O2 " + cur[0][4].ToString("0.0") + "%  Batt " + cur[0][2].ToString("0.0") + "%" + (batteryDamaged ? " DAMAGED " + batteryBadCount.ToString("0") : "");
    ImportWorkerStatus();
    ImportMb1ServicePacket();
    UpdateBuildQueueCluster();
    lastScan = "blocks=" + tagged.Count + " inv=" + inventoryBlocks.Count + " cargo=" + managedCargo.Count + " tanks=" + gasTanks.Count;
}

void AddItem(MyInventoryItem it)
{
    string typeId = it.Type.TypeId.ToString();
    string sub = it.Type.SubtypeId.ToString();
    double amount = (double)it.Amount;
    if (typeId.IndexOf("Ore", StringComparison.OrdinalIgnoreCase) >= 0) Add(ores, sub, amount);
    else if (typeId.IndexOf("Ingot", StringComparison.OrdinalIgnoreCase) >= 0) Add(ingots, sub, amount);
    else if (typeId.IndexOf("Component", StringComparison.OrdinalIgnoreCase) >= 0) Add(comps, sub, amount);
    else if (typeId.IndexOf("AmmoMagazine", StringComparison.OrdinalIgnoreCase) >= 0) Add(ammo, sub, amount);
}

void Add(Dictionary<string, double> d, string k, double v)
{
    double old;
    if (d.TryGetValue(k, out old)) d[k] = old + v;
    else d.Add(k, v);
}

double Map(Dictionary<string, double> d, string key)
{
    double v;
    return d.TryGetValue(key, out v) ? v : 0;
}

void ImportWorkerStatus()
{
    workerState = workerPB == null ? "OFFLINE" : "ONLINE";
    workerFault = "";
    workerRefineryAutoBlocked = false;
    workerRefineryAutoBlockReason = "";
    workerRefiningEffective = "";
    if (workerPB == null) return;
    string block = ExtractDataBlock(workerPB.CustomData, WORKER_STATUS_BEGIN, WORKER_STATUS_END);
    if (block.Length == 0) return;
    string[] lines = block.Split('\n');
    for (int i = 0; i < lines.Length; i++)
    {
        string line = lines[i].Trim();
        int eq = line.IndexOf('=');
        if (eq <= 0) continue;
        string k = line.Substring(0, eq).Trim();
        string v = line.Substring(eq + 1).Trim();
        if (k.Equals("State", StringComparison.OrdinalIgnoreCase)) workerState = v;
        else if (k.Equals("Fault", StringComparison.OrdinalIgnoreCase)) workerFault = v;
        else if (k.Equals("RefiningEffective", StringComparison.OrdinalIgnoreCase))
        {
            workerRefiningEffective = v;
            if (v.IndexOf("AUTO_BLOCKED", StringComparison.OrdinalIgnoreCase) >= 0) workerRefineryAutoBlocked = true;
        }
        else if (k.Equals("RefineryAutoBlocked", StringComparison.OrdinalIgnoreCase)) workerRefineryAutoBlocked = IsTrue(v);
        else if (k.Equals("RefineryAutoBlockReason", StringComparison.OrdinalIgnoreCase)) workerRefineryAutoBlockReason = v;
        else if (k.Equals("GroupRegroupAckSeq", StringComparison.OrdinalIgnoreCase))
        {
            int ack;
            if (int.TryParse(v, out ack))
            {
                if (ack > groupRegroupAckSeq) groupRegroupAckTick = renderTick;
                groupRegroupAckSeq = ack;
            }
        }
    }
    ImportDockedVessels();
}


void ImportDockedVessels()
{
    dockedVessels.Clear();
    if (workerPB == null) return;
    string block = ExtractDataBlock(workerPB.CustomData, DOCKED_BEGIN, DOCKED_END);
    if (block.Length == 0) return;
    string[] lines = block.Split('\n');
    for (int i = 0; i < lines.Length; i++)
    {
        string line = lines[i].Trim();
        if (line.Length == 0 || line.StartsWith("#")) continue;
        string[] p = line.Split('|');
        if (p.Length < 5 || !p[0].Equals("DOCK", StringComparison.OrdinalIgnoreCase)) continue;
        DockedVessel d = new DockedVessel();
        d.Key = p[1];
        d.Name = p[2];
        d.Dock = p[3];
        d.WorkerState = p[4];
        dockedVessels.Add(d);
    }
    if (pageKind[page] == AUTH_PAGE) ClampField();
}

void LoadAuthLedger()
{
    unloadAuth.Clear();
    string block = ExtractDataBlock(Me.CustomData, UNLOAD_AUTH_BEGIN, UNLOAD_AUTH_END);
    if (block.Length == 0) return;
    string[] lines = block.Split('\n');
    for (int i = 0; i < lines.Length; i++)
    {
        string line = lines[i].Trim();
        if (line.Length == 0 || line.StartsWith("#")) continue;
        string[] p = line.Split('|');
        if (p.Length < 3 || !p[0].Equals("AUTH", StringComparison.OrdinalIgnoreCase)) continue;
        int st = p[2].Equals("AUTH", StringComparison.OrdinalIgnoreCase) ? 1 : (p[2].Equals("DENY", StringComparison.OrdinalIgnoreCase) ? -1 : 0);
        if (st != 0) unloadAuth[p[1]] = st;
    }
}

void SaveAuthLedger()
{
    StringBuilder sb = new StringBuilder();
    sb.AppendLine(UNLOAD_AUTH_BEGIN);
    foreach (KeyValuePair<string, int> kv in unloadAuth)
        sb.AppendLine("AUTH|" + kv.Key + "|" + (kv.Value > 0 ? "AUTH" : "DENY"));
    sb.AppendLine(UNLOAD_AUTH_END);
    Me.CustomData = ReplaceDataBlock(Me.CustomData, UNLOAD_AUTH_BEGIN, UNLOAD_AUTH_END, sb.ToString());
}

void ImportMb1ServicePacket()
{
    mb1Needs.Clear(); mb1Surplus.Clear(); mb1Modes.Clear();
    mb1PacketState = "OFFLINE";
    mb1Seq = -1;
    if (mb1PB1 == null) { mb1Line = "MB1 packet offline"; return; }
    string block = ExtractDataBlock(mb1PB1.CustomData, MB1_SERVICE_BEGIN, MB1_SERVICE_END);
    if (block.Length == 0) { mb1Line = "MB1 packet missing"; return; }
    mb1PacketState = "ONLINE";
    string[] lines = block.Split('\n');
    for (int i = 0; i < lines.Length; i++)
    {
        string line = lines[i].Trim();
        if (line.Length == 0) continue;
        int eq = line.IndexOf('=');
        if (eq > 0)
        {
            string k = line.Substring(0, eq).Trim();
            string v = line.Substring(eq + 1).Trim();
            int n;
            if (k.Equals("Seq", StringComparison.OrdinalIgnoreCase) && int.TryParse(v, out n)) mb1Seq = n;
            else if (k.Equals("BuildQueue.SourceTag", StringComparison.OrdinalIgnoreCase)) mb1BqsTag = v;
            else if (k.Equals("BuildQueue.SourceSeq", StringComparison.OrdinalIgnoreCase) && int.TryParse(v, out n)) mb1BqsSeq = n;
            else if (k.Equals("BuildQueue.SourceEntity", StringComparison.OrdinalIgnoreCase)) mb1BqsSrc = v;
            else if (k.Equals("BuildQueue.ClusterTag", StringComparison.OrdinalIgnoreCase)) mb1BqsTag = v;
            else if (k.Equals("BuildQueue.ClusterSeq", StringComparison.OrdinalIgnoreCase) && int.TryParse(v, out n)) mb1BqsSeq = n;
            else if (k.Equals("BuildQueue.ClusterSource", StringComparison.OrdinalIgnoreCase)) mb1BqsSrc = v;
            continue;
        }
        string[] p = line.Split('|');
        if (p.Length < 5) continue;
        if (p[0].Equals("MODE", StringComparison.OrdinalIgnoreCase) && p.Length >= 5)
        {
            string key = p[1] + "." + p[2];
            if (mb1Modes.ContainsKey(key)) mb1Modes[key] = p[4]; else mb1Modes.Add(key, p[4]);
        }
        else if (p[0].Equals("TGT", StringComparison.OrdinalIgnoreCase))
        {
            ServiceLine s = ParseServiceLine(p);
            if (s == null) continue;
            if (s.Need > 0) mb1Needs.Add(s);
            if (s.Surplus > 0) mb1Surplus.Add(s);
        }
    }
    SortService(mb1Needs, true);
    SortService(mb1Surplus, false);
    mb1Line = "MB1 " + mb1PacketState + " seq=" + mb1Seq + " BQS=" + mb1BqsTag + "#" + mb1BqsSeq + " need=" + mb1Needs.Count + " surplus=" + mb1Surplus.Count;
}

ServiceLine ParseServiceLine(string[] p)
{
    ServiceLine s = new ServiceLine();
    s.Page = p[1]; s.Item = p[2];
    for (int i = 3; i + 1 < p.Length; i += 2)
    {
        double v;
        if (!double.TryParse(p[i + 1], out v)) v = 0;
        if (p[i].Equals("min", StringComparison.OrdinalIgnoreCase)) s.Min = v;
        else if (p[i].Equals("max", StringComparison.OrdinalIgnoreCase)) s.Max = v;
        else if (p[i].Equals("cur", StringComparison.OrdinalIgnoreCase)) s.Cur = v;
        else if (p[i].Equals("need", StringComparison.OrdinalIgnoreCase)) s.Need = v;
        else if (p[i].Equals("surplus", StringComparison.OrdinalIgnoreCase)) s.Surplus = v;
    }
    return s;
}

void SortService(List<ServiceLine> list, bool need)
{
    for (int i = 0; i < list.Count - 1; i++)
    {
        int best = i;
        for (int j = i + 1; j < list.Count; j++)
        {
            double a = need ? list[j].Need : list[j].Surplus;
            double b = need ? list[best].Need : list[best].Surplus;
            if (a > b) best = j;
        }
        if (best == i) continue;
        ServiceLine t = list[i]; list[i] = list[best]; list[best] = t;
    }
}

void DrawAll()
{
    DrawConsoles(false);
    DrawDetailSurfaces();
    // V014: mirrored tagged seats; Apps/None surfaces; refinery page agreement applied.
    EchoStatus();
}

string BuildMainText()
{
    StringBuilder sb = new StringBuilder();
    sb.AppendLine("OB1 IMS PB1 V032");
    sb.AppendLine(consoleLine);
    sb.AppendLine(cargoLine);
    sb.AppendLine(gasLine);
    sb.AppendLine("Worker " + workerState + (workerFault.Length > 0 && workerFault != "NONE" ? " fault=" + workerFault : ""));
    UpdateBuildQueueCluster();
    sb.AppendLine("BQS cluster " + bqsClusterTag + " seq " + bqsClusterSeq + " src " + bqsClusterSrc);
    sb.AppendLine(mb1Line);
    sb.AppendLine("Page " + pages[page] + "  Field " + (field + 1) + "/" + FieldCount() + "  " + StepTiny());
    sb.AppendLine("--------------------------------");
    if (pageKind[page] == AUTH_PAGE) AppendDockedPage(sb);
    else if (pageKind[page] == 2) AppendMb1Page(sb);
    else
    {
        for (int i = 0; i < items[page].Length; i++)
        {
            int selItem = SelectedItem();
            string mark = i == selItem ? "> " : "  ";
            sb.Append(mark).Append(items[page][i]).Append(" ");
            if (pageKind[page] == 0)
            {
                sb.Append("cur ").Append(FormatValue(page, items[page][i], cur[page][i])).Append(SelectedIsMax() && i == selItem ? " min " : " MIN ").Append(FormatValue(page, items[page][i], mins[page][i])).Append(!SelectedIsMax() && i == selItem ? " max " : " MAX ").Append(FormatValue(page, items[page][i], maxs[page][i]));
            }
            else sb.Append(DisplayModeText(pages[page], items[page][i], cur[page][i]));
            sb.AppendLine();
        }
    }
    sb.AppendLine("--------------------------------");
    sb.AppendLine("Commands: PAGE/NEXT/PREV/INC/DEC/STEP/FOCUS");
    return sb.ToString();
}



void AppendDockedPage(StringBuilder sb)
{
    if (dockedVessels.Count == 0) { sb.AppendLine("No vessels docked at [UNLOAD] ports."); return; }
    for (int i = 0; i < dockedVessels.Count; i++)
    {
        DockedVessel d = dockedVessels[i];
        bool authSel = field == i * 2;
        bool denySel = field == i * 2 + 1;
        int st = GetAuthState(d.Key);
        sb.Append(authSel || denySel ? "> " : "  ");
        sb.Append(ShortDockName(d.Name)).Append(" dock ").Append(d.Dock).Append("  ");
        sb.Append(st > 0 ? "AUTH" : (st < 0 ? "DENY" : "PENDING"));
        sb.AppendLine();
    }
}

void AdjustDockAuth()
{
    if (dockedVessels.Count == 0) return;
    ClampField();
    int row = field / 2;
    if (row < 0 || row >= dockedVessels.Count) return;
    int value = (field % 2) == 0 ? 1 : -1;
    string key = dockedVessels[row].Key;
    if (key == null || key.Length == 0) return;
    unloadAuth[key] = value;
    SaveAuthLedger();
}

int GetAuthState(string key)
{
    if (key == null) return 0;
    int v;
    return unloadAuth.TryGetValue(key, out v) ? v : 0;
}

string ShortDockName(string name)
{
    if (name == null || name.Length == 0) return "UNKNOWN";
    if (name.Length > 18) return name.Substring(0, 18);
    return name;
}

void AppendMb1Page(StringBuilder sb)
{
    if (field == 0) AppendServiceList(sb, mb1Needs, true, 14);
    else if (field == 1) AppendServiceList(sb, mb1Surplus, false, 14);
    else if (field == 2)
    {
        int shown = 0;
        foreach (KeyValuePair<string, string> kv in mb1Modes)
        {
            sb.AppendLine(kv.Key + "=" + kv.Value);
            shown++;
            if (shown >= 14) break;
        }
        if (shown == 0) sb.AppendLine("No MB1 modes visible.");
    }
    else sb.AppendLine(mb1Line);
}



string BuildStationText()
{
    StringBuilder sb = new StringBuilder();
    sb.AppendLine("OB1 STATION STATUS");
    sb.AppendLine(lastScan);
    sb.AppendLine(consoleLine);
    sb.AppendLine(cargoLine);
    sb.AppendLine(gasLine);
    sb.AppendLine("Worker " + workerState + " Fault " + (workerFault.Length == 0 ? "NONE" : workerFault));
    int rp = PageIndex("REFINERY");
    int sp = PageIndex("SYSTEM");
    if (rp >= 0)
    {
        sb.AppendLine("Refining " + ModeText("REFINERY", "REFINING", cur[rp][0]));
        sb.AppendLine("Ore unload " + ModeText("REFINERY", "ORE UNLOAD", cur[rp][3]));
    }
    if (sp >= 0)
    {
        int wi = ItemIndex(sp, "WORKER MODE");
        if (wi >= 0) sb.AppendLine("Worker mode " + ModeText("SYSTEM", "WORKER MODE", cur[sp][wi]));
    }
    return sb.ToString();
}

void AppendServiceList(StringBuilder sb, List<ServiceLine> list, bool need, int max)
{
    int count = list.Count < max ? list.Count : max;
    for (int i = 0; i < count; i++)
    {
        ServiceLine s = list[i];
        sb.Append(s.Page).Append("/").Append(s.Item).Append(" ");
        sb.Append(need ? "need " : "surplus ").Append(Format(need ? s.Need : s.Surplus));
        sb.Append(" cur ").Append(Format(s.Cur)).AppendLine();
    }
    if (count == 0) sb.AppendLine("None.");
}

string StepTiny()
{
    int item = SelectedItem();
    if (page < 0 || page >= items.Length || item < 0 || item >= items[page].Length) return "";
    string n = items[page][item];
    if (pageKind[page] == AUTH_PAGE) return "";
    if (pageKind[page] == MODE_PAGE)
    {
        if (pages[page] == "SYSTEM" && n == "REGROUP AUTO") return "PRESS";
        if (pages[page] == "SYSTEM" && n == "LCD ROTATION") return "STEP " + CurrentStep().ToString("0") + "s";
        if (ModeMax(page, item) == 1) return "TOGGLE";
        return "MODE";
    }
    double st = CurrentStep();
    if (IsPercentField(page, item)) return "STEP " + st.ToString("0") + "%";
    return "STEP " + Format(st);
}

string ShortName(string name)
{
    if (name == "INT. PLATE") return "INT. PLATE";
    if (name == "CONST. COMP") return "CONST. COMP";
    if (name == "B. GLASS") return "B. GLASS";
    if (name == "REACTOR COMP") return "REACTOR";
    if (name == "THRUSTER COMP") return "THRUSTER";
    if (name == "SUPERCOND.") return "SUPERCOND.";
    if (name == "MAGNESIUM") return "MAG";
    if (name == "PLATINUM") return "PLAT";
    if (name == "ICE PROCESSING") return "ICE PROC";
    if (name == "INGOT CLEARING") return "INGOT CLEAR";
    if (name == "DISTRIBUTION METHOD") return "DISTRIB";
    if (name == "REGROUP AUTO") return "REGROUP";
    if (name == "COMPONENT STORAGE") return "COMP STORE";
    if (name == "WORKER MODE") return "WORKER MODE";
    if (name == "BUILD QUEUE SOURCE") return "BUILD QUEUE";
    if (name == "SHOW GRAVEL") return "SHOW GRAVEL";
    if (name == "LCD ROTATION") return "LCD ROTATE";
    return name;
}

string FormatValue(int p, string item, double value)
{
    if (p == 0 && (item == "H2" || item == "O2" || item == "BATTERY")) return value.ToString("0") + "%";
    if (p >= 0 && p < pageKind.Length && pageKind[p] == TARGET_PAGE) return Format(value);
    return value.ToString("0");
}

string ModeText(string p, string item, double v)
{
    int n = (int)Math.Round(v);
    if (item == "ASSEMBLY") return n > 0 ? "ON" : "AUTO";
    if (item == "REFINING") return n <= 0 ? "OFF" : (n == 1 ? "AUTO" : "OPTIMIZED");
    if (item == "ORE UNLOAD") return n > 0 ? "ON" : "OFF";
    if (item == "ICE PROCESSING") return n <= 0 ? "OFF" : (n == 1 ? "MIN" : "AUTO");
    if (item == "INGOT CLEARING") return n > 0 ? "ON" : "OFF";
    if (item == "DISTRIBUTION METHOD") return n > 0 ? "GROUPED" : "REDUNDANT";
    if (item == "REGROUP AUTO") return RegroupButtonText();
    if (item == "COMPONENT STORAGE") return n > 0 ? "SEPARATE" : "REST";
    if (item == "WORKER MODE") return n > 0 ? "PAUSE" : "AUTO";
    if (item == "BUILD QUEUE SOURCE") return n <= 0 ? "AUTO" : (n == 1 ? "LOCAL" : "MB1");
    if (item == "SHOW GRAVEL") return n > 0 ? "ON" : "OFF";
    if (item == "LCD ROTATION") return n <= 0 ? "OFF" : n.ToString("0") + "s";
    if (n <= 0) return "OFF";
    if (n == 1) return "MIN";
    return "MAX";
}

string DisplayModeText(string p, string item, double v)
{
    if (item == "BUILD QUEUE SOURCE") return BuildQueueDisplayText();
    return ModeText(p, item, v);
}

string BuildQueueDisplayText()
{
    UpdateBuildQueueCluster();
    if (bqsClusterTag.Length == 0 || bqsClusterTag == "AUTO") return "AUTO";
    if (bqsClusterTag == IMS_ENTITY_TAG) return "LOCAL";
    return bqsClusterTag;
}

string BuildQueueSourceText()
{
    int sp = PageIndex("SYSTEM");
    int bi = ItemIndex(sp, "BUILD QUEUE SOURCE");
    if (sp < 0 || bi < 0) return "AUTO";
    return ModeText("SYSTEM", "BUILD QUEUE SOURCE", cur[sp][bi]);
}

string BuildQueueSourceTag()
{
    string src = BuildQueueSourceText();
    if (src == "LOCAL") return IMS_ENTITY_TAG;
    return src;
}

void BqsLocalChanged()
{
    bqsLocalSeq = Math.Max(bqsLocalSeq, bqsClusterSeq) + 1;
    if (bqsLocalSeq > 999999) bqsLocalSeq = 1;
    UpdateBuildQueueCluster();
}

void UpdateBuildQueueCluster()
{
    string localTag = BuildQueueSourceTag();
    bqsClusterTag = localTag;
    bqsClusterSeq = bqsLocalSeq;
    bqsClusterSrc = IMS_ENTITY_TAG;
    if (mb1BqsSeq > bqsClusterSeq)
    {
        bqsClusterTag = mb1BqsTag;
        bqsClusterSeq = mb1BqsSeq;
        bqsClusterSrc = mb1BqsSrc.Length > 0 ? mb1BqsSrc : "MB1";
    }
}

string BuildQueueClusterTag()
{
    UpdateBuildQueueCluster();
    return bqsClusterTag;
}

void ExportPackets()
{
    ExportWorkerPacket();
    ExportStationStatus();
}

void ExportWorkerPacket()
{
    seq++;
    StringBuilder sb = new StringBuilder();
    sb.AppendLine(PB1_EXPORT_BEGIN);
    sb.AppendLine("Seq=" + seq.ToString());
    sb.AppendLine("Console.EditHold=" + (editHold > 0 ? "1" : "0"));
    sb.AppendLine("IMS.EntityTag=" + IMS_ENTITY_TAG);
    sb.AppendLine("IMS.EntityLabel=" + IMS_ENTITY_LABEL);
    UpdateBuildQueueCluster();
    sb.AppendLine("BuildQueue.Source=" + BuildQueueSourceText());
    sb.AppendLine("BuildQueue.SourceTag=" + BuildQueueSourceTag());
    sb.AppendLine("BuildQueue.SourceSeq=" + bqsLocalSeq.ToString());
    sb.AppendLine("BuildQueue.SourceEntity=" + IMS_ENTITY_TAG);
    sb.AppendLine("BuildQueue.ClusterTag=" + bqsClusterTag);
    sb.AppendLine("BuildQueue.ClusterSeq=" + bqsClusterSeq.ToString());
    sb.AppendLine("BuildQueue.ClusterSource=" + bqsClusterSrc);
    sb.AppendLine("Policy.TolerancePercent=5");
    for (int p = 0; p < pages.Length; p++)
    {
        if (pageKind[p] == READONLY_PAGE || pageKind[p] == AUTH_PAGE) continue;
        for (int i = 0; i < items[p].Length; i++)
        {
            string key = SafeKey(pages[p]) + "." + SafeKey(items[p][i]);
            if (pageKind[p] == 1) sb.AppendLine("Mode." + key + "=" + cur[p][i].ToString("0"));
            else
            {
                sb.AppendLine("Target." + key + ".Min=" + mins[p][i].ToString("0.###"));
                sb.AppendLine("Target." + key + ".Max=" + maxs[p][i].ToString("0.###"));
                sb.AppendLine("Current." + key + "=" + cur[p][i].ToString("0.###"));
            }
        }
    }
    sb.AppendLine("Cmd.SYSTEM.GROUP_REGROUP_SEQ=" + groupRegroupSeq.ToString());
    foreach (KeyValuePair<string, int> kv in unloadAuth)
        sb.AppendLine("AUTH|" + kv.Key + "|" + (kv.Value > 0 ? "AUTH" : "DENY"));
    sb.AppendLine(PB1_EXPORT_END);
    string pkt = sb.ToString();
    if (workerPB != null) workerPB.CustomData = ReplaceDataBlock(workerPB.CustomData, PB1_EXPORT_BEGIN, PB1_EXPORT_END, pkt);
    if (prodPB != null) prodPB.CustomData = ReplaceDataBlock(prodPB.CustomData, PB1_EXPORT_BEGIN, PB1_EXPORT_END, pkt);
}

void ExportStationStatus()
{
    StringBuilder sb = new StringBuilder();
    sb.AppendLine(OB1_STATUS_BEGIN);
    sb.AppendLine("Packet=OB1_IMS_STATION_STATUS");
    sb.AppendLine("Version=V030");
    sb.AppendLine("RefineryAutoBlocked=" + (workerRefineryAutoBlocked ? "1" : "0"));
    sb.AppendLine("RefineryAutoBlockReason=" + workerRefineryAutoBlockReason);
    sb.AppendLine("InstallTag=" + INSTALL_TAG);
    sb.AppendLine("Seq=" + seq.ToString());
    sb.AppendLine("WorkerState=" + workerState);
    sb.AppendLine("WorkerFault=" + (workerFault.Length == 0 ? "NONE" : workerFault));
    UpdateBuildQueueCluster();
    sb.AppendLine("BuildQueueSource=" + BuildQueueSourceText());
    sb.AppendLine("BuildQueueSourceTag=" + BuildQueueSourceTag());
    sb.AppendLine("BuildQueueSourceSeq=" + bqsLocalSeq.ToString());
    sb.AppendLine("BuildQueueClusterTag=" + bqsClusterTag);
    sb.AppendLine("BuildQueueClusterSeq=" + bqsClusterSeq.ToString());
    sb.AppendLine("BuildQueueClusterSource=" + bqsClusterSrc);
    sb.AppendLine("GroupRegroupSeq=" + groupRegroupSeq.ToString());
    sb.AppendLine("GroupRegroupAckSeq=" + groupRegroupAckSeq.ToString());
    sb.AppendLine("ManagedCargoCount=" + managedCargo.Count);
    sb.AppendLine("IceKg=" + cur[0][0].ToString("0.###"));
    sb.AppendLine("UraniumIngotKg=" + cur[0][1].ToString("0.###"));
    sb.AppendLine("H2Percent=" + cur[0][3].ToString("0.###"));
    sb.AppendLine("O2Percent=" + cur[0][4].ToString("0.###"));
    sb.AppendLine("BatteryPercent=" + cur[0][2].ToString("0.###"));
    sb.AppendLine("BatteryGood=" + batteryGoodCount.ToString("0"));
    sb.AppendLine("BatteryDamaged=" + batteryBadCount.ToString("0"));
    sb.AppendLine("MB1Packet=" + mb1PacketState);
    sb.AppendLine("MB1Seq=" + mb1Seq);
    sb.AppendLine("Focus=" + FocusName());
    sb.AppendLine("DetailSurfaces=" + auxSurfaces.Count);
    sb.AppendLine("MB1Needs=" + mb1Needs.Count);
    sb.AppendLine("MB1Surplus=" + mb1Surplus.Count);
    sb.AppendLine("DockedVessels=" + dockedVessels.Count);
    sb.AppendLine("UnloadAuthEntries=" + unloadAuth.Count);
    sb.AppendLine(OB1_STATUS_END);
    Me.CustomData = ReplaceDataBlock(Me.CustomData, OB1_STATUS_BEGIN, OB1_STATUS_END, sb.ToString());
}


void DrawDetailSurfaces()
{
    for (int i = 0; i < auxSurfaces.Count; i++)
    {
        IMyTextSurface s = auxSurfaces[i];
        if (s == null) continue;
        if (i == focusIndex) DrawFocusedDetail(s);
        else DrawTextPanel(s, BuildStationText());
    }
}

void DrawFocusedDetail(IMyTextSurface s)
{
    if (s == null) return;
    DrawTextPanel(s, "OB1 IMS DETAIL FOCUS\n" + pages[page] + "\nField " + (field + 1).ToString() + "/" + FieldCount().ToString() + "\n\n" + BuildMainText());
}

void DrawConsoles(bool force)
{
    PickActiveConsole();
    for (int i = 0; i < consoleRigs.Count; i++)
    {
        ConsoleRig rig = consoleRigs[i];
        bool occupied = IsOccupied(rig.Block);
        bool became = occupied != rig.WasOccupied;
        rig.WasOccupied = occupied;
        bool dueSlow = (renderTick - rig.LastDrawTick) >= 60;
        bool draw = force || consoleDirty || i == activeConsoleIndex || became || dueSlow;
        if (!draw) continue;
        DrawConsoleSurfaces(rig.Surfaces);
        rig.LastDrawTick = renderTick;
    }
    consoleDirty = false;
}

void DrawConsoleSurfaces(List<IMyTextSurface> surfaces)
{
    if (surfaces == null || surfaces.Count == 0) return;
    DrawConsoleMain(surfaces[0]);
    if (surfaces.Count > 1) DrawConsolePages(surfaces[1]);
    if (surfaces.Count > 2) DrawConsoleGraph(surfaces[2]);
    if (surfaces.Count > 3) DrawConsoleWorker(surfaces[3]);
    if (surfaces.Count > 4) DrawConsoleHelp(surfaces[4]);
}

void DrawTextPanel(IMyTextSurface s, string txt)
{
    if (s == null) return;
    Vector2 size = s.SurfaceSize;
    Vector2 origin = GetOrigin(s);
    float w = size.X, h = size.Y;
    float u = Math.Min(w / 512f, h / 512f);
    if (u < 0.45f) u = 0.45f;
    using (MySpriteDrawFrame frame = s.DrawFrame())
    {
        AddRect(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w, h), C_BG);
        AddFrame(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w - 10f * u, h - 10f * u), 2f * u, C_CYAN);
        string[] lines = txt.Split('\n');
        float y = 18f * u;
        for (int i = 0; i < lines.Length && i < 18; i++)
        {
            AddText(ref frame, lines[i], origin + new Vector2(18f * u, y), C_TEXT, 0.42f * u, LEF);
            y += 24f * u;
        }
    }
}

void DrawConsoleMain(IMyTextSurface s)
{
    ClampField();
    Vector2 size = s.SurfaceSize;
    Vector2 origin = GetOrigin(s);
    float w = size.X, h = size.Y;
    float u = Math.Min(w / 512f, h / 307f);
    if (u < 0.45f) u = 0.45f;
    if (pageKind[page] == AUTH_PAGE) { DrawDockedConsoleMain(s); return; }
    int sub = SelectedItem() / CONSOLE_ROWS;
    int total = (items[page].Length + CONSOLE_ROWS - 1) / CONSOLE_ROWS;
    if (total < 1) total = 1;
    int start = sub * CONSOLE_ROWS;
    int end = start + CONSOLE_ROWS;
    if (end > items[page].Length) end = items[page].Length;
    using (MySpriteDrawFrame frame = s.DrawFrame())
    {
        AddRect(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w, h), C_BG);
        AddFrame(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w - 12f * u, h - 12f * u), 3f * u, C_CYAN);
        string holdGlyph = EditHoldGlyph();
        if (holdGlyph.Length > 0) AddText(ref frame, holdGlyph, origin + new Vector2(14f * u, 16f * u), C_GREEN, 0.48f * u, LEF);
        AddText(ref frame, pages[page], origin + new Vector2((holdGlyph.Length > 0 ? 43f : 22f) * u, 16f * u), C_TEXT, 0.76f * u, LEF);
        string st = StepTiny();
        if (st.Length > 0) AddText(ref frame, st, origin + new Vector2(w / 2f, 16f * u), C_MUTED, 0.54f * u, CEN);
        AddText(ref frame, total > 1 ? (sub + 1).ToString("0") + "/" + total.ToString("0") : "", origin + new Vector2(w - 24f * u, 16f * u), C_MUTED, 0.46f * u, RIG);
        if (pageKind[page] == TARGET_PAGE)
        {
            AddText(ref frame, "MIN", origin + new Vector2(w * 0.585f, 45f * u), C_TEXT, 0.54f * u, CEN);
            AddText(ref frame, "MAX", origin + new Vector2(w * 0.825f, 45f * u), C_TEXT, 0.54f * u, CEN);
        }
        else if (pageKind[page] == READONLY_PAGE)
        {
            AddText(ref frame, mb1Line, origin + new Vector2(w * 0.50f, 48f * u), C_MUTED, 0.38f * u, CEN);
        }
        float y0 = 84f * u;
        float rowH = 41f * u;
        for (int item = start; item < end; item++)
        {
            if (pageKind[page] == MODE_PAGE) DrawConsoleModeRow(ref frame, origin, w, y0 + (item - start) * rowH, item, u);
            else if (pageKind[page] == TARGET_PAGE) DrawConsoleTargetRow(ref frame, origin, w, y0 + (item - start) * rowH, item, u);
            else DrawConsoleMb1Row(ref frame, origin, w, y0 + (item - start) * rowH, item, u);
        }
    }
}


void DrawDockedConsoleMain(IMyTextSurface s)
{
    ClampField();
    Vector2 size = s.SurfaceSize;
    Vector2 origin = GetOrigin(s);
    float w = size.X, h = size.Y;
    float u = Math.Min(w / 512f, h / 307f);
    if (u < 0.45f) u = 0.45f;
    int row = field / 2;
    int sub = row / CONSOLE_ROWS;
    int total = (dockedVessels.Count + CONSOLE_ROWS - 1) / CONSOLE_ROWS;
    if (total < 1) total = 1;
    int start = sub * CONSOLE_ROWS;
    int end = start + CONSOLE_ROWS;
    if (end > dockedVessels.Count) end = dockedVessels.Count;
    using (MySpriteDrawFrame frame = s.DrawFrame())
    {
        AddRect(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w, h), C_BG);
        AddFrame(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w - 12f * u, h - 12f * u), 3f * u, C_CYAN);
        AddText(ref frame, "DOCKED VESSELS", origin + new Vector2(22f * u, 16f * u), C_TEXT, 0.70f * u, LEF);
        AddText(ref frame, total > 1 ? (sub + 1).ToString("0") + "/" + total.ToString("0") : "", origin + new Vector2(w - 24f * u, 16f * u), C_MUTED, 0.46f * u, RIG);
        AddText(ref frame, "VESSEL", origin + new Vector2(34f * u, 52f * u), C_TEXT, 0.42f * u, LEF);
        AddText(ref frame, "DOCK", origin + new Vector2(w * 0.53f, 52f * u), C_TEXT, 0.42f * u, CEN);
        AddText(ref frame, "AUTHORIZED", origin + new Vector2(w * 0.70f, 52f * u), C_TEXT, 0.34f * u, CEN);
        AddText(ref frame, "DENIED", origin + new Vector2(w * 0.86f, 52f * u), C_TEXT, 0.34f * u, CEN);
        if (dockedVessels.Count == 0)
        {
            AddText(ref frame, "NO DOCKED VESSELS", origin + new Vector2(w / 2f, h * 0.55f), C_MUTED, 0.58f * u, CEN);
            return;
        }
        float y0 = 86f * u;
        float rowH = 41f * u;
        for (int i = start; i < end; i++) DrawDockedRow(ref frame, origin, w, y0 + (i - start) * rowH, i, u);
    }
}

void DrawDockedRow(ref MySpriteDrawFrame frame, Vector2 origin, float w, float y, int row, float u)
{
    DockedVessel d = dockedVessels[row];
    bool authSel = field == row * 2;
    bool denySel = field == row * 2 + 1;
    int st = GetAuthState(d.Key);
    Vector2 rowCenter = origin + new Vector2(w / 2f, y);
    AddRect(ref frame, rowCenter, new Vector2(w - 28f * u, 39f * u), (authSel || denySel) ? new Color(13, 44, 50) : new Color(4, 13, 18));
    AddRect(ref frame, rowCenter + new Vector2(-w / 2f + 18f * u, 0), new Vector2(7f * u, 31f * u), st > 0 ? C_GREEN : (st < 0 ? C_RED : C_YELLOW));
    AddText(ref frame, ShortDockName(d.Name), origin + new Vector2(34f * u, y - 15f * u), C_TEXT, 0.50f * u, LEF);
    AddText(ref frame, d.Dock, origin + new Vector2(w * 0.53f, y - 15f * u), C_MUTED, 0.50f * u, CEN);
    DrawAuthBox(ref frame, origin + new Vector2(w * 0.70f, y), st > 0, authSel, C_GREEN, u);
    DrawAuthBox(ref frame, origin + new Vector2(w * 0.86f, y), st < 0, denySel, C_RED, u);
}

void DrawAuthBox(ref MySpriteDrawFrame frame, Vector2 center, bool active, bool selected, Color activeColor, float u)
{
    Vector2 outer = new Vector2(58f * u, 26f * u);
    Vector2 lens = new Vector2(42f * u, 18f * u);
    Color baseColor = new Color(2, 11, 15);
    Color edgeColor = active ? activeColor : C_DIM;
    AddRect(ref frame, center, outer, baseColor);
    AddFrame(ref frame, center, outer, 1.2f * u, edgeColor);
    AddRect(ref frame, center, lens, active ? activeColor : new Color(5, 21, 25));
    if (selected) AddFrame(ref frame, center, new Vector2(66f * u, 32f * u), 2f * u, C_CYAN);
}

void DrawConsoleTargetRow(ref MySpriteDrawFrame frame, Vector2 origin, float w, float y, int item, float u)
{
    string name = items[page][item];
    double current = cur[page][item];
    double min = mins[page][item];
    double max = maxs[page][item];
    bool minSel = field == item * 2;
    bool maxSel = field == item * 2 + 1;
    bool rowSel = minSel || maxSel;
    Color state = current < min ? C_YELLOW : C_GREEN;
    Vector2 rowCenter = origin + new Vector2(w / 2f, y);
    AddRect(ref frame, rowCenter, new Vector2(w - 28f * u, 39f * u), rowSel ? new Color(13, 44, 50) : new Color(4, 13, 18));
    AddRect(ref frame, rowCenter + new Vector2(-w / 2f + 18f * u, 0), new Vector2(7f * u, 31f * u), state);
    AddText(ref frame, ShortName(name), origin + new Vector2(34f * u, y - 16f * u), C_TEXT, 0.54f * u, LEF);
    AddText(ref frame, FormatValue(page, name, current), origin + new Vector2(w * 0.390f, y - 16f * u), state, 0.52f * u, RIG);
    if (page == 0 && name == "BATTERY" && batteryDamaged && ((renderTick / 6) % 2) == 0)
        AddText(ref frame, "DAMAGED", origin + new Vector2(w * 0.390f, y + 2f * u), C_RED, 0.28f * u, RIG);
    DrawFieldBox(ref frame, origin + new Vector2(w * 0.585f, y), new Vector2(116f * u, 34f * u), FormatValue(page, name, min), minSel, u);
    DrawFieldBox(ref frame, origin + new Vector2(w * 0.825f, y), new Vector2(116f * u, 34f * u), FormatValue(page, name, max), maxSel, u);
}

void DrawConsoleModeRow(ref MySpriteDrawFrame frame, Vector2 origin, float w, float y, int item, float u)
{
    string name = items[page][item];
    bool selected = field == item;
    Color state = ModeColor(name, cur[page][item]);
    Vector2 rowCenter = origin + new Vector2(w / 2f, y);
    AddRect(ref frame, rowCenter, new Vector2(w - 28f * u, 39f * u), selected ? new Color(13, 44, 50) : new Color(4, 13, 18));
    AddRect(ref frame, rowCenter + new Vector2(-w / 2f + 18f * u, 0), new Vector2(7f * u, 31f * u), state);
    AddText(ref frame, ShortName(name), origin + new Vector2(34f * u, y - 16f * u), C_TEXT, 0.54f * u, LEF);
    Vector2 box = origin + new Vector2(w * 0.76f, y);
    string txt = DisplayModeText(pages[page], name, cur[page][item]);
    bool flash = name == "REGROUP AUTO" && (txt == "SENT" || txt == "ACK");
    AddRect(ref frame, box, new Vector2(190f * u, 34f * u), flash ? C_GREEN : (selected ? new Color(25, 70, 76) : new Color(2, 11, 15)));
    if (selected) AddFrame(ref frame, box, new Vector2(190f * u, 34f * u), 2f * u, flash ? Color.White : C_GREEN);
    AddText(ref frame, txt, box + new Vector2(0, -13f * u), (selected || flash) ? Color.Black : state, 0.50f * u, CEN);
}

void DrawConsoleMb1Row(ref MySpriteDrawFrame frame, Vector2 origin, float w, float y, int item, float u)
{
    bool selected = field == item;
    Vector2 rowCenter = origin + new Vector2(w / 2f, y);
    AddRect(ref frame, rowCenter, new Vector2(w - 28f * u, 39f * u), selected ? new Color(13, 44, 50) : new Color(4, 13, 18));
    AddRect(ref frame, rowCenter + new Vector2(-w / 2f + 18f * u, 0), new Vector2(7f * u, 31f * u), selected ? C_GREEN : C_DIM);
    AddText(ref frame, items[page][item], origin + new Vector2(34f * u, y - 16f * u), C_TEXT, 0.54f * u, LEF);
    string v = item == 0 ? mb1Needs.Count.ToString("0") : (item == 1 ? mb1Surplus.Count.ToString("0") : (item == 2 ? mb1Modes.Count.ToString("0") : mb1PacketState));
    AddText(ref frame, v, origin + new Vector2(w * 0.83f, y - 16f * u), item == 3 && mb1PacketState != "ONLINE" ? C_YELLOW : C_GREEN, 0.54f * u, RIG);
}

void DrawFieldBox(ref MySpriteDrawFrame frame, Vector2 center, Vector2 size, string value, bool selected, float u)
{
    Color bg = selected ? new Color(25, 70, 76) : new Color(2, 11, 15);
    Color fg = selected ? Color.Black : C_MUTED;
    AddRect(ref frame, center, size, bg);
    if (selected) AddFrame(ref frame, center, size, 2f * u, C_GREEN);
    AddText(ref frame, value, center + new Vector2(0, -14f * u), fg, 0.48f * u, CEN);
}

void DrawConsolePages(IMyTextSurface s)
{
    Vector2 size = s.SurfaceSize;
    Vector2 origin = GetOrigin(s);
    float w = size.X, h = size.Y;
    float u = Math.Min(w / 256f, h / 171f);
    if (u < 0.45f) u = 0.45f;
    int per = 6;
    int sub = page / per;
    int total = (pages.Length + per - 1) / per;
    int first = sub * per;
    int last = first + per;
    if (last > pages.Length) last = pages.Length;
    using (MySpriteDrawFrame frame = s.DrawFrame())
    {
        AddRect(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w, h), C_BG);
        AddFrame(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w - 8f * u, h - 8f * u), 2f * u, C_CYAN);
        AddText(ref frame, "PAGES", origin + new Vector2(w / 2f, 10f * u), C_TEXT, 0.52f * u, CEN);
        if (total > 1) AddText(ref frame, (sub + 1).ToString("0") + "/" + total.ToString("0"), origin + new Vector2(w - 12f * u, 10f * u), C_MUTED, 0.38f * u, RIG);
        float rowH = 23f * u;
        float y = 33f * u;
        for (int i = first; i < last; i++)
        {
            bool selected = i == page;
            Vector2 c = origin + new Vector2(w / 2f, y + (i - first) * rowH);
            AddRect(ref frame, c, new Vector2(w - 22f * u, rowH - 4f * u), selected ? new Color(13, 44, 50) : new Color(4, 13, 18));
            AddRect(ref frame, c + new Vector2(-w / 2f + 16f * u, 0), new Vector2(5f * u, rowH - 8f * u), selected ? C_GREEN : C_DIM);
            AddText(ref frame, pages[i], c + new Vector2(8f * u, -7f * u), selected ? C_TEXT : C_MUTED, 0.31f * u, CEN);
        }
    }
}

bool IsTrue(string v)
{
    if (v == null) return false;
    v = v.Trim();
    return v == "1" || v.Equals("YES", StringComparison.OrdinalIgnoreCase) || v.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || v.Equals("ON", StringComparison.OrdinalIgnoreCase);
}

bool ShowRefineryAutoBlockedGraph()
{
    int item = SelectedItem();
    return page >= 0 && page < pages.Length && pages[page] == "REFINERY" && item >= 0 && item < items[page].Length && items[page][item] == "REFINING" && ModeText(pages[page], items[page][item], cur[page][item]) == "AUTO" && workerRefineryAutoBlocked;
}

void DrawConsoleGraph(IMyTextSurface s)
{
    Vector2 size = s.SurfaceSize;
    Vector2 origin = GetOrigin(s);
    float w = size.X, h = size.Y;
    float u = Math.Min(w / 256f, h / 171f);
    if (u < 0.45f) u = 0.45f;
    int item = SelectedItem();
    using (MySpriteDrawFrame frame = s.DrawFrame())
    {
        AddRect(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w, h), C_BG);
        AddFrame(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w - 8f * u, h - 8f * u), 2f * u, C_CYAN);
        AddText(ref frame, item >= 0 && item < items[page].Length ? ShortName(items[page][item]) : pages[page], origin + new Vector2(w / 2f, 16f * u), C_TEXT, 0.55f * u, CEN);
        if (pageKind[page] == AUTH_PAGE)
        {
            int auth = 0, deny = 0;
            for (int i = 0; i < dockedVessels.Count; i++)
            {
                int st = GetAuthState(dockedVessels[i].Key);
                if (st > 0) auth++; else if (st < 0) deny++;
            }
            AddText(ref frame, "DOCKED", origin + new Vector2(w / 2f, h * 0.40f), C_TEXT, 0.72f * u, CEN);
            AddText(ref frame, dockedVessels.Count.ToString("0") + " LIVE", origin + new Vector2(w / 2f, h * 0.57f), dockedVessels.Count > 0 ? C_GREEN : C_MUTED, 0.50f * u, CEN);
            AddText(ref frame, "AUTH " + auth.ToString("0") + "  DENY " + deny.ToString("0"), origin + new Vector2(w / 2f, h * 0.74f), C_MUTED, 0.36f * u, CEN);
        }
        else if (pageKind[page] == MODE_PAGE)
        {
            Color c = ModeColor(items[page][item], cur[page][item]);
            bool blocked = ShowRefineryAutoBlockedGraph();
            if (blocked && ((renderTick / 6) % 2) == 0)
                AddText(ref frame, "BLOCKED", origin + new Vector2(w / 2f, h * 0.34f), C_RED, 0.82f * u, CEN);
            AddText(ref frame, DisplayModeText(pages[page], items[page][item], cur[page][item]), origin + new Vector2(w / 2f, h * 0.52f), c, 0.70f * u, CEN);
            DrawBar(ref frame, origin + new Vector2(w / 2f, h * 0.72f), new Vector2(w * 0.70f, 18f * u), cur[page][item] > 0 ? 1 : 0, 0, 1, c, u, 0);
        }
        else if (pageKind[page] == TARGET_PAGE)
        {
            double current = cur[page][item], min = mins[page][item], max = maxs[page][item];
            AddText(ref frame, FormatValue(page, items[page][item], current), origin + new Vector2(w / 2f, 42f * u), current < min ? C_YELLOW : C_GREEN, 0.45f * u, CEN);
            if (page == 0 && item >= 0 && item < items[page].Length && items[page][item] == "BATTERY" && batteryDamaged && ((renderTick / 6) % 2) == 0)
                AddText(ref frame, "DAMAGED", origin + new Vector2(w / 2f, 60f * u), C_RED, 0.25f * u, CEN);
            DrawBar(ref frame, origin + new Vector2(w / 2f, h * 0.62f), new Vector2(w * 0.78f, 31f * u), current, min, max, current < min ? C_YELLOW : C_GREEN, u, 1);
            AddText(ref frame, "MIN " + FormatValue(page, items[page][item], min), origin + new Vector2(w * 0.10f, h - 28f * u), C_YELLOW, 0.30f * u, LEF);
            AddText(ref frame, "MAX " + FormatValue(page, items[page][item], max), origin + new Vector2(w * 0.90f, h - 28f * u), C_GREEN, 0.30f * u, RIG);
        }
        else
        {
            AddText(ref frame, "MB1", origin + new Vector2(w / 2f, h * 0.45f), mb1PacketState == "ONLINE" ? C_GREEN : C_YELLOW, 0.90f * u, CEN);
            AddText(ref frame, mb1PacketState, origin + new Vector2(w / 2f, h * 0.68f), C_MUTED, 0.42f * u, CEN);
        }
    }
}

void DrawConsoleWorker(IMyTextSurface s)
{
    Vector2 z = s.SurfaceSize;
    Vector2 o = GetOrigin(s);
    float w = z.X, h = z.Y;
    bool paused = cur[4][0] > 0.5;
    Color c = workerState == "OFFLINE" ? C_YELLOW : (workerFault.Length > 0 && workerFault != "NONE" ? C_RED : C_GREEN);
    using (MySpriteDrawFrame f = s.DrawFrame())
    {
        AddRect(ref f, o + new Vector2(w / 2f, h / 2f), z, C_BG);
        if (paused)
        {
            AddRect(ref f, o + new Vector2(w * 0.41f, h * 0.5f), new Vector2(w * 0.14f, h * 0.62f), c);
            AddRect(ref f, o + new Vector2(w * 0.59f, h * 0.5f), new Vector2(w * 0.14f, h * 0.62f), c);
        }
        else
        {
            MySprite sp = new MySprite(SpriteType.TEXTURE, "Triangle", o + new Vector2(w * 0.54f, h * 0.5f), new Vector2(h * 0.72f, h * 0.72f), c);
            sp.RotationOrScale = (float)(Math.PI / 2.0);
            f.Add(sp);
        }
    }
}

void DrawConsoleHelp(IMyTextSurface s)
{
    Vector2 size = s.SurfaceSize;
    Vector2 origin = GetOrigin(s);
    float w = size.X, h = size.Y;
    float u = Math.Min(w / 256f, h / 192f);
    if (u < 0.45f) u = 0.45f;
    using (MySpriteDrawFrame frame = s.DrawFrame())
    {
        AddRect(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w, h), C_BG);
        AddFrame(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w - 8f * u, h - 8f * u), 2f * u, C_CYAN);
        AddText(ref frame, "HOTBAR", origin + new Vector2(w / 2f, 10f * u), C_TEXT, 0.68f * u, CEN);
        float y = 50f * u, lh = 32f * u, lx = w * 0.29f, rx = w * 0.71f;
        AddText(ref frame, "PAGE PREV", origin + new Vector2(lx, y), C_GREEN, 0.50f * u, CEN);
        AddText(ref frame, "PAGE NEXT", origin + new Vector2(rx, y), C_GREEN, 0.50f * u, CEN);
        AddText(ref frame, "PREV", origin + new Vector2(lx, y + lh), C_GREEN, 0.50f * u, CEN);
        AddText(ref frame, "NEXT", origin + new Vector2(rx, y + lh), C_GREEN, 0.50f * u, CEN);
        AddText(ref frame, "DEC", origin + new Vector2(lx, y + lh * 2f), C_GREEN, 0.50f * u, CEN);
        AddText(ref frame, "INC", origin + new Vector2(rx, y + lh * 2f), C_GREEN, 0.50f * u, CEN);
        AddText(ref frame, "STEP", origin + new Vector2(lx, y + lh * 3f), C_GREEN, 0.50f * u, CEN);
        AddText(ref frame, "FOCUS", origin + new Vector2(rx, y + lh * 3f), C_GREEN, 0.50f * u, CEN);
    }
}

string RegroupButtonText()
{
    if (groupRegroupSeq > 0 && groupRegroupAckSeq >= groupRegroupSeq && renderTick - groupRegroupAckTick <= 12) return "ACK";
    if (groupRegroupSeq > 0 && renderTick - groupRegroupSentTick <= 12) return "SENT";
    return "PRESS";
}

bool RegroupButtonActive()
{
    string t = RegroupButtonText();
    return t == "SENT" || t == "ACK";
}

Color ModeColor(string name, double value)
{
    if (name == "BUILD QUEUE SOURCE")
    {
        UpdateBuildQueueCluster();
        if (bqsClusterTag.Length == 0 || bqsClusterTag == "AUTO") return C_DIM;
        return bqsClusterTag == IMS_ENTITY_TAG ? C_GREEN : C_CYAN;
    }
    if (name == "WORKER MODE") return value > 0 ? C_DIM : C_GREEN;
    if (name == "DISTRIBUTION METHOD") return value > 0 ? C_CYAN : C_GREEN;
    if (name == "REGROUP AUTO") return RegroupButtonActive() ? C_GREEN : C_YELLOW;
    if (name == "COMPONENT STORAGE") return value > 0 ? C_CYAN : C_GREEN;
    if (name == "LCD ROTATION") return value > 0 ? C_GREEN : C_DIM;
    if (value <= 0) return C_DIM;
    if (value >= 2) return C_CYAN;
    return C_GREEN;
}


Vector2 GetOrigin(IMyTextSurface s)
{
    return (s.TextureSize - s.SurfaceSize) * 0.5f;
}

void AddRect(ref MySpriteDrawFrame frame, Vector2 pos, Vector2 size, Color color)
{
    frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", pos, size, color));
}

void AddFrame(ref MySpriteDrawFrame frame, Vector2 center, Vector2 size, float thickness, Color color)
{
    AddRect(ref frame, center + new Vector2(0, -size.Y / 2f + thickness / 2f), new Vector2(size.X, thickness), color);
    AddRect(ref frame, center + new Vector2(0, size.Y / 2f - thickness / 2f), new Vector2(size.X, thickness), color);
    AddRect(ref frame, center + new Vector2(-size.X / 2f + thickness / 2f, 0), new Vector2(thickness, size.Y), color);
    AddRect(ref frame, center + new Vector2(size.X / 2f - thickness / 2f, 0), new Vector2(thickness, size.Y), color);
}

void AddText(ref MySpriteDrawFrame frame, string text, Vector2 pos, Color color, float scale, TextAlignment align)
{
    MySprite sprite = MySprite.CreateText(text, "White", color, scale, align);
    sprite.Position = pos;
    frame.Add(sprite);
}

void DrawBar(ref MySpriteDrawFrame frame, Vector2 center, Vector2 size, double value, double mark1, double mark2, Color color, float u, int mode)
{
    AddRect(ref frame, center, size, C_PANEL);
    double max = mode == 0 ? 1.0 : mark2;
    if (max <= 0.001) max = 1.0;
    double ratio = value / max;
    float fillW = (float)(size.X * Clamp01(ratio));
    if (fillW < 1f && value > 0.001) fillW = 1f;
    AddRect(ref frame, center + new Vector2((fillW - size.X) / 2f, 0), new Vector2(fillW, size.Y - 6f * u), color);
    if (mode > 0 && mark1 > 0.001 && mark1 < max)
    {
        float x = center.X - size.X / 2f + (float)(size.X * (mark1 / max));
        AddRect(ref frame, new Vector2(x, center.Y), new Vector2(2f * u, size.Y + 5f * u), C_YELLOW);
    }
    if (mode == 1 && value > max) AddRect(ref frame, center + new Vector2(size.X / 2f - 2f * u, 0), new Vector2(4f * u, size.Y + 5f * u), C_TEXT);
}

double Clamp01(double v)
{
    if (v < 0) return 0;
    if (v > 1) return 1;
    return v;
}

void EchoStatus()
{
    Echo("OB1 IMS PB1 V032");
    Echo(consoleLine);
    Echo(lastScan);
    Echo(cargoLine);
    Echo(gasLine);
    Echo("Worker " + workerState + " " + workerFault);
    UpdateBuildQueueCluster();
    Echo("Prod " + (prodPB == null ? "OFFLINE" : "ONLINE") + " BQS local " + BuildQueueSourceText() + "->" + BuildQueueSourceTag());
    Echo("BQS cluster " + bqsClusterTag + " seq " + bqsClusterSeq + " src " + bqsClusterSrc);
    Echo(mb1Line);
}

void SaveOfficerData()
{
    StringBuilder sb = new StringBuilder();
    sb.AppendLine(OFFICER_DATA_BEGIN);
    sb.AppendLine("Page=" + page.ToString());
    sb.AppendLine("Field=" + field.ToString());
    sb.AppendLine("Step=" + stepIndex.ToString());
    sb.AppendLine("Focus=" + focusIndex.ToString());
    sb.AppendLine("DataVersion=V032");
    for (int p = 0; p < pages.Length; p++)
    {
        for (int i = 0; i < items[p].Length; i++)
        {
            string key = SafeKey(pages[p]) + "." + SafeKey(items[p][i]);
            if (pageKind[p] == TARGET_PAGE)
            {
                sb.AppendLine("Min." + key + "=" + mins[p][i].ToString("0.###"));
                sb.AppendLine("Max." + key + "=" + maxs[p][i].ToString("0.###"));
            }
            else if (pageKind[p] == MODE_PAGE) sb.AppendLine("Mode." + key + "=" + cur[p][i].ToString("0"));
        }
    }
    sb.AppendLine(OFFICER_DATA_END);
    Me.CustomData = ReplaceDataBlock(Me.CustomData, OFFICER_DATA_BEGIN, OFFICER_DATA_END, sb.ToString());
}

void LoadOfficerData()
{
    string block = ExtractDataBlock(Me.CustomData, OFFICER_DATA_BEGIN, OFFICER_DATA_END);
    if (block.Length == 0) return;
    bool hasV014 = block.IndexOf("DataVersion=V014", StringComparison.OrdinalIgnoreCase) >= 0;
    bool hasV015 = block.IndexOf("DataVersion=V015", StringComparison.OrdinalIgnoreCase) >= 0;
    bool hasV016 = block.IndexOf("DataVersion=V016", StringComparison.OrdinalIgnoreCase) >= 0;
    bool hasV017 = block.IndexOf("DataVersion=V017", StringComparison.OrdinalIgnoreCase) >= 0;
    bool hasV018 = block.IndexOf("DataVersion=V018", StringComparison.OrdinalIgnoreCase) >= 0;
    bool hasV021 = block.IndexOf("DataVersion=V021", StringComparison.OrdinalIgnoreCase) >= 0;
    bool hasV022 = block.IndexOf("DataVersion=V022", StringComparison.OrdinalIgnoreCase) >= 0;
    bool hasV023 = block.IndexOf("DataVersion=V023", StringComparison.OrdinalIgnoreCase) >= 0;
    bool hasV025 = block.IndexOf("DataVersion=V025", StringComparison.OrdinalIgnoreCase) >= 0;
    bool hasV026 = block.IndexOf("DataVersion=V026", StringComparison.OrdinalIgnoreCase) >= 0;
    bool hasV027 = block.IndexOf("DataVersion=V027", StringComparison.OrdinalIgnoreCase) >= 0;
    bool hasV029 = block.IndexOf("DataVersion=V031", StringComparison.OrdinalIgnoreCase) >= 0;
    legacyOfficerData = !(hasV014 || hasV015 || hasV016 || hasV017 || hasV018 || hasV021 || hasV022 || hasV023 || hasV025 || hasV026 || hasV027 || hasV029);
    string[] lines = block.Split('\n');
    for (int n = 0; n < lines.Length; n++)
    {
        string line = lines[n].Trim();
        int eq = line.IndexOf('=');
        if (eq <= 0) continue;
        string k = line.Substring(0, eq).Trim();
        string v = line.Substring(eq + 1).Trim();
        int iv;
        if (k == "Page" && int.TryParse(v, out iv)) page = ClampInt(iv, 0, pages.Length - 1);
        else if (k == "Field" && int.TryParse(v, out iv)) field = iv;
        else if (k == "Step" && int.TryParse(v, out iv)) stepIndex = ClampInt(iv, 0, 4);
        else if (k == "Focus" && int.TryParse(v, out iv)) focusIndex = iv;
        else LoadValue(k, v);
    }
    ClampField();
    NormalizeAllTargets();
    if (stepIndex >= ActiveStepCount()) stepIndex = ActiveStepCount() - 1;
}

void LoadValue(string k, string v)
{
    double d;
    if (!double.TryParse(v, out d)) return;
    for (int p = 0; p < pages.Length; p++)
    {
        for (int i = 0; i < items[p].Length; i++)
        {
            string key = SafeKey(pages[p]) + "." + SafeKey(items[p][i]);
            if (pageKind[p] == TARGET_PAGE && k == "Min." + key) { mins[p][i] = d; NormalizeTargetPair(p, i); }
            else if (pageKind[p] == TARGET_PAGE && k == "Max." + key) { maxs[p][i] = d; NormalizeTargetPair(p, i); }
            else if (k == "Mode." + key && pageKind[p] == MODE_PAGE)
            {
                d = MigrateLegacyMode(key, d);
                if (d < 0) d = 0;
                int hi = ModeMax(p, i);
                if (d > hi) d = hi;
                cur[p][i] = d;
            }
        }
    }
}

double MigrateLegacyMode(string key, double value)
{
    if (!legacyOfficerData) return value;
    // V013 had no visible OFF for REFINING. Preserve old visible intent when loading old officer data.
    if (key.Equals("REFINERY.REFINING", StringComparison.OrdinalIgnoreCase)) return value <= 0 ? 1 : 2;
    // V016 collapses ORE UNLOAD from OFF/AUTO/ON into OFF/ON. Any old nonzero intent becomes ON.
    if (key.Equals("REFINERY.ORE_UNLOAD", StringComparison.OrdinalIgnoreCase)) return value <= 0 ? 0 : 1;
    return value;
}

int ItemIndex(int p, string name)
{
    if (p < 0 || p >= items.Length) return -1;
    for (int i = 0; i < items[p].Length; i++)
        if (items[p][i].Equals(name, StringComparison.OrdinalIgnoreCase)) return i;
    return -1;
}

int PageIndex(string name)
{
    for (int i = 0; i < pages.Length; i++)
        if (pages[i].Equals(name, StringComparison.OrdinalIgnoreCase)) return i;
    return -1;
}

int ClampInt(int v, int lo, int hi)
{
    if (v < lo) return lo;
    if (v > hi) return hi;
    return v;
}

bool HasDetailTag(IMyTerminalBlock b)
{
    return b != null && b.CustomName != null && b.CustomName.IndexOf(DETAIL_TAG_PREFIX, StringComparison.OrdinalIgnoreCase) >= 0;
}

bool HasTag(IMyTerminalBlock b, string tag)
{
    return b != null && b.CustomName != null && b.CustomName.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0;
}

double CargoCapacityL(IMyTerminalBlock b)
{
    if (b == null || b.InventoryCount <= 0) return 0;
    double l = 0;
    for (int i = 0; i < b.InventoryCount; i++)
    {
        IMyInventory inv = b.GetInventory(i);
        if (inv != null) l += (double)inv.MaxVolume * 1000.0;
    }
    return l;
}

string SafeKey(string s)
{
    if (s == null) return "";
    return s.Trim().ToUpper().Replace(' ', '_').Replace('-', '_');
}

string Format(double v)
{
    double a = Math.Abs(v);
    if (a >= 1000000) return (v / 1000000.0).ToString("0.##") + "M";
    if (a >= 1000) return (v / 1000.0).ToString("0.##") + "k";
    return v.ToString("0.##");
}

string ExtractDataBlock(string text, string begin, string end)
{
    if (text == null) return "";
    int a = text.IndexOf(begin, StringComparison.OrdinalIgnoreCase);
    if (a < 0) return "";
    a += begin.Length;
    int b = text.IndexOf(end, a, StringComparison.OrdinalIgnoreCase);
    if (b < 0) return "";
    return text.Substring(a, b - a).Trim();
}

string ReplaceDataBlock(string existing, string begin, string end, string block)
{
    if (existing == null) existing = "";
    int a = existing.IndexOf(begin, StringComparison.OrdinalIgnoreCase);
    int b = existing.IndexOf(end, StringComparison.OrdinalIgnoreCase);
    string clean = block.Trim();
    if (a >= 0 && b > a) return existing.Remove(a, b + end.Length - a).Insert(a, clean);
    if (existing.Trim().Length == 0) return clean;
    return existing.TrimEnd() + "\n" + clean;
}
