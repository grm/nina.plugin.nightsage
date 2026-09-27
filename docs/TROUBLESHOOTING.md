# Troubleshooting

## Target Scheduler says "waiting to load"

Target Scheduler is loaded lazily by N.I.N.A.

NightSage checks periodically and should switch to `ready` automatically after Target Scheduler is loaded. If it remains unavailable:

1. confirm Target Scheduler is installed and enabled;
2. open its UI once;
3. return to NightSage;
4. click **Refresh** if necessary.

Public beta write support covers Target Scheduler **5.9.x and 5.10.x**.

## Target Scheduler version is unsupported

NightSage fails closed rather than writing through an unverified internal API.

Planning/framing still work. Project creation remains disabled until that Target Scheduler version is validated.

Open a compatibility issue and include the exact Target Scheduler version.

## Provider timeout

Cloud providers can have variable latency or rate limiting.

NightSage uses a 180-second HTTP timeout and retries once for timeout, HTTP 429 and HTTP 5xx.

If it continues, retry later, test the provider from Settings, try another model/provider, or check provider quota/rate limits.

## "CDS Sesame could not resolve"

Use a standard catalog name such as `M1`, `NGC 6888`, `IC 1396` or `Sh2-129`.

The resolver requires network access to CDS.

## Wrong/stale framing appears

Starting a new manual target, discovered target or Find targets search resets the shared framing state.

If you intentionally changed the standard N.I.N.A. Framing Assistant, remember that NightSage shares the same framing VM.

## Plan says framing changed / Create is disabled

This is intentional.

If center, rotation, panels or overlap change after the acquisition plan was calculated:

1. go to **Framing**;
2. confirm the geometry;
3. click **Recalculate plan**;
4. NightSage returns to **Target & Plan**;
5. review and create.

## Embedded Framing Assistant is unavailable

Use **Open native tab**.

NightSage shares the same `IFramingAssistantVM`, so the framing state remains usable even if a future N.I.N.A. UI change prevents the embedded view from loading.

Please report your exact N.I.N.A. version.

## A filter is rejected

NightSage only uses actual configured N.I.N.A. filter names.

For OSC/no physical filter wheel with Target Scheduler, configure an appropriate named dummy filter in N.I.N.A.; the physical wheel can remain disconnected.

## NightSage wants to create a new Exposure Template

NightSage first tries to use an exact existing same-filter/same-duration template.

A new/derived template is normally proposed only when the plan requests a duration that is not already available.

Review the selector and choose another same-filter base if appropriate. NightSage asks for confirmation before creation.

## Mosaic total looks much larger

The plan displays integration **per panel** and **project total**.

A 2×2 mosaic with 8 h per panel is a 32 h project. This is expected.

## Reporting a bug

Include N.I.N.A. version, NightSage version, Target Scheduler version if relevant, provider/model (never the API key), active setup summary, exact steps, screenshot and relevant N.I.N.A. log excerpt.
