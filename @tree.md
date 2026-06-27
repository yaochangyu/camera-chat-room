# Project Structure

```
chat-room/
├── .archive/
│   ├── ChatRoom.plan.md      # 已完成之實作計畫檔案 (.NET 10)
│   ├── DirectMessage.plan.md # 已完成之私聊實作計畫檔案 (.NET 10)
│   ├── Friendship.plan.md    # 已完成之好友機制實作計畫檔案 (.NET 10)
│   ├── Readme.plan.md        # 已完成之專案 README 實作計畫檔案 (.NET 10)
│   ├── compress-blog-intro.plan.md # 已完成之壓縮部落格前言實作計畫 (.NET 10)
│   ├── generate-png-images.plan.md # 已完成之產生四種風格 PNG 圖片實作計畫 (.NET 10)
│   ├── generate-svg-images.plan.md # 已完成之產生四種風格 SVG 圖片實作計畫 (.NET 10)
│   └── update-blog-intro.plan.md # 已完成之調整部落格前言實作計畫 (.NET 10)
├── .issues/
│   └── ChatRoom.issues.md    # 記錄實作過程中碰到的錯誤與排除方式
├── ChatRoom.IntegrationTest/
│   ├── ChatRoom.IntegrationTest.csproj
│   ├── BaseStep.cs           # 所有共用 BDD Steps（HTTP、斷言、DB 驗證）
│   ├── ScenarioContextExtension.cs  # ScenarioContext 存取擴充方法
│   ├── TestServer.cs         # WebApplicationFactory with SQLite In-Memory
│   ├── Usings.cs             # 全域 using 與 assembly 屬性
│   ├── _01_Auth/
│   │   └── 身分驗證.feature
│   ├── _02_Users/
│   │   └── 使用者資料.feature
│   ├── _03_Friends/
│   │   └── 好友管理.feature
│   ├── _04_Rooms/
│   │   └── 聊天室訊息.feature
│   └── _05_Messages/
│       └── 私訊.feature
├── Controllers/
│   ├── AuthController.cs     # 登入與 Hub Token 換發 API
│   ├── FriendsController.cs  # 好友清單與加好友 API
│   ├── MessagesController.cs # 私訊歷史訊息 API
│   ├── RoomsController.cs    # 聊天室歷史訊息 API
│   ├── TurnController.cs     # TURN 短效憑證 API（WebRTC ICE Server）
│   └── UsersController.cs    # 使用者個人資料 API
├── Data/
│   └── ChatDbContext.cs      # EF Core DbContext 類別 (SQLite)
├── Hubs/
│   ├── IChatClient.cs        # 強型別 Hub 用戶端介面（含通話 Signaling）
│   └── ChatHub.cs            # SignalR 聊天室 Hub 類別（含通話 Signaling）
├── Models/
│   ├── ChatMessage.cs        # 聊天訊息資料模型
│   ├── User.cs               # 使用者個人資訊檔案模型
│   ├── Friendship.cs         # 好友關係模型
│   ├── AddFriendRequest.cs   # 好友 API 請求資料傳輸模型 (DTO)
│   ├── CallSignal.cs         # WebRTC 通話 Signaling DTO
│   └── LoginRequest.cs       # 登入 API 請求資料傳輸模型 (DTO)
├── Services/
│   ├── JwtTokenService.cs    # 產生 API 與 Hub Token 的驗證服務
│   ├── NameUserIdProvider.cs # 提供自訂 SignalR User ID (Username)
│   └── UsernameNormalizer.cs # 統一 username 正規化規則
├── Properties/
│   └── launchSettings.json   # 專案啟動設定檔
├── wwwroot/
│   └── index.html            # 聊天室前端展示與測試網頁
├── @tree.md                  # 專案資料夾結構維護檔案
├── @README.md                # 專案說明與運作指南文件
├── SignalR_BestPractices.md  # ASP.NET Core SignalR 最佳實踐文件 (.NET 10)
├── Program.cs                # 應用程式進入點與服務配置
├── chat-room.csproj          # .NET 10 專案設定檔
├── chat-room.http            # HTTP 請求測試檔
├── appsettings.json          # 全域組態設定檔
├── appsettings.Development.json # 開發環境組態設定檔
├── net_spa_secure_integration_of_signalr_building_a_defense_line_with_pkce_three_stage_ticket_exchange_and_csp_joint_defense_claymorphism.png # 部落格封面圖 - 3D 黏土擬態風格 PNG
├── net_spa_secure_integration_of_signalr_building_a_defense_line_with_pkce_three_stage_ticket_exchange_and_csp_joint_defense_line_art.png # 部落格封面圖 - 極簡資安線條風格 PNG
├── net_spa_secure_integration_of_signalr_building_a_defense_line_with_pkce_three_stage_ticket_exchange_and_csp_joint_defense_ink_wash.png # 部落格封面圖 - 東方水墨禪意風格 PNG
├── net_spa_secure_integration_of_signalr_building_a_defense_line_with_pkce_three_stage_ticket_exchange_and_csp_joint_defense_pixel_art.png # 部落格封面圖 - 8位元復古遊戲風格 PNG
├── net_spa_secure_integration_of_signalr_building_a_defense_line_with_pkce_three_stage_ticket_exchange_and_csp_joint_defense_neo_brutalism.svg # 部落格封面圖 - 新粗獷主義風格 SVG
├── net_spa_secure_integration_of_signalr_building_a_defense_line_with_pkce_three_stage_ticket_exchange_and_csp_joint_defense_glassmorphism.svg # 部落格封面圖 - 玻璃擬態風格 SVG
├── net_spa_secure_integration_of_signalr_building_a_defense_line_with_pkce_three_stage_ticket_exchange_and_csp_joint_defense_flat_tech.svg # 部落格封面圖 - 扁平科技插畫風格 SVG
├── net_spa_secure_integration_of_signalr_building_a_defense_line_with_pkce_three_stage_ticket_exchange_and_csp_joint_defense_retro_cyber.svg # 部落格封面圖 - 復古終端與賽步龐克風格 SVG
└── .gitignore                # Git 忽略清單
```
