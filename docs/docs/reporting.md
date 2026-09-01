# Reporting

The AI Proxy Admin portal provides a reporting feature that allows you to view the usage of the AI Proxy service. All events are listed in the reporting page, and you can filter the events by key words from the event title and event owner.

## TL;DR

1. Open the admin portal and select **Reporting**.
2. Search for the event by title or owner and open its title.
3. Confirm registrations, requests, and per-resource token totals increase during rehearsal.
4. Compare unusual latency, errors, or flattened request rates with Azure Monitor model and
   Container App metrics.
5. Capture or export the final event totals according to your reporting process.

**Success:** the rehearsal request appears under the correct event and resource with non-zero
request and token usage.

## Before you start

You need admin access and at least one registered attendee request. Allow a short delay for metrics
to be persisted before treating a missing data point as an error.

The report page summarizes the events that match the search criteria.

![This chart shows the events reporting page](media/events-report.png)

## Detailed metrics for each event

You can view detailed metrics for each event by clicking on the event title. The detailed metrics include the following information:

1. Summary of the event, including the event title, event owner, start date and time, end date and time, and the number of registrations.
1. The **New Active Registrations** chart. This is the number of new attendees that have completed an activity in the event. For example, they made a request via the Foundry Toolkit or called an API.
1. The **Requests** chart. This is the number of requests that have been made to the AI Proxy service for the event.
1. The **Resources** table, which is a breakdown of the resources that have been used in the event. The number of requests and tokens for a model.

### New Active Registrations over time

![This chart shows new active registrations](media/new-active-registrations.png)

### AI Proxy Requests over time

![This chart shows AI Proxy requests over time](media/requests.png)

### Event resource usage

![This chart shows break down of resource usage](media/event-resource-usage.png)

## Verify

Send one known test request, note its model, and refresh the event report. The request count and
resource token totals should increase for that model.

## Troubleshooting

| Symptom | Fix |
|---|---|
| Event is absent | Clear the search filter and confirm you are signed into the owner/admin context that created the event. |
| Registration exists but no activity appears | Make a model request; registration alone is not active usage. |
| Request count increases but tokens do not | Confirm the proxy revision includes streaming Responses usage accounting and that the upstream terminal event contains usage. |
| Users see 429 errors | Check Azure model TPM/RPM in Azure Monitor; event reporting does not increase upstream quota. |
| Proxy metrics and Azure Monitor differ | Compare identical time windows, allow persistence delay, and account for rejected upstream requests. |

## Next step

[Use the event operations checklist](event-ready.md#event-operations).
