# SE LDC1 Grid Identity Impact Study V001

## Trigger

LDC1 Bay10 test article was maxed to build volume, merged, and unmerged. The user-facing small-grid name changed after the merge/unmerge cycle. Numeric `CubeGrid.EntityId` has not yet been logged, but the observed grid-name mutation is enough to treat grid identity metadata as volatile across merge operations.

## Working conclusion

`CubeGrid.EntityId` and `CubeGrid.CustomName` are useful as live topology signals, but they must not be treated as permanent ownership identity for LDC1 drone birth, service, launch, or later DCS tracking.

The durable identity standard should be script-level identity:

- `Serial`
- `Origin`
- `Bay`
- `Wave`
- `Role/Short`
- Drone PB `EntityId` if the PB block survives
- heartbeat identity packet

Grid ID remains useful as a current boundary, not as the identity itself.

---

## System-by-system impact

## DPS — Drone Printer System

### Current exposure

DPS uses grid relationships heavily during commissioning, bay grouping, dock-head identification, and bay geometry inference. It also records/compares registered grid IDs in some paths, especially around commissioned bay/dock geometry.

Observed code patterns include:

- `RegGrid[n]` comparisons
- `CubeGrid.EntityId` included in diagnostics
- `AnchorGrid()` choosing a merge/connector grid near accepted geometry
- bay core diagnostics reporting `GridId`
- bay clustering and commissioning based partly on same-grid relationships

### Risk

If a bay small-grid interface changes grid identity after merge/unmerge, any DPS feature that treats the old grid ID as permanent can become stale.

Highest-risk areas:

- support/dock-head recovery if it keys too strongly on old grid ID;
- bay interface/dock-head registration if it expects the same grid ID after a remerge cycle;
- future DPS/DCS packet fields if they publish grid ID as if it is durable;
- projector/connector/merge slot anchoring if future patches simplify too much around `GridId`.

### What is probably safe

DPS’s core bay hardware on the large-grid carrier is mostly safe because those blocks are not on the disposable small-grid birth article. Bay welders, gates, bay LCDs, large-grid carrier hardware, and accepted bay labels are not expected to change identity through drone merge/unmerge.

### Required hardening

DPS should treat grid IDs as cache fields.

DPS should publish, if needed:

```ini
Bay10.InterfaceGridId=<current live interface grid>
Bay10.InterfaceGridName=<diagnostic only>
Bay10.InterfaceAnchorMergeId=<bay-side merge block EntityId>
Bay10.InterfaceAnchorConnectorId=<bay-side connector EntityId>
Bay10.LoadedSlot=PJ02
Bay10.NextSlot=PJ02
Bay10.State=READY
```

DPS should be able to refresh `InterfaceGridId` from stable accepted bay anchors: bay-side merge/connector block `EntityId`, accepted bay tags, and current topology.

Do not use `InterfaceGridId` as permanent bay identity.

---

## DCS — Drone Control System

### Current exposure

DCS currently discovers the birth candidate PB by:

- finding the bay merge carrying `[BAY##]`;
- using that merge’s `CubeGrid` as the interface grid;
- selecting an untagged PB on that exact grid;
- writing assignment;
- enforcing one-time captive safety;
- using heartbeat/serial for status.

### Risk

DCS is the most affected system because the planned birth guard wants to use cached bay interface grid identity for fast safety enforcement.

If DCS assumes the cached `InterfaceGridId` is durable across merge cycles, it could either:

- guard the wrong grid;
- fail to guard the correct newborn grid;
- fail to find the drone PB;
- incorrectly reject a valid drone after grid identity changes;
- keep stale service context after remerge/unmerge.

### Correct model

DCS should split identity into two layers:

#### Birth boundary

```ini
Bay10.InterfaceGridId=<current live bay small-grid topology boundary>
```

Useful for:

- birth guard;
- finding the newborn drone PB;
- safing newborn blocks;
- assignment write;
- pre-heartbeat custody.

#### Drone identity

```ini
Serial=T2A10
Origin=LDC1
Bay=10
Wave=A
Role=TEST_2
DronePbEntityId=<if known>
CurrentGridId=<telemetry only>
```

Useful for:

- heartbeat tracking;
- post-separation identity;
- DCS roster;
- future command routing.

### Required hardening

DCS birth guard should:

- use current `InterfaceGridId` only while it validates against current bay anchors;
- refresh `InterfaceGridId` from DPS/bay merge state when stale;
- stop and fault if it cannot validate the bay interface grid;
- never broaden into connector-visible “all blocks” fallback during live guard;
- hand off to drone PB heartbeat identity after heartbeat is proven.

DCS should log/report:

```ini
OriginGridId=<interface grid at assignment>
CurrentGridId=<latest drone heartbeat grid>
GridChanged=YES/NO
```

A changed `CurrentGridId` after merge separation should not be a fault by itself.

---

## Drone Base PB

### Current exposure

Drone PB mostly uses `SameGrid()` / `Me.CubeGrid` to scan its local block set. This is normally good after the PB is alive, because it should own whatever grid it is currently on.

### Risk

If `Me.CubeGrid.EntityId` changes after merge separation/remerge, any saved field treating the previous grid ID as durable will become stale.

The Drone PB should not identify itself by grid ID. It should identify itself by assignment and heartbeat identity.

### Required hardening

Drone PB should report both:

```ini
OriginGridId=<grid ID assigned by DCS during birth>
CurrentGridId=<Me.CubeGrid.EntityId now>
CurrentGridName=<diagnostic only>
DronePbEntityId=<Me.EntityId>
```

Drone PB should treat `CurrentGridId` changes as telemetry unless the state machine says the change is impossible for the current phase.

Post-heartbeat custody should use `Me.CubeGrid` live, not a saved old grid ID.

---

## ARGOS

### Current exposure

ARGOS is intentionally protecting printed drone blocks from being autonamed while they are across the merge boundary. The observed grid-name mutation increases the importance of ARGOS not using grid name or grid ID as the sole ownership source.

### Risk

If ARGOS ever treats a changed grid name/ID as local carrier identity, it could rename or classify drone blocks incorrectly after merge/unmerge.

### Required hardening

ARGOS should remain conservative:

- do not tag through connectors;
- do not tag through unaccepted merge topology;
- do not rely on grid name alone;
- use accepted scope/topology plus explicit entity tag authority.

For LDC1 drone birth, ARGOS should not be the birth identity authority. DCS + Drone PB serial/heartbeat should own drone identity.

---

## WSS / WSO

### Current exposure

A real issue was found and patched in MB1 WSO/WSS PB2: tactical control could affect connector-visible tactical blocks because the tactical apply loop lacked the `[MB1]` ownership gate. This proves connector-visible block exposure is a real operational hazard.

### Impact from grid identity volatility

WSS/DCS future drone command should not depend on `CubeGrid.EntityId` as permanent drone identity. If WSS later commands drones by grid ID, it may lose or misidentify drones after merge/remerge/split events.

### Required hardening

WSS should command drones by:

- serial;
- role;
- wave;
- DCS registry;
- IGC channel/source identity;
- optional current grid ID only as telemetry.

Do not command “all visible AI Offensive blocks” without ownership gating.

---

## LDC1 LCD/UI impact

### DPS LCD

DPS should continue to show bay state, loaded, and next. It should not expose grid IDs on small bay LCDs.

### DCS LCD

DCS active drone roster should use serial/state/telemetry, not grid name.

Good roster line:

```text
B10 T2A10 SVC B100 H100 HB1
```

Bad roster line:

```text
B10 GridNameWhatever...
```

Grid name belongs only in diagnostic Custom Data.

---

## Immediate test requirement

Create or patch a tiny logger to capture numeric identity through merge cycles.

Required fields:

```ini
Phase=
Tick=
MeEntityId=
MeGridId=
MeGridName=
Merge01EntityId=
Merge01GridId=
Merge01GridName=
Connector01Status=
Connector01EntityId=
Connector01GridId=
Battery01EntityId=
Battery01GridId=
AiOff01EntityId=
AiOff01GridId=
```

Test sequence:

1. Captive merged.
2. DCS service merge off / connector locked.
3. Connector release / free drone.
4. Connector reconnect only.
5. Merge reconnect.
6. Merge off again.

Expected useful result:

- determine whether numeric `CubeGrid.EntityId` changes with the observed grid name change;
- determine whether individual block `EntityId` survives;
- determine whether remerge assigns the carrier-side or drone-side grid identity.

---

## Required doctrine update

Do not write this as memory until verified further. For project MDs, record it as a working design constraint:

```text
Grid identity is volatile across merge/unmerge until proven otherwise. Use grid IDs as current topology/cache only. Durable identity belongs to script-level serial/assignment/heartbeat and stable block EntityIds where applicable.
```

---

## Practical priority list

1. Add grid identity logger test.
2. Patch Drone PB heartbeat to include `CurrentGridId`, `CurrentGridName`, and `DronePbEntityId`.
3. Patch DCS to distinguish `OriginGridId` from `CurrentGridId`.
4. Add DCS birth guard only after the above identity telemetry is visible.
5. Update DPS/DCS packets so interface grid is clearly labeled as current/cache, not permanent identity.
6. Keep WSS/WSS boundary gates strict before drone command integration.

