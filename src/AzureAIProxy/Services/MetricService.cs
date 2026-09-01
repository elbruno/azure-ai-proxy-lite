using System.Text.Json;
using AzureAIProxy.Shared.Database;

namespace AzureAIProxy.Services;

public class MetricService(IMetricChannel metricChannel, IRateLimitService rateLimitService) : IMetricService
{
    public Task LogApiUsageAsync(RequestContext requestContext, Deployment deployment, string? responseContent)
    {
        var (promptTokens, completionTokens, totalTokens) = GetUsage(responseContent);

        // Update in-memory rate limit counters
        rateLimitService.IncrementUsage(requestContext.ApiKey, totalTokens);

        // Enqueue metric for background flush
        var resource = $"{deployment.ModelType} | {deployment.DeploymentName}";
        metricChannel.Enqueue(new MetricUpdate(requestContext.EventId, resource, promptTokens, completionTokens, totalTokens));

        return Task.CompletedTask;
    }

    private static (int promptTokens, int completionTokens, int totalTokens) GetUsage(string? responseContent)
    {
        if (string.IsNullOrEmpty(responseContent))
            return (0, 0, 0);

        if (TryParseUsageJson(responseContent, out var usage))
            return usage;

        (int promptTokens, int completionTokens, int totalTokens)? streamUsage = null;
        foreach (var line in responseContent.Split('\n'))
        {
            var trimmedLine = line.Trim();
            if (!trimmedLine.StartsWith("data:", StringComparison.Ordinal))
                continue;

            var eventPayload = trimmedLine["data:".Length..].Trim();
            if (eventPayload.Length == 0 || eventPayload == "[DONE]")
                continue;

            if (TryParseUsageJson(eventPayload, out usage))
                streamUsage = usage;
        }

        return streamUsage ?? (0, 0, 0);
    }

    private static bool TryParseUsageJson(
        string content,
        out (int promptTokens, int completionTokens, int totalTokens) result
    )
    {
        result = (0, 0, 0);

        try
        {
            using var jsonDoc = JsonDocument.Parse(content);
            var root = jsonDoc.RootElement;
            if (root.TryGetProperty("response", out var response) && response.ValueKind == JsonValueKind.Object)
                root = response;

            if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
                return false;

            var prompt = GetTokenValue(usage, "prompt_tokens", "input_tokens");
            var completion = GetTokenValue(usage, "completion_tokens", "output_tokens");
            var total = GetTokenValue(usage, "total_tokens");
            result = (prompt, completion, total > 0 ? total : prompt + completion);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static int GetTokenValue(JsonElement usage, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (
                usage.TryGetProperty(propertyName, out var value)
                && value.ValueKind == JsonValueKind.Number
                && value.TryGetInt32(out var tokenCount)
            )
            {
                return tokenCount;
            }
        }

        return 0;
    }
}
