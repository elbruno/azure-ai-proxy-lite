# Attendee registration

Once you have created an event and assigned resources to the event, you can share the event URL with the attendees. The attendees can register for the event and receive a time bound API Key to access the AI Proxy service.

GitHub authentication is the normal registration path. A GitHub account is not required when the
event organizer explicitly provides shared-code access.

## TL;DR

### GitHub registration

1. Open the attendee event URL supplied by the organizer.
2. Select **Login with GitHub**.
3. Read the event description and terms, then select **Register**.
4. In **Registration Details**, reveal and copy the **Event API Key**, **Proxy Endpoint**, and model
   name. The endpoint already ends in `/api/v1`.
5. Keep the page available: revisiting it while signed in shows the same event details and key.

### Shared-code access

1. Obtain the event ID and shared code from the organizer.
2. Build the event key exactly as:

    ```text
    event-id@shared-code/your-email-address
    ```

3. Use the event Proxy Endpoint and model name supplied by the organizer.

**Success:** a request made with the event key reaches an event model and returns a response before
the event end time.

## Before you start

You need the event URL and either a GitHub account or shared-code details from the organizer. Do not
share the generated event key; it identifies your event usage and expires with the event.

1. Select the `Events` tab, then select the event you want to share with the attendees.

    ![](./media/attendee_registration.png)

1. From the event details page, select the highlighted in red `Attendee/People` icon. The event registration page will open. Share the link to this page with your audience. The following image is an example of the event registration page.

    ![](./media/attendee-registration.png)

1. The attendee authenticates with the AI Proxy service using their GitHub account.
1. Next, the attendee is presented with the event details and the `Register` button.
1. The attendee selects the `Register` button to register for the event to receive a time bound API Key to access the AI Proxy service.

## Event registration

The event registration page displays the event details and the `Register` button. The attendee selects the `Register` button to register for the event. The attendee is then presented with the `API Key` and Endpoint that is valid for the duration of the event.

![The image shows event registration and the API Key and Endpoint](./media/event-registration.png)

## Verify

Use the displayed values with one of the event-page examples or configure the
[GitHub Copilot App](github_copilot_app.md). Send a short test prompt and confirm a model response is
returned.

## Troubleshooting

| Symptom | Likely cause and fix |
|---|---|
| Event has not started or has ended | Check the displayed event time in your local time zone or contact the organizer. |
| No Register button appears | Sign in with GitHub and reload the active event page. |
| 401 Unauthorized | Copy the complete current event key again; it may be incorrect or expired. |
| 404 Not Found | Use the Proxy Endpoint exactly as displayed and select the API route required by the client. |
| `rate_limit_exceeded` or 429 | The shared Azure model quota is saturated; wait briefly and notify the organizer. |
| Copilot settings changed but behavior did not | Save the settings, start a new chat, and reselect the event model. |
| Copilot fails before the proxy receives a request | Reduce unused Copilot context sources or raise the configured maximum prompt tokens to 80000. |

## Next step

[Configure the GitHub Copilot App with the event values](github_copilot_app.md).
