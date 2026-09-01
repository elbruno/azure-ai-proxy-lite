# GitHub Copilot desktop app

The [GitHub Copilot desktop app](https://github.com/features/copilot) can call models exposed by the
Azure AI Proxy directly, through its **Model providers** settings. This lets attendees chat with
proxy-managed models (GPT-5-family and others) from a familiar client with no SDK or code required.

There are two ways to wire the proxy up as a provider — pick whichever matches the model you're
adding:

| Provider type | Best for | Wire API |
|---|---|---|
| **Azure OpenAI** | Attendees who want the proxy to look like a normal Azure OpenAI resource, with per-model deployment names. | `Chat Completions` or `Responses`, per model |
| **Custom endpoint** | Any OpenAI-compatible model, using a single base URL for every model. Simplest option for GPT-5-family models. | `Responses` (required for GPT-5-family) |

> ⚠️ **GPT-5-family models (`gpt-5-mini`, `gpt-5.6-luna`, etc.) require Wire API = `Responses`.**
> The Chat Completions API is not supported for these models. The app's own help text calls this out:
> *"Most OpenAI-compatible gateways are Chat Completions; OpenAI's own GPT-5 series uses Responses."*
> If you leave the default (`Completions`) you will see `CAPIError: 404 Not Found` in every chat.

## Prerequisites

- Register for an event to obtain your **event API key** and the **proxy endpoint URL**
  (see [Attendee registration](attendee.md)).
- The proxy endpoint always ends in `/api/v1` — for example:
  `https://<your-proxy-host>/api/v1`

## Option A — Custom endpoint (recommended for GPT-5-family)

1. Open the GitHub Copilot app → **Settings** (`Ctrl+,` / `Cmd+,`) → **Model providers**.
1. Select **+ Add provider** → **Custom endpoint**.
1. Fill in the form:

    | Field | Value |
    |---|---|
    | Display name | Any name you like, e.g. `Azure AI Proxy` |
    | Base URL | Your proxy endpoint + `/api/v1`, e.g. `https://<your-proxy-host>/api/v1` |
    | Wire API | **Responses** |
    | API key | Your event API key |
    | Custom headers | Leave blank |

1. Select **Add provider**, then add each model you want to use (e.g. `gpt-5-mini`).
1. Edit each added model and set conservative token caps:

    | Field | Recommended value |
    |---|---:|
    | Max prompt tokens | `80000` |
    | Max output tokens | `4096` |

    This prompt cap leaves room for Copilot's MCP servers, tools, and instructions while keeping
    requests under a typical 100K TPM Azure OpenAI deployment quota. Event organizers can raise or
    lower it based on the backing deployment's TPM headroom and expected concurrent attendees.
1. Start a new chat, pick your provider/model from the model picker, and send a message. After
   changing provider or model settings, open another new chat so Copilot uses the saved values.

## Option B — Azure OpenAI template

1. Open the GitHub Copilot app → **Settings** → **Model providers**.
1. Select **+ Add provider** → **Azure OpenAI**.
1. Fill in the form:

    | Field | Value |
    |---|---|
    | Endpoint | Your proxy endpoint + `/api/v1`, e.g. `https://<your-proxy-host>/api/v1` |
    | API version | `2025-04-01-preview` or later (GPT-5-family Responses API requires `2025-03-01-preview`+) |
    | API key | Your event API key |
    | Wire API | **Responses** for GPT-5-family models |

1. Add each deployment name you were given (e.g. `gpt-5-mini`) as a model.
1. Start a new chat and select your model. After changing provider or model settings, open another
   new chat so Copilot uses the saved values.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `CAPIError: 404 Not Found` on every message | Wire API is set to `Completions`/`Chat Completions` for a GPT-5-family model. | Edit the provider, change Wire API to `Responses`, save, and start a new chat. |
| `CAPIError: 429` / limit reached | The model request exceeded the Azure OpenAI deployment's RPM/TPM quota. | Edit the model and set token caps, for example `80000` max prompt tokens and `4096` max output tokens, or increase the deployment capacity. |
| Copilot says MCP servers, tools, and instructions use too much context before the proxy logs a request | The model's max prompt token cap is too low for Copilot's local context budget. | Raise the model's max prompt tokens, for example to `80000`, or run `/context` and disable unused MCP servers, tools, or instructions. |
| Settings look correct but an existing chat still fails | The existing chat was created before the latest provider/model setting change. | Start a new chat and reselect the custom model. |
| `CAPIError: 401` | Wrong or expired event API key. | Re-copy the key from your event registration or admin portal. |
| Model not found / not listed | The deployment name doesn't match a resource attached to your event, or the event isn't active. | Confirm the exact deployment name with the event organiser. |
| Works once, then fails after a redeploy | The container app briefly serves the old revision while draining. | Wait ~10 seconds and retry. |

## Lessons learned from the GitHub Copilot app validation

- **Use the Custom endpoint provider for GPT-5-family models.** Configure the proxy base URL with
  `/api/v1`, set Wire API to **Responses**, and add model IDs such as `gpt-5-mini` and
  `gpt-5.6-luna`.
- **Start a new chat after every provider or model configuration change.** Existing chats can keep
  stale model/provider settings, including old token caps and old Wire API selections.
- **Treat “too much context” as a client-side Copilot App error first.** If the proxy logs show no
  new request, the app rejected the request locally. Raise the model's max prompt token cap or run
  `/context` and disable unused MCP servers, tools, or instructions.
- **Use token caps that leave room for Copilot's local context.** `80000` max prompt tokens and
  `4096` max output tokens worked against a 100K TPM Azure OpenAI deployment. A lower prompt cap
  such as `32000` can be too small when MCP/tools/instructions are enabled.
- **Use proxy logs to tell client-side failures from upstream failures.** No new log entry means the
  request never reached the proxy. A logged upstream `429 rate_limit_exceeded` means the route is
  correct but the Azure OpenAI deployment quota is too low for the request/retry pattern.
- **Clean up failed test chats before handing off.** In the Copilot App's **Chats** section,
  right-click failed chats and select **Delete** so future testing starts from a clean list.
- **Do not add flat `/api/v1/chat/completions` or `/api/v1/embeddings` routes for this scenario.**
  GPT-5-family Copilot App traffic should use `/api/v1/responses`, while the existing
  Azure-inference routes already own those flat chat/embedding paths.

## For event organizers

No special resource type is required on the proxy side — any `Foundry_Model` resource with a GPT-5
or GPT-5.6 deployment works with the GitHub Copilot app's Custom endpoint or Azure OpenAI providers,
as long as attendees select **Wire API = Responses**.
