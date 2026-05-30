# SE Architecture Matrix V054 — Service Locked Registration Refresh

## Change Classification

Shared LDC1 DPS/DCS interop standard. Candidate pattern for future carrier/drone factory systems.

## Problem

Bay-side grid identity can mutate after merge/unmerge. Any cached bay interface grid ID can become stale and weaken DPS dock-head self-heal.

## Resolution

DCS emits a service-locked event packet after drone heartbeat state reaches `SERVICE_LOCKED`. DPS consumes that packet and refreshes only the affected bay's current registration geometry.

## System Roles

| System | Responsibility |
|---|---|
| DPS | Bay hardware tags, accepted bay roles, registration geometry, bay self-heal cache |
| DCS | Drone assignment, heartbeat/service state, service-locked event packet |
| Drone PB | Post-heartbeat internal custody and status heartbeat |
| ARGOS | Entity identity/access/watchkeeper; current behavior remains outside drone birth hardware |

## Rules

- Do not treat bay `CubeGrid.EntityId` as permanent identity.
- Do not use DCS service packet to command bay hardware directly.
- DPS refreshes `DPS_REG` from DPS-tagged bay hardware only.
- DCS `ServiceLockedSeq` is an event counter, not the drone identity.
- Drone serial remains the durable drone identity.

## Deferred

- DCS Birth Guard Fast Lane.
- Automatic full mature birth flow replacing temporary REG command surface.
- Final DCS roster LCD polish.
