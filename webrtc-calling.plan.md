# WebRTC 1-on-1 語音/視訊通話實作計畫

## 目標
在現有 SignalR 聊天室上，新增好友間 1 對 1 語音與視訊通話功能。
媒體串流走 WebRTC P2P，SignalR 作為 Signaling Channel，TURN 使用 Metered.ca。

## 架構摘要
```
瀏覽器 A ──SignalR Signaling──▶ ChatHub ──▶ 瀏覽器 B
     │                                          │
     └────── WebRTC P2P（音訊/視訊串流）────────┘
                    （必要時經 TURN 中繼）
```

---

## 步驟

- [x] **步驟 1：申請 Metered.ca TURN 帳號並取得憑證**
  - 為什麼：生產環境必須有 TURN Server 才能穿透 NAT/防火牆，Metered.ca 提供免費額度且支援 ephemeral credentials。
  - 動作：至 https://www.metered.ca/ 申請帳號，取得 API Key 與 TURN Host。
  - 將以下設定加入 `appsettings.json`（敏感值放環境變數）：
    ```json
    "Turn": {
      "ApiKey": "",
      "Host": ""
    }
    ```

- [x] **步驟 2：新增 TURN Credential API（後端）**
  - 為什麼：前端不能直接持有長效 TURN 憑證，需由後端動態產生時效性憑證（Ephemeral Credentials）防止濫用。
  - 新增 `Controllers/TurnController.cs`，呼叫 Metered.ca API 取得短效 ICE Server 清單並回傳給前端。
  - 端點：`GET /api/turn/credentials`（需登入）

- [x] **步驟 3：新增 Call Signaling Model（後端）**
  - 為什麼：Hub 方法需要強型別的 DTO 傳遞 WebRTC SDP Offer/Answer 與 ICE Candidate。
  - 新增 `Models/CallSignal.cs`，包含：
    - `CallOfferDto`（CallerUsername, SdpOffer）
    - `CallAnswerDto`（CallerUsername, SdpAnswer）
    - `IceCandidateDto`（TargetUsername, Candidate, SdpMid, SdpMLineIndex）

- [x] **步驟 4：在 ChatHub 新增 Signaling 方法（後端）**
  - 為什麼：WebRTC 建立連線需要雙方交換 SDP 與 ICE Candidate，SignalR Hub 作為中介轉發這些訊號。
  - 在 `Hubs/ChatHub.cs` 新增：
    - `CallUser(CallOfferDto dto)` — 發起通話，轉發 Offer 給對方
    - `AnswerCall(CallAnswerDto dto)` — 接受通話，回傳 Answer
    - `RejectCall(string callerUsername)` — 拒絕通話
    - `HangUp(string targetUsername)` — 掛斷
    - `RelayICECandidate(IceCandidateDto dto)` — 轉發 ICE Candidate
  - 在 `IChatClient.cs` 新增對應的 Client 介面方法：
    - `ReceiveCall`, `CallAnswered`, `CallRejected`, `CallEnded`, `ReceiveICECandidate`

- [x] **步驟 5：前端通話 UI**
  - 為什麼：使用者需要有發起通話、接受/拒絕來電、掛斷、靜音、關閉鏡頭的操作介面。
  - 在 `wwwroot/index.html` 新增：
    - 好友列表旁的「語音」📞 / 「視訊」📹 按鈕
    - 來電通知 Modal（顯示來電者、接聽/拒絕按鈕）
    - 通話中覆蓋層（本地/遠端 `<video>` 元素、靜音/關閉鏡頭/掛斷按鈕）

- [x] **步驟 6：前端 WebRTC 核心邏輯**
  - 為什麼：需要建立 `RTCPeerConnection`，取得本地媒體，並透過 SignalR 交換 SDP 與 ICE Candidate 完成 P2P 連線。
  - 實作內容：
    - `getUserMedia({ audio: true, video: true/false })` 取得媒體串流
    - 建立 `RTCPeerConnection`，設定從後端取得的 ICE Server 清單
    - 發起方：建立 Offer → 透過 SignalR 送出
    - 接收方：收到 Offer → 建立 Answer → 透過 SignalR 回傳
    - 雙方互相轉發 ICE Candidate
    - 連線建立後顯示遠端串流於 `<video>` 元素
    - 掛斷時關閉 `RTCPeerConnection` 並停止媒體軌道

- [x] **步驟 7：整合測試與驗證**
  - 為什麼：確認通話流程在實際瀏覽器環境中正常運作（局域網與跨網路）。
  - 測試項目：
    - [x] Build 無錯誤（`dotnet build`）— 通過，0 errors 0 warnings
    - [x] 整合測試全過（`dotnet test`）— 25/25 passed，現有功能無回歸
    - [ ] **⚠️ TURN API Key 待修正**：Metered.ca 後台「API Keys」分頁的 API Key 與「TURN Credentials」頁的密碼不同。目前 `appsettings.Development.json` 填入的是 TURN 密碼，需改為 API Key（`GET /api/v1/turn/credentials?apiKey=<正確key>` 才能回傳 ICE Servers）。
    - [ ] 發起語音通話 → 對方收到來電 → 接聽 → 通話成功（需瀏覽器 + 正確 TURN key）
    - [ ] 發起視訊通話 → 雙方看到彼此畫面（需瀏覽器 + 正確 TURN key）
    - [ ] 拒絕來電 → 發起方收到通知（需瀏覽器）
    - [ ] 通話中掛斷 → 雙方結束通話（需瀏覽器）
    - [ ] 靜音/關閉鏡頭切換正常（需瀏覽器）

---

## 注意事項
- TURN 憑證（API Key）不可 hardcode，統一走環境變數
- 將來換自架 coturn，只需修改 `appsettings.json`，程式碼不動
- `<video>` 元素需設定 `autoplay playsinline muted`（本地預覽靜音避免回音）
