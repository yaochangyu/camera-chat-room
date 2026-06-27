# SignalR 聊天室實作計畫 (基于 .NET 10)

本計畫旨在透過 .NET 10 實作一個高效、安全且具備抗瞬斷能力的即時聊天室系統。以下為逐步實作計畫。

---

## 實作步驟

- [x] **步驟一：初始化 .NET 10 Web API 專案與設定基礎環境**
  * **為什麼需要這個步驟**：我們需要建立一個乾淨且符合 .NET 10 規範的伺服器端環境。此步驟需要：
    1. 建立 Web API 專案並配置 `.gitignore`。
    2. 安裝必要的 NuGet 套件：
       - `Microsoft.AspNetCore.SignalR.Common`
       - `Microsoft.AspNetCore.Authentication.JwtBearer`
       - `Microsoft.EntityFrameworkCore.Sqlite` (或 PostgreSQL)
       - `Microsoft.Extensions.Caching.Hybrid` (.NET 10 HybridCache)
    3. 在 `Program.cs` 規劃正確的中介軟體註冊與宣告順序，防範連線與驗證錯誤：
       - `UseRouting()` -> `UseCors()` -> `UseAuthentication()` -> `UseAuthorization()` -> `MapHub()`。

- [x] **步驟二：配置 SignalR 服務與啟用 Stateful Reconnect**
  * **為什麼需要這個步驟**：啟用 .NET 10 的 `AllowStatefulReconnects` 機制，為聊天室建立伺服器端與前端的訊息快取緩衝區，在網路短暫中斷時提供無縫的重連與訊息補發體驗。

- [x] **步驟三：設計 Strongly-typed Hub 與聊天室群組 (Groups) 管理**
  * **為什麼需要這個步驟**：使用強型別 Hub (`Hub<IChatClient>`) 可以在編譯期確保客戶端呼叫的方法名稱正確；實作群組管理（加入、離開房間）則是支援「多聊天室」功能的基礎。若考慮 Native AOT，需預留 JSON Source Generator 設定。

- [x] **步驟四：整合 JWT 驗證與實作二階段連線核發「短效 Hub Token」機制**
  * **為什麼需要這個步驟**：確保即時通訊的安全性。
    1. 實作二階段連線機制：前端先以 Header 攜帶長效 Token 請求 API，伺服器核發一個 30~60 秒過期的連線專用 JWT (Hub Token)。
    2. 對臨時 Token 的核發 API 端點（如 `/api/auth/hub-token`）套用 .NET 10 的 `Rate Limiting`（限流中介軟體）以防 DDoS 與頻繁重連負載。
    3. 配合 .NET 10 規範，在驗證失敗時直接回傳 `401 Unauthorized`，避免重導向，確保敏感 Token 不會在 URL 軌跡或日誌中曝光。

- [x] **步驟五：實作資料庫持久化與 HybridCache 歷史訊息快取**
  * **為什麼需要這個步驟**：SignalR 本身不儲存訊息狀態。為了解決使用者重連或初次進入房間時的歷史對話載入需求，我們需要整合資料庫持久化，並整合 .NET 10 的 **`HybridCache`** 快取熱點房間的歷史對話，提供高性能且防快取雪崩的架獲。

- [x] **步驟六：建立前端/測試客戶端並驗證雙向即時通訊**
  * **為什麼需要這個步驟**：透過實際的用戶端連線測試，驗證聊天訊息發送、房間切換、斷線重連。必須在前端的 `accessTokenFactory` 實作非同步獲取最新短效 Token 的邏輯，避免重試連線時因使用過期 Token 而重連失敗。
