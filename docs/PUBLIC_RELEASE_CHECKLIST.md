# Public release checklist

NightSage 0.2.0-beta.5 is the current public-beta candidate.

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
- [x] Provider-specific API-key onboarding and provider setup documentation
- [x] Versioned release workflow that derives and creates the release tag from the built assembly
- [x] N.I.N.A. beta manifest generation from the final packaged build
- [x] Target Scheduler compatibility fails closed
- [x] Target Scheduler 5.9.x and 5.10.x compatibility checked for the APIs NightSage uses
- [x] No raw SQL writes
- [x] Target Scheduler database backup requested before writes
- [x] API keys protected with Windows DPAPI
- [x] Cloud data flow disclosed in-app and in documentation
- [x] No NightSage telemetry
- [x] Startup / embedded Framing Assistant regression fixed
- [x] Single-frame and 2×2 mosaic workflow exercised in N.I.N.A.

## Repository publication

- [x] Repository visibility changed to Public
- [x] Commit author email history rewritten to the GitHub noreply address
- [x] Basic repository scan found no committed API key, token, private key, private endpoint or old personal email
- [ ] Install the beta ZIP on a clean/fresh N.I.N.A. plugin directory and run one smoke test
- [ ] Confirm screenshots contain nothing the maintainer considers private
- [ ] Add a concise GitHub repository description/topics

## Official N.I.N.A. beta submission

- [x] Generate the archive and N.I.N.A. beta manifest from the same compiled output
- [x] Include `Channel: Beta` through the official manifest-generation script
- [ ] Publish the 0.2.0-beta.5 tagged prerelease and generated manifest
- [ ] Validate the generated manifest against the current `nina.plugin.manifests` schema / `gather.js`
- [ ] Submit the manifest to `isbeorn/nina.plugin.manifests`
- [ ] Address maintainer review feedback
- [ ] After acceptance, document the N.I.N.A. beta-repository installation path in the README

## Public beta follow-up

- [ ] Verify README images and CI badge anonymously
- [ ] Verify the prerelease ZIP can be downloaded without authentication
- [ ] Create a small public beta announcement and ask testers to use Preview mode first
- [ ] Collect N.I.N.A. / Target Scheduler compatibility reports

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
