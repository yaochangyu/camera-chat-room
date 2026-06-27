Feature: 聊天室訊息

    Background:
        Given 初始化測試伺服器
        Given 調用端已以 "Alice" 身分登入並取得 API Token

    Scenario: 取得聊天室歷史訊息（無訊息）
        When 調用端發送 "GET" 請求至 "api/rooms/general/messages"
        Then 預期得到 HttpStatusCode 為 "200"
        Then 預期回傳內容為
        """
        []
        """

    Scenario: 未帶 Authorization 查詢聊天室訊息回傳 401
        Given 調用端未設定 Authorization Header
        When 調用端發送 "GET" 請求至 "api/rooms/general/messages"
        Then 預期得到 HttpStatusCode 為 "401"

    Scenario: 傳送聊天室訊息後可查詢到歷史訊息
        Given 調用端以 API Token 換取 Hub Token
        Given 透過 SignalR 傳送聊天室訊息至房間 "general" 內容 "Hello World"
        When 調用端發送 "GET" 請求至 "api/rooms/general/messages"
        Then 預期得到 HttpStatusCode 為 "200"
        Then 預期回傳內容中路徑 "$[0].username" 的"字串等於" "alice"
        Then 預期回傳內容中路徑 "$[0].message" 的"字串等於" "Hello World"
