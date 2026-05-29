# ARGOS Working Memory V014 / V040 - DPS Birth Seam Ignore

## Purpose

This note records the ARGOS refinement made after LDC1 ARGOS V036 correctly failed safe, but too aggressively suspended AutoTag when a printed drone/test article was merged to a DPS bay birth seam.

## Observed problem

LDC1 ARGOS V036 reported:

```text
ARGOS V036 LDC1
Tag: SUSP auto ON tagNew ON dryrunOK NO
Tag stop: unknown merge seam -1/5/1<>-2/5/1
Scope: grids 31/31 mech 30/30 merges 4/2 amb 0 blk 0 dyn 0
Firewall: TRACE far 0 amb 0
```

The seam coordinates matched the Bay10 DPS/drone merge pair previously logged by the temporary grid ID logger:

```text
Bay merge:   -1,5,1
Drone merge: -2,5,1
```

ARGOS was not tagging the drone. It was suspending AutoTag because the connected merge seam was not part of the accepted ARGOS scope.

## Root cause

V036’s dynamic local seam ignore logic was written for the earlier expectation that the far/printed side of a safe production seam would be untagged. The current DCS/Drone birth flow names the drone side with a serial identity such as:

```text
[LDC1-T2A10] MERGE 01
```

That is not an ARGOS local entity tag, but it is also not untagged. V036 therefore treated the seam as unknown.

## V037 refinement

ARGOS V037 recognizes expected DPS birth seams when:

```text
one endpoint is local [LDC1] [DPS] [BAY##] merge hardware
other endpoint is either untagged or carries a DCS serial tag like [LDC1-T2A10]
```

This prevents global AutoTag suspension for expected DPS production/birth seams.

## Boundary preserved

This does not make the drone side local.

The merge firewall remains authoritative:

```text
local side remains ARGOS/LDC1 local
far side remains firewalled/skipped
ambiguous remains skipped
```

ARGOS still must not tag printed drone hardware during birth.

## Authority doctrine

- ARGOS owns entity identity/access/watchkeeping.
- DPS owns bay/factory hardware.
- DCS owns drone birth/control orchestration.
- Drone PB owns post-heartbeat internal custody.
- DPS production merge seams are expected dynamic seams, not unknown ARGOS topology changes.

## Test expectation

With a Bay10 drone/test article merged:

```text
Tag stop: unknown merge seam -1/5/1<>-2/5/1
```

should clear.

ARGOS should continue AutoTag maintenance on local LDC1 blocks and still skip printed drone/test article hardware.
