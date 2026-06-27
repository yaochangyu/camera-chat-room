# 撰寫專案 README.md 實作計畫

本計畫旨在根據聊天室專案目前的架構與現況（包含 JWT + 短效 Hub Token 二階段驗證、即時好友機制、個人檔案展示與 HybridCache 快取優化等），撰寫一份詳細的專案說明文件 `@README.md`。

---

## 實作步驟

- [x] **步驟一：撰寫 `@README.md` 內容草稿**
  * **為什麼需要這個步驟**：整理專案架構（.NET 10 Minimal API、SQLite、SignalR）、認證流程（長效 API Token 與短效 Hub Token 交換機制）、API 路由清單、SignalR Hub 事件、資料庫 Seed Data 與前端 UI 特色（好友在線圓點與毛玻璃個人資料 Modal），確保 README 完整反映現況。

- [x] **步驟二：建立專案根目錄之 `@README.md` 檔案**
  * **為什麼需要這個步驟**：將整理完成的 README 內容寫入專案根目錄的 `@README.md` 實體檔案中。

- [x] **步驟三：更新專案目錄維護檔 `@tree.md`**
  * **為什麼需要這個步驟**：新增了 `@README.md`，必須依專案規範更新 `@tree.md` 檔案以確保資料夾結構的一致性。

- [x] **步驟四：將計畫書歸檔至 `.archive`**
  * **為什麼需要這個步驟**：本計畫書所有步驟執行完畢後，將移動到封存資料夾 `.archive/`。
