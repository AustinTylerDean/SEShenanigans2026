// MB1 AUTO DOOR CLOSER
// Controls visible IMyDoor blocks with [MB1] in the name.
// Works across subgrids/small grids/merged constructs ONLY by tag.
// Excludes [NOADC] and Airtight Hangar Door-style blocks.
// Uses explicit CloseDoor(), never toggle.

const string SHIP_TAG = "[MB1]";
const string EXCLUDE_TAG = "[NOADC]";

const double CLOSE_DELAY_SECONDS = 0.5;
const int SCAN_EVERY_RUNS = 30;     // Update10: about 5 seconds
const int MAX_CLOSES_PER_RUN = 25;

List<IMyDoor> doors = new List<IMyDoor>();
Dictionary<long, double> openSince = new Dictionary<long, double>();

int run = 0;
double clock = 0;
bool paused = false;

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    ScanDoors();
}

public void Main(string argument, UpdateType updateSource)
{
    argument = (argument ?? "").Trim().ToLower();

    if (argument == "rescan")
    {
        ScanDoors();
    }
    else if (argument == "pause")
    {
        paused = true;
    }
    else if (argument == "resume")
    {
        paused = false;
    }
    else if (argument == "toggle")
    {
        paused = !paused;
    }

    clock += Runtime.TimeSinceLastRun.TotalSeconds;
    run++;

    if (run % SCAN_EVERY_RUNS == 0)
        ScanDoors();

    if (!paused)
        ProcessDoors();

    EchoStatus();
}

void ScanDoors()
{
    doors.Clear();

    var found = new List<IMyDoor>();
    GridTerminalSystem.GetBlocksOfType<IMyDoor>(found);

    for (int i = 0; i < found.Count; i++)
    {
        IMyDoor d = found[i];
        if (d == null) continue;

        string n = d.CustomName;

        if (n.IndexOf(SHIP_TAG, StringComparison.OrdinalIgnoreCase) < 0) continue;
        if (n.IndexOf(EXCLUDE_TAG, StringComparison.OrdinalIgnoreCase) >= 0) continue;
        if (IsHangarStyleDoor(d)) continue;

        doors.Add(d);
    }

    var remove = new List<long>();

    foreach (var kv in openSince)
    {
        bool stillFound = false;

        for (int i = 0; i < doors.Count; i++)
        {
            if (doors[i].EntityId == kv.Key)
            {
                stillFound = true;
                break;
            }
        }

        if (!stillFound)
            remove.Add(kv.Key);
    }

    for (int i = 0; i < remove.Count; i++)
        openSince.Remove(remove[i]);
}

bool IsHangarStyleDoor(IMyDoor d)
{
    string display = d.DefinitionDisplayNameText ?? "";
    string name = d.CustomName ?? "";

    // Excludes vanilla Airtight Hangar Doors.
    // Does NOT exclude Gate Doors or Large Round Doors.
    if (display.IndexOf("Hangar", StringComparison.OrdinalIgnoreCase) >= 0)
        return true;

    // Fallback in case localized/custom display text is weird.
    // Only excludes if the block's own name says Hangar.
    if (name.IndexOf("Hangar", StringComparison.OrdinalIgnoreCase) >= 0)
        return true;

    return false;
}

void ProcessDoors()
{
    int closes = 0;

    for (int i = 0; i < doors.Count; i++)
    {
        IMyDoor d = doors[i];
        if (d == null) continue;

        long id = d.EntityId;

        if (!d.IsFunctional)
        {
            openSince.Remove(id);
            continue;
        }

        if (d.Status == DoorStatus.Open)
        {
            double t;

            if (!openSince.TryGetValue(id, out t))
            {
                openSince[id] = clock;
                continue;
            }

            if (clock - t >= CLOSE_DELAY_SECONDS)
            {
                if (closes < MAX_CLOSES_PER_RUN)
                {
                    d.CloseDoor();
                    closes++;
                }
            }
        }
        else if (d.Status == DoorStatus.Closed)
        {
            openSince.Remove(id);
        }
        else
        {
            // Opening / Closing:
            // Do nothing. This protects gate/round doors from being interrupted mid-animation.
        }
    }
}

void EchoStatus()
{
    int open = 0;
    int opening = 0;
    int closing = 0;
    int closed = 0;
    int broken = 0;

    for (int i = 0; i < doors.Count; i++)
    {
        IMyDoor d = doors[i];
        if (d == null) continue;

        if (!d.IsFunctional)
        {
            broken++;
            continue;
        }

        if (d.Status == DoorStatus.Open) open++;
        else if (d.Status == DoorStatus.Opening) opening++;
        else if (d.Status == DoorStatus.Closing) closing++;
        else if (d.Status == DoorStatus.Closed) closed++;
    }

    Echo("MB1 AUTO DOOR CLOSER");
    Echo("====================");
    Echo("State: " + (paused ? "PAUSED" : "ACTIVE"));
    Echo("Tag: " + SHIP_TAG);
    Echo("Doors controlled: " + doors.Count);
    Echo("");
    Echo("Open: " + open);
    Echo("Opening: " + opening);
    Echo("Closing: " + closing);
    Echo("Closed: " + closed);
    Echo("Broken: " + broken);
    Echo("");
    Echo("Delay: " + CLOSE_DELAY_SECONDS.ToString("0.0") + "s");
    Echo("Excluded: Hangar, " + EXCLUDE_TAG);
    Echo("");
    Echo("Args:");
    Echo("rescan / pause / resume / toggle");
}
