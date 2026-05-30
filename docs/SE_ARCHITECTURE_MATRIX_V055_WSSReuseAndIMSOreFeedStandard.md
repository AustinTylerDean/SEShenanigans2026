# SE Architecture Matrix V055 — Donor UI Reuse and Managed Ore Feed Standard

## Change classification

Shared project standard update.

This update affects:

- all future UI/display work;
- LDC1 WSS current direction;
- IMS optimized refinery feed design;
- MB1 backport planning.

## Prime doctrine reinforcement

`Refine. Simplify, standardize. elegance. always.`

New hard interpretation:

```text
A working project reference is not inspiration. It is donor code.
```

If a user points to MB1 IMS/WSS, DPS, DCS, ARGOS, or another proven project script as the reference, the next patch must port the actual donor mechanics, not recreate something that looks similar.

## Donor-code reuse standard

Every code patch must pass a Reuse Gate.

Required section:

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

Rules:

- `PROVEN`: donor function body first, adaptation second.
- `SIMILAR`: donor mechanics first, with explicit differences.
- `NEW`: only when no existing project pattern applies.
- “Same idea” is not a port.
- Rebuilt helper math is not a port.
- UI scale and coordinate systems must be preserved unless specifically rejected after testing.

## UI donor hierarchy

Known donor patterns:

| Need | Preferred donor |
|---|---|
| Cockpit hotbar/help card | MB1 IMS `DrawOfficerHelp` |
| Cockpit page selector | MB1 IMS `DrawOfficerPages` |
| External LCD focus rails | MB1 IMS detail LCD focus rails |
| Center external-focus overlay / tactical backdrop | MB1 WSS PB1 background/focus overlay code |
| Focus-slot and focused-page control | MB1 WSS PB1 focus/page mechanics |
| Inventory/target officer rows | MB1 IMS officer console |
| Bay/factory status | LDC1 DPS current bay state model |
| Drone service/heartbeat status | LDC1 DCS/drone PB current packet/status model |

## LDC1 WSS authority

WSS is the LDC1 helm/control-seat tactical front-end.

WSS owns:

- control-seat display surfaces;
- external `[LDC1] [WSS#]` LCD surfaces;
- selected bay / selected slot UI intent;
- boresight waypoint/range/mark/FSU command packet;
- WSS command packet export.

WSS does not own:

- DPS bay hardware;
- DCS drone birth/service state;
- drone internal custody;
- weapons/turrets until a real WSS weapons authority page is intentionally added;
- connectors, merge blocks, AI blocks, or production hardware.

## LDC1 WSS surface contract

Confirmed cockpit physical surface order:

```text
left-to-right physical: 3, 1, 0, 2, 4
```

Surface assignment:

```text
surface 0 = center working page
surface 1 = front-left page selector only
surface 2 = front-right selected-bay/status visual
surface 3 = far-left decorative/status visual
surface 4 = far-right hotbar/help card
```

The center surface carries actual UI work.

Other cockpit screens must be low-cognitive-load surfaces:

- page selector;
- hotbar guide;
- one-state helper visuals;
- icons/status blocks.

## WSS external LCD doctrine

External WSS LCDs use numbered tags:

```text
[LDC1] [WSS1]
[LDC1] [WSS2]
[LDC1] [WSS3]
```

Rules:

- each LCD is independently selectable by `FOCUS`;
- each LCD keeps its selected page across normal scans;
- external focus must show a center-screen overlay using the MB1 WSS visual contract;
- external LCD pages must not show fake selectable highlights when commands do not affect that value;
- avoid filler pages until real subsystem data exists.

Current external LCD page set:

```text
BAY DETAIL
DRONE ROSTER
TACTICAL
```

Future turret page returns only when turret state data exists.

## IMS managed ore feed standard

V075 OB1 IMS PB2 established a shared standard:

```text
Optimized refinery feed selection must source from the same managed ore pool that GROUPED storage uses.
```

Do not let separate ore-counting paths disagree with actual managed cargo contents.

### Stone/Gravel alias standard

Stone/Gravel must be handled centrally as an alias pair:

```text
Input ore key: Stone
Refined UI key: GRAVEL
Refined item subtype: Stone
Display label: GRAVEL
```

Stone/Gravel is not a one-off production hack; it is a material alias exception that generic refinery/demand/count logic must understand.

### V075 resolution

Problem:

- Gravel demand was visible and correct;
- Stone was in OB1 `[ORE*]` grouped cargo;
- old refinery-feed availability logic could still decide `noWanted` because its per-ore soft count disagreed with the actual managed ore pool.

Resolution:

- scan actual managed cargo ore items directly;
- score physically available ore items;
- keep Stone -> Gravel demand mapping;
- remove temp diagnostics.

## MB1 backport candidate

OB1 IMS PB2 V075 should be reviewed against MB1 IMS PB2.

If MB1 has equivalent optimized refinery feed logic using a separate per-ore soft count path, backport the generic managed-ore feed selection pattern.

## Deferred

- DCS consuming WSS packet.
- WSS turret page with real turret state.
- More elegant BAYS page physical footprint representation.
- Final COMMS page packet/contract presentation.
- WSS/DCS FSU lifecycle indication.
