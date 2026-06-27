import { test, expect, chromium, Browser, BrowserContext, Page } from '@playwright/test';

const BASE = 'http://localhost:5158';

// ── 輔助函式 ────────────────────────────────────────────────────────────────

/** 確保兩位使用者互為好友（idempotent，已是好友時忽略錯誤） */
async function ensureFriendship(userA: string, passA: string, userB: string) {
  const loginRes = await fetch(`${BASE}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username: userA, password: passA }),
  });
  const { token } = await loginRes.json() as { token: string };
  await fetch(`${BASE}/api/friends/add`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify({ targetUsername: userB }),
  });
}

async function loginAndConnect(page: Page, username: string, password: string) {
  await page.goto('/');
  await page.fill('#username-input', username);
  await page.fill('#password-input', password);
  await page.click('#login-btn');

  // 等待 JS 的 longLivedToken 被設定（login-card 被隱藏）
  await page.waitForFunction(
    () => (document.getElementById('login-card') as HTMLElement)?.style.display === 'none',
    { timeout: 10_000 },
  );

  // 點擊建立連線
  const connectBtn = page.locator('#connect-btn');
  await connectBtn.waitFor({ state: 'visible', timeout: 5_000 });
  await connectBtn.click();

  // 等待 SignalR 連線成功
  await expect(page.locator('#status-text')).toHaveText('已連線', { timeout: 20_000 });
}

/** 等待好友的通話按鈕變為可用（好友必須在線） */
async function waitForFriendOnline(page: Page, friendUsername: string) {
  await page.waitForFunction(
    (name: string) => {
      const item = document.getElementById(`item-${name.toLowerCase()}`);
      if (!item) return false;
      const btns = item.querySelectorAll<HTMLButtonElement>('.call-icon-btn');
      return btns.length >= 2 && !btns[0].disabled;
    },
    friendUsername,
    { timeout: 15_000 },
  );
}

/** 輪詢等待 JS 條件成立 */
async function waitForJs(page: Page, expression: string, timeoutMs = 15_000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const ok = await page.evaluate(expression).catch(() => false);
    if (ok) return;
    await page.waitForTimeout(300);
  }
  throw new Error(`Timeout: ${expression}`);
}

async function getPeerState(page: Page): Promise<string> {
  return page.evaluate(() => (window as any).peerConnection?.connectionState ?? 'null');
}

// ── 測試套件 ────────────────────────────────────────────────────────────────

test.describe('WebRTC 1-on-1 通話', () => {
  let browser: Browser;
  let caller: Page;  // Charlie — 發起通話
  let callee: Page;  // Bob    — 接聽通話

  test.beforeAll(async () => {
    // 確保 Charlie 和 Bob 互為好友
    await ensureFriendship('Charlie', 'charlie123', 'Bob');
    await ensureFriendship('Bob', 'bob123', 'Charlie');

    browser = await chromium.launch({
      args: [
        '--use-fake-ui-for-media-stream',
        '--use-fake-device-for-media-stream',
        '--no-sandbox',
        '--disable-dev-shm-usage',
        '--allow-file-access-from-files',
        '--allow-loopback-in-peer-connection',
        '--disable-web-security',
      ],
    });

    const callerCtx: BrowserContext = await browser.newContext({ permissions: ['camera', 'microphone'] });
    const calleeCtx: BrowserContext = await browser.newContext({ permissions: ['camera', 'microphone'] });

    caller = await callerCtx.newPage();
    callee = await calleeCtx.newPage();

    // 平行登入並連線
    await Promise.all([
      loginAndConnect(caller, 'Charlie', 'charlie123'),
      loginAndConnect(callee, 'Bob', 'bob123'),
    ]);

    // 等待雙方看到對方在線（friends list 更新）
    await Promise.all([
      waitForFriendOnline(caller, 'Bob'),
      waitForFriendOnline(callee, 'Charlie'),
    ]);
  });

  test.afterAll(async () => {
    await browser.close();
  });

  // ── 測試 1：語音通話 Offer/Answer 流程 ────────────────────────

  test('語音通話：發起方出現撥號覆蓋層，接收方出現來電 Modal', async () => {
    const audioBtn = caller
      .locator('.user-item', { hasText: 'Bob' })
      .locator('.call-icon-btn', { hasText: '📞' });
    await audioBtn.click();

    await expect(caller.locator('#call-overlay')).toBeVisible({ timeout: 8_000 });
    await expect(caller.locator('#call-status-label')).toContainText('bob', { ignoreCase: true });

    await expect(callee.locator('#incoming-call-modal')).toBeVisible({ timeout: 8_000 });
    await expect(callee.locator('#incoming-caller-name')).toContainText('charlie', { ignoreCase: true });
    await expect(callee.locator('#incoming-call-type')).toHaveText('語音通話');
  });

  // ── 測試 2：接聽後 P2P 連線建立 ───────────────────────────────

  test('語音通話：接聽後 WebRTC P2P 連線建立（connectionState=connected）', async () => {
    await callee.click('.btn-accept');
    await expect(callee.locator('#call-overlay')).toBeVisible({ timeout: 8_000 });

    await waitForJs(caller, `window.peerConnection?.connectionState === 'connected'`, 25_000);
    await waitForJs(callee, `window.peerConnection?.connectionState === 'connected'`, 25_000);

    expect(await getPeerState(caller)).toBe('connected');
    expect(await getPeerState(callee)).toBe('connected');
  });

  // ── 測試 3：遠端媒體串流抵達 ─────────────────────────────────

  test('語音通話：遠端媒體串流抵達（remoteVideo.srcObject 非 null）', async () => {
    const remoteActive = await callee.evaluate(
      () => (document.getElementById('remote-video') as HTMLVideoElement)?.srcObject != null,
    );
    expect(remoteActive).toBe(true);
  });

  // ── 測試 4：靜音切換 ──────────────────────────────────────────

  test('靜音：點靜音後音訊 enabled=false，再點恢復 enabled=true', async () => {
    await caller.click('#mute-btn');
    const afterMute = await caller.evaluate(
      () => (window as any).localStream?.getAudioTracks()[0]?.enabled,
    );
    expect(afterMute).toBe(false);

    await caller.click('#mute-btn');
    const afterUnmute = await caller.evaluate(
      () => (window as any).localStream?.getAudioTracks()[0]?.enabled,
    );
    expect(afterUnmute).toBe(true);
  });

  // ── 測試 5：掛斷 ─────────────────────────────────────────────

  test('掛斷：雙方覆蓋層關閉，peerConnection 清除為 null', async () => {
    await caller.click('.ctrl-btn.hangup');

    await expect(caller.locator('#call-overlay')).toBeHidden({ timeout: 6_000 });
    await expect(callee.locator('#call-overlay')).toBeHidden({ timeout: 6_000 });

    expect(await getPeerState(caller)).toBe('null');
    expect(await getPeerState(callee)).toBe('null');
  });

  // ── 測試 6：視訊通話 callType 傳遞 ───────────────────────────

  test('視訊通話：來電 Modal 顯示「視訊通話」', async () => {
    const videoBtn = caller
      .locator('.user-item', { hasText: 'Bob' })
      .locator('.call-icon-btn', { hasText: '📹' });
    await videoBtn.click();

    await expect(callee.locator('#incoming-call-modal')).toBeVisible({ timeout: 8_000 });
    await expect(callee.locator('#incoming-call-type')).toHaveText('視訊通話');

    // 拒絕清場
    await callee.click('.btn-reject');
    await expect(callee.locator('#incoming-call-modal')).toBeHidden({ timeout: 5_000 });
    await expect(caller.locator('#call-overlay')).toBeHidden({ timeout: 5_000 });
  });

  // ── 測試 7：拒絕來電 ─────────────────────────────────────────

  test('拒絕來電：發起方通話覆蓋層自動關閉', async () => {
    const audioBtn = caller
      .locator('.user-item', { hasText: 'Bob' })
      .locator('.call-icon-btn', { hasText: '📞' });
    await audioBtn.click();
    await expect(callee.locator('#incoming-call-modal')).toBeVisible({ timeout: 8_000 });

    await callee.click('.btn-reject');

    await expect(caller.locator('#call-overlay')).toBeHidden({ timeout: 6_000 });
    await expect(callee.locator('#incoming-call-modal')).toBeHidden({ timeout: 3_000 });
  });

  // ── 測試 8：視訊通話鏡頭切換 ─────────────────────────────────

  test('視訊通話：關閉鏡頭後視訊軌道 enabled=false', async () => {
    const videoBtn = caller
      .locator('.user-item', { hasText: 'Bob' })
      .locator('.call-icon-btn', { hasText: '📹' });
    await videoBtn.click();
    await expect(callee.locator('#incoming-call-modal')).toBeVisible({ timeout: 8_000 });
    await callee.click('.btn-accept');
    await waitForJs(caller, `window.peerConnection?.connectionState === 'connected'`, 25_000);

    // 關閉鏡頭
    await caller.click('#camera-btn');
    const camEnabled = await caller.evaluate(
      () => (window as any).localStream?.getVideoTracks()[0]?.enabled,
    );
    expect(camEnabled).toBe(false);

    // 掛斷清場
    await caller.click('.ctrl-btn.hangup');
    await expect(caller.locator('#call-overlay')).toBeHidden({ timeout: 6_000 });
    await expect(callee.locator('#call-overlay')).toBeHidden({ timeout: 6_000 });
  });
});
