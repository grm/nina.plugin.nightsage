# NightSage V1 architecture

## Product boundary

NightSage is an acquisition-planning assistant, not a replacement scheduler. Target Scheduler remains responsible for deciding what to run at execution time. NightSage turns an active N.I.N.A. equipment profile plus either a requested target or a seven-day discovery window into validated Target Scheduler configuration.

## Flow

`Active N.I.N.A. profile -> SetupContext -> LLM -> structured ImagingPlan -> PlanValidator -> TargetSchedulerAdapter`

Discovery adds a deterministic scoring pass between the LLM candidate list and the UI.

## Trust boundary

The LLM never receives a database handle and never writes to N.I.N.A. or Target Scheduler. It can only return JSON. NightSage checks filter membership, coordinates, sensible numeric ranges, field fit and visibility before applying the plan.

## Target Scheduler adapter

Target Scheduler 5.9.x has a read-only HTTP API. NightSage therefore uses a reflection adapter against the loaded plugin assembly, with a strict 5.9.x version gate, and calls Target Scheduler's own `SchedulerDatabaseInteraction.GetContext()` methods rather than issuing raw SQL against `schedulerdb.sqlite`.

Creation order:

1. Reuse matching exposure templates where possible.
2. Create missing templates only for actual configured N.I.N.A. filters.
3. Build Project -> Target -> ExposurePlan object graph.
4. Persist through `AddNewProject` so Target Scheduler owns relational persistence.

## Providers

`ILLMProvider` is the stable interface. Native implementations exist for OpenAI Responses, Anthropic Messages, Gemini GenerateContent, and generic OpenAI-compatible Chat Completions. New providers should be adapters only; planner prompts and validation are provider-independent.
