# NightSage architecture

## Product boundary

NightSage is a **target and acquisition design assistant**, not an execution scheduler.

It answers four connected questions:

1. **What should I shoot?**
2. **How should I acquire it with the active setup?**
3. **How should I frame it—including rotation and mosaics?**
4. **How should that design be materialized in Target Scheduler?**

N.I.N.A. and Target Scheduler remain responsible for hardware control and execution-time scheduling.

## End-to-end flow

```text
Active N.I.N.A. profile
        ↓
EquipmentContextService
        ↓
SetupContext
        ↓
DiscoveryService / PlanningService
        ↓
LLM structured JSON
        ↓
PlanValidator
        ↓
ImagingPlan
        ↓
FramingAssistantIntegration
(shared native IFramingAssistantVM)
        ↓
FramingSnapshot / camera rectangles
        ↓
PlanningService recalculation
        ↓
TargetSchedulerReflectionAdapter
        ↓
Project → Target(s) → ExposurePlan(s)
```

## Trust boundary

The LLM never receives a database connection or arbitrary object graph and never writes to N.I.N.A. or Target Scheduler.

Provider responses are structured JSON. NightSage applies deterministic checks for coordinates, configured filters, numeric ranges, visibility/field-fit constraints and plan shape before the plan is eligible for writing.

The user remains the final authority over framing, template selection and project creation.

## Equipment context

NightSage uses the **currently loaded N.I.N.A. profile** as the setup identity. It reads relevant profile/camera/site information and calculates field of view and image scale.

No telescope/camera/filter combination is hardcoded.

## Async context safety

Planning and discovery requests are bound to the N.I.N.A. profile that was active when they started.

Profile changes cancel in-flight work. After every provider wait, NightSage checks the captured profile ID again before accepting the result. Each validated `ImagingPlan` also carries its source profile ID, and the Target Scheduler adapter refuses to write a plan into any different profile.

Framing recalculation uses the same pattern: it captures a framing fingerprint, cancels the request if framing changes, and checks the fingerprint again before accepting the recalculated plan.

## Astrometry and visibility

NightSage keeps its own product policy for visibility sampling (10-minute samples, twilight fallback thresholds, minimum-altitude filtering and candidate scoring), but it does not maintain independent Sun/Moon ephemerides.

Target altitude is calculated through N.I.N.A. `Coordinates.Transform(...)`. Observer-specific Sun altitude and Moon position come from N.I.N.A. `AstroUtil` / NOVAS using the active profile latitude, longitude and elevation. This keeps NightSage planning aligned with the astrometry already used by N.I.N.A.

The unit-test runner does not ship N.I.N.A.'s native NOVAS/JPL runtime files, so those native calls are verified by compiling against the declared minimum `NINA.Plugin 3.2.0.9001`; NightSage unit tests cover the surrounding deterministic scoring and safety logic without duplicating N.I.N.A.'s astrometry tests.

## Discovery

Discovery has two layers:

1. The LLM proposes candidates appropriate to the active setup, selected target families and integration ambition.
2. NightSage resolves catalog coordinates through CDS Sesame, computes deterministic visibility/fit scores and applies the Quick/Balanced/Deep duration policy.

Discovery preserves category diversity first, then fills remaining result slots with the strongest alternatives.

## Integration ambition

Quick, Balanced and Deep are **nested tolerances**, not exclusive duration buckets.

- Quick softly penalizes projects beyond roughly 10 h.
- Balanced has no minimum and softly penalizes projects substantially beyond roughly 20 h.
- Deep applies no duration penalty and no duration bonus.

For approved mosaics, the planning prompt treats exposure totals as per-panel values while the ambition is evaluated against whole-project integration.

## Exposure Template policy

Target Scheduler Exposure Templates are the technical source of truth.

Matching hierarchy:

1. Same filter + requested duration → reuse unchanged.
2. Missing duration + same-filter template available → propose it as a derivation base.
3. User may select another same-filter base.
4. Derived template preserves technical settings and changes the intended duration.
5. No suitable base → explicit new template path.

Low-level settings are not silently replaced by LLM values when an existing user template can be used.

## Native Framing Assistant integration

NightSage receives N.I.N.A.'s shared `IFramingAssistantVM` through dependency injection.

The embedded Framing Assistant view is prepared through a runtime WPF `DataTemplate` so the actual N.I.N.A. control is created only after it is inside the live visual tree.

NightSage observes target state, center coordinates, camera rectangles, panel rows/columns, overlap and position angle.

A framing fingerprint marks the acquisition plan stale whenever geometry changes. Target Scheduler creation is disabled until the plan is recalculated.

The standard N.I.N.A. Framing Assistant tab and NightSage's embedded Framing tab edit the same VM/state.

## Target Scheduler adapter

NightSage does not write raw SQL.

It loads `NINA.Plugin.TargetScheduler` only when available and uses its own `SchedulerDatabaseInteraction.GetContext()` API through reflection.

Supported write versions in the public beta:

- Target Scheduler 5.9.x
- Target Scheduler 5.10.x

The methods and schema properties used by NightSage were checked against 5.9.6.0 and 5.10.3.0.

Creation flow:

1. Read existing targets and Exposure Templates for the active profile.
2. Reuse or derive Exposure Templates.
3. Back up the Target Scheduler database through Target Scheduler's own backup method.
4. Create the Project object.
5. For a single field, create one Target.
6. For a mosaic, create one Target per native Framing Assistant camera rectangle.
7. Attach Exposure Plans to every target.
8. Persist through Target Scheduler's `AddNewProject`.

If a supported Target Scheduler assembly is not loaded, planning and framing continue to work but write actions remain disabled.

## Provider abstraction

`ILLMProvider` is the provider boundary. Current adapters:

- OpenAI Responses API
- Anthropic Messages API
- Google Gemini GenerateContent
- generic OpenAI-compatible Chat Completions

Planning prompts and validation are provider-independent.

Transient timeouts, HTTP 429 and server 5xx responses receive one retry.

## Persistence and secrets

NightSage settings are stored under the current Windows user's local application data.

Provider API keys are encrypted with Windows DPAPI using `DataProtectionScope.CurrentUser`.

NightSage has no telemetry.

See `docs/PRIVACY.md` for the external data-flow boundary.
