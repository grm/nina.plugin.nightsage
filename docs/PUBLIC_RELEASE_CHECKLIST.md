# Public release checklist

NightSage 0.2.0-beta.1 is the first public-beta candidate.

## Already completed

- [x] Build and tests run in GitHub Actions on Windows
- [x] Versioned prerelease ZIP
- [x] MPL-2.0 license
- [x] Public-facing README
- [x] User guide
- [x] Privacy/data-flow documentation
- [x] Troubleshooting guide
- [x] Architecture document
- [x] Contributing guide
- [x] Security policy
- [x] Bug and feature issue templates
- [x] N.I.N.A. ShortDescription / Changelog metadata
- [x] N.I.N.A. screenshot metadata
- [x] Target Scheduler compatibility fails closed
- [x] Target Scheduler 5.9.x and 5.10.x compatibility checked for the APIs NightSage uses
- [x] No raw SQL writes
- [x] Target Scheduler database backup requested before writes
- [x] API keys protected with Windows DPAPI
- [x] Cloud data flow disclosed in-app and in documentation
- [x] No NightSage telemetry
- [x] Startup / embedded Framing Assistant regression fixed
- [x] Single-frame and 2×2 mosaic workflow exercised in N.I.N.A.

## Before changing the repository to Public

- [ ] Install the beta ZIP on a clean/fresh N.I.N.A. plugin directory and run one smoke test
- [ ] Confirm no API key, token, private endpoint or private user data is committed
- [ ] Confirm screenshots contain nothing the maintainer considers private
- [ ] Add a concise GitHub repository description/topics after visibility is public

## After changing the repository to Public

- [ ] Verify README images and CI badge anonymously
- [ ] Verify the prerelease ZIP can be downloaded without authentication
- [ ] Create a small public beta announcement and ask testers to use Preview mode first
- [ ] Collect N.I.N.A. / Target Scheduler compatibility reports
- [ ] Prepare the contribution/submission to the official N.I.N.A. plugin manifest repository when the beta is stable

## Suggested first public beta test

1. Install NightSage on N.I.N.A. 3.2.
2. Configure one supported provider.
3. Test provider.
4. Find targets in Preview mode.
5. Build one single-field plan.
6. Frame and recalculate it.
7. Build one 2×2 mosaic and recalculate it.
8. Create the project in Target Scheduler.
9. Confirm exposure templates, panel coordinates/rotation, per-panel integration and whole-project integration.

Do not publish secrets or API keys in test reports.
