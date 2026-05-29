# Space Engineers Shenanigans 2026

Active repository for Space Engineers programmable block scripts, handoff notes, and architecture doctrine.

## Doctrine

- CS files are Space Engineers Programmable Block scripts.
- MD files are working memory, architecture, passdown, roadmap, and patch notes.
- GitHub is the active source of truth going forward.
- Google Drive is archive and backup only.
- Every code patch must pass PATCH_GATE_SE_2026.md before delivery.

## Intended structure

- scripts/LDC1/ARGOS
- scripts/LDC1/DCS
- scripts/LDC1/DPS
- scripts/LDC1/DRONE
- scripts/LDC1/WSS
- scripts/MB1/IMS
- scripts/MB1/ARGOS
- scripts/MB1/WSS
- scripts/OB1/IMS
- scripts/OB1/ARGOS
- docs/active-memory
- docs/architecture
- docs/LDC1
- docs/MB1
- docs/OB1
- tests/LDC1/grid-id
- tests/LDC1/launch
- tests/LDC1/bay-commissioning
- archive

## Current note

This repository was initialized from the ChatGPT project workflow. PATCH_GATE_SE_2026.md is present. The active script/source baseline is tracked in docs/ACTIVE_BASELINE_MANIFEST.md and should be populated into the matching folders as raw CS and MD files.
