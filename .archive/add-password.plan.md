# Add Password 實作計畫

## 現況分析

- `LoginRequest` 有 `Password` 欄位，但 `AuthController.Login` 從未驗證
- `User` model 無 `PasswordHash` 欄位
- 測試與前端皆送空字串 `password: ""`
- 首次登入自動建立帳號，需決定密碼規則

## 密碼規則

- 空密碼 → 400 Bad Request
- 非空密碼 + 新用戶 → 建立帳號並儲存 hash
- 非空密碼 + 既有用戶 → 驗證 hash，不符回 401
- 雜湊演算法：`Rfc2898DeriveBytes.Pbkdf2`（PBKDF2-SHA256，100000 次，已內建於 .NET，無需額外套件）

## 測試預設密碼

- 整合測試統一使用 `test@123` 作為測試帳號密碼
- Seed 帳號（Bob / Charlie / David）於 `Program.cs` 設定固定 hash

---

## 步驟

- [x] **步驟一：新增 `PasswordHasher` 服務**
  - 新增 `Services/PasswordHasher.cs`，提供 `Hash(password)` 與 `Verify(password, hash)` 兩個靜態方法
  - 原因：集中密碼雜湊邏輯，Controller 與 Seed 都使用同一套實作

- [ ] **步驟二：`User` model 新增 `PasswordHash` 欄位**
  - 在 `Models/User.cs` 加入 `public string PasswordHash { get; set; } = string.Empty;`
  - 原因：資料庫需儲存雜湊後的密碼

- [ ] **步驟三：更新 `AuthController.Login` 驗證邏輯**
  - 空密碼 → 400
  - 新用戶：hash 後寫入 DB，回傳 token
  - 既有用戶：`PasswordHasher.Verify` 失敗 → 401
  - 原因：實際執行密碼驗證

- [ ] **步驟四：更新 `Program.cs` Seed 資料**
  - Bob / Charlie / David 各自設定固定 `PasswordHash`（對應密碼如 `bob123`）
  - `BaseStep.cs` 的 `Given資料庫已存在使用者` 也要加 `PasswordHash`（使用 `test@123`）
  - 原因：Seed 帳號與測試建立帳號都需要有效的 hash

- [ ] **步驟五：更新整合測試**
  - `BaseStep.cs` `Given調用端已以身分登入並取得ApiToken`：password 改為 `"test@123"`
  - `身分驗證.feature`：所有 `"password": ""` 改為 `"password": "test@123"`
  - 新增 Scenario：密碼錯誤回傳 401
  - 原因：測試需覆蓋密碼驗證的正常與異常路徑

- [ ] **步驟六：更新前端**
  - `wwwroot/index.html` 登入區加入 `<input type="password">` 欄位
  - `login()` 函式從 input 取 password 值，改寫 fetch body
  - 原因：使用者需輸入密碼才能登入

- [ ] **步驟七：更新流程圖與文件**
  - `README.md` / `blog.md` 流程圖登入步驟加回 `{ username, password }`
  - 原因：現在 password 真的有被驗證了

- [ ] **步驟八：Build 驗證**

- [ ] **步驟九：詢問是否執行整合測試**

---

## 狀態

| 步驟 | 狀態 |
|------|------|
| 步驟一：PasswordHasher 服務 | ⬜ 待實作 |
| 步驟二：User model | ⬜ 待實作 |
| 步驟三：AuthController | ⬜ 待實作 |
| 步驟四：Seed 資料 | ⬜ 待實作 |
| 步驟五：整合測試 | ⬜ 待實作 |
| 步驟六：前端 | ⬜ 待實作 |
| 步驟七：文件 | ⬜ 待實作 |
| 步驟八：Build | ⬜ 待實作 |
| 步驟九：執行測試 | ⬜ 待實作 |
