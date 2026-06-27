# Move Minimal APIs to Controllers

## 步驟

- [x] 步驟一：在 `Program.cs` 加入 `AddControllers()` 與 `MapControllers()`
- [x] 步驟二：建立 `Controllers/AuthController.cs`（login、hub-token）
- [x] 步驟三：建立 `Controllers/RoomsController.cs`（room messages）
- [x] 步驟四：建立 `Controllers/MessagesController.cs`（DM messages）
- [x] 步驟五：建立 `Controllers/UsersController.cs`（user profile）
- [x] 步驟六：建立 `Controllers/FriendsController.cs`（friends list、add friend）
- [x] 步驟七：移除 `Program.cs` 中所有 Minimal API 端點，更新 `@tree.md`
- [x] 步驟八：Build 驗證 — 0 errors, 0 warnings
