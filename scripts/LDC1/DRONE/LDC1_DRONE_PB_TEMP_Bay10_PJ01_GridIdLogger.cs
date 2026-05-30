// LDC1 DRONE PB TEMP - BAY10 PJ01 GRID ID LOGGER
// Expendable diagnostic script. Do not treat as a Drone PB version.
// Purpose: log PB/block/grid identity across merge/unmerge while connector remains connected.
// Install in the Bay10 PJ01 test article drone PB, toggle merge state, then restore normal Drone PB.

const string TEST_LABEL = "BAY10 PJ01 TEMP GRID ID LOGGER";
const string LOG_BEGIN = "# TEMP_GRID_ID_LOG_BEGIN";
const string LOG_END = "# TEMP_GRID_ID_LOG_END";
const int MAX_LOG_ENTRIES = 24;

List<IMyShipMergeBlock> _merges = new List<IMyShipMergeBlock>();
List<IMyShipConnector> _connectors = new List<IMyShipConnector>();
List<string> _entries = new List<string>();
StringBuilder _sb = new StringBuilder(8192);

int _tick = 0;
string _lastSig = "";
string _lastReason = "BOOT";

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    ReadOldLog();
    Capture("BOOT");
}

public void Save()
{
    WriteLog();
}

public void Main(string argument, UpdateType updateSource)
{
    string arg = argument == null ? "" : argument.Trim().ToUpperInvariant();
    _tick++;

    if (arg == "CLEAR")
    {
        _entries.Clear();
        _lastSig = "";
        Capture("CLEAR+SNAP");
        return;
    }

    if (arg == "LOG" || arg == "SNAP" || arg == "STATUS" || arg == "SCAN")
    {
        Capture(arg.Length > 0 ? arg : "MANUAL");
        return;
    }

    string sig = BuildSignature();
    if (sig != _lastSig)
    {
        _lastSig = sig;
        Capture("CHANGE");
    }

    EchoStatus();
}

void Capture(string reason)
{
    _lastReason = reason;
    string sig = BuildSignature();
    _lastSig = sig;

    _sb.Clear();
    _sb.AppendLine("--- " + reason + " T" + _tick.ToString() + " ---");
    AppendBlockIdentity(Me, "ME/PB");
    AppendGridIdentity(Me.CubeGrid, "ME.GRID");
    AppendMergeSummary();
    AppendConnectorSummary();
    _sb.AppendLine("Sig=" + sig);

    _entries.Add(_sb.ToString().TrimEnd());
    while (_entries.Count > MAX_LOG_ENTRIES) _entries.RemoveAt(0);
    WriteLog();
    EchoStatus();
}

string BuildSignature()
{
    int mergeOn = 0, mergeConnected = 0, connConnected = 0, connReady = 0;
    ScanMergeConnector();
    for (int i = 0; i < _merges.Count; i++)
    {
        IMyShipMergeBlock m = _merges[i];
        if (m == null || m.CubeGrid != Me.CubeGrid) continue;
        if (m.Enabled) mergeOn++;
        if (m.IsConnected) mergeConnected++;
    }
    for (int i = 0; i < _connectors.Count; i++)
    {
        IMyShipConnector c = _connectors[i];
        if (c == null || c.CubeGrid != Me.CubeGrid) continue;
        if (c.Status == MyShipConnectorStatus.Connected) connConnected++;
        else if (c.Status == MyShipConnectorStatus.Connectable) connReady++;
    }
    return "PB=" + Me.EntityId.ToString() +
        "|Grid=" + Me.CubeGrid.EntityId.ToString() +
        "|GName=" + Safe(Me.CubeGrid.CustomName) +
        "|MOn=" + mergeOn.ToString() +
        "|MC=" + mergeConnected.ToString() +
        "|CC=" + connConnected.ToString() +
        "|CR=" + connReady.ToString();
}

void ScanMergeConnector()
{
    _merges.Clear();
    _connectors.Clear();
    GridTerminalSystem.GetBlocksOfType<IMyShipMergeBlock>(_merges);
    GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(_connectors);
}

void AppendBlockIdentity(IMyTerminalBlock b, string label)
{
    if (b == null)
    {
        _sb.AppendLine(label + "=NULL");
        return;
    }
    IMyFunctionalBlock f = b as IMyFunctionalBlock;
    _sb.AppendLine("[" + label + "]");
    _sb.AppendLine("EntityId=" + b.EntityId.ToString());
    _sb.AppendLine("Name=" + Safe(b.CustomName));
    _sb.AppendLine("DefName=" + Safe(b.DefinitionDisplayNameText));
    _sb.AppendLine("TypeId=" + Safe(b.BlockDefinition.TypeIdString));
    _sb.AppendLine("Subtype=" + Safe(b.BlockDefinition.SubtypeName));
    _sb.AppendLine("GridId=" + (b.CubeGrid != null ? b.CubeGrid.EntityId.ToString() : "0"));
    _sb.AppendLine("GridName=" + (b.CubeGrid != null ? Safe(b.CubeGrid.CustomName) : "-"));
    _sb.AppendLine("Pos=" + V3I(b.Position) + " Min=" + V3I(b.Min) + " Max=" + V3I(b.Max));
    Vector3D wp = b.GetPosition();
    _sb.AppendLine("World=" + F(wp.X) + "," + F(wp.Y) + "," + F(wp.Z));
    _sb.AppendLine("Functional=" + YN(b.IsFunctional) + " Working=" + YN(b.IsWorking) + " Enabled=" + (f != null ? YN(f.Enabled) : "-"));
}

void AppendGridIdentity(IMyCubeGrid g, string label)
{
    _sb.AppendLine("[" + label + "]");
    if (g == null)
    {
        _sb.AppendLine("Grid=NULL");
        return;
    }
    _sb.AppendLine("EntityId=" + g.EntityId.ToString());
    _sb.AppendLine("Name=" + Safe(g.CustomName));
    _sb.AppendLine("Static=" + YN(g.IsStatic));
    _sb.AppendLine("GridSize=" + g.GridSize.ToString("0.###"));
    _sb.AppendLine("GridSizeEnum=" + g.GridSizeEnum.ToString());
}

void AppendMergeSummary()
{
    ScanMergeConnector();
    _sb.AppendLine("[LOCAL MERGES]");
    int n = 0;
    for (int i = 0; i < _merges.Count; i++)
    {
        IMyShipMergeBlock m = _merges[i];
        if (m == null || m.CubeGrid != Me.CubeGrid) continue;
        n++;
        _sb.AppendLine("M" + n.ToString("00") + " Id=" + m.EntityId.ToString());
        _sb.AppendLine("  Name=" + Safe(m.CustomName));
        _sb.AppendLine("  GridId=" + m.CubeGrid.EntityId.ToString() + " GridName=" + Safe(m.CubeGrid.CustomName));
        _sb.AppendLine("  On=" + YN(m.Enabled) + " Connected=" + YN(m.IsConnected) + " Pos=" + V3I(m.Position));
    }
    if (n == 0) _sb.AppendLine("None on current PB grid");
}

void AppendConnectorSummary()
{
    _sb.AppendLine("[LOCAL CONNECTORS]");
    int n = 0;
    for (int i = 0; i < _connectors.Count; i++)
    {
        IMyShipConnector c = _connectors[i];
        if (c == null || c.CubeGrid != Me.CubeGrid) continue;
        n++;
        _sb.AppendLine("C" + n.ToString("00") + " Id=" + c.EntityId.ToString());
        _sb.AppendLine("  Name=" + Safe(c.CustomName));
        _sb.AppendLine("  GridId=" + c.CubeGrid.EntityId.ToString() + " GridName=" + Safe(c.CubeGrid.CustomName));
        _sb.AppendLine("  On=" + YN(c.Enabled) + " Status=" + c.Status.ToString() + " Pos=" + V3I(c.Position));
        if (c.OtherConnector != null)
        {
            _sb.AppendLine("  OtherId=" + c.OtherConnector.EntityId.ToString());
            _sb.AppendLine("  OtherGridId=" + c.OtherConnector.CubeGrid.EntityId.ToString());
            _sb.AppendLine("  OtherGridName=" + Safe(c.OtherConnector.CubeGrid.CustomName));
            _sb.AppendLine("  OtherName=" + Safe(c.OtherConnector.CustomName));
        }
    }
    if (n == 0) _sb.AppendLine("None on current PB grid");
}

void ReadOldLog()
{
    _entries.Clear();
    string block = Extract(Me.CustomData, LOG_BEGIN, LOG_END);
    if (block.Length == 0) return;
    string[] parts = block.Replace("\r", "").Split(new string[] { "\n--- " }, StringSplitOptions.None);
    for (int i = 0; i < parts.Length; i++)
    {
        string p = parts[i].Trim();
        if (p.Length == 0) continue;
        if (i > 0) p = "--- " + p;
        _entries.Add(p);
    }
    while (_entries.Count > MAX_LOG_ENTRIES) _entries.RemoveAt(0);
}

void WriteLog()
{
    _sb.Clear();
    _sb.AppendLine(LOG_BEGIN);
    _sb.AppendLine("Label=" + TEST_LABEL);
    _sb.AppendLine("Note=Temporary expendable Drone PB logger. Restore normal Drone PB after test.");
    _sb.AppendLine("Commands=LOG/CLEAR/STATUS");
    _sb.AppendLine("Entries=" + _entries.Count.ToString());
    _sb.AppendLine("");
    for (int i = 0; i < _entries.Count; i++)
    {
        _sb.AppendLine(_entries[i]);
        _sb.AppendLine("");
    }
    _sb.AppendLine(LOG_END);
    Me.CustomData = Replace(Me.CustomData, LOG_BEGIN, LOG_END, _sb.ToString());
}

void EchoStatus()
{
    Echo("TEMP DRONE GRID ID LOGGER");
    Echo(TEST_LABEL);
    Echo("Tick " + _tick.ToString());
    Echo("Reason " + _lastReason);
    Echo("PB " + Me.EntityId.ToString());
    Echo("Grid " + Me.CubeGrid.EntityId.ToString());
    Echo("Name " + Safe(Me.CubeGrid.CustomName));
    Echo("Entries " + _entries.Count.ToString());
    Echo("Cmds: LOG | CLEAR | STATUS");
}

string Extract(string text, string begin, string end)
{
    if (text == null) return "";
    int a = text.IndexOf(begin, StringComparison.OrdinalIgnoreCase);
    if (a < 0) return "";
    a += begin.Length;
    int b = text.IndexOf(end, a, StringComparison.OrdinalIgnoreCase);
    if (b < 0) return "";
    return text.Substring(a, b - a).Trim();
}

string Replace(string text, string begin, string end, string block)
{
    if (text == null) text = "";
    int a = text.IndexOf(begin, StringComparison.OrdinalIgnoreCase);
    int b = text.IndexOf(end, StringComparison.OrdinalIgnoreCase);
    if (a >= 0 && b > a)
    {
        b += end.Length;
        text = text.Remove(a, b - a).Trim();
    }
    string clean = block.Trim();
    if (text.Length == 0) return clean;
    return text.TrimEnd() + "\n\n" + clean;
}

string V3I(Vector3I v)
{
    return v.X.ToString() + "," + v.Y.ToString() + "," + v.Z.ToString();
}

string F(double v)
{
    return v.ToString("0.000");
}

string YN(bool v)
{
    return v ? "YES" : "NO";
}

string Safe(string s)
{
    if (s == null) return "";
    return s.Replace("\r", " ").Replace("\n", " ").Replace("|", "/");
}
