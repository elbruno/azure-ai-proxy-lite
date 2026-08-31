using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AzureAIProxy.Shared.Database;
using AzureAIProxy.Routes.CustomResults;
using AzureAIProxy.Services;
using AzureAIProxy.Models;
using AzureAIProxy.Middleware;

namespace AzureAIProxy.Routes;

public static class AzureOpenAI
{
    public static RouteGroupBuilder MapAzureOpenAIRoutes(this RouteGroupBuilder builder)
    {
        // Azure AI Search Query Routes
        builder.MapPost("/indexes/{deploymentName}/docs/search", ProcessRequestAsync);
        builder.MapPost("/indexes('{deploymentName}')/docs/search.post.search", ProcessRequestAsync);

        // Azure OpenAI Routes
        var openAIGroup = builder.MapGroup("/openai/deployments/{deploymentName}");
        openAIGroup.MapPost("/chat/completions", ProcessRequestAsync);
        openAIGroup.MapPost("/extensions/chat/completions", ProcessRequestAsync);
        openAIGroup.MapPost("/completions", ProcessRequestAsync);
        openAIGroup.MapPost("/embeddings", ProcessRequestAsync);

        // Azure OpenAI Responses API. Unlike chat/completions, the Responses API path
        // has no "/deployments/{name}" segment — the deployment/model name is instead
        // supplied in the request body's "model" property. Some clients (e.g. the
        // GitHub Copilot desktop app's "Responses" wire API) call "/responses" directly
        // against an endpoint that already ends in "/api/v1", instead of "/openai/responses".
        // Both are mapped here; the upstream call always targets "openai/responses".
        builder.MapPost("/openai/responses", ProcessResponsesRequestAsync);
        builder.MapPost("/responses", ProcessResponsesRequestAsync);

        // Generic OpenAI-compatible top-level "/completions" route (no "/deployments/{name}"
        // segment in the incoming URL, deployment/model name comes from the request body's
        // "model" property instead). NOTE: top-level "/chat/completions" and "/embeddings"
        // are intentionally NOT added here — those exact paths are already mapped (with a
        // different, Bearer-token-only auth contract) by AzureInference.MapAzureInferenceRoutes
        // for Foundry Models-as-a-Service / Mistral-style deployments whose endpoint URL is
        // itself the full completions/embeddings endpoint (no Azure "openai/deployments/{name}"
        // prefix). Registering the same path twice causes an AmbiguousMatchException at request
        // time (confirmed via testing), so do not duplicate them here. GPT-5-family models
        // (which most "Custom endpoint" clients target) must use "/responses" or
        // "/openai/responses" above instead, per the Wire API = "Responses" requirement.
        builder.MapPost("/completions", ProcessTopLevelCompletionsRequestAsync);

        return builder;
    }

    [Authorize(AuthenticationSchemes = $"{ProxyAuthenticationOptions.ApiKeyScheme},{ProxyAuthenticationOptions.BearerTokenScheme}")]
    private static async Task<IResult> ProcessRequestAsync(
        [FromServices] ICatalogService catalogService,
        [FromServices] IProxyService proxyService,
        [FromServices] ILoggerFactory loggerFactory,
        HttpContext context,
        string deploymentName
    )
    {
        var logger = loggerFactory.CreateLogger("AzureOpenAI");
        string requestPath = (string)context.Items["requestPath"]!;
        RequestContext requestContext = (RequestContext)context.Items["RequestContext"]!;
        JsonDocument requestJsonDoc = (JsonDocument)context.Items["jsonDoc"]!;
        bool streaming = (bool?)context.Items["IsStreaming"] ?? false;

        var (deployment, eventCatalog) = await catalogService.GetCatalogItemAsync(
            requestContext.EventId,
            deploymentName!
        );

        if (deployment is null)
        {
            logger.LogWarning(
                "Deployment '{DeploymentName}' not found for event '{EventId}'. Available: {Available}",
                deploymentName, requestContext.EventId,
                string.Join(", ", eventCatalog.Select(d => d.DeploymentName)));
            return OpenAIResult.NotFound(
                $"Deployment '{deploymentName}' not found for this event. Available deployments are: {string.Join(", ", eventCatalog.Select(d => d.DeploymentName))}"
            );
        }

        var url = new UriBuilder(deployment.EndpointUrl.TrimEnd('/'))
        {
            Path = requestPath
        };

        var authHeader = await proxyService.GetAuthenticationHeaderAsync(deployment);
        List<RequestHeader> requestHeaders = [authHeader];

        try
        {
            if (streaming)
            {
                await proxyService.HttpPostStreamAsync(
                    url,
                    requestHeaders,
                    context,
                    requestJsonDoc,
                    requestContext,
                    deployment
                );
                return new ProxyResult(null!, (int)HttpStatusCode.OK);
            }


            var (responseContent, statusCode) = await proxyService.HttpPostAsync(
                url,
                requestHeaders,
                context,
                requestJsonDoc,
                requestContext,
                deployment
            );
            return new ProxyResult(responseContent, statusCode);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            return OpenAIResult.ServiceUnavailable("The request was canceled due to timeout. Inner exception: " + ex.InnerException.Message);
        }
        catch (TaskCanceledException ex)
        {
            return OpenAIResult.ServiceUnavailable("The request was canceled: " + ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return OpenAIResult.ServiceUnavailable("The request failed: " + ex.Message);
        }
    }

    [Authorize(AuthenticationSchemes = $"{ProxyAuthenticationOptions.ApiKeyScheme},{ProxyAuthenticationOptions.BearerTokenScheme}")]
    private static Task<IResult> ProcessResponsesRequestAsync(
        [FromServices] ICatalogService catalogService,
        [FromServices] IProxyService proxyService,
        [FromServices] ILoggerFactory loggerFactory,
        HttpContext context
    ) => ProcessModelInBodyRequestAsync(
        catalogService, proxyService, loggerFactory, context,
        // Always forward to Azure's "openai/responses" upstream path, regardless of
        // whether the incoming request hit "/openai/responses" or the shorter "/responses".
        static _ => "openai/responses"
    );

    [Authorize(AuthenticationSchemes = $"{ProxyAuthenticationOptions.ApiKeyScheme},{ProxyAuthenticationOptions.BearerTokenScheme}")]
    private static Task<IResult> ProcessTopLevelCompletionsRequestAsync(
        [FromServices] ICatalogService catalogService,
        [FromServices] IProxyService proxyService,
        [FromServices] ILoggerFactory loggerFactory,
        HttpContext context
    ) => ProcessModelInBodyRequestAsync(
        catalogService, proxyService, loggerFactory, context,
        static model => $"openai/deployments/{model}/completions"
    );

    // Shared handler for any top-level (deployment-less) OpenAI-compatible route where the
    // deployment/model name is supplied via the request body's "model" property instead of
    // a "/deployments/{name}" URL segment. "upstreamPathBuilder" turns the resolved model
    // name into the correct Azure-style upstream path for the specific wire API in use.
    private static async Task<IResult> ProcessModelInBodyRequestAsync(
        ICatalogService catalogService,
        IProxyService proxyService,
        ILoggerFactory loggerFactory,
        HttpContext context,
        Func<string, string> upstreamPathBuilder
    )
    {
        var logger = loggerFactory.CreateLogger("AzureOpenAI");
        RequestContext requestContext = (RequestContext)context.Items["RequestContext"]!;
        JsonDocument requestJsonDoc = (JsonDocument)context.Items["jsonDoc"]!;
        bool streaming = (bool?)context.Items["IsStreaming"] ?? false;

        string? deploymentName = (string?)context.Items["ModelName"];
        if (string.IsNullOrEmpty(deploymentName))
        {
            return OpenAIResult.BadRequest("Request body must include a \"model\" property identifying the deployment.");
        }

        var (deployment, eventCatalog) = await catalogService.GetCatalogItemAsync(
            requestContext.EventId,
            deploymentName!
        );

        if (deployment is null)
        {
            logger.LogWarning(
                "Deployment '{DeploymentName}' not found for event '{EventId}'. Available: {Available}",
                deploymentName, requestContext.EventId,
                string.Join(", ", eventCatalog.Select(d => d.DeploymentName)));
            return OpenAIResult.NotFound(
                $"Deployment '{deploymentName}' not found for this event. Available deployments are: {string.Join(", ", eventCatalog.Select(d => d.DeploymentName))}"
            );
        }

        var url = new UriBuilder(deployment.EndpointUrl.TrimEnd('/'))
        {
            Path = upstreamPathBuilder(deploymentName!)
        };

        var authHeader = await proxyService.GetAuthenticationHeaderAsync(deployment);
        List<RequestHeader> requestHeaders = [authHeader];

        try
        {
            if (streaming)
            {
                await proxyService.HttpPostStreamAsync(
                    url,
                    requestHeaders,
                    context,
                    requestJsonDoc,
                    requestContext,
                    deployment
                );
                return new ProxyResult(null!, (int)HttpStatusCode.OK);
            }

            var (responseContent, statusCode) = await proxyService.HttpPostAsync(
                url,
                requestHeaders,
                context,
                requestJsonDoc,
                requestContext,
                deployment
            );
            return new ProxyResult(responseContent, statusCode);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            return OpenAIResult.ServiceUnavailable("The request was canceled due to timeout. Inner exception: " + ex.InnerException.Message);
        }
        catch (TaskCanceledException ex)
        {
            return OpenAIResult.ServiceUnavailable("The request was canceled: " + ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return OpenAIResult.ServiceUnavailable("The request failed: " + ex.Message);
        }
    }
}
