# Integration Test 計畫

## 架構說明

- 測試框架：xUnit + Reqnroll（BDD）
- 測試伺服器：`WebApplicationFactory<Program>`
- 測試替身：SQLite In-Memory（真實資料庫引擎，非 Mock；SQLite 為 file-based，無 Server 可容器化）
- 斷言套件：FluentAssertions + Json.Path.Net
- 若未來加入 Redis / PostgreSQL，再補 TestContainers

---

## 測試案例

### Feature: 身分驗證（6 個）

| # | Scenario | 驗證重點 |
|---|---|---|
| 1 | 登入成功取得 API Token | 回傳 200 且 token 不為空 |
| 2 | 未提供使用者名稱登入回傳 400 | 輸入驗證 |
| 3 | 首次登入自動建立帳號與預設好友關係 | 新帳號建立 + 好友資料落庫 |
| 4 | 使用 API Token 換取 Hub Token | 回傳 200 且 token 不為空 |
| 5 | 使用 Hub Token 呼叫 hub-token 回傳 403 | audience 驗證 |
| 6 | 未帶 Authorization 呼叫 hub-token 回傳 401 | 驗證守門 |

### Feature: 使用者資料（3 個）

| # | Scenario | 驗證重點 |
|---|---|---|
| 7 | 取得存在的使用者個人資料 | 回傳 200 且 username 正確 |
| 8 | 查詢不存在的使用者回傳 404 | 找不到資源 |
| 9 | 未帶 Authorization 查詢使用者資料回傳 401 | 驗證守門 |

### Feature: 好友管理（5 個）

| # | Scenario | 驗證重點 |
|---|---|---|
| 10 | 取得好友清單 | 登入後自動建立的預設好友出現 |
| 11 | 新增好友 | 好友關係落庫 |
| 12 | 不能加自己為好友回傳 400 | 商業邏輯 |
| 13 | 新增不存在的使用者為好友回傳 404 | 找不到目標 |
| 14 | 重複加好友不產生重複資料 | 冪等性 |

### Feature: 聊天室訊息（2 個）

| # | Scenario | 驗證重點 |
|---|---|---|
| 15 | 取得聊天室歷史訊息（無訊息） | 回傳 200 空陣列 |
| 16 | 未帶 Authorization 查詢聊天室訊息回傳 401 | 驗證守門 |

### Feature: 私訊（2 個）

| # | Scenario | 驗證重點 |
|---|---|---|
| 17 | 取得私訊歷史（無訊息） | 回傳 200 空陣列 |
| 18 | 未帶 Authorization 查詢私訊回傳 401 | 驗證守門 |

---

## 實作步驟

- [x] 步驟一：建立測試專案 `ChatRoom.IntegrationTest`（xUnit + Reqnroll + FluentAssertions + Json.Path.Net）
- [x] 步驟二：建立 `TestServer.cs`（繼承 `WebApplicationFactory<Program>`）
- [x] 步驟三：建立 `TestAssistant.cs` 與 `ScenarioContextExtension.cs`
- [x] 步驟四：建立 `BaseStep.cs`（Before/AfterScenario、共用 HTTP Steps）
- [x] 步驟五：建立 `_01_Auth/` — Feature 檔
- [x] 步驟六：建立 `_02_Users/` — Feature 檔
- [x] 步驟七：建立 `_03_Friends/` — Feature 檔
- [x] 步驟八：建立 `_04_Rooms/` — Feature 檔
- [x] 步驟九：建立 `_05_Messages/` — Feature 檔
- [x] 步驟十：加入測試專案至 `chat-room.slnx`，執行所有測試 — **18/18 全部通過**
- [x] 步驟十一：更新 `@tree.md`

---

## 修正記錄

- `FriendsController.AddFriend`、`GetFriends`、`MessagesController.GetDmMessages`、`AuthController.HubToken`：
  移除 `ClaimsPrincipal user` action 參數，改用 `ControllerBase.User` 屬性，避免 .NET 10 `[ApiController]` 模型綁定將其誤判為 `[FromBody]`
- 主專案 `chat-room.csproj` 加入 `<Compile Remove="ChatRoom.IntegrationTest/**" />` 排除規則，
  避免 Reqnroll 生成的 `*.feature.cs` 被主專案 glob 到
- `Program.cs` 加入 `public partial class Program { }` 供 `WebApplicationFactory<Program>` 存取
