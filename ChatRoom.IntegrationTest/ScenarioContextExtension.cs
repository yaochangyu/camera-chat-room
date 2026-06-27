namespace ChatRoom.IntegrationTest;

public static class ScenarioContextExtension
{
    public static T? GetOrDefault<T>(this ScenarioContext context, string key, T? defaultValue = default)
        => context.ContainsKey(key) ? context.Get<T>(key) : defaultValue;

    public static void SetServiceProvider(this ScenarioContext context, IServiceProvider sp) =>
        context.Set(sp, "ServiceProvider");

    public static IServiceProvider GetServiceProvider(this ScenarioContext context) =>
        context.Get<IServiceProvider>("ServiceProvider");

    public static void SetHttpClient(this ScenarioContext context, HttpClient client) =>
        context.Set(client, "HttpClient");

    public static HttpClient GetHttpClient(this ScenarioContext context) =>
        context.Get<HttpClient>("HttpClient");

    public static void SetApiToken(this ScenarioContext context, string token) =>
        context.Set(token, "ApiToken");

    public static string? GetApiToken(this ScenarioContext context) =>
        context.GetOrDefault<string>("ApiToken");

    public static void SetHubToken(this ScenarioContext context, string token) =>
        context.Set(token, "HubToken");

    public static string? GetHubToken(this ScenarioContext context) =>
        context.GetOrDefault<string>("HubToken");

    public static void SetAuthorizationCleared(this ScenarioContext context) =>
        context.Set(true, "AuthorizationCleared");

    public static bool IsAuthorizationCleared(this ScenarioContext context) =>
        context.GetOrDefault("AuthorizationCleared", false);

    public static void SetHttpRequestBody(this ScenarioContext context, string body) =>
        context.Set(body, "HttpRequestBody");

    public static string? GetHttpRequestBody(this ScenarioContext context) =>
        context.GetOrDefault<string>("HttpRequestBody");

    public static void SetHttpResponseBody(this ScenarioContext context, string body) =>
        context.Set(body, "HttpResponseBody");

    public static string? GetHttpResponseBody(this ScenarioContext context) =>
        context.GetOrDefault<string>("HttpResponseBody");

    public static void SetHttpStatusCode(this ScenarioContext context, HttpStatusCode code) =>
        context.Set(code, "HttpStatusCode");

    public static HttpStatusCode GetHttpStatusCode(this ScenarioContext context) =>
        context.Get<HttpStatusCode>("HttpStatusCode");

    public static void SetJsonNode(this ScenarioContext context, JsonNode node) =>
        context.Set(node, "JsonNode");

    public static JsonNode? GetJsonNode(this ScenarioContext context) =>
        context.GetOrDefault<JsonNode>("JsonNode");
}
