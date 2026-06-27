# Auth Security 改善計畫

> 目標：強化 ChatRoom 驗證安全性，涵蓋登入防爆破、PKCE 換票、CSP Header。
> JWT Secret 移至設定檔：**跳過（使用者決定）**

---

## 步驟一：登入端點加 Rate Limiting

- [x] 在 `Program.cs` 新增 `LoginPolicy`（FixedWindow：每 60 秒最多 5 次）
- [x] 在 `AuthController.Login` 加上 `[EnableRateLimiting("LoginPolicy")]`

**原因：** 目前 `/api/auth/login` 無任何限制，任意 username 可無限嘗試，容易遭受枚舉攻擊。

---

## 步驟二：實作 PKCE 換票機制（API Token → Hub Token）

### 概念
Client 在換 Hub Token 前，先產生 `code_verifier`（隨機字串），計算 `code_challenge = BASE64URL(SHA256(code_verifier))`。
換票時送出 `code_challenge`，Server 回傳一次性 `auth_code`；
Client 再用 `auth_code + code_verifier` 換取真正的 Hub Token。
Server 驗證 `SHA256(code_verifier) == 儲存的 code_challenge`，防止 API Token 被竊後直接換票。

```
[Client]
  1. 產生 code_verifier（32 bytes random）
  2. code_challenge = BASE64URL(SHA256(code_verifier))

[POST /api/auth/hub-token/challenge]  ← 帶 API Token + code_challenge
  Server: 驗證 API Token，將 (auth_code, code_challenge) 存入 IMemoryCache（TTL 60s）
  回傳: { auth_code }

[POST /api/auth/hub-token/exchange]   ← 帶 auth_code + code_verifier
  Server: 取出 code_challenge，驗證 SHA256(code_verifier) == code_challenge
  回傳: { token (Hub Token) }
```

### 實作項目
- [x] `AuthController` 新增 `POST /api/auth/hub-token/challenge`
  - 驗證 API Token（Audience: ChatRoomClients）
  - 接收 `{ code_challenge }`（Base64Url SHA256）
  - 產生一次性 `auth_code`（Guid）存入 `IMemoryCache`（key: auth_code, value: (username, code_challenge), TTL 60s）
  - 回傳 `{ auth_code }`
- [x] `AuthController` 新增 `POST /api/auth/hub-token/exchange`
  - 接收 `{ auth_code, code_verifier }`
  - 從 cache 取出 `(username, code_challenge)`，驗證 `BASE64URL(SHA256(code_verifier)) == code_challenge`
  - 驗證後立即從 cache 刪除（防重放）
  - 回傳 `{ token (Hub Token) }`
- [x] 原 `POST /api/auth/hub-token` **保留**（相容性），後續可標記 Deprecated
- [x] `wwwroot/index.html` 更新換票流程，改用 PKCE 兩步驟

---

## 步驟三：新增 CSP Header

- [x] 在 `Program.cs` 加入自訂 Middleware，設定 `Content-Security-Policy` Header

```
Content-Security-Policy:
  default-src 'self';
  script-src 'self' 'unsafe-inline';
  connect-src 'self' ws://localhost:* wss://localhost:*;
  style-src 'self' 'unsafe-inline';
  img-src 'self' data:;
  frame-ancestors 'none'
```

**原因：** 有靜態 `wwwroot/index.html`，無 CSP 則 XSS 可注入腳本竊取記憶體中的 Token。

> 注意：`unsafe-inline` 是因為 index.html 有 inline `<script>` 與 `<style>`。
> 若後續前端拆分為獨立 JS 檔，可改為 nonce-based CSP。

---

## 完成後驗證

- [x] `dotnet build` 無錯誤（0 errors, 0 warnings）
- [ ] 詢問是否執行整合測試

---

## 狀態

| 步驟 | 狀態 |
|------|------|
| 步驟一：Login Rate Limiting | ✅ 完成 |
| 步驟二：PKCE 換票 | ✅ 完成 |
| 步驟三：CSP Header | ✅ 完成 |
