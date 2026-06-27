# Cookie Session (方案 B) 實作計畫

## 設計

- Login 回傳 `{ token }` body（App 用）並同時設定 `HttpOnly` cookie（Browser 用）
- `GET /api/auth/session`：從 cookie 驗證身分，回傳新的 API Token body，並刷新 cookie 到期時間
- `POST /api/auth/logout`：清除 cookie
- Frontend：頁面載入時先呼叫 `/api/auth/session`，成功則略過 Login UI；失敗才顯示登入表單
- App 完全不受影響，繼續使用 Bearer Token

## Cookie 設定

| 屬性 | 值 | 理由 |
|------|-----|------|
| HttpOnly | true | JS 無法讀取，防 XSS 竊取 |
| SameSite | Strict | 防 CSRF |
| Secure | false（開發）/ true（正式） | 本機走 HTTP |
| Path | / | 全路徑有效 |
| MaxAge | 1 小時 | 與 API Token 效期一致 |

---

## 步驟

- [ ] **步驟一：`JwtTokenService` 新增 `ValidateApiToken` 方法**
  - 用 `JwtSecurityTokenHandler.ValidateToken` 驗證 cookie 內的 JWT，回傳 username 或 null
  - 原因：`/api/auth/session` 需要自行驗證 cookie token，不能走 `[Authorize]` Middleware（cookie 不是 Bearer scheme）

- [ ] **步驟二：`AuthController.Login` 設定 HttpOnly cookie**
  - 登入成功後，`Response.Cookies.Append("auth_token", token, CookieOptions)`
  - 原因：讓瀏覽器在後續請求（包含頁面重整）自動附帶 cookie

- [ ] **步驟三：新增 `GET /api/auth/session` 端點**
  - 讀取 `Request.Cookies["auth_token"]` → `ValidateApiToken` → 核發新 Token → 刷新 cookie → 回傳 `{ token }`
  - 無 cookie 或驗證失敗 → 401
  - 原因：頁面重整時前端呼叫此端點還原 JS 記憶體中的 Token

- [ ] **步驟四：新增 `POST /api/auth/logout` 端點**
  - `Response.Cookies.Delete("auth_token")` → 204
  - 原因：提供明確的登出機制，清除 cookie

- [ ] **步驟五：更新前端 `index.html`**
  - 頁面載入時：`GET /api/auth/session` 成功 → 存入 `longLivedToken`，直接顯示主畫面；失敗 → 顯示登入表單
  - 新增登出按鈕：呼叫 `POST /api/auth/logout`，清除 JS 狀態，返回登入畫面
  - 原因：實現無感知的 session 恢復

- [ ] **步驟六：新增整合測試**
  - 登入後 session 端點回傳 200 + token
  - 無 cookie 呼叫 session 回傳 401
  - 登出後 session 回傳 401
  - 原因：驗證 cookie 生命週期正確

- [ ] **步驟七：更新 README.md / blog.md**
  - 補充 Cookie Session 機制說明

- [ ] **步驟八：Build 驗證**

- [ ] **步驟九：執行整合測試**

---

## 狀態

| 步驟 | 狀態 |
|------|------|
| 步驟一：JwtTokenService.ValidateApiToken | ⬜ 待實作 |
| 步驟二：Login 設定 cookie | ⬜ 待實作 |
| 步驟三：GET /api/auth/session | ⬜ 待實作 |
| 步驟四：POST /api/auth/logout | ⬜ 待實作 |
| 步驟五：前端 session 恢復 + 登出按鈕 | ⬜ 待實作 |
| 步驟六：整合測試 | ⬜ 待實作 |
| 步驟七：文件更新 | ⬜ 待實作 |
| 步驟八：Build | ⬜ 待實作 |
| 步驟九：執行測試 | ⬜ 待實作 |
