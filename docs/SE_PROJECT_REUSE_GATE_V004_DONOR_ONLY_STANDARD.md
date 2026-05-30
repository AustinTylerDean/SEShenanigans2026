# SE Project Reuse Gate V004 — Donor Only Standard

## Non-negotiable rule

When a user references an existing working project script, screen, behavior, or subsystem:

```text
Reference = donor code transplant.
```

No remake. No approximation. No “same idea.” No screenshot recreation. No helper-math substitution.

## Required before coding

Classify the requested feature:

```text
PROVEN  = a working project implementation exists.
SIMILAR = a close project implementation exists.
NEW     = no project implementation exists.
```

For `PROVEN` and `SIMILAR`, identify:

```text
Donor file(s):
Donor function(s):
Donor constants/fields:
Donor command model:
Donor scale/geometry model:
```

## Required delivery section

Every code patch must include:

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

Invalid patch conditions:

- `PROVEN` feature with no donor function named.
- UI reference with changed scale basis and no explicit user-approved reason.
- “Copied function body from donor” should be YES but is NO.
- Patch claims reuse but only recreates the appearance.
- Patch changes command routing while supposedly only changing display.
- Patch leaves warnings or unused stale fields.

## UI-specific rules

UI donor port means preserving:

- `SurfaceSize` vs `TextureSize` choice;
- scale basis (`u`, `sc`, etc.);
- minimum scale clamps;
- frame/card coordinates;
- font sizes;
- row spacing;
- focus overlay mechanics;
- page selector mechanics;
- color role intent;
- helper draw semantics.

Content labels may change. Geometry should not change until the donor version is proven on the target screen.

## Current known UI donors

| Use | Donor |
|---|---|
| Hotbar/help card | MB1 IMS `DrawOfficerHelp` |
| Page selector | MB1 IMS `DrawOfficerPages` |
| Detail LCD focus rails | MB1 IMS `DrawDetailFocusRails` |
| Center focus overlay / tactical background | MB1 WSS PB1 donor background/focus-card code |
| Focus/page routing | MB1 WSS PB1 `StepFocus`, `StepFocusedPage`, `FocusName` |
| Officer selectable rows | MB1 IMS officer row functions |

## PB budget gate still applies

Before delivery:

- character count under 100k;
- estimate or preserve instruction headroom under 50k;
- no broad scans in hot paths;
- cached block lists and phased scanning where needed;
- command work chunked where needed;
- no async, dynamic, reflection, Task/thread APIs, LINQ-heavy hot paths;
- brace/parens checks;
- stale version string scan;
- unused field/helper cleanup.

## Current user preference

Do not use GitHub unless the user explicitly asks.

Use current local/recent files and generated handoff files for fast iteration during the active thread.
