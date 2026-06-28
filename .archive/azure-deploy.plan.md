# Azure App Service 部署計畫

## 目標
將 camera-chat-room（ASP.NET Core 10 + SignalR + SQLite）部署到 Azure App Service，
並透過 GitHub Actions 實現 CI/CD 自動部署。

## 架構
```
GitHub Push → GitHub Actions → dotnet publish → Azure App Service (Linux)
                                                       │
                                              App Settings（JWT, TURN）
                                              WebSocket 啟用
                                              /home/data/chatroom.db（持久化）
```

## 注意事項
- SQLite 路徑改為 `/home/data/`（Azure App Service Linux 唯一持久目錄）
- JWT Secret 從 hardcode 改為讀取環境變數 / App Settings
- TURN 憑證從 App Settings 注入
- 單一執行個體（B1 Basic），避免 SQLite 多實例競爭

---

## 步驟

- [x] **步驟 1：將 JWT Secret 改為從組態讀取**（PoC 略過，維持 hardcode）
  - 為什麼：hardcode 的 Secret 不安全，部署到 Azure 後需從 App Settings 注入。
  - 修改 `Program.cs`：`jwtSecret = builder.Configuration["Jwt:Secret"]`
  - 修改 `appsettings.json`：新增預設值（開發用）
  - 修改 `appsettings.Development.json`：保留本機開發值

- [x] **步驟 2：將 SQLite 路徑改為 `/home/data/`（Linux 持久磁碟）**
  - 為什麼：Azure App Service Linux 的 `/home` 是持久掛載點，重啟不會遺失資料；其他路徑重啟後消失。
  - 修改 `Program.cs`：Production 環境用 `/home/data/`，其他環境維持原邏輯。

- [x] **步驟 3：建立 Azure 資源**
  - 為什麼：需要 Resource Group、App Service Plan、Web App 才能部署。
  - 使用 Azure CLI 建立：
    - Resource Group: `rg-camera-chat-room`
    - App Service Plan: `asp-camera-chat-room`（Linux B1）
    - Web App: `camera-chat-room`（.NET 10）
  - 啟用 WebSocket（SignalR 必要）

- [x] **步驟 4：設定 Azure App Settings**
  - 為什麼：敏感設定（JWT Secret、TURN Key）不能放 appsettings.json，透過 App Settings 注入到環境變數。
  - 設定項目：
    - `Jwt__Secret`
    - `Turn__ApiKey`
    - `Turn__Host`
    - `WEBSITES_PORT=8080`（或讓 Kestrel 讀 PORT 環境變數）

- [x] **步驟 5：建立 GitHub Actions CI/CD Workflow**
  - 為什麼：每次 push 到 main 時自動 build + deploy，不需手動操作。
  - 建立 `.github/workflows/azure-deploy.yml`
  - 取得 Azure Publish Profile → 存入 GitHub Secret `AZURE_WEBAPP_PUBLISH_PROFILE`

- [x] **步驟 6：首次部署並驗證**
  - 為什麼：確認部署成功且功能正常（登入、SignalR 連線、通話 Signaling）。
  - 確認項目：
    - [ ] App Service 啟動，首頁回應 200
    - [ ] 使用者可登入（JWT 正常）
    - [ ] SignalR WebSocket 連線成功（「已連線」）
    - [ ] 好友通話 UI 正常
