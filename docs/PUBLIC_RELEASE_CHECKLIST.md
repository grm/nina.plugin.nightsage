# Public release checklist

NightSage **0.2.0** (`0.2.0.6`) is the first stable Release-channel build.

## Already completed

- [x] Build and tests run in GitHub Actions on Windows
- [x] Versioned release ZIP
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
- [x] N.I.N.A. FeaturedImageURL plugin icon
- [x] Provider-specific API-key onboarding and provider setup documentation
- [x] Versioned release workflow that derives and creates the release tag from the built assembly
- [x] N.I.N.A. Release manifest generation from the final packaged build
- [x] Pending async results rejected/cancelled on profile or framing changes
- [x] Plans bound to their source profile before Target Scheduler writes
- [x] Non-finite LLM/plan numeric values rejected
- [x] Visibility calculations use N.I.N.A. astrometry primitives
- [x] Target Scheduler compatibility fails closed
- [x] Target Scheduler 5.9.x and 5.10.x compatibility checked for the APIs NightSage uses
- [x] No raw SQL writes
- [x] Target Scheduler database backup requested before writes
- [x] API keys protected with Windows DPAPI
- [x] Cloud data flow disclosed in-app and in documentation
- [x] No NightSage telemetry
- [x] Startup / embedded Framing Assistant regression fixed
- [x] Single-frame and 2×2 mosaic workflow exercised in N.I.N.A.
- [x] Repository visibility changed to Public
- [x] Maintainer smoke-tested the plugin before graduating from beta

## Official N.I.N.A. Release submission

- [x] Generate the archive and N.I.N.A. Release manifest from the same compiled output
- [x] Omit `-beta` so the manifest uses the Release channel
- [ ] Publish the `v0.2.0` tagged release and generated manifest
- [ ] Validate the generated manifest against the current `nina.plugin.manifests` schema / `gather.js`
- [ ] Submit the manifest to `isbeorn/nina.plugin.manifests` as `manifest.0.2.0.6.json`
- [ ] Address maintainer review feedback
- [ ] After acceptance, document the N.I.N.A. plugin-repository installation path in the README

## Post-release follow-up

- [ ] Verify README images, plugin icon and CI badge anonymously
- [ ] Verify the release ZIP can be downloaded without authentication
- [ ] Create a short stable-release announcement
