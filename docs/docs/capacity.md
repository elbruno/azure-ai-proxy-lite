# Capacity planning

This is particularly important when running a workshop with many concurrent attendees. This is less important for long running hacks where model requests are spread over a longer period and therefore unlikely to exceed the model deployment per minute limits.

## TL;DR

1. Estimate peak simultaneous users, prompts per user per minute, reserved input tokens, and maximum
   output tokens.
2. Calculate:

    ```text
    Required RPM = users x prompts per user per minute
    Required TPM = users x prompts per user per minute x (input tokens + max output tokens)
    ```

3. Compare both numbers with the deployment's Azure RPM and TPM limits; the smaller margin is the
   practical bottleneck.
4. Add headroom for retries and unrelated traffic, then scale the model deployment before the event.
5. Run the synchronized 10/25/50-user [Copilot load test](20-service-installation/70-testing/20-load-testing.md)
   using the real event model and configuration.
6. Confirm completed Responses API events and compare proxy results with Azure Monitor.

**Success:** every planned stage completes without `rate_limit_exceeded`, `response.failed`, or
`response.incomplete`, and Azure Monitor shows remaining TPM/RPM headroom.

## Before you start

Know the expected attendance, client, average prompt/context size, maximum output tokens, prompt
frequency, and the TPM/RPM assigned to each model deployment.

When running a workshop, you need to ensure that you have enough capacity from the model(s) underpinning the workshop.

There are two model deployment limits you need to be aware of:

- Tokens per Minute Rate Limit.
- Number of requests per minute.

![image shows the model deployment config](./media/model_deployment.png)

## What happens if limits are exceeded

The model will start rate limiting requests and it will not accept new prompts for approx. 20 seconds. This will affect everyone in the workshop as they are sharing the same model, and it will not be a great user experience.

## How to calculate required deployment capacity

These calculations are based on the following assumptions:

- 100 attendees at a workshop.
- Attendees are generating 4 prompts per minute.
- Each prompt requires 200 tokens to successfully complete.

Calculate the needed Tokens per minute using the following formula:

**Required Tokens per Minute = Number of Attendees * Number of Prompts per Minute * Number of Tokens per Prompt**

This calculation is very generous, as it's unlikely attendees would collectively be generating that number of prompts every minute for the whole workshop, but it's better to overestimate than underestimate.

For this example, a model deployment would need at least:

1. `Tokens per Minute Rate Limit` of 80K TPM.
1. `Number of requests per minute` of 400.

Use the maximum requested output size in planning, not only expected billed usage. Clients reserve
capacity from the deployment limits based on the request shape.

## What is the Max Token Cap parameter

When you create an event, you will set the `Max Token Cap` parameter. The Max Token Cap limits the
output tokens per request to a realistic number required for the prompt to complete successfully.
This prevents clients from reserving more deployment capacity than the workload needs. Billing is
based on the actual tokens consumed.

What would happen if there was no Max Token cap? If 20 attendees decided to set the Max response to 4000 via the Foundry Toolkit or SDKs, that would be 80000 TPM, multiply by 4 prompts per minute = 240000 TPM, you'll quickly run out of capacity impacting everyone in the workshop.

So, the Max Token Cap limits the Max response size for requests made via the Foundry Toolkit and developer SDKs.

## Scaling capacity

If the subscription has quota available, update the model deployment capacity in the Azure portal
or recreate the deployment with a higher SKU capacity. If quota is unavailable, request a quota
increase or use another suitable region/model deployment.

## Tested Copilot App profile

The repository-session stress test used 6784 input tokens per virtual user, high reasoning,
streaming Responses API calls, and up to 4096 output tokens.

| Deployment capacity | 10 users | 25 users | 50 users |
|---|---:|---:|---:|
| 100K TPM / 100 RPM | 100% success | 52% success | 26% success |
| 600K TPM / 600 RPM | 100% success | 100% success | 100% success |

At 100K TPM, failures were Azure `rate_limit_exceeded` events. At 600K TPM, all 85 requests across
the three stages completed. The single proxy replica peaked at approximately 2.5% CPU and 8% memory,
so Azure model quota—not Container App compute—was the limiting resource for this profile.

These measurements are a starting point, not a universal guarantee. Repeat the test with the event's
actual model, context size, and output limit.

## Rule of thumb

- For a GitHub Copilot App workshop, use the tested 4096 maximum output tokens only when the model
  deployment has enough TPM for the intended concurrency.
- For a multi-hour or multi-day hack, you might set the `Max Token Cap` to 4000 and a `Daily Request Cap` of 5000. Capacity planning is less critical for longer running hacks as the requests are spread over a longer period.

The Copilot maximum prompt tokens, event Max Token Cap, and Azure TPM are different controls:

| Control | Purpose |
|---|---|
| Copilot maximum prompt tokens | Local client context budget; 80000 avoids rejecting large repository contexts before a request is sent |
| Event Max Token Cap | Maximum output tokens allowed for one attendee request |
| Azure TPM/RPM | Shared per-minute model deployment capacity across attendees |

## Verify

Run the load-test stages and require 100% `response.completed` results at the target concurrency.
Then confirm Azure Monitor request/token counts agree with the proxy report and show acceptable
headroom.

## Troubleshooting

| Symptom | Fix |
|---|---|
| HTTP 200 streams later fail | Parse the SSE terminal event; only `response.completed` is success. |
| `rate_limit_exceeded` or 429 | Increase model TPM/RPM, reduce concurrency/request size, or distribute traffic. |
| Copilot rejects the request before proxy logs appear | Raise Copilot maximum prompt tokens to 80000 or disable unused local context sources. |
| Proxy CPU or memory is high | Scale Container App replicas/resources and repeat the same workload. |
| Token totals appear lower than expected | Compare requested limits, actual usage, event caps, and the complete terminal SSE usage event. |

## Next step

[Run the Copilot repository-session load test](20-service-installation/70-testing/20-load-testing.md).
