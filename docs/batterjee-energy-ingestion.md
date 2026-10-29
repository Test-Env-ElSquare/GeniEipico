# Energy ingestion: the Production pattern

Device -> IngestController.StoreAndExtractAsync -> TelemetryRaw -> TelemetryTags
-> EnergyProjectionService (background) -> IEnergyRecordWriter -> EnergyReadings.

The former synchronous BatterjeeEnergyIngestionService and BatterjeePowerController
were removed. No business table is written by the energy HTTP action anymore.
Raw-to-tags extraction runs inside the ingestion request, as it does for Production;
an arbitrary SQL INSERT into TelemetryRaw alone does not trigger extraction.

## Generic behavior

- Any registered transformer name is supported, under any active factory/customer.
  Name must resolve uniquely; deleted/ambiguous definitions are not projected.
- Match TelemetryTag.DeviceId to Transformer.TransformerName, then use numeric Id.
- CustomerId comes from the transformer's factory. No fixed factory/customer IDs.
- Transformer has no LineId: when its factory has exactly one active line, use it;
  otherwise save LineId=null. Explicit multi-line transformer assignment is future work.
- Group by source + RecordedAt. Require all 27 field names V/I/Pf/Pact/Preact/Papp/
  Energy/THDv/THDi, each with suffix 1..3; incomplete groups survive batch boundaries.
  Missing fields leave the signal pending, as required fields do for Production.
  Nonnumeric/blank values retain their raw/tag representation and map to null.
- The worker polls every second by default. Registered using AddHostedService.
- Every new signal appends; replaying a transformer/timestamp is skipped using a
  transaction-owned lock and the existing unique index. Existing rows are never updated.
- Timestamp is copied from tags, Quality=null. No time conversion in the worker.
- Watermark is in memory, as in Production. Restart replays tags from the beginning
  safely. Define transformers before sending; otherwise restart after adding definitions
  to reconsider old skipped tags. Pending groups are bounded to 10,000, with warning
  on eviction; source tags remain stored. This is not a durable queue redesign.

## Legacy Batterjee adapter / deployment

1. Run scripts/define_batterjee_energy_transformers.sql (only definition inserts,
   validates factory 5/customer 3/line 13; never overwrites existing definitions).
2. Publish API. No migration is required. Production ingestion is unchanged.
3. Adapter: GET /GeniPark/Batterjee/energy under IIS, /Batterjee/energy locally.
   It forwards the original query through StoreAndExtractAsync and timestamps this
   legacy payload at UTC+3, matching its old endpoint. All existing generic ingestion
   timestamp semantics stay unchanged; generic callers may supply ReceivedAt.
4. Response stays 'Energy Signal Saved', acknowledging raw/tag ingestion ONLY.
   EnergyReadings appears asynchronously once all required tags have arrived and
   the transformer/factory definition is valid. Check logs for projection errors.
5. Generic clients can use POST /v1/ingest/telemetry (or batch) with their sourcePath,
   payload and ReceivedAt; no Batterjee URL or names required by the energy worker.

Do not configure a second mapping writer for the same energy signals. No historical
copy from BatterjeeRealtime and no Overview energy/view changes were made.

## IIS cutover still requires the actual public URL/current rule

The old controller proves app-relative /api/source, not the actual device URL.
Obtain the current Production rewrite or IIS device request path before activation.
If the actual public path is /BatterjeeRealTime/api/source, a site-level rule may
match ^BatterjeeRealTime/api/source/?$ and proxy to
http://localhost/GeniPark/Batterjee/energy with appendQueryString=true and
stopProcessing=true. Confirm host binding/port and rule placement before using it.
Use Rewrite, not Redirect; never replace the whole web.config or Production rule.
Back up rules and disable only the new rule to roll back. Restrict ingestion to
trusted device/gateway IPs at IIS/firewall; legacy device identifiers are not auth.
Disable caching for this write-through GET. No IIS settings were changed here.

ARR reference: https://learn.microsoft.com/en-us/iis/extensions/url-rewrite-module/reverse-proxy-with-url-rewrite-v2-and-application-request-routing
