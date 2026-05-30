# SE Active Working Memory V009 — Service Locked Reg Refresh

## Current Patch Set

- `LDC1_DCS_V013_ServiceLockedDpsRegPacket.cs`
- `LDC1_DPS_MANAGER_V183_ServiceLockedRegRefresh.cs`

## Accepted Finding

Bay-side small-grid identity can change across merge/unmerge. Drone identity should not depend on bay-side `CubeGrid.EntityId` or grid name.

Operational identity split:

- Drone identity: DCS/drone serial plus assignment metadata.
- Bay hardware identity: DPS tags and accepted bay roles.
- Grid IDs: live topology/cache only.

## New Interop Contract

DCS publishes a compact service event packet in its Custom Data:

```ini
# <DCS_SERVICE_BEGIN>
[DCS SERVICE]
BAY10.ServiceLockedSeq=1
BAY10.Serial=T2A10
BAY10.State=SERVICE_LOCKED
# <DCS_SERVICE_END>
```

DPS reads the packet and, when a new `ServiceLockedSeq` appears for a bay, immediately refreshes that bay's `DPS_REG` entry from current live DPS-tagged bay hardware.

## Purpose

`DPS_REG` is now treated as a current captive dock-head geometry cache, not a permanent bay identity record.

The refresh trigger is `SERVICE_LOCKED` because this is the stable captive moment after merge separation/service handoff, while the bay connector remains locked and DPS-tagged bay hardware is still available.

## Authority Split

- DCS owns drone birth/service event reporting.
- DPS owns bay registration geometry and bay hardware self-heal cache.
- Drone PB owns post-heartbeat internal custody.

## Test Target

After loading DCS V013 and DPS V183:

1. Produce or use a Bay10 service-locked drone.
2. Confirm DCS Custom Data contains `BAY10.ServiceLockedSeq=`.
3. Confirm DPS Echo reports `DCS REG BAY10 seq #`.
4. Confirm `[DPS_REG] D10=` updates to the current post-service bay-side grid ID/cache.

## Notes

This patch does not implement the future DCS birth guard fast lane. It only adds the service-locked event bridge and DPS per-bay registration refresh.
