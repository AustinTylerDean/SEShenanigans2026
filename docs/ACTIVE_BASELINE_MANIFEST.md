# ACTIVE BASELINE MANIFEST

Current repository baseline after local publish commit `7846ceb52baf29d50bd01c2dc024da5973e95fd3` and the current `main` state.

GitHub is now the working source of truth for this project. Update this manifest whenever a PB/script baseline changes meaningfully.

## Active scripts

### LDC1

- `scripts/LDC1/ARGOS/LDC1_ARG_V037_DpsBirthSeamIgnore.cs` — active LDC1 ARGOS with DPS birth-seam ignore.
- `scripts/LDC1/DCS/LDC1_DCS_V014_ExpandedStateLabels.cs` — active DCS with expanded LCD state labels and service-lock packet path.
- `scripts/LDC1/DPS/LDC1_DPS_MANAGER_V183_ServiceLockedRegRefresh.cs` — active DPS with SERVICE_LOCKED reg-refresh handling.
- `scripts/LDC1/DRONE/LDC1_DRONE_BASE_PB_V004_CustodyAiNoFight.cs` — active drone base PB with custody AI/no-fight behavior.
- `scripts/LDC1/DRONE/LDC1_DRONE_PB_TEMP_Bay10_PJ01_GridIdLogger.cs` — temporary Bay10/PJ01 merge-grid identity logger.
- `scripts/LDC1/MISSILE/Missile-V18.cs` — legacy/current missile script reference.
- `scripts/LDC1/MISSILE/Missile Rack V7.cs` — legacy/current missile rack script reference.

### MB1

- `scripts/MB1/ADS/MB1_Auto_Door_Closer.cs` — MB1 automatic door closer legacy/standalone script.
- `scripts/MB1/IMS/MB1_IMS_PB1_V58_BuildQueueSourceLocalDisplay.cs` — active MB1 IMS PB1 console.
- `scripts/MB1/IMS/MB1_IMS_PB2_V30_BuildQueueSourceEnforcement.cs` — active MB1 IMS PB2 worker.
- `scripts/MB1/REACTOR/Ship_Reactor_Display_Demo_V18_Audio.cs` — MB1 reactor display/audio script.
- `scripts/MB1/SIS/MB1_SIO_PB1_V14.cs` — active MB1 SIS/SIO PB1 script.
- `scripts/MB1/WSS/MB1_WSO_PB2_V12_BoundaryGuard.cs` — active MB1 WSO/WSS PB2 boundary guard fix.
- `scripts/MB1/WSS/MB1_WSS_PB1_V11_68.cs` — active MB1 WSS/WSO PB1 console script.

### OB1

- `scripts/OB1/ARGOS/OB1_ARGOS_V010_LocalSeamAutoTagFix.cs` — OB1 ARGOS baseline.
- `scripts/OB1/IMS/OB1_IMS_PB1_V034_BuildQueueSourceLocalDisplay.cs` — OB1 IMS PB1 console.
- `scripts/OB1/IMS/OB1_IMS_PB2_Current.cs` — OB1 IMS PB2 worker.
- `scripts/OB1/IMS/OB1_IMS_PB3_V015_BuildPlannerQueueRebalance.cs` — OB1 IMS PB3 production/assembly worker.

## Active docs / accessory files

- `PATCH_GATE_SE_2026.md` — required patch gate reminder.
- `README.md` — repository overview.
- `docs/ARGOS/ARGOS_V014_V040_WorkingMemory_DpsBirthSeamIgnore.md` — ARGOS DPS birth-seam refinement note.
- `docs/LDC1/SE_LDC1_GRID_ID_IMPACT_STUDY_V001.md` — LDC1 grid identity impact study.
- `docs/LDC1/SE_LDC1_GRID_ID_SCRIPT_IMPACT_AUDIT_V002.md` — script-level grid identity impact audit.
- `docs/active-memory/SE_ACTIVE_WORKING_MEMORY_V009_ServiceLockedRegRefresh.md` — current service-lock/reg-refresh working memory.
- `docs/architecture/SE_ARCHITECTURE_MATRIX_V054_ServiceLockedRegRefresh.md` — current architecture matrix update.

## Current doctrine notes

- GitHub is the source of truth from this point forward.
- New patches should read the current file from GitHub before editing.
- PB delivery must follow `PATCH_GATE_SE_2026.md`.
- Active file names should keep version tags; do not overwrite history by silently renaming old baselines unless the manifest is updated too.
- WSS work should borrow structure from existing IMS/DPS/DCS/WSO patterns instead of becoming a bespoke standalone invention.
