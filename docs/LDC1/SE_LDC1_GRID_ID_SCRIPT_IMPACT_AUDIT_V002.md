# SE_LDC1_GRID_ID_SCRIPT_IMPACT_AUDIT_V002

## Scope
Reviewed the current LDC1-related script files in the working set for exposure to bay small-grid name / `CubeGrid.EntityId` volatility after merge/unmerge:

- `LDC1_DPS_MANAGER_V182_ServiceMergeRespect.cs`
- `LDC1_DCS_V012_CustodyAiNoFight.cs`
- `LDC1_DRONE_BASE_PB_V004_CustodyAiNoFight.cs`
- `LDC1_ARG_V036_LocalSeamAutoTagFix.cs`
- Relevant upstream lesson from `MB1_WSO_PB2_V12_BoundaryGuard.cs`

This audit treats the observed fact as known: Bay10 small-grid user-facing grid name changed after merge/unmerge, so grid identity metadata is not stable enough to use as permanent ownership or identity.

## Summary Table

| System | Current risk | Why | Immediate action |
|---|---:|---|---|
| DPS V182 | Medium | Persists `RegGrid[]` in Custom Data and only sets it if missing. A bay interface grid ID/name changing can stale this cache. | Do not use persisted `RegGrid` as drone/birth authority. Refresh current interface grid from live bay merge/connector when exporting to DCS. |
| DCS V012 | Low now / High for planned birth guard | Current discovery uses live bay merge `CubeGrid`, not persisted InterfaceGridId. Future fast birth guard could be wrong if it relies on stale grid ID. | Birth guard must use current live interface grid, refreshed/validated from bay merge/connector/PB evidence. |
| Drone PB V004 | Low | Uses `Me.CubeGrid` live each scan. Does not depend on stored grid ID. | Add `CurrentGridId` telemetry later for diagnostics only, not authority. |
| ARGOS V036 | Medium | Does not persist grid ID doctrine, but AutoTag ownership scope can expand through accepted mechanical/topology paths. A merged newborn grid can temporarily appear local. | Exclude DPS birth/bay interface grids or untagged drone-birth candidates from AutoTag maintenance. |
| WSO/WSS lesson | High class risk, patched for MB1 | Connector-visible broad control can affect foreign blocks. Grid ID instability reinforces not using visible grid alone as authority. | Future LDC1 WSS/DCS must require explicit local authority, not visibility. |

## DPS V182 Detailed Impact

### What is safe
DPS core bay operation is mostly tag and live-block based:

- Welders/projectors/merge/connectors are discovered by `[LDC1]`, `[DPS]`, `[BAY##]`, and block type.
- Current bay states (`IDLE`, `PRINTING`, `COMPLETE`, `READY`, `LAUNCHING`) are persisted by bay/slot, not by grid ID.
- Launch/print hardware control uses currently discovered bay hardware lists, not permanent grid identity.

### What is exposed
DPS has persistent registration fields:

```csharp
bool[] Reg = new bool[MAXB+1];
long[] RegGrid = new long[MAXB+1];
Vector3D[] RegMin, RegMax;
```

`UpdateReg()` only populates a bay if `Reg[i]` is false. It does not automatically refresh a stale `RegGrid[i]` if the bay interface grid identity changes.

Affected functions/uses:

- `RegDock(...)` requires `c.CubeGrid.EntityId == RegGrid[n]`.
- `FindPanelBay(...)` can use `RegDock(...)` for panel assignment.
- `CurBayForPV(...)` compares `AnchorGrid(pv).EntityId` against `RegGrid[i]`.
- Commissioning/assignment helpers use current `AnchorGrid(...)` and live `CubeGrid` refs, but persisted `RegGrid` can still mislead diagnostics or accessory association.

### Current severity
Medium. It probably does **not** break current printing/ready/launch basics, but it can break or mislead accessory association and would be unsafe as the source of truth for DCS birth guard.

### Required doctrine change
DPS may still keep a registration box/geometry cache, but must distinguish:

- `RegGrid` = stale-prone historical registration / geometry hint.
- `CurrentInterfaceGridId` = live current bay interface grid from current bay merge/connector/PB evidence.

DPS should export current interface grid only when freshly derived in the current scan. DCS should not consume stale `RegGrid` as authority.

## DCS V012 Detailed Impact

### What is safe now
DCS does not currently persist InterfaceGridId as accepted state. Accepted state persists:

- Accepted
- ServiceDone
- Serial
- State

`DiscoverBay(...)` recomputes `InterfaceGridId` from the currently discovered bay merge block:

```csharp
if(CandMerges.Count==1){
  y.Merge=CandMerges[0];
  iface=y.Merge.CubeGrid;
  y.InterfaceGridId=iface.EntityId;
}
```

That is good for the current manual REG workflow because it uses the live merge block's current grid.

### What will be exposed
The planned fast birth guard is exactly where stale grid ID can bite us. If DCS caches `Bay10.InterfaceGridId` once and keeps using it after merge/unmerge/re-merge, it can either:

- stop guarding the correct newborn grid, or
- guard the wrong grid if IDs/names changed underneath it.

### Required doctrine change
DCS fast guard must use a validated current boundary:

1. Prefer the current bay merge/connector/PB evidence from a fresh or recently validated DCS/DPS scan.
2. Use current `CubeGrid` object / current `EntityId` as a runtime boundary only.
3. Fault clearly if current interface grid cannot be validated.
4. Never treat old InterfaceGridId as permanent drone identity.

## Drone PB V004 Detailed Impact

### What is safe
Drone PB uses:

```csharp
bool SameGrid(IMyTerminalBlock b){ return b != null && b.CubeGrid == Me.CubeGrid; }
```

That is the correct pattern for a self-owned drone script. If the grid changes identity after merge separation, the PB's local scan follows `Me.CubeGrid` automatically.

It also uses Serial/Bay/Role/Wave from assignment for identity, not grid ID.

### Gap
Heartbeat/status does not currently report `CurrentGridId` or `Me.EntityId`.

### Required later improvement
Add telemetry only:

- `PbEntityId`
- `CurrentGridId`
- optional `GridName`

Do not use those as permanent identity. Use them as diagnostics and DCS routing evidence.

## ARGOS V036 Detailed Impact

### What is safe
ARGOS does not appear to persist a permanent grid ID doctrine. It rebuilds `_ownedGridIds` during task passes from:

- `Me.CubeGrid.EntityId`
- accepted mechanical relationships
- merge/topology evaluation

### What is exposed
ARGOS AutoTag ownership can become temporarily too broad if a newborn drone grid is merged into an accepted local mechanical/bay interface grid. Since the merged drone may appear as part of the local owned scope, untagged drone blocks could be treated as local AutoTag candidates.

This is especially relevant because the drone birth doctrine intentionally wants newborn drone blocks untagged until the drone PB self-names after service separation.

### Current severity
Medium. We have not observed ARGOS tagging the current test drone, but the topology behavior means the risk is real enough to guard before enabling mature automated birth workflows.

### Required doctrine change
ARGOS should avoid AutoTagging drone birth grids. Candidate mitigations:

- Exclude blocks on known DPS bay interface/current birth grids while a bay is printing/loaded/service-locked.
- Exclude untagged PB candidate grids that DCS reports as active birth candidates.
- Treat unexpected merge expansion into a DPS bay birth grid as suspend/review rather than tag-through.

## WSO/WSS Boundary Lesson

The MB1 WSO PB2 bug proved a separate but related class of issue: connector-visible blocks are not owned blocks. Future LDC1 WSS/DCS work must not command blocks merely because the terminal system can see them.

Required standard:

- Explicit local entity tag/authority check before hardware writes.
- Reject foreign leading tags.
- Treat connector visibility as non-ownership.
- Treat grid ID as runtime topology, not identity.

## Immediate Fix Priorities

1. **Before DCS birth guard:** patch the design so DCS uses live/current bay interface grid validation, not stale DPS `RegGrid`.
2. **Before full AutoTag maintenance near drone bays:** patch ARGOS exclusion/suspend behavior for DPS birth grids.
3. **DPS modernization:** split persisted registration geometry from current live interface grid export.
4. **Drone telemetry:** add `CurrentGridId` and `PbEntityId` for diagnostics only.

## Current Bottom Line

The grid identity/name volatility does not appear to break current DCS heartbeat/service-locked testing directly.

It **will** bite later if we build DCS birth guard or WSS/DCS routing on a cached grid ID as if it were permanent.

The highest-impact current exposure is DPS `RegGrid[]` staleness and ARGOS potential AutoTag ownership expansion during a merged newborn-drone state.
