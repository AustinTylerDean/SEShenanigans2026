# PATCH_GATE_SE_2026

Before delivering any PB/script patch, do all of this silently and report the results.

## 1) SCOPE LOCK

- State exactly which PB files are affected.
- If feature touches multiple PBs, patch every affected PB now or explicitly defer with reason.
- Do not modify unrelated behavior.
- Preserve proven behavior unless explicitly changing it.
- Identify whether patch is: DPS / DCS / DRONE / ARGOS / IMS / WSS / shared standard.

## 2) IMPACT CHECK

- Cross-check all functions that read/write the same hardware/data.
- Look for PB-vs-PB authority fights.
- Confirm no connector-visible foreign blocks can be touched unless explicitly intended.
- Confirm no stale CubeGrid/GridName assumptions are used as durable identity.
- Use tags/serials/block roles/accepted records as identity; grid IDs only as current topology hints.
- For merge/connector/docked/subgrid logic, explain exact boundary rule.

## 3) PB BUDGET GATE

- Character count under 100,000.
- Estimate/guard against 50,000 instruction cap.
- No broad scans in fast/update lanes unless cached/sliced/guarded.
- Fast lanes must be tiny and priority-based.
- Slow truth scans must be paced.
- Custom Data writes only when needed; no spam writes.
- Avoid huge diagnostics unless explicitly requested.

## 4) KEEN PB COMPATIBILITY

- C# 6 / Space Engineers PB-safe only.
- No local functions.
- No async/await.
- No dynamic.
- No reflection.
- No file/network/thread/task APIs.
- Avoid LINQ, especially hot paths.
- Avoid unsupported syntax.
- Check for broken multiline strings.
- Check all helper methods are class-level.

## 5) CLEANUP PASS

- Remove unused fields/methods/constants.
- Remove stale diagnostics/old version strings.
- No compiler warnings unless explicitly justified.
- Verify brace balance.
- Verify parentheses balance.
- Verify no duplicate method/field names.
- Verify version labels/header/Echo match delivered filename.
- Search for old version strings and stale labels.

## 6) CUSTOM DATA / UI QUALITY

- Keep Custom Data clean, ordered, minimal.
- Use managed BEGIN/END sections.
- Avoid sloppy long unwrapped report lines.
- Do not overwrite user-edited config unless intended.
- If LCD/display touched: respect physical layout constraints and prior doctrine.
- If row-limited LCD: one row per entity unless explicitly approved.

## 7) AUTHORITY CONTRACT

- DPS owns bay/factory hardware.
- DCS owns drone birth/control orchestration and pre-heartbeat firebreak.
- Drone PB owns post-heartbeat internal custody.
- ARGOS owns entity identity/access/watchkeeping but must not claim printed drone birth hardware.
- WSS owns tactical/weapons/drone command only inside approved ownership boundaries.
- No PB should silently fight another PB for the same block setting.

## 8) TEST EXPECTATIONS

- Give exact post-load/recompile test steps.
- Include expected Echo/Custom Data/LCD evidence.
- Include what failure would look like.
- If STATUS/SCAN/etc. is required after recompile, say so explicitly.
- If recompile alone should show the change, say that.

## 9) DELIVERY REPORT REQUIRED

Report:

- File links.
- Character count per file.
- Brace/parens pass.
- Forbidden feature scan pass/fail.
- Version/header/Echo verification.
- Old version string count.
- Scope summary.
- Impact summary.
- Test steps.
- Any known risk or deferred work.

**DO NOT SAY “patched” UNTIL THIS GATE IS COMPLETE.**

## Tiny version

```text
PATCH_GATE_SE_2026: Before delivering code, run scope/impact/PB-budget/Keen-compat/cleanup/authority/UI/test gates. Cross-check PB fights and connector-visible boundaries. Count chars, brace/parens, stale versions, forbidden features, unused code, and old labels. Report file links, checks, scope, risks, and exact test steps. Do not deliver without the checklist.
```
