Feature: 好友管理

    Background:
        Given 初始化測試伺服器
        Given 調用端已以 "Alice" 身分登入並取得 API Token

    Scenario: 取得好友清單
        When 調用端發送 "GET" 請求至 "api/friends"
        Then 預期得到 HttpStatusCode 為 "200"
        Then 預期回傳好友清單包含 "Bob"

    Scenario: 新增好友
        Given 資料庫已存在使用者 "Eve"
        Given 調用端已準備 Body 參數(Json)
        """
        { "targetUsername": "Eve" }
        """
        When 調用端發送 "POST" 請求至 "api/friends/add"
        Then 預期得到 HttpStatusCode 為 "200"
        Then 預期資料庫已存在 "Alice" 與 "Eve" 的好友關係

    Scenario: 不能加自己為好友回傳 400
        Given 調用端已準備 Body 參數(Json)
        """
        { "targetUsername": "alice" }
        """
        When 調用端發送 "POST" 請求至 "api/friends/add"
        Then 預期得到 HttpStatusCode 為 "400"

    Scenario: 新增不存在的使用者為好友回傳 404
        Given 調用端已準備 Body 參數(Json)
        """
        { "targetUsername": "nobody" }
        """
        When 調用端發送 "POST" 請求至 "api/friends/add"
        Then 預期得到 HttpStatusCode 為 "404"

    Scenario: 重複加好友不產生重複資料
        Given 調用端已準備 Body 參數(Json)
        """
        { "targetUsername": "Bob" }
        """
        When 調用端發送 "POST" 請求至 "api/friends/add"
        Then 預期得到 HttpStatusCode 為 "200"
        Then 預期資料庫好友關係 "Alice" 與 "Bob" 只有一筆
