# ARGOS V014/V040 Working Memory — DPS Birth Seam Ignore

## Patch summary

LDC1 ARGOS now has a targeted V037 refinement for DPS drone-bay birth seams.

Previous V036 behavior:

- ARGOS correctly treated connected merge blocks as hard ownership firewalls.
- ARGOS allowed local dynamic seams only when one endpoint was local `[LDC1]` and the far endpoint had no leading tag.
- After DCS assigned a printed drone serial such as `[LDC1-T2A10]`, the far merge endpoint was no longer untagged.
- Result: ARGOS saw the Bay10 seam `-1/5/1<>-2/5/1` as an unknown seam and suspended AutoTag, even though it still did not tag the drone.

V037 refinement:

- ARGOS ignores scope-acceptance pressure from DPS birth seams when exactly one merge endpoint is a DPS bay merge and the opposite endpoint is either untagged or carries a DCS-generated serial tag such as `[LDC1-T2A10]`.
- The far side remains firewalled. This patch does not make drone-side blocks local.
- This only prevents global AutoTag suspension for expected production/drone-birth seams.

## Boundary rule

A seam is treated as a DPS birth seam only when:

- one endpoint starts with the local entity tag;
- that local endpoint contains `[DPS]`;
- that local endpoint contains `[BAY`;
- the opposite endpoint is untagged or has a DCS serial-style leading tag formed from the local entity tag plus dash, e.g. `[LDC1-T2A10]`.

This avoids accepting arbitrary foreign merge seams.

## Authority impact

- ARGOS still owns entity identity and AutoTag safety.
- DPS still owns bay/factory hardware.
- DCS/drone serial tags are treated as expected disposable drone identity, not local ARGOS ownership.
- No ADS door behavior is changed.

## Test expectation

With a Bay10 printed drone/test article merged and DCS-named:

- ARGOS should not report `Tag stop: unknown merge seam -1/5/1<>-2/5/1`.
- AutoTag should remain active if the rest of scope is clean.
- Drone-side blocks should remain skipped/firewalled, not tagged by ARGOS.
