# Troubleshooting and frequently asked questions

Start with the symptom. Follow the linked guide when the quick fix does not resolve it.

| Symptom | Likely cause | First action |
|---|---|---|
| No models are available in an event | No active resource is attached | Create an active [resource](resources.md), then edit the event and select it. |
| Admin redirects to an unexpected login or cannot sign in | Authentication mode or tenant mismatch | Review [Entra ID and local-password modes](deployment/azure.md#authenticating-with-the-ai-proxy-admin). |
| Copilot fails before a proxy request is logged | Local context or stale client configuration | Use 80000 prompt tokens, disable unused context, save, and start a new chat. |
| 401 Unauthorized | Incorrect or expired event key | Re-copy the key and confirm the [event window](attendee.md#troubleshooting). |
| 404 Not Found | Wrong endpoint, route, Wire API, or deployment name | Use the exact `/api/v1` endpoint and Responses for GPT-5-family models. |
| 429 or `rate_limit_exceeded` | Azure model TPM/RPM is saturated | Review [capacity planning](capacity.md) and scale or reduce the workload. |
| HTTP 200 stream later fails | Responses API terminal event reports failure | Parse SSE and require `response.completed`; use the [load harness](20-service-installation/70-testing/20-load-testing.md). |
| Managed Identity returns 403 | Missing role or wrong scope | Apply the roles in the [Managed Identity guide](deployment/managed_identity.md). |
| Reporting counts requests but not streaming tokens | Old proxy revision or missing terminal usage | Deploy the current proxy and verify terminal Responses usage. |
| Shared-code access fails | Incorrect key format | Use `event-id@shared-code/email-address`; the code must be at least five alphanumeric characters. |

## Common questions

### What resource types does the proxy support?

Foundry Model, Foundry Agent, MCP Server, Foundry Toolkit, and Azure AI Search. See
[Configuring resources](resources.md).

### Can Azure deployments use local admin authentication?

Yes. Entra ID is the default, but Azure deployments can set `ADMIN_AUTH_MODE=password`,
`ADMIN_USERNAME`, and `ADMIN_PASSWORD` before `azd up`.

### Can attendees participate without GitHub?

Yes, when the organizer configures Event Shared Code access. GitHub registration is the normal path;
shared code is recommended only for short workshops.

### What do the three token limits mean?

- Copilot maximum prompt tokens control local client context.
- Event Max Token Cap controls output tokens allowed per attendee request.
- Azure TPM/RPM controls shared model deployment capacity.

See [Capacity planning](capacity.md).

### How do I redeploy one service?

```shell
azd deploy proxy
azd deploy admin
azd deploy registration
```

Run `azd up` to provision and deploy everything.

## Next step

[Follow the complete event-ready administrator journey](event-ready.md).
