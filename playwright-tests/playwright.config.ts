import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  timeout: 60_000,
  retries: 0,
  workers: 1,
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'test-results/html' }]],
  use: {
    baseURL: 'http://localhost:5158',
    launchOptions: {
      args: [
        '--use-fake-ui-for-media-stream',
        '--use-fake-device-for-media-stream',
        '--no-sandbox',
        '--disable-dev-shm-usage',
        '--allow-file-access-from-files',
      ],
    },
    permissions: ['camera', 'microphone'],
    video: 'off',
    screenshot: 'only-on-failure',
    trace: 'on-first-retry',
  },
  webServer: {
    command: 'ASPNETCORE_ENVIRONMENT=Development dotnet run --project ../ChatRoom/ChatRoom.csproj --launch-profile http',
    url: 'http://localhost:5158',
    reuseExistingServer: true,
    timeout: 30_000,
  },
});
