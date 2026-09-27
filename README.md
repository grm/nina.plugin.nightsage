# NightSage

**AI target discovery & imaging planning for N.I.N.A.**

NightSage uses the **currently loaded N.I.N.A. profile** as the one and only setup for every plan. It can work in two directions:

1. **Target → plan** — enter `M1`, `IC 434`, `NGC 6888`, etc. NightSage proposes the acquisition strategy for the active telescope/camera/filter set.
2. **Setup + next 7 days → targets → plan** — NightSage proposes one strong candidate per useful target class, ranks them, explains why, and can turn a selection into a full Target Scheduler project.

## V0.1 test scope

NightSage reads what it needs from the active profile: focal length, focal ratio, camera/pixel information, connected sensor dimensions where available, gain/offset/readout mode, filter wheel, and observing site. It calculates image scale, field of view and deterministic visibility/fit scores locally.

The LLM is used for the high-level astrophotography decision: target suitability, filter strategy, sub length, integration goal and moon/twilight strategy. Before anything is written, NightSage validates the returned plan against the actual active equipment. A requested filter that does not exist is never silently created as a fake N.I.N.A. filter.

## Autonomy modes

- **Preview** — analyze and show the plan; never write automatically.
- **Create** — analyzing a target can immediately create the project in Target Scheduler.
- **Autopilot** — discovery selects the highest-ranked recommendation, builds the plan and creates it automatically.

An explicit **Create in Target Scheduler** button is always available once a plan exists.

## LLM providers

Native adapters:

- OpenAI (Responses API; default model `gpt-5.6-terra`)
- Anthropic
- Google Gemini
- OpenAI-compatible endpoint

The OpenAI-compatible adapter is intentionally generic and can be pointed at Ollama, LM Studio, OpenRouter, Groq, or another compatible server. Model and endpoint are user-editable; NightSage does not lock planning to one vendor.

API keys are encrypted locally with Windows DPAPI for the current Windows user. Local endpoints can be used without a key when their server permits it.

## Target Scheduler integration

NightSage does **not** modify `schedulerdb.sqlite` with hand-written SQL. Target Scheduler 5.9.x currently exposes a read-only public HTTP API, so NightSage detects the already-loaded Target Scheduler assembly and calls its own database interaction layer by reflection. This keeps Target Scheduler optional and gives the integration a hard version gate.

For a new target NightSage can create project, target coordinates/rotation, missing exposure templates when a real configured N.I.N.A. filter exists, exposure plans and desired counts, project altitude/minimum-time/priority settings, moon/twilight settings, and active or draft project state.

Duplicate target names in the same active N.I.N.A. profile are blocked rather than silently duplicated.

## Requirements

- Windows x64
- N.I.N.A. 3.2.0.9001 or newer
- For automatic project creation: Target Scheduler 5.9.x
- Network access for cloud LLM providers; local OpenAI-compatible providers can run offline
- A correctly configured N.I.N.A. equipment profile. For Target Scheduler creation, exposure filters must correspond to actual N.I.N.A. filter definitions.

## Install a test build

1. Close N.I.N.A.
2. Extract the release ZIP into the NightSage plugin folder used by your N.I.N.A. installation.
3. Start N.I.N.A. and enable NightSage if required.
4. Go to **Options → Plugins → NightSage** and configure provider, model and API key/endpoint.
5. In the Imaging workspace, open the **NightSage** dock.
6. Start in **Preview** mode for the first test.

## Recommended first test

Load the exact N.I.N.A. profile you want to use, verify the setup summary, enter `M1`, click **Analyze target**, inspect filters/exposure times, confirm Target Scheduler is reported ready, then click **Create in Target Scheduler** and inspect the project before running it. Afterwards test **Find targets** for the next seven days.

## Development status

`0.1.0-alpha.1` is intentionally an integration test release. The Target Scheduler write adapter is version-gated to 5.9.x and fails closed if the expected internal API is not present. The LLM never receives or handles database objects directly; it returns a structured plan which NightSage validates and applies.

### AI-assisted development disclosure

The initial NightSage implementation was developed with substantial AI coding assistance. A human maintainer remains responsible for reviewing, testing and publishing releases, in line with N.I.N.A. plugin contribution expectations.

## License

MPL-2.0.
