# Security policy

## Supported versions

Security fixes are applied to the latest NightSage beta/release line.

## Reporting a vulnerability

Please avoid publishing a security-sensitive issue containing secrets or exploit details.

Contact the maintainer privately through the GitHub account associated with this repository, then provide affected NightSage/N.I.N.A. versions, impact and reproduction details.

## Secrets

Never include LLM API keys in issues, logs, screenshots or pull requests.

NightSage stores configured API keys with Windows DPAPI for the current user.

## Target Scheduler writes

NightSage intentionally version-gates the Target Scheduler integration, asks Target Scheduler to back up its database before writes, calls Target Scheduler's own database interaction layer and does not issue raw SQL.

If compatibility is uncertain, NightSage disables write support instead of guessing.
