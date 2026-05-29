const string SHIP_TAG = "[MB1]";
const string CONSOLE_TAG = "[WSO]";

List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
List<IMyTextSurfaceProvider> providers = new List<IMyTextSurfaceProvider>();
StringBuilder sb = new StringBuilder(2048);

int page = 0;
int activeProfile = 0;
int shownProfile = 0;
int pendingProfile = -1;
int stageTicks = 0;
int anim = 0;
bool aiActive = false;
bool safetyLocked = true;
string last = "BOOT";
string target = "NONE";

const int STAGE_TICKS = 30; 

string[] pages = new string[] { "PROFILE", "TACTICAL", "WEAPONS", "AMMO BOX QTY" };
string[] profiles = new string[] { "PRESS", "STALK", "STANDOFF", "HOLD", "WITHDRAW" };

string[] staticPages = new string[] { "CONTACTS", "WEAPON STATUS" };
string[] helpPages = new string[] { "HOTBAR", "SAFETY" };

class ExtDisplay
{
    public int Num;
    public string Name;
    public IMyTextSurface Surface;
    public int Page;
}

List<IMyTextPanel> lcdBlocks = new List<IMyTextPanel>();
List<ExtDisplay> extDisplays = new List<ExtDisplay>();

int focusSlot = 0; 
int helpPage = 0;

const int CONTACT_SOFT_TICKS = 12; 
int contactPick = 0;
int contactSoft = -1;
int contactSoftTicks = 0;
int contactManual = -1;
int wpnPick = 0;
int magPickField = 0;
int magStepIndex = 1;
int[] magSteps = new int[] { 1, 10, 100 };
const string MAGCFG_BEGIN = "# WSO_MAGCFG_BEGIN";
const string MAGCFG_END = "# WSO_MAGCFG_END";
const string TACTCFG_BEGIN = "# WSO_TACTCFG_BEGIN";
const string TACTCFG_END = "# WSO_TACTCFG_END";
const string WSO_STATE_BEGIN = "# WSO_STATE_BEGIN";
const string WSO_STATE_END = "# WSO_STATE_END";
const int TACT_STAGE_TICKS = 12; // 2 seconds at Update10

string[] tactFields = new string[] { "FIRE", "TARGET", "TURRETS" };
string[] tactFireModes = new string[] { "SAFE", "ENEMIES", "NEUTRALS", "BOTH" };
string[] tactTargets = new string[] { "DEFAULT", "WEAPONS", "POWER", "THRUSTERS" };
string[] tactTurrets = new string[] { "ON", "OFF" };
int tactField = 0;
int tactFire = 0;
int tactTarget = 0;
int tactTurret = 0;
int pendingTactFire = -1;
int pendingTactTurret = -1;
int tactStageTicks = 0;
string tactState = "BOOT";
int tactCtrl = 0, tactSkipStation = 0, tactFail = 0;
string tactPb2 = "NO DATA";

class ContactDemo
{
    public string Id;
    public string Rel;
    public string ClassName;
    public string Arc;
    public string Move;
    public float RangeKm;
    public float X;
    public float Y;
    public int SizeClass; 
    public int ElevBand; 
    public bool WeaponTarget; 
}

class WpnModule
{
    public string Key;
    public Vector3D Pos;
    public int Count;
    public int Offline;
    public int Degraded;
    public string WeaponType;
    public double Ammo;
    public bool AmmoSeen;
    public string Reason;
}

class MagRow
{
    public string Wpn;
    public string AmmoSub;
    public string AmmoLabel;
    public string Loc;
    public int Box;
    public int Gun;
    public int Min;
    public int Max;
    public string State;
    public bool Live;
    public Vector3D Pos;
    public bool HasPos;
}

List<MagRow> magRows = new List<MagRow>();

List<WpnModule> wpnModules = new List<WpnModule>();
bool hullReady = false;
Vector3D hullMin = new Vector3D();
Vector3D hullMax = new Vector3D();
int hullBlockCount = 0;
List<IMyProgrammableBlock> pb2Blocks = new List<IMyProgrammableBlock>();
string pb2Status = "NO PB2";

List<ContactDemo> contacts = new List<ContactDemo>();


int shieldW = 118;
int shieldH = 147;

string[] SHIELD = new string[] {
"",
"",
"",
"55:8",
"52:14",
"48:22",
"44:30",
"41:36",
"38:42",
"34:50",
"30:58",
"26:29,63:29",
"23:29,66:29",
"19:29,70:29",
"16:28,74:29",
"12:29,77:29",
"8:30,80:30",
"5:29,58:1,84:29",
"3:27,54:5,88:27",
"3:24,50:9,91:24",
"3:20,47:12,95:20",
"3:16,43:16,99:16",
"3:13,40:19,102:13",
"3:9,36:23,106:9",
"3:7,32:27,108:7",
"3:7,29:30,108:7",
"3:7,25:34,108:7",
"3:7,22:37,108:7",
"3:7,18:41,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:7,17:42,108:7",
"3:8,17:42,107:8",
"3:8,17:42,107:8",
"3:8,17:42,107:8",
"3:8,18:41,107:8",
"3:8,18:41,107:8",
"3:8,18:41,107:8",
"4:8,19:40,106:8",
"4:8,19:40,106:8",
"4:9,20:39,105:9",
"5:8,20:39,105:8",
"5:8,21:38,105:8",
"5:9,21:38,104:9",
"6:8,22:37,104:8",
"6:9,22:37,103:9",
"7:9,23:36,102:9",
"7:9,24:35,102:9",
"8:9,25:34,101:9",
"8:9,25:34,101:9",
"9:9,26:33,100:9",
"10:9,27:32,99:9",
"10:10,28:31,98:10",
"11:10,29:30,97:10",
"12:9,30:29,97:9",
"13:9,31:28,96:9",
"13:10,32:27,95:10",
"14:10,33:26,94:10",
"15:10,33:26,93:10",
"16:10,34:25,92:10",
"16:11,35:24,91:11",
"17:11,36:23,90:11",
"18:10,37:22,90:10",
"19:10,38:21,89:10",
"20:10,39:20,88:10",
"21:10,40:19,87:10",
"22:10,41:18,86:10",
"23:10,42:17,85:10",
"23:11,43:16,84:11",
"24:12,44:15,82:12",
"25:12,46:13,81:12",
"26:12,47:12,80:12",
"27:12,48:11,79:12",
"28:12,49:10,78:12",
"30:11,50:9,77:11",
"31:11,52:7,76:11",
"32:11,53:6,75:11",
"33:11,54:5,74:11",
"34:11,55:4,73:11",
"35:12,57:2,71:12",
"36:12,58:1,70:12",
"37:12,69:12",
"38:12,68:12",
"40:12,66:12",
"40:13,65:13",
"42:12,64:12",
"43:12,63:12",
"44:13,61:13",
"45:13,60:13",
"47:24",
"48:22",
"49:20",
"51:16",
"52:14",
"53:12",
"55:8",
"56:6",
"58:2",
"",
"",
""
};

int[] binocW = new int[] { 132 };
int[] binocH = new int[] { 100 };

string[] B0 = new string[] {
"",
"",
"43:6,84:6",
"41:10,81:10",
"39:14,79:14",
"38:16,78:16",
"37:18,77:19",
"36:20,76:20",
"36:21,76:21",
"35:22,75:22",
"34:23,63:6,75:23",
"34:24,61:11,75:23",
"34:24,59:14,74:24",
"33:66",
"33:66",
"32:68",
"32:68",
"32:68",
"31:70",
"31:70",
"28:76",
"27:78",
"26:81",
"24:84",
"24:85",
"23:87",
"22:88",
"21:90",
"20:92",
"20:92",
"20:93",
"19:94",
"18:96",
"18:96",
"17:98",
"17:98",
"16:100",
"16:100",
"15:102",
"15:103",
"14:104",
"14:104",
"13:50,69:50",
"12:50,70:50",
"12:48,72:48",
"12:48,72:49",
"11:48,73:48",
"10:48,74:48",
"10:48,74:48",
"9:49,74:49",
"9:17,35:23,74:24,107:17",
"8:15,37:21,74:21,109:15",
"8:12,40:18,74:18,112:12",
"7:12,41:18,73:18,113:12",
"7:11,43:17,72:18,115:11",
"6:10,44:16,72:16,116:10",
"6:9,45:17,70:17,117:10",
"5:9,46:17,69:18,118:9",
"4:10,46:40,118:10",
"4:9,47:38,119:9",
"4:8,48:36,120:8",
"4:8,48:36,120:9",
"3:9,49:35,121:8",
"3:8,49:34,121:8",
"3:8,49:9,60:13,75:8,121:9",
"2:9,50:8,62:8,74:9,122:8",
"2:8,50:8,74:8,122:8",
"2:8,50:8,74:8,122:8",
"2:8,50:8,74:8,122:8",
"2:8,50:8,74:8,122:8",
"2:8,50:8,74:8,122:8",
"2:8,50:8,74:8,122:8",
"2:8,50:8,74:8,122:8",
"2:8,50:8,74:8,122:8",
"2:8,50:8,74:9,122:8",
"3:8,50:8,75:8,122:8",
"3:8,49:8,75:8,121:8",
"3:9,49:8,75:9,121:8",
"4:8,48:9,76:8,120:9",
"4:8,48:8,76:8,120:8",
"4:9,47:9,76:9,119:9",
"4:10,46:10,76:10,118:10",
"5:9,46:9,77:9,118:9",
"6:9,45:10,78:9,117:10",
"6:10,44:10,78:10,116:10",
"7:10,43:11,79:10,115:11",
"8:11,41:12,80:11,113:12",
"8:12,40:12,80:12,112:12",
"9:13,38:13,81:13,110:13",
"10:15,36:14,82:15,108:14",
"11:38,83:38",
"12:36,84:36",
"14:33,86:33",
"14:32,87:31",
"16:28,88:28",
"18:24,90:24",
"21:18,93:18",
"24:12,96:12",
"",
""
};

int withdrawW = 116;
int withdrawH = 120;

string[] WITHDRAW = new string[] {
"",
"",
"59:19",
"55:26",
"52:32",
"49:37",
"48:40",
"46:44",
"45:47",
"43:50",
"42:53",
"40:56",
"38:61",
"37:63",
"36:65",
"35:67",
"34:69",
"33:71",
"32:72",
"31:74",
"31:74",
"30:76",
"30:76",
"30:77",
"29:79",
"28:80",
"28:81",
"27:82",
"27:83",
"26:41,69:41",
"26:37,74:36",
"25:34,78:33",
"25:32,79:33",
"25:31,81:31",
"24:31,82:30",
"24:30,83:29",
"24:29,83:30",
"24:29,84:29",
"23:29,85:28",
"23:28,85:28",
"23:28,86:27",
"23:28,86:28",
"23:27,86:28",
"23:27,86:28",
"23:27,86:28",
"23:27,86:28",
"22:27,86:28",
"22:27,86:28",
"22:27,86:28",
"22:27,86:28",
"22:27,86:28",
"22:27,86:28",
"22:27,86:28",
"22:27,86:28",
"22:27,86:28",
"22:27,86:28",
"22:27,86:28",
"22:27,86:28",
"22:27,86:28",
"22:27,86:28",
"22:27,86:28",
"22:27,86:28",
"22:27,86:28",
"22:27,86:28",
"2:68,86:28",
"3:66,86:28",
"3:66,86:28",
"4:64,86:28",
"5:62,86:28",
"5:62,86:28",
"6:60,86:28",
"7:59,86:28",
"8:57,86:28",
"8:57,86:28",
"9:55,86:28",
"10:53,86:28",
"10:53,86:28",
"11:51,86:28",
"11:50,86:28",
"11:50,86:28",
"12:48,86:28",
"12:47,86:28",
"13:46,86:28",
"14:44,86:28",
"14:44,86:28",
"15:42,86:28",
"16:40,86:28",
"16:40,86:28",
"17:38,86:28",
"18:36,86:28",
"19:34,86:28",
"19:33,86:28",
"20:32,86:28",
"21:30,86:28",
"21:30,86:28",
"22:28,86:28",
"23:26,86:28",
"23:26,86:28",
"24:25,86:28",
"25:23,86:28",
"25:23,86:28",
"26:21,86:28",
"26:20,86:28",
"27:19,86:28",
"28:17,86:28",
"28:17,86:28",
"29:15,86:28",
"30:13,86:28",
"30:12,86:28",
"31:10,86:28",
"31:10,86:28",
"32:8,86:28",
"32:7,86:28",
"33:6,86:28",
"34:4,86:28",
"34:4,86:28",
"35:2,86:28",
"86:28",
"",
""
};

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    ReadMagConfig();
    ReadWsoState();
    Rescan();
    WriteMagConfig();
    WriteTactConfig();
    DrawAll();
}

public void Save() { CommitPendingTactical(); SaveWsoState(); WriteMagConfig(); WriteTactConfig(); }

public void Main(string argument, UpdateType updateSource)
{
    string a = argument == null ? "" : argument.Trim().ToUpperInvariant();

    if (a.Length == 0 && (updateSource & UpdateType.Update10) != 0)
    {
        TickStage();
        TickTacticalStage();
        TickContactSelection();
        anim++;
        if ((anim % 6) == 0) ReadPb2Packet();
        DrawAll();
        return;
    }

    if (a == "FOCUS")
    {
        StepFocus();
        last = a;
    }
    else if (a == "PAGE NEXT")
    {
        StepFocusedPage(1);
        last = a;
    }
    else if (a == "PAGE PREV")
    {
        StepFocusedPage(-1);
        last = a;
    }
    else if (a == "NEXT")
    {
        if (focusSlot == 0 && pages[page] == "PROFILE") StageProfile(1);
        else if (focusSlot == 0 && pages[page] == "TACTICAL") StepTacticalField(1);
        else if (focusSlot == 0 && pages[page] == "AMMO BOX QTY") StepMagField(1);
        else if (FocusedContacts()) StepContactPick(1);
        else if (FocusedWeaponStatus()) StepWeaponPick(1);
        last = a;
    }
    else if (a == "PREV")
    {
        if (focusSlot == 0 && pages[page] == "PROFILE") StageProfile(-1);
        else if (focusSlot == 0 && pages[page] == "TACTICAL") StepTacticalField(-1);
        else if (focusSlot == 0 && pages[page] == "AMMO BOX QTY") StepMagField(-1);
        else if (FocusedContacts()) StepContactPick(-1);
        else if (FocusedWeaponStatus()) StepWeaponPick(-1);
        last = a;
    }
    else if (a == "INC")
    {
        if (focusSlot == 0 && pages[page] == "PROFILE") StageProfile(1);
        else if (focusSlot == 0 && pages[page] == "TACTICAL") AdjustTactical(1);
        else if (focusSlot == 0 && pages[page] == "AMMO BOX QTY") AdjustMagValue(1);
        else if (FocusedContacts()) ConfirmContactPick();
        last = a;
    }
    else if (a == "DEC")
    {
        if (focusSlot == 0 && pages[page] == "PROFILE") StageProfile(-1);
        else if (focusSlot == 0 && pages[page] == "TACTICAL") AdjustTactical(-1);
        else if (focusSlot == 0 && pages[page] == "AMMO BOX QTY") AdjustMagValue(-1);
        else if (FocusedContacts()) ReleaseContactPick();
        last = a;
    }
    else if (a == "STEP")
    {
        if (focusSlot == 0 && pages[page] == "AMMO BOX QTY") StepMagAmount();
        last = "STEP " + MagStep().ToString();
    }
    else if (HandleTacticalHotbar(a))
    {
        last = a;
    }
    else if (a == "SAFE")
    {
        safetyLocked = !safetyLocked;
        page = 0;

        if (safetyLocked)
        {
            aiActive = false;
            pendingProfile = -1;
            stageTicks = 0;
        }
        else
        {
            pendingProfile = shownProfile;
            stageTicks = STAGE_TICKS;
            aiActive = false;
        }

        last = safetyLocked ? "SAFE LOCKED" : "SAFE UNLOCK";
        SaveWsoState();
        WriteTactConfig();
    }
    else if (a == "RESCAN" || a == "SCAN")
    {
        Rescan();
        last = a;
    }
    else if (a.Length > 0)
    {
        last = a;
    }

    anim++;
    DrawAll();
}

bool FocusedContacts()
{
    if (focusSlot <= 0 || focusSlot > extDisplays.Count) return false;
    return extDisplays[focusSlot - 1].Page == 0;
}

bool FocusedWeaponStatus()
{
    if (focusSlot <= 0 || focusSlot > extDisplays.Count) return false;
    return extDisplays[focusSlot - 1].Page == 1;
}

void StepWeaponPick(int d)
{
    if (wpnModules.Count == 0) return;
    wpnPick += d;
    if (wpnPick >= wpnModules.Count) wpnPick = 0;
    if (wpnPick < 0) wpnPick = wpnModules.Count - 1;
}

void StepContactPick(int d)
{
    if (contacts.Count == 0) return;
    contactPick += d;
    if (contactPick >= contacts.Count) contactPick = 0;
    if (contactPick < 0) contactPick = contacts.Count - 1;
    contactSoft = -1;
    contactSoftTicks = CONTACT_SOFT_TICKS;
}

void TickContactSelection()
{
    if (!FocusedContacts()) return;
    if (contacts.Count == 0) return;

    if (contactSoft == contactPick) return;
    if (contactSoftTicks <= 0) contactSoftTicks = CONTACT_SOFT_TICKS;

    contactSoftTicks--;
    if (contactSoftTicks <= 0)
    {
        contactSoft = contactPick;
        contactSoftTicks = 0;
    }
}

void ConfirmContactPick()
{
    if (contacts.Count == 0) return;
    contactManual = contactPick;
    contactSoft = contactPick;
    contactSoftTicks = 0;
}

void ReleaseContactPick()
{
    contactManual = -1;
}

void StepFocus()
{
    int max = extDisplays.Count + 1; 
    focusSlot++;
    if (focusSlot > max) focusSlot = 0;
}

void StepFocusedPage(int d)
{
    if (focusSlot == 0)
    {
        page += d;
        if (page >= pages.Length) page = 0;
        if (page < 0) page = pages.Length - 1;
        return;
    }

    if (focusSlot <= extDisplays.Count)
    {
        ExtDisplay ex = extDisplays[focusSlot - 1];
        ex.Page += d;
        if (ex.Page >= staticPages.Length) ex.Page = 0;
        if (ex.Page < 0) ex.Page = staticPages.Length - 1;
        return;
    }

    helpPage += d;
    if (helpPage >= helpPages.Length) helpPage = 0;
    if (helpPage < 0) helpPage = helpPages.Length - 1;
}

string FocusName()
{
    if (focusSlot == 0) return "COMMAND";
    if (focusSlot <= extDisplays.Count) return "[" + "WSO" + extDisplays[focusSlot - 1].Num.ToString() + "]";
    return "HELP";
}

void StageProfile(int d)
{
    shownProfile += d;
    if (shownProfile >= profiles.Length) shownProfile = 0;
    if (shownProfile < 0) shownProfile = profiles.Length - 1;

    if (safetyLocked)
    {
        pendingProfile = -1;
        stageTicks = 0;
        aiActive = false;
        SaveWsoState();
        WriteTactConfig();
        return;
    }

    pendingProfile = shownProfile;
    stageTicks = STAGE_TICKS;
    aiActive = false;
    SaveWsoState();
    WriteTactConfig();
}

void TickStage()
{
    if (safetyLocked)
    {
        aiActive = false;
        pendingProfile = -1;
        stageTicks = 0;
        return;
    }

    if (pendingProfile < 0) return;

    stageTicks--;
    if (stageTicks <= 0)
    {
        activeProfile = pendingProfile;
        shownProfile = activeProfile;
        pendingProfile = -1;
        stageTicks = 0;
        aiActive = true;
        SaveWsoState();
        WriteTactConfig();
    }
}

bool IsArming()
{
    return !safetyLocked && pendingProfile >= 0;
}

bool IsProfileLive()
{
    return !safetyLocked && aiActive && pendingProfile < 0 && shownProfile == activeProfile && focusSlot == 0 && page == 0;
}

void Rescan()
{
    blocks.Clear();
    providers.Clear();
    lcdBlocks.Clear();
    extDisplays.Clear();
    target = "NONE";

    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks, IsConsole);

    for (int i = 0; i < blocks.Count; i++)
    {
        IMyTextSurfaceProvider p = blocks[i] as IMyTextSurfaceProvider;
        if (p != null && p.SurfaceCount > 0)
        {
            providers.Add(p);
            if (target == "NONE") target = blocks[i].CustomName;
        }
    }

    if (providers.Count == 0)
    {
        IMyTextSurfaceProvider p = Me as IMyTextSurfaceProvider;
        if (p != null && p.SurfaceCount > 0)
        {
            providers.Add(p);
            target = Me.CustomName + " PB";
        }
    }

    GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(lcdBlocks);
    for (int i = 0; i < lcdBlocks.Count; i++)
    {
        int n = WsoExternalNumber(lcdBlocks[i].CustomName);
        if (n > 0)
        {
            ExtDisplay ex = new ExtDisplay();
            ex.Num = n;
            ex.Name = lcdBlocks[i].CustomName;
            ex.Surface = lcdBlocks[i] as IMyTextSurface;
            ex.Page = 0;
            extDisplays.Add(ex);
        }
    }

    SortExternalDisplays();
    ReadPb2Packet();

    int max = extDisplays.Count + 1;
    if (focusSlot > max) focusSlot = 0;
}

int WsoExternalNumber(string name)
{
    if (name == null) return -1;
    int p = name.IndexOf("[WSO", StringComparison.OrdinalIgnoreCase);
    while (p >= 0)
    {
        int i = p + 4;
        int n = 0;
        bool any = false;
        while (i < name.Length && name[i] >= '0' && name[i] <= '9')
        {
            any = true;
            n = n * 10 + (int)(name[i] - '0');
            i++;
        }
        if (any && i < name.Length && name[i] == ']') return n;
        p = name.IndexOf("[WSO", p + 1, StringComparison.OrdinalIgnoreCase);
    }
    return -1;
}

void SortExternalDisplays()
{
    for (int i = 0; i < extDisplays.Count - 1; i++)
    {
        for (int j = i + 1; j < extDisplays.Count; j++)
        {
            if (extDisplays[j].Num < extDisplays[i].Num)
            {
                ExtDisplay t = extDisplays[i];
                extDisplays[i] = extDisplays[j];
                extDisplays[j] = t;
            }
        }
    }
}

bool IsConsole(IMyTerminalBlock b)
{
    if (b == null) return false;
    string n = b.CustomName;
    return n.IndexOf(SHIP_TAG, StringComparison.OrdinalIgnoreCase) >= 0 &&
           n.IndexOf(CONSOLE_TAG, StringComparison.OrdinalIgnoreCase) >= 0;
}

void DrawAll()
{
    Echo("WSO PB1 V11_68_AI_AUTH");
    Echo("Page " + pages[page] + " | Focus " + FocusName());
    Echo("PB2 " + pb2Status + " | WPN " + wpnModules.Count.ToString() + " | CON " + contacts.Count.ToString());
    Echo(safetyLocked ? "AI INACTIVE" : ("Active " + profiles[activeProfile]));

    for (int i = 0; i < providers.Count; i++)
    {
        IMyTextSurfaceProvider p = providers[i];
        int c = p.SurfaceCount;
        if (c > 0) DrawSurface0(p.GetSurface(0));          
        if (c > 1) DrawPageSelectCard(p.GetSurface(1));    
        if (c > 2) DrawProfileNameCard(p.GetSurface(2));   
        if (c > 3) DrawTinyStatusSurface(p.GetSurface(3)); 
        if (c > 4) DrawHelpSurface(p.GetSurface(4));        
    }

    DrawExternalDisplays();
}

void TextSurface(IMyTextSurface s, string t, float fs, TextAlignment a)
{
    s.ContentType = ContentType.TEXT_AND_IMAGE;
    s.Script = "";
    s.FontSize = fs;
    s.Alignment = a;
    s.WriteText(t);
}

void ConfigureSpriteSurface(IMyTextSurface s)
{
    s.ContentType = ContentType.SCRIPT;
    s.Script = "";
    s.ScriptBackgroundColor = Bg();
}

void BlankSurface(IMyTextSurface s)
{
    s.ContentType = ContentType.TEXT_AND_IMAGE;
    s.Script = "";
    s.FontSize = 0.7f;
    s.Alignment = TextAlignment.CENTER;
    s.WriteText("");
}

string Support()
{
    sb.Clear();
    sb.AppendLine("WSO PROFILE");
    sb.AppendLine(profiles[shownProfile]);
    sb.AppendLine();
    if (safetyLocked) sb.AppendLine("AI INACTIVE");
    else if (IsArming())
    {
        sb.AppendLine("STAGING");
        sb.AppendLine(stageTicks.ToString() + "/" + STAGE_TICKS.ToString());
    }
    else sb.AppendLine("ACTIVE");
    return sb.ToString();
}

string Help()
{
    sb.Clear();
    sb.AppendLine("HOTBAR");
    sb.AppendLine("FOCUS target");
    sb.AppendLine("PAGE focused page");
    sb.AppendLine("PREV/NEXT profile");
    sb.AppendLine("SAFE lock");
    return sb.ToString();
}

void DrawSurface0(IMyTextSurface s)
{
    ConfigureSpriteSurface(s);

    Vector2 size = s.TextureSize;
    Vector2 c = size * 0.5f;
    float sc = Math.Min(size.X, size.Y) / 512f;

    using (MySpriteDrawFrame f = s.DrawFrame())
    {
        DrawBg(ref f, size, sc);

        if (focusSlot != 0)
        {
            DrawFocusCard(ref f, c, size, sc);
            return;
        }

        if (pages[page] == "PROFILE")
        {
            BadgeButton(ref f, c, sc, IsProfileLive());
            DrawProfileIcon(ref f, c, sc, shownProfile);
            DrawProfileCarrots(ref f, c, sc);

            if (IsArming())
                Text(ref f, "ARMING", c + new Vector2(0, 202f * sc), TextCol(), 0.45f * sc, TextAlignment.CENTER);
            else if (!safetyLocked && aiActive)
                Text(ref f, "ACTIVE", c + new Vector2(0, 202f * sc), TextCol(), 0.45f * sc, TextAlignment.CENTER);
            else if (safetyLocked)
                DrawAIInactiveOverlay(ref f, c, size, sc);
        }
        else if (pages[page] == "AMMO BOX QTY")
        {
            DrawAmmoBoxQtyPage(ref f, size, c, sc);
        }
        else if (pages[page] == "TACTICAL")
        {
            DrawTacticalPage(ref f, size, c, sc);
        }
        else
        {
            Text(ref f, pages[page], c + new Vector2(0, -20f * sc), Cyan(), 1.0f * sc, TextAlignment.CENTER);
            Text(ref f, "PLACEHOLDER", c + new Vector2(0, 58f * sc), TextCol(), 0.55f * sc, TextAlignment.CENTER);
        }
    }
}

void DrawFocusCard(ref MySpriteDrawFrame f, Vector2 c, Vector2 size, float sc)
{
    Vector2 card = new Vector2(380, 225) * sc;
    BeveledFill(ref f, c + new Vector2(5, 7) * sc, card, 16f * sc, Shadow());
    BeveledFill(ref f, c, card, 16f * sc, new Color(5, 24, 34, 232));
    BeveledOutline(ref f, c, card, 16f * sc, Cyan(), 1.7f * sc);
    BeveledOutline(ref f, c, card - new Vector2(22, 22) * sc, 12f * sc, new Color(220, 252, 255, 95), 0.8f * sc);

    Text(ref f, "FOCUS", c + new Vector2(0, -78f * sc), Pale(), 0.95f * sc, TextAlignment.CENTER);
    Text(ref f, FocusName(), c + new Vector2(0, -6f * sc), Cyan(), 0.82f * sc, TextAlignment.CENTER);
    Text(ref f, "COMMAND OFFLINE", c + new Vector2(0, 55f * sc), new Color(220, 252, 255, 205), 0.46f * sc, TextAlignment.CENTER);

    string state = safetyLocked ? "AI INACTIVE / SAFE LOCKED" : (IsArming() ? "ARMING" : "AUTHORITY READY");
    Color stateCol = safetyLocked ? WarnOrange() : Cyan();
    Text(ref f, state, c + new Vector2(0, 98f * sc), stateCol, 0.38f * sc, TextAlignment.CENTER);
}

void DrawProfileIcon(ref MySpriteDrawFrame f, Vector2 c, float sc, int p)
{
    if (p == 0) DrawPressBlade(ref f, c, sc);
    else if (p == 1) DrawBinocularTrace(ref f, c, sc);
    else if (p == 2) DrawCrosshair(ref f, c, sc);
    else if (p == 3) DrawWhiteShield(ref f, c, sc);
    else DrawWithdrawTrace(ref f, c, sc);
}

void DrawProfileCarrots(ref MySpriteDrawFrame f, Vector2 c, float sc)
{
    Color col = new Color(220, 252, 255, 215);
    float x = 196f * sc;
    float h = 42f * sc;
    float w = 3.0f * sc;

    Line(ref f, c + new Vector2(-x + h, -h), c + new Vector2(-x, 0), w, col);
    Line(ref f, c + new Vector2(-x, 0), c + new Vector2(-x + h, h), w, col);

    Line(ref f, c + new Vector2(x - h, -h), c + new Vector2(x, 0), w, col);
    Line(ref f, c + new Vector2(x, 0), c + new Vector2(x - h, h), w, col);
}

void DrawAIInactiveOverlay(ref MySpriteDrawFrame f, Vector2 c, Vector2 size, float sc)
{
    Rect(ref f, c, size, new Color(0, 0, 0, 92), true, 1f, 0f);

    Vector2 card = new Vector2(355, 136) * sc;
    Vector2 cc = c + new Vector2(0, 2f) * sc;

    Color orange = new Color(255, 146, 48, 225);
    Color orangeDim = new Color(140, 72, 25, 118);
    Color glass = new Color(8, 26, 34, 205);
    Color glass2 = new Color(16, 44, 54, 135);

    BeveledFill(ref f, cc + new Vector2(5, 7) * sc, card, 14f * sc, new Color(0, 0, 0, 150));
    BeveledFill(ref f, cc, card, 14f * sc, glass);
    BeveledFill(ref f, cc, card - new Vector2(14, 14) * sc, 10f * sc, glass2);

    BeveledOutline(ref f, cc, card, 14f * sc, orange, 1.65f * sc);
    BeveledOutline(ref f, cc, card - new Vector2(18, 18) * sc, 10f * sc, new Color(255, 188, 92, 145), 0.85f * sc);
    Line(ref f, cc + new Vector2(-card.X * 0.38f, -card.Y * 0.33f), cc + new Vector2(-card.X * 0.20f, -card.Y * 0.33f), 1.4f * sc, orangeDim);
    Line(ref f, cc + new Vector2(card.X * 0.20f, -card.Y * 0.33f), cc + new Vector2(card.X * 0.38f, -card.Y * 0.33f), 1.4f * sc, orangeDim);

    Text(ref f, "AI", cc + new Vector2(0, -34f * sc), Pale(), 1.28f * sc, TextAlignment.CENTER);
    Text(ref f, "INACTIVE", cc + new Vector2(0, 26f * sc), orange, 0.64f * sc, TextAlignment.CENTER);
}

void DrawPageSelectCard(IMyTextSurface s)
{
    ConfigureSpriteSurface(s);

    Vector2 size = s.TextureSize;
    Vector2 c = size * 0.5f;
    float sc = Math.Min(size.X, size.Y) / 512f;

    using (MySpriteDrawFrame f = s.DrawFrame())
    {
        DrawBg(ref f, size, sc);
        Vector2 safeC = c + new Vector2(0, -26f * sc);
        Vector2 card = new Vector2(size.X * 0.94f, size.Y * 0.72f);
        BeveledFill(ref f, safeC + new Vector2(3, 5) * sc, card, 10f * sc, Shadow());
        BeveledFill(ref f, safeC, card, 10f * sc, Panel());
        BeveledOutline(ref f, safeC, card, 10f * sc, Cyan(), 1.45f * sc);

        Vector2 inner = new Vector2(card.X * 0.90f, card.Y * 0.72f);
        BeveledOutline(ref f, safeC + new Vector2(0, 2f * sc), inner, 8f * sc, new Color(82, 205, 245, 115), 0.8f * sc);
        Text(ref f, "PAGE", safeC + new Vector2(0, -card.Y * 0.33f), new Color(220, 252, 255, 190), 0.52f * sc, TextAlignment.CENTER);
        Text(ref f, pages[page], safeC + new Vector2(0, -card.Y * 0.17f), Pale(), 1.05f * sc, TextAlignment.CENTER);
        float y0 = safeC.Y - card.Y * 0.01f;
        float gap = 30f * sc;

        for (int i = 0; i < pages.Length; i++)
        {
            bool sel = i == page;
            Color col = sel ? Pale() : new Color(82, 205, 245, 190);
            float fs = sel ? 0.55f * sc : 0.45f * sc;

            string left = sel ? "> " : "  ";
            Text(ref f, left + pages[i], new Vector2(safeC.X, y0 + i * gap), col, fs, TextAlignment.CENTER);
        }
        float ax = card.X * 0.42f;
        float ay = card.Y * 0.39f;
        Line(ref f, safeC + new Vector2(-ax + 20f * sc, -ay), safeC + new Vector2(-ax, -ay + 20f * sc), 1.1f * sc, new Color(220, 252, 255, 150));
        Line(ref f, safeC + new Vector2(-ax, -ay + 20f * sc), safeC + new Vector2(-ax + 20f * sc, -ay + 40f * sc), 1.1f * sc, new Color(220, 252, 255, 150));
        Line(ref f, safeC + new Vector2(ax - 20f * sc, -ay), safeC + new Vector2(ax, -ay + 20f * sc), 1.1f * sc, new Color(220, 252, 255, 150));
        Line(ref f, safeC + new Vector2(ax, -ay + 20f * sc), safeC + new Vector2(ax - 20f * sc, -ay + 40f * sc), 1.1f * sc, new Color(220, 252, 255, 150));
    }
}

void DrawProfileNameCard(IMyTextSurface s)
{
    ConfigureSpriteSurface(s);

    Vector2 size = s.TextureSize;
    Vector2 c = size * 0.5f;
    float sc = Math.Min(size.X, size.Y) / 512f;

    using (MySpriteDrawFrame f = s.DrawFrame())
    {
        DrawBg(ref f, size, sc);
        Vector2 safeC = c + new Vector2(0, -22f * sc);

        Vector2 card = new Vector2(size.X * 0.94f, size.Y * 0.72f);
        BeveledFill(ref f, safeC + new Vector2(3, 5) * sc, card, 10f * sc, Shadow());
        BeveledFill(ref f, safeC, card, 10f * sc, Panel());
        BeveledOutline(ref f, safeC, card, 10f * sc, Cyan(), 1.45f * sc);

        Vector2 nameBox = new Vector2(card.X * 0.88f, card.Y * 0.62f);
        BeveledOutline(ref f, safeC, nameBox, 7f * sc, new Color(82, 205, 245, 110), 0.75f * sc);

        if (safetyLocked)
        {
            Text(ref f, "AI", safeC + new Vector2(0, -48f * sc), Pale(), 2.05f * sc, TextAlignment.CENTER);
            Text(ref f, "INACTIVE", safeC + new Vector2(0, 32f * sc), WarnOrange(), 1.38f * sc, TextAlignment.CENTER);
            return;
        }

        string name = profiles[shownProfile];
        string sub = IsArming() ? "ARMING" : "ACTIVE";

        float scale = 1.6f * sc;
        if (name == "PRESS") scale = 2.05f * sc;
        else if (name == "STALK") scale = 2.05f * sc;
        else if (name == "HOLD") scale = 2.25f * sc;
        else if (name == "STANDOFF") scale = 1.52f * sc;
        else if (name == "WITHDRAW") scale = 1.46f * sc;

        Text(ref f, name, safeC + new Vector2(0, -14f * sc), Pale(), scale, TextAlignment.CENTER);
        Text(ref f, sub, safeC + new Vector2(0, card.Y * 0.32f), IsArming() ? WarnOrange() : Cyan(), 0.50f * sc, TextAlignment.CENTER);
    }
}

void DrawExternalDisplays()
{
    for (int i = 0; i < extDisplays.Count; i++)
    {
        DrawStaticPage(extDisplays[i].Surface, extDisplays[i].Page, "[" + "WSO" + extDisplays[i].Num.ToString() + "]");
    }
}

void DrawTinyStatusSurface(IMyTextSurface s)
{
    ConfigureSpriteSurface(s);

    Vector2 size = s.TextureSize;
    Vector2 c = size * 0.5f;
    float sc = Math.Min(size.X, size.Y) / 512f;

    using (MySpriteDrawFrame f = s.DrawFrame())
    {
        DrawBg(ref f, size, sc);

        Color col = safetyLocked ? WarnOrange() : Cyan();

        if (safetyLocked)
        {
            Rect(ref f, c + new Vector2(-18, 0) * sc, new Vector2(15, 64) * sc, col, true, 1f, 0f);
            Rect(ref f, c + new Vector2(18, 0) * sc, new Vector2(15, 64) * sc, col, true, 1f, 0f);
        }
        else
        {
            FillTri(ref f, c + new Vector2(-18, -38) * sc, c + new Vector2(-18, 38) * sc, c + new Vector2(42, 0) * sc, col, 1f);
        }

        Text(ref f, focusSlot == 0 ? "CMD" : "FOC", c + new Vector2(0, 72f * sc), TextCol(), 0.36f * sc, TextAlignment.CENTER);
    }
}

void DrawHelpSurface(IMyTextSurface s)
{
    ConfigureSpriteSurface(s);

    Vector2 size = s.TextureSize;
    Vector2 c = size * 0.5f;
    float sc = Math.Min(size.X, size.Y) / 512f;

    using (MySpriteDrawFrame f = s.DrawFrame())
    {
        DrawBg(ref f, size, sc);
        Vector2 card = new Vector2(size.X * 0.90f, size.Y * 0.76f);
        BeveledFill(ref f, c + new Vector2(3, 5) * sc, card, 10f * sc, Shadow());
        BeveledFill(ref f, c, card, 10f * sc, Panel());
        BeveledOutline(ref f, c, card, 10f * sc, focusSlot == extDisplays.Count + 1 ? Pale() : Cyan(), 1.25f * sc);

        string title = helpPages[helpPage];
        Text(ref f, "WSO HELP", c + new Vector2(0, -card.Y * 0.32f), Pale(), 0.58f * sc, TextAlignment.CENTER);
        Text(ref f, title, c + new Vector2(0, -card.Y * 0.13f), Cyan(), 0.78f * sc, TextAlignment.CENTER);

        if (helpPage == 0)
        {
            Text(ref f, "FOCUS: TARGET", c + new Vector2(0, 8f * sc), TextCol(), 0.42f * sc, TextAlignment.CENTER);
            Text(ref f, "PAGE: DISPLAY", c + new Vector2(0, 42f * sc), TextCol(), 0.42f * sc, TextAlignment.CENTER);
            Text(ref f, "SAFE: LOCK", c + new Vector2(0, 76f * sc), WarnOrange(), 0.42f * sc, TextAlignment.CENTER);
        }
        else
        {
            Text(ref f, "SAFE LOCKED", c + new Vector2(0, 8f * sc), WarnOrange(), 0.45f * sc, TextAlignment.CENTER);
            Text(ref f, "NO PROFILE COMMIT", c + new Vector2(0, 43f * sc), TextCol(), 0.38f * sc, TextAlignment.CENTER);
            Text(ref f, "PROFILE PREVIEW OK", c + new Vector2(0, 76f * sc), TextCol(), 0.38f * sc, TextAlignment.CENTER);
        }
    }
}

void DrawStaticPage(IMyTextSurface s, int p, string label)
{
    if (s == null) return;
    ConfigureSpriteSurface(s);

    Vector2 size = s.TextureSize;
    Vector2 c = size * 0.5f;
    float sc = Math.Min(size.X, size.Y) / 512f;

    using (MySpriteDrawFrame f = s.DrawFrame())
    {
        bool focused = false;
        if (focusSlot > 0 && focusSlot <= extDisplays.Count)
            focused = extDisplays[focusSlot - 1].Surface == s;

        int n = ExternalNumFromLabel(label);
        int hatchVariant = HatchVariant(n, p);

        if (p == 0)
        {
            Rect(ref f, c, size, Bg(), true, 1f, 0f);
            DrawContactCard(ref f, size, c, sc, label, focused, hatchVariant);
        }
        else
        {
            Rect(ref f, c, size, Bg(), true, 1f, 0f);
            DrawWeaponStatusCard(ref f, size, c, sc, label, focused, hatchVariant);
        }
    }
}

int ExternalNumFromLabel(string label)
{
    if (label == null) return 1;
    int n = 0;
    for (int i = 0; i < label.Length; i++)
    {
        char ch = label[i];
        if (ch >= '0' && ch <= '9') n = n * 10 + (int)(ch - '0');
    }
    return n <= 0 ? 1 : n;
}

int HatchVariant(int lcdNum, int page)
{
    return 11;
}


void ReadPb2Packet()
{
    pb2Blocks.Clear();
    GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>(pb2Blocks);
    IMyProgrammableBlock src = null;
    for (int i = 0; i < pb2Blocks.Count; i++)
    {
        IMyProgrammableBlock p = pb2Blocks[i];
        if (p != null && HasTag(p.CustomName, SHIP_TAG) && HasTag(p.CustomName, "[WSO-PB2]")) { src = p; break; }
    }
    if (src == null)
    {
        pb2Status = "NO [WSO-PB2]";
        return;
    }

    string data = src.CustomData;
    if (data == null || data.IndexOf("[WSO-PB2 V", StringComparison.OrdinalIgnoreCase) < 0)
    {
        pb2Status = "PB2 NO PACKET";
        return;
    }

    wpnModules.Clear();
    contacts.Clear();
    magRows.Clear();
    hullReady = false;
    hullBlockCount = 0;

    string[] lines = data.Split('\n');
    for (int i = 0; i < lines.Length; i++)
    {
        string line = lines[i].Trim();
        if (line.Length == 0) continue;
        string[] p = line.Split('|');
        if (p.Length == 0) continue;
        if (p[0] == "STATE")
        {
            pb2Status = p.Length > 1 ? p[1] : "OK";
        }
        else if (p[0] == "HULL" && p.Length >= 8)
        {
            double minx, miny, minz, maxx, maxy, maxz;
            if (D(p[1], out minx) && D(p[2], out miny) && D(p[3], out minz) && D(p[4], out maxx) && D(p[5], out maxy) && D(p[6], out maxz))
            {
                hullMin = new Vector3D(minx, miny, minz);
                hullMax = new Vector3D(maxx, maxy, maxz);
                int.TryParse(p[7], out hullBlockCount);
                hullReady = true;
            }
        }
        else if (p[0] == "WPN" && p.Length >= 11)
        {
            WpnModule m = new WpnModule();
            m.Key = p[1];
            string st = p[2];
            m.WeaponType = p[3];
            m.AmmoSeen = p[4] == "1";
            double ammo, x, y, z;
            D(p[5], out ammo); D(p[6], out x); D(p[7], out y); D(p[8], out z);
            m.Ammo = ammo;
            m.Pos = new Vector3D(x, y, z);
            m.Count = 1;
            m.Reason = p.Length > 11 ? p[11] : "";
            m.Offline = st == "OFFLINE" ? 1 : 0;
            m.Degraded = st == "DEG" ? 1 : 0;
            wpnModules.Add(m);
        }
        else if (p[0] == "MAGSTAT" && p.Length >= 4)
        {
            MagRow r = new MagRow();
            r.Wpn = p[1];
            r.AmmoSub = p[2];
            r.AmmoLabel = p[3];
            r.Box = ToInt(Field(p, "BOX"), 0);
            r.Gun = ToInt(Field(p, "GUN"), 0);
            r.Loc = Field(p, "LOC");
            double mx, my, mz;
            if (D(Field(p, "X"), out mx) && D(Field(p, "Y"), out my) && D(Field(p, "Z"), out mz))
            {
                r.Pos = new Vector3D(mx, my, mz);
                r.HasPos = true;
                if (r.Loc.Length == 0) r.Loc = LocFromPos(r.Pos);
            }
            r.State = Field(p, "STATE");
            r.Live = true;
            ApplyMagSaved(r);
            magRows.Add(r);
        }
        else if (p[0] == "TACTSTAT")
        {
            tactPb2 = p.Length > 1 ? p[1] : "OK";
            tactCtrl = ToInt(Field(p, "CTRL"), tactCtrl);
            tactSkipStation = ToInt(Field(p, "SKIPSTATIC"), tactSkipStation);
            tactFail = ToInt(Field(p, "FAIL"), tactFail);
        }
        else if (p[0] == "CON" && p.Length >= 12)
        {
            ContactDemo ct = new ContactDemo();
            ct.Id = p[1];
            ct.Rel = p[2];
            ct.ClassName = p[3];
            ct.WeaponTarget = p[4] == "1";
            double range, x, y;
            D(p[5], out range); D(p[6], out x); D(p[7], out y);
            ct.RangeKm = (float)range;
            ct.X = (float)x;
            ct.Y = (float)y;
            int.TryParse(p[8], out ct.SizeClass);
            int.TryParse(p[9], out ct.ElevBand);
            ct.Arc = p[10];
            ct.Move = p.Length > 13 ? p[13] : "";
            contacts.Add(ct);
        }
    }

    if (!hullReady && pb2Status != "ANCHOR LOST")
    {
        hullReady = true;
        hullMin = new Vector3D(-20, -8, -35);
        hullMax = new Vector3D(20, 8, 35);
    }
    // AMMO BOX QTY is intentionally magazine-only: do not synthesize rows from weapon packets.
    // Weapons without a tagged cargo magazine should not appear on this page.
    SortMagRows();
    if (magPickField >= MagFieldCount()) magPickField = Math.Max(0, MagFieldCount() - 1);
    WriteMagConfig();
    if (wpnPick >= wpnModules.Count) wpnPick = Math.Max(0, wpnModules.Count - 1);
    if (contactPick >= contacts.Count) contactPick = Math.Max(0, contacts.Count - 1);
    if (contactManual >= contacts.Count) contactManual = -1;
    if (contactSoft >= contacts.Count) contactSoft = -1;
}

string Field(string[] p, string key)
{
    for (int i = 1; i + 1 < p.Length; i++) if (p[i].Equals(key, StringComparison.OrdinalIgnoreCase)) return p[i + 1];
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

bool D(string s, out double v)
{
    return double.TryParse(s, out v);
}

bool HasTag(string name, string tag)
{
    if (name == null || tag == null) return false;
    return name.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0;
}

float Norm(double v, double mn, double mx)
{
    double d = mx - mn;
    if (Math.Abs(d) < 0.001) return 0.5f;
    float r = (float)((v - mn) / d);
    if (r < 0f) return 0f;
    if (r > 1f) return 1f;
    return r;
}

Color ModuleColor(WpnModule m)
{
    if (m.Offline > 0) return HostileRed();
    if (m.Degraded > 0) return WarnOrange();
    return Pale();
}

string ModuleState(WpnModule m)
{
    if (m == null) return "UNKNOWN";
    if (m.Offline > 0) return "OFFLINE";
    if (m.Degraded > 0)
    {
        if (m.Reason != null && m.Reason.Length > 0 && AmmoText(m) != m.Reason) return ShortText(m.Reason, 15);
        return "";
    }
    return "READY";
}

string AmmoText(WpnModule m)
{
    if (m == null || !m.AmmoSeen) return "AMMO --";
    if (m.Ammo <= 0.01) return "AMMO EMPTY";
    return "AMMO " + ((int)Math.Round(m.Ammo)).ToString();
}

Color AmmoColor(WpnModule m)
{
    if (m != null && m.AmmoSeen && m.Ammo <= 0.01) return HostileRed();
    return Pale();
}

string ShortText(string t, int max)
{
    if (t == null) return "";
    if (t.Length <= max) return t;
    return t.Substring(0, max);
}

int MagFieldCount()
{
    return magRows.Count * 2;
}

int MagStep()
{
    if (magStepIndex < 0) magStepIndex = 0;
    if (magStepIndex >= magSteps.Length) magStepIndex = magSteps.Length - 1;
    return magSteps[magStepIndex];
}

void StepMagAmount()
{
    magStepIndex++;
    if (magStepIndex >= magSteps.Length) magStepIndex = 0;
}

void StepMagField(int d)
{
    int count = MagFieldCount();
    if (count <= 0) return;
    magPickField += d;
    if (magPickField >= count) magPickField = 0;
    else if (magPickField < 0) magPickField = count - 1;
}

void AdjustMagValue(int dir)
{
    int count = MagFieldCount();
    if (count <= 0) return;
    if (magPickField < 0) magPickField = 0;
    if (magPickField >= count) magPickField = count - 1;
    int row = magPickField / 2;
    int field = magPickField % 2;
    MagRow r = magRows[row];
    int delta = dir * MagStep();
    if (field == 0)
    {
        r.Min += delta;
        if (r.Min < 0) r.Min = 0;
        if (r.Max < r.Min) r.Max = r.Min;
    }
    else
    {
        r.Max += delta;
        if (r.Max < 0) r.Max = 0;
        if (r.Max < r.Min) r.Min = r.Max;
    }
    WriteMagConfig();
}

void StepTacticalField(int d)
{
    tactField += d;
    if (tactField >= tactFields.Length) tactField = 0;
    if (tactField < 0) tactField = tactFields.Length - 1;
}


void CommitPendingTactical()
{
    if (pendingTactFire >= 0) tactFire = pendingTactFire;
    if (pendingTactTurret >= 0) tactTurret = pendingTactTurret;
    pendingTactFire = -1;
    pendingTactTurret = -1;
    tactStageTicks = 0;
}

bool HandleTacticalHotbar(string a)
{
    if (a == null) return false;
    if (a.StartsWith("TACT TARGET ", StringComparison.OrdinalIgnoreCase))
    {
        SetTacticalTargetCommand(a.Substring(12).Trim());
        return true;
    }
    if (a.StartsWith("TARGET ", StringComparison.OrdinalIgnoreCase))
    {
        SetTacticalTargetCommand(a.Substring(7).Trim());
        return true;
    }
    if (a.Equals("TACT TARGET", StringComparison.OrdinalIgnoreCase) || a.Equals("TARGET", StringComparison.OrdinalIgnoreCase))
    {
        StepTacticalTarget(1);
        return true;
    }
    return false;
}

void StepTacticalTarget(int d)
{
    tactTarget += d;
    if (tactTarget >= tactTargets.Length) tactTarget = 0;
    if (tactTarget < 0) tactTarget = tactTargets.Length - 1;
    tactState = "APPLIED";
    WriteTactConfig();
}

void SetTacticalTargetCommand(string v)
{
    if (v == null) return;
    v = v.Trim().ToUpperInvariant();
    if (v == "NEXT" || v == "INC" || v == "+") { StepTacticalTarget(1); return; }
    if (v == "PREV" || v == "PREVIOUS" || v == "DEC" || v == "-") { StepTacticalTarget(-1); return; }
    if (v == "PROP" || v == "PROPULSION" || v == "THRUSTER") v = "THRUSTERS";
    int idx = IndexOf(tactTargets, v, -1);
    if (idx >= 0)
    {
        tactTarget = idx;
        tactState = "APPLIED";
        WriteTactConfig();
    }
}

int FireRank(int f)
{
    if (f <= 0) return 0;
    if (f == 3) return 2;
    return 1;
}

void AdjustTactical(int d)
{
    if (tactField == 0)
    {
        int cur = pendingTactFire >= 0 ? pendingTactFire : tactFire;
        int n = cur + d;
        if (n >= tactFireModes.Length) n = 0;
        if (n < 0) n = tactFireModes.Length - 1;

        // Only crossing from actual SAFE into an armed fire mode gets a soft delay.
        // Skating through pending choices remains free; armed-to-armed changes are instant.
        bool armsFromSafe = tactFire == 0 && n != 0;
        if (armsFromSafe)
        {
            pendingTactFire = n;
            tactStageTicks = TACT_STAGE_TICKS;
            tactState = "PENDING";
        }
        else
        {
            tactFire = n;
            pendingTactFire = -1;
            tactStageTicks = 0;
            tactState = "APPLIED";
            WriteTactConfig();
        }
    }
    else if (tactField == 1)
    {
        tactTarget += d;
        if (tactTarget >= tactTargets.Length) tactTarget = 0;
        if (tactTarget < 0) tactTarget = tactTargets.Length - 1;
        tactState = "APPLIED";
        WriteTactConfig();
    }
    else
    {
        int cur = pendingTactTurret >= 0 ? pendingTactTurret : tactTurret;
        int n = cur + d;
        if (n >= tactTurrets.Length) n = 0;
        if (n < 0) n = tactTurrets.Length - 1;
        bool armingOn = tactTurrets[n] == "ON" && tactTurrets[tactTurret] == "OFF";
        if (armingOn)
        {
            pendingTactTurret = n;
            tactStageTicks = TACT_STAGE_TICKS;
            tactState = "PENDING";
        }
        else
        {
            tactTurret = n;
            pendingTactTurret = -1;
            tactStageTicks = 0;
            tactState = "APPLIED";
            WriteTactConfig();
        }
    }
}

void TickTacticalStage()
{
    if (pendingTactFire < 0 && pendingTactTurret < 0) return;
    tactStageTicks--;
    if (tactStageTicks > 0) return;
    if (pendingTactFire >= 0) tactFire = pendingTactFire;
    if (pendingTactTurret >= 0) tactTurret = pendingTactTurret;
    pendingTactFire = -1;
    pendingTactTurret = -1;
    tactStageTicks = 0;
    tactState = "APPLIED";
    WriteTactConfig();
}

string TactDisplayValue(int idx)
{
    if (idx == 0) return pendingTactFire >= 0 ? tactFireModes[pendingTactFire] : tactFireModes[tactFire];
    if (idx == 1) return tactTargets[tactTarget];
    return pendingTactTurret >= 0 ? tactTurrets[pendingTactTurret] : tactTurrets[tactTurret];
}


string WsoStateLine()
{
    string ai = aiActive ? "ON" : "OFF";
    string safe = safetyLocked ? "ON" : "OFF";
    return "WSOSTATE|FIRE|" + tactFireModes[tactFire] + "|TARGET|" + tactTargets[tactTarget] + "|TURRETS|" + tactTurrets[tactTurret] + "|AI|" + ai + "|SAFELOCK|" + safe + "|ACTIVEPROFILE|" + profiles[activeProfile] + "|SHOWNPROFILE|" + profiles[shownProfile];
}

bool ApplyWsoStateLine(string line)
{
    if (line == null) return false;
    line = line.Trim();
    if (!(line.StartsWith("WSOSTATE|", StringComparison.OrdinalIgnoreCase) || line.StartsWith("TACTCFG|", StringComparison.OrdinalIgnoreCase))) return false;
    string[] p = line.Split('|');
    int nf = IndexOf(tactFireModes, Field(p, "FIRE"), -1);
    int nt = IndexOf(tactTargets, Field(p, "TARGET"), -1);
    int nu = IndexOf(tactTurrets, Field(p, "TURRETS"), -1);
    if (nf >= 0) tactFire = nf;
    if (nt >= 0) tactTarget = nt;
    if (nu >= 0) tactTurret = nu;
    string ai = Field(p, "AI");
    if (ai.Length == 0) ai = Field(p, "AIAUTH");
    if (ai.Length > 0) aiActive = ai.Equals("ON", StringComparison.OrdinalIgnoreCase);
    string sl = Field(p, "SAFELOCK");
    if (sl.Length > 0) safetyLocked = sl.Equals("ON", StringComparison.OrdinalIgnoreCase) || sl.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || sl.Equals("1");
    else if (line.StartsWith("TACTCFG|", StringComparison.OrdinalIgnoreCase)) safetyLocked = !aiActive;
    int ap = IndexOf(profiles, Field(p, "ACTIVEPROFILE"), -1);
    if (ap < 0) ap = IndexOf(profiles, Field(p, "PROFILE"), -1);
    if (ap >= 0) activeProfile = ap;
    int sp = IndexOf(profiles, Field(p, "SHOWNPROFILE"), -1);
    if (sp >= 0) shownProfile = sp;
    else shownProfile = activeProfile;
    if (safetyLocked) aiActive = false;
    pendingProfile = -1;
    pendingTactFire = -1;
    pendingTactTurret = -1;
    stageTicks = 0;
    tactStageTicks = 0;
    tactState = "RESTORED";
    return true;
}

bool ReadWsoStateFromText(string text)
{
    string block = ExtractBlock(text, WSO_STATE_BEGIN, WSO_STATE_END);
    if (block.Length == 0) return false;
    string[] lines = Lines(block);
    for (int i = 0; i < lines.Length; i++) if (ApplyWsoStateLine(lines[i])) return true;
    return false;
}

void ReadWsoState()
{
    if (ReadWsoStateFromText(Storage)) return;
    if (ReadWsoStateFromText(Me.CustomData)) return;
    ReadTactConfig();
}

void SaveWsoState()
{
    StringBuilder b = new StringBuilder();
    b.AppendLine(WSO_STATE_BEGIN);
    b.AppendLine(WsoStateLine());
    b.AppendLine(WSO_STATE_END);
    string blk = b.ToString();
    Storage = ReplaceBlock(Storage == null ? "" : Storage, WSO_STATE_BEGIN, WSO_STATE_END, blk);
    Me.CustomData = ReplaceBlock(Me.CustomData, WSO_STATE_BEGIN, WSO_STATE_END, blk);
}

string[] Lines(string s)
{
    if (s == null) s = "";
    s = s.Replace("\r", "");
    return s.Split(new string[] { ((char)10).ToString() }, StringSplitOptions.None);
}

void ReadTactConfig()
{
    string block = ExtractBlock(Me.CustomData, TACTCFG_BEGIN, TACTCFG_END);
    if (block.Length == 0) return;
    string[] lines = Lines(block);
    for (int i = 0; i < lines.Length; i++)
    {
        string line = lines[i].Trim();
        if (ApplyWsoStateLine(line)) return;
    }
}

int IndexOf(string[] arr, string val, int fallback)
{
    if (val == null || val.Length == 0) return fallback;
    for (int i = 0; i < arr.Length; i++) if (arr[i].Equals(val, StringComparison.OrdinalIgnoreCase)) return i;
    return fallback;
}

void WriteTactConfig()
{
    StringBuilder b = new StringBuilder();
    b.AppendLine(TACTCFG_BEGIN);
    b.AppendLine("# WSO TACTICAL CONFIG - STAGE 3 AI AUTHORITY + FULL STATE PERSIST");
    string aiAuth = (!safetyLocked && aiActive) ? "ON" : "OFF";
    string prof = (!safetyLocked && aiActive) ? profiles[activeProfile] : "AI INACTIVE";
    b.AppendLine("TACTCFG|FIRE|" + tactFireModes[tactFire] + "|TARGET|" + tactTargets[tactTarget] + "|TURRETS|" + tactTurrets[tactTurret] + "|AIAUTH|" + aiAuth + "|PROFILE|" + prof + "|PERSIST|1|SEQ|" + anim.ToString());
    b.AppendLine(TACTCFG_END);
    Me.CustomData = ReplaceBlock(Me.CustomData, TACTCFG_BEGIN, TACTCFG_END, b.ToString());
    SaveWsoState();
}

void DrawTacticalPage(ref MySpriteDrawFrame f, Vector2 size, Vector2 c, float sc)
{
    float headerY = size.Y * 0.205f;
    Rect(ref f, new Vector2(size.X * 0.500f, headerY + 1.5f * sc), new Vector2(size.X * 0.780f, 24f * sc), new Color(2, 13, 20, 160), true, 1f, 0f);
    Text(ref f, "TACTICAL", new Vector2(size.X * 0.105f, headerY), Pale(), 0.50f * sc, TextAlignment.LEFT);
    string st = (pendingTactFire >= 0 || pendingTactTurret >= 0) ? ("PENDING " + (tactStageTicks / 6.0).ToString("0.0") + "s") : tactState;
    Text(ref f, st, new Vector2(size.X * 0.895f, headerY), (st.StartsWith("PENDING") ? WarnOrange() : Pale()), 0.36f * sc, TextAlignment.RIGHT);

    Vector2 cardS = new Vector2(size.X * 0.78f, size.Y * 0.105f);
    float top = size.Y * 0.335f;
    for (int i = 0; i < tactFields.Length; i++)
    {
        Vector2 rc = new Vector2(size.X * 0.5f, top + i * size.Y * 0.145f);
        bool sel = tactField == i;
        BeveledFill(ref f, rc + new Vector2(2, 3) * sc, cardS, 7f * sc, Shadow());
        BeveledFill(ref f, rc, cardS, 7f * sc, sel ? new Color(8, 34, 46, 226) : new Color(5, 24, 34, 195));
        BeveledOutline(ref f, rc, cardS, 7f * sc, sel ? Pale() : new Color(82, 205, 245, 120), sel ? 1.2f * sc : 0.75f * sc);
        Text(ref f, (sel ? "> " : "  ") + tactFields[i], new Vector2(rc.X - cardS.X * 0.40f, rc.Y - 10f * sc), sel ? new Color(255, 218, 72, 235) : Pale(), 0.42f * sc, TextAlignment.LEFT);
        Text(ref f, TactDisplayValue(i), new Vector2(rc.X + cardS.X * 0.36f, rc.Y - 10f * sc), TextCol(), 0.42f * sc, TextAlignment.RIGHT);
    }

    Vector2 info = new Vector2(size.X * 0.5f, size.Y * 0.830f);
    Vector2 infoS = new Vector2(size.X * 0.78f, size.Y * 0.080f);
    BeveledFill(ref f, info, infoS, 6f * sc, new Color(2, 13, 20, 145));
    Text(ref f, "CTRL " + tactCtrl.ToString() + "   SKIP STATIC " + tactSkipStation.ToString() + "   FAIL " + tactFail.ToString(), info + new Vector2(0, -9f * sc), TextCol(), 0.30f * sc, TextAlignment.CENTER);
    Text(ref f, "PB2 " + tactPb2, info + new Vector2(0, 16f * sc), Dim(), 0.25f * sc, TextAlignment.CENTER);
}

void ApplyMagSaved(MagRow r)
{
    string block = ExtractBlock(Me.CustomData, MAGCFG_BEGIN, MAGCFG_END);
    if (block.Length == 0) return;
    string[] lines = Lines(block);
    for (int i = 0; i < lines.Length; i++)
    {
        string line = lines[i].Trim();
        if (!line.StartsWith("MAGCFG|", StringComparison.OrdinalIgnoreCase)) continue;
        string[] p = line.Split('|');
        if (p.Length < 3) continue;
        if (!p[1].Equals(r.Wpn, StringComparison.OrdinalIgnoreCase)) continue;
        if (r.AmmoSub.Length > 0 && p[2].Length > 0 && !p[2].Equals(r.AmmoSub, StringComparison.OrdinalIgnoreCase)) continue;
        r.Min = ToInt(Field(p, "MIN"), r.Min);
        r.Max = ToInt(Field(p, "MAX"), r.Max);
        return;
    }
}

void ReadMagConfig()
{
    string block = ExtractBlock(Me.CustomData, MAGCFG_BEGIN, MAGCFG_END);
    if (block.Length == 0) return;
    string[] lines = Lines(block);
    for (int i = 0; i < lines.Length; i++)
    {
        string line = lines[i].Trim();
        if (!line.StartsWith("MAGCFG|", StringComparison.OrdinalIgnoreCase)) continue;
        string[] p = line.Split('|');
        if (p.Length < 3) continue;
        MagRow r = FindMagRow(p[1], p[2]);
        if (r == null) continue; // Saved config must not create visible rows without a live ammo box.
        r.Min = ToInt(Field(p, "MIN"), r.Min);
        r.Max = ToInt(Field(p, "MAX"), r.Max);
    }
}

MagRow FindMagRow(string wpn, string ammo)
{
    for (int i = 0; i < magRows.Count; i++)
    {
        MagRow r = magRows[i];
        if (!r.Wpn.Equals(wpn, StringComparison.OrdinalIgnoreCase)) continue;
        if (ammo.Length == 0 || r.AmmoSub.Length == 0 || r.AmmoSub.Equals(ammo, StringComparison.OrdinalIgnoreCase)) return r;
    }
    return null;
}

void WriteMagConfig()
{
    StringBuilder b = new StringBuilder();
    b.AppendLine(MAGCFG_BEGIN);
    b.AppendLine("# WSO AMMO BOX QTY CONFIG");
    for (int i = 0; i < magRows.Count; i++)
    {
        MagRow r = magRows[i];
        if (r == null || r.Wpn == null || r.Wpn.Length == 0 || r.AmmoSub == null || r.AmmoSub.Length == 0) continue;
        b.AppendLine("MAGCFG|" + r.Wpn + "|" + r.AmmoSub + "|MIN|" + r.Min.ToString() + "|MAX|" + r.Max.ToString());
    }
    b.AppendLine(MAGCFG_END);
    Me.CustomData = ReplaceBlock(Me.CustomData, MAGCFG_BEGIN, MAGCFG_END, b.ToString());
}

string ExtractBlock(string text, string begin, string end)
{
    if (text == null) return "";
    int a = text.IndexOf(begin, StringComparison.OrdinalIgnoreCase);
    if (a < 0) return "";
    a += begin.Length;
    int b = text.IndexOf(end, a, StringComparison.OrdinalIgnoreCase);
    if (b < 0) return "";
    return text.Substring(a, b - a).Trim();
}

string ReplaceBlock(string text, string begin, string end, string block)
{
    if (text == null) text = "";
    int a = text.IndexOf(begin, StringComparison.OrdinalIgnoreCase);
    int b = text.IndexOf(end, StringComparison.OrdinalIgnoreCase);
    if (a >= 0 && b > a) text = text.Remove(a, b + end.Length - a).Trim();
    if (text.Length == 0) return block.Trim();
    return text + "\n\n" + block.Trim();
}

void BuildFallbackMagRows()
{
    for (int i = 0; i < wpnModules.Count; i++)
    {
        WpnModule w = wpnModules[i];
        if (w == null || w.Key == null || w.Key.Length == 0) continue;
        string sub = AmmoSubFromWeaponType(w.WeaponType);
        if (sub.Length == 0 || sub == "MIXED") continue;
        MagRow r = new MagRow();
        r.Wpn = w.Key;
        r.AmmoSub = sub;
        r.AmmoLabel = AmmoLabelFromSub(sub);
        r.Loc = LocFromPos(w.Pos);
        r.Pos = w.Pos;
        r.HasPos = true;
        r.Box = 0;
        r.Gun = (int)Math.Round(w.Ammo);
        r.State = MagState(r);
        r.Live = false;
        ApplyMagSaved(r);
        magRows.Add(r);
    }
}

void SortMagRows()
{
    magRows.Sort(delegate(MagRow a, MagRow b)
    {
        int na = WpnNumber(a == null ? null : a.Wpn);
        int nb = WpnNumber(b == null ? null : b.Wpn);
        if (na != nb) return na.CompareTo(nb);
        string sa = a == null || a.Wpn == null ? "" : a.Wpn;
        string sb = b == null || b.Wpn == null ? "" : b.Wpn;
        return string.Compare(sa, sb, StringComparison.OrdinalIgnoreCase);
    });
}

int WpnNumber(string s)
{
    if (s == null) return 999999;
    int n = 0;
    bool any = false;
    for (int i = 0; i < s.Length; i++)
    {
        char ch = s[i];
        if (ch >= '0' && ch <= '9')
        {
            any = true;
            n = n * 10 + (ch - '0');
        }
        else if (any) break;
    }
    return any ? n : 999999;
}

string AmmoSubFromWeaponType(string t)
{
    if (t == null) return "";
    if (t.IndexOf("ART", StringComparison.OrdinalIgnoreCase) >= 0) return "LargeCalibreAmmo";
    if (t.IndexOf("ASSAULT", StringComparison.OrdinalIgnoreCase) >= 0) return "MediumCalibreAmmo";
    if (t.IndexOf("AUTO", StringComparison.OrdinalIgnoreCase) >= 0) return "AutocannonClip";
    if (t.IndexOf("GAT", StringComparison.OrdinalIgnoreCase) >= 0) return "NATO_25x184mmMagazine";
    if (t.IndexOf("MISS", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("ROCKET", StringComparison.OrdinalIgnoreCase) >= 0) return "Missile200mm";
    if (t.IndexOf("RAIL", StringComparison.OrdinalIgnoreCase) >= 0) return "SmallRailgunAmmo";
    if (t.IndexOf("INTERIOR", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("RIFLE", StringComparison.OrdinalIgnoreCase) >= 0) return "RapidFireAutomaticRifleGun_Mag_50rd";
    return "";
}

string AmmoLabelFromSub(string s)
{
    if (s == null) return "AMMO";
    if (s.IndexOf("LargeCalibre", StringComparison.OrdinalIgnoreCase) >= 0) return "ARTILLERY";
    if (s.IndexOf("MediumCalibre", StringComparison.OrdinalIgnoreCase) >= 0) return "ASSAULT";
    if (s.IndexOf("Autocannon", StringComparison.OrdinalIgnoreCase) >= 0) return "AUTOCANNON";
    if (s.IndexOf("NATO_25x184", StringComparison.OrdinalIgnoreCase) >= 0) return "GATLING";
    if (s.IndexOf("Missile", StringComparison.OrdinalIgnoreCase) >= 0) return "MISSILE";
    if (s.IndexOf("Railgun", StringComparison.OrdinalIgnoreCase) >= 0) return "RAILGUN";
    if (s.IndexOf("RapidFire", StringComparison.OrdinalIgnoreCase) >= 0) return "RIFLE MAG";
    return ShortText(s, 12).ToUpperInvariant();
}

string LocFromPos(Vector3D p)
{
    // PB2 sends raw local meters, not normalized coordinates.
    // Normalize against the hull bounds before labeling, otherwise a tiny centerline offset
    // like -0.5m on a wide ship gets mislabeled PORT.
    Vector3D n = NormShipPos(p);

    string z = n.Z > 0.32 ? "FWD" : (n.Z < -0.32 ? "AFT" : "MID");
    string x = n.X > 0.32 ? "STBD" : (n.X < -0.32 ? "PORT" : "CTR");
    string y = n.Y > 0.40 ? "DORSAL" : (n.Y < -0.40 ? "VENTRAL" : "MID");

    if (z != "MID" && x != "CTR") return z + " " + x;
    if (z != "MID" && y != "MID") return z + " " + y;
    if (z != "MID") return z;
    if (x != "CTR" && y != "MID") return y + " " + x;
    if (x != "CTR") return x;
    if (y != "MID") return y;
    return "MID";
}

Vector3D NormShipPos(Vector3D p)
{
    if (!hullReady) return p;
    Vector3D c = (hullMin + hullMax) * 0.5;
    Vector3D h = (hullMax - hullMin) * 0.5;
    double x = Math.Abs(h.X) > 0.1 ? (p.X - c.X) / h.X : 0;
    double y = Math.Abs(h.Y) > 0.1 ? (p.Y - c.Y) / h.Y : 0;
    double z = Math.Abs(h.Z) > 0.1 ? (p.Z - c.Z) / h.Z : 0;
    return new Vector3D(ClampD(x, -1, 1), ClampD(y, -1, 1), ClampD(z, -1, 1));
}

string PosProof(MagRow r)
{
    if (r == null || !r.HasPos) return "";
    return "XYZ " + Signed2(r.Pos.X) + " / " + Signed2(r.Pos.Y) + " / " + Signed2(r.Pos.Z);
}

double ClampD(double v, double lo, double hi)
{
    if (v < lo) return lo;
    if (v > hi) return hi;
    return v;
}

string Signed2(double v)
{
    return (v >= 0 ? "+" : "-") + Math.Abs(v).ToString("0.00");
}

string MagState(MagRow r)
{
    int total = r.Box + r.Gun;
    if (total <= 0) return "RED";
    if (r.Min > 0 && total < r.Min) return "YELLOW";
    return "GREEN";
}

Color MagStateColor(MagRow r)
{
    string st = r.State == null || r.State.Length == 0 ? MagState(r) : r.State;
    if (st.Equals("RED", StringComparison.OrdinalIgnoreCase)) return HostileRed();
    if (st.Equals("YELLOW", StringComparison.OrdinalIgnoreCase)) return WarnOrange();
    return Pale();
}

void DrawAmmoBoxQtyPage(ref MySpriteDrawFrame f, Vector2 size, Vector2 c, float sc)
{
    if (magRows.Count == 0)
    {
        Text(ref f, "AMMO BOX QTY", new Vector2(c.X, size.Y * 0.145f), Pale(), 0.62f * sc, TextAlignment.CENTER);
        Text(ref f, "NO MAG PACKETS", c + new Vector2(0, -12f * sc), WarnOrange(), 0.55f * sc, TextAlignment.CENTER);
        Text(ref f, "WSO PB2 MAGSTAT NEEDED", c + new Vector2(0, 42f * sc), TextCol(), 0.36f * sc, TextAlignment.CENTER);
        return;
    }

    int cols = 2;
    int rows = 3;
    int perPage = cols * rows;
    int selectedRow = magPickField / 2;
    int first = (selectedRow / perPage) * perPage;
    int pageNo = first / perPage + 1;
    int pageMax = (magRows.Count + perPage - 1) / perPage;

    // Cockpit-console safe viewport: the physical frame masks the top and bottom of this surface.
    // Everything on this page is deliberately built inside this visible band, not the full TextSurface.
    float headerY = size.Y * 0.230f;
    float gridTop = size.Y * 0.300f;
    float safeLeft = size.X * 0.060f;
    float safeRight = size.X * 0.940f;

    float gapX = size.X * 0.020f;
    float gapY = size.Y * 0.015f;
    float areaW = safeRight - safeLeft;
    // Hard cockpit-safe layout: cards are 25% shorter than the previous pass and
    // the grid is lowered to balance the visible cockpit padding above and below it.
    float oldAreaH = size.Y * (0.812f - 0.192f);
    float oldCardH = (oldAreaH - size.Y * 0.014f * 2f) / 3f;
    float cardH = oldCardH * 0.75f;
    Vector2 cardS = new Vector2((areaW - gapX) / 2f, cardH);

    for (int si = 0; si < perPage; si++)
    {
        int idx = first + si;
        if (idx >= magRows.Count) break;
        // Display/navigation order is column-major: top-to-bottom down the left column,
        // then top-to-bottom down the right column. This keeps numeric WPN order intuitive.
        int gridRow = si % rows;
        int gridCol = si / rows;
        MagRow r = magRows[idx];
        bool rowSel = idx == selectedRow;
        Vector2 cardTopLeft = new Vector2(safeLeft + gridCol * (cardS.X + gapX), gridTop + gridRow * (cardS.Y + gapY));
        Vector2 rc = cardTopLeft + cardS * 0.5f;

        Color edge = rowSel ? Pale() : new Color(82, 205, 245, 125);
        Color fill = rowSel ? new Color(8, 34, 46, 220) : new Color(5, 24, 34, 205);
        BeveledFill(ref f, rc + new Vector2(1.7f, 2.2f) * sc, cardS, 5.5f * sc, Shadow());
        BeveledFill(ref f, rc, cardS, 5.5f * sc, fill);
        BeveledOutline(ref f, rc, cardS, 5.5f * sc, edge, rowSel ? 1.12f * sc : 0.72f * sc);

        // Thin state stripe: ammo sufficiency cue without stealing layout space.
        Vector2 stripeC = new Vector2(rc.X - cardS.X * 0.468f, rc.Y);
        Rect(ref f, stripeC, new Vector2(3.0f * sc, cardS.Y * 0.70f), MagStateColor(r), true, 0f, 0f);

        float lx = rc.X - cardS.X * 0.415f;
        float rx = rc.X + cardS.X * 0.405f;
        float top = rc.Y - cardS.Y * 0.340f;
        float line = cardS.Y * 0.225f;

        // First line is intentionally larger: custom module/location labels are the hardest to read.
        Text(ref f, ShortText(r.Wpn + " " + r.Loc, 18), new Vector2(lx, top), Pale(), 0.465f * sc, TextAlignment.LEFT);
        Text(ref f, ShortText(r.AmmoLabel, 13), new Vector2(lx, top + line * 0.98f), TextCol(), 0.320f * sc, TextAlignment.LEFT);
        Text(ref f, "BOX " + r.Box.ToString(), new Vector2(lx, top + line * 1.88f), TextCol(), 0.310f * sc, TextAlignment.LEFT);
        Text(ref f, "GUN " + r.Gun.ToString(), new Vector2(rc.X - cardS.X * 0.070f, top + line * 1.88f), TextCol(), 0.310f * sc, TextAlignment.LEFT);

        bool minSel = rowSel && (magPickField % 2 == 0);
        bool maxSel = rowSel && (magPickField % 2 == 1);
        Color selectedQtyCol = new Color(255, 218, 72, 235);
        Color normalQtyCol = TextCol();
        Vector2 q1 = new Vector2(rc.X + cardS.X * 0.275f, top + line * 1.00f);
        Vector2 q2 = new Vector2(rc.X + cardS.X * 0.275f, top + line * 1.90f);
        Text(ref f, (minSel ? ">" : " ") + "MIN " + r.Min.ToString(), q1, minSel ? selectedQtyCol : normalQtyCol, 0.355f * sc, TextAlignment.LEFT);
        Text(ref f, (maxSel ? ">" : " ") + "MAX " + r.Max.ToString(), q2, maxSel ? selectedQtyCol : normalQtyCol, 0.355f * sc, TextAlignment.LEFT);
    }

    // Draw header last so it cannot be buried by card plates. It is placed in the visible
    // cockpit-safe top band, with the card grid pushed down below it.
    Rect(ref f, new Vector2(size.X * 0.500f, headerY + 1.5f * sc), new Vector2(size.X * 0.780f, 22f * sc), new Color(2, 13, 20, 150), true, 1f, 0f);
    Text(ref f, "AMMO BOX QTY", new Vector2(size.X * 0.105f, headerY), Pale(), 0.455f * sc, TextAlignment.LEFT);
    Text(ref f, "STEP " + MagStep().ToString(), new Vector2(size.X * 0.500f, headerY), Pale(), 0.380f * sc, TextAlignment.CENTER);
    Text(ref f, "P" + pageNo.ToString() + "/" + pageMax.ToString(), new Vector2(size.X * 0.895f, headerY), Pale(), 0.380f * sc, TextAlignment.RIGHT);
}

void DrawExtFocusRails(ref MySpriteDrawFrame f, Vector2 size, float sc)
{
    Color col = Pale();
    float yTop = size.Y * 0.035f, yBot = size.Y * 0.965f;
    float x1 = size.X * 0.175f, x2 = size.X * 0.825f;
    float wing = 18f * sc, outY = 12f * sc;
    float w = 1.55f * sc;
    Line(ref f, new Vector2(x1, yTop), new Vector2(x2, yTop), w, col);
    Line(ref f, new Vector2(x1, yBot), new Vector2(x2, yBot), w, col);
    // Top: /________\ ; Bottom: \________/ .
    Line(ref f, new Vector2(x1 - wing, yTop + outY), new Vector2(x1, yTop), w, col);
    Line(ref f, new Vector2(x2, yTop), new Vector2(x2 + wing, yTop + outY), w, col);
    Line(ref f, new Vector2(x1 - wing, yBot - outY), new Vector2(x1, yBot), w, col);
    Line(ref f, new Vector2(x2, yBot), new Vector2(x2 + wing, yBot - outY), w, col);
}

void DrawWeaponStatusCard(ref MySpriteDrawFrame f, Vector2 size, Vector2 c, float sc, string label, bool focused, int hatchVariant)
{
    Color dim = new Color(82, 205, 245, 65);
    Color band = new Color(5, 24, 34, 145);
    Color band2 = new Color(5, 24, 34, 118);

    DrawBgPattern(ref f, size, sc, hatchVariant);

    if (focused) DrawExtFocusRails(ref f, size, sc);

    Vector2 headC = new Vector2(c.X, size.Y * 0.155f);
    Vector2 headS = new Vector2(size.X * 0.54f, 48f * sc);
    Rect(ref f, headC, headS, band, true, 1f, 0f);
    Text(ref f, "WEAPON STATUS", headC + new Vector2(0, -8f * sc), Pale(), 0.76f * sc, TextAlignment.CENTER);

    Vector2 sideC = new Vector2(size.X * 0.42f, size.Y * 0.355f);
    Vector2 frontC = new Vector2(size.X * 0.42f, size.Y * 0.690f);
    Vector2 sideS = new Vector2(size.X * 0.61f, size.Y * 0.245f);
    Vector2 frontS = new Vector2(size.X * 0.61f, size.Y * 0.245f);
    Rect(ref f, sideC, sideS, new Color(2, 7, 11, 168), true, 1f, 0f);
    Rect(ref f, frontC, frontS, new Color(2, 7, 11, 168), true, 1f, 0f);

    Vector2 rightC = new Vector2(size.X * 0.805f, size.Y * 0.535f);
    Vector2 rightS = new Vector2(size.X * 0.25f, size.Y * 0.49f);
    Rect(ref f, rightC, rightS, band2, true, 1f, 0f);

    if (pb2Status == "ANCHOR LOST")
    {
        Text(ref f, "NAV", new Vector2(c.X, size.Y * 0.430f), WarnOrange(), 0.90f * sc, TextAlignment.CENTER);
        Text(ref f, "ANCHOR LOST", new Vector2(c.X, size.Y * 0.525f), WarnOrange(), 0.62f * sc, TextAlignment.CENTER);
        Text(ref f, "TAG [FC] [WSO] OR [IMS]", new Vector2(c.X, size.Y * 0.640f), TextCol(), 0.36f * sc, TextAlignment.CENTER);
        return;
    }

    DrawSideHull(ref f, sideC, sideS, sc, dim);
    DrawFrontHull(ref f, frontC, frontS, sc, dim);

    Text(ref f, "SIDE", sideC + new Vector2(-sideS.X * 0.45f, -sideS.Y * 0.43f), new Color(220, 252, 255, 185), 0.58f * sc, TextAlignment.LEFT);
    Text(ref f, "FRONT", frontC + new Vector2(-frontS.X * 0.45f, -frontS.Y * 0.43f), new Color(220, 252, 255, 185), 0.58f * sc, TextAlignment.LEFT);

    int ready = 0, degraded = 0, offline = 0;
    for (int i = 0; i < wpnModules.Count; i++)
    {
        WpnModule m = wpnModules[i];
        if (m.Offline > 0) offline++;
        else if (m.Degraded > 0) degraded++;
        else ready++;
        DrawModuleDotViews(ref f, m, i == wpnPick, sideC, sideS, frontC, frontS, sc);
    }

    // Right card uses tight clusters. Legend-size text is acceptable here only for compact status rows; no debug-list spacing.
    Text(ref f, "MODULES", new Vector2(rightC.X, size.Y * 0.310f), Cyan(), 0.52f * sc, TextAlignment.CENTER);
    Text(ref f, "READY " + ready.ToString(), new Vector2(rightC.X, size.Y * 0.365f), Pale(), 0.46f * sc, TextAlignment.CENTER);
    Text(ref f, "DEG " + degraded.ToString(), new Vector2(rightC.X, size.Y * 0.405f), WarnOrange(), 0.44f * sc, TextAlignment.CENTER);
    Text(ref f, "OFFLINE " + offline.ToString(), new Vector2(rightC.X, size.Y * 0.445f), HostileRed(), 0.40f * sc, TextAlignment.CENTER);

    if (wpnModules.Count == 0)
    {
        Text(ref f, "NO WEAPONS", new Vector2(rightC.X, size.Y * 0.680f), WarnOrange(), 0.42f * sc, TextAlignment.CENTER);
        return;
    }

    if (wpnPick >= wpnModules.Count) wpnPick = Math.Max(0, wpnModules.Count - 1);
    WpnModule sel = wpnModules[wpnPick];
    string state = ModuleState(sel);
    Color stateCol = ModuleColor(sel);
    string ammo = AmmoText(sel);
    Color ammoCol = AmmoColor(sel);

    Text(ref f, "SELECT", new Vector2(rightC.X, size.Y * 0.550f), Cyan(), 0.46f * sc, TextAlignment.CENTER);
    Text(ref f, sel.Key, new Vector2(rightC.X, size.Y * 0.605f), Pale(), 0.66f * sc, TextAlignment.CENTER);
    Text(ref f, ShortText(sel.WeaponType, 13), new Vector2(rightC.X, size.Y * 0.660f), TextCol(), 0.43f * sc, TextAlignment.CENTER);
    Text(ref f, ammo, new Vector2(rightC.X, size.Y * 0.720f), ammoCol, 0.43f * sc, TextAlignment.CENTER);
    if (state.Length > 0) Text(ref f, state, new Vector2(rightC.X, size.Y * 0.765f), stateCol, 0.41f * sc, TextAlignment.CENTER);
}

void HullLine(ref MySpriteDrawFrame f, Vector2 a, Vector2 b, float sc, Color col)
{
    Line(ref f, a, b, 2.4f * sc, new Color(0, 0, 0, 150));
    Line(ref f, a, b, 1.0f * sc, col);
}

void HullInnerLine(ref MySpriteDrawFrame f, Vector2 a, Vector2 b, float sc)
{
    Line(ref f, a, b, 0.55f * sc, new Color(82, 205, 245, 55));
}

void DrawSideHull(ref MySpriteDrawFrame f, Vector2 c, Vector2 s, float sc, Color col)
{
    float l = c.X - s.X * 0.40f, r = c.X + s.X * 0.40f;
    float t = c.Y - s.Y * 0.28f, b = c.Y + s.Y * 0.28f;
    float n = r - 26f * sc;
    Color fill = new Color(16, 76, 96, 34);
    FillQuad(ref f, new Vector2(l, t), new Vector2(n, t), new Vector2(n, b), new Vector2(l, b), fill, Math.Max(1.25f, 1.4f * sc));
    FillTri(ref f, new Vector2(n, t), new Vector2(r, c.Y), new Vector2(n, b), fill, Math.Max(1.25f, 1.4f * sc));
    HullLine(ref f, new Vector2(l, t), new Vector2(n, t), sc, col);
    HullLine(ref f, new Vector2(n, t), new Vector2(r, c.Y), sc, col);
    HullLine(ref f, new Vector2(r, c.Y), new Vector2(n, b), sc, col);
    HullLine(ref f, new Vector2(n, b), new Vector2(l, b), sc, col);
    HullLine(ref f, new Vector2(l, b), new Vector2(l, t), sc, col);
    float inset = 5f * sc;
    HullInnerLine(ref f, new Vector2(l + inset, t + inset), new Vector2(n - inset, t + inset), sc);
    HullInnerLine(ref f, new Vector2(l + inset, b - inset), new Vector2(n - inset, b - inset), sc);
    HullInnerLine(ref f, new Vector2(l + inset, t + inset), new Vector2(l + inset, b - inset), sc);
    HullInnerLine(ref f, new Vector2(n - inset, t + inset), new Vector2(r - inset * 0.55f, c.Y), sc);
    HullInnerLine(ref f, new Vector2(r - inset * 0.55f, c.Y), new Vector2(n - inset, b - inset), sc);
}

void DrawFrontHull(ref MySpriteDrawFrame f, Vector2 c, Vector2 s, float sc, Color col)
{
    float l = c.X - s.X * 0.25f, r = c.X + s.X * 0.25f;
    float t = c.Y - s.Y * 0.34f, b = c.Y + s.Y * 0.34f;
    Vector2 top = new Vector2(c.X, t);
    Vector2 rs = new Vector2(r, c.Y - s.Y * 0.08f);
    Vector2 rb = new Vector2(r * 0.94f + c.X * 0.06f, b);
    Vector2 lb = new Vector2(l * 0.94f + c.X * 0.06f, b);
    Vector2 ls = new Vector2(l, c.Y - s.Y * 0.08f);
    Color fill = new Color(16, 76, 96, 34);
    FillTri(ref f, top, rs, ls, fill, Math.Max(1.25f, 1.4f * sc));
    FillQuad(ref f, ls, rs, rb, lb, fill, Math.Max(1.25f, 1.4f * sc));
    HullLine(ref f, top, rs, sc, col);
    HullLine(ref f, rs, rb, sc, col);
    HullLine(ref f, rb, lb, sc, col);
    HullLine(ref f, lb, ls, sc, col);
    HullLine(ref f, ls, top, sc, col);
    float inset = 5f * sc;
    Vector2 top2 = new Vector2(c.X, t + inset * 1.35f);
    Vector2 rs2 = new Vector2(r - inset, c.Y - s.Y * 0.08f + inset * 0.45f);
    Vector2 rb2 = new Vector2(rb.X - inset, b - inset);
    Vector2 lb2 = new Vector2(lb.X + inset, b - inset);
    Vector2 ls2 = new Vector2(l + inset, c.Y - s.Y * 0.08f + inset * 0.45f);
    HullInnerLine(ref f, top2, rs2, sc);
    HullInnerLine(ref f, rs2, rb2, sc);
    HullInnerLine(ref f, rb2, lb2, sc);
    HullInnerLine(ref f, lb2, ls2, sc);
    HullInnerLine(ref f, ls2, top2, sc);
}

void DrawModuleDotViews(ref MySpriteDrawFrame f, WpnModule m, bool selected, Vector2 sideC, Vector2 sideS, Vector2 frontC, Vector2 frontS, float sc)
{
    Color col = ModuleColor(m);
    float z = Norm(m.Pos.Z, hullMin.Z, hullMax.Z);
    float x = Norm(m.Pos.X, hullMin.X, hullMax.X);
    float y = Norm(m.Pos.Y, hullMin.Y, hullMax.Y);

    Vector2 sideP = new Vector2(sideC.X - sideS.X * 0.40f + z * sideS.X * 0.80f, sideC.Y + sideS.Y * 0.30f - y * sideS.Y * 0.60f);
    Vector2 frontP = new Vector2(frontC.X + frontS.X * 0.28f - x * frontS.X * 0.56f, frontC.Y + frontS.Y * 0.35f - y * frontS.Y * 0.70f);
    Dot(ref f, sideP, 5.2f * sc, col);
    Dot(ref f, frontP, 5.2f * sc, col);
    Ring(ref f, sideP, 7.0f * sc, new Color(0, 0, 0, 120), 0.75f * sc);
    Ring(ref f, frontP, 7.0f * sc, new Color(0, 0, 0, 120), 0.75f * sc);
    if (selected)
    {
        Ring(ref f, sideP, 10.0f * sc, Pale(), 1.15f * sc);
        Ring(ref f, frontP, 10.0f * sc, Pale(), 1.15f * sc);
    }
}

void DrawContactCard(ref MySpriteDrawFrame f, Vector2 size, Vector2 c, float sc, string label, bool focused, int hatchVariant)
{
    Color rail = focused ? Pale() : new Color(82, 205, 245, 145);
    Color dim = new Color(82, 205, 245, 65);
    Color band = new Color(5, 24, 34, 145);
    Color band2 = new Color(5, 24, 34, 118);
    DrawBgPattern(ref f, size, sc, hatchVariant);
    if (focused) DrawExtFocusRails(ref f, size, sc);
    Vector2 headC = new Vector2(c.X, size.Y * 0.168f);
    Vector2 headS = new Vector2(size.X * 0.42f, 50f * sc);
    Rect(ref f, headC, headS, band, true, 1f, 0f);
    Text(ref f, "CONTACTS", headC + new Vector2(0, -9f * sc), Pale(), 0.88f * sc, TextAlignment.CENTER);
    Vector2 radar = new Vector2(size.X * 0.335f, size.Y * 0.545f);
    float outer = 112f * sc;
    float mid = 72f * sc;
    float inner = 36f * sc;
    Rect(ref f, radar, new Vector2(286f, 286f) * sc, new Color(2, 7, 11, 172), true, 1f, 0f);
    Vector2 rcMask = new Vector2(size.X * 0.775f, size.Y * 0.555f);
    Vector2 rsMask = new Vector2(size.X * 0.29f, size.Y * 0.47f);
    Rect(ref f, rcMask, rsMask, band2, true, 1f, 0f);

    Ring(ref f, radar, outer, dim, 1.05f * sc);
    Ring(ref f, radar, mid, new Color(82, 205, 245, 55), 0.85f * sc);
    Ring(ref f, radar, inner, new Color(82, 205, 245, 38), 0.65f * sc);
    Line(ref f, radar + new Vector2(-outer, 0), radar + new Vector2(outer, 0), 0.65f * sc, new Color(82, 205, 245, 42));
    FillTri(ref f, radar + new Vector2(0, -15f) * sc, radar + new Vector2(-9f, 10f) * sc, radar + new Vector2(9f, 10f) * sc, Cyan(), 1f * sc);
    Dot(ref f, radar, 3.5f * sc, Pale());

    for (int i = 0; i < contacts.Count; i++)
    {
        ContactDemo ct = contacts[i];
        Vector2 p = radar + new Vector2(ct.X, ct.Y) * outer;
        bool selected = (i == contactPick && focused);
        bool soft = (i == contactSoft);
        bool manual = (i == contactManual);
        DrawContactMarker(ref f, p, sc, ct, selected, soft, manual);
    }

    float rx = size.X * 0.775f;
    if (contacts.Count > 0)
    {
        ContactDemo sel = contacts[contactPick];
        Color selCol = sel.WeaponTarget ? WarnOrange() : (sel.Rel == "HOSTILE" ? HostileRed() : Pale());
        string elev = ElevText(sel.ElevBand);
        string mode = "SELECT";
        if (contactManual == contactPick) mode = "MANUAL";
        else if (contactSoft == contactPick) mode = "SCOPE";
        else if (focused) mode = "PENDING";
        Text(ref f, mode, new Vector2(rx, size.Y * 0.36f), mode == "MANUAL" ? Pale() : Cyan(), 0.52f * sc, TextAlignment.CENTER);
        Text(ref f, sel.Id, new Vector2(rx, size.Y * 0.430f), selCol, 1.02f * sc, TextAlignment.CENTER);
        Text(ref f, sel.Rel, new Vector2(rx, size.Y * 0.492f), selCol, 0.52f * sc, TextAlignment.CENTER);
        float classY = size.Y * 0.595f;
        if (sel.Move != null && sel.Move.Length > 0)
        {
            Text(ref f, sel.Move, new Vector2(rx, size.Y * 0.545f), selCol, 0.44f * sc, TextAlignment.CENTER);
            classY = size.Y * 0.625f;
        }

        Text(ref f, sel.ClassName, new Vector2(rx, classY), TextCol(), 0.51f * sc, TextAlignment.CENTER);
        Text(ref f, sel.Arc + " / " + elev, new Vector2(rx, classY + 29f * sc), TextCol(), 0.47f * sc, TextAlignment.CENTER);
        Text(ref f, sel.RangeKm.ToString("0.00") + " KM", new Vector2(rx, size.Y * 0.735f), Pale(), 0.62f * sc, TextAlignment.CENTER);
    }
    else
    {
        Text(ref f, "NO", new Vector2(rx, size.Y * 0.45f), Cyan(), 0.78f * sc, TextAlignment.CENTER);
        Text(ref f, "CONTACTS", new Vector2(rx, size.Y * 0.535f), Pale(), 0.58f * sc, TextAlignment.CENTER);
        Text(ref f, "PB2 TARGET DATA", new Vector2(rx, size.Y * 0.64f), TextCol(), 0.40f * sc, TextAlignment.CENTER);
    }
    Vector2 lc = new Vector2(size.X * 0.125f, size.Y * 0.845f);
    Dot(ref f, lc + new Vector2(0, 0), 4.5f * sc, HostileRed());
    Text(ref f, "ENEMY", lc + new Vector2(15f * sc, -3f * sc), TextCol(), 0.31f * sc, TextAlignment.LEFT);
    Dot(ref f, lc + new Vector2(0, 16f * sc), 4.5f * sc, WarnOrange());
    Text(ref f, "TARGET", lc + new Vector2(15f * sc, 13f * sc), TextCol(), 0.31f * sc, TextAlignment.LEFT);
    Dot(ref f, lc + new Vector2(0, 32f * sc), 4.5f * sc, Pale());
    Text(ref f, "NEUTRAL", lc + new Vector2(15f * sc, 29f * sc), TextCol(), 0.31f * sc, TextAlignment.LEFT);
}

void DrawContactMarker(ref MySpriteDrawFrame f, Vector2 p, float sc, ContactDemo ct, bool selected, bool soft, bool manual)
{
    Color col = ct.WeaponTarget ? WarnOrange() : (ct.Rel == "HOSTILE" ? HostileRed() : Pale());
    float r = 4.2f * sc;
    if (ct.SizeClass == 1) r = 6.0f * sc;
    else if (ct.SizeClass >= 2) r = 8.0f * sc;
    if (ct.ElevBand != 0)
    {
        float deg = Math.Abs(ct.ElevBand) == 1 ? 30f : 60f;
        float signY = ct.ElevBand > 0 ? -1f : 1f;
        double a = Math.PI / 180.0 * deg;
        Vector2 dir = new Vector2((float)Math.Cos(a), signY * (float)Math.Sin(a));
        Vector2 a0 = p + dir * (r * 0.55f);
        Vector2 a1 = p + dir * (r + 13f * sc);
        Line(ref f, a0, a1, 2.2f * sc, col);
    }

    Dot(ref f, p, r, col);

    if (selected || soft || manual)
    {
        Color ringCol = manual ? Pale() : (soft ? new Color(220, 252, 255, 190) : new Color(220, 252, 255, 150));
        Ring(ref f, p, r + (manual ? 8f * sc : 6f * sc), ringCol, manual ? 1.7f * sc : 1.15f * sc);
    }
}

string ElevText(int e)
{
    if (e >= 2) return "STEEP HIGH";
    if (e == 1) return "HIGH";
    if (e == 0) return "LEVEL";
    if (e == -1) return "LOW";
    return "STEEP LOW";
}

Color Bg() { return new Color(2, 7, 11); }
Color Panel() { return new Color(5, 24, 34, 240); }
Color Grid() { return new Color(20, 74, 92, 72); }
Color Grid2() { return new Color(42, 130, 148, 100); }
Color Cyan() { return new Color(82, 205, 245); }
Color Cyan2() { return new Color(28, 142, 210); }
Color HostileRed() { return new Color(255, 68, 58); }
Color WarnOrange() { return new Color(255, 146, 48); }
Color WarnOrangeDim() { return new Color(140, 72, 25); }
Color BlueMid() { return new Color(24, 118, 190); }
Color BlueDark() { return new Color(5, 48, 88); }
Color Ink() { return new Color(1, 8, 15); }
Color Pale() { return new Color(220, 252, 255); }
Color TextCol() { return new Color(210, 248, 255); }
Color Dim() { return new Color(38, 115, 145, 150); }
Color Shadow() { return new Color(0, 0, 0, 185); }

void DrawBgPattern(ref MySpriteDrawFrame f, Vector2 size, float sc, int variant)
{
    float step = 128f * sc;
    float t = 1.0f * sc;
    Color hatch = Grid();

    int rows = (int)Math.Ceiling(size.Y / step) + 1;
    int cols = (int)Math.Ceiling(size.X / step) + 1;
    float[] vA = new float[] { 0.34f, 0.57f, 0.43f, 0.71f, 0.49f, 0.62f };
    float[,] hA = new float[,] { {0.27f,0.61f}, {0.36f,0.73f}, {0.24f,0.52f}, {0.43f,0.69f}, {0.31f,0.58f} };

    int hRows = hA.GetLength(0);
    for (int r = 0; r < rows; r++)
    {
        float y0 = r * step;
        int idx = r % hRows;
        HatchFullH(ref f, size, y0 + step * hA[idx,0], t, hatch);
        HatchFullH(ref f, size, y0 + step * hA[idx,1], t, hatch);
    }
    for (int col = 0; col < cols; col++)
    {
        float x0 = col * step;
        HatchFullV(ref f, size, x0 + step * vA[col % vA.Length], t, hatch);
    }
    for (float x = 0; x <= size.X; x += 128f * sc)
        Line(ref f, new Vector2(x, 0), new Vector2(x, size.Y), 1.0f * sc, Grid2());
    for (float y = 0; y <= size.Y; y += 128f * sc)
        Line(ref f, new Vector2(0, y), new Vector2(size.X, y), 1.0f * sc, Grid2());
}

void HatchFullH(ref MySpriteDrawFrame f, Vector2 size, float y, float t, Color col)
{
    if (y <= 1f || y >= size.Y - 1f) return;
    Line(ref f, new Vector2(0, y), new Vector2(size.X, y), t, col);
}

void HatchFullV(ref MySpriteDrawFrame f, Vector2 size, float x, float t, Color col)
{
    if (x <= 1f || x >= size.X - 1f) return;
    Line(ref f, new Vector2(x, 0), new Vector2(x, size.Y), t, col);
}

void HatchSeg(ref MySpriteDrawFrame f, Vector2 size, float x1, float x2, float y, float t, Color col)
{
    if (y <= 1f || y >= size.Y - 1f) return;
    x1 = ClampF(x1, 0f, size.X);
    x2 = ClampF(x2, 0f, size.X);
    if (x2 - x1 > 3f) Line(ref f, new Vector2(x1, y), new Vector2(x2, y), t, col);
}

void HatchVSeg(ref MySpriteDrawFrame f, Vector2 size, float x, float y1, float y2, float t, Color col)
{
    if (x <= 1f || x >= size.X - 1f) return;
    y1 = ClampF(y1, 0f, size.Y);
    y2 = ClampF(y2, 0f, size.Y);
    if (y2 - y1 > 3f) Line(ref f, new Vector2(x, y1), new Vector2(x, y2), t, col);
}

float ClampF(float v, float lo, float hi)
{
    if (v < lo) return lo;
    if (v > hi) return hi;
    return v;
}

void DrawBg(ref MySpriteDrawFrame f, Vector2 size, float sc)
{
    for (float x = 0; x <= size.X; x += 32f * sc)
        Line(ref f, new Vector2(x, 0), new Vector2(x, size.Y), 0.55f * sc, Grid());
    for (float y = 0; y <= size.Y; y += 32f * sc)
        Line(ref f, new Vector2(0, y), new Vector2(size.X, y), 0.55f * sc, Grid());

    for (float x = 0; x <= size.X; x += 128f * sc)
        Line(ref f, new Vector2(x, 0), new Vector2(x, size.Y), 1.0f * sc, Grid2());
    for (float y = 0; y <= size.Y; y += 128f * sc)
        Line(ref f, new Vector2(0, y), new Vector2(size.X, y), 1.0f * sc, Grid2());
}

void BadgeButton(ref MySpriteDrawFrame f, Vector2 c, float sc, bool live)
{
    Vector2 outer = new Vector2(286, 286) * sc;
    Vector2 inner = new Vector2(274, 274) * sc;
    float cutOuter = 16f * sc;
    float cutInner = 12f * sc;

    Color outerColor = Cyan();
    Color glowColor = new Color(82, 205, 245, 0);
    Color innerLine = new Color(38, 115, 145, 120);
    float outerW = 1.1f * sc;
    float innerW = 0.85f * sc;

    if (live)
    {
        float pulse = 0.5f + 0.5f * (float)Math.Sin(anim * 0.32f);
        byte aOuter = (byte)(165 + 90 * pulse);
        byte aGlow = (byte)(55 + 120 * pulse);

        byte innerA = (byte)(70 + 185 * pulse);
        byte innerBlue = (byte)(90 + 130 * pulse);
        byte innerGreen = (byte)(145 + 107 * pulse);
        byte innerRed = (byte)(32 + 188 * pulse);

        outerColor = new Color(82, 205, 245, aOuter);
        glowColor = new Color(82, 205, 245, aGlow);
        innerLine = new Color(innerRed, innerGreen, innerBlue, innerA);
        outerW = (1.6f + 1.2f * pulse) * sc;
        innerW = (0.75f + 1.25f * pulse) * sc;

        BeveledFill(ref f, c, outer + new Vector2(24, 24) * sc, cutOuter + 5f * sc, new Color(82, 205, 245, (byte)(32 + 70 * pulse)));
        BeveledFill(ref f, c, outer + new Vector2(14, 14) * sc, cutOuter + 3f * sc, glowColor);
    }

    BeveledFill(ref f, c + new Vector2(4, 6) * sc, outer, cutOuter, Shadow());
    BeveledFill(ref f, c, outer, cutOuter, outerColor);
    BeveledFill(ref f, c, inner, cutInner, Panel());

    BeveledOutline(ref f, c, new Vector2(286, 286) * sc, 16f * sc, outerColor, outerW);
    BeveledOutline(ref f, c, new Vector2(254, 254) * sc, 10f * sc, innerLine, innerW);

    DrawCornerTick(ref f, c, sc, 0);
    DrawCornerTick(ref f, c, sc, 1);
    DrawCornerTick(ref f, c, sc, 2);
    DrawCornerTick(ref f, c, sc, 3);
}

void DrawCornerTick(ref MySpriteDrawFrame f, Vector2 c, float sc, int corner)
{
    float d = 132f * sc;
    float l = 18f * sc;
    float w = 1.45f * sc;
    Color col = new Color(220, 252, 255, 215);

    if (corner == 0)
    {
        Line(ref f, c + new Vector2(-d, -d + l), c + new Vector2(-d, -d), w, col);
        Line(ref f, c + new Vector2(-d, -d), c + new Vector2(-d + l, -d), w, col);
    }
    else if (corner == 1)
    {
        Line(ref f, c + new Vector2(d - l, -d), c + new Vector2(d, -d), w, col);
        Line(ref f, c + new Vector2(d, -d), c + new Vector2(d, -d + l), w, col);
    }
    else if (corner == 2)
    {
        Line(ref f, c + new Vector2(d - l, d), c + new Vector2(d, d), w, col);
        Line(ref f, c + new Vector2(d, d - l), c + new Vector2(d, d), w, col);
    }
    else
    {
        Line(ref f, c + new Vector2(-d, d - l), c + new Vector2(-d, d), w, col);
        Line(ref f, c + new Vector2(-d, d), c + new Vector2(-d + l, d), w, col);
    }
}

void BeveledFill(ref MySpriteDrawFrame f, Vector2 c, Vector2 size, float cut, Color col)
{
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

void DrawPressBlade(ref MySpriteDrawFrame f, Vector2 c, float sc)
{
    Vector2 o = c + new Vector2(0, -2) * sc;
    float s = sc * 0.70f;
    FillTri(ref f, o + new Vector2(0,-170)*s, o + new Vector2(-42,2)*s, o + new Vector2(42,2)*s, Pale(), .65f*s);
    FillTri(ref f, o + new Vector2(-3,-158)*s, o + new Vector2(-36,0)*s, o + new Vector2(-18,-22)*s, Cyan2(), .75f*s);
    FillTri(ref f, o + new Vector2(-13,-130)*s, o + new Vector2(-27,-24)*s, o + new Vector2(-17,-29)*s, BlueDark(), .85f*s);
    FillTri(ref f, o + new Vector2(5,-160)*s, o + new Vector2(18,-24)*s, o + new Vector2(36,0)*s, new Color(244,255,255), .85f*s);

    FillTri(ref f, o + new Vector2(-42,2)*s, o + new Vector2(-24,-75)*s, o + new Vector2(-8,16)*s, Cyan(), .65f*s);
    FillTri(ref f, o + new Vector2(42,2)*s, o + new Vector2(24,-75)*s, o + new Vector2(8,16)*s, Pale(), .65f*s);
    FillTri(ref f, o + new Vector2(0,-102)*s, o + new Vector2(-5,14)*s, o + new Vector2(5,14)*s, Ink(), .65f*s);
    FillQuad(ref f, o + new Vector2(-3,8)*s, o + new Vector2(3,8)*s, o + new Vector2(3,35)*s, o + new Vector2(-3,35)*s, Ink(), .65f*s);
    float y = 42f, w = 69f;
    FillQuad(ref f, o + new Vector2(-22,20)*s, o + new Vector2(22,20)*s, o + new Vector2(16,38)*s, o + new Vector2(-16,38)*s, Pale(), .75f*s);
    FillQuad(ref f, o + new Vector2(-w,y)*s, o + new Vector2(w,y)*s, o + new Vector2(w-24,y+13)*s, o + new Vector2(-w+24,y+13)*s, Pale(), .75f*s);
    FillTri(ref f, o + new Vector2(-w,y)*s, o + new Vector2(-w-18,y+7)*s, o + new Vector2(-w+16,y+13)*s, Pale(), .75f*s);
    FillTri(ref f, o + new Vector2(w,y)*s, o + new Vector2(w+18,y+7)*s, o + new Vector2(w-16,y+13)*s, Pale(), .75f*s);
    FillQuad(ref f, o + new Vector2(-w+16,y+13)*s, o + new Vector2(w-16,y+13)*s, o + new Vector2(w-30,y+22)*s, o + new Vector2(-w+30,y+22)*s, Cyan2(), .75f*s);
    float gripBottom = 138f, point = 174f;
    FillQuad(ref f, o + new Vector2(-9,62)*s, o + new Vector2(9,62)*s, o + new Vector2(8,gripBottom)*s, o + new Vector2(-8,gripBottom)*s, Pale(), .75f*s);
    FillQuad(ref f, o + new Vector2(-9,62)*s, o + new Vector2(-2,62)*s, o + new Vector2(-2,gripBottom)*s, o + new Vector2(-8,gripBottom)*s, Cyan2(), .75f*s);
    FillTri(ref f, o + new Vector2(0,point)*s, o + new Vector2(-12,gripBottom)*s, o + new Vector2(12,gripBottom)*s, Pale(), .75f*s);

    Line(ref f, o + new Vector2(-12,80)*s, o + new Vector2(12,80)*s, 1.25f*s, Ink());
    Line(ref f, o + new Vector2(-12,98)*s, o + new Vector2(12,98)*s, 1.25f*s, Ink());
    Line(ref f, o + new Vector2(-12,116)*s, o + new Vector2(12,116)*s, 1.25f*s, Ink());
    Line(ref f, o + new Vector2(-12,134)*s, o + new Vector2(12,134)*s, 1.25f*s, Ink());
}

void DrawWhiteShield(ref MySpriteDrawFrame f, Vector2 c, float sc)
{
    float targetW = 189f * sc;
    float targetH = 220f * sc;
    float cell = Math.Min(targetW / shieldW, targetH / shieldH);
    Vector2 origin = c - new Vector2(shieldW * cell * 0.5f, shieldH * cell * 0.5f) + new Vector2(0, -2f * sc);

    for (int y = 0; y < SHIELD.Length; y++)
    {
        string row = SHIELD[y];
        if (row.Length == 0) continue;

        int pos = 0;
        while (pos < row.Length)
        {
            int colon = row.IndexOf(':', pos);
            if (colon < 0) break;
            int comma = row.IndexOf(',', colon + 1);
            if (comma < 0) comma = row.Length;

            int x = ParseInt(row, pos, colon - 1);
            int len = ParseInt(row, colon + 1, comma - 1);

            Vector2 rectPos = origin + new Vector2((x + len * 0.5f) * cell, (y + 0.5f) * cell);
            Vector2 rectSize = new Vector2(len * cell + 0.15f, cell + 0.15f);

            Rect(ref f, rectPos, rectSize, Pale(), true, 1f, 0f);

            pos = comma + 1;
        }
    }
}

void DrawReticleGlass(ref MySpriteDrawFrame f, Vector2 c, float sc)
{
    float s = sc * .9f;
    Vector2 o = c + new Vector2(0, -6) * sc;

    Ring(ref f, o, 92f*s, Pale(), 5f*s);
    Ring(ref f, o, 61f*s, Cyan(), 2f*s);
    Ring(ref f, o, 18f*s, Pale(), 3f*s);

    Line(ref f, o + new Vector2(-122,0)*s, o + new Vector2(-42,0)*s, 3f*s, Pale());
    Line(ref f, o + new Vector2(42,0)*s, o + new Vector2(122,0)*s, 3f*s, Pale());
    Line(ref f, o + new Vector2(0,-122)*s, o + new Vector2(0,-42)*s, 3f*s, Pale());
    Line(ref f, o + new Vector2(0,42)*s, o + new Vector2(0,122)*s, 3f*s, Pale());

    Line(ref f, o + new Vector2(-74,-74)*s, o + new Vector2(-51,-51)*s, 2f*s, Cyan2());
    Line(ref f, o + new Vector2(74,-74)*s, o + new Vector2(51,-51)*s, 2f*s, Cyan2());
    Line(ref f, o + new Vector2(-74,74)*s, o + new Vector2(-51,51)*s, 2f*s, Cyan2());
    Line(ref f, o + new Vector2(74,74)*s, o + new Vector2(51,51)*s, 2f*s, Cyan2());

    Dot(ref f, o, 7f*s, Cyan());
}

void DrawBinocularTrace(ref MySpriteDrawFrame f, Vector2 c, float sc)
{
    string[] data = B0;
    int w = binocW[0];
    int h = binocH[0];
    float targetW = 230f * sc;
    float targetH = 193f * sc;
    float cell = Math.Min(targetW / w, targetH / h);

    Vector2 origin = c - new Vector2(w * cell * 0.5f, h * cell * 0.5f) + new Vector2(0, 0f * sc);

    for (int y = 0; y < data.Length; y++)
    {
        string row = data[y];
        if (row.Length == 0) continue;

        int pos = 0;
        while (pos < row.Length)
        {
            int colon = row.IndexOf(':', pos);
            if (colon < 0) break;
            int comma = row.IndexOf(',', colon + 1);
            if (comma < 0) comma = row.Length;

            int x = ParseInt(row, pos, colon - 1);
            int len = ParseInt(row, colon + 1, comma - 1);

            Vector2 rectPos = origin + new Vector2((x + len * 0.5f) * cell, (y + 0.5f) * cell);
            Vector2 rectSize = new Vector2(len * cell + 0.15f, cell + 0.15f);

            Rect(ref f, rectPos, rectSize, Pale(), true, 1f, 0f);

            pos = comma + 1;
        }
    }
}

void DrawWithdrawTrace(ref MySpriteDrawFrame f, Vector2 c, float sc)
{
    string[] data = WITHDRAW;
    int w = withdrawW;
    int h = withdrawH;
    float targetW = 183f * sc;
    float targetH = 183f * sc;
    float cell = Math.Min(targetW / w, targetH / h);

    Vector2 origin = c - new Vector2(w * cell * 0.5f, h * cell * 0.5f) + new Vector2(-10f * sc, -3f * sc);

    for (int y = 0; y < data.Length; y++)
    {
        string row = data[y];
        if (row.Length == 0) continue;

        int pos = 0;
        while (pos < row.Length)
        {
            int colon = row.IndexOf(':', pos);
            if (colon < 0) break;
            int comma = row.IndexOf(',', colon + 1);
            if (comma < 0) comma = row.Length;

            int x = ParseInt(row, pos, colon - 1);
            int len = ParseInt(row, colon + 1, comma - 1);

            Vector2 rectPos = origin + new Vector2((x + len * 0.5f) * cell, (y + 0.5f) * cell);
            Vector2 rectSize = new Vector2(len * cell + 0.15f, cell + 0.15f);

            Rect(ref f, rectPos, rectSize, Pale(), true, 1f, 0f);

            pos = comma + 1;
        }
    }
}

void DrawCrosshair(ref MySpriteDrawFrame f, Vector2 c, float sc)
{
    float s = sc * .92f;
    Vector2 o = c + new Vector2(0, -4) * sc;

    Ring(ref f, o, 103f*s, Pale(), 4f*s);
    Ring(ref f, o, 66f*s, Cyan(), 2f*s);
    Ring(ref f, o, 26f*s, Pale(), 3f*s);
    Line(ref f, o + new Vector2(-134,0)*s, o + new Vector2(-82,0)*s, 4f*s, Pale());
    Line(ref f, o + new Vector2(-46,0)*s, o + new Vector2(-16,0)*s, 2f*s, Cyan());
    Line(ref f, o + new Vector2(16,0)*s, o + new Vector2(46,0)*s, 2f*s, Cyan());
    Line(ref f, o + new Vector2(82,0)*s, o + new Vector2(134,0)*s, 4f*s, Pale());

    Line(ref f, o + new Vector2(0,-134)*s, o + new Vector2(0,-82)*s, 4f*s, Pale());
    Line(ref f, o + new Vector2(0,-46)*s, o + new Vector2(0,-16)*s, 2f*s, Cyan());
    Line(ref f, o + new Vector2(0,16)*s, o + new Vector2(0,46)*s, 2f*s, Cyan());
    Line(ref f, o + new Vector2(0,82)*s, o + new Vector2(0,134)*s, 4f*s, Pale());
    Rect(ref f, o + new Vector2(0,-104)*s, new Vector2(12,33)*s, Pale(), true, 1f, 0f);
    Rect(ref f, o + new Vector2(0,104)*s, new Vector2(12,33)*s, Pale(), true, 1f, 0f);
    Rect(ref f, o + new Vector2(-104,0)*s, new Vector2(33,12)*s, Pale(), true, 1f, 0f);
    Rect(ref f, o + new Vector2(104,0)*s, new Vector2(33,12)*s, Pale(), true, 1f, 0f);

    Dot(ref f, o, 6f*s, Cyan2());
}

int ParseInt(string s, int a, int b)
{
    int v = 0;
    for (int i = a; i <= b && i < s.Length; i++)
    {
        char ch = s[i];
        if (ch >= '0' && ch <= '9') v = v * 10 + (int)(ch - '0');
    }
    return v;
}

void Text(ref MySpriteDrawFrame f, string text, Vector2 pos, Color col, float scale, TextAlignment align)
{
    MySprite sp = MySprite.CreateText(text, "Debug", col, scale, align);
    sp.Position = pos;
    f.Add(sp);
}

void Dot(ref MySpriteDrawFrame f, Vector2 p, float r, Color col)
{
    f.Add(new MySprite(SpriteType.TEXTURE, "Circle", p, new Vector2(r * 2f, r * 2f), col));
}

void Ring(ref MySpriteDrawFrame f, Vector2 c, float r, Color col, float w)
{
    int seg = 96;
    Vector2 last = c + new Vector2(r, 0);
    for (int i = 1; i <= seg; i++)
    {
        double a = Math.PI * 2.0 * i / seg;
        Vector2 p = c + new Vector2((float)Math.Cos(a) * r, (float)Math.Sin(a) * r);
        Line(ref f, last, p, w, col);
        last = p;
    }
}

void Rect(ref MySpriteDrawFrame f, Vector2 c, Vector2 sz, Color col, bool fill, float w, float rot)
{
    if (fill)
    {
        f.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", c, sz, col, null, TextAlignment.CENTER, rot));
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

void Line(ref MySpriteDrawFrame f, Vector2 a, Vector2 b, float w, Color col)
{
    Vector2 mid = (a + b) * 0.5f;
    Vector2 d = b - a;
    float len = d.Length();
    if (len < 0.01f) return;

    float rot = (float)Math.Atan2(d.Y, d.X);
    f.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", mid, new Vector2(len, w), col, null, TextAlignment.CENTER, rot));
}

void FillQuad(ref MySpriteDrawFrame f, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col, float step)
{
    FillTri(ref f, a, b, c, col, step);
    FillTri(ref f, a, c, d, col, step);
}

void FillTri(ref MySpriteDrawFrame f, Vector2 a, Vector2 b, Vector2 c, Color col, float step)
{
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
    if ((y < p1.Y && y < p2.Y) || (y > p1.Y && y > p2.Y)) return;
    if (Math.Abs(p2.Y - p1.Y) < 0.001f) return;

    float t = (y - p1.Y) / (p2.Y - p1.Y);
    if (t < 0f || t > 1f) return;

    float x = p1.X + (p2.X - p1.X) * t;

    if (hits == 0) x1 = x;
    else if (hits == 1) x2 = x;
    hits++;
}
