# Publishing NightSage to the official N.I.N.A. plugin repository

The workflow `.github/workflows/publish-nina-manifest.yml` runs when a GitHub Release is published. It also supports manual dispatch with an existing release tag (useful for submitting the first stable release).

## One-time setup

1. Fork [isbeorn/nina.plugin.manifests](https://github.com/isbeorn/nina.plugin.manifests) into `grm/nina.plugin.manifests`.
2. Create a fine-grained GitHub PAT with **Contents: Read and write** on that fork and permission to create PRs in the upstream repository. Depending on GitHub's permission model, upstream PR creation may require a classic PAT with `public_repo` scope instead.
3. Add it to this repository's Actions secrets as `NINA_MANIFEST_PAT`.
4. Ensure Actions are allowed to run and the fork permits pushes.

## Behavior

- **Stable GitHub Release:** the generated manifest is submitted without a `Channel` property (Release channel).
- **GitHub prerelease:** the generated manifest is submitted with `"Channel": "Beta"`.
- The workflow downloads the *existing* ZIP and manifest attached to the release; it does not rebuild or recompute the checksum.
- The archive checksum and installer URL are checked against the actual release assets.
- It validates the manifest using the upstream `gather.js`, pushes a versioned branch to your fork, and opens a PR to `isbeorn/nina.plugin.manifests`.
- A maintainer must still review and merge the PR. No upstream changes are merged automatically.

**Important:** The existing `ci.yml` builds and publishes stable releases on `main`. To publish a beta, first create a GitHub prerelease with a matching ZIP and generated manifest. Merely naming a tag `-beta` is not sufficient; the GitHub Release must have its **prerelease** flag enabled.

For the already-published stable release, use **Actions → Submit N.I.N.A. plugin manifest → Run workflow** and supply the existing tag (for example `v0.2.0`).

The official repository may require additional review or a different path depending on the supported N.I.N.A. major version. The workflow currently targets `manifests/n/NightSage/3.0.0`, matching the project's existing CI manifest validation path.
