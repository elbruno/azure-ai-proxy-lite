# Load testing

Two load tests are available:

- `loadtest/copilot_responses_load_test.py` models simultaneous GitHub Copilot repository sessions
  using the streaming Responses API.
- `loadtest/openai_proxy load test.jmx` is the older JMeter plan for generic HTTP throughput tests.

## TL;DR

1. Install the harness dependency:

    ```shell
    python -m pip install -r loadtest/requirements.txt
    ```

2. Set `PROXY_API_KEY` to an active test attendee key.
3. Run the synchronized Copilot profile:

    ```shell
    python loadtest/copilot_responses_load_test.py \
      --proxy-url https://<proxy-host>/api/v1 \
      --model gpt-5-mini \
      --concurrency 10 25 50 \
      --prompt-chars 32000 \
      --max-output-tokens 4096 \
      --reasoning-effort high \
      --stage-pause-seconds 65
    ```

4. Require 100% `response.completed` at the target concurrency; HTTP 200 by itself is not success.
5. Compare request/token totals and throttling with Azure Monitor.

**Success:** all target stages complete without `response.failed`, `response.incomplete`, missing
terminal events, or rate-limit errors.

## Before you start

Use a non-production event key, confirm the test model and event caps, and ensure the subscription
has enough quota to run the planned profile.

## Copilot repository-session stress test

The Python harness sends synchronized streaming requests to `/api/v1/responses`. Each virtual user
receives approximately 32K characters of repository context, uses high reasoning effort, and allows
up to 4096 output tokens. It records HTTP status, Responses API error events, latency, time to first
byte, and token usage. It never writes credentials, prompt contents, or model responses to the
results file.

Install the existing load-test dependency:

```bash
python -m pip install -r loadtest/requirements.txt
```

Set an active event API key without putting it on the command line:

PowerShell:

```powershell
$env:PROXY_API_KEY = "<event-api-key>"
```

Bash:

```bash
export PROXY_API_KEY="<event-api-key>"
```

Run the three staged concurrency levels:

```bash
python loadtest/copilot_responses_load_test.py \
  --proxy-url https://<proxy-host>/api/v1 \
  --model gpt-5-mini \
  --concurrency 10 25 50 \
  --prompt-chars 32000 \
  --max-output-tokens 4096 \
  --reasoning-effort high \
  --stage-pause-seconds 65
```

The pause isolates stages across Azure OpenAI's one-minute RPM/TPM windows. A successful streaming
request can still use HTTP 200 and later emit a `response.failed` event, so the harness parses the
SSE events and requires `response.completed` instead of treating every HTTP 200 as success.
`response.incomplete` and streams that end without a terminal event are reported as failures.

### Measured results

The following test used `gpt-5-mini`, one 0.75-vCPU/1.5-GiB Container App replica, and 6784 input
tokens per virtual user.

| Model capacity | Concurrent users | Success rate | Successful latency p95 | Result |
|---:|---:|---:|---:|---|
| 100K TPM / 100 RPM | 10 | 100% | 21.7 s | Pass |
| 100K TPM / 100 RPM | 25 | 52% | 21.2 s | 12 `rate_limit_exceeded` events |
| 100K TPM / 100 RPM | 50 | 26% | 22.7 s | 37 `rate_limit_exceeded` events |
| 600K TPM / 600 RPM | 10 | 100% | 27.4 s | Pass |
| 600K TPM / 600 RPM | 25 | 100% | 24.1 s | Pass |
| 600K TPM / 600 RPM | 50 | 100% | 26.6 s | Pass |

At 50 users, the successful run processed 339,200 input tokens and 102,211 output tokens
(441,411 total). Container App CPU remained low, so Azure OpenAI TPM—not proxy compute—was the
limiting resource. For this profile, use at least 600K TPM for 50 simultaneous sessions and retain
headroom for retries and unrelated traffic.

Across the complete successful 10/25/50 sequence, Azure Monitor recorded 85 requests, 576,640 input
tokens, 174,044 output tokens, and zero errors. The single Container App replica peaked at 2.5% CPU
and 8% memory.

Scale an existing Global Standard deployment by recreating it with the same model/version and a
higher capacity:

```bash
az cognitiveservices account deployment create \
  --resource-group <resource-group> \
  --name <foundry-account> \
  --deployment-name gpt-5-mini \
  --model-format OpenAI \
  --model-name gpt-5-mini \
  --model-version 2025-08-07 \
  --sku-name GlobalStandard \
  --sku-capacity 600
```

Raw reports are committed as:

- `loadtest/copilot_responses_results_100k.json`
- `loadtest/copilot_responses_results_600k.json`

## Verify

Check the JSON report for a 100% success rate at each required stage. Confirm Azure Monitor request
and token counts cover the same test window and that Container App CPU/memory do not indicate a
separate proxy bottleneck.

## Troubleshooting

| Symptom | Fix |
|---|---|
| HTTP 200 counted as success but the request failed | Use this harness; it parses terminal SSE events and requires `response.completed`. |
| `rate_limit_exceeded` | Increase Azure model TPM/RPM or reduce concurrency/context/output limits. |
| 401 | Use an active event attendee key and confirm the event time window. |
| 404 | Use the exact event model ID and a proxy endpoint ending in `/api/v1`. |
| Missing terminal event | Treat the stream as failed and inspect proxy/upstream logs for cancellation or timeout. |
| Proxy metrics show zero streaming tokens | Confirm the deployed proxy includes terminal SSE usage accounting. |

## JMeter throughput test

1. You'll need to update the URL in the `HTTP Request Defaults` element to point to your REST API endpoint.

    ![update url](../../media/jmeter_requests.png)

2. You'll need to update the `HTTP Header Manager` element to include your event code.

    ![update event code](../../media/jmeter-request-header.png)

### Example load test

![](../../media/example_perf_jmeter.png)

## Next step

[Monitor the rehearsal or live event](../../reporting.md).
