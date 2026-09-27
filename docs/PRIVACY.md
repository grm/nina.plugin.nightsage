# Privacy and data flow

NightSage has **no telemetry** and does not run its own cloud service.

The data leaving your computer depends on which features/providers you use.

## LLM provider

If you configure a cloud LLM provider, NightSage sends planning context required for the current request.

Depending on the operation, this can include:

- target/catalog name and resolved coordinates
- active N.I.N.A. profile name
- telescope name, focal length and focal ratio
- camera name, sensor/pixel information, field of view and image scale
- configured filter names
- observing-site latitude, longitude and elevation
- Target Scheduler Exposure Template names and technical metadata
- optional user planning instructions
- discovery horizon and integration ambition
- existing Target Scheduler target/project names and completion state when **Use existing Target Scheduler targets during discovery** is enabled
- approved framing/mosaic panel coordinates and rotations when recalculating a framed plan

Do not use a cloud provider with information you are not comfortable sending to that provider.

A local OpenAI-compatible endpoint can keep LLM planning context on-device.

## API keys

Provider API keys are stored locally and encrypted with Windows DPAPI using the current Windows user account.

NightSage does not intentionally include API keys in prompts or logs.

## CDS Sesame

Catalog-name resolution uses the CDS Sesame service:

`https://cds.unistra.fr/cgi-bin/nph-sesame/`

The target name you ask NightSage to resolve is therefore sent to CDS.

## Framing Assistant survey/image sources

NightSage embeds N.I.N.A.'s native Framing Assistant. Loading a survey image can contact the source selected in N.I.N.A.

Those requests are made by N.I.N.A.'s Framing Assistant, not by the NightSage LLM provider layer.

## Target Scheduler

Target Scheduler access is local.

NightSage reads/writes through the loaded Target Scheduler plugin's database interaction layer. It does not upload Target Scheduler database contents to a NightSage service.

Only the planning metadata described above may be included in an LLM request.

## Logs

NightSage writes operational information and errors through N.I.N.A.'s logging facilities. Check logs before posting them publicly if target names, local paths or other context are sensitive to you.
