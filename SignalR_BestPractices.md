# ASP.NET Core SignalR 最佳實踐 (.NET 10)

本文件整理了基於 **.NET 10** 與 **ASP.NET Core SignalR** 開發即時聊天室系統的最佳實踐與注意事項。

---

## 1. 身分驗證最佳實踐

在 SignalR (尤其是 WebSockets) 中，由於瀏覽器限制無法自訂 HTTP Header，我們主要有以下兩種安全的身分驗證方案：

### 方案 A：Cookie-based Authentication (瀏覽器環境推薦)
若您的前後端部署在同一個網域下，Cookie 驗證是首選，因為 Cookie 會由瀏覽器在 WebSocket 握手時自動帶上，且不暴露在 URL 中。
* **CORS 配置**：必須指定信任的 Origin，並啟用 `.AllowCredentials()`。
* **安全屬性**：
  * `HttpOnly = true`（防止 XSS 讀取）
  * `SecurePolicy = CookieSecurePolicy.Always`（強制僅透過 HTTPS 傳送）
  * `SameSite = SameSiteMode.Strict`（同網域下防範 CSRF 最安全）

---

### 方案 B：JWT 驗證 + 短期「連線專用 Token」(跨網域/多端點共用推薦)
若您必須維持 JWT 作法（例如跨網域、有行動端 App），且必須在 URL Query String 中傳遞 `access_token`，請務必遵循以下防護措施來確保安全：

#### 1. 使用極短效的「Hub 連線專用 Token (Hub Token)」
**絕對不要**將原本用於 API 呼叫的長效 JWT Token (通常為 1 小時至數天) 直接放在 URL 中傳遞。
* **二階段連線機制**：
  1. 使用者先透過標準 API 請求（其 Authorization Header 帶有長效 Token，非常安全），向伺服器申請一個「SignalR 連線金鑰」。
  2. 伺服器核發一個**極短效（30 ~ 60 秒過期）**的專用 JWT Token (Hub Token)。
  3. 此短效 Token 僅包含基本的使用者識別資訊 (如 `sub` 或 `userId`)，且其 `Audience` 或 `Scope` 必須限制為僅能用於驗證 `/chatHub`，**無法**用於呼叫任何其他 API 端點。
  4. 前端取得該短效 Token 後，隨即將其傳入 `accessTokenFactory` 發起 SignalR 連線。
  5. 由於連線建立（Handshake）通常在幾秒內完成，一旦 WebSocket 管道建立，即使 Token 在 30 秒後過期也不會影響已建立的連線。如此一來，即使該 Token 洩漏到 Log 中，攻擊者拿到時也早已失效。

#### 2. 核發端點限流與安全防護
* **限流保護 (Rate Limiting)**：核發短效 Token 的 API 端點 (如 `/api/auth/hub-token`) 容易在網路不穩、大量客戶端頻繁斷線重連時承受極高負載，甚至遭受 DoS 攻擊。**必須**在該端點套用 .NET 10 的 `Rate Limiting` 中介軟體進行頻率限制。
* **前端非同步獲取**：前端的 `accessTokenFactory` 必須是非同步函數。每次 SignalR 嘗試連接或重新進行協議握手時，都必須即時向伺服器請求全新的短效 Token，切勿重複使用已過期的 Token：
  ```javascript
  const connection = new signalR.HubConnectionBuilder()
      .withUrl("/chatHub", {
          accessTokenFactory: async () => {
              // 每次連接或重連時，非同步獲取最新的短效 Token
              const response = await fetch("/api/auth/hub-token", {
                  headers: { "Authorization": `Bearer ${userLongLivedToken}` }
              });
              const data = await response.json();
              return data.token;
          }
      })
      .withAutomaticReconnect()
      .build();
  ```

#### 3. 伺服器端日誌去識別化 (Log Redaction)
必須防止 Web 伺服器與 APM (Application Performance Monitoring) 系統將 Token 寫入 Access Log 中。
* **Reverse Proxy 設定**：在 Nginx 中，日誌格式應記錄不含參數的 `$uri`，而不是含參數的 `$request_uri`。
* **伺服器 Log 遮罩**：配置 .NET 10 的日誌篩選器，對包含 `/chatHub` 與 `access_token=` 的請求網址進行遮罩 (Masking)。

#### 4. 伺服器端 Token 攔截 (Program.cs)
```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                
                // 僅在 SignalR Hub 的路徑攔截 Query String
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chatHub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
```

---

## 2. 連線穩定性與 Stateful Reconnect (.NET 8+)

* **啟用有狀態重新連線 (Stateful Reconnect)**：
  此功能可在網路短暫中斷時自動在伺服器與用戶端快取訊息，並使用 ACK 機制進行補發，防止訊息遺失，且不會改變用戶端的 Connection ID。
  * *伺服器端配置*：
    ```csharp
    app.MapHub<ChatHub>("/chatHub", options =>
    {
        options.AllowStatefulReconnects = true;
    });
    ```
  * *注意*：Stateful Reconnect 屬於「最佳努力 (best-effort)」機制。若斷線時間過長導致快取溢位，連線仍會重置 Connection ID。

---

## 3. 效能與傳輸優化

* **使用 MessagePack 協定**：
  若聊天室訊息吞吐量極高，建議以 **MessagePack** 取代 JSON 作為序列化協定，能顯著降低封包體積。
* **高頻事件節流 (Throttling)**：
  對於「輸入中... (Typing)」或「已讀狀態」等高頻率發送的事件，前端應實作節流機制，限制每秒發送最多 2 次。
* **設定最大訊息限制**：
  預設 SignalR 的單一訊息上限為 **32 KB**。若使用者會發送大型文字或 JSON，可適度調大限制。

---

## 4. 水平擴充 (Scale-out) 與高可用性

* **Sticky Sessions (會話親和性)**：
  當使用多台 Web 伺服器進行負載平衡時，負載平衡器**必須**開啟 Sticky Sessions。
* **同步 Backplane**：
  多伺服器架構下，必須配置 **Azure SignalR Service** 或自建 **Redis Backplane** 進行跨伺服器的訊息同步。

---

## 5. 安全、資料持久化與快取

* **CORS 配置**：
  切勿將 CORS 設為 `AllowAnyOrigin` (*)。必須明確指定信任的前端網域，並啟用 `AllowCredentials()`。
* **中介軟體 (Middleware) 註冊順序**：
  在 `Program.cs` 中，中介軟體的宣告順序極度重要，順序錯誤將直接導致 CORS 被阻擋或驗證失效。正確順序如下：
  ```csharp
  app.UseRouting();
  app.UseCors("CorsPolicy");   // 必須在 Routing 之後，Authentication 之前
  app.UseAuthentication();     // 必須在 Authorization 之前
  app.UseAuthorization();
  app.MapHub<ChatHub>("/chatHub"); // 必須在 Authorization 之後
  ```
* **訊息持久化與 HybridCache (.NET 10)**：
  SignalR 只負責**訊息傳輸**，並不具備儲存能力。
  * 所有聊天訊息應寫入資料庫（如 PostgreSQL/SQLite）。
  * 針對聊天室「歷史訊息」的查詢優化，建議整合 .NET 10 的 **`HybridCache`**，以提供高性能的快取機制，並具備內建的快取雪崩防護（Stampede Protection）。
* **Native AOT 部署注意 (若適用)**：
  若專案未來計畫採用 Native AOT 部署，必須使用 `System.Text.Json` 的 **Source Generator**（例如宣告 `JsonSerializable`）來避免執行期因反射 (Reflection) 失敗而導致 Hub 崩潰。
