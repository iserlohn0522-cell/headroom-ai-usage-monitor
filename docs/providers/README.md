# Allowance provider contract v1

Headroom accepts credential-free **local JSON snapshots**. A producer owns authentication and writes a snapshot; Headroom validates and displays it. Headroom never runs code named in a manifest, follows URLs in a snapshot, or asks a custom provider for tokens.

The stable boundary is JSON, not the internal C#/Python classes. Additive unknown fields are ignored. A breaking change requires a new integer `schemaVersion`; unrecognized versions fail visibly. Both frontends implement v1 and consume the same conformance cases in `tests/allowance-cases.json`.

## Manifest

```json
{"schemaVersion":1,"providers":[{"id":"demo-plan","path":"mock.json"}]}
```

At most eight providers. IDs are unique lowercase ASCII slugs (40 characters maximum); `codex` and `claude` are reserved. Paths are relative JSON filenames below the manifest directory; absolute, escaping and UNC paths are rejected. Keep this directory under your control. Each snapshot's `providerId` must match the entry.

## Snapshot

```json
{
  "schemaVersion": 1,
  "providerId": "demo-plan",
  "displayName": "Demo Plan",
  "observedAt": "2026-10-02T12:00:00Z",
  "status": "ok",
  "windows": [
    {"id":"weekly","label":"7d","support":"supported",
     "remainingPercent":68,"state":"fresh",
     "reset":{"kind":"rolling","at":"2026-10-06T12:00:00Z"}},
    {"id":"five-hour","label":"5h","support":"unsupported"}
  ]
}
```

The [JSON Schema](allowance-provider-v1.schema.json) documents the shape. Runtime checks additionally reject duplicate window IDs, control characters, identity mismatches, non-finite numbers and files larger than 1 MiB. At most 16 windows per provider; lists use pages of six rows. Every supported window remains selectable for the ball.

| Field | Meaning |
|---|---|
| `observedAt` | Successful observation time, with ISO 8601 timezone. Reading an unchanged file must not refresh it. |
| `status` | `ok`, `loading`, or `error`. An error may carry last known values and their original observation time. |
| `support` | `supported`: window exists; `unsupported`: not offered; `unknown`: capability not established. |
| `remainingPercent` | Optional finite number in [0,100], only on supported windows. **0 is exhausted; null is not zero.** |
| `state` | Optional `fresh` (default), `loading`, `stale`, `error`, or `unknown`. Unknown state cannot carry a balance. |
| `reset.kind` | `fixed`, `rolling`, or `unknown`, as reported by the provider. |
| `reset.at` | Optional provider reset instant, with timezone. Never computed by adding the window duration or rolling a past reset into tomorrow. |

Use `five-hour` and `weekly` for those exact windows. Other stable IDs and human labels are supported. Do not represent credits, paid fallback, or another metered bucket as included weekly allowance.

## Display and failure rules

- Unsupported windows have no bar/ring. Unknown data has no numeric fill. With no known windows the panel shows one service status.
- The ball shows **one identified provider/window**, never an average. Auto selection prefers supported five-hour, then weekly, then the first other supported window.
- A weekly-only snapshot selects weekly and explains the absent 5h window. An unavailable saved preference falls back visibly. Missing never becomes 0 or 100.
- A valid zero is `0% / Exhausted`. Nonzero fractions below 1% remain decimal percentages.
- Cached loading/error/stale values carry `~` and muted patterned tracks. Freshness uses `observedAt`, not last attempted read. A failed file read retains the previous snapshot with a read error.
- After twice the configured refresh interval (minimum two minutes), valued data is stale. Observations over five minutes in the future are errors. Later successful reads recover automatically.
- Files are re-read every 10 seconds and on manual refresh. Producers should write a same-directory temporary file and atomically replace the snapshot.

## Safe mock

```shell
python examples/providers/mock_provider.py --out .local-demo --scenario weekly-only
python -m portable.app --providers .local-demo/manifest.json
```

Windows native, with synthetic built-in data and isolated settings:

```powershell
$env:HEADROOM_SETTINGS_PATH = (Join-Path $PWD ".local-demo/settings.json")
.\debug\Headroom.fixture.exe --fixture .\docs\fixtures\06-pro-weekly-only --providers .\.local-demo\manifest.json
```

Native **Visible quotas** can disable both built-ins for a custom-only widget. **Disconnect local providers** removes their configuration without deleting files or enabling a built-in. CLI manifest paths are retained in native settings; use an isolated settings path for demos.

Re-run with `--state exhausted`, `loading`, `stale`, `error`, or `unsupported`. The mock uses no network, credentials or coding model. Checked-in `mock.json` has a fixed timestamp and becomes stale; generate a fresh example when needed.

## Optional Codex adapter

Portable `--codex-cli` explicitly opts into an existing installed, signed-in Codex CLI. It sends only `initialize`, `initialized`, and `account/rateLimits/read` over stdio. It never starts a thread/turn, performs login, supplies tokens, buys credits, consumes resets, or requests grants. Stdout is bounded; stderr and RPC error bodies are not logged. The process stops after the read. Tested with a fake process and representative schema, **not a live account**.

It uses `rateLimitsByLimitId.codex` when available, otherwise the compatibility `rateLimits` view. Durations, percentages and reset instants come from that bucket; other buckets and credits are not aggregated.

Checked 2026-10-02: [official app-server schema](https://developers.openai.com/codex/app-server/) (`account/rateLimits/read`, `usedPercent`, `windowDurationMins`, `resetsAt`); [official Pro allowance guidance](https://help.openai.com/en/articles/20001516-managing-usage-with-gpt-6-astra-in-work-and-codex). Capabilities take precedence over plan labels.
