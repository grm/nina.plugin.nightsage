# Changelog

## 0.1.0-alpha.1 — 2026-09-27

First test release.

- Read the currently loaded N.I.N.A. profile and connected camera information.
- Build full acquisition plans from a target name.
- Discover and rank targets suitable for the current setup over the next seven days.
- One candidate per useful target category: emission nebula, reflection/dark nebula, galaxy, planetary nebula, cluster/broadband.
- Deterministic validation of target coordinates, filter availability, field fit and visibility scoring.
- OpenAI, Anthropic, Google Gemini and OpenAI-compatible provider backends.
- OpenAI-compatible backend supports local and gateway providers such as Ollama, LM Studio, OpenRouter, Groq and compatible Mistral endpoints.
- Preview, Create and Autopilot modes.
- Target Scheduler 5.9.x integration without a compile-time dependency.
- Create Project → Target → Exposure Templates → Exposure Plans and constraints.
- Read current Target Scheduler targets for continuation-aware recommendations and duplicate protection.
- API keys stored locally using Windows DPAPI.
