# WebRTC 1-on-1 通話驗證計畫

## 任務
驗證 camera-chat-room 的 WebRTC 語音/視訊通話功能正確運作。

## Critical Points

- [x] CP1: Charlie 登入後看到「已連線」狀態（SignalR 連線成功）
- [x] CP2: Bob 登入後看到「已連線」狀態（SignalR 連線成功）
- [x] CP3: Charlie 的好友列表顯示 Bob，且通話按鈕（📞 📹）可點擊
- [x] CP4: Charlie 點擊語音通話按鈕後，撥號覆蓋層（#call-overlay）出現
- [x] CP5: Bob 看到來電 Modal（#incoming-call-modal），顯示 Charlie 來電
- [x] CP6: 來電 Modal 顯示「語音通話」字樣
- [x] CP7: Bob 拒絕來電後，Charlie 的撥號覆蓋層自動關閉
- [x] CP8: Bob 接聽來電後，Bob 也出現撥號覆蓋層（通話中 UI）
- [x] CP9: Charlie 點擊 📹 按鈕，Bob 的來電 Modal 顯示「視訊通話」
- [x] CP10: 掛斷後雙方覆蓋層均消失，通話結束

## 驗證結果

**狀態：SUCCESS**

所有 10 個 Critical Points 全部通過。

額外發現：在 CP8（Bob 接聽語音通話）後，系統日誌顯示 `WebRTC 狀態: connected`，
代表真實 P2P 媒體連線也成功建立（不只是 Signaling 交換）。

驗證腳本：`final_runs/run_001/final_script.py`
截圖：`final_runs/run_001/screenshots/`（11 張）
日誌：`final_runs/run_001/final_script_log.txt`
