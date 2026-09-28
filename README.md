# NightSage

**AI target discovery, native framing, mosaics and acquisition planning for N.I.N.A.**

[![CI](https://github.com/grm/nina.plugin.nightsage/actions/workflows/ci.yml/badge.svg)](https://github.com/grm/nina.plugin.nightsage/actions/workflows/ci.yml)

> **Status:** 0.2 public beta candidate. Start in **Preview** mode and review every plan before acquisition.

NightSage turns the **currently loaded N.I.N.A. profile** into a planning workspace. It can suggest what to shoot, build an acquisition strategy, let you compose the field in N.I.N.A.'s native Framing Assistant, handle mosaics, and create the resulting project in Target Scheduler.

NightSage is a community plugin and is not part of N.I.N.A. core.

![NightSage plan preview](docs/images/plan-preview.jpg)

## What NightSage does

- **Discover targets** for the next seven nights using the active telescope, camera, field of view, filters, observing site, visibility and selected integration ambition.
- **Plan a known target** from a catalog name such as `M1`, `IC 1396` or `NGC 6888`.
- **Choose target families** to include in discovery: emission/HII, reflection/dark nebulae, galaxies, planetary nebulae, SNR/WR shells and clusters/star fields.
- **Quick / Balanced / Deep** integration ambitions. These are nested preferences, not duration buckets: Deep still allows a perfect 6-hour target.
- **Reuse Target Scheduler Exposure Templates** as the technical source of truth. NightSage does not silently replace your gain, offset, binning, readout or moon-avoidance setup.
- **Native framing inside NightSage.** The Framing tab uses the same N.I.N.A. `IFramingAssistantVM` as the standard Framing Assistant.
- **Rotation and mosaics.** Change the center, rotation, panel grid and overlap, then recalculate the acquisition plan.
- **Create Target Scheduler projects**, including mosaic projects with one target per native N.I.N.A. camera rectangle.

## Workflow

```text
Active N.I.N.A. profile
        ↓
Discover a target or enter one manually
        ↓
Build / review acquisition plan
        ↓
Frame target
        ↓
Center / rotate / build mosaic in native Framing Assistant
        ↓
Recalculate plan
        ↓
Review integration + Exposure Template mapping
        ↓
Create in Target Scheduler
```

When framing changes after a plan has been calculated, NightSage marks the plan stale and blocks Target Scheduler creation until you recalculate it.

## Requirements

- Windows x64
- N.I.N.A. **3.2.0.9001 or newer**
- A correctly configured active N.I.N.A. profile
- An LLM provider:
  - OpenAI
  - Anthropic
  - Google Gemini
  - OpenAI-compatible endpoint
- Network access for cloud LLMs and CDS Sesame target-name resolution
- For automatic project creation: **Target Scheduler 5.9.x or 5.10.x**

Target Scheduler is optional for discovery/planning/framing. It is only required for the final project creation step.

## Install the beta

Until NightSage is accepted into the official N.I.N.A. plugin manifest repository, installation is manual:

1. Download the latest NightSage ZIP from **Releases**.
2. Close N.I.N.A.
3. Extract the ZIP into the N.I.N.A. plugin directory for your N.I.N.A. version.
4. Start N.I.N.A. and open **Options → Plugins → NightSage**.
5. Configure an AI provider in the **Settings** tab.
6. Start with **Autonomy mode = Preview**.

If you are upgrading an existing NightSage test build, replace the old plugin files while N.I.N.A. is closed.

## Quick start

1. Load the N.I.N.A. profile you actually want to use.
2. Open **Options → Plugins → NightSage → Target & Plan**.
3. Confirm the **Active setup** summary and Target Scheduler status.
4. Choose an **Integration ambition**:
   - **Quick** — favor compelling projects around 10 h or less.
   - **Balanced** — no minimum; allows moderate projects around 20 h.
   - **Deep** — no time-based penalty; long projects are allowed but never rewarded merely for being long.
5. Either:
   - enter a target and click **Analyze target**, or
   - choose target types and click **Find targets**.
6. Review the acquisition plan and Exposure Template mapping.
7. Click **Frame target**.
8. In the embedded N.I.N.A. Framing Assistant, adjust center, rotation, panels and overlap.
9. Click **Recalculate plan**.
10. Review per-panel and whole-project integration.
11. Click **Create in Target Scheduler**.

## Exposure Templates: user settings win

NightSage separates the **scientific acquisition plan** from the **technical equipment configuration**.

If an exact same-filter/same-duration Target Scheduler Exposure Template exists, NightSage reuses it unchanged.

If NightSage needs a special duration—for example a 60 s or 15 s HDR exposure—it proposes an existing same-filter template as the base. You can select another base. When a derived template is created, NightSage preserves the selected template's technical settings and changes only the intended duration.

Creation of missing/derived templates requires confirmation.

## Mosaics

The Framing tab is the native N.I.N.A. Framing Assistant embedded in NightSage. The embedded view and N.I.N.A.'s normal Framing Assistant tab share the same live state.

![NightSage embedded native Framing Assistant with a 2×2 mosaic](docs/images/framing-mosaic.jpg)

For a mosaic, NightSage reads the actual N.I.N.A. camera rectangles. After **Recalculate plan**:

- exposure totals are displayed **per panel**;
- project integration is calculated across all panels;
- Target Scheduler receives one mosaic project;
- every panel becomes a Target Scheduler target with its own RA, Dec and position angle.

## Autonomy modes

- **Preview** — build and review; never write automatically.
- **Create** — a completed analysis can proceed to project creation, with confirmation where required.
- **Autopilot** — discovery can choose a recommendation, build the plan and proceed automatically.

For first use and public-beta testing, **Preview** is strongly recommended.

## Providers and privacy

API keys are encrypted locally with Windows DPAPI for the current Windows user.

Cloud-provider planning can include target names/coordinates, active equipment/profile characteristics, configured filter names, observing-site coordinates, Target Scheduler Exposure Template metadata, optional user instructions and—when enabled—existing Target Scheduler target/project names.

NightSage itself has no telemetry. A local OpenAI-compatible endpoint can keep LLM planning context on-device. Target-name resolution uses CDS Sesame, and the native N.I.N.A. Framing Assistant may contact the selected survey/image service.

See [Privacy and data flow](docs/PRIVACY.md).

## Safety / review model

NightSage is a planning assistant. LLM output can be wrong or suboptimal. NightSage validates important deterministic constraints, but it cannot guarantee that a suggested integration, filter balance, moon strategy, framing or project duration is ideal for every optical system, sky condition or processing goal.

Review the plan before acquisition—especially on remote or unattended systems.

## Documentation

- [User guide](docs/USER_GUIDE.md)
- [Privacy and data flow](docs/PRIVACY.md)
- [Troubleshooting](docs/TROUBLESHOOTING.md)
- [Architecture](ARCHITECTURE.md)
- [Changelog](CHANGELOG.md)
- [Contributing](CONTRIBUTING.md)
- [Security](SECURITY.md)
- [Public release checklist](docs/PUBLIC_RELEASE_CHECKLIST.md)

## Development

```powershell
dotnet restore NINA.Plugin.NightSage.sln
dotnet build NINA.Plugin.NightSage.sln -c Release -p:Platform=x64
dotnet test tests/NINA.Plugin.NightSage.Tests/NINA.Plugin.NightSage.Tests.csproj -c Release -p:Platform=x64
```

The Target Scheduler adapter intentionally uses Target Scheduler's own database interaction layer through reflection; NightSage does not write raw SQL to `schedulerdb.sqlite`.

### AI-assisted development disclosure

NightSage has been developed with substantial AI coding assistance. The maintainer remains responsible for reviewing changes, testing builds and publishing releases.

## License

NightSage is licensed under the **Mozilla Public License 2.0 (MPL-2.0)**. See [LICENSE.txt](LICENSE.txt).
