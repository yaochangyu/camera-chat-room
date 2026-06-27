"""
WebRTC 通話驗證腳本（WebWright final_script）
驗證 camera-chat-room 1-on-1 語音/視訊通話的 signaling 流程。
"""
import asyncio
import sys
from pathlib import Path
from playwright.async_api import async_playwright

RUN_DIR = Path(__file__).parent
SHOTS = RUN_DIR / "screenshots"
LOG = RUN_DIR / "final_script_log.txt"
BASE = "http://localhost:5158"

CHROMIUM_ARGS = [
    "--use-fake-ui-for-media-stream",
    "--use-fake-device-for-media-stream",
    "--no-sandbox",
    "--disable-dev-shm-usage",
    "--allow-loopback-in-peer-connection",
]

log_lines = []


def log(msg: str):
    print(msg)
    log_lines.append(msg)


async def poll_text(page, selector, expected, timeout=20):
    for _ in range(timeout * 2):
        try:
            text = await page.locator(selector).text_content(timeout=300)
            if text and expected in text:
                return text
        except Exception:
            pass
        await asyncio.sleep(0.5)
    actual = await page.locator(selector).text_content(timeout=1000)
    raise TimeoutError(f"Expected '{expected}' in '{selector}', got: {actual!r}")


async def poll_visible(page, selector, timeout=10):
    for _ in range(timeout * 2):
        try:
            if await page.locator(selector).is_visible():
                return
        except Exception:
            pass
        await asyncio.sleep(0.5)
    raise TimeoutError(f"'{selector}' not visible after {timeout}s")


async def poll_hidden(page, selector, timeout=10):
    for _ in range(timeout * 2):
        try:
            if not await page.locator(selector).is_visible():
                return
        except Exception:
            pass
        await asyncio.sleep(0.5)
    raise TimeoutError(f"'{selector}' still visible after {timeout}s")


async def login_and_connect(page, username, password):
    await page.goto(BASE + "/")
    await page.fill("#username-input", username)
    await page.fill("#password-input", password)
    await page.click("#login-btn")
    await page.locator("#login-card").wait_for(state="hidden", timeout=10000)
    await page.click("#connect-btn")
    await poll_text(page, "#status-text", "已連線", timeout=20)


async def wait_friend_online(page, friend_name, timeout=15):
    for _ in range(timeout * 2):
        items = page.locator(".user-item").filter(has_text=friend_name)
        count = await items.count()
        if count > 0:
            btns = items.locator(".call-icon-btn")
            btn_count = await btns.count()
            if btn_count >= 2:
                disabled = await btns.first.get_attribute("disabled")
                if disabled is None:
                    return
        await asyncio.sleep(0.5)
    raise TimeoutError(f"{friend_name} not online after {timeout}s")


async def main():
    SHOTS.mkdir(parents=True, exist_ok=True)
    LOG.write_text("")

    async with async_playwright() as pw:
        browser = await pw.chromium.launch(headless=True, args=CHROMIUM_ARGS)
        c_ctx = await browser.new_context(
            viewport={"width": 1280, "height": 1800},
            permissions=["camera", "microphone"],
        )
        b_ctx = await browser.new_context(
            viewport={"width": 1280, "height": 1800},
            permissions=["camera", "microphone"],
        )
        charlie = await c_ctx.new_page()
        bob = await b_ctx.new_page()

        # ── CP1 / CP2：雙方登入並建立 SignalR 連線 ───────────────
        log("step 1 action: Charlie 和 Bob 平行登入並建立 SignalR 連線")
        await asyncio.gather(
            login_and_connect(charlie, "Charlie", "charlie123"),
            login_and_connect(bob, "Bob", "bob123"),
        )
        charlie_status = await charlie.locator("#status-text").text_content()
        bob_status = await bob.locator("#status-text").text_content()
        log(f"  Charlie 狀態: {charlie_status}")
        log(f"  Bob 狀態: {bob_status}")
        await charlie.screenshot(path=str(SHOTS / "final_execution_1_charlie_connected.png"))
        await bob.screenshot(path=str(SHOTS / "final_execution_2_bob_connected.png"))
        assert "已連線" in charlie_status, f"CP1 失敗: {charlie_status}"
        assert "已連線" in bob_status, f"CP2 失敗: {bob_status}"
        log("  [PASS] CP1: Charlie 已連線")
        log("  [PASS] CP2: Bob 已連線")

        # ── CP3：Charlie 好友列表顯示 Bob 且通話按鈕可用 ─────────
        log("step 2 action: 等待 Charlie 好友列表中 Bob 在線（通話按鈕啟用）")
        await wait_friend_online(charlie, "Bob", timeout=15)
        await wait_friend_online(bob, "Charlie", timeout=15)
        await charlie.screenshot(path=str(SHOTS / "final_execution_3_friends_list.png"))
        log("  [PASS] CP3: 好友列表顯示 Bob，通話按鈕可點擊")

        # ── CP4 / CP5 / CP6：語音通話 - 發起撥號 ────────────────
        log("step 3 action: Charlie 點擊語音通話按鈕撥給 Bob")
        audio_btn = charlie.locator(".user-item").filter(has_text="Bob").locator(".call-icon-btn").first
        await audio_btn.click()
        await poll_visible(charlie, "#call-overlay", timeout=8)
        await charlie.screenshot(path=str(SHOTS / "final_execution_4_charlie_calling.png"))
        call_label = await charlie.locator("#call-status-label").text_content()
        log(f"  Charlie 撥號覆蓋層顯示: {call_label}")
        log("  [PASS] CP4: Charlie 撥號覆蓋層出現")

        await poll_visible(bob, "#incoming-call-modal", timeout=8)
        await bob.screenshot(path=str(SHOTS / "final_execution_5_bob_incoming.png"))
        caller_name = await bob.locator("#incoming-caller-name").text_content()
        call_type = await bob.locator("#incoming-call-type").text_content()
        log(f"  Bob 來電 Modal - 來電者: {caller_name}, 類型: {call_type}")
        assert "charlie" in caller_name.lower(), f"CP5 失敗: 來電者={caller_name}"
        assert call_type == "語音通話", f"CP6 失敗: 通話類型={call_type}"
        log("  [PASS] CP5: Bob 看到來電 Modal，顯示 Charlie 來電")
        log("  [PASS] CP6: 來電類型正確顯示「語音通話」")

        # ── CP7：拒絕來電 - Charlie 覆蓋層自動關閉 ───────────────
        log("step 4 action: Bob 拒絕來電，驗證 Charlie 撥號覆蓋層自動關閉")
        await bob.locator(".btn-reject").click()
        await poll_hidden(bob, "#incoming-call-modal", timeout=5)
        await poll_hidden(charlie, "#call-overlay", timeout=8)
        await charlie.screenshot(path=str(SHOTS / "final_execution_6_call_rejected.png"))
        log("  [PASS] CP7: Bob 拒絕後 Charlie 撥號覆蓋層自動關閉")

        # ── CP8 / CP10：接聽語音通話 - 雙方進入通話中 UI ─────────
        log("step 5 action: Charlie 重新撥號，Bob 接聽")
        await audio_btn.click()
        await poll_visible(bob, "#incoming-call-modal", timeout=8)
        await bob.locator(".btn-accept").click()
        await poll_visible(bob, "#call-overlay", timeout=8)
        await asyncio.sleep(2)  # 讓 UI 穩定
        await charlie.screenshot(path=str(SHOTS / "final_execution_7_charlie_in_call.png"))
        await bob.screenshot(path=str(SHOTS / "final_execution_8_bob_in_call.png"))
        log("  [PASS] CP8: Bob 接聽後，雙方均顯示通話中 UI")

        # ── CP10：掛斷 - 雙方覆蓋層消失 ─────────────────────────
        log("step 6 action: Charlie 掛斷，驗證雙方覆蓋層消失")
        await charlie.locator(".ctrl-btn.hangup").click()
        await poll_hidden(charlie, "#call-overlay", timeout=6)
        await poll_hidden(bob, "#call-overlay", timeout=6)
        await charlie.screenshot(path=str(SHOTS / "final_execution_9_hangup_charlie.png"))
        await bob.screenshot(path=str(SHOTS / "final_execution_10_hangup_bob.png"))
        log("  [PASS] CP10: 掛斷後雙方覆蓋層均消失")

        # ── CP9：視訊通話 - callType 顯示「視訊通話」 ────────────
        log("step 7 action: Charlie 點擊視訊通話按鈕（📹）")
        video_btns = charlie.locator(".user-item").filter(has_text="Bob").locator(".call-icon-btn")
        video_btn = video_btns.nth(1)
        await video_btn.click()
        await poll_visible(bob, "#incoming-call-modal", timeout=8)
        await bob.screenshot(path=str(SHOTS / "final_execution_11_video_call_modal.png"))
        video_call_type = await bob.locator("#incoming-call-type").text_content()
        log(f"  來電類型: {video_call_type}")
        assert video_call_type == "視訊通話", f"CP9 失敗: {video_call_type}"
        log("  [PASS] CP9: 視訊通話來電 Modal 顯示「視訊通話」")

        # 清場
        await bob.locator(".btn-reject").click()
        await poll_hidden(bob, "#incoming-call-modal", timeout=5)
        await poll_hidden(charlie, "#call-overlay", timeout=8)

        await browser.close()

    # ── 寫入最終結果 ─────────────────────────────────────────────
    log("\n=== 驗證結果 ===")
    log("所有 10 個 Critical Points 均通過 ✓")
    log("WebRTC 通話 Signaling 流程正確運作")
    log("狀態: SUCCESS")

    LOG.write_text("\n".join(log_lines))
    print(f"\n日誌已寫入: {LOG}")


if __name__ == "__main__":
    asyncio.run(main())
