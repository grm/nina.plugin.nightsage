# NightSage user guide

This guide describes the 0.2 public-beta workflow.

## 1. Open NightSage

NightSage lives on its plugin page:

**Options → Plugins → NightSage**

The workspace has three tabs:

- **Target & Plan**
- **Framing**
- **Settings**

NightSage intentionally does not occupy the Imaging workspace.

## 2. Configure Settings

Open **Settings → AI provider**.

Choose OpenAI, Anthropic, Google Gemini or an OpenAI-compatible endpoint. Set the model and API key, then use **Test provider**.

The Settings page includes a provider-specific **Get an API key ↗** link. For compatible/local servers it links to the NightSage endpoint guide instead.

See [AI provider setup](PROVIDERS.md) for cloud-provider key links and ready-to-use OpenAI-compatible examples for Ollama, LM Studio, OpenRouter, Groq and Mistral.

Provider API usage may incur charges from the selected provider. NightSage does not provide or resell API access.

For compatible/local servers, set the endpoint.

### Privacy note

If you use a cloud provider, planning context can include the observing site's coordinates. See `PRIVACY.md` before using NightSage with a private/home observing location.

## 3. Confirm the active setup

Return to **Target & Plan**.

The header shows the active N.I.N.A. profile, telescope, focal length/f-ratio, camera, field of view and filters. It also shows whether a supported Target Scheduler assembly is ready.

NightSage always plans for the **currently active N.I.N.A. profile**.

## 4. Choose autonomy

- **Preview**: safest mode; review everything manually.
- **Create**: analysis may proceed to creation.
- **Autopilot**: discovery may choose, plan and create automatically.

Use Preview while learning or beta-testing.

## 5. Choose integration ambition

### Quick

Favors targets that can produce a compelling result in roughly 10 h or less. Slight overruns are allowed when justified.

### Balanced

No minimum. A 5 h or 8 h target remains fully valid. Longer projects up to roughly 20 h are allowed without penalty; substantially longer projects receive a soft penalty.

### Deep

Time is not a limiting factor. A 40 h project may be suggested if justified, but a perfect 6 h target remains equally eligible.

## 6. Plan a known target

Under **Plan a target**:

1. Enter a resolvable catalog name.
2. Add optional planning instructions if useful:
   - HDR core with 15 s / 60 s exposures
   - short RGB stars
   - preferred palette
   - desired total integration
3. Click **Analyze target**.

NightSage resolves the target through CDS Sesame, builds a strategy and validates it against the active setup.

## 7. Discover targets

Open **Discover targets**.

Under **Target types & result limit**, choose any combination of:

- Emission / HII
- Reflection / dark nebula
- Galaxy
- Planetary nebula
- SNR / WR shell
- Cluster / star field

Set a maximum result count from 1 to 30 and click **Find targets**.

Each result shows target family, catalog name, NightSage score, estimated integration, maximum altitude and dark/usable hours.

Select one and click **Build plan for selected target**.

Starting a new search resets stale framing from the previous target.

## 8. Read the plan

The plan preview includes target type, coordinates, integration ambition, framing geometry if already applied, integration per panel, project total, minimum altitude/session, priority, strategy summary and warnings.

### Exposure Template mapping

Each exposure row includes:

- filter
- sub-exposure duration
- desired frame count
- Target Scheduler template
- integration

An exact existing template is used unchanged.

For a missing special duration, the selector identifies the proposed base template. You may choose another compatible same-filter template before creation.

## 9. Frame the target

Click **Frame target**.

NightSage switches to **Framing** and loads the target into N.I.N.A.'s native Framing Assistant.

Use the normal N.I.N.A. controls to change image/survey source, center, field, rotation, horizontal/vertical panels, overlap and preserve alignment.

NightSage and the standard N.I.N.A. Framing Assistant tab share the same state.

## 10. Mosaics

For a mosaic, the native Framing Assistant creates the camera rectangles.

NightSage tracks each panel's name, RA, Dec and position angle.

When framing changes, NightSage marks the acquisition plan stale. Click **Recalculate plan**.

NightSage adopts the native panel geometry, recalculates the acquisition strategy and returns automatically to **Target & Plan**.

The integration summary distinguishes **Integration per panel** and **Project total**.

Example: 7 h per panel on a 2×2 mosaic becomes a 28 h project.

## 11. Create in Target Scheduler

When the plan is current, click **Create in Target Scheduler**.

NightSage:

1. checks for duplicate targets;
2. reuses exact Exposure Templates;
3. asks before creating missing/derived templates;
4. requests Target Scheduler to back up its database;
5. creates the project through Target Scheduler's own database interaction layer.

For mosaics, the project is marked as a mosaic and every N.I.N.A. framing rectangle becomes a Target Scheduler target with its exact coordinates and position angle.

## 12. Review before execution

After creation, open Target Scheduler and review the project before running it.

Pay particular attention to total project integration, per-filter balance, template selection, moon-avoidance settings, panel geometry and project state.

NightSage assists planning; it does not replace astrophotographer judgement.
