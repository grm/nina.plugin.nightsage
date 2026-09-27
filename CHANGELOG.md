# Changelog

## 0.1.1-alpha.1 — 2026-09-27

Template-aware planning and first UI polish pass.

- Existing Target Scheduler Exposure Templates are now planning inputs and the technical source of truth.
- Exact same-filter/same-duration templates are reused unchanged.
- Special exposure durations (HDR, bright cores/stars, etc.) can be derived from an existing same-filter template.
- Derived templates preserve gain, offset, binning, readout mode, twilight, dithering, humidity and all moon-avoidance settings; only exposure duration changes.
- NightSage asks before creating any missing/derived template.
- Every exposure row exposes the proposed Target Scheduler template in a selector; the user can choose another compatible base before creation.
- Exposure Plans inherit their duration from the selected/created template instead of overriding it.
- Multiple exposure durations for the same filter are retained, enabling HDR plans.
- Target Scheduler detection now refreshes automatically while the plugin is waiting for the assembly to load; manual Refresh is no longer required in the normal startup path.
- Discovery list no longer renders as a large white block and has clearer selection guidance.
- Added explicit labels/help for target input, optional planning instructions, autonomy mode and Target Scheduler option switches.

## 0.1.0-alpha.1 — 2026-09-27

First test release.

- Read the currently loaded N.I.N.A. profile and connected camera information.
- Build full acquisition plans from a target name.
- Discover and rank targets suitable for the current setup over the next seven days.
- Deterministic validation of target coordinates, filter availability, field fit and visibility scoring.
- OpenAI, Anthropic, Google Gemini and OpenAI-compatible provider backends.
- Preview, Create and Autopilot modes.
- Target Scheduler 5.9.x integration without a compile-time dependency.
- API keys stored locally using Windows DPAPI.
