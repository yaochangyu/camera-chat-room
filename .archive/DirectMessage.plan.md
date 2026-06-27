# 一對一聊天室與聯絡人清單實作計畫

本計畫旨在擴充現有的 SignalR 聊天室系統，使其支援「左側聯絡人清單（在線使用者）」與「右側一對一私聊訊息視窗」。

---

## 實作步驟

- [x] **步驟一：擴充資料模型以支援私訊儲存**
  * **為什麼需要這個步驟**：修改 `ChatMessage` 模型，新增 `ReceiverUsername` 欄位以區分「群組廣播訊息」與「一對一私聊訊息」，確保資料庫能正確儲存與識別私訊對話。

- [x] **步驟二：實作自訂 `IUserIdProvider` 服務**
  * **為什麼需要這個步驟**：ASP.NET Core SignalR 預設以 `NameIdentifier` 作為連線 ID。我們需要實作以使用者名稱（Username）為標識的 `NameUserIdProvider` 並註冊至 DI 容器中，以便在 Hub 中使用 `Clients.User(username)` 定點發送私訊。

- [x] **步驟三：修改 `ChatHub` 支援私訊傳送與在線狀態廣播**
  * **為什麼需要這個步驟**：
    1. 實作 `SendPrivateMessage` 方法，將私訊傳送給目標使用者。
    2. 維護一個全域的在線使用者清單，並在使用者連線 (`OnConnectedAsync`) 與中斷 (`OnDisconnectedAsync`) 時，自動向所有在線客戶端廣播最新名單。

- [x] **步驟四：新增一對一歷史對話 API 與 HybridCache 最佳實踐**
  * **為什麼需要這個步驟**：
    1. 新增 API 端點 `/api/messages/dm/{targetUser}`，讀取雙方的一對一歷史對話。
    2. 使用 `HybridCache` 優化此 API 的效能（快取 Key 採用雙方名稱排序拼接，例如 `messages-dm-alice-bob`）。
    3. 在發送私訊時，自動失效（Evict）該對應的快取。

- [x] **步驟五：更新前端 `index.html` 介面與私聊邏輯**
  * **為什麼需要這個步驟**：
    - 將 `wwwroot/index.html` 改造成雙欄配置：左側顯示「在線聯絡人清單（點擊可切換聊天對象）」，右側顯示「私聊訊息對話框」。
    - 前端調整連線與接收邏輯，以對接全新的私聊 Hub 事件與歷史紀錄拉取。

- [x] **步驟六：進行多視窗端到端測試**
  * **為什麼需要這個步驟**：開啟多個瀏覽器分頁登入不同帳號，驗證左側聯絡人清單是否即時更新、一對一私訊發送與接收是否正常，並確保 `HybridCache` 與資料庫持久化行為正確。
