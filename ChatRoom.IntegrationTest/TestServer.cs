using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace ChatRoom.IntegrationTest;

public class TestServer : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public TestServer()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // 移除原本的 SQLite 檔案設定
            var descriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<ChatDbContext>))
                .ToList();
            foreach (var d in descriptors)
                services.Remove(d);

            // 替換為 In-Memory SQLite（保持連線開啟，確保資料表不被清除）
            services.AddDbContext<ChatDbContext>(options =>
                options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _connection.Dispose();
        base.Dispose(disposing);
    }
}
