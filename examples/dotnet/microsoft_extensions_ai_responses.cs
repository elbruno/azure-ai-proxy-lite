#:property ManagePackageVersionsCentrally=false
#:property NoWarn=OPENAI001
#:package OpenAI@2.12.0
#:package Microsoft.Extensions.AI@10.9.0
#:package Microsoft.Extensions.AI.Abstractions@10.9.0
#:package Microsoft.Extensions.AI.OpenAI@10.9.0
#:package DotNetEnv@2.5.0

using System.ClientModel;
using DotNetEnv;
using Microsoft.Extensions.AI;
using OpenAI;

Env.Load();

static string GetRequiredEnvironmentVariable(string name)
{
    string? value = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Set the {name} environment variable before running this sample.");
    }

    return value;
}

string endpoint = GetRequiredEnvironmentVariable("PROXY_ENDPOINT").TrimEnd('/');
string apiKey = GetRequiredEnvironmentVariable("PROXY_API_KEY");
string modelName = Environment.GetEnvironmentVariable("MODEL_NAME") ?? "gpt-5-mini";

var options = new OpenAIClientOptions
{
    Endpoint = new Uri(endpoint),
};

using IChatClient chatClient = new OpenAIClient(
        new ApiKeyCredential(apiKey),
        options)
    .GetResponsesClient()
    .AsIChatClient(modelName);

ChatResponse response = await chatClient.GetResponseAsync(
    "Reply with one short sentence confirming the Azure AI Proxy works.",
    new ChatOptions
    {
        MaxOutputTokens = 256,
    });

Console.WriteLine(response.Text);
