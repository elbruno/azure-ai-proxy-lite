using System.Net;
using System.Text.Json;
using AzureAIProxy.Models;
using AzureAIProxy.Services;
using AzureAIProxy.Shared.Database;
using AzureAIProxy.Tests.TestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AzureAIProxy.Tests.Proxy;

public class ProxyServiceBehaviorTests
{
    [Fact]
    public async Task GetAuthenticationHeaderAsync_EndpointKeyMode_UsesApiKeyHeader()
    {
        var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(TestData.JsonResponse(HttpStatusCode.OK, "{}")));

        var service = new ProxyService(
            new StubHttpClientFactory(new HttpClient(handler)),
            new NoopMetricService(),
            NullLogger<ProxyService>.Instance,
            new ConfigurationBuilder().Build());

        var deployment = TestData.CreateDeployment(ModelType.Foundry_Model.ToStorageString(), useManagedIdentity: false, endpointKey: "secret-key");

        var header = await service.GetAuthenticationHeaderAsync(deployment, useBearerToken: false);

        Assert.Equal("api-key", header.Key);
        Assert.Equal("secret-key", header.Value);
    }

    [Fact]
    public async Task GetAuthenticationHeaderAsync_BearerMode_UsesAuthorizationHeader()
    {
        var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(TestData.JsonResponse(HttpStatusCode.OK, "{}")));

        var service = new ProxyService(
            new StubHttpClientFactory(new HttpClient(handler)),
            new NoopMetricService(),
            NullLogger<ProxyService>.Instance,
            new ConfigurationBuilder().Build());

        var deployment = TestData.CreateDeployment(ModelType.Foundry_Model.ToStorageString(), useManagedIdentity: false, endpointKey: "bearer-token");

        var header = await service.GetAuthenticationHeaderAsync(deployment, useBearerToken: true);

        Assert.Equal("Authorization", header.Key);
        Assert.Equal("Bearer bearer-token", header.Value);
    }

    [Fact]
    public async Task HttpPostAsync_FoundryToolkit_RewritesMaxTokensToMaxCompletionTokens()
    {
        var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(TestData.JsonResponse(HttpStatusCode.OK, "{\"ok\":true}")));
        var httpClient = new HttpClient(handler);
        var metricService = new NoopMetricService();

        var service = new ProxyService(
            new StubHttpClientFactory(httpClient),
            metricService,
            NullLogger<ProxyService>.Instance,
            new ConfigurationBuilder().Build());

        var deployment = TestData.CreateDeployment(ModelType.Foundry_Toolkit.ToStorageString(), useManagedIdentity: false);
        deployment.UseMaxCompletionTokens = true;

        using var body = JsonDocument.Parse("{\"messages\":[{\"role\":\"user\",\"content\":\"hi\"}],\"max_tokens\":123}");
        var context = new DefaultHttpContext();

        var (responseContent, statusCode) = await service.HttpPostAsync(
            new UriBuilder("https://upstream.example.com/openai/deployments/test/chat/completions?api-version=2024-10-21"),
            [new RequestHeader("api-key", "proxy-key")],
            context,
            body,
            TestData.CreateRequestContext(),
            deployment);

        Assert.Equal(200, statusCode);
        Assert.Equal("{\"ok\":true}", responseContent);
        Assert.Equal(1, metricService.Calls);

        Assert.NotNull(handler.LastContent);
        using var rewrittenBody = JsonDocument.Parse(handler.LastContent!);
        Assert.True(rewrittenBody.RootElement.TryGetProperty("max_completion_tokens", out var maxCompletionTokens));
        Assert.Equal(123, maxCompletionTokens.GetInt32());
        Assert.False(rewrittenBody.RootElement.TryGetProperty("max_tokens", out _));
    }

    [Fact]
    public async Task HttpPostAsync_MissingApiVersion_FallsBackToConfiguredDefault()
    {
        var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(TestData.JsonResponse(HttpStatusCode.OK, "{\"ok\":true}")));

        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DefaultApiVersion"] = "2024-10-21" })
            .Build();

        var service = new ProxyService(
            new StubHttpClientFactory(new HttpClient(handler)),
            new NoopMetricService(),
            NullLogger<ProxyService>.Instance,
            configuration);

        var deployment = TestData.CreateDeployment(ModelType.Foundry_Model.ToStorageString(), useManagedIdentity: false);

        using var body = JsonDocument.Parse("{\"messages\":[{\"role\":\"user\",\"content\":\"hi\"}]}");
        // Simulate a client request with NO query string at all (the observed GitHub Copilot app bug).
        var context = new DefaultHttpContext();

        var (_, statusCode) = await service.HttpPostAsync(
            new UriBuilder("https://upstream.example.com/openai/deployments/test/chat/completions"),
            [new RequestHeader("api-key", "proxy-key")],
            context,
            body,
            TestData.CreateRequestContext(),
            deployment);

        Assert.Equal(200, statusCode);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal("api-version=2024-10-21", handler.LastRequest!.RequestUri!.Query.TrimStart('?'));
    }

    [Fact]
    public async Task HttpPostAsync_ApiVersionAlreadyPresent_DoesNotOverrideIt()
    {
        var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(TestData.JsonResponse(HttpStatusCode.OK, "{\"ok\":true}")));

        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DefaultApiVersion"] = "2024-10-21" })
            .Build();

        var service = new ProxyService(
            new StubHttpClientFactory(new HttpClient(handler)),
            new NoopMetricService(),
            NullLogger<ProxyService>.Instance,
            configuration);

        var deployment = TestData.CreateDeployment(ModelType.Foundry_Model.ToStorageString(), useManagedIdentity: false);

        using var body = JsonDocument.Parse("{\"messages\":[{\"role\":\"user\",\"content\":\"hi\"}]}");
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?api-version=2025-01-01-preview");

        var (_, statusCode) = await service.HttpPostAsync(
            new UriBuilder("https://upstream.example.com/openai/deployments/test/chat/completions"),
            [new RequestHeader("api-key", "proxy-key")],
            context,
            body,
            TestData.CreateRequestContext(),
            deployment);

        Assert.Equal(200, statusCode);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal("api-version=2025-01-01-preview", handler.LastRequest!.RequestUri!.Query.TrimStart('?'));
    }

    [Fact]
    public async Task HttpPostStreamAsync_CapturesStreamingResponseForMetrics()
    {
        const string sseResponse = """
            event: response.completed
            data: {"type":"response.completed","response":{"usage":{"input_tokens":10,"output_tokens":5,"total_tokens":15}}}

            data: [DONE]
            """;
        var handler = new RecordingHttpMessageHandler((_, _) =>
        {
            var response = TestData.JsonResponse(HttpStatusCode.OK, sseResponse);
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
            return Task.FromResult(response);
        });
        var metricService = new NoopMetricService();
        var service = new ProxyService(
            new StubHttpClientFactory(new HttpClient(handler)),
            metricService,
            NullLogger<ProxyService>.Instance,
            new ConfigurationBuilder().Build());
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        using var body = JsonDocument.Parse("""{"model":"gpt-5-mini","stream":true}""");

        await service.HttpPostStreamAsync(
            new UriBuilder("https://upstream.example.com/openai/responses"),
            [new RequestHeader("api-key", "proxy-key")],
            context,
            body,
            TestData.CreateRequestContext(),
            TestData.CreateDeployment(ModelType.Foundry_Model.ToStorageString(), useManagedIdentity: false)
        );

        context.Response.Body.Position = 0;
        var streamedContent = await new StreamReader(context.Response.Body).ReadToEndAsync();

        Assert.Equal(sseResponse, streamedContent);
        Assert.Equal("text/event-stream", context.Response.ContentType);
        Assert.Equal(1, metricService.Calls);
        Assert.Equal(sseResponse, metricService.LastResponseContent);
    }

    [Fact]
    public async Task HttpPostStreamAsync_LargeStreamRetainsTerminalUsageForMetrics()
    {
        var largeDelta = new string('x', (1024 * 1024) + 100);
        var sseResponse =
            "event: response.output_text.delta\n"
            + $"data: {{\"type\":\"response.output_text.delta\",\"delta\":\"{largeDelta}\"}}\n\n"
            + "event: response.completed\n"
            + "data: {\"type\":\"response.completed\",\"response\":{\"usage\":{\"input_tokens\":100,\"output_tokens\":50,\"total_tokens\":150}}}\n\n"
            + "data: [DONE]\n";
        var handler = new RecordingHttpMessageHandler((_, _) =>
        {
            var response = TestData.JsonResponse(HttpStatusCode.OK, sseResponse);
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
            return Task.FromResult(response);
        });
        var metricService = new NoopMetricService();
        var service = new ProxyService(
            new StubHttpClientFactory(new HttpClient(handler)),
            metricService,
            NullLogger<ProxyService>.Instance,
            new ConfigurationBuilder().Build());
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        using var body = JsonDocument.Parse("""{"model":"gpt-5-mini","stream":true}""");

        await service.HttpPostStreamAsync(
            new UriBuilder("https://upstream.example.com/openai/responses"),
            [new RequestHeader("api-key", "proxy-key")],
            context,
            body,
            TestData.CreateRequestContext(),
            TestData.CreateDeployment(ModelType.Foundry_Model.ToStorageString(), useManagedIdentity: false)
        );

        Assert.Equal(sseResponse.Length, context.Response.Body.Length);
        Assert.NotNull(metricService.LastResponseContent);
        Assert.True(metricService.LastResponseContent.Length <= 1024 * 1024);
        Assert.Contains("response.completed", metricService.LastResponseContent);
        Assert.Contains("\"total_tokens\":150", metricService.LastResponseContent);
    }
}
