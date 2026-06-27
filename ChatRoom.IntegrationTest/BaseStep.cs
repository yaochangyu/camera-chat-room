using ChatRoom.Hubs;
using ChatRoom.Services;
using System.Text.Json;

namespace ChatRoom.IntegrationTest;

[Binding]
[CollectionDefinition("ChatRoom.IntegrationTest", DisableParallelization = true)]
public class BaseStep : Steps
{
    [AfterScenario]
    public void AfterScenario()
    {
        var server = this.ScenarioContext.GetOrDefault<TestServer>("TestServer");
        server?.Dispose();
    }

    // ─── 初始化 ───────────────────────────────────────────────────────────────

    [Given(@"初始化測試伺服器")]
    public void Given初始化測試伺服器()
    {
        var server = new TestServer();
        var client = server.CreateClient();
        this.ScenarioContext.Set(server, "TestServer");
        this.ScenarioContext.SetHttpClient(client);
        this.ScenarioContext.SetServiceProvider(server.Services);
    }

    // ─── 身分驗證 Steps ────────────────────────────────────────────────────────

    [Given(@"調用端已以 ""(.*)"" 身分登入並取得 API Token")]
    public async Task Given調用端已以身分登入並取得ApiToken(string username)
    {
        var client = this.ScenarioContext.GetHttpClient();
        var body = JsonSerializer.Serialize(new
        {
            username,
            password = "test@123"
        });
        var uri = new Uri(client.BaseAddress!, "api/auth/login");
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        var json = JsonNode.Parse(responseBody);
        var token = json?["token"]?.GetValue<string>();
        token.Should().NotBeNullOrEmpty("登入應回傳 API token");

        this.ScenarioContext.SetApiToken(token!);
    }

    [Given(@"調用端已以 ""(.*)"" 身分登入並取得 Hub Token")]
    public async Task Given調用端已以身分登入並取得HubToken(string username)
    {
        await Given調用端已以身分登入並取得ApiToken(username);

        var client = this.ScenarioContext.GetHttpClient();
        var apiToken = this.ScenarioContext.GetApiToken();
        var uri = new Uri(client.BaseAddress!, "api/auth/hub-token");

        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        var json = JsonNode.Parse(responseBody);
        var hubToken = json?["token"]?.GetValue<string>();
        hubToken.Should().NotBeNullOrEmpty("hub-token 應回傳 token");

        // 覆蓋成 Hub Token，後續請求自動帶此 token
        this.ScenarioContext.SetApiToken(hubToken!);
    }

    [Given(@"調用端未設定 Authorization Header")]
    public void Given調用端未設定AuthorizationHeader()
    {
        this.ScenarioContext.SetAuthorizationCleared();
    }

    // ─── HTTP 操作 Steps ──────────────────────────────────────────────────────

    [Given(@"調用端已準備 Body 參數\(Json\)")]
    public void Given調用端已準備BodyJson(string json)
    {
        this.ScenarioContext.SetHttpRequestBody(json);
    }

    [When(@"調用端發送 ""(.*)"" 請求至 ""(.*)""")]
    public async Task When調用端發送請求至(string method, string url)
    {
        var client = this.ScenarioContext.GetHttpClient();
        var uri = new Uri(client.BaseAddress!, url);
        using var request = new HttpRequestMessage(new HttpMethod(method), uri);

        if (!this.ScenarioContext.IsAuthorizationCleared())
        {
            var token = this.ScenarioContext.GetApiToken();
            if (token != null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var body = this.ScenarioContext.GetHttpRequestBody();
        if (!string.IsNullOrEmpty(body))
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        this.ScenarioContext.SetHttpResponseBody(responseBody);
        this.ScenarioContext.SetHttpStatusCode(response.StatusCode);

        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            try
            {
                var node = JsonNode.Parse(responseBody);
                if (node != null)
                    this.ScenarioContext.SetJsonNode(node);
            }
            catch { /* 非 JSON 回應，略過 */ }
        }
    }

    // ─── 斷言 Steps ───────────────────────────────────────────────────────────

    [Then(@"預期得到 HttpStatusCode 為 ""(.*)""")]
    public void Then預期得到HttpStatusCode為(int expected)
    {
        var actual = (int)this.ScenarioContext.GetHttpStatusCode();
        actual.Should().Be(expected);
    }

    [Then(@"預期回傳內容為")]
    public void Then預期回傳內容為(string expected)
    {
        var actual = this.ScenarioContext.GetHttpResponseBody() ?? "null";
        var actualNode = JsonNode.Parse(actual);
        var expectedNode = JsonNode.Parse(expected);
        actualNode?.ToJsonString().Should().Be(expectedNode?.ToJsonString());
    }

    [Then(@"預期回傳內容中路徑 ""(.*)"" 的""字串等於"" ""(.*)""")]
    public void Then預期回傳內容中路徑字串等於(string path, string expected)
    {
        var node = this.ScenarioContext.GetJsonNode();
        node.Should().NotBeNull("回應應為 JSON 格式");
        var result = JsonPath.Parse(path).Evaluate(node!);
        var actual = result.Matches.FirstOrDefault()?.Value?.GetValue<string>();
        actual.Should().Be(expected);
    }

    [Then(@"預期回傳內容中路徑 ""(.*)"" 的""字串不為空""")]
    public void Then預期回傳內容中路徑字串不為空(string path)
    {
        var node = this.ScenarioContext.GetJsonNode();
        node.Should().NotBeNull("回應應為 JSON 格式");
        var result = JsonPath.Parse(path).Evaluate(node!);
        var actual = result.Matches.FirstOrDefault()?.Value?.GetValue<string>();
        actual.Should().NotBeNullOrEmpty();
    }

    [Then(@"預期回傳好友清單包含 ""(.*)""")]
    public void Then預期回傳好友清單包含(string username)
    {
        var node = this.ScenarioContext.GetJsonNode();
        node.Should().NotBeNull("回應應為 JSON 陣列");
        var array = node!.AsArray();
        var contains = array.Any(item =>
            item?["username"]?.GetValue<string>()
                ?.Equals(username, StringComparison.OrdinalIgnoreCase) == true);
        contains.Should().BeTrue($"好友清單應包含 '{username}'");
    }

    // ─── SignalR Steps ────────────────────────────────────────────────────────

    [Given(@"調用端以 API Token 換取 Hub Token")]
    public async Task Given調用端以ApiToken換取HubToken()
    {
        var client = this.ScenarioContext.GetHttpClient();
        var apiToken = this.ScenarioContext.GetApiToken();
        var uri = new Uri(client.BaseAddress!, "api/auth/hub-token");

        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken!);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonNode.Parse(body);
        var hubToken = json?["token"]?.GetValue<string>();
        hubToken.Should().NotBeNullOrEmpty("換取 Hub Token 應回傳 token");

        this.ScenarioContext.SetHubToken(hubToken!);
    }

    [Given(@"透過 SignalR 傳送聊天室訊息至房間 ""(.*)"" 內容 ""(.*)""")]
    public async Task Given透過SignalR傳送聊天室訊息(string roomName, string message)
    {
        var hubToken = this.ScenarioContext.GetHubToken();
        hubToken.Should().NotBeNullOrEmpty("請先執行「調用端以 API Token 換取 Hub Token」步驟");
        var factory = this.ScenarioContext.Get<TestServer>("TestServer");

        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/chatHub", opts =>
            {
                opts.Transports = HttpTransportType.LongPolling;
                opts.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                opts.AccessTokenProvider = () => Task.FromResult<string?>(hubToken);
            })
            .Build();

        await connection.StartAsync();
        await connection.InvokeAsync("JoinRoom", roomName);
        await connection.InvokeAsync("SendMessageToRoom", roomName, message);
        await connection.StopAsync();
        await connection.DisposeAsync();
    }

    [Given(@"透過 SignalR 傳送私訊至使用者 ""(.*)"" 內容 ""(.*)""")]
    public async Task Given透過SignalR傳送私訊(string targetUser, string message)
    {
        var hubToken = this.ScenarioContext.GetHubToken();
        hubToken.Should().NotBeNullOrEmpty("請先執行「調用端以 API Token 換取 Hub Token」步驟");
        var factory = this.ScenarioContext.Get<TestServer>("TestServer");

        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/chatHub", opts =>
            {
                opts.Transports = HttpTransportType.LongPolling;
                opts.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                opts.AccessTokenProvider = () => Task.FromResult<string?>(hubToken);
            })
            .Build();

        await connection.StartAsync();
        await connection.InvokeAsync("SendPrivateMessage", targetUser, message);
        await connection.StopAsync();
        await connection.DisposeAsync();
    }

    // ─── 資料庫斷言 Steps ─────────────────────────────────────────────────────

    [Given(@"資料庫已存在使用者 ""(.*)""")]
    public async Task Given資料庫已存在使用者(string username)
    {
        var sp = this.ScenarioContext.GetServiceProvider();
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        if (!await db.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower()))
        {
            db.Users.Add(new User
            {
                Username = username,
                Email = $"{username.ToLower()}@test.com",
                Bio = string.Empty,
                StatusMessage = string.Empty,
                PasswordHash = ChatRoom.Services.PasswordHasher.Hash("test@123"),
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }

    [Then(@"預期資料庫已存在使用者 ""(.*)""")]
    public async Task Then預期資料庫已存在使用者(string username)
    {
        var sp = this.ScenarioContext.GetServiceProvider();
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var exists = await db.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower());
        exists.Should().BeTrue($"資料庫應存在使用者 '{username}'");
    }

    [Then(@"預期資料庫已存在 ""(.*)"" 與 ""(.*)"" 的好友關係")]
    public async Task Then預期資料庫已存在好友關係(string user1, string user2)
    {
        var sp = this.ScenarioContext.GetServiceProvider();
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var exists = await db.Friendships.AnyAsync(f =>
            (f.User1.ToLower() == user1.ToLower() && f.User2.ToLower() == user2.ToLower()) ||
            (f.User1.ToLower() == user2.ToLower() && f.User2.ToLower() == user1.ToLower()));
        exists.Should().BeTrue($"資料庫應存在 '{user1}' 與 '{user2}' 的好友關係");
    }

    [Then(@"預期資料庫好友關係 ""(.*)"" 與 ""(.*)"" 只有一筆")]
    public async Task Then預期資料庫好友關係只有一筆(string user1, string user2)
    {
        var sp = this.ScenarioContext.GetServiceProvider();
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var count = await db.Friendships.CountAsync(f =>
            (f.User1.ToLower() == user1.ToLower() && f.User2.ToLower() == user2.ToLower()) ||
            (f.User1.ToLower() == user2.ToLower() && f.User2.ToLower() == user1.ToLower()));
        count.Should().Be(1, $"'{user1}' 與 '{user2}' 的好友關係應只有一筆");
    }
}
