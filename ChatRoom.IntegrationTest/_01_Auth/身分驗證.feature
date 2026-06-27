Feature: 身分驗證

    Background:
        Given 初始化測試伺服器

    Scenario: 登入成功取得 API Token
        Given 調用端已準備 Body 參數(Json)
        """
        { "username": "Alice", "password": "test@123" }
        """
        When 調用端發送 "POST" 請求至 "api/auth/login"
        Then 預期得到 HttpStatusCode 為 "200"
        Then 預期回傳內容中路徑 "$.token" 的"字串不為空"

    Scenario: 未提供使用者名稱登入回傳 400
        Given 調用端已準備 Body 參數(Json)
        """
        { "username": "", "password": "test@123" }
        """
        When 調用端發送 "POST" 請求至 "api/auth/login"
        Then 預期得到 HttpStatusCode 為 "400"

    Scenario: 未提供密碼登入回傳 400
        Given 調用端已準備 Body 參數(Json)
        """
        { "username": "Alice", "password": "" }
        """
        When 調用端發送 "POST" 請求至 "api/auth/login"
        Then 預期得到 HttpStatusCode 為 "400"

    Scenario: 首次登入自動建立帳號與預設好友關係
        Given 調用端已準備 Body 參數(Json)
        """
        { "username": "NewUser", "password": "test@123" }
        """
        When 調用端發送 "POST" 請求至 "api/auth/login"
        Then 預期得到 HttpStatusCode 為 "200"
        Then 預期資料庫已存在使用者 "NewUser"
        Then 預期資料庫已存在 "NewUser" 與 "Bob" 的好友關係

    Scenario: 密碼錯誤登入回傳 401
        Given 調用端已以 "Alice" 身分登入並取得 API Token
        Given 調用端已準備 Body 參數(Json)
        """
        { "username": "Alice", "password": "wrongpassword" }
        """
        When 調用端發送 "POST" 請求至 "api/auth/login"
        Then 預期得到 HttpStatusCode 為 "401"

    Scenario: 使用 API Token 換取 Hub Token
        Given 調用端已以 "Alice" 身分登入並取得 API Token
        When 調用端發送 "POST" 請求至 "api/auth/hub-token"
        Then 預期得到 HttpStatusCode 為 "200"
        Then 預期回傳內容中路徑 "$.token" 的"字串不為空"

    Scenario: 使用 Hub Token 呼叫 hub-token 回傳 403
        Given 調用端已以 "Alice" 身分登入並取得 Hub Token
        When 調用端發送 "POST" 請求至 "api/auth/hub-token"
        Then 預期得到 HttpStatusCode 為 "403"

    Scenario: 未帶 Authorization 呼叫 hub-token 回傳 401
        When 調用端發送 "POST" 請求至 "api/auth/hub-token"
        Then 預期得到 HttpStatusCode 為 "401"

    Scenario: 登入後呼叫 session 端點回傳 200 與新 Token
        Given 調用端已以 "Alice" 身分登入並取得 API Token
        When 調用端發送 "GET" 請求至 "api/auth/session"
        Then 預期得到 HttpStatusCode 為 "200"
        Then 預期回傳內容中路徑 "$.token" 的"字串不為空"

    Scenario: 無 Cookie 呼叫 session 端點回傳 401
        When 調用端發送 "GET" 請求至 "api/auth/session"
        Then 預期得到 HttpStatusCode 為 "401"

    Scenario: 登出後呼叫 session 端點回傳 401
        Given 調用端已以 "Alice" 身分登入並取得 API Token
        When 調用端發送 "POST" 請求至 "api/auth/logout"
        Then 預期得到 HttpStatusCode 為 "204"
        When 調用端發送 "GET" 請求至 "api/auth/session"
        Then 預期得到 HttpStatusCode 為 "401"
