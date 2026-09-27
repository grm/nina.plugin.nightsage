# Changelog

## 0.1.1-alpha.2 — 2026-09-27

UI cleanup and provider resilience.

- Removed the extra per-row "Use existing..." text; template reuse/derivation is now shown directly inside the template selector.
- Template selectors now offer contextual labels such as "existing" or "base → create 60s", plus an explicit "create new from plan settings" choice.
- Refresh is a compact button aligned to the right of the NightSage title instead of filling the whole header.
- Starting Analyze, Find targets or Build plan now clears the previous plan immediately so stale results are never shown while a new request is running.
- LLM request timeout increased from 120s to 180s.
- Added one automatic retry for transient HTTP failures (429/5xx) and provider timeouts.
- Timeout errors now explicitly say the provider may be temporarily slow or rate-limited.

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
- Target Scheduler detection refreshes automatically while waiting for the assembly to load.
- Discovery list follows the N.I.N.A. theme and has clearer selection guidance.
- Added explicit labels/help for target input, optional planning instructions, autonomy mode and Target Scheduler option switches.

## 0.1.0-alpha.1 — 2026-09-27

First test release.
