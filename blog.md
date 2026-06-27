---
title: '[.NET] SPA 安全整合 SignalR：以 PKCE 三階段換票與 CSP 聯防打造的安全防線'
abstract: <p>在 SPA 架構下，整合 ASP.NET Core SignalR 常面臨安全挑戰。由於瀏覽器的 WebSocket 握手階段無法自訂 Headers，開發者常被迫將 JWT 放入 Query String 傳遞，使 Token 暴露於 URL 中。此外，一旦 SPA 遭遇 XSS 漏洞，記憶體中的憑證仍有被竊取的風險。<br>本文將介紹如何以 PKCE (Proof Key for Code Exchange) 三階段換票機制安全換取 30 秒短效 Hub Token，並結合 CSP (Content Security Policy)<br>聯防作為最後防線，從後端配置一路講到前端原生 JavaScript 整合，為 SignalR Hub 提供全方位的安全防護。</p><figure class="image"><img style="aspect-ratio:1376/768;" src="https://dotblogsfile.blob.core.windows.net/user/余小章/52186d91-0092-4b31-8df6-20af509b46f3/1782576214.png.png" width="1376" height="768"></figure>
keywords: ASP.NET Core,ASP.NET Core SignalR
categories: ASP.NET Core SignalR
weblogName: 余小章 @ 大內殿堂
postId: 52186d91-0092-4b31-8df6-20af509b46f3
postDate: 2026-06-27T15:46:14.0000000
postStatus: 
dontInferFeaturedImage: false
stripH1Header: true
---
# [.NET] SPA 安全整合 SignalR：以 PKCE 三階段換票與 CSP 聯防打造的安全防線

## 開發環境

- OS：Windows 11 + WSL2 Ubuntu 22.04
- .NET：10.0.102
- ASP.NET Core SignalR：10.0.x（內建，不需另外裝套件）
- 前端：原生 HTML + JavaScript（@microsoft/signalr CDN）
- 資料庫：SQLite（測試用）

---

## 零、驗證流程

### 帳號建立機制

本專案採「**首次登入即建立帳號**」設計，沒有獨立的 `/register` 端點：

- 輸入的 `username` **不存在** → 以該密碼建立帳號（PBKDF2-SHA256 hash 後存入 DB），自動加 Seed 帳號為好友，回傳 API Token
- 輸入的 `username` **已存在** → 驗證密碼 hash，正確登入、錯誤回 401

密碼用 `Rfc2898DeriveBytes.Pbkdf2`（10 萬次迭代、16 bytes 隨機 Salt、SHA-256），不存明文，不需引入額外套件（.NET 內建）。

```
// Services/PasswordHasher.cs
public static string Hash(string password)
{
    var salt = RandomNumberGenerator.GetBytes(16);
    var hash = Rfc2898DeriveBytes.Pbkdf2(
        Encoding.UTF8.GetBytes(password), salt, 100_000, HashAlgorithmName.SHA256, 32);
    return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
}

public static bool Verify(string password, string storedHash)
{
    var parts = storedHash.Split(':');
    if (parts.Length != 2) return false;
    var salt = Convert.FromBase64String(parts[0]);
    var expected = Convert.FromBase64String(parts[1]);
    var actual = Rfc2898DeriveBytes.Pbkdf2(
        Encoding.UTF8.GetBytes(password), salt, 100_000, HashAlgorithmName.SHA256, 32);
    return CryptographicOperations.FixedTimeEquals(actual, expected); // 防時序攻擊
}
```

`CryptographicOperations.FixedTimeEquals` 讓比對時間固定，避免 timing attack 洩漏 hash 差異位置。

### PKCE 三階段換票

WebSocket 握手無法帶 Authorization header，這個限制讓 SignalR + JWT 的設定多了一道手續。這個專案用 **PKCE（Proof Key for Code Exchange）三階段換票**解決：

1. **長效 API Token**：`POST /api/auth/login { username, password }`，效期 1 小時，給一般 REST API 用。加了 Rate Limiting（每 60 秒最多 5 次），防帳號枚舉。
2. **PKCE Challenge**：連線前，Client 產生 `code_verifier`（32 bytes 隨機字串），計算 `code_challenge = BASE64URL(SHA256(code_verifier))`，帶著長效 token + `code_challenge` 呼叫 `POST /api/auth/hub-token/challenge`。Server 把 `(username, code_challenge)` 存進 MemoryCache（TTL 60s），回傳一次性 `auth_code`。
3. **PKCE Exchange → 短效 Hub Token**：Client 再用 `auth_code + code_verifier` 呼叫 `POST /api/auth/hub-token/exchange`。Server 驗算 `SHA256(code_verifier) == 儲存的 code_challenge`，通過才回傳效期 30 秒、Audience 限定為 `ChatRoomSignalRHub` 的短效 token，驗算後立即從 cache 刪除（防重放）。

為什麼三階段比兩階段更安全？PKCE 加了一個「你才知道 verifier」的綁定，就算長效 token 被攔截，攻擊者拿不到 `code_verifier`，就無法完成 Exchange 換出 Hub Token。短效 token 只有 30 秒且 Audience 受限，就算連 query string 的 token 也被抓走，用途與時效都已降到最低。

### 驗證 → 連線 → 私訊 → 大廳 完整流程

```
sequenceDiagram
    autonumber
    actor A as 用戶端 A (Alice)
    participant API as Auth API
    participant H as SignalR ChatHub
    participant DB as SQLite 資料庫
    participant B as 用戶端 B (Bob)

    Note over A, API: 一、登入取得長效 API Token
    A->>API: POST /api/auth/login { username, password }
    API-->>A: { token: API_Token (1h) } + Set-Cookie: auth_token (HttpOnly)

    Note over A, API: 一 (B)、頁面重整時 Session 恢復（瀏覽器專用）
    A->>API: GET /api/auth/session (cookie 自動帶)
    API-->>A: { token: 新 API_Token (1h) } + 刷新 cookie

    Note over A, API: 二、PKCE 換票取得短效 Hub Token
    A->>A: 產生 code_verifier，計算 code_challenge = BASE64URL(SHA256(verifier))
    A->>API: POST /api/auth/hub-token/challenge<br/>Bearer API_Token + { code_challenge }
    API->>API: 存入 MemoryCache(auth_code → code_challenge, TTL 60s)
    API-->>A: { auth_code }
    A->>API: POST /api/auth/hub-token/exchange { auth_code, code_verifier }
    API->>API: 驗算 SHA256(verifier)==challenge，刪除 cache（防重放）
    API-->>A: { token: Hub_Token (30s, aud=ChatRoomSignalRHub) }

    Note over A, B: 三、連線與狀態更新
    A->>H: 建立 WebSocket 連線 (query string 攜帶短效 Hub Token)
    activate H
    H->>H: 驗證 Audience=ChatRoomSignalRHub，連線數 +1，判定 Alice 上線
    H->>DB: 查詢 Alice 的好友名單
    DB-->>H: 回傳好友名單 [Bob, Charlie, David]
    H->>H: 廣播最新在線使用者名單 (UpdateUserList)
    H->>B: 推送狀態變更事件 FriendStatusChanged(Alice, Online, 自訂狀態)
    deactivate H

    Note over A, B: 四、一對一私聊 (Alice 傳送給 Bob)
    A->>H: 呼叫 SendPrivateMessage(Bob, "哈囉")
    activate H
    H->>DB: 寫入對話紀錄 (Sender=Alice, Receiver=Bob, Message="哈囉")
    H->>H: 清除 HybridCache 私聊快取 messages-dm-alice-bob
    H->>B: 定點推送 ReceivePrivateMessage(Alice, "哈囉")
    H-->>A: 回傳 invoke 成功
    deactivate H

    Note over A, B: 五、公共大廳訊息 (Alice 傳送大廳)
    A->>H: 呼叫 SendMessageToRoom("lobby", "大家好")
    activate H
    H->>DB: 寫入對話紀錄 (Sender=Alice, Room="lobby", Message="大家好")
    H->>H: 清除 HybridCache 大廳快取 messages-lobby
    H->>H: 廣播 ReceiveMessage(Alice, "大家好") 給大廳所有成員
    H-->>B: 推送 ReceiveMessage
    H-->>A: 推送 ReceiveMessage
    deactivate H
```

---

## 一、後端設定

### 1-1 SignalR 服務與 JWT 設定

.NET 10 已內建 SignalR，不用裝額外套件：

```
// 註冊 SignalR 服務
builder.Services.AddSignalR();
```

JWT 這邊要特別處理：WebSocket 握手不能帶 Authorization header，要改從 query string 取 token。少了這段，Hub 連線永遠 401。

```
// JWT Bearer 事件：WebSocket 連線從 query string 取 access_token
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chatHub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
```

PKCE 換票需要 `IMemoryCache` 暫存 `auth_code`：

```
// PKCE auth_code 暫存用（TTL 60 秒的一次性票）
builder.Services.AddMemoryCache();
```

PKCE 的兩支換票端點邏輯：

```
// Challenge：驗證 API Token，存 code_challenge，回傳一次性 auth_code
[HttpPost("hub-token/challenge")]
[Authorize]
public IActionResult HubTokenChallenge([FromBody] PkceChallengeRequest request)
{
    // 確認 audience 是 API token 而非 Hub token
    var isApiToken = User.HasClaim("aud", "ChatRoomClients");
    if (!isApiToken) return Forbid();

    var authCode = Guid.NewGuid().ToString("N");
    cache.Set($"pkce:{authCode}", (username, request.CodeChallenge), TimeSpan.FromSeconds(60));
    return Ok(new { auth_code = authCode });
}

// Exchange：驗算 code_verifier，防重放後回傳短效 Hub Token
[HttpPost("hub-token/exchange")]
public IActionResult HubTokenExchange([FromBody] PkceExchangeRequest request)
{
    if (!cache.TryGetValue($"pkce:{request.AuthCode}", out (string Username, string CodeChallenge) entry))
        return BadRequest(new { message = "無效或已過期的 auth_code" });

    cache.Remove($"pkce:{request.AuthCode}"); // 立即刪除，防重放

    using var sha256 = SHA256.Create();
    var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(request.CodeVerifier));
    var computedChallenge = Base64UrlEncoder.Encode(hash);

    if (computedChallenge != entry.CodeChallenge)
        return BadRequest(new { message = "code_verifier 驗證失敗" });

    return Ok(new { token = tokenService.GenerateHubToken(entry.Username) });
}
```

### 1-2 Map Hub 路由

```
// ChatHub 掛在 /chatHub，啟用 Stateful Reconnect（.NET 8+ 支援）
app.MapHub<ChatHub>("/chatHub", options =>
{
    options.AllowStatefulReconnects = true;
});
```

`AllowStatefulReconnects` 讓短暫斷線重連時，Hub 上的群組成員與狀態不會被清掉。

### 1-3 自訂 UserId Provider

SignalR 預設用 `ClaimTypes.NameIdentifier` 當 UserId。如果 JWT 的 sub 跟你資料庫的 username 不一樣，或你想統一正規化，就自訂：

```
// 用 Identity.Name（JWT sub）正規化後當 SignalR UserId
public class NameUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return UsernameNormalizer.NormalizeNullable(connection.User?.Identity?.Name);
    }
}
```

注意要在 `AddSignalR()` 之前註冊，否則 DI 不會用你自訂的：

```
// 必須在 AddSignalR 之前，才能覆寫預設行為
builder.Services.AddSingleton<IUserIdProvider, NameUserIdProvider>();
builder.Services.AddSignalR();
```

---

## 二、Hub 生命週期

Hub 有三個關鍵事件。缺任何一個，上下線狀態就會歪掉。

### 2-1 強型別 Client 介面

先定義前端可以收到的事件，這樣 typo 在 build 時就能抓到，不用等到 runtime：

```
// 前端可接收的所有推送事件
public interface IChatClient
{
    Task ReceiveMessage(string user, string message);
    Task ReceivePrivateMessage(string sender, string message);
    Task UpdateUserList(string[] users);
    Task UserJoined(string connectionId, string roomName);
    Task UserLeft(string connectionId, string roomName);
    Task ReceiveNotification(string message);
    Task FriendStatusChanged(string username, bool isOnline, string? statusMessage);
}
```

Hub 繼承 `Hub<IChatClient>`，不要用沒有型別的 `Hub`：

```
[Authorize]
public class ChatHub : Hub<IChatClient>
{
    // ConcurrentDictionary 管理線上使用者，Key 是正規化後的 username，Value 是連線數
    private static readonly ConcurrentDictionary<string, int> _onlineUsers = new();
}
```

### 2-2 OnConnectedAsync — 上線事件

同一個帳號用三個分頁連進來，這個事件會被觸發三次。連線數從 0 變 1 才算真正上線。

```
// 上線事件：驗證 token、維護線上名單、通知好友
public override async Task OnConnectedAsync()
{
    // 驗證這條連線的 token audience 是否正確
    var isHubToken = Context.User?.HasClaim("aud", "ChatRoomSignalRHub") ?? false;
    if (!isHubToken)
    {
        Context.Abort(); // audience 不對，直接斷線
        return;
    }

    var username = UsernameNormalizer.NormalizeNullable(Context.User?.Identity?.Name);
    if (!string.IsNullOrEmpty(username))
    {
        bool isNewlyOnline = false;
        // AddOrUpdate 是 thread-safe 的原子操作
        _onlineUsers.AddOrUpdate(username,
            k => { isNewlyOnline = true; return 1; },
            (k, val) => val + 1);

        await BroadcastOnlineUsers();

        if (isNewlyOnline)
            await NotifyFriendsStatusChanged(username, true);
    }

    await base.OnConnectedAsync();
}
```

### 2-3 OnDisconnectedAsync — 下線事件

這邊有個坑：單純 `TryGetValue` 再 `TryUpdate` 在多連線同時斷線時會有競態 (Race Condition)，計數可能卡住，讓使用者永遠顯示線上。要用 CAS 重試迴圈：

```
// 下線事件：CAS 重試遞減連線數，全斷才視為完全下線
public override async Task OnDisconnectedAsync(Exception? exception)
{
    var username = UsernameNormalizer.NormalizeNullable(Context.User?.Identity?.Name);
    if (!string.IsNullOrEmpty(username))
    {
        bool isFullyOffline = false;
        while (true)
        {
            if (!_onlineUsers.TryGetValue(username, out var count)) break;

            if (count <= 1)
            {
                if (_onlineUsers.TryRemove(username, out _))
                { isFullyOffline = true; break; }
                continue; // TryRemove 失敗，重試
            }

            if (_onlineUsers.TryUpdate(username, count - 1, count)) break;
            // TryUpdate 失敗代表被其他執行緒搶先更新，繼續重試
        }

        await BroadcastOnlineUsers();

        if (isFullyOffline)
            await NotifyFriendsStatusChanged(username, false);
    }

    await base.OnDisconnectedAsync(exception);
}
```

### 2-4 Hub Method — 用戶端呼叫的伺服器方法

前端透過 `invoke` 呼叫這些方法；伺服器在裡面處理邏輯、推送結果：

```
// 群組聊天：存訊息到 DB，廣播給同房間所有人
public async Task SendMessageToRoom(string roomName, string message)
{
    var user = UsernameNormalizer.NormalizeNullable(Context.User?.Identity?.Name) ?? "anonymous";
    _dbContext.ChatMessages.Add(new ChatMessage { Username = user, RoomName = roomName, Message = message });
    await _dbContext.SaveChangesAsync();
    await Clients.Group(roomName).ReceiveMessage(user, message);
}

// 一對一私訊：只推給目標帳號的所有連線（多分頁全收到）
public async Task SendPrivateMessage(string targetUser, string message)
{
    var sender = UsernameNormalizer.NormalizeNullable(Context.User?.Identity?.Name);
    var canonicalTarget = UsernameNormalizer.NormalizeNullable(targetUser);
    if (string.IsNullOrEmpty(sender) || string.IsNullOrEmpty(canonicalTarget)) return;
    await Clients.User(canonicalTarget).ReceivePrivateMessage(sender, message);
}
```

`Clients.User(userId)` 會推給同一個 UserId 的所有連線，ConnectionId 管理不用自己來（這不是這篇的重點，就不多說了）。

---

## 三、前端使用 SignalR

### 3-1 引入套件

```
<!-- SignalR JS 套件（cdnjs） -->
<script src="https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js"></script>
```

注意 CDN 來源要和 CSP `script-src` 允許的清單一致，否則瀏覽器會封鎖腳本，`signalR` 變數就不存在了。

### 3-2 PKCE helper

瀏覽器原生 `crypto.subtle` 就能做 SHA-256，不需要引入任何套件：

```
function generateCodeVerifier() {
    const array = new Uint8Array(32);
    crypto.getRandomValues(array);
    return btoa(String.fromCharCode(...array))
        .replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
}

async function generateCodeChallenge(verifier) {
    const data = new TextEncoder().encode(verifier);
    const hash = await crypto.subtle.digest('SHA-256', data);
    return btoa(String.fromCharCode(...new Uint8Array(hash)))
        .replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
}

async function getHubTokenWithPkce() {
    const codeVerifier = generateCodeVerifier();
    const codeChallenge = await generateCodeChallenge(codeVerifier);

    // 第一步：拿 challenge 換 auth_code
    const challengeRes = await fetch('/api/auth/hub-token/challenge', {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${longLivedToken}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ codeChallenge })
    });
    const { auth_code } = await challengeRes.json();

    // 第二步：拿 auth_code + verifier 換 Hub Token
    const exchangeRes = await fetch('/api/auth/hub-token/exchange', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ authCode: auth_code, codeVerifier })
    });
    const { token } = await exchangeRes.json();
    return token;
}
```

### 3-3 建立連線

`accessTokenFactory` 每次連線握手前都會呼叫，改成非同步跑完整的 PKCE 換票：

```
// 建立 HubConnection，每次連線前自動走 PKCE 換票取得最新短效 Hub Token
function _01_建立連線() {
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/chatHub", {
            accessTokenFactory: () => getHubTokenWithPkce()
        })
        .withAutomaticReconnect()   // 預設退避：0s → 2s → 10s → 30s
        .build();
    return connection;
}
```

`withAutomaticReconnect()` 不傳參數就用預設退避策略，也可以傳自訂間隔陣列。

### 3-4 綁定推送事件

事件一定要在 `start()` 之前綁好，不然連線建立瞬間推過來的訊息會漏掉：

```
// start() 之前先綁好所有事件
function _02_綁定事件(connection) {
    // 群組訊息
    connection.on("ReceiveMessage", (user, message) => {
        console.log(`[群組] ${user}: ${message}`);
    });

    // 私訊
    connection.on("ReceivePrivateMessage", (sender, message) => {
        console.log(`[私訊] ${sender}: ${message}`);
    });

    // 線上使用者名單更新
    connection.on("UpdateUserList", (users) => {
        // users 是 string[]，拿來渲染 sidebar 好友狀態
    });

    // 好友上下線通知
    connection.on("FriendStatusChanged", (username, isOnline, statusMessage) => {
        console.log(`${username} 目前 ${isOnline ? "上線" : "下線"}`);
    });

    // 系統通知（別 alert，用 toast）
    connection.on("ReceiveNotification", (msg) => {
        console.log("[通知]", msg);
    });
}
```

### 3-5 啟動連線與呼叫伺服器方法

```
// 啟動連線，成功後才能 invoke Hub 方法
async function _03_啟動並呼叫(connection) {
    try {
        await connection.start();
        console.log("✅ SignalR 連線成功");

        await connection.invoke("JoinRoom", "general");
        await connection.invoke("SendMessageToRoom", "general", "大家好！");
        await connection.invoke("SendPrivateMessage", "bob", "嗨，私訊你");

    } catch (err) {
        console.error("❌ 連線失敗:", err);
    }
}
```

`invoke` 是非同步的，一定要 await，失敗要 catch，不然 unhandled rejection 噴掉了啦!!!

### 3-6 監控連線狀態

```
// 斷線、重連中、重連成功事件
function _04_監控狀態(connection) {
    connection.onclose((error) => {
        console.warn("連線斷開", error);
    });

    connection.onreconnecting((error) => {
        console.warn("重連中...", error);
    });

    connection.onreconnected((connectionId) => {
        console.log("✅ 重連成功，新 ConnectionId:", connectionId);
    });
}
```

### 3-7 Cookie Session 與頁面重整自動重連

WebSocket 握手過後，使用者重新整理頁面，`accessToken` 就從記憶體消失了，每次都要重新輸入帳密很煩。但 App 端（iOS / Android）不會有這個問題，token 存在 Keychain / Keystore。

**方案選擇**

|  | 瀏覽器 | App |
| --- | --- | --- |
| Token 存放 | HttpOnly Cookie（JS 不可讀，防 XSS 竊取） | Keychain / Keystore |
| Session 恢復 | `GET /api/auth/session`（Cookie 自動帶） | 直接用儲存的 Token |
| 後端改動 | 需要 `/session` + `/logout` 端點 | 不需要 |

採「**方案 B**」：登入時同時設 HttpOnly Cookie，瀏覽器走 `/session` 換票；App 端不用 Cookie，繼續用 Bearer Token，兩者互不干擾。

**後端：設 Cookie**

```
private void SetAuthCookie(string token)
{
    Response.Cookies.Append("auth_token", token, new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Secure = false, // 本機 http 開發；正式環境改 true
        Path = "/",
        MaxAge = TimeSpan.FromHours(1)
    });
}

[HttpGet("session")]
public IActionResult Session()
{
    if (!Request.Cookies.TryGetValue("auth_token", out var cookieToken))
        return Unauthorized();

    var username = tokenService.ValidateApiToken(cookieToken);
    if (username is null) return Unauthorized();

    var newToken = tokenService.GenerateApiToken(username);
    SetAuthCookie(newToken); // 刷新到期時間
    return Ok(new { token = newToken });
}

[HttpPost("logout")]
public IActionResult Logout()
{
    Response.Cookies.Delete("auth_token");
    return NoContent();
}
```

**前端：頁面載入時恢復 Session**

```
async function restoreSession() {
    try {
        const res = await fetch('/api/auth/session');
        if (!res.ok) return;

        const { token } = await res.json();
        longLivedToken = token;
        currentUser = parseJwt(token).sub;
        await fetchFriends();

        if (sessionStorage.getItem('wasConnected') === 'true') {
            await startSignalR(); // 上次有連線 → 自動重連
        }
    } catch (_) { /* 靜默失敗，讓使用者手動登入 */ }
}

window.addEventListener('DOMContentLoaded', restoreSession);
```

`sessionStorage` **記錄連線狀態**

`sessionStorage` 的生命週期是「同一個分頁的整個瀏覽器 session」：關分頁就清除，但重新整理不清除。利用這個特性記錄「刷新前是否有開連線」：

```
async function startSignalR() {
    await connection.start();
    sessionStorage.setItem('wasConnected', 'true');

    connection.onclose(() => {
        sessionStorage.removeItem('wasConnected'); // 主動斷線清掉
    });
}

async function logout() {
    sessionStorage.removeItem('wasConnected'); // 登出一定清除
    await connection?.stop();
    await fetch('/api/auth/logout', { method: 'POST' });
}
```

多個 Tab 各自有獨立的 `sessionStorage`，不互相干擾。

**parseJwt 的 base64url 陷阱**

JWT Payload 用 Base64**URL** 編碼（`-` 代替 `+`、`_` 代替 `/`），但 `atob()` 只接受標準 Base64，直接丟進去會拋例外：

```
// 正確做法：先轉換字元
function parseJwt(token) {
    const base64 = token.split('.')[1]
        .replace(/-/g, '+')
        .replace(/_/g, '/');
    return JSON.parse(atob(base64));
}
```

---

## 四、CSP（Content Security Policy）

SignalR + PKCE 解決了 Token 安全，但如果頁面本身可以被 XSS 注入，攻擊者還是可以直接竊取記憶體裡的 `longLivedToken`。加 CSP 是最後一道防線。

後端用 Middleware 注入 header，放在 `UseStaticFiles()` 之前讓所有回應都套到：

```
app.Use(async (context, next) =>
{
    context.Response.Headers.Append(
        "Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://fonts.googleapis.com; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://fonts.gstatic.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "connect-src 'self' ws: wss:; " +
        "img-src 'self' data:; " +
        "frame-ancestors 'none'");
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
```

幾個要注意的點：

- `script-src` 必須包含實際使用的 CDN 來源，少一個就封鎖一個，`signalR` 不存在就全壞了（踩過）
- `connect-src 'self' ws: wss:` — `ws:`/`wss:` 允許任意主機的 WebSocket，`'self'` 涵蓋同源的 `fetch`
- `'unsafe-inline'` 因為 index.html 內有 inline `<script>` 和 `<style>`；若拆成獨立 JS 檔可改成 nonce-based
- `frame-ancestors 'none'` 防點擊劫持（Clickjacking）

服務啟動後的樣貌

![](https://dotblogsfile.blob.core.windows.net/user/余小章/52186d91-0092-4b31-8df6-20af509b46f3/1782575420.png.png)

---

## 完整程式碼位置

<https://github.com/yaochangyu/chat-room>
