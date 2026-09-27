# Contributing to NightSage

Contributions, bug reports and compatibility testing are welcome.

## Before opening an issue

For bugs, collect N.I.N.A. version, NightSage version, Target Scheduler version if involved, LLM provider/model, reproduction steps, screenshot and a relevant log excerpt.

Never include API keys.

## Development setup

Requirements: Windows, .NET 8 SDK and a N.I.N.A. plugin development environment.

```powershell
dotnet restore NINA.Plugin.NightSage.sln
dotnet build NINA.Plugin.NightSage.sln -c Release -p:Platform=x64
dotnet test tests/NINA.Plugin.NightSage.Tests/NINA.Plugin.NightSage.Tests.csproj -c Release -p:Platform=x64
```

## Design rules

- the active N.I.N.A. profile is the setup;
- the LLM proposes strategy but never writes directly to N.I.N.A./Target Scheduler;
- deterministic validation happens before write actions;
- existing Target Scheduler Exposure Templates are the technical source of truth;
- avoid hardcoding a single astrophotography style or equipment setup;
- framing uses N.I.N.A.'s native Framing Assistant state rather than a parallel geometry implementation;
- Target Scheduler writes go through its own interaction layer, never raw SQL;
- unsupported integration versions fail closed.

## Pull requests

Keep PRs focused. Add or update tests for deterministic logic. Document user-visible behavior changes in `CHANGELOG.md`.

Do not change the plugin GUID or exported type/namespace identity after public publication without a migration plan.

AI-assisted code is welcome, but contributors remain responsible for understanding, reviewing and testing what they submit.
