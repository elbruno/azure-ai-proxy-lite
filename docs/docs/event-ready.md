# Deploy to event-ready

Use this journey when preparing the Azure AI Proxy for a workshop, hackathon, or other event.

## TL;DR

1. [Deploy the three services](deployment/azure.md) with `azd up` and verify proxy, admin, and
   registration health.
2. Deploy the required Foundry/OpenAI models and [configure Managed Identity](deployment/managed_identity.md).
3. [Add active proxy resources](resources.md) using exact upstream deployment names and endpoints.
4. [Create an active event](events.md), attach resources, set the schedule, and set request/token
   caps.
5. [Plan and test capacity](capacity.md) with the synchronized
   [10/25/50-user load test](20-service-installation/70-testing/20-load-testing.md).
6. Rehearse registration and the attendee client from a private browser window.
7. During the event, use [reporting](reporting.md) and Azure Monitor to watch registrations,
   requests, tokens, failures, and quota.

**Success:** a test attendee can register, configure the GitHub Copilot App with Responses, receive
a model response, and produce matching proxy and Azure Monitor metrics.

## Before you start

You need Azure deployment permissions, admin-portal access, model quota in the target region, an
event schedule and expected attendance, and one test attendee identity.

## Pre-event rehearsal checklist

- [ ] Proxy endpoint is reachable and returns 401 without an event key.
- [ ] Admin login works using the configured Entra ID or local-password mode.
- [ ] Registration site and the event attendee URL load in a private browser window.
- [ ] Every attached resource is active and uses the exact upstream deployment name.
- [ ] Managed Identity roles or stored upstream keys have been tested.
- [ ] Event start/end times and time zone are correct.
- [ ] GitHub registration works; shared-code access is tested if the event uses it.
- [ ] Copilot uses **Custom endpoint**, **Responses**, the exact `/api/v1` endpoint, and the event
      model ID.
- [ ] Copilot limits are 80000 prompt tokens and 4096 output tokens for the tested GPT-5 profile.
- [ ] A new Copilot chat returns a response and failed test chats have been deleted.
- [ ] The 10/25/50-user test passes at the planned peak concurrency.
- [ ] Reporting and Azure Monitor show the rehearsal request and token usage.
- [ ] An operator knows how to scale model quota and roll back a Container App revision.

## Event operations

### Monitor

- Watch event registrations, requests, and per-resource token totals in the admin
  [Reporting](reporting.md) page.
- Watch Azure OpenAI/Foundry request, token, latency, and throttling metrics in Azure Monitor.
- Watch Container App replica count, CPU, memory, restarts, and HTTP errors.
- Treat `rate_limit_exceeded` as model deployment saturation unless proxy compute metrics also show
  pressure.

### Scale model capacity

Increase the model deployment SKU capacity before the event when the load test shows insufficient
TPM/RPM. Repeat the same load profile after changing capacity.

### Roll back an application revision

List revisions and direct traffic back to the last known-good revision:

```shell
az containerapp revision list \
  --name <container-app-name> \
  --resource-group <resource-group> \
  --output table

az containerapp ingress traffic set \
  --name <container-app-name> \
  --resource-group <resource-group> \
  --revision-weight <known-good-revision>=100
```

### Back up and restore

Use the admin portal backup feature before major event or catalog changes. Store exported backup
files according to your organization's data handling policy. Restore only into the intended
environment and verify resources/events before reopening attendee access.

### Clean up after the event

1. Confirm the event end time has passed or disable the event.
2. Review and capture final reporting totals.
3. Remove temporary shared codes.
4. Delete failed Copilot test chats from the Copilot App's **Chats** list.
5. Scale temporary model capacity down if it is no longer required.
6. Remove temporary role assignments, test resources, and test events that are not needed.

## Troubleshooting

| Symptom | First action |
|---|---|
| Attendee client fails but no proxy request is logged | Check client context, endpoint, Wire API, and whether a new chat was started. |
| 401 | Re-copy the active event key and confirm the event time window. |
| 404 | Confirm `/api/v1`, Responses routing, exact deployment name, and event resource assignment. |
| 429 or `rate_limit_exceeded` | Check Azure model TPM/RPM and scale or reduce the request profile. |
| Proxy CPU/memory pressure | Increase Container App resources or replicas, then repeat the load test. |
| Metrics do not match | Wait for metric persistence, then compare the terminal SSE usage event with reporting and Azure Monitor. |

## Next step

[Deploy the solution to Azure](deployment/azure.md).
