# ChatRoom Issues Log

## Issue 1: HybridCache Compilation Error
* **步驟**：步驟五：實作資料庫持久化與 HybridCache 歷史訊息快取
* **第一次失敗**：
  在 `Hubs/ChatHub.cs` 中呼叫了 `await _cache.RemoveKeyAsync(cacheKey);`，編譯失敗，回報無此定義。
* **第二次失敗**：
  參考網路文件改為 `await _cache.RemoveByKeyAsync(cacheKey);`，但編譯仍失敗，回報無此定義。
* **原因與解決方式**：
  經由 reflection 在執行期動態檢索我們所安裝之 `Microsoft.Extensions.Caching.Hybrid` (10.7.0) 的公開方法後，證實該版本之 `HybridCache` 移除快取的方法為 **`RemoveAsync`** 與 `RemoveByTagAsync`，而非 `RemoveByKeyAsync`。
  * **解決方式**：改用 `await _cache.RemoveAsync(cacheKey);`。

## Issue 2: SQLite Schema Missing Column (ReceiverUsername)
* **步驟**：步驟六：多視窗端到端測試
* **現象**：
  發送私訊或載入私訊時失敗，伺服器日誌拋出 `Microsoft.Data.Sqlite.SqliteException: SQLite Error 1: 'table ChatMessages has no column named ReceiverUsername'`。
* **原因**：
  在實作「一對一聊天室」前，系統已經在本地產生了舊版 Schema 的 `chatroom.db`。當擴充欄位後，`Program.cs` 中的 `dbContext.Database.EnsureCreated()` 由於偵測到資料庫已存在，因此不會自動更新已存在的 Table 欄位，導致執行期找不到新欄位。
* **解決方式**：
  手動刪除本地的 `chatroom.db`，讓伺服器在重啟時由 `EnsureCreated()` 重新生成最新包含 `ReceiverUsername` 欄位的資料庫結構。
