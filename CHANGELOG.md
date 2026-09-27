# Changelog

## 0.1.1-alpha.5 — 2026-09-27

Dedicated plugin workspace and configurable discovery.

- NightSage planner moved from the Imaging dock to the NightSage plugin page, freeing the Imaging workspace.
- The plugin page now uses the available width instead of a narrow dock.
- Settings are collapsed by default.
- Planner sections are collapsible.
- Discovery target types are individually selectable: Emission/HII, Reflection/Dark, Galaxy, Planetary Nebula, SNR/WR shell, Cluster/Star field.
- Discovery result count is configurable from 1 to 30, with 12 as the default for new settings.
- Find targets can now return multiple alternatives per category rather than being hard-limited to one result per category.
- Discovery preserves category diversity first, then fills remaining slots with the strongest alternatives.
- Existing integration ambition behavior (Quick/Balanced/Deep) applies to all returned candidates.
- The Imaging tab no longer receives a NightSage IDockableVM export.

## 0.1.1-alpha.4 — 2026-09-27

Integration ambition and discovery polish.

- Added Quick, Balanced and Deep integration ambitions to the main NightSage workspace.
- Ambitions are nested rather than buckets: Balanced and Deep still consider excellent short-integration targets.
- Quick favors compelling projects around 10h or less, with only a soft penalty beyond that.
- Balanced has no minimum and softly discourages projects substantially beyond about 20h.
- Deep removes time-based penalties entirely, but never rewards a target merely for needing more integration.
- Analyze target and Find targets both use the selected ambition.
- Discovery now asks for and displays a realistic estimated integration time for each candidate.
- Plans show the ambition used and their planned total integration.
- Changing ambition clears stale results so the next plan/discovery cannot be confused with the previous profile.
- Find targets is now a compact right-aligned button.

## 0.1.1-alpha.3 — 2026-09-27

Integration-time visibility.

- Added planned integration time at the right of every exposure row.
- Integration is calculated from the actual Target Scheduler plan: desired frame count × sub-exposure duration.
- Added per-filter integration totals, including correct aggregation of HDR/multi-duration rows for the same filter.
- Added total planned integration for the full target.
- Added compact column headers to make the exposure mapping table easier to scan.

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

- Existing Target Scheduler Exposure Templates are planning inputs and the technical source of truth.
- Exact same-filter/same-duration templates are reused unchanged.
- Special exposure durations can be derived from an existing same-filter template.
- Derived templates preserve technical settings; only exposure duration changes.
- NightSage asks before creating any missing/derived template.
- User-controlled template mapping and HDR multi-duration plans.
- Automatic Target Scheduler detection.

## 0.1.0-alpha.1 — 2026-09-27

First test release.
