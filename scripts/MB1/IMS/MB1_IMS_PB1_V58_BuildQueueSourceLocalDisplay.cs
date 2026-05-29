// IMS_MB1_V58_BuildQueueSourceEffectiveDisplay
// Based on V54R. Numbered IMS detail LCDs plus WSO/SIO-style white focus rails on detail LCDs.
// Numbered IMS detail LCDs: [IMS-DETAIL1], [IMS-DETAIL2], etc.
// FOCUS cycles CONSOLE -> numbered detail LCDs in order -> CONSOLE.
// Detail LCD discovery is refreshed during normal scans; no PB recompile needed for newly tagged LCDs.
// Detail LCD quantity pages auto-cycle while the IMS seat is not controlled; delay is controlled on SYSTEM > LCD ROTATION.

const string INSTALL_TAG = "[MB1]";
const string IMS_ENTITY_TAG = "MB1";
const string IMS_ENTITY_LABEL = "MB1";
const string DISPLAY_TAG = "[IMS]";
const string OFFICER_TAG = "[OFFICER]";
const string DETAIL_PREFIX = "[IMS-DETAIL";
const string WORKER_TAG = "[IMSWORKER]";
const string MAIN_CARGO_TAG = "[MAIN]";
const int SCAN_TICKS = 12; // Update10 * 12 ~= 2 seconds
const double FULL_WARN_RATIO = 0.85;
const double FULL_CRIT_RATIO = 0.95;
const double BALANCE_WARN_DELTA = 0.20;
const double BALANCE_CRIT_DELTA = 0.35;
const double ICE_LOW_KG = 5000.0;
const double URANIUM_LOW_KG = 10.0;
const double AMMO_LOW_UNITS = 10.0;
const double COMPONENT_LOW_UNITS = 500.0;
const TextAlignment CEN = TextAlignment.CENTER, LEF = TextAlignment.LEFT, RIG = TextAlignment.RIGHT;
List<IMyTerminalBlock> allTaggedBlocks = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> inventoryBlocks = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> cargoBlocks = new List<IMyTerminalBlock>();
List<IMyTerminalBlock> mainCargoBlocks = new List<IMyTerminalBlock>();
List<IMyGasTank> gasTanks = new List<IMyGasTank>();
List<IMyBatteryBlock> batteryBlocks = new List<IMyBatteryBlock>();
List<IMyTextSurface> surfaces = new List<IMyTextSurface>();
List<DetailTarget> detailTargets = new List<DetailTarget>();
IMyTerminalBlock oBlock = null;
IMyTextSurface oSurface = null;
List<IMyTextSurface> oSurfaces = new List<IMyTextSurface>();
IMyProgrammableBlock workerPB = null;
IMyProgrammableBlock ob1PB1 = null;
string workerState = "OFFLINE";
string workerFault = "";
int workerSeq = 0;
List<MyInventoryItem> itemScratch = new List<MyInventoryItem>();
const int OFFICER_TARGET_PAGE = 0;
const int OFFICER_MODE_PAGE = 1;
const int OFFICER_ROWS = 5;
int oPage = 0;
int oField = 0;
int oStep = 1;
int editHold = 0;
int bqsLocalSeq = 0;
int bqsClusterSeq = 0;
string bqsClusterTag = "AUTO";
string bqsClusterSrc = "MB1";
string ob1BqsTag = "AUTO";
int ob1BqsSeq = -1;
string ob1BqsSrc = "OB1";
string ob1BqsLine = "OB1 BQS offline";
bool displayOnlyCommand = false;
int detailFocusIndex = -1;
int detailIdleCycleTick = 0;
const int EDIT_HOLD_TICKS = 30;
double[] officerSteps = new double[] { 10, 100, 1000 };
string[] oPages = new string[] { "SUPPLY", "AMMO", "COMPONENTS", "REFINED", "ASSEMBLY", "REFINERY", "SYSTEM" };
int[] oKind = new int[] { OFFICER_TARGET_PAGE, OFFICER_TARGET_PAGE, OFFICER_TARGET_PAGE, OFFICER_TARGET_PAGE, OFFICER_MODE_PAGE, OFFICER_MODE_PAGE, OFFICER_MODE_PAGE };
int[] oRemember = new int[] { 0, 0, 0, 0, 0, 0, 0 };
string[][] oItems = new string[][]
{
new string[] { "ICE", "URANIUM", "BATTERY", "H2", "O2" },
new string[] { "GATLING BOX", "RIFLE MAG", "MISSILES", "ARTILLERY", "ASSAULT", "AUTOCANNON", "RAILGUN" },
new string[] { "STEEL PLATE", "INT. PLATE", "CONST. COMP", "MOTOR", "COMPUTER", "DISPLAY", "GIRDER", "SMALL TUBE", "LARGE TUBE", "METAL GRID", "B. GLASS", "MEDICAL", "DETECTOR", "RADIO-COMM", "POWER CELL", "REACTOR COMP", "SUPERCOND.", "THRUSTER COMP" },
new string[] { "IRON", "NICKEL", "COBALT", "SILICON", "MAGNESIUM", "SILVER", "GOLD", "PLATINUM", "GRAVEL" },
new string[] { "ASSEMBLY", "COMPONENTS", "AMMO", "TOOLS" },
new string[] { "REFINING", "ICE PROCESSING", "INGOT CLEARING", "ORE UNLOAD" },
new string[] { "DISTRIBUTION METHOD", "WORKER MODE", "BUILD QUEUE SOURCE", "SHOW GRAVEL", "LCD ROTATION" }
};
double[][] oMin = new double[][]
{
new double[] { 50000, 50, 30, 70, 40 },
new double[] { 25, 250, 25, 50, 50, 50, 10 },
new double[] { 500, 500, 500, 200, 200, 50, 200, 500, 500, 200, 100, 25, 10, 10, 100, 25, 25, 50 },
new double[] { 50000, 10000, 5000, 5000, 1000, 1000, 500, 200, 10000 },
new double[] { 0, 0, 0, 0 },
new double[] { 0, 0, 0, 0 },
new double[] { 0, 0, 0, 0, 0 }
};
double[][] oMax = new double[][]
{
new double[] { 100000, 100, 100, 100, 100 },
new double[] { 500, 2000, 500, 500, 500, 500, 100 },
new double[] { 2000, 2000, 2000, 1000, 1000, 500, 1000, 2000, 2000, 1000, 500, 100, 50, 50, 500, 100, 100, 250 },
new double[] { 200000, 50000, 25000, 25000, 10000, 5000, 3000, 1000, 50000 },
new double[] { 1, 2, 2, 2 },
new double[] { 1, 2, 1, 1 },
new double[] { 1, 1, 2, 1, 600 }
};
double[][] oCur = new double[][]
{
new double[] { 30700, 47, 0, 62, 88 },
new double[] { 5, 0, 0, 0, 0, 0, 0 },
new double[] { 1100, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
new double[] { 79800, 0, 0, 0, 0, 0, 0, 0, 0 },
new double[] { 0, 0, 0, 0 },
new double[] { 0, 2, 1, 0 },
new double[] { 0, 0, 0, 1, 5 }
};
InventorySummary cached = new InventorySummary();
int tick = 0;
bool discovered = false;
bool iceProcessingModeV2 = false;
Color C_BG = new Color(2, 5, 7);
Color C_DEEP = new Color(5, 12, 16);
Color C_PANEL = new Color(9, 20, 27);
Color C_PANEL2 = new Color(15, 34, 43);
Color C_TEXT = new Color(218, 245, 240);
Color C_MUTED = new Color(110, 165, 170);
Color C_DIM = new Color(40, 72, 82);
Color C_CARGO_FRAME = new Color(38, 112, 118);
Color C_CYAN = new Color(70, 245, 220);
Color C_GREEN = new Color(85, 230, 145);
Color C_YELLOW = new Color(245, 190, 70);
Color C_ORANGE = new Color(245, 125, 45);
Color C_RED = new Color(245, 75, 65);
class CargoBay
{
public string Name;
public double CurrentL;
public double MaxL;
public double Fill;
}
class DetailTarget
{
public int Num;
public IMyTextSurface Surface;
public int Cat;
public int[] Sub = new int[] { 0, 0, 0, 0 };
}
class InventorySummary
{
public double CurrentVolumeL;
public double MaxVolumeL;
public double FillRatio;
public double MainCurrentL;
public double MainMaxL;
public double MainFillRatio;
public double BalanceDelta;
public int MainCargoBlocks;
public double IngotKg;
public double ComponentUnits;
public double AmmoUnits;
public double IceKg;
public double UraniumKg;
public double BatteryPercent;
public double HydrogenPercent;
public double OxygenPercent;
public Dictionary<string, double> ComponentBySubtype = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
public Dictionary<string, double> IngotBySubtype = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
public Dictionary<string, double> AmmoBySubtype = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
public CargoBay[] Bays = new CargoBay[8];
public string Status;
public Color StatusColor;
public string DistributionState;
public Color DistributionColor;
}
public Program()
{
Runtime.UpdateFrequency = UpdateFrequency.Update10;
Discover();
LoadOfficerData();
cached = ScanInventory();
UpdateOfficerCurrentFromLive();
RefreshConsole(false, false);
}
public void Main(string argument, UpdateType updateSource)
{
string arg = argument == null ? "" : argument.Trim().ToUpper();
if (arg == "RESET" || arg == "SCAN" || arg == "DISCOVER")
{
RefreshConsole(true, true);
return;
}
if (arg == "STATUS")
{
DrawAll();
ExportWorkerPacket();
return;
}
if (HandleOfficerCommand(arg))
{
if (!displayOnlyCommand) editHold = EDIT_HOLD_TICKS;
RefreshConsole(true, true);
return;
}
tick++;
if (editHold > 0) editHold--;
bool cycledDetailPages = IdleCycleDetailPages();
if (!discovered || tick >= SCAN_TICKS)
{
tick = 0;
RefreshConsole(true, true);
}
else if (cycledDetailPages)
{
DrawAll();
}
}

void RefreshConsole(bool discoverNow, bool scanNow)
{
if (discoverNow) Discover();
if (scanNow)
{
cached = ScanInventory();
UpdateOfficerCurrentFromLive();
}
ImportOb1BuildQueuePacket();
UpdateBuildQueueCluster();
DrawAll();
DrawOfficerConsole();
ExportWorkerPacket();
}
bool IsOfficerConsoleCandidate(IMyTerminalBlock b)
{
IMyTextSurfaceProvider provider = b as IMyTextSurfaceProvider;
if (provider == null || provider.SurfaceCount <= 0) return false;
if (HasTag(b, DISPLAY_TAG) && b is IMyShipController && provider.SurfaceCount >= 5) return true;
if (HasTag(b, OFFICER_TAG)) return true;
return false;
}
int OfficerConsoleScore(IMyTerminalBlock b)
{
IMyTextSurfaceProvider provider = b as IMyTextSurfaceProvider;
if (provider == null) return -1;
int score = provider.SurfaceCount;
if (HasTag(b, DISPLAY_TAG) && b is IMyShipController && provider.SurfaceCount >= 5) score += 3000;
else if (HasTag(b, OFFICER_TAG)) score += 2000;
return score;
}
void AssignOfficerConsole(IMyTerminalBlock b)
{
IMyTextSurfaceProvider officerProvider = b as IMyTextSurfaceProvider;
if (officerProvider == null || officerProvider.SurfaceCount <= 0) return;
if (oBlock != null && OfficerConsoleScore(b) <= OfficerConsoleScore(oBlock)) return;
oBlock = b;
oSurface = officerProvider.GetSurface(0);
oSurfaces.Clear();
for (int os = 0; os < officerProvider.SurfaceCount; os++)
{
IMyTextSurface osurf = officerProvider.GetSurface(os);
if (osurf == null) continue;
PrepareSurface(osurf);
oSurfaces.Add(osurf);
}
}
void Discover()
{
int oldFocusNum = -1;
if (detailFocusIndex >= 0 && detailFocusIndex < detailTargets.Count) oldFocusNum = detailTargets[detailFocusIndex].Num;
List<DetailTarget> oldDetailTargets = new List<DetailTarget>();
for (int i = 0; i < detailTargets.Count; i++)
{
DetailTarget src = detailTargets[i];
if (src == null) continue;
DetailTarget cp = new DetailTarget();
cp.Num = src.Num;
cp.Cat = src.Cat;
for (int k = 0; k < cp.Sub.Length && k < src.Sub.Length; k++) cp.Sub[k] = src.Sub[k];
oldDetailTargets.Add(cp);
}
allTaggedBlocks.Clear();
inventoryBlocks.Clear();
cargoBlocks.Clear();
mainCargoBlocks.Clear();
gasTanks.Clear();
batteryBlocks.Clear();
surfaces.Clear();
detailTargets.Clear();
oBlock = null;
oSurface = null;
oSurfaces.Clear();
workerPB = null;
GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(
allTaggedBlocks,
b => b.IsSameConstructAs(Me) && HasTag(b, INSTALL_TAG)
);
for (int i = 0; i < allTaggedBlocks.Count; i++)
{
IMyTerminalBlock b = allTaggedBlocks[i];
if (workerPB == null && b != Me && HasTag(b, WORKER_TAG) && b is IMyProgrammableBlock) workerPB = b as IMyProgrammableBlock;
bool officerCandidate = IsOfficerConsoleCandidate(b);
if (officerCandidate)
AssignOfficerConsole(b);
if (b.HasInventory)
inventoryBlocks.Add(b);
if (b is IMyCargoContainer)
cargoBlocks.Add(b);
IMyBatteryBlock batt = b as IMyBatteryBlock;
if (batt != null) batteryBlocks.Add(batt);
int detailNum = ParseDetailNumber(b.CustomName);
if (detailNum > 0)
AddDetailTarget(b, detailNum, oldDetailTargets);
if (HasTag(b, DISPLAY_TAG) && !officerCandidate)
{
IMyTextSurfaceProvider provider = b as IMyTextSurfaceProvider;
if (provider != null)
{
for (int s = 0; s < provider.SurfaceCount; s++)
{
IMyTextSurface surface = provider.GetSurface(s);
if (surface == null) continue;
PrepareSurface(surface);
surfaces.Add(surface);
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
string cd = pb.CustomData;
if (cd != null && cd.IndexOf(OB1_STATUS_BEGIN, StringComparison.OrdinalIgnoreCase) >= 0) { ob1PB1 = pb; break; }
}
SortDetailTargets();
if (oldFocusNum > 0)
{
detailFocusIndex = -1;
for (int i = 0; i < detailTargets.Count; i++)
if (detailTargets[i].Num == oldFocusNum) { detailFocusIndex = i; break; }
}
if (detailTargets.Count == 0 || detailFocusIndex >= detailTargets.Count) detailFocusIndex = -1;
SelectPrimaryCargoBank();
discovered = true;
UpdateBuildQueueCluster();
Echo("IMS_MB1_V58 BQS local " + BuildQueueSourceText() + "->" + BuildQueueSourceTag());
Echo("BQS cluster " + bqsClusterTag + " seq " + bqsClusterSeq + " src " + bqsClusterSrc);
Echo(ob1BqsLine);
}
const double MAIN_CARGO_MIN_L = 300000.0;
void SelectPrimaryCargoBank()
{
mainCargoBlocks.Clear();
gasTanks.Clear();
if (cargoBlocks.Count == 0) return;
for (int i = 0; i < cargoBlocks.Count; i++)
{
IMyTerminalBlock b = cargoBlocks[i];
if (b == null) continue;
if (!HasTag(b, INSTALL_TAG)) continue;
if (HasWeaponTag(b.CustomName)) continue;
double cap = CargoCapacityL(b);
if (cap >= MAIN_CARGO_MIN_L)
mainCargoBlocks.Add(b);
}
}
double CargoCapacityL(IMyTerminalBlock b)
{
if (b == null || b.InventoryCount <= 0) return 0;
double liters = 0;
for (int i = 0; i < b.InventoryCount; i++)
{
IMyInventory inv = b.GetInventory(i);
if (inv == null) continue;
liters += (double)inv.MaxVolume * 1000.0;
}
return liters;
}
bool HasTag(IMyTerminalBlock block, string tag)
{
return block != null
&& block.CustomName != null
&& block.CustomName.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0;
}
bool HasWeaponTag(string name)
{
if (string.IsNullOrEmpty(name)) return false;
return name.IndexOf("[WPN", StringComparison.OrdinalIgnoreCase) >= 0;
}
int ParseDetailNumber(string name)
{
if (string.IsNullOrEmpty(name)) return -1;
int idx = name.IndexOf(DETAIL_PREFIX, StringComparison.OrdinalIgnoreCase);
while (idx >= 0)
{
int p = idx + DETAIL_PREFIX.Length;
int n = 0;
bool any = false;
while (p < name.Length && name[p] >= '0' && name[p] <= '9')
{
any = true;
n = n * 10 + (name[p] - '0');
p++;
}
if (any && p < name.Length && name[p] == ']') return n;
idx = name.IndexOf(DETAIL_PREFIX, idx + 1, StringComparison.OrdinalIgnoreCase);
}
return -1;
}
void AddDetailTarget(IMyTerminalBlock b, int num, List<DetailTarget> oldList)
{
for (int i = 0; i < detailTargets.Count; i++)
if (detailTargets[i].Num == num) return;
IMyTextSurface s = null;
IMyTextSurfaceProvider p = b as IMyTextSurfaceProvider;
if (p != null && p.SurfaceCount > 0) s = p.GetSurface(0);
if (s == null) s = b as IMyTextSurface;
if (s == null) return;
PrepareSurface(s);
DetailTarget t = new DetailTarget();
t.Num = num;
t.Surface = s;
t.Cat = DefaultDetailCat(num);
for (int i = 0; i < oldList.Count; i++)
{
if (oldList[i].Num != num) continue;
t.Cat = oldList[i].Cat;
for (int k = 0; k < t.Sub.Length && k < oldList[i].Sub.Length; k++) t.Sub[k] = oldList[i].Sub[k];
break;
}
detailTargets.Add(t);
}
int DefaultDetailCat(int num)
{
if (num == 1) return 0; // Components
if (num == 2) return 2; // Refined
if (num == 3) return 1; // Ammo
return 3; // Supply
}
void SortDetailTargets()
{
for (int i = 0; i < detailTargets.Count - 1; i++)
{
int best = i;
for (int j = i + 1; j < detailTargets.Count; j++)
if (detailTargets[j].Num < detailTargets[best].Num) best = j;
if (best == i) continue;
DetailTarget tmp = detailTargets[i];
detailTargets[i] = detailTargets[best];
detailTargets[best] = tmp;
}
}
DetailTarget ActiveDetailTarget()
{
if (detailFocusIndex < 0 || detailFocusIndex >= detailTargets.Count) return null;
return detailTargets[detailFocusIndex];
}
void CycleFocusTarget()
{
if (detailTargets.Count == 0)
{
detailFocusIndex = -1;
return;
}
detailFocusIndex++;
if (detailFocusIndex >= detailTargets.Count) detailFocusIndex = -1;
}

bool OfficerSeatControlled()
{
IMyShipController c = oBlock as IMyShipController;
return c != null && c.IsUnderControl;
}

bool IdleCycleDetailPages()
{
if (OfficerSeatControlled() || editHold > 0)
{
detailIdleCycleTick = 0;
return false;
}
int cycleTicks = DetailIdleCycleTicks();
if (cycleTicks <= 0)
{
    detailIdleCycleTick = 0;
    return false;
}
detailIdleCycleTick++;
if (detailIdleCycleTick < cycleTicks) return false;
detailIdleCycleTick = 0;
bool changed = false;
for (int i = 0; i < detailTargets.Count; i++)
{
DetailTarget t = detailTargets[i];
if (t == null) continue;
if (t.Cat < 0 || t.Cat > 3) continue;
int total = DetailPageCount(t.Cat);
if (total <= 1) continue;
t.Sub[t.Cat] = (t.Sub[t.Cat] + 1) % total;
changed = true;
}
return changed;
}
void PrepareSurface(IMyTextSurface s)
{
s.ContentType = ContentType.SCRIPT;
s.ScriptBackgroundColor = C_BG;
s.ScriptForegroundColor = C_TEXT;
}
bool HandleOfficerCommand(string arg)
{
displayOnlyCommand = false;
if (arg == null) arg = "";
arg = arg.Trim().ToUpper();
if (arg.StartsWith("IMS "))
arg = arg.Substring(4).Trim();
if (arg == "FOCUS" || arg == "LCD")
{
CycleFocusTarget();
displayOnlyCommand = true;
return true;
}
if (detailFocusIndex >= 0 && HandleDetailCommand(arg))
{
displayOnlyCommand = true;
return true;
}
if (arg == "OFFICER" || arg == "CONSOLE")
return true;
if (arg == "PAGE NEXT" || arg == "PAGENEXT" || arg == "PAGE")
{
SaveOfficerField();
oPage++;
if (oPage >= oPages.Length) oPage = 0;
RestoreOfficerField();
return true;
}
if (arg == "PAGE PREV" || arg == "PAGEPREV")
{
SaveOfficerField();
oPage--;
if (oPage < 0) oPage = oPages.Length - 1;
RestoreOfficerField();
return true;
}
if (arg == "NEXT" || arg == "RIGHT")
{
MoveOfficerField(1);
SaveOfficerField();
return true;
}
if (arg == "PREV" || arg == "LEFT")
{
MoveOfficerField(-1);
SaveOfficerField();
return true;
}
if (arg == "INC" || arg == "UP" || arg == "+")
{
AdjustOfficerValue(1);
SaveOfficerData();
return true;
}
if (arg == "DEC" || arg == "DOWN" || arg == "-")
{
AdjustOfficerValue(-1);
SaveOfficerData();
return true;
}
if (arg == "STEP" || arg == "SCALE")
{
oStep++;
if (oStep >= officerSteps.Length) oStep = 0;
SaveOfficerData();
return true;
}
return false;
}
bool HandleDetailCommand(string arg)
{
DetailTarget t = ActiveDetailTarget();
if (t == null) return false;
if (arg == "PAGE NEXT" || arg == "PAGENEXT" || arg == "PAGE")
{
t.Cat++;
if (t.Cat > 3) t.Cat = 0;
return true;
}
if (arg == "PAGE PREV" || arg == "PAGEPREV")
{
t.Cat--;
if (t.Cat < 0) t.Cat = 3;
return true;
}
int pageCount = DetailPageCount(t.Cat);
if (arg == "NEXT" || arg == "RIGHT")
{
t.Sub[t.Cat]++;
if (t.Sub[t.Cat] >= pageCount) t.Sub[t.Cat] = 0;
return true;
}
if (arg == "PREV" || arg == "LEFT")
{
t.Sub[t.Cat]--;
if (t.Sub[t.Cat] < 0) t.Sub[t.Cat] = pageCount - 1;
return true;
}
if (arg == "INC" || arg == "DEC" || arg == "UP" || arg == "DOWN" || arg == "+" || arg == "-" || arg == "STEP" || arg == "SCALE")
return true;
return false;
}
int DetailDataPage(int cat)
{
if (cat == 0) return 2;
if (cat == 1) return 1;
if (cat == 2) return 3;
return 0;
}
string DetailName(int cat)
{
if (cat == 0) return "COMPONENTS";
if (cat == 1) return "AMMO";
if (cat == 2) return "REFINED";
return "SUPPLY";
}
int DetailItemCount(int p)
{
if (p == 3 && !ShowGravelOnDetail()) return oItems[p].Length - 1;
return oItems[p].Length;
}
bool ShowGravelOnDetail()
{
return oCur[6].Length < 4 || oCur[6][3] > 0.5;
}
int DetailPageCount(int cat)
{
int p = DetailDataPage(cat);
int count = DetailItemCount(p);
int pages = (count + 7) / 8;
return pages < 1 ? 1 : pages;
}
int DetailIdleCycleTicks()
{
int sec = 0;
if (oCur.Length > 6 && oCur[6].Length > 4) sec = (int)Math.Round(oCur[6][4]);
if (sec <= 0) return 0;
if (sec > 600) sec = 600;
return sec * 6;
}
void SaveOfficerField()
{
if (oPage >= 0 && oPage < oRemember.Length)
oRemember[oPage] = oField;
SaveOfficerData();
}
void RestoreOfficerField()
{
if (oPage >= 0 && oPage < oRemember.Length)
oField = oRemember[oPage];
NormalizeOfficerField();
}
void MoveOfficerField(int dir)
{
int max = OfficerFieldCount();
if (max <= 0) { oField = 0; return; }
for (int n = 0; n < max; n++)
{
oField += dir;
if (oField >= max) oField = 0;
if (oField < 0) oField = max - 1;
if (!OfficerCurrentFieldDisabled()) return;
}
}
void NormalizeOfficerField()
{
int max = OfficerFieldCount();
if (max <= 0) { oField = 0; return; }
if (oField < 0) oField = 0;
if (oField >= max) oField = max - 1;
if (OfficerCurrentFieldDisabled()) MoveOfficerField(1);
}
bool OfficerCurrentFieldDisabled()
{
if (oPage < 0 || oPage >= oItems.Length) return false;
return OfficerModeRowDisabled(oPage, OfficerSelectedItem());
}
bool OfficerModeRowDisabled(int p, int item)
{
return p == 4 && item > 0 && item < oItems[p].Length && oCur[4][0] < 0.5;
}
int OfficerFieldCount()
{
int p = oPage;
if (p < 0 || p >= oItems.Length) return 1;
if (oKind[p] == OFFICER_MODE_PAGE)
return oItems[p].Length;
return oItems[p].Length * 2;
}
int OfficerSelectedItem()
{
if (oKind[oPage] == OFFICER_MODE_PAGE)
return oField;
return oField / 2;
}
bool OfficerSelectedIsMax()
{
if (oKind[oPage] == OFFICER_MODE_PAGE) return false;
return (oField % 2) == 1;
}
void AdjustOfficerValue(int dir)
{
NormalizeOfficerField();
int p = oPage;
int item = OfficerSelectedItem();
if (OfficerModeRowDisabled(p, item)) return;
if (p < 0 || p >= oItems.Length) return;
if (item < 0 || item >= oItems[p].Length) return;
if (oKind[p] == OFFICER_MODE_PAGE)
{
string name = oItems[p][item];
int v = (int)Math.Round(oCur[p][item]);
int min = (int)Math.Round(oMin[p][item]);
int max = (int)Math.Round(oMax[p][item]);
if (name == "LCD ROTATION")
{
    int lcdStepSeconds = oStep == 0 ? 1 : (oStep == 1 ? 10 : 100);
    v += dir * lcdStepSeconds;
    if (v < min) v = min;
    if (v > max) v = max;
    detailIdleCycleTick = 0;
}
else
{
    v += dir;
    if (v > max) v = min;
    if (v < min) v = max;
}
oCur[p][item] = v;
if (name == "BUILD QUEUE SOURCE") BqsLocalChanged();
SaveOfficerField();
return;
}
double officerStepValue = OfficerStep(p, oItems[p][item]);
if (OfficerSelectedIsMax())
{
oMax[p][item] += officerStepValue * dir;
if (oMax[p][item] < 0) oMax[p][item] = 0;
if (oMax[p][item] < oMin[p][item]) oMin[p][item] = oMax[p][item];
}
else
{
oMin[p][item] += officerStepValue * dir;
if (oMin[p][item] < 0) oMin[p][item] = 0;
if (oMin[p][item] > oMax[p][item]) oMax[p][item] = oMin[p][item];
}
SaveOfficerField();
}
double OfficerStep(int page, string item)
{
if (oStep < 0 || oStep >= officerSteps.Length)
oStep = 1;
if (page == 0)
{
if (item == "ICE")
{
if (oStep == 0) return 100;
if (oStep == 1) return 1000;
return 10000;
}
if (item == "URANIUM")
{
if (oStep == 0) return 1;
if (oStep == 1) return 10;
return 100;
}
if (item == "BATTERY")
{
if (oStep == 0) return 1;
if (oStep == 1) return 5;
return 10;
}
if (oStep == 0) return 1;
if (oStep == 1) return 5;
return 10;
}
if (page == 1)
{
if (oStep == 0) return 1;
if (oStep == 1) return 10;
return 100;
}
if (page == 2)
{
if (oStep == 0) return 10;
if (oStep == 1) return 100;
return 1000;
}
if (page == 3)
{
if (item == "URANIUM")
{
if (oStep == 0) return 1;
if (oStep == 1) return 10;
return 100;
}
if (oStep == 0) return 100;
if (oStep == 1) return 1000;
return 10000;
}
return 1;
}
string OfficerStepTiny()
{
int p=oPage,item=OfficerSelectedItem();
if(p<0||p>=oItems.Length||item<0||item>=oItems[p].Length)return "";
string n=oItems[p][item];
if(oKind[p]==OFFICER_MODE_PAGE)
{
    if(n=="LCD ROTATION") return "STEP " + (oStep==0?"1":(oStep==1?"10":"100")) + "s";
    return "";
}
double st=OfficerStep(p,n);
if(p==0&&(n=="H2"||n=="O2"||n=="BATTERY"))return "STEP "+st.ToString("0")+"%";
if(p==0||p==3)return "STEP "+FormatMass(st);
return "STEP "+st.ToString("0");
}
void UpdateOfficerCurrentFromLive()
{
oCur[0][0] = cached.IceKg;
oCur[0][1] = cached.UraniumKg;
oCur[0][2] = cached.BatteryPercent;
oCur[0][3] = cached.HydrogenPercent;
oCur[0][4] = cached.OxygenPercent;
oCur[1][0] = MapValue(cached.AmmoBySubtype, "NATO_25x184mm");
oCur[1][1] = MapValue(cached.AmmoBySubtype, "RapidFireAutomaticRifleGun_Mag_50rd");
oCur[1][2] = MapValue(cached.AmmoBySubtype, "Missile200mm");
oCur[1][3] = MapValue(cached.AmmoBySubtype, "LargeCalibreAmmo");
oCur[1][4] = MapValue(cached.AmmoBySubtype, "MediumCalibreAmmo");
oCur[1][5] = MapValue(cached.AmmoBySubtype, "AutocannonClip");
oCur[1][6] = MapValue(cached.AmmoBySubtype, "LargeRailgunAmmo") + MapValue(cached.AmmoBySubtype, "SmallRailgunAmmo");
oCur[2][0] = MapValue(cached.ComponentBySubtype, "SteelPlate");
oCur[2][1] = MapValue(cached.ComponentBySubtype, "InteriorPlate");
oCur[2][2] = MapValue(cached.ComponentBySubtype, "Construction");
oCur[2][3] = MapValue(cached.ComponentBySubtype, "Motor");
oCur[2][4] = MapValue(cached.ComponentBySubtype, "Computer");
oCur[2][5] = MapValue(cached.ComponentBySubtype, "Display");
oCur[2][6] = MapValue(cached.ComponentBySubtype, "Girder");
oCur[2][7] = MapValue(cached.ComponentBySubtype, "SmallTube");
oCur[2][8] = MapValue(cached.ComponentBySubtype, "LargeTube");
oCur[2][9] = MapValue(cached.ComponentBySubtype, "MetalGrid");
oCur[2][10] = MapValue(cached.ComponentBySubtype, "BulletproofGlass");
oCur[2][11] = MapValue(cached.ComponentBySubtype, "Medical");
oCur[2][12] = MapValue(cached.ComponentBySubtype, "Detector");
oCur[2][13] = MapValue(cached.ComponentBySubtype, "RadioCommunication");
oCur[2][14] = MapValue(cached.ComponentBySubtype, "PowerCell");
oCur[2][15] = MapValue(cached.ComponentBySubtype, "Reactor");
oCur[2][16] = MapValue(cached.ComponentBySubtype, "Superconductor");
oCur[2][17] = MapValue(cached.ComponentBySubtype, "Thrust");
oCur[3][0] = MapValue(cached.IngotBySubtype, "Iron");
oCur[3][1] = MapValue(cached.IngotBySubtype, "Nickel");
oCur[3][2] = MapValue(cached.IngotBySubtype, "Cobalt");
oCur[3][3] = MapValue(cached.IngotBySubtype, "Silicon");
oCur[3][4] = MapValue(cached.IngotBySubtype, "Magnesium");
oCur[3][5] = MapValue(cached.IngotBySubtype, "Silver");
oCur[3][6] = MapValue(cached.IngotBySubtype, "Gold");
oCur[3][7] = MapValue(cached.IngotBySubtype, "Platinum");
oCur[3][8] = MapValue(cached.IngotBySubtype, "Stone");
}

const string PB1_EXPORT_BEGIN = "# IMS_PB1_EXPORT_BEGIN";
const string PB1_EXPORT_END = "# IMS_PB1_EXPORT_END";
const string WORKER_STATUS_BEGIN = "# IMS_WORKER_STATUS_BEGIN";
const string WORKER_STATUS_END = "# IMS_WORKER_STATUS_END";
const string SERVICE_TARGETS_BEGIN = "# MB1_IMS_SERVICE_TARGETS_BEGIN";
const string SERVICE_TARGETS_END = "# MB1_IMS_SERVICE_TARGETS_END";
const string OB1_STATUS_BEGIN = "# OB1_IMS_STATION_STATUS_BEGIN";
const string OB1_STATUS_END = "# OB1_IMS_STATION_STATUS_END";

string BuildQueueSourceText()
{
int sp = 6;
int bi = -1;
for (int i = 0; i < oItems[sp].Length; i++) if (oItems[sp][i] == "BUILD QUEUE SOURCE") { bi = i; break; }
if (bi < 0) return "AUTO";
return FormatOfficerMode("BUILD QUEUE SOURCE", oCur[sp][bi]);
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
if (ob1BqsSeq > bqsClusterSeq)
{
bqsClusterTag = ob1BqsTag;
bqsClusterSeq = ob1BqsSeq;
bqsClusterSrc = ob1BqsSrc.Length > 0 ? ob1BqsSrc : "OB1";
}
}
void ImportOb1BuildQueuePacket()
{
ob1BqsLine = "OB1 BQS offline";
ob1BqsSeq = -1;
ob1BqsTag = "AUTO";
ob1BqsSrc = "OB1";
if (ob1PB1 == null) return;
string block = ExtractDataBlock(ob1PB1.CustomData, OB1_STATUS_BEGIN, OB1_STATUS_END);
if (block.Length == 0) { ob1BqsLine = "OB1 BQS missing"; return; }
string[] lines = block.Split('\n');
for (int i = 0; i < lines.Length; i++)
{
string line = lines[i].Trim();
int eq = line.IndexOf('=');
if (eq < 0) continue;
string k = line.Substring(0, eq).Trim();
string v = line.Substring(eq + 1).Trim();
int n;
if (k.Equals("BuildQueueClusterTag", StringComparison.OrdinalIgnoreCase)) ob1BqsTag = v;
else if (k.Equals("BuildQueueClusterSeq", StringComparison.OrdinalIgnoreCase) && int.TryParse(v, out n)) ob1BqsSeq = n;
else if (k.Equals("BuildQueueClusterSource", StringComparison.OrdinalIgnoreCase)) ob1BqsSrc = v;
else if (k.Equals("BuildQueueSourceTag", StringComparison.OrdinalIgnoreCase) && ob1BqsTag == "AUTO") ob1BqsTag = v;
else if (k.Equals("BuildQueueSourceSeq", StringComparison.OrdinalIgnoreCase) && int.TryParse(v, out n) && ob1BqsSeq < 0) ob1BqsSeq = n;
}
ob1BqsLine = "OB1 BQS " + ob1BqsTag + " seq " + ob1BqsSeq + " src " + ob1BqsSrc;
}
void ExportWorkerPacket()
{
ExportServiceTargetsPacket();
if (workerPB == null) { workerState = "OFFLINE"; workerFault = ""; return; }
ImportWorkerStatus();
StringBuilder sb = new StringBuilder();
workerSeq++;
sb.AppendLine(PB1_EXPORT_BEGIN);
sb.AppendLine("Seq=" + workerSeq.ToString("0"));
sb.AppendLine("Console.EditHold=" + (editHold > 0 ? "1" : "0"));
sb.AppendLine("IMS.EntityTag=" + IMS_ENTITY_TAG);
sb.AppendLine("IMS.EntityLabel=" + IMS_ENTITY_LABEL);
UpdateBuildQueueCluster();
sb.AppendLine("BuildQueue.Source=" + BuildQueueSourceText());
sb.AppendLine("BuildQueue.SourceTag=" + BuildQueueSourceTag());
sb.AppendLine("BuildQueue.SourceSeq=" + bqsLocalSeq.ToString("0"));
sb.AppendLine("BuildQueue.SourceEntity=" + IMS_ENTITY_TAG);
sb.AppendLine("BuildQueue.ClusterTag=" + bqsClusterTag);
sb.AppendLine("BuildQueue.ClusterSeq=" + bqsClusterSeq.ToString("0"));
sb.AppendLine("BuildQueue.ClusterSource=" + bqsClusterSrc);
sb.AppendLine("Policy.TolerancePercent=5");
for (int p = 0; p < oPages.Length; p++)
{
for (int i = 0; i < oItems[p].Length; i++)
{
string key = SafeKey(oPages[p]) + "." + SafeKey(oItems[p][i]);
if (oKind[p] == OFFICER_MODE_PAGE)
sb.AppendLine("Mode." + key + "=" + oCur[p][i].ToString("0"));
else
{
sb.AppendLine("Target." + key + ".Min=" + oMin[p][i].ToString("0.###"));
sb.AppendLine("Target." + key + ".Max=" + oMax[p][i].ToString("0.###"));
sb.AppendLine("Current." + key + "=" + oCur[p][i].ToString("0.###"));
}
}
}
sb.AppendLine(PB1_EXPORT_END);
workerPB.CustomData = ReplaceDataBlock(workerPB.CustomData, PB1_EXPORT_BEGIN, PB1_EXPORT_END, sb.ToString());
}
void ExportServiceTargetsPacket()
{
StringBuilder sb = new StringBuilder();
sb.AppendLine(SERVICE_TARGETS_BEGIN);
sb.AppendLine("Packet=MB1_IMS_SERVICE_TARGETS");
sb.AppendLine("Version=V56B");
sb.AppendLine("InstallTag=" + INSTALL_TAG);
sb.AppendLine("IMS.EntityTag=" + IMS_ENTITY_TAG);
sb.AppendLine("IMS.EntityLabel=" + IMS_ENTITY_LABEL);
UpdateBuildQueueCluster();
sb.AppendLine("BuildQueue.Source=" + BuildQueueSourceText());
sb.AppendLine("BuildQueue.SourceTag=" + BuildQueueSourceTag());
sb.AppendLine("BuildQueue.SourceSeq=" + bqsLocalSeq.ToString("0"));
sb.AppendLine("BuildQueue.SourceEntity=" + IMS_ENTITY_TAG);
sb.AppendLine("BuildQueue.ClusterTag=" + bqsClusterTag);
sb.AppendLine("BuildQueue.ClusterSeq=" + bqsClusterSeq.ToString("0"));
sb.AppendLine("BuildQueue.ClusterSource=" + bqsClusterSrc);
sb.AppendLine("Seq=" + workerSeq.ToString("0"));
sb.AppendLine("Note=Read-only public target/current packet for station/base service displays.");
for (int p = 0; p < oPages.Length; p++)
{
for (int i = 0; i < oItems[p].Length; i++)
{
string page = SafeKey(oPages[p]);
string item = SafeKey(oItems[p][i]);
if (oKind[p] == OFFICER_MODE_PAGE)
{
sb.AppendLine("MODE|" + page + "|" + item + "|value|" + oCur[p][i].ToString("0.###"));
}
else
{
double cur = oCur[p][i];
double min = oMin[p][i];
double max = oMax[p][i];
double need = min - cur;
if (need < 0) need = 0;
double surplus = cur - max;
if (surplus < 0) surplus = 0;
sb.AppendLine("TGT|" + page + "|" + item + "|min|" + min.ToString("0.###") + "|max|" + max.ToString("0.###") + "|cur|" + cur.ToString("0.###") + "|need|" + need.ToString("0.###") + "|surplus|" + surplus.ToString("0.###"));
}
}
}
sb.AppendLine(SERVICE_TARGETS_END);
Me.CustomData = ReplaceDataBlock(Me.CustomData, SERVICE_TARGETS_BEGIN, SERVICE_TARGETS_END, sb.ToString());
}
void ImportWorkerStatus()
{
workerState = "ONLINE";
workerFault = "";
if (workerPB == null) { workerState = "OFFLINE"; return; }
string block = ExtractDataBlock(workerPB.CustomData, WORKER_STATUS_BEGIN, WORKER_STATUS_END);
if (block.Length == 0) return;
string[] lines = block.Split('\n');
for (int i = 0; i < lines.Length; i++)
{
string line = lines[i].Trim();
int eq = line.IndexOf('=');
if (eq < 0) continue;
string key = line.Substring(0, eq).Trim();
string val = line.Substring(eq + 1).Trim();
if (key.Equals("State", StringComparison.OrdinalIgnoreCase)) workerState = val;
else if (key.Equals("Fault", StringComparison.OrdinalIgnoreCase)) workerFault = val;
}
}
string ReplaceDataBlock(string existing, string begin, string end, string block)
{
if (existing == null) existing = "";
int start = existing.IndexOf(begin);
int stop = existing.IndexOf(end);
if (start >= 0 && stop > start)
{
stop += end.Length;
existing = existing.Remove(start, stop - start).Trim();
}
if (existing.Length == 0) return block.Trim();
return existing + "\n\n" + block.Trim();
}
string ExtractDataBlock(string data, string begin, string end)
{
if (data == null) return "";
int start = data.IndexOf(begin);
int stop = data.IndexOf(end);
if (start < 0 || stop <= start) return "";
start += begin.Length;
return data.Substring(start, stop - start).Trim();
}
string SafeKey(string text)
{
if (text == null) return "";
return text.Replace(" ", "_").Replace("/", "_").Replace("-", "_").Replace(".", "_");
}

const string OFFICER_DATA_BEGIN = "# IMS_OFFICER_BEGIN";
const string OFFICER_DATA_END = "# IMS_OFFICER_END";
void LoadOfficerData()
{
string data = Me.CustomData;
string[] lines = data.Split('\n');
for (int i = 0; i < lines.Length; i++)
{
string line = lines[i].Trim();
if (line.Length == 0 || line.StartsWith("#")) continue;
int eq = line.IndexOf('=');
if (eq < 0) continue;
string key = line.Substring(0, eq).Trim();
string val = line.Substring(eq + 1).Trim();
double d;
int n;
if (key == "OfficerPage" && int.TryParse(val, out n)) oPage = ClampInt(n, 0, oPages.Length - 1);
else if (key == "OfficerField" && int.TryParse(val, out n)) oField = n;
else if (key == "OfficerStepIndex" && int.TryParse(val, out n)) oStep = ClampInt(n, 0, officerSteps.Length - 1);
else if (key == "IceProcessingModeV2") iceProcessingModeV2 = true;
else if (key.StartsWith("Remember.") && int.TryParse(val, out n))
{
int p = PageIndexByName(key.Substring(9));
if (p >= 0) oRemember[p] = n;
}
else if (key.EndsWith(".Min") && double.TryParse(val, out d))
{
SetOfficerTargetByKey(key.Substring(0, key.Length - 4), false, d);
}
else if (key.EndsWith(".Max") && double.TryParse(val, out d))
{
SetOfficerTargetByKey(key.Substring(0, key.Length - 4), true, d);
}
else if (key.StartsWith("Mode.") && double.TryParse(val, out d))
{
SetOfficerModeByKey(key.Substring(5), d);
}
}
if (!iceProcessingModeV2 && oCur.Length > 5 && oCur[5].Length > 1)
{
// Legacy value was 0=AUTO, 1=MIN. V2 is 0=OFF, 1=MIN, 2=AUTO.
oCur[5][1] = oCur[5][1] > 0.5 ? 1 : 2;
iceProcessingModeV2 = true;
}
RestoreOfficerField();
}
void SaveOfficerData()
{
StringBuilder sb = new StringBuilder();
sb.AppendLine(OFFICER_DATA_BEGIN);
sb.AppendLine("OfficerPage=" + oPage.ToString("0"));
sb.AppendLine("OfficerField=" + oField.ToString("0"));
sb.AppendLine("OfficerStepIndex=" + oStep.ToString("0"));
sb.AppendLine("IceProcessingModeV2=1");
for (int p = 0; p < oPages.Length; p++)
sb.AppendLine("Remember." + oPages[p] + "=" + oRemember[p].ToString("0"));
for (int p = 0; p < oPages.Length; p++)
for (int i = 0; i < oItems[p].Length; i++)
{
string key = oPages[p] + "." + oItems[p][i];
if (oKind[p] == OFFICER_MODE_PAGE)
sb.AppendLine("Mode." + key + "=" + oCur[p][i].ToString("0"));
else
{
sb.AppendLine(key + ".Min=" + oMin[p][i].ToString("0.###"));
sb.AppendLine(key + ".Max=" + oMax[p][i].ToString("0.###"));
}
}
sb.AppendLine(OFFICER_DATA_END);
Me.CustomData = ReplaceDataBlock(Me.CustomData, OFFICER_DATA_BEGIN, OFFICER_DATA_END, sb.ToString());
}
int PageIndexByName(string page)
{
for (int i = 0; i < oPages.Length; i++)
if (oPages[i].Equals(page, StringComparison.OrdinalIgnoreCase)) return i;
return -1;
}
void SetOfficerTargetByKey(string key, bool max, double value)
{
for (int p = 0; p < oPages.Length; p++)
{
if (oKind[p] != OFFICER_TARGET_PAGE) continue;
string prefix = oPages[p] + ".";
if (!key.StartsWith(prefix)) continue;
string itemName = key.Substring(prefix.Length);
for (int i = 0; i < oItems[p].Length; i++)
{
if (!oItems[p][i].Equals(itemName, StringComparison.OrdinalIgnoreCase)) continue;
if (max) oMax[p][i] = value;
else oMin[p][i] = value;
return;
}
}
}
void SetOfficerModeByKey(string key, double value)
{
for (int p = 0; p < oPages.Length; p++)
{
if (oKind[p] != OFFICER_MODE_PAGE) continue;
string prefix = oPages[p] + ".";
if (!key.StartsWith(prefix)) continue;
string itemName = key.Substring(prefix.Length);
for (int i = 0; i < oItems[p].Length; i++)
{
if (!oItems[p][i].Equals(itemName, StringComparison.OrdinalIgnoreCase)) continue;
if (value < oMin[p][i]) value = oMin[p][i];
if (value > oMax[p][i]) value = oMax[p][i];
oCur[p][i] = value;
return;
}
}
}
int ClampInt(int v, int min, int max)
{
if (v < min) return min;
if (v > max) return max;
return v;
}
void DrawOfficerConsole()
{
if (oSurfaces.Count == 0) return;
DrawOfficerMain(oSurfaces[0]);
if (oSurfaces.Count > 1) DrawOfficerPages(oSurfaces[1]);
if (oSurfaces.Count > 2) DrawOfficerGraph(oSurfaces[2]);
if (oSurfaces.Count > 3) DrawOfficerWorkerIcon(oSurfaces[3]);
if (oSurfaces.Count > 4) DrawOfficerHelp(oSurfaces[4]);
}
int OfficerSubPage()
{
int item = OfficerSelectedItem();
if (item < 0) item = 0;
return item / OFFICER_ROWS;
}
int OfficerTotalSubPages()
{
int p = oPage;
if (p < 0 || p >= oItems.Length) return 1;
int count = oItems[p].Length;
int pages = (count + OFFICER_ROWS - 1) / OFFICER_ROWS;
if (pages < 1) pages = 1;
return pages;
}
void DrawOfficerMain(IMyTextSurface s)
{
NormalizeOfficerField();
Vector2 size = s.SurfaceSize;
Vector2 origin = GetOrigin(s);
float w = size.X;
float h = size.Y;
float u = Math.Min(w / 512f, h / 307f);
if (u < 0.45f) u = 0.45f;
int p = oPage;
int sub = OfficerSubPage();
int total = OfficerTotalSubPages();
int startItem = sub * OFFICER_ROWS;
int endItem = startItem + OFFICER_ROWS;
if (endItem > oItems[p].Length) endItem = oItems[p].Length;
using (MySpriteDrawFrame frame = s.DrawFrame())
{
AddRect(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w, h), C_BG);
AddFrame(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w - 12f * u, h - 12f * u), 3f * u, C_CYAN);
AddText(ref frame, oPages[p], origin + new Vector2(22f * u, 16f * u), C_TEXT, 0.76f * u, LEF);
string st=OfficerStepTiny();
string pg=total>1?(sub+1).ToString("0")+"/"+total.ToString("0"):"";
if(st.Length>0)AddText(ref frame,st,origin+new Vector2(w/2f,16f*u),C_MUTED,0.54f*u,CEN);
if(pg.Length>0)AddText(ref frame,pg,origin+new Vector2(w-24f*u,16f*u),C_MUTED,0.54f*u,RIG);
float y0 = 84f * u;
float rowH = 41f * u;
if (oKind[p] == OFFICER_TARGET_PAGE)
{
AddText(ref frame, "MIN", origin + new Vector2(w * 0.585f, 45f * u), C_TEXT, 0.54f * u, CEN);
AddText(ref frame, "MAX", origin + new Vector2(w * 0.825f, 45f * u), C_TEXT, 0.54f * u, CEN);
}
for (int item = startItem; item < endItem; item++)
{
if (oKind[p] == OFFICER_MODE_PAGE)
DrawOfficerModeRow(ref frame, origin, w, y0 + (item - startItem) * rowH, p, item, u);
else
DrawOfficerTargetRow(ref frame, origin, w, y0 + (item - startItem) * rowH, p, item, u);
}
if (detailFocusIndex >= 0) DrawDetailFocusOverlay(ref frame, origin, w, h, u);
}
}
void DrawDetailFocusOverlay(ref MySpriteDrawFrame frame, Vector2 origin, float w, float h, float u)
{
Vector2 c = origin + new Vector2(w / 2f, h / 2f);
AddRect(ref frame, c, new Vector2(w * 0.82f, h * 0.62f), new Color(1, 8, 11));
AddFrame(ref frame, c, new Vector2(w * 0.82f, h * 0.62f), 3f * u, C_GREEN);
AddText(ref frame, "LCD CONTROL", origin + new Vector2(w / 2f, h * 0.38f), C_GREEN, 0.95f * u, CEN);
AddText(ref frame, DetailTitle(), origin + new Vector2(w / 2f, h * 0.53f), C_TEXT, 0.62f * u, CEN);
AddText(ref frame, "FOCUS TO RETURN", origin + new Vector2(w / 2f, h * 0.64f), C_MUTED, 0.44f * u, CEN);
AddText(ref frame, "CYCLE " + DetailCycleSecondsText(), origin + new Vector2(w / 2f, h * 0.72f), C_MUTED, 0.38f * u, CEN);
}
string DetailCycleSecondsText()
{
int sec = 0;
if (oCur.Length > 6 && oCur[6].Length > 4) sec = (int)Math.Round(oCur[6][4]);
return sec <= 0 ? "OFF" : sec.ToString("0") + "s";
}
string DetailTitle()
{
DetailTarget t = ActiveDetailTarget();
if (t == null) return "CONSOLE";
return "IMS-DETAIL" + t.Num.ToString("0") + "  " + DetailName(t.Cat) + " " + (t.Sub[t.Cat] + 1).ToString("0") + "/" + DetailPageCount(t.Cat).ToString("0");
}
void DrawDetailLCDs()
{
for (int i = 0; i < detailTargets.Count; i++) DrawDetailLCD(detailTargets[i]);
}
void DrawDetailLCD(DetailTarget t)
{
if (t == null || t.Surface == null) return;
IMyTextSurface s = t.Surface;
Vector2 size = s.SurfaceSize;
Vector2 origin = GetOrigin(s);
float w = size.X, h = size.Y;
float u = Math.Min(w / 512f, h / 512f);
if (u < 0.45f) u = 0.45f;
if (t.Cat < 0 || t.Cat > 3) t.Cat = 0;
int p = DetailDataPage(t.Cat);
int page = t.Sub[t.Cat];
int total = DetailPageCount(t.Cat);
if (page >= total) { page = total - 1; t.Sub[t.Cat] = page; }
if (page < 0) { page = 0; t.Sub[t.Cat] = 0; }
int first = page * 8;
using (MySpriteDrawFrame frame = s.DrawFrame())
{
AddRect(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w, h), C_BG);
bool focused = detailFocusIndex >= 0 && detailFocusIndex < detailTargets.Count && detailTargets[detailFocusIndex].Num == t.Num;
AddFrame(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w - 14f * u, h - 14f * u), 3f * u, C_CYAN);
AddText(ref frame, "IMS " + DetailName(t.Cat) + " STOCK", origin + new Vector2(24f * u, 18f * u), C_TEXT, 0.62f * u, LEF);
AddText(ref frame, "D" + t.Num.ToString("0") + "  " + (page + 1).ToString("0") + "/" + total.ToString("0"), origin + new Vector2(w - 24f * u, 18f * u), C_MUTED, 0.50f * u, RIG);
float top = 62f * u;
float rowH = (h - 92f * u) / 8f;
for (int r = 0; r < 8; r++)
{
int item = first + r;
if (item >= DetailItemCount(p)) break;
DrawDetailBarRow(ref frame, origin, w, top + rowH * r, rowH, p, item, u);
}
if (focused) DrawDetailFocusRails(ref frame, origin, w, h, u);
}
}
void DrawDetailFocusRails(ref MySpriteDrawFrame frame, Vector2 origin, float w, float h, float u)
{
Color col = C_TEXT;
float yTop = origin.Y + h * 0.035f, yBot = origin.Y + h * 0.965f;
float x1 = origin.X + w * 0.175f, x2 = origin.X + w * 0.825f;
float wing = 18f * u, outY = 12f * u;
float lw = 1.55f * u;
Line(ref frame, new Vector2(x1, yTop), new Vector2(x2, yTop), lw, col);
Line(ref frame, new Vector2(x1, yBot), new Vector2(x2, yBot), lw, col);
// Top: /________\ ; Bottom: \________/ .
Line(ref frame, new Vector2(x1 - wing, yTop + outY), new Vector2(x1, yTop), lw, col);
Line(ref frame, new Vector2(x2, yTop), new Vector2(x2 + wing, yTop + outY), lw, col);
Line(ref frame, new Vector2(x1 - wing, yBot - outY), new Vector2(x1, yBot), lw, col);
Line(ref frame, new Vector2(x2, yBot), new Vector2(x2 + wing, yBot - outY), lw, col);
}

void Line(ref MySpriteDrawFrame frame, Vector2 a, Vector2 b, float lineWidth, Color col)
{
Vector2 mid = (a + b) * 0.5f;
Vector2 d = b - a;
float len = d.Length();
if (len < 0.01f) return;
float rot = (float)Math.Atan2(d.Y, d.X);
frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", mid, new Vector2(len, lineWidth), col, null, TextAlignment.CENTER, rot));
}
void DrawDetailBarRow(ref MySpriteDrawFrame frame, Vector2 origin, float w, float y, float rowH, int p, int item, float u)
{
string name = ShortOfficerName(oItems[p][item]);
double cur = oCur[p][item], min = oMin[p][item], max = oMax[p][item];
Color c = TargetStockColor(cur, min);
float left = 26f * u;
float right = w - 26f * u;
Vector2 row = origin + new Vector2(w / 2f, y + rowH * 0.50f);
AddRect(ref frame, row, new Vector2(w - 34f * u, rowH - 5f * u), new Color(4, 13, 18));
AddRect(ref frame, origin + new Vector2(left + 5f * u, y + rowH * 0.50f), new Vector2(8f * u, rowH - 13f * u), c);
AddText(ref frame, name, origin + new Vector2(left + 20f * u, y + rowH * 0.22f), C_TEXT, 0.46f * u, LEF);
DrawBar(ref frame, origin + new Vector2(w * 0.67f, y + rowH * 0.52f), new Vector2((right - left) * 0.48f, 16f * u), cur, min, max, c, u, 1);
}
void DrawOfficerTargetRow(ref MySpriteDrawFrame frame, Vector2 origin, float w, float y, int p, int item, float u)
{
string name = oItems[p][item];
double current = oCur[p][item];
double min = oMin[p][item];
double max = oMax[p][item];
bool minSel = oField == item * 2;
bool maxSel = oField == item * 2 + 1;
bool rowSel = minSel || maxSel;
Vector2 rowCenter = origin + new Vector2(w / 2f, y);
AddRect(ref frame, rowCenter, new Vector2(w - 28f * u, 39f * u), rowSel ? new Color(13, 44, 50) : new Color(4, 13, 18));
Color state = current < min ? C_YELLOW : C_GREEN;
AddRect(ref frame, rowCenter + new Vector2(-w / 2f + 18f * u, 0), new Vector2(7f * u, 31f * u), state);
AddText(ref frame, ShortOfficerName(name), origin + new Vector2(34f * u, y - 16f * u), C_TEXT, 0.56f * u, LEF);
AddText(ref frame, FormatOfficerValue(p, name, current), origin + new Vector2(w * 0.390f, y - 16f * u), state, 0.54f * u, RIG);
DrawOfficerFieldBox(ref frame, origin + new Vector2(w * 0.585f, y), new Vector2(116f * u, 34f * u), FormatOfficerValue(p, name, min), minSel, u);
DrawOfficerFieldBox(ref frame, origin + new Vector2(w * 0.825f, y), new Vector2(116f * u, 34f * u), FormatOfficerValue(p, name, max), maxSel, u);
}
void DrawOfficerFieldBox(ref MySpriteDrawFrame frame, Vector2 center, Vector2 size, string value, bool selected, float u)
{
Color bg = selected ? new Color(25, 70, 76) : new Color(2, 11, 15);
Color fg = selected ? Color.Black : C_MUTED;
AddRect(ref frame, center, size, bg);
if (selected) AddFrame(ref frame, center, size, 2f * u, C_GREEN);
AddText(ref frame, value, center + new Vector2(0, -14f * u), fg, 0.50f * u, CEN);
}
void DrawOfficerModeRow(ref MySpriteDrawFrame frame, Vector2 origin, float w, float y, int p, int item, float u)
{
string name = oItems[p][item];
double value = oCur[p][item];
bool disabled = OfficerModeRowDisabled(p, item);
bool selected = oField == item && !disabled;
Vector2 rowCenter = origin + new Vector2(w / 2f, y);
AddRect(ref frame, rowCenter, new Vector2(w - 28f * u, 39f * u), selected ? new Color(13, 44, 50) : (disabled ? new Color(2, 8, 10) : new Color(4, 13, 18)));
Color state = disabled ? C_DIM : OfficerModeColor(name, value);
AddRect(ref frame, rowCenter + new Vector2(-w / 2f + 18f * u, 0), new Vector2(7f * u, 31f * u), state);
AddText(ref frame, ShortOfficerName(name), origin + new Vector2(34f * u, y - 16f * u), disabled ? C_DIM : C_TEXT, 0.56f * u, LEF);
Vector2 box = origin + new Vector2(w * 0.76f, y);
AddRect(ref frame, box, new Vector2(190f * u, 34f * u), selected ? new Color(25, 70, 76) : (disabled ? new Color(1, 6, 8) : new Color(2, 11, 15)));
if (selected) AddFrame(ref frame, box, new Vector2(190f * u, 34f * u), 2f * u, C_GREEN);
AddText(ref frame, DisplayOfficerMode(name, value), box + new Vector2(0, -13f * u), selected ? Color.Black : state, 0.52f * u, CEN);
}
void DrawOfficerPages(IMyTextSurface s)
{
Vector2 size = s.SurfaceSize;
Vector2 origin = GetOrigin(s);
float w = size.X;
float h = size.Y;
float u = Math.Min(w / 256f, h / 171f);
if (u < 0.45f) u = 0.45f;
int per = 6;
int sub = oPage / per;
int total = (oPages.Length + per - 1) / per;
int first = sub * per;
int last = first + per;
if (last > oPages.Length) last = oPages.Length;
using (MySpriteDrawFrame frame = s.DrawFrame())
{
AddRect(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w, h), C_BG);
AddFrame(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w - 8f * u, h - 8f * u), 2f * u, C_CYAN);
AddText(ref frame, "PAGES", origin + new Vector2(w / 2f, 10f * u), C_TEXT, 0.52f * u, CEN);
if (total > 1) AddText(ref frame, (sub + 1).ToString("0") + "/" + total.ToString("0"), origin + new Vector2(w - 12f * u, 10f * u), C_MUTED, 0.38f * u, RIG);
float rowH = 23f * u;
float startY = 33f * u;
for (int i = first; i < last; i++)
{
bool selected = i == oPage;
Vector2 c = origin + new Vector2(w / 2f, startY + (i - first) * rowH);
AddRect(ref frame, c, new Vector2(w - 22f * u, rowH - 4f * u), selected ? new Color(13, 44, 50) : new Color(4, 13, 18));
AddRect(ref frame, c + new Vector2(-w / 2f + 16f * u, 0), new Vector2(5f * u, rowH - 8f * u), selected ? C_GREEN : C_DIM);
AddText(ref frame, oPages[i], c + new Vector2(8f * u, -7f * u), selected ? C_TEXT : C_MUTED, 0.32f * u, CEN);
}
}
}

void DrawOfficerGraph(IMyTextSurface s)
{
Vector2 size = s.SurfaceSize;
Vector2 origin = GetOrigin(s);
float w = size.X;
float h = size.Y;
float u = Math.Min(w / 256f, h / 171f);
if (u < 0.45f) u = 0.45f;
int p = oPage;
int item = OfficerSelectedItem();
if (p < 0 || p >= oItems.Length || item < 0 || item >= oItems[p].Length) return;
string name = oItems[p][item];
using (MySpriteDrawFrame frame = s.DrawFrame())
{
AddRect(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w, h), C_BG);
AddFrame(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w - 8f * u, h - 8f * u), 2f * u, C_CYAN);
AddText(ref frame, ShortOfficerName(name), origin + new Vector2(w / 2f, 16f * u), C_TEXT, 0.55f * u, CEN);
if (oKind[p] == OFFICER_MODE_PAGE)
{
double mode = oCur[p][item];
Color c = OfficerModeColor(name, mode);
AddText(ref frame, DisplayOfficerMode(name, mode), origin + new Vector2(w / 2f, h * 0.48f), c, 0.82f * u, CEN);
DrawBar(ref frame, origin + new Vector2(w / 2f, h * 0.72f), new Vector2(w * 0.70f, 18f * u), mode > 0 ? 1 : 0, 0, 0, c, u, 0);
return;
}
double current = oCur[p][item];
double min = oMin[p][item];
double max = oMax[p][item];
AddText(ref frame, FormatOfficerValue(p, name, current), origin + new Vector2(w / 2f, 42f * u), current < min ? C_YELLOW : C_GREEN, 0.45f * u, CEN);
DrawOfficerMinMaxBar(ref frame, origin + new Vector2(w / 2f, h * 0.62f), new Vector2(w * 0.78f, 31f * u), current, min, max, u);
AddText(ref frame, "MIN " + FormatOfficerValue(p, name, min), origin + new Vector2(w * 0.10f, h - 28f * u), C_YELLOW, 0.30f * u, LEF);
AddText(ref frame, "MAX " + FormatOfficerValue(p, name, max), origin + new Vector2(w * 0.90f, h - 28f * u), C_GREEN, 0.30f * u, RIG);
}
}
void DrawOfficerMinMaxBar(ref MySpriteDrawFrame frame, Vector2 center, Vector2 size, double current, double min, double max, float u)
{
AddRect(ref frame, center, size, new Color(12, 30, 36));
if (max <= 0) max = 1;
double minRatio = min / max;
if (minRatio < 0) minRatio = 0;
if (minRatio > 1) minRatio = 1;
AddRect(ref frame, center + new Vector2((float)(-size.X / 2 + size.X * minRatio / 2), 0), new Vector2((float)(size.X * minRatio), size.Y), new Color(95, 68, 12));
AddRect(ref frame, center + new Vector2((float)(-size.X / 2 + size.X * minRatio + size.X * (1 - minRatio) / 2), 0), new Vector2((float)(size.X * (1 - minRatio)), size.Y), new Color(18, 85, 49));
AddFrame(ref frame, center, size, 2f * u, C_TEXT);
double curRatio = current / max;
if (curRatio < 0) curRatio = 0;
if (curRatio <= 1)
{
float x = center.X - size.X / 2f + (float)(size.X * curRatio);
AddRect(ref frame, new Vector2(x, center.Y), new Vector2(4f * u, size.Y + 12f * u), C_TEXT);
}
else
{
AddRect(ref frame, center + new Vector2(size.X / 2f - 4f * u, 0), new Vector2(7f * u, size.Y + 14f * u), C_TEXT);
AddText(ref frame, "OVER", center + new Vector2(0, -size.Y * 0.95f), C_TEXT, 0.32f * u, CEN);
}
}
void DrawOfficerWorkerIcon(IMyTextSurface s)
{
Vector2 z = s.SurfaceSize;
Vector2 o = GetOrigin(s);
float w = z.X, h = z.Y;
bool pause = oCur[6][1] > 0.5;
using (MySpriteDrawFrame f = s.DrawFrame())
{
AddRect(ref f, o + new Vector2(w / 2f, h / 2f), z, C_BG);
if (pause)
{
AddRect(ref f, o + new Vector2(w * 0.41f, h * 0.5f), new Vector2(w * 0.14f, h * 0.62f), C_GREEN);
AddRect(ref f, o + new Vector2(w * 0.59f, h * 0.5f), new Vector2(w * 0.14f, h * 0.62f), C_GREEN);
}
else
{
MySprite sp = new MySprite(SpriteType.TEXTURE, "Triangle", o + new Vector2(w * 0.54f, h * 0.5f), new Vector2(h * 0.72f, h * 0.72f), C_GREEN);
sp.RotationOrScale = (float)(Math.PI / 2.0);
f.Add(sp);
}
}
}
void DrawOfficerHelp(IMyTextSurface s)
{
Vector2 size = s.SurfaceSize;
Vector2 origin = GetOrigin(s);
float w = size.X;
float h = size.Y;
float u = Math.Min(w / 256f, h / 192f);
if (u < 0.45f) u = 0.45f;
using (MySpriteDrawFrame frame = s.DrawFrame())
{
AddRect(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w, h), C_BG);
AddFrame(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w - 8f * u, h - 8f * u), 2f * u, C_CYAN);
AddText(ref frame, "HOTBAR", origin + new Vector2(w / 2f, 10f * u), C_TEXT, 0.68f * u, CEN);
float y = 50f * u;
float lh = 32f * u;
float lx = w * 0.29f;
float rx = w * 0.71f;
AddText(ref frame, "PAGE PREV", origin + new Vector2(lx, y), C_GREEN, 0.52f * u, CEN);
AddText(ref frame, "PAGE NEXT", origin + new Vector2(rx, y), C_GREEN, 0.52f * u, CEN);
AddText(ref frame, "PREV", origin + new Vector2(lx, y + lh), C_GREEN, 0.52f * u, CEN);
AddText(ref frame, "NEXT", origin + new Vector2(rx, y + lh), C_GREEN, 0.52f * u, CEN);
AddText(ref frame, "DEC", origin + new Vector2(lx, y + lh * 2f), C_GREEN, 0.52f * u, CEN);
AddText(ref frame, "INC", origin + new Vector2(rx, y + lh * 2f), C_GREEN, 0.52f * u, CEN);
AddText(ref frame, "STEP", origin + new Vector2(lx, y + lh * 3f), C_GREEN, 0.52f * u, CEN);
AddText(ref frame, "FOCUS", origin + new Vector2(rx, y + lh * 3f), C_GREEN, 0.52f * u, CEN);
}
}
Color OfficerModeColor(string name, double value)
{
if (name == "BUILD QUEUE SOURCE")
{
UpdateBuildQueueCluster();
if (bqsClusterTag.Length == 0 || bqsClusterTag == "AUTO") return C_DIM;
return bqsClusterTag == IMS_ENTITY_TAG ? C_GREEN : C_CYAN;
}
if (name == "WORKER MODE") return value > 0 ? C_DIM : C_GREEN;
if (name == "DISTRIBUTION METHOD") return value > 0 ? C_DIM : C_GREEN;
if (name == "LCD ROTATION") return value > 0 ? C_GREEN : C_DIM;
if (value <= 0) return C_DIM;
if (value >= 2) return C_CYAN;
return C_GREEN;
}
string DisplayOfficerMode(string name, double value)
{
if (name == "BUILD QUEUE SOURCE") return BuildQueueDisplayText();
return FormatOfficerMode(name, value);
}

string BuildQueueDisplayText()
{
UpdateBuildQueueCluster();
if (bqsClusterTag.Length == 0 || bqsClusterTag == "AUTO") return "AUTO";
if (bqsClusterTag == IMS_ENTITY_TAG) return "LOCAL";
return bqsClusterTag;
}

string FormatOfficerMode(string name, double value)
{
int v = (int)Math.Round(value);
if (name == "ASSEMBLY") return v > 0 ? "ON" : "AUTO";
if (name == "REFINING") return v > 0 ? "OPTIMIZED" : "AUTO";
if (name == "ORE UNLOAD") return v > 0 ? "ON" : "AUTO";
if (name == "ICE PROCESSING") return v <= 0 ? "OFF" : (v == 1 ? "MIN" : "AUTO");
if (name == "INGOT CLEARING") return v > 0 ? "ON" : "OFF";
if (name == "DISTRIBUTION METHOD") return v > 0 ? "DESIGNATED" : "EVEN";
if (name == "WORKER MODE") return v > 0 ? "PAUSE" : "AUTO";
if (name == "BUILD QUEUE SOURCE") return v <= 0 ? "AUTO" : (v == 1 ? "LOCAL" : "OB1");
if (name == "SHOW GRAVEL") return v > 0 ? "ON" : "OFF";
if (name == "LCD ROTATION") return v <= 0 ? "OFF" : v.ToString("0") + "s";
if (v <= 0) return "OFF";
if (v == 1) return "MIN";
return "MAX";
}
string ShortOfficerName(string name)
{
if (name == "STEEL PLATE") return "STEEL PLATE";
if (name == "INT. PLATE") return "INT. PLATE";
if (name == "CONST. COMP") return "CONST. COMP";
if (name == "B. GLASS") return "B. GLASS";
if (name == "REACTOR COMP") return "REACTOR";
if (name == "THRUSTER COMP") return "THRUSTER";
if (name == "SUPERCOND.") return "SUPERCOND.";
if (name == "MAGNESIUM") return "MAG";
if (name == "PLATINUM") return "PLAT";
if (name == "ASSEMBLY") return "ASSEMBLY";
if (name == "REFINING") return "REFINING";
if (name == "ICE PROCESSING") return "ICE PROC";
if (name == "INGOT CLEARING") return "INGOT CLEAR";
if (name == "ORE UNLOAD") return "ORE UNLOAD";
if (name == "DISTRIBUTION METHOD") return "DIST. METHOD";
if (name == "WORKER MODE") return "WORKER MODE";
if (name == "BUILD QUEUE SOURCE") return "BUILD QUEUE";
if (name == "SHOW GRAVEL") return "SHOW GRAVEL";
if (name == "LCD ROTATION") return "LCD ROTATE";
return name;
}
string FormatOfficerValue(int page, string item, double value)
{
if (page == 0)
{
if (item == "H2" || item == "O2" || item == "BATTERY") return value.ToString("0") + "%";
return FormatMass(value);
}
if (page == 3) return FormatMass(value);
return FormatCount(value);
}
InventorySummary ScanInventory()
{
InventorySummary d = new InventorySummary();
for (int i = 0; i < d.Bays.Length; i++)
d.Bays[i] = new CargoBay();
d.MainCargoBlocks = mainCargoBlocks.Count;
for (int i = 0; i < inventoryBlocks.Count; i++)
{
IMyTerminalBlock b = inventoryBlocks[i];
for (int invIndex = 0; invIndex < b.InventoryCount; invIndex++)
{
IMyInventory inv = b.GetInventory(invIndex);
if (inv == null) continue;
d.CurrentVolumeL += (double)inv.CurrentVolume * 1000.0;
d.MaxVolumeL += (double)inv.MaxVolume * 1000.0;
itemScratch.Clear();
inv.GetItems(itemScratch);
for (int k = 0; k < itemScratch.Count; k++)
{
MyInventoryItem item = itemScratch[k];
string category = CategorizeItem(item.Type.TypeId, item.Type.SubtypeId);
if (category != "AMMO") AddCategoryAmount(d, category, (double)item.Amount);
AddSpecificAmount(d, item.Type.TypeId, item.Type.SubtypeId, (double)item.Amount);
}
}
}
ScanMainCargoBank(d);
ScanBatteries(d);
ScanGasTanks(d);
if (d.MaxVolumeL > 0.001) d.FillRatio = d.CurrentVolumeL / d.MaxVolumeL;
d.FillRatio = Clamp01(d.FillRatio);
if (d.MainMaxL > 0.001) d.MainFillRatio = d.MainCurrentL / d.MainMaxL;
d.MainFillRatio = Clamp01(d.MainFillRatio);
SetStatus(d);
return d;
}
void ScanMainCargoBank(InventorySummary d)
{
double minFill = 999;
double maxFill = -999;
int count = Math.Min(mainCargoBlocks.Count, d.Bays.Length);
for (int i = 0; i < count; i++)
{
IMyTerminalBlock b = mainCargoBlocks[i];
CargoBay bay = d.Bays[i];
bay.Name = "C" + (i + 1).ToString("0");
for (int invIndex = 0; invIndex < b.InventoryCount; invIndex++)
{
IMyInventory inv = b.GetInventory(invIndex);
if (inv == null) continue;
bay.CurrentL += (double)inv.CurrentVolume * 1000.0;
bay.MaxL += (double)inv.MaxVolume * 1000.0;
itemScratch.Clear();
inv.GetItems(itemScratch);
for (int k = 0; k < itemScratch.Count; k++)
{
MyInventoryItem item = itemScratch[k];
string category = CategorizeItem(item.Type.TypeId, item.Type.SubtypeId);
if (category == "AMMO")
{
AddCategoryAmount(d, category, (double)item.Amount);
AddAmmoReserveAmount(d, item.Type.SubtypeId, (double)item.Amount);
}
}
}
if (bay.MaxL > 0.001) bay.Fill = bay.CurrentL / bay.MaxL;
bay.Fill = Clamp01(bay.Fill);
d.MainCurrentL += bay.CurrentL;
d.MainMaxL += bay.MaxL;
if (bay.Fill < minFill) minFill = bay.Fill;
if (bay.Fill > maxFill) maxFill = bay.Fill;
}
if (count > 0) d.BalanceDelta = maxFill - minFill;
}
string CategorizeItem(string typeId, string subtype)
{
if (typeId.EndsWith("_Ore"))
{
if (subtype.Equals("Ice", StringComparison.OrdinalIgnoreCase)) return "ICE";
return "ORE";
}
if (typeId.EndsWith("_Ingot"))
{
if (subtype.Equals("Uranium", StringComparison.OrdinalIgnoreCase)) return "URANIUM";
return "INGOT";
}
if (typeId.EndsWith("_Component")) return "COMPONENT";
if (typeId.EndsWith("_AmmoMagazine")) return "AMMO";
if (typeId.EndsWith("_OxygenContainerObject")) return "BOTTLE";
if (typeId.EndsWith("_GasContainerObject")) return "BOTTLE";
if (typeId.EndsWith("_PhysicalGunObject")) return "TOOL";
return "OTHER";
}
void AddCategoryAmount(InventorySummary d, string category, double amount)
{
if (category == "INGOT") d.IngotKg += amount;
else if (category == "COMPONENT") d.ComponentUnits += amount;
else if (category == "AMMO") d.AmmoUnits += amount;
else if (category == "ICE") d.IceKg += amount;
else if (category == "URANIUM") d.UraniumKg += amount;
}

void ScanBatteries(InventorySummary d)
{
    double stored = 0;
    double max = 0;
    for (int i = 0; i < batteryBlocks.Count; i++)
    {
        IMyBatteryBlock b = batteryBlocks[i];
        if (b == null) continue;
        if (!b.IsFunctional) continue;
        stored += b.CurrentStoredPower;
        max += b.MaxStoredPower;
    }
    d.BatteryPercent = max > 0.0001 ? (stored / max) * 100.0 : 0;
}

void RefreshGasTankList()
{
gasTanks.Clear();
List<IMyTerminalBlock> gasScanBlocks = new List<IMyTerminalBlock>();
GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(
gasScanBlocks,
b => HasTag(b, INSTALL_TAG) && LooksTankRelated(b)
);
for (int gi = 0; gi < gasScanBlocks.Count; gi++)
{
IMyGasTank tank = gasScanBlocks[gi] as IMyGasTank;
if (tank == null) continue;
if (!ContainsGasTank(gasTanks, tank))
gasTanks.Add(tank);
}
}
void ScanGasTanks(InventorySummary d)
{
RefreshGasTankList();
double h2Filled = 0;
double h2Capacity = 0;
double o2Filled = 0;
double o2Capacity = 0;
for (int i = 0; i < gasTanks.Count; i++)
{
IMyGasTank tank = gasTanks[i];
if (tank == null) continue;
double capacity = tank.Capacity;
double filled = tank.FilledRatio * capacity;
int gasKind = GasTankKind(tank);
if (gasKind == 1)
{
h2Capacity += capacity;
h2Filled += filled;
}
else if (gasKind == 2)
{
o2Capacity += capacity;
o2Filled += filled;
}
}
d.HydrogenPercent = h2Capacity > 0 ? (h2Filled / h2Capacity) * 100.0 : 0;
d.OxygenPercent = o2Capacity > 0 ? (o2Filled / o2Capacity) * 100.0 : 0;
}
int GasTankKind(IMyGasTank tank)
{
string s = tank.CustomName + " " + tank.DisplayNameText + " " + tank.BlockDefinition.SubtypeId + " " + tank.BlockDefinition.ToString();
if (ContainsIgnoreCase(s, "Hydrogen") || ContainsIgnoreCase(s, " H2") || ContainsIgnoreCase(s, "H2 ")) return 1;
if (ContainsIgnoreCase(s, "Oxygen") || ContainsIgnoreCase(s, " O2") || ContainsIgnoreCase(s, "O2 ")) return 2;
return 0;
}
bool LooksTankRelated(IMyTerminalBlock b)
{
if (b == null) return false;
string s = "";
s += b.CustomName + " ";
s += b.DisplayNameText + " ";
s += b.BlockDefinition.ToString() + " ";
s += b.BlockDefinition.SubtypeId + " ";
return ContainsIgnoreCase(s, "Tank");
}
bool ContainsGasTank(List<IMyGasTank> list, IMyGasTank tank)
{
for (int i = 0; i < list.Count; i++)
if (list[i] == tank) return true;
return false;
}
bool ContainsIgnoreCase(string text, string value)
{
if (text == null || value == null) return false;
return text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
}
void AddSpecificAmount(InventorySummary d, string typeId, string subtype, double amount)
{
if (subtype == null) subtype = "";
if (typeId.EndsWith("_Component"))
AddToMap(d.ComponentBySubtype, subtype, amount);
else if (typeId.EndsWith("_Ingot"))
AddToMap(d.IngotBySubtype, subtype, amount);
}
void AddAmmoReserveAmount(InventorySummary d, string subtype, double amount)
{
if (subtype == null) subtype = "";
AddToMap(d.AmmoBySubtype, subtype, amount);
}
void AddToMap(Dictionary<string, double> map, string key, double amount)
{
double old;
if (!map.TryGetValue(key, out old)) old = 0;
map[key] = old + amount;
}
double MapValue(Dictionary<string, double> map, string key)
{
double v;
if (map.TryGetValue(key, out v)) return v;
return 0;
}
void SetStatus(InventorySummary d)
{
d.Status = "LOGISTICS NOMINAL";
d.StatusColor = C_GREEN;
d.DistributionState = "DISTRIBUTION ACTIVE";
d.DistributionColor = C_GREEN;
if (d.MainCargoBlocks == 0)
{
d.Status = "NO MAIN CARGO";
d.StatusColor = C_RED;
d.DistributionState = "DISTRIBUTION FAULT";
d.DistributionColor = C_RED;
}
else if (d.FillRatio >= FULL_CRIT_RATIO || d.MainFillRatio >= FULL_CRIT_RATIO)
{
d.Status = "STORAGE CRITICAL";
d.StatusColor = C_RED;
}
else if (d.BalanceDelta >= BALANCE_CRIT_DELTA)
{
d.Status = "DISTRIBUTION FAULT";
d.StatusColor = C_RED;
d.DistributionState = "DISTRIBUTION FAULT";
d.DistributionColor = C_RED;
}
else if (d.FillRatio >= FULL_WARN_RATIO || d.MainFillRatio >= FULL_WARN_RATIO)
{
d.Status = "STORAGE HIGH";
d.StatusColor = C_YELLOW;
}
else if (d.BalanceDelta >= BALANCE_WARN_DELTA)
{
d.Status = "DISTRIBUTION DEGRADED";
d.StatusColor = C_YELLOW;
d.DistributionState = "DISTRIBUTION DEGRADED";
d.DistributionColor = C_YELLOW;
}
else if (d.UraniumKg > 0 && d.UraniumKg < URANIUM_LOW_KG)
{
d.Status = "URANIUM LOW";
d.StatusColor = C_YELLOW;
}
else if (d.IceKg > 0 && d.IceKg < ICE_LOW_KG)
{
d.Status = "ICE LOW";
d.StatusColor = C_YELLOW;
}
else if (d.AmmoUnits > 0 && d.AmmoUnits < AMMO_LOW_UNITS)
{
d.Status = "AMMO LOW";
d.StatusColor = C_YELLOW;
}
else if (d.ComponentUnits > 0 && d.ComponentUnits < COMPONENT_LOW_UNITS)
{
d.Status = "PARTS LOW";
d.StatusColor = C_YELLOW;
}
}
void DrawAll()
{
DrawDetailLCDs();
for (int i = 0; i < surfaces.Count; i++)
{
IMyTextSurface s = surfaces[i];
string layout = DetermineLayout(s);
if (layout == "WIDE") DrawWideIMS(s, cached);
else if (layout == "STRIP") DrawStripIMS(s, cached);
else DrawSquareIMS(s, cached);
}
}
string DetermineLayout(IMyTextSurface s)
{
Vector2 size = s.SurfaceSize;
if (size.Y <= 0.1f) return "SQUARE";
float ratio = size.X / size.Y;
if (ratio >= 3.0f) return "STRIP";
if (ratio >= 1.35f) return "WIDE";
return "SQUARE";
}
void DrawWideIMS(IMyTextSurface s, InventorySummary d)
{
Vector2 size = s.SurfaceSize;
Vector2 origin = GetOrigin(s);
float w = size.X;
float h = size.Y;
float u = Math.Min(w / 1024f, h / 512f);
if (u < 0.45f) u = 0.45f;
float margin = 18f * u;
float topH = 64f * u;
float rightW = 275f * u;
using (MySpriteDrawFrame frame = s.DrawFrame())
{
AddRect(ref frame, origin + new Vector2(w / 2, h / 2), new Vector2(w, h), C_BG);
AddRect(ref frame, origin + new Vector2(w / 2, h / 2), new Vector2(w - margin, h - margin), C_DEEP);
AddFrame(ref frame, origin + new Vector2(w / 2, h / 2), new Vector2(w - margin, h - margin), 3f * u, C_CYAN);
AddRect(ref frame, origin + new Vector2(w * 0.50f, margin + topH / 2f), new Vector2(w - margin * 2f, topH), C_PANEL2);
AddText(ref frame, "INVENTORY MANAGEMENT SYSTEM", origin + new Vector2(margin * 1.6f, margin + 14f * u), C_TEXT, 0.82f * u, LEF);
AddText(ref frame, "[MB1] DISTRIBUTED STORAGE", origin + new Vector2(margin * 1.6f, margin + 38f * u), C_MUTED, 0.45f * u, LEF);
DrawStatusPill(ref frame, origin + new Vector2(w - rightW * 0.50f, margin + topH / 2f), new Vector2(rightW * 0.86f, 42f * u), d.Status, d.StatusColor, u);
DrawPrimaryCargoSchematic(ref frame, origin + new Vector2(w * 0.205f, h * 0.440f), new Vector2(w * 0.345f, h * 0.500f), d, u);
DrawTotalStorageBand(ref frame, origin + new Vector2(w * 0.205f, h * 0.790f), new Vector2(w * 0.345f, 78f * u), d, u);
DrawQuantityBoard(ref frame, origin + new Vector2(w * 0.670f, h * 0.540f), new Vector2(w * 0.505f, h * 0.710f), d, u);
}
}
void DrawPrimaryCargoSchematic(ref MySpriteDrawFrame frame, Vector2 center, Vector2 size, InventorySummary d, float u)
{
AddRect(ref frame, center, size, new Color(6, 18, 24));
AddText(ref frame, "PRIMARY CARGO  " + d.MainCargoBlocks.ToString("0") + " LANES", center + new Vector2(0, -size.Y / 2f + 18f * u), C_TEXT, 0.47f * u, CEN);
int bays = Math.Min(d.MainCargoBlocks, d.Bays.Length);
if (bays <= 0)
{
AddText(ref frame, "NO CARGO BANK", center + new Vector2(0, 0), C_RED, 0.65f * u, CEN);
return;
}
int cols = bays <= 2 ? bays : 2;
int rows = (bays + cols - 1) / cols;
float gapX = 6f * u;
float gapY = 6f * u;
float labelReserve = 48f * u;
float cellW = (size.X - 36f * u - gapX * (cols - 1)) / cols;
float cellH = (size.Y - labelReserve - 34f * u - gapY * (rows - 1)) / rows;
if (cellH > cellW * 0.96f) cellH = cellW * 0.96f;
if (cellW > 142f * u) cellW = 142f * u;
float totalW = cellW * cols + gapX * (cols - 1);
float totalH = cellH * rows + gapY * (rows - 1);
float startX = center.X - totalW / 2f + cellW / 2f;
float startY = center.Y - totalH / 2f + cellH / 2f + 6f * u;
for (int i = 0; i < bays; i++)
{
int col = i % cols;
int row = i / cols;
Vector2 c = new Vector2(startX + col * (cellW + gapX), startY + row * (cellH + gapY));
DrawLargeContainerIcon(ref frame, c, new Vector2(cellW, cellH), d.Bays[i], i, u);
}
AddText(ref frame, d.DistributionState, center + new Vector2(0, size.Y / 2f - 22f * u), d.DistributionColor, 0.39f * u, CEN);
}
void DrawLargeContainerIcon(ref MySpriteDrawFrame frame, Vector2 center, Vector2 size, CargoBay bay, int index, float u)
{
Color sh=new Color(0,5,6),dk=new Color(38,43,43),ed=new Color(63,68,66),fa=new Color(118,124,120),pa=new Color(92,98,96),hi=new Color(168,174,170),lo=new Color(28,32,32),ye=new Color(218,178,34),po=new Color(32,34,32);
float r=Math.Min(size.X,size.Y)*.44f;
Vector2 b=new Vector2(r*1.92f,r*1.78f);
AddRect(ref frame,center+new Vector2(4f*u,5f*u),b,sh);
AddRect(ref frame,center,b,dk);
AddRect(ref frame,center,new Vector2(b.X-r*.10f,b.Y-r*.10f),ed);
AddRect(ref frame,center,new Vector2(b.X-r*.28f,b.Y-r*.24f),fa);
AddRect(ref frame,center+new Vector2(0,b.Y*.28f),new Vector2(b.X-r*.44f,r*.14f),pa);
AddRect(ref frame,center+new Vector2(0,-b.Y*.29f),new Vector2(b.X-r*.40f,r*.10f),hi);
float x=b.X*.48f,y=b.Y*.46f;
for(int sx=-1;sx<=1;sx+=2){
AddRect(ref frame,center+new Vector2(sx*x,0),new Vector2(r*.18f,b.Y*.86f),dk);
AddRect(ref frame,center+new Vector2(sx*(x-r*.05f),0),new Vector2(r*.08f,b.Y*.70f),hi);
AddRect(ref frame,center+new Vector2(sx*r*.36f,-r*.30f),new Vector2(r*.18f,r*.42f),dk);
AddRect(ref frame,center+new Vector2(sx*r*.36f,-r*.30f),new Vector2(r*.10f,r*.34f),pa);
AddRect(ref frame,center+new Vector2(sx*r*.36f,r*.34f),new Vector2(r*.18f,r*.45f),dk);
AddRect(ref frame,center+new Vector2(sx*r*.36f,r*.34f),new Vector2(r*.10f,r*.36f),pa);
AddRect(ref frame,center+new Vector2(sx*r*.36f,0),new Vector2(r*.06f,r*.30f),lo);
AddRect(ref frame,center+new Vector2(sx*x,-r*.04f),new Vector2(r*.035f,r*.42f),ye);
}
AddRect(ref frame,center+new Vector2(0,-y),new Vector2(r*.46f,r*.055f),ye);
AddRect(ref frame,center+new Vector2(0,y),new Vector2(r*.46f,r*.055f),ye);
for(int sy=-1;sy<=1;sy+=2){
AddRect(ref frame,center+new Vector2(-r*.42f,sy*r*.47f),new Vector2(r*.26f,r*.08f),lo);
AddRect(ref frame,center+new Vector2(r*.42f,sy*r*.47f),new Vector2(r*.26f,r*.08f),lo);
}
Vector2 h=new Vector2(r*.58f,r*.54f);
AddRect(ref frame,center,h,lo);
AddRect(ref frame,center,new Vector2(h.X*.88f,h.Y*.88f),ye);
AddRect(ref frame,center,new Vector2(h.X*.72f,h.Y*.72f),po);
AddRect(ref frame,center+new Vector2(0,-h.Y*.16f),new Vector2(h.X*.50f,r*.035f),hi);
AddRect(ref frame,center+new Vector2(0,h.Y*.16f),new Vector2(h.X*.50f,r*.035f),dk);
AddText(ref frame,((char)('A'+index)).ToString(),center+new Vector2(-size.X*.39f,-size.Y*.47f),C_TEXT,.30f*u,CEN);
}
void DrawQuantityBoard(ref MySpriteDrawFrame frame, Vector2 center, Vector2 size, InventorySummary d, float u)
{
AddRect(ref frame, center, size, new Color(6, 18, 24));
float rowH = 50f * u;
float startY = center.Y - size.Y / 2f + 32f * u;
float leftX = center.X - size.X / 2f + 26f * u;
float rightX = center.X + size.X / 2f - 26f * u;
DrawQuantityRow(ref frame, leftX, rightX, startY + rowH * 0, "ICE", FormatMass(d.IceKg), d.IceKg, OfficerTargetMin(0, "ICE"), OfficerTargetMax(0, "ICE"), u);
DrawQuantityRow(ref frame, leftX, rightX, startY + rowH * 1, "URANIUM", FormatMass(d.UraniumKg), d.UraniumKg, OfficerTargetMin(0, "URANIUM"), OfficerTargetMax(0, "URANIUM"), u);
DrawQuantityRow(ref frame, leftX, rightX, startY + rowH * 2, "AMMO", FormatCount(d.AmmoUnits), d.AmmoUnits, OfficerTargetMinSum(1), OfficerTargetMaxSum(1), u);
DrawQuantityRow(ref frame, leftX, rightX, startY + rowH * 3, "COMPONENTS", FormatCount(d.ComponentUnits), d.ComponentUnits, OfficerTargetMinSum(2), OfficerTargetMaxSum(2), u);
DrawQuantityRow(ref frame, leftX, rightX, startY + rowH * 4, "INGOTS", FormatMass(d.IngotKg), d.IngotKg, OfficerTargetMinSum(3), OfficerTargetMaxSum(3), u);
DrawWideSupplyRow(ref frame, leftX, rightX, startY + rowH * 5 + 3f * u, d, u);
}
void DrawQuantityRow(ref MySpriteDrawFrame frame, float leftX, float rightX, float y, string label, string value, double current, double targetMin, double targetMax, float u)
{
float rowW = rightX - leftX;
Vector2 rowCenter = new Vector2((leftX + rightX) / 2f, y + 8f * u);
Color valueColor = TargetStockColor(current, targetMin);
AddRect(ref frame, rowCenter, new Vector2(rowW, 44f * u), new Color(4, 13, 18));
AddRect(ref frame, new Vector2(leftX + 5f * u, y + 8f * u), new Vector2(8f * u, 34f * u), valueColor);
Vector2 barCenter = new Vector2(leftX + rowW * 0.56f, y + 10f * u);
Vector2 barSize = new Vector2(rowW * 0.43f, 18f * u);
DrawBar(ref frame, barCenter, barSize, current, targetMin, targetMax, valueColor, u, 1);
AddText(ref frame, label, new Vector2(leftX + 22f * u, y - 9f * u), C_MUTED, 0.72f * u, LEF);
AddText(ref frame, value, new Vector2(rightX - 10f * u, y - 9f * u), valueColor, 0.72f * u, RIG);
}
void DrawWideSupplyRow(ref MySpriteDrawFrame frame, float leftX, float rightX, float y, InventorySummary d, float u)
{
float rowW = rightX - leftX;
Vector2 rowCenter = new Vector2((leftX + rightX) / 2f, y + 13f * u);
AddRect(ref frame, rowCenter, new Vector2(rowW, 62f * u), new Color(4, 13, 18));
AddRect(ref frame, new Vector2(leftX + 5f * u, y + 13f * u), new Vector2(8f * u, 50f * u), SupplyCombinedColor(d));
float midX = leftX + rowW * 0.56f;
float barW = rowW * 0.43f;
float barH = 7f * u;
DrawGasMiniLine(ref frame, "H2", d.HydrogenPercent, OfficerGasMin("H2"), OfficerGasMax("H2"), leftX + 22f * u, rightX - 10f * u, midX, y - 4f * u, barW, barH, u);
DrawGasMiniLine(ref frame, "O2", d.OxygenPercent, OfficerGasMin("O2"), OfficerGasMax("O2"), leftX + 22f * u, rightX - 10f * u, midX, y + 14f * u, barW, barH, u);
DrawGasMiniLine(ref frame, "BAT", d.BatteryPercent, OfficerTargetMin(0, "BATTERY"), OfficerTargetMax(0, "BATTERY"), leftX + 22f * u, rightX - 10f * u, midX, y + 32f * u, barW, barH, u);
}
void DrawGasMiniLine(ref MySpriteDrawFrame frame, string label, double percent, double minPercent, double maxPercent, float labelX, float valueX, float barX, float y, float barW, float barH, float u)
{
Color color = GasColor(percent, minPercent);
AddText(ref frame, label, new Vector2(labelX, y - 9f * u), C_MUTED, 0.44f * u, LEF);
AddText(ref frame, FormatGasPercent(percent), new Vector2(valueX, y - 9f * u), color, 0.44f * u, RIG);
DrawBar(ref frame, new Vector2(barX, y + 1f * u), new Vector2(barW, barH), percent, minPercent, maxPercent, color, u, 2);
}
void DrawTotalStorageBand(ref MySpriteDrawFrame frame, Vector2 center, Vector2 size, InventorySummary d, float u)
{
AddRect(ref frame, center, size, new Color(5, 18, 23));
AddText(ref frame, "TOTAL STORAGE", center + new Vector2(0, -size.Y / 2f + 10f * u), C_MUTED, 0.40f * u, CEN);
AddText(ref frame, Percent(d.FillRatio), center + new Vector2(-size.X * 0.37f, -4f * u), FillColor(d.FillRatio), 0.64f * u, CEN);
Vector2 barCenter = center + new Vector2(size.X * 0.08f, -2f * u);
Vector2 barSize = new Vector2(size.X * 0.58f, 20f * u);
DrawBar(ref frame, barCenter, barSize, d.FillRatio, 0, 0, FillColor(d.FillRatio), u, 0);
AddText(ref frame, FormatVolume(d.CurrentVolumeL) + " USED", center + new Vector2(size.X * 0.42f, -18f * u), C_TEXT, 0.44f * u, RIG);
AddText(ref frame, FormatVolume(d.MaxVolumeL) + " CAP", center + new Vector2(size.X * 0.42f, 6f * u), C_MUTED, 0.42f * u, RIG);
}
void DrawSquareIMS(IMyTextSurface s, InventorySummary d)
{
Vector2 size = s.SurfaceSize;
Vector2 origin = GetOrigin(s);
float w = size.X;
float h = size.Y;
float u = Math.Min(w, h) / 512f;
if (u < 0.45f) u = 0.45f;
using (MySpriteDrawFrame frame = s.DrawFrame())
{
AddRect(ref frame, origin + new Vector2(w / 2, h / 2), new Vector2(w, h), C_BG);
AddFrame(ref frame, origin + new Vector2(w / 2, h / 2), new Vector2(w - 16f * u, h - 16f * u), 3f * u, C_CYAN);
AddText(ref frame, "IMS", origin + new Vector2(w / 2, 18f * u), C_TEXT, 0.76f * u, CEN);
AddText(ref frame, "DISTRIBUTED STORAGE", origin + new Vector2(w / 2, 45f * u), C_MUTED, 0.34f * u, CEN);
DrawMiniCargoBank(ref frame, origin + new Vector2(w / 2, h * 0.185f), new Vector2(w * 0.74f, h * 0.145f), d, u);
AddText(ref frame, "TOTAL " + Percent(d.FillRatio), origin + new Vector2(w / 2, h * 0.315f), FillColor(d.FillRatio), 0.58f * u, CEN);
DrawBar(ref frame, origin + new Vector2(w / 2, h * 0.365f), new Vector2(w * 0.68f, 16f * u), d.FillRatio, 0, 0, FillColor(d.FillRatio), u, 0);
Vector2 gridCenter = origin + new Vector2(w / 2, h * 0.665f);
Vector2 gridSize = new Vector2(w * 0.88f, h * 0.470f);
DrawSquareQuantityGrid(ref frame, gridCenter, gridSize, d, u);
AddText(ref frame, d.Status, origin + new Vector2(w / 2, h * 0.940f), d.StatusColor, 0.46f * u, CEN);
}
}
void DrawSquareQuantityGrid(ref MySpriteDrawFrame frame, Vector2 center, Vector2 size, InventorySummary d, float u)
{
AddRect(ref frame, center, size, new Color(6, 18, 24));
float gapX = 10f * u;
float gapY = 10f * u;
float cellW = (size.X - 34f * u - gapX) / 2f;
float cellH = (size.Y - 28f * u - gapY * 2f) / 3f;
float startX = center.X - (cellW * 2f + gapX) / 2f + cellW / 2f;
float startY = center.Y - (cellH * 3f + gapY * 2f) / 2f + cellH / 2f;
DrawSquareQuantityCell(ref frame, new Vector2(startX, startY), new Vector2(cellW, cellH), "ICE", FormatMass(d.IceKg), d.IceKg, OfficerTargetMin(0, "ICE"), OfficerTargetMax(0, "ICE"), u);
DrawSquareQuantityCell(ref frame, new Vector2(startX + cellW + gapX, startY), new Vector2(cellW, cellH), "URANIUM", FormatMass(d.UraniumKg), d.UraniumKg, OfficerTargetMin(0, "URANIUM"), OfficerTargetMax(0, "URANIUM"), u);
DrawSquareQuantityCell(ref frame, new Vector2(startX, startY + cellH + gapY), new Vector2(cellW, cellH), "AMMO", FormatCount(d.AmmoUnits), d.AmmoUnits, OfficerTargetMinSum(1), OfficerTargetMaxSum(1), u);
DrawSquareQuantityCell(ref frame, new Vector2(startX + cellW + gapX, startY + cellH + gapY), new Vector2(cellW, cellH), "COMPONENTS", FormatCount(d.ComponentUnits), d.ComponentUnits, OfficerTargetMinSum(2), OfficerTargetMaxSum(2), u);
DrawSquareQuantityCell(ref frame, new Vector2(startX, startY + (cellH + gapY) * 2f), new Vector2(cellW, cellH), "INGOT", FormatMass(d.IngotKg), d.IngotKg, OfficerTargetMinSum(3), OfficerTargetMaxSum(3), u);
DrawSquareGasCell(ref frame, new Vector2(startX + cellW + gapX, startY + (cellH + gapY) * 2f), new Vector2(cellW, cellH), d, u);
}
void DrawSquareQuantityCell(ref MySpriteDrawFrame frame, Vector2 center, Vector2 size, string label, string value, double current, double targetMin, double targetMax, float u)
{
Color color = TargetStockColor(current, targetMin);
AddRect(ref frame, center, size, new Color(4, 13, 18));
AddRect(ref frame, center + new Vector2(-size.X / 2f + 5f * u, 0), new Vector2(7f * u, size.Y - 10f * u), color);
float labelScale = label.Length > 7 ? 0.34f * u : 0.52f * u;
AddText(ref frame, label, center + new Vector2(-size.X * 0.38f, -size.Y * 0.34f), C_MUTED, labelScale, LEF);
AddText(ref frame, value, center + new Vector2(size.X * 0.39f, -size.Y * 0.34f), color, 0.50f * u, RIG);
DrawBar(ref frame, center + new Vector2(size.X * 0.08f, size.Y * 0.24f), new Vector2(size.X * 0.68f, 14f * u), current, targetMin, targetMax, color, u, 1);
}
void DrawSquareGasCell(ref MySpriteDrawFrame frame, Vector2 center, Vector2 size, InventorySummary d, float u)
{
AddRect(ref frame, center, size, new Color(4, 13, 18));
AddRect(ref frame, center + new Vector2(-size.X / 2f + 5f * u, 0), new Vector2(7f * u, size.Y - 10f * u), SupplyCombinedColor(d));
float labelX = center.X - size.X * 0.36f;
float valueX = center.X + size.X * 0.39f;
float barX = center.X + size.X * 0.08f;
float barW = size.X * 0.66f;
float barH = 6f * u;
DrawGasMiniLine(ref frame, "H2", d.HydrogenPercent, OfficerGasMin("H2"), OfficerGasMax("H2"), labelX, valueX, barX, center.Y - size.Y * 0.31f, barW, barH, u);
DrawGasMiniLine(ref frame, "O2", d.OxygenPercent, OfficerGasMin("O2"), OfficerGasMax("O2"), labelX, valueX, barX, center.Y + size.Y * 0.02f, barW, barH, u);
DrawGasMiniLine(ref frame, "BAT", d.BatteryPercent, OfficerTargetMin(0, "BATTERY"), OfficerTargetMax(0, "BATTERY"), labelX, valueX, barX, center.Y + size.Y * 0.34f, barW, barH, u);
}
void DrawMiniCargoBank(ref MySpriteDrawFrame frame, Vector2 center, Vector2 size, InventorySummary d, float u)
{
AddRect(ref frame, center, size, new Color(6, 18, 24));
int bays = Math.Min(d.MainCargoBlocks, d.Bays.Length);
if (bays <= 0)
{
AddText(ref frame, "NO CARGO", center + new Vector2(0, -8f * u), C_RED, 0.50f * u, CEN);
return;
}
float gap = 7f * u;
float cellW = (size.X - gap * (bays + 1)) / bays;
float cellH = size.Y - 22f * u;
if (cellH > cellW * 0.95f) cellH = cellW * 0.95f;
float totalW = cellW * bays + gap * (bays - 1);
float startX = center.X - totalW / 2f + cellW / 2f;
float cellY = center.Y;
float miniU = u * 0.58f;
for (int i = 0; i < bays; i++)
{
Vector2 c = new Vector2(startX + i * (cellW + gap), cellY);
DrawLargeContainerIcon(ref frame, c, new Vector2(cellW, cellH), d.Bays[i], i, miniU);
}
}
void DrawStripIMS(IMyTextSurface s, InventorySummary d)
{
Vector2 size = s.SurfaceSize;
Vector2 origin = GetOrigin(s);
float w = size.X;
float h = size.Y;
float u = h / 96f;
if (u < 0.35f) u = 0.35f;
using (MySpriteDrawFrame frame = s.DrawFrame())
{
AddRect(ref frame, origin + new Vector2(w / 2, h / 2), new Vector2(w, h), C_BG);
AddRect(ref frame, origin + new Vector2(w * 0.05f, h / 2), new Vector2(w * 0.10f, h), d.StatusColor);
AddText(ref frame, "IMS", origin + new Vector2(w * 0.13f, h * 0.25f), C_TEXT, 0.72f * u, LEF);
AddText(ref frame, d.Status + "   TOTAL " + Percent(d.FillRatio) + "   BATT " + FormatGasPercent(d.BatteryPercent) + "   " + d.DistributionState, origin + new Vector2(w * 0.13f, h * 0.58f), d.StatusColor, 0.52f * u, LEF);
DrawBar(ref frame, origin + new Vector2(w * 0.80f, h * 0.52f), new Vector2(w * 0.34f, h * 0.30f), d.FillRatio, 0, 0, FillColor(d.FillRatio), u, 0);
}
}
void DrawStatusPill(ref MySpriteDrawFrame frame, Vector2 center, Vector2 size, string text, Color color, float u)
{
AddCircle(ref frame, center + new Vector2(-size.X / 2f + size.Y / 2f, 0), new Vector2(size.Y, size.Y), color);
AddCircle(ref frame, center + new Vector2(size.X / 2f - size.Y / 2f, 0), new Vector2(size.Y, size.Y), color);
AddRect(ref frame, center, new Vector2(size.X - size.Y, size.Y), color);
AddRect(ref frame, center, new Vector2(size.X * 0.64f, size.Y * 0.50f), new Color(2, 20, 22, 215));
AddText(ref frame, text, center + new Vector2(0, -12f * u), C_TEXT, 0.43f * u, CEN);
}
Vector2 GetOrigin(IMyTextSurface s)
{
return (s.TextureSize - s.SurfaceSize) * 0.5f;
}
void AddRect(ref MySpriteDrawFrame frame, Vector2 pos, Vector2 size, Color color)
{
frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", pos, size, color));
}
void AddRotRect(ref MySpriteDrawFrame frame, Vector2 pos, Vector2 size, Color color, float rotation)
{
MySprite sp = new MySprite(SpriteType.TEXTURE, "SquareSimple", pos, size, color);
sp.RotationOrScale = rotation;
frame.Add(sp);
}
void AddCircle(ref MySpriteDrawFrame frame, Vector2 pos, Vector2 size, Color color)
{
frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", pos, size, color));
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
double max = mode == 0 ? 1.0 : (mode == 2 ? 100.0 : mark2);
if (max <= 0.001) max = 1.0;
double ratio = value / max;
float fillW = (float)(size.X * Clamp01(ratio));
if (fillW < 1f && value > 0.001) fillW = 1f;
float fillH = mode == 2 ? size.Y : size.Y - 6f * u;
AddRect(ref frame, center + new Vector2((fillW - size.X) / 2f, 0), new Vector2(fillW, fillH), color);
if (mode > 0 && mark1 > 0.001 && mark1 < max)
{
float x = center.X - size.X / 2f + (float)(size.X * (mark1 / max));
AddRect(ref frame, new Vector2(x, center.Y), new Vector2(2f * u, size.Y + 5f * u), C_YELLOW);
}
if (mode == 1 && value > max)
AddRect(ref frame, center + new Vector2(size.X / 2f - 2f * u, 0), new Vector2(4f * u, size.Y + 5f * u), C_TEXT);
if (mode == 2 && mark2 > 0.001 && mark2 < 100.0)
{
float x2 = center.X - size.X / 2f + (float)(size.X * (mark2 / 100.0));
AddRect(ref frame, new Vector2(x2, center.Y), new Vector2(2f * u, size.Y + 6f * u), C_CYAN);
}
}
double OfficerTargetMin(int page, string item)
{
int idx = OfficerItemIndex(page, item);
if (idx < 0) return 0;
return oMin[page][idx];
}
double OfficerTargetMax(int page, string item)
{
int idx = OfficerItemIndex(page, item);
if (idx < 0) return 1;
return oMax[page][idx];
}
int OfficerItemIndex(int page, string item)
{
if (page < 0 || page >= oItems.Length) return -1;
for (int i = 0; i < oItems[page].Length; i++)
{
if (oItems[page][i].Equals(item, StringComparison.OrdinalIgnoreCase)) return i;
}
return -1;
}
double OfficerTargetMinSum(int page)
{
if (page < 0 || page >= oMin.Length) return 0;
double sum = 0;
for (int i = 0; i < oMin[page].Length; i++) sum += oMin[page][i];
return sum;
}
double OfficerTargetMaxSum(int page)
{
if (page < 0 || page >= oMax.Length) return 1;
double sum = 0;
for (int i = 0; i < oMax[page].Length; i++) sum += oMax[page][i];
if (sum <= 0.001) sum = 1;
return sum;
}
Color TargetStockColor(double current, double targetMin)
{
if (current <= 0.001) return C_DIM;
if (current < targetMin) return C_YELLOW;
return C_GREEN;
}
Color GasColor(double percent, double minPercent)
{
if (percent <= 0.001) return C_DIM;
if (percent < minPercent) return C_YELLOW;
return C_GREEN;
}

Color SupplyCombinedColor(InventorySummary d)
{
    if (d.BatteryPercent <= 0.001 && d.HydrogenPercent <= 0.001 && d.OxygenPercent <= 0.001) return C_DIM;
    if (d.BatteryPercent < OfficerTargetMin(0, "BATTERY") || d.HydrogenPercent < OfficerGasMin("H2") || d.OxygenPercent < OfficerGasMin("O2")) return C_YELLOW;
    return C_GREEN;
}

Color GasCombinedColor(InventorySummary d)
{
double h2Min = OfficerGasMin("H2");
double o2Min = OfficerGasMin("O2");
if (d.HydrogenPercent <= 0.001 && d.OxygenPercent <= 0.001) return C_DIM;
if (d.HydrogenPercent < h2Min || d.OxygenPercent < o2Min) return C_YELLOW;
return C_GREEN;
}
double OfficerGasMin(string gas)
{
if (gas == "H2") return oMin[0][3];
if (gas == "O2") return oMin[0][4];
return 0;
}
double OfficerGasMax(string gas)
{
if (gas == "H2") return oMax[0][3];
if (gas == "O2") return oMax[0][4];
return 100;
}
string FormatGasPercent(double percent)
{
if (percent < 0) percent = 0;
if (percent > 100) percent = 100;
return percent.ToString("0") + "%";
}
string FormatMass(double kg)
{
if (kg >= 1000000.0) return (kg / 1000000.0).ToString("0.00") + " kt";
if (kg >= 1000.0) return (kg / 1000.0).ToString("0.0") + " t";
if (kg >= 100.0) return kg.ToString("0") + " kg";
if (kg >= 10.0) return kg.ToString("0.0") + " kg";
if (kg >= 1.0) return kg.ToString("0.00") + " kg";
return kg.ToString("0.000") + " kg";
}
string FormatVolume(double liters)
{
if (liters >= 1000000.0) return (liters / 1000000.0).ToString("0.00") + " ML";
if (liters >= 1000.0) return (liters / 1000.0).ToString("0.0") + " kL";
return liters.ToString("0") + " L";
}
string FormatCount(double count)
{
if (count >= 1000000.0) return (count / 1000000.0).ToString("0.00") + "M";
if (count >= 1000.0) return (count / 1000.0).ToString("0.0") + "k";
return count.ToString("0");
}
string Percent(double value)
{
return (Clamp01(value) * 100.0).ToString("0") + "%";
}
double Clamp01(double value)
{
if (value < 0) return 0;
if (value > 1) return 1;
return value;
}
Color FillColor(double fill)
{
if (fill >= .95) return C_RED;
if (fill >= .90) return C_ORANGE;
if (fill >= .80) return C_YELLOW;
return C_GREEN;
}
Color SupplyColor(double amount, double low)
{
if (amount <= 0.001) return C_DIM;
if (amount < low) return C_YELLOW;
return C_GREEN;
}
float ToRad(float deg)
{
return deg * 0.0174532925f;
}
Color ScaleColor(Color c, float scale)
{
if (scale < 0) scale = 0;
if (scale > 1) scale = 1;
return new Color(
(int)(c.R * scale),
(int)(c.G * scale),
(int)(c.B * scale),
c.A
);
}
