Feature: 私訊

    Background:
        Given 初始化測試伺服器
        Given 調用端已以 "Alice" 身分登入並取得 API Token

    Scenario: 取得私訊歷史（無訊息）
        When 調用端發送 "GET" 請求至 "api/messages/dm/Bob"
        Then 預期得到 HttpStatusCode 為 "200"
        Then 預期回傳內容為
        """
        []
        """

    Scenario: 未帶 Authorization 查詢私訊回傳 401
        Given 調用端未設定 Authorization Header
        When 調用端發送 "GET" 請求至 "api/messages/dm/Bob"
        Then 預期得到 HttpStatusCode 為 "401"

    Scenario: 傳送私訊後可查詢到歷史訊息
        Given 調用端以 API Token 換取 Hub Token
        Given 透過 SignalR 傳送私訊至使用者 "bob" 內容 "Hi Bob"
        When 調用端發送 "GET" 請求至 "api/messages/dm/bob"
        Then 預期得到 HttpStatusCode 為 "200"
        Then 預期回傳內容中路徑 "$[0].username" 的"字串等於" "alice"
        Then 預期回傳內容中路徑 "$[0].message" 的"字串等於" "Hi Bob"
