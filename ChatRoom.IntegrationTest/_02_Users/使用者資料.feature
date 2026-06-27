Feature: 使用者資料

    Background:
        Given 初始化測試伺服器
        Given 調用端已以 "Alice" 身分登入並取得 API Token

    Scenario: 取得存在的使用者個人資料
        When 調用端發送 "GET" 請求至 "api/users/Bob"
        Then 預期得到 HttpStatusCode 為 "200"
        Then 預期回傳內容中路徑 "$.username" 的"字串等於" "Bob"

    Scenario: 查詢不存在的使用者回傳 404
        When 調用端發送 "GET" 請求至 "api/users/nobody"
        Then 預期得到 HttpStatusCode 為 "404"

    Scenario: 未帶 Authorization 查詢使用者資料回傳 401
        Given 調用端未設定 Authorization Header
        When 調用端發送 "GET" 請求至 "api/users/Bob"
        Then 預期得到 HttpStatusCode 為 "401"
