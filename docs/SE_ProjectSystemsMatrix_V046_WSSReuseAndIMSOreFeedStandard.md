# SE Project Systems Matrix V046 — WSS Donor Reuse, LDC1 Command UI, and IMS Ore Feed Standard

## Purpose

This document is the cross-system passdown for the current Space Engineers project after the LDC1 WSS UI foundation work and OB1 IMS PB2 Gravel/Stone refinery investigation.

GitHub exists as the repository, but current user preference is:

```text
Do not use GitHub unless explicitly told to.
Use current local/recent files for active thread patches.
```

## Current active baselines

| System | Current file | Status |
|---|---|---|
| LDC1 WSS | `LDC1_WSS_V010_MarkToggleRaySanity.cs` | Active local handoff, not yet repo-published unless user pushes it |
| OB1 IMS PB2 | `OB1_IMS_PB2_V075_GenericOreFeedAvailability.cs` | Active fix confirmed by user: Stone is topping off refineries |
| LDC1 DCS | `LDC1_DCS_V014_ExpandedStateLabels.cs` | Active prior baseline; not yet patched to consume WSS packet |
| LDC1 DPS | `LDC1_DPS_MANAGER_V183_ServiceLockedRegRefresh.cs` | Active prior baseline |
| LDC1 Drone PB | `LDC1_DRONE_BASE_PB_V004_CustodyAiNoFight.cs` | Active prior baseline |
| LDC1 ARGOS | `LDC1_ARG_V037_DpsBirthSeamIgnore.cs` | Active prior baseline |
| OB1 ARGOS | `OB1_ARGOS_V010_LocalSeamAutoTagFix.cs` | Active prior baseline |
| MB1 IMS/WSS | repo/local baselines | Stable reference/donor sources; MB1 IMS PB2 may need V075-style backport later |

## New hard rule: references are donor code

Any future patch using an existing project script as a reference must port code directly.

Forbidden patch behavior:

- remaking an existing UI from memory;
- approximating helper math;
- using “same idea” functions when donor functions exist;
- shrinking fonts or changing scale basis from donor without explicit reason;
- claiming donor reuse without actual function-body transplant.

Required patch behavior:

- fetch/inspect or use current local donor code;
- name donor file and function;
- port function body first;
- adapt variable names/content second;
- provide Reuse Gate.

## LDC1 WSS current architecture

WSS is a new command/UI PB for LDC1.

It is currently a foundation layer:

- cockpit/control-seat UI;
- external WSS LCD pages;
- boresight waypoint selection;
- range selection with smart bounded steps;
- mark lock toggle;
- FSU command packet foundation;
- selected bay and selected slot intent foundation.

It does **not** yet command DCS or launch drones.

### WSS commands

Core navigation:

```text
FOCUS
PAGE NEXT / PAGE PREV
NEXT / PREV
INC / DEC
STEP
SCAN
STATUS
```

WSS command intent:

```text
RANGE INC
RANGE DEC
MARK
FSU
ABORT
```

Diagnostics:

```text
IDSCREENS
```

### WSS focus order

Current desired order after discussion:

```text
CENTER -> WSS1 -> WSS2 -> ... -> HOTBAR -> CENTER
```

HOTBAR should still be focusable so `PAGE NEXT/PREV` can switch HOTBAR / HOTBAR 2.

### WSS cockpit surface map

```text
surface 0 = center working page
surface 1 = front-left page selector only
surface 2 = front-right selected-bay/status visual
surface 3 = far-left decorative/status visual
surface 4 = far-right hotbar/help card
```

### WSS pages

Center pages currently:

```text
BAYS
TACTICAL
TURRETS
COMMS
```

Notes:

- TURRETS is not final and should not be filler; it needs real turret state before serious use.
- HELP was removed from center because HOTBAR/HOTBAR 2 owns the help role.
- COMMS needs future simplification.
- BAYS is functional but visually bland and needs later design.

External LCD pages currently:

```text
BAY DETAIL
DRONE ROSTER
TACTICAL
```

## WSS visual donor decisions

Accepted donor sources:

- MB1 IMS hotbar card for far-right HOTBAR/HOTBAR 2;
- MB1 IMS page-selector/detail-LCD mechanics where applicable;
- MB1 WSS tactical background and focus card for center focus overlay/background.

Current problem history:

- early WSS versions failed by remaking instead of porting;
- V004 began donor-transplant direction;
- V009 fixed TACTICAL page clipping;
- V010 added mark-toggle/ray sanity.

Future WSS UI work must avoid redesign unless a donor does not exist or user explicitly asks for new design.

## OB1 IMS PB2 Gravel/Stone final finding

Observed:

- Gravel was yellow/below min on PB1;
- Stone existed in correct OB1 `[ORE*]` bulk container;
- old diagnostic reported `cargo=0/feedSoft=0`;
- temp ore-pool diagnostic proved `POOL OREPOOL all=1M ore=1M/1`;
- RFILL trace showed Stone could be moved from `[ORE*]` into refineries;
- user confirmed after V075: refineries are topping off Stone piles.

Root cause:

```text
Optimized refinery feed selection used an availability/count path that could disagree with the actual managed GROUPED ore pool.
```

Stone/Gravel exposed the issue because it is an alias edge case:

```text
Ore input: Stone
Refined output displayed as Gravel but item subtype may be Stone
UI target key: REFINED.GRAVEL
```

Accepted fix:

```text
Refinery feed selection scans actual managed cargo ore items directly and scores physically available ore.
```

This is a generic IMS standard, not a Stone-only hack.

## Do not use

Do not load or build from:

- `OB1_IMS_PB2_V071_GravelStoneHold.cs`
- any V072/V073/V074 temp diagnostic as a permanent branch

Use:

- `OB1_IMS_PB2_V075_GenericOreFeedAvailability.cs`

## Next work candidates

### If continuing OB1 IMS

- monitor V075 long enough to ensure Stone/Gravel stays stable;
- trim/remove any temp diagnostic artifacts if still installed anywhere;
- compare MB1 IMS PB2 for equivalent managed-ore feed backport.

### If continuing LDC1 WSS/DCS

- patch DCS to read WSS packet;
- define FSU consumption contract: WSS emits intent, DCS selects drones and executes assignment/launch logic;
- improve BAYS page visual only after function is stable;
- add real turret status before revisiting TURRETS page.

### If patching any PB

- use Reuse Gate;
- use PB Budget Gate;
- verify version strings;
- scan stale/unused fields/helpers;
- do not leave warnings;
- keep scripts under 100k chars and respect 50k instruction cap.
