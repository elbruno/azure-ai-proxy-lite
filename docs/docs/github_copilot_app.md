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
1. Start a new chat, pick your provider/model from the model picker, and send a message.

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
1. Start a new chat and select your model.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `CAPIError: 404 Not Found` on every message | Wire API is set to `Completions`/`Chat Completions` for a GPT-5-family model. | Edit the provider, change Wire API to `Responses`, save, and start a new chat. |
| `CAPIError: 401` | Wrong or expired event API key. | Re-copy the key from your event registration or admin portal. |
| Model not found / not listed | The deployment name doesn't match a resource attached to your event, or the event isn't active. | Confirm the exact deployment name with the event organiser. |
| Works once, then fails after a redeploy | The container app briefly serves the old revision while draining. | Wait ~10 seconds and retry. |

## For event organizers

No special resource type is required on the proxy side — any `Foundry_Model` resource with a GPT-5
or GPT-5.6 deployment works with the GitHub Copilot app's Custom endpoint or Azure OpenAI providers,
as long as attendees select **Wire API = Responses**.
