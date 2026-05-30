# SE Active Working Memory V010 — LDC1 WSS Donor-UI Reset and OB1 IMS Gravel/Stone Fix

## Current active patch set

### LDC1 WSS

Current handoff script:

- `LDC1_WSS_V010_MarkToggleRaySanity.cs`

Current purpose:

- new LDC1 WSS helm/control-seat front-end;
- cockpit surface ownership and external WSS LCD handling;
- boresight waypoint range/mark/FSU command packet foundation;
- donor-transplanted MB1 WSS tactical background/focus card direction;
- donor-transplanted MB1 IMS hotbar card direction;
- `MARK` now toggles open/locked;
- locked waypoint/ray state is reloaded/recomputed after recompile.

### OB1 IMS PB2

Current handoff script:

- `OB1_IMS_PB2_V075_GenericOreFeedAvailability.cs`

Current purpose:

- permanent generic optimized-refinery ore availability fix;
- Stone input demand maps to Gravel refined target/current;
- refinery feed scans the actual managed ore pool rather than relying on the old misleading per-ore soft count;
- temp diagnostics removed.

Do **not** use/load:

- `OB1_IMS_PB2_V071_GravelStoneHold.cs` — speculative and invalid path.
- `OB1_IMS_PB2_V072_TEMP_*` / `V073_TEMP_*` / `V074*_TEMP_*` as permanent scripts — diagnostics only.

## Hard workflow correction: donor code is mandatory

This thread proved that “approximate an existing working UI” is not acceptable. For this project:

- a user-provided working reference means donor code must be transplanted first;
- no remake, no approximate helper math, no “same idea” implementations;
- donor function body first, rename/adapt second;
- if a function exists in MB1 IMS/WSS/DPS/DCS/ARGOS with equal intent, port it directly unless there is a specific reason it cannot apply;
- every patch must include a Reuse Gate.

Required patch section:

```text
REUSE GATE
Feature:
Classification: PROVEN / SIMILAR / NEW
Donor file(s):
Donor function(s):
Copied function body from donor: YES/NO
Copied unchanged:
Rename-only:
Adapted:
New code:
Reason new code was necessary:
```

If `Copied function body from donor` should be YES and is not YES, the patch is invalid.

## LDC1 WSS current doctrine

### Cockpit surface mapping

Confirmed physical surface order, left to right:

```text
3, 1, 0, 2, 4
```

Meaning:

```text
far-left    = surface 3
front-left  = surface 1
center      = surface 0
front-right = surface 2
far-right   = surface 4
```

### Surface roles

```text
center / surface 0:
- active working page
- BAYS / TACTICAL / TURRETS / COMMS
- focus overlay when focus is not CENTER

front-left / surface 1:
- page selector only
- donor-style PAGES screen
- do not add bay detail helper card here

front-right / surface 2:
- selected bay helper/status visual
- mostly visual, low text

far-left / surface 3:
- decorative carrier/posture visual
- almost no text

far-right / surface 4:
- HOTBAR / HOTBAR 2 help card
- MB1 IMS donor hotbar layout
- cyan frame only when HOTBAR is focused is acceptable
```

### External WSS LCD tags

Use numbered LCDs:

```text
[LDC1] [WSS1]
[LDC1] [WSS2]
[LDC1] [WSS3]
...
```

Expected behavior:

- each LCD is independently selectable with `FOCUS`;
- selected LCD shows focus rails;
- each LCD preserves its selected page across normal scans;
- `PAGE NEXT` / `PAGE PREV` changes the page of the focused LCD;
- `NEXT` / `PREV` changes subpage/selection where that LCD page actually supports it;
- `INC` / `DEC` must not duplicate page changes on external LCDs when there is no value field to adjust.

Current external LCD pages should remain lean:

```text
BAY DETAIL
DRONE ROSTER
TACTICAL
```

The temporary/filler TURRETS LCD page should stay removed until turret status has real data.

### WSS command model

Current hotbar/control commands:

```text
FOCUS
PAGE NEXT
PAGE PREV
NEXT
PREV
INC
DEC
STEP
RANGE INC
RANGE DEC
MARK
FSU
ABORT
SCAN
STATUS
IDSCREENS
```

Current WSS packet concept:

```ini
# WSS_PACKET_BEGIN
Seq=#
Command=MARK/FSU/ABORT/NONE
CommandEvent=0/1
Mode=BORESIGHT
SelectedWave=A
RangeMeters=#
RangeStepMeters=#
Marked=0/1
MarkSeq=#
FsuSeq=#
AbortSeq=#
Waypoint.X/Y/Z=#
Source.X/Y/Z=#
Forward.X/Y/Z=#
SelectedBay=#
SelectedSlot=PJ##
# WSS_PACKET_END
```

DCS has not yet been patched to consume WSS packets. WSS is currently command/UI/packet foundation only.

### MARK behavior

Accepted change:

```text
MARK toggles waypoint lock.
OPEN -> LOCKED: capture current helm boresight and recompute waypoint.
LOCKED -> OPEN: clear locked state but preserve useful display/packet context as implemented.
FSU while unlocked may auto-lock first before sending command intent.
```

Startup/recompile behavior:

- WSS reloads mark/range/ray/waypoint data from Custom Data;
- if locked data exists, WSS recomputes the waypoint immediately from source + forward × range;
- this prevents stale world-zero display such as X=0, Y=0, Z=-5km unless that is truly the helm ray.

### Current WSS display notes

Recently fixed:

- tactical lower BORESIGHT card no longer clips bottom;
- tactical top cards were restored after V008 over-corrected the top;
- V009 finally produced acceptable TACTICAL fit;
- V010 adds mark-toggle and ray sanity without layout changes.

Still not final / future polish:

- BAYS page is functional but bland;
- occupied state should be green-dominant, empty muted gray, selected cyan-dominant;
- avoid yellow as “occupied” because it reads like UI selection/warning;
- future bay graphic could infer/represent physical bay footprint, but that is a separate design cycle;
- COMMS page needs later simplification and a clearer packet/contract visual;
- TURRETS center page needs real turret data before being useful.

## OB1 IMS Gravel/Stone investigation result

Problem observed:

- PB1 showed Gravel yellow / below minimum;
- Stone briefly fed refineries after V070;
- after changing the Gravel minimum, refineries stopped being topped off;
- Stone was physically in the correct OB1 `[ORE*]` bulk container;
- old diagnostic path reported `cargo=0 ref=0` even though Stone existed in the managed ore pool.

Important Space Engineers naming detail:

```text
Stone ore input:      MyObjectBuilder_Ore / Stone
Gravel refined output: MyObjectBuilder_Ingot / Stone  (displayed as Gravel)
```

Therefore Stone/Gravel is a real alias exception:

- ore input key = `Stone`;
- refined UI target key = `GRAVEL`;
- refined item subtype may be `Stone` even though display label is Gravel.

### V070

V070 correctly added the first necessary mapping:

```text
Stone ore demand -> Target.REFINED.GRAVEL.Min
Stone ore demand -> Current.REFINED.GRAVEL
```

This part was valid and retained.

### V071

V071 was speculative and should not be used.

Reason:

- it attempted a Gravel demand hold/memory without enough proof;
- it introduced undefined variables in the delivered file;
- it was not compile-safe.

### V072–V074A diagnostics

Diagnostics proved:

```text
POOL OREPOOL all=1M ore=1M/1 rest=0/0 other=0/0 feedSoft=0 first=[OB1] Bulk Cargo Container B 4 [ORE*]
TRC ST002 RFILL OK 20.3k [OB1] Bulk Cargo Container B 4 [ORE*] -> [OB1] Refinery 1
```

Interpretation:

- Stone was in the correct OB1 grouped ore pool;
- PB2 could move Stone from the `[ORE*]` container into a refinery;
- the old `cargo=0/feedSoft=0` path was misleading/broken;
- LDC1 connector was not the cause in the captured evidence (`fcon=0`, `fblk=0`);
- the real issue was refinery-feed availability selection not using the same managed ore pool that GROUPED storage uses.

### V075 permanent fix

V075 is the accepted direction:

```text
Optimized refinery ore selection scans actual managed cargo ore items directly once and scores physically available ore items.
```

It keeps:

- V070 Stone -> Gravel demand mapping;
- generic managed-ore availability logic;
- no connector/boundary changes;
- no Stone-only feed hack.

User reported after V075:

```text
“OK it's topping the piles off in the refineries now.”
```

Therefore V075 is current good OB1 IMS PB2 baseline.

## Shared IMS standard candidate

The V075 concept should become shared IMS standard and MB1 backport candidate:

```text
Refinery feed selection must source from the same managed ore pool that GROUPED storage uses.
```

Do not maintain separate “soft” per-ore count logic if it can disagree with the actual managed ore pool.

## Immediate next-thread priorities

1. If staying on OB1 IMS:
   - keep V075;
   - monitor Stone/Gravel until stable;
   - remove/ignore all temp diagnostics;
   - consider MB1 IMS PB2 backport if MB1 has equivalent refinery-feed path.

2. If returning to LDC1 WSS:
   - continue from V010;
   - do not remake UI donor functions;
   - next likely task is DCS consuming WSS packet or WSS display polish with donor-code reuse gate.

3. Any new patch:
   - no GitHub unless explicitly requested;
   - use recent local files/current handoff scripts;
   - apply Reuse Gate before coding;
   - provide PB budget gate and warning cleanup.
