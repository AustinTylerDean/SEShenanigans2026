// LDC1_WSS_V010_MarkToggleRaySanity
// LDC1 Weapons Systems Station / command-front-end PB.
// Purpose: cockpit/control-seat UI authority and WSS command packet source.
// V010: MARK toggles lock/open and boresight ray state is validated/recomputed on load/range changes; no donor UI mechanics changed.
//   front-left remains proven page indicator/carousel only
//   center = active working page
//   front-right = selected bay detail visual indicator
//   far-right = large hotbar setup guide
//   far-left = decorative/status-visual surface
// This PB does not touch weapons, drones, AI blocks, connectors, merges, DPS bay hardware, or DCS custody.

const string VERSION = "LDC1_WSS_V010_MarkToggleRaySanity";
const string TAG = "[LDC1]";
const string WSS = "[WSS]";
const string HELM = "[HELM]";
const string DCS = "[DCS]";
const string PACKET_BEGIN = "# WSS_PACKET_BEGIN";
const string PACKET_END = "# WSS_PACKET_END";
const string CONFIG_BEGIN = "# WSS_CONFIG_BEGIN";
const string CONFIG_END = "# WSS_CONFIG_END";
const int SCAN_EVERY = 30;

// Cockpit/control-seat surface contract. Change here if Keen surface order differs on the chosen seat.
// Confirmed cockpit physical order left-to-right: 3,1,0,2,4.
// Role mapping: far-left=3, front-left=1, center=0, front-right=2, far-right=4.
int SURF_FRONT_LEFT = 1;
int SURF_CENTER = 0;
int SURF_FRONT_RIGHT = 2;
int SURF_FAR_LEFT = 3;
int SURF_FAR_RIGHT = 4;

// Hard visual safe areas for the LDC1 cockpit block.
// The model/bezel cuts the top and outside edges before the LCD texture ends.
const float CENTER_PAD_X = 62f;
const float CENTER_PAD_TOP = 62f;
const float CENTER_PAD_BOTTOM = 54f;
const float SIDE_PAD_X = 36f;
const float SIDE_PAD_TOP = 44f;
const float SIDE_PAD_BOTTOM = 34f;

Color BG = new Color(2, 5, 7);
Color PANEL = new Color(7, 18, 25);
Color PANEL2 = new Color(10, 30, 38);
Color TXT = new Color(218, 245, 240);
Color MUTED = new Color(95, 145, 150);
Color DIM = new Color(30, 60, 70);
Color CYAN = new Color(70, 245, 220);
Color GREEN = new Color(85, 230, 145);
Color YELLOW = new Color(245, 190, 70);
Color RED = new Color(245, 75, 65);
Color BLUE = new Color(65, 145, 245);
Color PURPLE = new Color(150, 105, 245);

string[] PAGES = new string[] { "BAYS", "TACTICAL", "TURRETS", "COMMS" };
string[] EXT_PAGES = new string[] { "BAY DETAIL", "DRONE ROSTER", "TACTICAL" };
string[] BAY_SLOTS = new string[] { "PJ01", "PJ02", "PJ03" };
string[] SLOT_NAMES = new string[] { "MISSILE", "KIN-FTR", "DEFENDER" };

class ExtDisplay
{
    public int Num;
    public IMyTextPanel Panel;
    public int Page;
    public int Sel;
}

class Bay
{
    public int Num;
    public int Slot;
    public string State = "EMPTY";
    public string Serial = "";
    public string Role = "";
    public bool Occupied = false;
}

class ExtComparer : IComparer<ExtDisplay>
{
    public int Compare(ExtDisplay a, ExtDisplay b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return -1;
        if (b == null) return 1;
        return a.Num.CompareTo(b.Num);
    }
}

List<IMyTerminalBlock> all = new List<IMyTerminalBlock>();
List<IMyShipController> seats = new List<IMyShipController>();
List<IMyTextPanel> panels = new List<IMyTextPanel>();
List<IMyProgrammableBlock> pbs = new List<IMyProgrammableBlock>();
List<ExtDisplay> exts = new List<ExtDisplay>();
Bay[] bays = new Bay[10];
IMyShipController helm = null;
IMyProgrammableBlock dcsPb = null;

int tick = 0;
int seq = 0;
int page = 0;
int field = 0;
int focus = 0; // 0=center, 1=seat hotbar/help, 2..n=external LCDs
int hotbarPage = 0;
int selectedBay = 0;
int rangeStepIndex = 1;
double rangeMeters = 10000;
double[] rangeSteps = new double[] { 250, 1000, 5000 };
string selectedWave = "A";
bool marked = false;
bool rayValid = false;
Vector3D sourcePos = new Vector3D();
Vector3D forwardVec = new Vector3D(0, 0, -1);
Vector3D waypoint = new Vector3D();
int markSeq = 0;
int fsuSeq = 0;
int abortSeq = 0;
string lastCommand = "BOOT";
string lastStatus = "INIT";
int lastInstr = 0;
int highInstr = 0;
string highWhere = "BOOT";
int screenIdTicks = 0;

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    for (int i = 0; i < bays.Length; i++) { bays[i] = new Bay(); bays[i].Num = i + 1; bays[i].Slot = 0; }
    LoadConfig();
    Scan();
    EnsureMarkedWaypointLoaded();
    WriteConfigIfMissing();
    ExportPacket(false, "BOOT");
    DrawAll();
}

public void Save() {}

public void Main(string argument, UpdateType updateSource)
{
    tick++;
    string arg = (argument == null ? "" : argument.Trim()).ToUpperInvariant();
    if (arg == "SCAN" || arg == "RESCAN" || tick % SCAN_EVERY == 0) Scan();

    if (arg == "STATUS" || arg == "") { }
    else if (arg == "FOCUS" || arg == "LCD") CycleFocus();
    else if (arg == "PAGE" || arg == "PAGE NEXT" || arg == "PAGENEXT") Page(1);
    else if (arg == "PAGE PREV" || arg == "PAGEPREV") Page(-1);
    else if (arg == "NEXT" || arg == "RIGHT") NextField(1);
    else if (arg == "PREV" || arg == "LEFT") NextField(-1);
    else if (arg == "INC" || arg == "UP" || arg == "+") Adjust(1);
    else if (arg == "DEC" || arg == "DOWN" || arg == "-") Adjust(-1);
    else if (arg == "STEP" || arg == "SCALE") Step();
    else if (arg == "RANGE INC" || arg == "RANGE+" || arg == "RANGE +") RangeAdjust(1);
    else if (arg == "RANGE DEC" || arg == "RANGE-" || arg == "RANGE -") RangeAdjust(-1);
    else if (arg == "MARK") Mark();
    else if (arg == "FSU") FireSendUpdate();
    else if (arg == "ABORT") Abort();
    else if (arg == "IDSCREENS" || arg == "SCREENID" || arg == "SCREEN ID" || arg == "ID SCREENS") ScreenId();
    else lastStatus = "UNKNOWN " + arg;

    if (screenIdTicks > 0 && arg.Length == 0) screenIdTicks--;
    ExportPacket(false, lastCommand);
    DrawAll();
    EchoStatus();
    MarkInstr("MAIN");
}

void ScreenId()
{
    screenIdTicks = 180;
    lastCommand = "IDSCREENS";
    lastStatus = "SCREEN ID ACTIVE";
}

void Scan()
{
    // Donor pattern from IMS Discover(): preserve per-display page/selection when rebuilding LCD target list.
    // Without this, the periodic scan rebuilds WSS external LCDs and resets each [WSS#] page.
    List<int> oldExtNum = new List<int>();
    List<int> oldExtPage = new List<int>();
    List<int> oldExtSel = new List<int>();
    for (int oi = 0; oi < exts.Count; oi++)
    {
        ExtDisplay old = exts[oi];
        if (old == null) continue;
        oldExtNum.Add(old.Num);
        oldExtPage.Add(old.Page);
        oldExtSel.Add(old.Sel);
    }

    seats.Clear(); panels.Clear(); pbs.Clear(); exts.Clear(); helm = null; dcsPb = null;
    GridTerminalSystem.GetBlocksOfType<IMyShipController>(seats);
    for (int i = 0; i < seats.Count; i++)
    {
        IMyShipController s = seats[i];
        if (s == null) continue;
        if (!Has(s.CustomName, TAG)) continue;
        if (Has(s.CustomName, WSS) || Has(s.CustomName, HELM)) { helm = s; break; }
    }
    if (helm == null)
    {
        for (int i = 0; i < seats.Count; i++)
        {
            IMyShipController s = seats[i];
            if (s != null && Has(s.CustomName, TAG)) { helm = s; break; }
        }
    }

    GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(panels);
    for (int i = 0; i < panels.Count; i++)
    {
        IMyTextPanel p = panels[i];
        if (p == null || !Has(p.CustomName, TAG)) continue;
        int n = NumberTag(p.CustomName, "[WSS");
        if (n <= 0) continue;
        ExtDisplay e = new ExtDisplay();
        e.Num = n;
        e.Panel = p;
        e.Page = 0;
        e.Sel = 0;
        RestoreExternalState(e, oldExtNum, oldExtPage, oldExtSel);
        exts.Add(e);
    }
    exts.Sort(new ExtComparer());
    if (focus > exts.Count + 1) focus = 0;

    GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>(pbs);
    for (int i = 0; i < pbs.Count; i++)
    {
        IMyProgrammableBlock p = pbs[i];
        if (p == null || p == Me) continue;
        if (Has(p.CustomName, TAG) && Has(p.CustomName, DCS)) { dcsPb = p; break; }
    }
    ReadDcsBayPacket();
    lastStatus = "SCAN OK";
    lastCommand = "SCAN";
}

void RestoreExternalState(ExtDisplay e, List<int> oldNum, List<int> oldPage, List<int> oldSel)
{
    if (e == null) return;
    for (int i = 0; i < oldNum.Count; i++)
    {
        if (oldNum[i] != e.Num) continue;
        e.Page = Wrap(oldPage[i], EXT_PAGES.Length);
        e.Sel = Wrap(oldSel[i], Math.Max(1, bays.Length));
        return;
    }
}

void ReadDcsBayPacket()
{
    for (int i = 0; i < bays.Length; i++) { bays[i].State = "EMPTY"; bays[i].Serial = ""; bays[i].Role = ""; bays[i].Occupied = false; }
    if (dcsPb == null) return;
    string cd = dcsPb.CustomData == null ? "" : dcsPb.CustomData;
    for (int i = 0; i < bays.Length; i++)
    {
        string b = "BAY" + (i + 1).ToString("00");
        string st = FirstVal(cd, new string[] { b + ".State=", b + ".Status=", "Bay" + (i + 1).ToString("00") + ".State=" });
        string ser = FirstVal(cd, new string[] { b + ".Serial=", "Bay" + (i + 1).ToString("00") + ".Serial=" });
        string role = FirstVal(cd, new string[] { b + ".Role=", b + ".Short=", "Bay" + (i + 1).ToString("00") + ".Role=" });
        if (st.Length > 0) bays[i].State = st;
        if (ser.Length > 0) bays[i].Serial = ser;
        if (role.Length > 0) bays[i].Role = role;
        bays[i].Occupied = (ser.Length > 0 || StateMeansOccupied(bays[i].State));
    }
}

bool StateMeansOccupied(string s)
{
    s = Safe(s).ToUpperInvariant();
    return s == "MERGED" || s == "SERVICE" || s == "SERVICE_LOCKED" || s == "READY" || s == "COMPLETE" || s == "PRINTING" || s == "SVC";
}

void CycleFocus()
{
    // Project rule: external LCDs first, then HOTBAR/help, then return to center.
    // This keeps the focused LCD PAGE/NEXT controls grouped before returning to the main working screen.
    if (focus == 0) focus = exts.Count > 0 ? 2 : 1;
    else if (focus > 1)
    {
        if (focus - 2 < exts.Count - 1) focus++;
        else focus = 1;
    }
    else focus = 0;
    lastCommand = "FOCUS";
    lastStatus = "FOCUS " + FocusName();
}

void Page(int d)
{
    if (focus == 0) { page = Wrap(page + d, PAGES.Length); field = 0; }
    else if (focus == 1) { hotbarPage = Wrap(hotbarPage + d, 2); }
    else { ExtDisplay e = exts[focus - 2]; e.Page = Wrap(e.Page + d, EXT_PAGES.Length); e.Sel = 0; }
    lastCommand = d > 0 ? "PAGE NEXT" : "PAGE PREV";
    lastStatus = FocusName() + " PAGE";
}

void NextField(int d)
{
    if (focus == 1) { lastStatus = "FOCUS " + FocusName(); }
    else if (focus > 1)
    {
        ExtDisplay e = exts[focus - 2];
        int max = ExternalSelCount(e);
        e.Sel = Wrap(e.Sel + d, max);
        lastStatus = FocusName() + " SEL " + (e.Sel + 1).ToString("0");
    }
    else if (CurrentPageName() == "BAYS") selectedBay = Wrap(selectedBay + d, bays.Length);
    else field = Wrap(field + d, 4);
    lastCommand = d > 0 ? "NEXT" : "PREV";
}

void Adjust(int d)
{
    if (focus > 1)
    {
        // External LCD pages use PAGE NEXT/PREV. NEXT/PREV changes the page selection/subpage.
        // INC/DEC intentionally do not duplicate PAGE behavior.
        lastCommand = d > 0 ? "INC" : "DEC";
        lastStatus = FocusName() + " NO INC/DEC";
        return;
    }
    if (focus != 0) { lastCommand = d > 0 ? "INC" : "DEC"; lastStatus = "FOCUS " + FocusName(); return; }
    string p = CurrentPageName();
    if (p == "BAYS")
    {
        bays[selectedBay].Slot = Wrap(bays[selectedBay].Slot + d, BAY_SLOTS.Length);
        lastStatus = BayName(selectedBay) + " NEXT " + BAY_SLOTS[bays[selectedBay].Slot];
    }
    else if (p == "TACTICAL") RangeAdjust(d);
    else if (p == "COMMS") { if (d > 0) selectedWave = NextWave(1); else selectedWave = NextWave(-1); }
    else RangeAdjust(d);
    lastCommand = d > 0 ? "INC" : "DEC";
}

void Step()
{
    rangeStepIndex = Wrap(rangeStepIndex + 1, rangeSteps.Length);
    lastCommand = "STEP";
    lastStatus = "STEP " + Km(rangeSteps[rangeStepIndex]);
    if (marked) EnsureRayThenCompute();
}

void RangeAdjust(int d)
{
    rangeMeters += rangeSteps[rangeStepIndex] * d;
    if (rangeMeters < 250) rangeMeters = 250;
    if (rangeMeters > 50000) rangeMeters = 50000;
    if (marked) EnsureRayThenCompute();
    lastCommand = d > 0 ? "RANGE INC" : "RANGE DEC";
    lastStatus = "RANGE " + Km(rangeMeters);
}

void Mark()
{
    // MARK is a lock toggle: OPEN -> LOCKED captures the current boresight ray; LOCKED -> OPEN clears the lock.
    markSeq++;
    lastCommand = "MARK";
    if (marked)
    {
        marked = false;
        lastStatus = "MARK OPEN";
        return;
    }
    CaptureBoresightRay();
    marked = true;
    ComputeWaypoint();
    lastStatus = "MARK LOCKED " + Km(rangeMeters);
}

void EnsureMarkedWaypointLoaded()
{
    if (!marked) return;
    EnsureRayThenCompute();
}

void EnsureRayThenCompute()
{
    if (!rayValid) CaptureBoresightRay();
    ComputeWaypoint();
}

void CaptureBoresightRay()
{
    if (helm != null)
    {
        sourcePos = helm.GetPosition();
        forwardVec = helm.WorldMatrix.Forward;
    }
    else
    {
        sourcePos = Me.GetPosition();
        forwardVec = Me.WorldMatrix.Forward;
    }
    NormalizeForward();
    rayValid = true;
}

void NormalizeForward()
{
    double len = Math.Sqrt(forwardVec.X * forwardVec.X + forwardVec.Y * forwardVec.Y + forwardVec.Z * forwardVec.Z);
    if (len < 0.0001) forwardVec = new Vector3D(0, 0, -1);
    else forwardVec = new Vector3D(forwardVec.X / len, forwardVec.Y / len, forwardVec.Z / len);
}

void ComputeWaypoint()
{
    if (!rayValid) return;
    waypoint = sourcePos + forwardVec * rangeMeters;
}

void FireSendUpdate()
{
    fsuSeq++;
    if (!marked) Mark();
    lastCommand = "FSU";
    lastStatus = "FSU SEQ " + fsuSeq.ToString();
    ExportPacket(true, "FSU");
}

void Abort()
{
    abortSeq++;
    lastCommand = "ABORT";
    lastStatus = "ABORT SEQ " + abortSeq.ToString();
    ExportPacket(true, "ABORT");
}

void ExportPacket(bool evt, string cmd)
{
    seq++;
    StringBuilder s = new StringBuilder(2048);
    s.AppendLine(PACKET_BEGIN);
    s.AppendLine("Version=" + VERSION);
    s.AppendLine("Seq=" + seq.ToString());
    s.AppendLine("Command=" + cmd);
    s.AppendLine("CommandEvent=" + (evt ? "1" : "0"));
    s.AppendLine("Mode=BORESIGHT");
    s.AppendLine("SelectedWave=" + selectedWave);
    s.AppendLine("HotbarPage=" + (hotbarPage + 1).ToString());
    s.AppendLine("SelectedBay=" + BayName(selectedBay));
    s.AppendLine("SelectedBayNextSlot=" + BAY_SLOTS[bays[selectedBay].Slot]);
    s.AppendLine("SelectedBayNextRole=" + SLOT_NAMES[bays[selectedBay].Slot]);
    s.AppendLine("Focus=" + FocusName());
    s.AppendLine("HotbarPage=" + (hotbarPage + 1).ToString());
    s.AppendLine("RangeMeters=" + F(rangeMeters));
    s.AppendLine("RangeStepMeters=" + F(rangeSteps[rangeStepIndex]));
    s.AppendLine("Marked=" + (marked ? "1" : "0"));
    s.AppendLine("MarkSeq=" + markSeq.ToString());
    s.AppendLine("FsuSeq=" + fsuSeq.ToString());
    s.AppendLine("AbortSeq=" + abortSeq.ToString());
    s.AppendLine("Waypoint.X=" + F(waypoint.X));
    s.AppendLine("Waypoint.Y=" + F(waypoint.Y));
    s.AppendLine("Waypoint.Z=" + F(waypoint.Z));
    s.AppendLine("Source.X=" + F(sourcePos.X));
    s.AppendLine("Source.Y=" + F(sourcePos.Y));
    s.AppendLine("Source.Z=" + F(sourcePos.Z));
    s.AppendLine("Forward.X=" + F(forwardVec.X));
    s.AppendLine("Forward.Y=" + F(forwardVec.Y));
    s.AppendLine("Forward.Z=" + F(forwardVec.Z));
    for (int i = 0; i < bays.Length; i++) s.AppendLine("Bay" + (i + 1).ToString("00") + ".NextSlot=" + BAY_SLOTS[bays[i].Slot]);
    s.AppendLine(PACKET_END);
    string next = ReplaceBlock(Me.CustomData == null ? "" : Me.CustomData, PACKET_BEGIN, PACKET_END, s.ToString());
    if (next != Me.CustomData) Me.CustomData = next;
}


bool RayLooksValid()
{
    double srcLen = Math.Sqrt(sourcePos.X * sourcePos.X + sourcePos.Y * sourcePos.Y + sourcePos.Z * sourcePos.Z);
    double fwdLen = Math.Sqrt(forwardVec.X * forwardVec.X + forwardVec.Y * forwardVec.Y + forwardVec.Z * forwardVec.Z);
    return srcLen > 0.5 && fwdLen > 0.5;
}

void LoadConfig()
{
    string cd = Me.CustomData == null ? "" : Me.CustomData;
    double v;
    if (TryVal(cd, "RangeMeters=", out v) && v > 0) rangeMeters = v;
    if (TryVal(cd, "RangeStepMeters=", out v) && v > 0) { rangeSteps[rangeStepIndex] = v; }
    string w = Val(cd, "SelectedWave="); if (w.Length > 0) selectedWave = w;
    string hp = Val(cd, "HotbarPage="); int hpn; if (int.TryParse(hp, out hpn)) hotbarPage = Wrap(hpn - 1, 2);
    int n;
    if (int.TryParse(Val(cd, "Marked="), out n)) marked = n != 0;
    if (int.TryParse(Val(cd, "MarkSeq="), out n)) markSeq = n;
    if (int.TryParse(Val(cd, "FsuSeq="), out n)) fsuSeq = n;
    if (int.TryParse(Val(cd, "AbortSeq="), out n)) abortSeq = n;
    double x, y, z;
    bool hasSource = false, hasForward = false;
    if (TryVal(cd, "Waypoint.X=", out x) && TryVal(cd, "Waypoint.Y=", out y) && TryVal(cd, "Waypoint.Z=", out z)) waypoint = new Vector3D(x, y, z);
    if (TryVal(cd, "Source.X=", out x) && TryVal(cd, "Source.Y=", out y) && TryVal(cd, "Source.Z=", out z)) { sourcePos = new Vector3D(x, y, z); hasSource = true; }
    if (TryVal(cd, "Forward.X=", out x) && TryVal(cd, "Forward.Y=", out y) && TryVal(cd, "Forward.Z=", out z)) { forwardVec = new Vector3D(x, y, z); hasForward = true; }
    rayValid = hasSource && hasForward && RayLooksValid();
    if (rayValid) NormalizeForward();
    for (int i = 0; i < bays.Length; i++)
    {
        string slot = Val(cd, "Bay" + (i + 1).ToString("00") + ".NextSlot=");
        int idx = SlotIndex(slot); if (idx >= 0) bays[i].Slot = idx;
    }
}

void WriteConfigIfMissing()
{
    if ((Me.CustomData == null ? "" : Me.CustomData).IndexOf(CONFIG_BEGIN, StringComparison.OrdinalIgnoreCase) >= 0) return;
    StringBuilder s = new StringBuilder(1024);
    s.AppendLine(CONFIG_BEGIN);
    s.AppendLine("RangeMeters=" + F(rangeMeters));
    s.AppendLine("RangeStepMeters=" + F(rangeSteps[rangeStepIndex]));
    s.AppendLine("SelectedWave=" + selectedWave);
    s.AppendLine("Marked=" + (marked ? "1" : "0"));
    s.AppendLine("MarkSeq=" + markSeq.ToString());
    s.AppendLine("FsuSeq=" + fsuSeq.ToString());
    s.AppendLine("AbortSeq=" + abortSeq.ToString());
    s.AppendLine("Waypoint.X=" + F(waypoint.X));
    s.AppendLine("Waypoint.Y=" + F(waypoint.Y));
    s.AppendLine("Waypoint.Z=" + F(waypoint.Z));
    s.AppendLine("Source.X=" + F(sourcePos.X));
    s.AppendLine("Source.Y=" + F(sourcePos.Y));
    s.AppendLine("Source.Z=" + F(sourcePos.Z));
    s.AppendLine("Forward.X=" + F(forwardVec.X));
    s.AppendLine("Forward.Y=" + F(forwardVec.Y));
    s.AppendLine("Forward.Z=" + F(forwardVec.Z));
    s.AppendLine("SurfaceFrontLeft=" + SURF_FRONT_LEFT.ToString());
    s.AppendLine("SurfaceCenter=" + SURF_CENTER.ToString());
    s.AppendLine("SurfaceFrontRight=" + SURF_FRONT_RIGHT.ToString());
    s.AppendLine("SurfaceFarLeft=" + SURF_FAR_LEFT.ToString());
    s.AppendLine("SurfaceFarRight=" + SURF_FAR_RIGHT.ToString());
    for (int i = 0; i < bays.Length; i++) s.AppendLine("Bay" + (i + 1).ToString("00") + ".NextSlot=" + BAY_SLOTS[bays[i].Slot]);
    s.AppendLine(CONFIG_END);
    string next = ReplaceBlock(Me.CustomData == null ? "" : Me.CustomData, CONFIG_BEGIN, CONFIG_END, s.ToString());
    if (next != Me.CustomData) Me.CustomData = next;
}

void DrawAll()
{
    if (helm != null)
    {
        IMyTextSurfaceProvider sp = helm as IMyTextSurfaceProvider;
        if (sp != null)
        {
            if (screenIdTicks > 0) DrawSeatSurfaceIds(sp);
            else
            {
                DrawSeatSurface(sp, SURF_FRONT_LEFT, "PAGE");
                DrawSeatSurface(sp, SURF_CENTER, "CENTER");
                DrawSeatSurface(sp, SURF_FRONT_RIGHT, "DECOR_R");
                DrawSeatSurface(sp, SURF_FAR_LEFT, "DECOR_L");
                DrawSeatSurface(sp, SURF_FAR_RIGHT, "HOTBAR");
            }
        }
    }
    for (int i = 0; i < exts.Count; i++)
    {
        if (screenIdTicks > 0) DrawExternalId(exts[i]);
        else DrawExternal(exts[i], focus == i + 2);
    }
}

void DrawSeatSurfaceIds(IMyTextSurfaceProvider sp)
{
    for (int i = 0; i < sp.SurfaceCount; i++) DrawSurfaceId(sp.GetSurface(i), i, SeatRoleName(i));
}

string SeatRoleName(int idx)
{
    if (idx == SURF_FRONT_LEFT) return "CFG FRONT_LEFT";
    if (idx == SURF_CENTER) return "CFG CENTER";
    if (idx == SURF_FRONT_RIGHT) return "CFG FRONT_RIGHT";
    if (idx == SURF_FAR_LEFT) return "CFG FAR_LEFT";
    if (idx == SURF_FAR_RIGHT) return "CFG FAR_RIGHT";
    return "UNMAPPED";
}

void DrawExternalId(ExtDisplay e)
{
    if (e == null || e.Panel == null) return;
    DrawSurfaceId(e.Panel, e.Num, "EXT WSS" + e.Num.ToString("00"));
}

void DrawSurfaceId(IMyTextSurface s, int idx, string role)
{
    if (s == null) return;
    Prep(s);
    MySpriteDrawFrame f = s.DrawFrame();
    Vector2 size = s.SurfaceSize;
    Panel(ref f, size / 2f, size, BG);
    Panel(ref f, size / 2f, new Vector2(size.X - 18, size.Y - 18), PANEL2);
    Panel(ref f, new Vector2(size.X / 2f, 34), new Vector2(size.X - 38, 10), CYAN);
    Txt(ref f, "SURFACE", new Vector2(size.X / 2f, size.Y * 0.22f), 1.2f, TXT, TextAlignment.CENTER);
    Txt(ref f, idx.ToString(), new Vector2(size.X / 2f, size.Y * 0.47f), 4.2f, CYAN, TextAlignment.CENTER);
    Txt(ref f, role, new Vector2(size.X / 2f, size.Y * 0.73f), 0.95f, YELLOW, TextAlignment.CENTER);
    Txt(ref f, "Screenshot this", new Vector2(size.X / 2f, size.Y - 42), 0.55f, MUTED, TextAlignment.CENTER);
    f.Dispose();
}

void DrawSeatSurface(IMyTextSurfaceProvider sp, int idx, string role)
{
    if (idx < 0 || idx >= sp.SurfaceCount) return;
    IMyTextSurface s = sp.GetSurface(idx);
    if (role == "PAGE") DrawPageIndicator(s);
    else if (role == "CENTER") DrawWorkingPage(s, page, focus == 0);
    else if (role == "HOTBAR") DrawHotbar(s, focus == 1);
    else if (role == "DECOR_L") DrawDecorLeft(s);
    else DrawDecorRight(s);
}

void DrawExternal(ExtDisplay e, bool foc)
{
    if (e == null || e.Panel == null) return;
    DrawExternalReadout(e, foc);
}

void Prep(IMyTextSurface s)
{
    s.ContentType = ContentType.SCRIPT;
    s.Script = "";
    s.ScriptBackgroundColor = Bg();
    s.ScriptForegroundColor = TextCol();
}

void DrawPageIndicator(IMyTextSurface s)
{
    // Donor pattern: MB1 IMS DrawOfficerPages surface/page selector.
    // This screen must stay clean. Selected-bay detail is routed to FRONT_RIGHT.
    if (s == null) return;
    Prep(s);
    Vector2 size = s.SurfaceSize;
    Vector2 origin = GetOrigin(s);
    float w = size.X;
    float h = size.Y;
    float u = Math.Min(w / 256f, h / 171f);
    if (u < 0.45f) u = 0.45f;
    int per = 5;
    int sub = page / per;
    int total = (PAGES.Length + per - 1) / per;
    int first = sub * per;
    int last = first + per;
    if (last > PAGES.Length) last = PAGES.Length;
    MySpriteDrawFrame frame = s.DrawFrame();
    Panel(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w, h), BG);
    Stroke(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w - 8f * u, h - 8f * u), CYAN, 2f * u);
    Txt(ref frame, "PAGES", origin + new Vector2(w / 2f, 10f * u), TXT, .52f * u, TextAlignment.CENTER);
    if (total > 1) Txt(ref frame, (sub + 1).ToString("0") + "/" + total.ToString("0"), origin + new Vector2(w - 12f * u, 10f * u), MUTED, .38f * u, TextAlignment.RIGHT);
    float rowH = 23f * u;
    float startY = 33f * u;
    for (int i = first; i < last; i++)
    {
        bool selected = i == page;
        Vector2 c = origin + new Vector2(w / 2f, startY + (i - first) * rowH);
        Panel(ref frame, c, new Vector2(w - 22f * u, rowH - 4f * u), selected ? new Color(13, 44, 50) : new Color(4, 13, 18));
        Panel(ref frame, c + new Vector2(-w / 2f + 16f * u, 0), new Vector2(5f * u, rowH - 8f * u), selected ? GREEN : DIM);
        Txt(ref frame, PAGES[i], c + new Vector2(8f * u, -7f * u), selected ? TXT : MUTED, .32f * u, TextAlignment.CENTER);
    }
    frame.Dispose();
}


void DrawWorkingPage(IMyTextSurface s, int pg, bool focused)
{
    if (s == null) return;
    Prep(s);
    Vector2 z = s.TextureSize;
    float sc = Scale(z);
    MySpriteDrawFrame f = s.DrawFrame();
    DrawBg(ref f, z, sc);
    DrawHeader(ref f, z, sc, PAGES[pg], focused);
    if (PAGES[pg] == "BAYS") DrawBays(ref f, z, sc);
    else if (PAGES[pg] == "TACTICAL") DrawTactical(ref f, z, sc);
    else if (PAGES[pg] == "TURRETS") DrawTurrets(ref f, z, sc);
    else DrawComms(ref f, z, sc);
    if (!focused) DrawFocusOverlay(ref f, z, sc);
    f.Dispose();
}

void DrawFocusOverlay(ref MySpriteDrawFrame f, Vector2 size, float sc)
{
    // DONOR TRANSPLANT: MB1 WSS PB1 DrawSurface0 non-command focus path.
    // MB1 WSS calls DrawBg(ref f,size,sc), then DrawFocusCard(ref f,c,size,sc).
    // This wrapper preserves that exact donor path and adapts only the displayed focus string.
    Vector2 c = size * 0.5f;
    DrawBg(ref f, size, sc);
    DrawFocusCard(ref f, c, size, sc);
}

void DrawBg(ref MySpriteDrawFrame f, Vector2 size, float sc)
{
    // DONOR BODY: MB1 WSS PB1 DrawBg.
    for (float x = 0; x <= size.X; x += 32f * sc)
        Line(ref f, new Vector2(x, 0), new Vector2(x, size.Y), 0.55f * sc, Grid());
    for (float y = 0; y <= size.Y; y += 32f * sc)
        Line(ref f, new Vector2(0, y), new Vector2(size.X, y), 0.55f * sc, Grid());

    for (float x = 0; x <= size.X; x += 128f * sc)
        Line(ref f, new Vector2(x, 0), new Vector2(x, size.Y), 1.0f * sc, Grid2());
    for (float y = 0; y <= size.Y; y += 128f * sc)
        Line(ref f, new Vector2(0, y), new Vector2(size.X, y), 1.0f * sc, Grid2());
}

void DrawFocusCard(ref MySpriteDrawFrame f, Vector2 c, Vector2 size, float sc)
{
    // DONOR BODY: MB1 WSS PB1 DrawFocusCard.
    // Adapted text only: FocusName() replaced by FocusOverlayTarget(); safety state collapsed to LDC1 WSS authority string.
    Vector2 card = new Vector2(380, 225) * sc;
    BeveledFill(ref f, c + new Vector2(5, 7) * sc, card, 16f * sc, Shadow());
    BeveledFill(ref f, c, card, 16f * sc, new Color(5, 24, 34, 232));
    BeveledOutline(ref f, c, card, 16f * sc, Cyan(), 1.7f * sc);
    BeveledOutline(ref f, c, card - new Vector2(22, 22) * sc, 12f * sc, new Color(220, 252, 255, 95), 0.8f * sc);

    Text(ref f, "FOCUS", c + new Vector2(0, -78f * sc), Pale(), 0.95f * sc, TextAlignment.CENTER);
    Text(ref f, FocusOverlayTarget(), c + new Vector2(0, -6f * sc), Cyan(), 0.82f * sc, TextAlignment.CENTER);
    Text(ref f, "COMMAND OFFLINE", c + new Vector2(0, 55f * sc), new Color(220, 252, 255, 205), 0.46f * sc, TextAlignment.CENTER);

    string state = "AUTHORITY READY";
    Color stateCol = Cyan();
    Text(ref f, state, c + new Vector2(0, 98f * sc), stateCol, 0.38f * sc, TextAlignment.CENTER);
}

string FocusOverlayTarget()
{
    if (focus == 1) return "HELP";
    if (focus > 1 && focus - 2 < exts.Count)
    {
        ExtDisplay e = exts[focus - 2];
        return "WSS" + e.Num.ToString("0") + "  " + ExtPageName(e);
    }
    return "CENTER";
}

void DrawHeader(ref MySpriteDrawFrame f, Vector2 z, float sc, string title, bool focused)
{
    float px = CENTER_PAD_X * sc;
    float top = CENTER_PAD_TOP * sc;
    Panel(ref f, new Vector2(z.X / 2, top + 18 * sc), new Vector2(z.X - px * 2, 42 * sc), PANEL2);
    Txt(ref f, title, new Vector2(px + 16 * sc, top + 4 * sc), .82f * sc, CYAN, TextAlignment.LEFT);
    Txt(ref f, focused ? "FOCUS" : "VIEW", new Vector2(z.X - px - 16 * sc, top + 6 * sc), .56f * sc, focused ? GREEN : MUTED, TextAlignment.RIGHT);
}

void DrawBays(ref MySpriteDrawFrame f, Vector2 z, float sc)
{
    float px = CENTER_PAD_X * sc, top = CENTER_PAD_TOP * sc, bot = CENTER_PAD_BOTTOM * sc;
    float left = px, right = z.X - px;
    float workTop = top + 58 * sc;
    float workBottom = z.Y - bot;
    float workW = right - left;

    Bay b = bays[selectedBay];
    Vector2 selCard = new Vector2(left + workW * .23f, workTop + 30 * sc);
    Vector2 selSize = new Vector2(workW * .38f, 56 * sc);
    Panel(ref f, selCard, selSize, new Color(5, 18, 24));
    Stroke(ref f, selCard, selSize, CYAN, 2 * sc);
    Txt(ref f, BayName(selectedBay), new Vector2(selCard.X - selSize.X * .42f, selCard.Y - 18 * sc), .50f * sc, CYAN, TextAlignment.LEFT);
    Txt(ref f, b.Occupied ? "OCCUPIED" : "EMPTY", new Vector2(selCard.X + selSize.X * .42f, selCard.Y - 18 * sc), .46f * sc, BayStatusColor(b), TextAlignment.RIGHT);
    Txt(ref f, BAY_SLOTS[b.Slot] + "  " + SLOT_NAMES[b.Slot], new Vector2(selCard.X, selCard.Y + 10 * sc), .43f * sc, MUTED, TextAlignment.CENTER);

    float bw = workW * .135f;
    float bh = (workBottom - workTop) * .205f;
    float gap = workW * .032f;
    Vector2 gridC = new Vector2(z.X * .5f, workTop + (workBottom - workTop) * .52f);
    for (int i = 0; i < bays.Length; i++)
    {
        int row = i < 5 ? 0 : 1;
        int col = i % 5;
        float x = gridC.X + (col - 2) * (bw + gap);
        float y = gridC.Y + (row == 0 ? -bh * .66f : bh * .66f);
        DrawBay(ref f, bays[i], new Vector2(x, y), new Vector2(bw, bh), sc, i == selectedBay);
    }
}


void DrawBay(ref MySpriteDrawFrame f, Bay b, Vector2 c, Vector2 sz, float sc, bool sel)
{
    Color state = BayStatusColor(b);
    Color edge = sel ? CYAN : (b.Occupied ? GREEN : DIM);
    Color fill = b.Occupied ? new Color(6, 31, 22) : new Color(4, 14, 18);
    Panel(ref f, c, sz, fill);
    Stroke(ref f, c, sz, edge, sel ? 4 * sc : 2 * sc);
    Txt(ref f, "B" + b.Num.ToString("00"), new Vector2(c.X, c.Y - sz.Y * .34f), .50f * sc, sel ? CYAN : TXT, TextAlignment.CENTER);
    string occ = b.Occupied ? "OCC" : "EMPTY";
    Txt(ref f, occ, new Vector2(c.X, c.Y - sz.Y * .02f), .45f * sc, state, TextAlignment.CENTER);
    Txt(ref f, BAY_SLOTS[b.Slot], new Vector2(c.X, c.Y + sz.Y * .30f), .40f * sc, b.Occupied ? GREEN : MUTED, TextAlignment.CENTER);
}

Color BayStatusColor(Bay b)
{
    if (b == null || !b.Occupied) return MUTED;
    string st = Safe(b.State).ToUpperInvariant();
    if (st == "FAULT" || st == "ERROR") return RED;
    return GREEN;
}


void DrawTactical(ref MySpriteDrawFrame f, Vector2 z, float sc)
{
    float px = CENTER_PAD_X * sc;
    float top = CENTER_PAD_TOP * sc;
    float left = px;
    float right = z.X - px;
    float w = right - left;

    // Hard cockpit fit: top cards are lowered inside the visible safe area; waypoint card remains above the physical bottom cutoff.
    float topCardY = top + 112 * sc;
    Vector2 topSize = new Vector2(w * .34f, 74 * sc);
    Card(ref f, new Vector2(left + w * .25f, topCardY), topSize, "RANGE", Km(rangeMeters), "STEP " + Km(rangeSteps[rangeStepIndex]), CYAN, sc);
    Card(ref f, new Vector2(left + w * .75f, topCardY), topSize, "MARK", marked ? "LOCKED" : "OPEN", "SEQ " + markSeq.ToString(), marked ? GREEN : YELLOW, sc);

    float mainTop = topCardY + topSize.Y * .5f + 18 * sc;
    float mainH = 136 * sc;
    float maxBottom = z.Y - 142 * sc;
    if (mainTop + mainH > maxBottom) mainH = maxBottom - mainTop;
    if (mainH < 118 * sc) mainH = 118 * sc;
    Vector2 mainSz = new Vector2(w * .70f, mainH);
    Vector2 mainC = new Vector2(z.X * .5f, mainTop + mainH * .5f);
    Panel(ref f, mainC, mainSz, PANEL);
    Stroke(ref f, mainC, mainSz, CYAN, 2 * sc);

    float tx = mainC.X - mainSz.X * .5f + 28 * sc;
    float ty = mainC.Y - mainSz.Y * .5f + 26 * sc;
    Txt(ref f, "BORESIGHT WAYPOINT", new Vector2(tx, ty), .46f * sc, CYAN, TextAlignment.LEFT);

    float rowY = ty + 35 * sc;
    float rowGap = 27 * sc;
    Txt(ref f, "X " + F(waypoint.X), new Vector2(tx + 8 * sc, rowY), .38f * sc, TXT, TextAlignment.LEFT);
    Txt(ref f, "Y " + F(waypoint.Y), new Vector2(tx + 8 * sc, rowY + rowGap), .38f * sc, TXT, TextAlignment.LEFT);
    Txt(ref f, "Z " + F(waypoint.Z), new Vector2(tx + 8 * sc, rowY + rowGap * 2f), .38f * sc, TXT, TextAlignment.LEFT);
}


void DrawTurrets(ref MySpriteDrawFrame f, Vector2 z, float sc)
{
    float px = CENTER_PAD_X * sc, top = CENTER_PAD_TOP * sc, bot = CENTER_PAD_BOTTOM * sc;
    float left = px, right = z.X - px, w = right - left;
    float workTop = top + 66 * sc, workBottom = z.Y - bot;
    Panel(ref f, new Vector2(z.X * .5f, (workTop + workBottom) * .5f), new Vector2(w * .94f, workBottom - workTop), PANEL);
    Txt(ref f, "TURRET POSTURE", new Vector2(left + 28 * sc, workTop + 24 * sc), .64f * sc, CYAN, TextAlignment.LEFT);
    DrawArc(ref f, new Vector2(z.X * .5f, workTop + (workBottom - workTop) * .48f), (workBottom - workTop) * .24f, sc, CYAN);
    Txt(ref f, "STANCE", new Vector2(left + 34 * sc, workBottom - 70 * sc), .50f * sc, MUTED, TextAlignment.LEFT);
    Txt(ref f, "PLACEHOLDER", new Vector2(right - 34 * sc, workBottom - 70 * sc), .50f * sc, YELLOW, TextAlignment.RIGHT);
    Txt(ref f, "MB1 WSS TURRET STATUS PATTERN", new Vector2(z.X * .5f, workBottom - 32 * sc), .42f * sc, MUTED, TextAlignment.CENTER);
}

void DrawComms(ref MySpriteDrawFrame f, Vector2 z, float sc)
{
    float px = CENTER_PAD_X * sc, top = CENTER_PAD_TOP * sc, bot = CENTER_PAD_BOTTOM * sc;
    float left = px, right = z.X - px, w = right - left;
    float workTop = top + 72 * sc, workBottom = z.Y - bot - 10 * sc;
    Card(ref f, new Vector2(left + w * .24f, workTop + 56 * sc), new Vector2(w * .34f, 90 * sc), "PACKET", "SEQ " + seq.ToString(), "WSS", CYAN, sc);
    Card(ref f, new Vector2(left + w * .76f, workTop + 56 * sc), new Vector2(w * .38f, 90 * sc), "DCS LINK", dcsPb != null ? "SEEN" : "NO PB", "READ ONLY", dcsPb != null ? GREEN : YELLOW, sc);
    float cardTop = workTop + 122 * sc;
    float cardH = workBottom - cardTop;
    Vector2 c = new Vector2(z.X * .5f, cardTop + cardH * .5f);
    Vector2 sz = new Vector2(w * .90f, cardH);
    Panel(ref f, c, sz, PANEL);
    Stroke(ref f, c, sz, CYAN, 2 * sc);
    float tx = c.X - sz.X * .5f + 28 * sc;
    float ty = c.Y - sz.Y * .5f + 28 * sc;
    Txt(ref f, "WSS -> DCS CONTRACT", new Vector2(tx, ty), .54f * sc, CYAN, TextAlignment.LEFT);
    Txt(ref f, "FSU sends bay / wave / range / waypoint", new Vector2(tx, ty + 52 * sc), .40f * sc, TXT, TextAlignment.LEFT);
    Txt(ref f, "DCS consumption not patched yet", new Vector2(tx, ty + 92 * sc), .40f * sc, YELLOW, TextAlignment.LEFT);
}





void DrawHotbar(IMyTextSurface s, bool focused)
{
    // Exact donor port from MB1 IMS PB1 V58 DrawOfficerHelp:
    // SurfaceSize + GetOrigin; 256x192 scale basis; title .68*u; commands .52*u;
    // y=50*u; lh=32*u; lx=.29w; rx=.71w; frame w-8u/h-8u.
    if (s == null) return;
    Prep(s);
    Vector2 size = s.SurfaceSize;
    Vector2 origin = GetOrigin(s);
    float w = size.X;
    float h = size.Y;
    float u = Math.Min(w / 256f, h / 192f);
    if (u < 0.45f) u = 0.45f;
    MySpriteDrawFrame frame = s.DrawFrame();
    Panel(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w, h), BG);
    if (focused) Stroke(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w - 8f * u, h - 8f * u), CYAN, 2f * u);
    Txt(ref frame, hotbarPage == 0 ? "HOTBAR" : "HOTBAR 2", origin + new Vector2(w / 2f, 10f * u), .68f * u, TXT, TextAlignment.CENTER);

    float y = 50f * u;
    float lh = 32f * u;
    float lx = w * 0.29f;
    float rx = w * 0.71f;

    string[] l; string[] r;
    if (hotbarPage == 0)
    {
        l = new string[] { "PAGE PREV", "PREV", "DEC", "STEP" };
        r = new string[] { "PAGE NEXT", "NEXT", "INC", "FOCUS" };
    }
    else
    {
        l = new string[] { "RANGE DEC", "MARK", "ABORT", "STATUS" };
        r = new string[] { "RANGE INC", "FSU", "SCAN", "IDSCREENS" };
    }

    Txt(ref frame, l[0], origin + new Vector2(lx, y), GREEN, .52f * u, TextAlignment.CENTER);
    Txt(ref frame, r[0], origin + new Vector2(rx, y), GREEN, .52f * u, TextAlignment.CENTER);
    Txt(ref frame, l[1], origin + new Vector2(lx, y + lh), GREEN, .52f * u, TextAlignment.CENTER);
    Txt(ref frame, r[1], origin + new Vector2(rx, y + lh), GREEN, .52f * u, TextAlignment.CENTER);
    Txt(ref frame, l[2], origin + new Vector2(lx, y + lh * 2f), GREEN, .52f * u, TextAlignment.CENTER);
    Txt(ref frame, r[2], origin + new Vector2(rx, y + lh * 2f), GREEN, .52f * u, TextAlignment.CENTER);
    Txt(ref frame, l[3], origin + new Vector2(lx, y + lh * 3f), GREEN, .52f * u, TextAlignment.CENTER);
    Txt(ref frame, r[3], origin + new Vector2(rx, y + lh * 3f), GREEN, .52f * u, TextAlignment.CENTER);
    frame.Dispose();
}

string ExtPageName(ExtDisplay e)
{
    if (e == null) return "EXT";
    if (e.Page < 0 || e.Page >= EXT_PAGES.Length) e.Page = 0;
    return EXT_PAGES[e.Page];
}

int ExternalSelCount(ExtDisplay e)
{
    if (e == null) return 1;
    if (e.Page == 0) return bays.Length;
    if (e.Page == 1) return (bays.Length + 7) / 8;
    return 1;
}

string FocusDetailTitle()
{
    if (focus == 1) return hotbarPage == 0 ? "HOTBAR 1" : "HOTBAR 2";
    if (focus > 1 && focus - 2 < exts.Count)
    {
        ExtDisplay e = exts[focus - 2];
        return "WSS" + e.Num.ToString("0") + "  " + ExtPageName(e) + "  " + (e.Page + 1).ToString("0") + "/" + EXT_PAGES.Length.ToString("0");
    }
    return "CENTER";
}

void DrawExternalReadout(ExtDisplay e, bool focused)
{
    // Donor pattern: MB1 IMS DrawDetailLCD external screen format.
    if (e == null || e.Panel == null) return;
    IMyTextSurface s = e.Panel;
    Prep(s);
    Vector2 size = s.SurfaceSize;
    Vector2 origin = GetOrigin(s);
    float w = size.X;
    float h = size.Y;
    float u = Math.Min(w / 512f, h / 512f);
    if (u < 0.45f) u = 0.45f;
    if (e.Page < 0 || e.Page >= EXT_PAGES.Length) e.Page = 0;
    MySpriteDrawFrame frame = s.DrawFrame();
    Panel(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w, h), BG);
    Stroke(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w - 14f * u, h - 14f * u), CYAN, 3f * u);
    Txt(ref frame, "WSS " + ExtPageName(e), origin + new Vector2(24f * u, 18f * u), .62f * u, TXT, TextAlignment.LEFT);
    Txt(ref frame, "D" + e.Num.ToString("0") + "  " + (e.Page + 1).ToString("0") + "/" + EXT_PAGES.Length.ToString("0"), origin + new Vector2(w - 24f * u, 18f * u), .50f * u, MUTED, TextAlignment.RIGHT);
    float top = 62f * u;
    float rowH = (h - 92f * u) / 8f;
    if (e.Page == 0) DrawExternalBayDetail(ref frame, origin, w, top, rowH, e, u);
    else if (e.Page == 1) DrawExternalDroneRoster(ref frame, origin, w, top, rowH, e, u);
    else DrawExternalTactical(ref frame, origin, w, h, top, rowH, e, u);
    if (focused) DrawExternalFocusRails(ref frame, origin, w, h, u);
    frame.Dispose();
}

void DrawExternalBayDetail(ref MySpriteDrawFrame frame, Vector2 origin, float w, float top, float rowH, ExtDisplay e, float u)
{
    int bi = Wrap(e.Sel, bays.Length);
    Bay b = bays[bi];
    DrawExternalRow(ref frame, origin, w, top + rowH * 0, rowH, "BAY", BayName(bi), CYAN, u, true);
    DrawExternalRow(ref frame, origin, w, top + rowH * 1, rowH, "NEXT DRONE", BAY_SLOTS[b.Slot] + "  " + SLOT_NAMES[b.Slot], GREEN, u, false);
    DrawExternalRow(ref frame, origin, w, top + rowH * 2, rowH, "BAY STATE", b.Occupied ? b.State : "EMPTY", BayStatusColor(b), u, false);
    DrawExternalRow(ref frame, origin, w, top + rowH * 3, rowH, "DRONE ID", b.Serial.Length > 0 ? b.Serial : "--", TXT, u, false);
    DrawExternalRow(ref frame, origin, w, top + rowH * 4, rowH, "ROLE", b.Role.Length > 0 ? b.Role : SLOT_NAMES[b.Slot], MUTED, u, false);
}


void DrawExternalDroneRoster(ref MySpriteDrawFrame frame, Vector2 origin, float w, float top, float rowH, ExtDisplay e, float u)
{
    int pageIndex = Wrap(e.Sel, (bays.Length + 7) / 8);
    int first = pageIndex * 8;
    for (int r = 0; r < 8; r++)
    {
        int i = first + r;
        if (i >= bays.Length) break;
        Bay b = bays[i];
        string val = (b.Serial.Length > 0 ? b.Serial : (b.Occupied ? b.State : "EMPTY"));
        DrawExternalRow(ref frame, origin, w, top + rowH * r, rowH, BayName(i), val, BayStatusColor(b), u, false);
    }
}


void DrawExternalTactical(ref MySpriteDrawFrame frame, Vector2 origin, float w, float h, float top, float rowH, ExtDisplay e, float u)
{
    DrawExternalRow(ref frame, origin, w, top + rowH * 0, rowH, "RANGE", Km(rangeMeters), CYAN, u, false);
    DrawExternalRow(ref frame, origin, w, top + rowH * 1, rowH, "RANGE STEP", Km(rangeSteps[rangeStepIndex]), MUTED, u, false);
    DrawExternalRow(ref frame, origin, w, top + rowH * 2, rowH, "MARK", marked ? "LOCKED" : "OPEN", marked ? GREEN : YELLOW, u, false);
    DrawExternalRow(ref frame, origin, w, top + rowH * 3, rowH, "WAVE", selectedWave, TXT, u, false);
    DrawExternalRow(ref frame, origin, w, top + rowH * 4, rowH, "WAYPOINT X", F(waypoint.X), MUTED, u, false);
    DrawExternalRow(ref frame, origin, w, top + rowH * 5, rowH, "WAYPOINT Y", F(waypoint.Y), MUTED, u, false);
    DrawExternalRow(ref frame, origin, w, top + rowH * 6, rowH, "WAYPOINT Z", F(waypoint.Z), MUTED, u, false);
}




string focusedText(ExtDisplay e)
{
    return focus > 1 && focus - 2 < exts.Count && exts[focus - 2] == e ? "YES" : "NO";
}

void DrawExternalRow(ref MySpriteDrawFrame frame, Vector2 origin, float w, float y, float rowH, string label, string value, Color c, float u, bool selected)
{
    float left = 26f * u;
    float right = w - 26f * u;
    Vector2 row = origin + new Vector2(w / 2f, y + rowH * 0.50f);
    Panel(ref frame, row, new Vector2(w - 34f * u, rowH - 5f * u), selected ? new Color(13, 44, 50) : new Color(4, 13, 18));
    Panel(ref frame, origin + new Vector2(left + 5f * u, y + rowH * 0.50f), new Vector2(8f * u, rowH - 13f * u), c);
    Txt(ref frame, label, origin + new Vector2(left + 20f * u, y + rowH * 0.22f), .46f * u, TXT, TextAlignment.LEFT);
    Txt(ref frame, value, origin + new Vector2(right - 4f * u, y + rowH * 0.22f), .46f * u, c, TextAlignment.RIGHT);
}

void DrawExternalFocusRails(ref MySpriteDrawFrame frame, Vector2 origin, float w, float h, float u)
{
    // Donor pattern: MB1 IMS DrawDetailFocusRails.
    Color col = TXT;
    float yTop = origin.Y + h * 0.035f;
    float yBot = origin.Y + h * 0.965f;
    float x1 = origin.X + w * 0.175f;
    float x2 = origin.X + w * 0.825f;
    float wing = 18f * u;
    float outY = 12f * u;
    float lw = 1.55f * u;
    Line(ref frame, new Vector2(x1, yTop), new Vector2(x2, yTop), lw, col);
    Line(ref frame, new Vector2(x1, yBot), new Vector2(x2, yBot), lw, col);
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

void DrawDecorLeft(IMyTextSurface s)
{
    if (s == null) return;
    Prep(s);
    Vector2 z = s.TextureSize; float sc = Scale(z); float pt = SIDE_PAD_TOP * sc; float pb = SIDE_PAD_BOTTOM * sc; MySpriteDrawFrame f = s.DrawFrame(); DrawBg(ref f, z, sc);
    Panel(ref f, new Vector2(z.X / 2, z.Y / 2 + pt * .2f), new Vector2(z.X - SIDE_PAD_X * 2 * sc, z.Y - pt - pb), new Color(4, 12, 17));
    DrawCarrierGlyph(ref f, new Vector2(z.X / 2, z.Y * .54f), Math.Min(z.X, z.Y) * .30f, sc);
    Txt(ref f, "LDC1", new Vector2(z.X / 2, pt + 10 * sc), .70f * sc, CYAN, TextAlignment.CENTER);
    Txt(ref f, "CARRIER", new Vector2(z.X / 2, z.Y - pb - 16 * sc), .48f * sc, MUTED, TextAlignment.CENTER);
    f.Dispose();
}

void DrawDecorRight(IMyTextSurface s)
{
    // FRONT_RIGHT: selected-bay helper/detail visual. Moved here from FRONT_LEFT so
    // the proven page selector stays clean and legible.
    if (s == null) return;
    Prep(s);
    Vector2 size = s.SurfaceSize;
    Vector2 origin = GetOrigin(s);
    float w = size.X;
    float h = size.Y;
    float u = Math.Min(w / 256f, h / 192f);
    if (u < 0.45f) u = 0.45f;
    Bay b = bays[selectedBay];
    MySpriteDrawFrame frame = s.DrawFrame();
    Panel(ref frame, origin + new Vector2(w / 2f, h / 2f), new Vector2(w, h), BG);
    Txt(ref frame, "BAY DETAIL", origin + new Vector2(w / 2f, 12f * u), TXT, .58f * u, TextAlignment.CENTER);
    Txt(ref frame, BayName(selectedBay), origin + new Vector2(w / 2f, 50f * u), CYAN, 1.05f * u, TextAlignment.CENTER);
    Color state = BayStatusColor(b);
    Panel(ref frame, origin + new Vector2(w / 2f, 90f * u), new Vector2(w - 34f * u, 32f * u), new Color(4, 13, 18));
    Panel(ref frame, origin + new Vector2(24f * u, 90f * u), new Vector2(7f * u, 24f * u), state);
    Txt(ref frame, b.Occupied ? b.State : "EMPTY", origin + new Vector2(w / 2f, 79f * u), state, .52f * u, TextAlignment.CENTER);
    Txt(ref frame, BAY_SLOTS[b.Slot], origin + new Vector2(w * .29f, 126f * u), GREEN, .50f * u, TextAlignment.CENTER);
    Txt(ref frame, SLOT_NAMES[b.Slot], origin + new Vector2(w * .71f, 126f * u), MUTED, .44f * u, TextAlignment.CENTER);
    string ident = b.Serial.Length > 0 ? b.Serial : "NO DRONE";
    Txt(ref frame, ident, origin + new Vector2(w / 2f, h - 34f * u), b.Occupied ? state : DIM, .44f * u, TextAlignment.CENTER);
    frame.Dispose();
}

void Card(ref MySpriteDrawFrame f, Vector2 c, Vector2 sz, string a, string b, string ctext, Color accent, float sc)
{
    Panel(ref f, c, sz, PANEL);
    Stroke(ref f, c, sz, accent, 2 * sc);
    float p = 18 * sc;
    Txt(ref f, a, new Vector2(c.X - sz.X * .5f + p, c.Y - sz.Y * .5f + p * .8f), .46f * sc, MUTED, TextAlignment.LEFT);
    Txt(ref f, b, new Vector2(c.X, c.Y - sz.Y * .02f), .82f * sc, accent, TextAlignment.CENTER);
    Txt(ref f, ctext, new Vector2(c.X, c.Y + sz.Y * .30f), .44f * sc, TXT, TextAlignment.CENTER);
}

void DrawCarrierGlyph(ref MySpriteDrawFrame f, Vector2 c, float r, float sc)
{
    Bar(ref f, new Vector2(c.X, c.Y), new Vector2(r * .34f, r * 1.45f), DIM);
    Bar(ref f, new Vector2(c.X - r * .34f, c.Y), new Vector2(r * .18f, r * 1.20f), CYAN);
    Bar(ref f, new Vector2(c.X + r * .34f, c.Y), new Vector2(r * .18f, r * 1.20f), CYAN);
    for (int i = 0; i < 5; i++)
    {
        float y = c.Y - r * .50f + i * r * .25f;
        Bar(ref f, new Vector2(c.X - r * .34f, y), new Vector2(r * .30f, 3 * sc), MUTED);
        Bar(ref f, new Vector2(c.X + r * .34f, y), new Vector2(r * .30f, 3 * sc), MUTED);
    }
}

void DrawArc(ref MySpriteDrawFrame f, Vector2 c, float r, float sc, Color col)
{
    for (int i = 0; i < 17; i++)
    {
        double a = Math.PI * (0.10 + i * 0.80 / 16.0);
        Vector2 p = new Vector2(c.X + (float)Math.Cos(a) * r, c.Y - (float)Math.Sin(a) * r);
        Bar(ref f, p, new Vector2(5 * sc, 5 * sc), col);
    }
    Bar(ref f, c, new Vector2(8 * sc, 8 * sc), GREEN);
}


// ===== MB1 WSS PB1 donor drawing helpers. Do not remake without explicit approval. =====
Color Bg() { return new Color(2, 7, 11); }
Color PanelColor() { return new Color(5, 24, 34, 240); }
Color Grid() { return new Color(20, 74, 92, 72); }
Color Grid2() { return new Color(42, 130, 148, 100); }
Color Cyan() { return new Color(82, 205, 245); }
Color WarnOrange() { return new Color(255, 146, 48); }
Color Pale() { return new Color(220, 252, 255); }
Color TextCol() { return new Color(210, 248, 255); }
Color Shadow() { return new Color(0, 0, 0, 185); }

void Text(ref MySpriteDrawFrame f, string text, Vector2 pos, Color col, float scale, TextAlignment align)
{
    // DONOR BODY: MB1 WSS PB1 Text helper uses Debug font.
    MySprite sp = MySprite.CreateText(text, "Debug", col, scale, align);
    sp.Position = pos;
    f.Add(sp);
}

void Rect(ref MySpriteDrawFrame f, Vector2 c, Vector2 sz, Color col, bool fill, float w, float rot)
{
    // DONOR BODY: MB1 WSS PB1 Rect helper.
    if (fill)
    {
        f.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", c, sz, col, null, TextAlignment.CENTER, rot));
        return;
    }

    if (Math.Abs(rot) < 0.001f)
    {
        Vector2 h = sz * 0.5f;
        Line(ref f, c + new Vector2(-h.X, -h.Y), c + new Vector2(h.X, -h.Y), w, col);
        Line(ref f, c + new Vector2(h.X, -h.Y), c + new Vector2(h.X, h.Y), w, col);
        Line(ref f, c + new Vector2(h.X, h.Y), c + new Vector2(-h.X, h.Y), w, col);
        Line(ref f, c + new Vector2(-h.X, h.Y), c + new Vector2(-h.X, -h.Y), w, col);
        return;
    }

    Vector2 x = new Vector2((float)Math.Cos(rot), (float)Math.Sin(rot));
    Vector2 y = new Vector2(-x.Y, x.X);
    Vector2 hx = x * sz.X * 0.5f;
    Vector2 hy = y * sz.Y * 0.5f;

    Vector2 a = c - hx - hy;
    Vector2 b = c + hx - hy;
    Vector2 d = c + hx + hy;
    Vector2 e = c - hx + hy;

    Line(ref f, a, b, w, col);
    Line(ref f, b, d, w, col);
    Line(ref f, d, e, w, col);
    Line(ref f, e, a, w, col);
}

void FillTri(ref MySpriteDrawFrame f, Vector2 a, Vector2 b, Vector2 c, Color col, float step)
{
    // DONOR BODY: MB1 WSS PB1 FillTri helper.
    float minY = Math.Min(a.Y, Math.Min(b.Y, c.Y));
    float maxY = Math.Max(a.Y, Math.Max(b.Y, c.Y));

    for (float y = minY; y <= maxY; y += step)
    {
        float x1 = 0f;
        float x2 = 0f;
        int hits = 0;

        Edge(a, b, y, ref x1, ref x2, ref hits);
        Edge(b, c, y, ref x1, ref x2, ref hits);
        Edge(c, a, y, ref x1, ref x2, ref hits);

        if (hits >= 2)
        {
            if (x2 < x1)
            {
                float t = x1;
                x1 = x2;
                x2 = t;
            }
            Line(ref f, new Vector2(x1, y), new Vector2(x2, y), step + 0.28f, col);
        }
    }
}

void Edge(Vector2 p1, Vector2 p2, float y, ref float x1, ref float x2, ref int hits)
{
    // DONOR BODY: MB1 WSS PB1 Edge helper.
    if ((y < p1.Y && y < p2.Y) || (y > p1.Y && y > p2.Y)) return;
    if (Math.Abs(p2.Y - p1.Y) < 0.001f) return;

    float t = (y - p1.Y) / (p2.Y - p1.Y);
    if (t < 0f || t > 1f) return;
    float x = p1.X + (p2.X - p1.X) * t;
    if (hits == 0) x1 = x;
    else x2 = x;
    hits++;
}

void BeveledFill(ref MySpriteDrawFrame f, Vector2 c, Vector2 size, float cut, Color col)
{
    // DONOR BODY: MB1 WSS PB1 BeveledFill.
    float hw = size.X * 0.5f;
    float hh = size.Y * 0.5f;

    Rect(ref f, c, new Vector2(size.X - 2f * cut, size.Y), col, true, 1f, 0f);
    Rect(ref f, c, new Vector2(size.X, size.Y - 2f * cut), col, true, 1f, 0f);

    FillTri(ref f, c + new Vector2(-hw + cut, -hh), c + new Vector2(-hw, -hh + cut), c + new Vector2(-hw + cut, -hh + cut), col, 1f);
    FillTri(ref f, c + new Vector2(hw - cut, -hh), c + new Vector2(hw, -hh + cut), c + new Vector2(hw - cut, -hh + cut), col, 1f);
    FillTri(ref f, c + new Vector2(-hw + cut, hh), c + new Vector2(-hw, hh - cut), c + new Vector2(-hw + cut, hh - cut), col, 1f);
    FillTri(ref f, c + new Vector2(hw - cut, hh), c + new Vector2(hw, hh - cut), c + new Vector2(hw - cut, hh - cut), col, 1f);
}

void BeveledOutline(ref MySpriteDrawFrame f, Vector2 c, Vector2 size, float cut, Color col, float w)
{
    // DONOR BODY: MB1 WSS PB1 BeveledOutline.
    float hw = size.X * 0.5f;
    float hh = size.Y * 0.5f;

    Vector2 a = c + new Vector2(-hw + cut, -hh);
    Vector2 b = c + new Vector2(hw - cut, -hh);
    Vector2 c1 = c + new Vector2(hw, -hh + cut);
    Vector2 d = c + new Vector2(hw, hh - cut);
    Vector2 e = c + new Vector2(hw - cut, hh);
    Vector2 g = c + new Vector2(-hw + cut, hh);
    Vector2 h = c + new Vector2(-hw, hh - cut);
    Vector2 i = c + new Vector2(-hw, -hh + cut);

    Line(ref f, a, b, w, col);
    Line(ref f, b, c1, w, col);
    Line(ref f, c1, d, w, col);
    Line(ref f, d, e, w, col);
    Line(ref f, e, g, w, col);
    Line(ref f, g, h, w, col);
    Line(ref f, h, i, w, col);
    Line(ref f, i, a, w, col);
}

Vector2 GetOrigin(IMyTextSurface s)
{
    return (s.TextureSize - s.SurfaceSize) / 2f;
}

void Txt(ref MySpriteDrawFrame f, string text, Vector2 pos, Color color, float size, TextAlignment align)
{
    Txt(ref f, text, pos, size, color, align);
}

void Panel(ref MySpriteDrawFrame f, Vector2 pos, Vector2 size, Color color)
{
    MySprite s = MySprite.CreateSprite("SquareSimple", pos, size);
    s.Color = color;
    f.Add(s);
}

void Stroke(ref MySpriteDrawFrame f, Vector2 c, Vector2 sz, Color color, float w)
{
    Bar(ref f, new Vector2(c.X, c.Y - sz.Y / 2), new Vector2(sz.X, w), color);
    Bar(ref f, new Vector2(c.X, c.Y + sz.Y / 2), new Vector2(sz.X, w), color);
    Bar(ref f, new Vector2(c.X - sz.X / 2, c.Y), new Vector2(w, sz.Y), color);
    Bar(ref f, new Vector2(c.X + sz.X / 2, c.Y), new Vector2(w, sz.Y), color);
}

void Bar(ref MySpriteDrawFrame f, Vector2 pos, Vector2 size, Color color)
{
    MySprite s = MySprite.CreateSprite("SquareSimple", pos, size);
    s.Color = color;
    f.Add(s);
}

void Fill(ref MySpriteDrawFrame f, Vector2 size, Color color)
{
    Bar(ref f, size / 2f, size, color);
}

void Txt(ref MySpriteDrawFrame f, string text, Vector2 pos, float size, Color color, TextAlignment align)
{
    MySprite s = MySprite.CreateText(text, "Monospace", color, size, align);
    s.Position = pos;
    f.Add(s);
}

float Scale(Vector2 z)
{
    return Math.Min(z.X, z.Y) / 512f;
}

string CurrentPageName()
{
    if (focus == 0) return PAGES[page];
    if (focus == 1) return hotbarPage == 0 ? "HOTBAR" : "HOTBAR 2";
    if (focus - 2 >= 0 && focus - 2 < exts.Count) return ExtPageName(exts[focus - 2]);
    return PAGES[page];
}

string FocusName()
{
    if (focus == 0) return "CENTER";
    if (focus == 1) return hotbarPage == 0 ? "HOTBAR" : "HOTBAR 2";
    return "WSS" + exts[focus - 2].Num.ToString();
}

string NextWave(int d)
{
    int n = 0;
    if (selectedWave == "A") n = 0; else if (selectedWave == "B") n = 1; else if (selectedWave == "C") n = 2;
    n = Wrap(n + d, 3);
    return n == 0 ? "A" : n == 1 ? "B" : "C";
}

string BayName(int i) { return "BAY" + (i + 1).ToString("00"); }
int Wrap(int v, int n) { if (n <= 0) return 0; while (v < 0) v += n; while (v >= n) v -= n; return v; }
bool Has(string s, string tag) { return (s == null ? "" : s).IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0; }
string Safe(string s) { return s == null ? "" : s; }
string F(double v) { return v.ToString("0.##"); }
string Km(double m) { if (m >= 1000) return (m / 1000.0).ToString("0.#") + " km"; return m.ToString("0") + " m"; }

int SlotIndex(string s)
{
    for (int i = 0; i < BAY_SLOTS.Length; i++) if (Safe(s).Equals(BAY_SLOTS[i], StringComparison.OrdinalIgnoreCase)) return i;
    return -1;
}

int NumberTag(string name, string prefix)
{
    string n = Safe(name).ToUpperInvariant();
    string p = prefix.ToUpperInvariant();
    int i = n.IndexOf(p, StringComparison.OrdinalIgnoreCase);
    if (i < 0) return -1;
    i += p.Length;
    StringBuilder d = new StringBuilder();
    while (i < n.Length && n[i] >= '0' && n[i] <= '9') { d.Append(n[i]); i++; }
    int r;
    if (d.Length > 0 && int.TryParse(d.ToString(), out r)) return r;
    return -1;
}

string FirstVal(string cd, string[] keys)
{
    for (int i = 0; i < keys.Length; i++)
    {
        string v = Val(cd, keys[i]);
        if (v.Length > 0) return v;
    }
    return "";
}

string Val(string cd, string key)
{
    int p = Safe(cd).IndexOf(key, StringComparison.OrdinalIgnoreCase);
    if (p < 0) return "";
    p += key.Length;
    int e = cd.IndexOf('\n', p);
    if (e < 0) e = cd.Length;
    return cd.Substring(p, e - p).Trim().Trim('\r');
}

bool TryVal(string cd, string key, out double v)
{
    v = 0;
    string s = Val(cd, key);
    if (s.Length == 0) return false;
    return double.TryParse(s, out v);
}

string ReplaceBlock(string cd, string begin, string end, string body)
{
    cd = Safe(cd);
    int b = cd.IndexOf(begin, StringComparison.OrdinalIgnoreCase);
    if (b < 0) return (cd.TrimEnd() + "\n\n" + body.TrimEnd() + "\n").TrimStart();
    int e = cd.IndexOf(end, b, StringComparison.OrdinalIgnoreCase);
    if (e < 0) return (cd.TrimEnd() + "\n\n" + body.TrimEnd() + "\n").TrimStart();
    e += end.Length;
    string pre = cd.Substring(0, b).TrimEnd();
    string post = cd.Substring(e).TrimStart();
    StringBuilder s = new StringBuilder();
    if (pre.Length > 0) s.AppendLine(pre).AppendLine();
    s.AppendLine(body.TrimEnd());
    if (post.Length > 0) s.AppendLine().Append(post);
    return s.ToString();
}

void EchoStatus()
{
    Echo("LDC1 WSS V009");
    Echo("State " + lastStatus);
    Echo("Focus " + FocusName() + " Page " + CurrentPageName());
    Echo("Bay " + BayName(selectedBay) + " Next " + BAY_SLOTS[bays[selectedBay].Slot]);
    Echo("Range " + Km(rangeMeters) + " Step " + Km(rangeSteps[rangeStepIndex]));
    Echo("Mark " + (marked ? "YES" : "NO") + " FSU " + fsuSeq.ToString());
    Echo("LCD " + exts.Count.ToString() + " Helm " + (helm != null ? "YES" : "NO") + " DCS " + (dcsPb != null ? "YES" : "NO"));
    Echo("Instr " + lastInstr.ToString() + " hi " + highInstr.ToString() + " " + highWhere);
}

void MarkInstr(string where)
{
    lastInstr = Runtime.CurrentInstructionCount;
    if (lastInstr > highInstr) { highInstr = lastInstr; highWhere = where; }
}
