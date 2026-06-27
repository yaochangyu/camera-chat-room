using System.Text;
using System.Threading.RateLimiting;
using ChatRoom.Data;
using ChatRoom.Hubs;
using ChatRoom.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// 註冊 OpenAPI 支援
builder.Services.AddOpenApi();

// 宣告 JWT 金鑰與設定參數 (與 JwtTokenService 保持一致)
var jwtSecret = "ThisIsAVerySecretKeyForChatRoomProject2026!!!";
var jwtIssuer = "ChatRoomServer";
var apiAudience = "ChatRoomClients";
var hubAudience = "ChatRoomSignalRHub";

// 註冊 JWT 驗證服務
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudiences = new[] { apiAudience, hubAudience },
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero // 強制 30 秒短效 Token 精準過期
        };

        // 針對 WebSockets 握手請求，從 Query String 抓取 access_token
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chatHub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// 註冊日誌服務
builder.Services.AddLogging();

// 註冊限流服務 (Rate Limiting)，以防臨時 Token API 遭受 DDoS 攻擊
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("LoginPolicy", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromSeconds(60);
        opt.QueueLimit = 0;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    options.AddFixedWindowLimiter("HubTokenPolicy", opt =>
    {
        opt.PermitLimit = 10;                     // 限制窗口期內最多 10 次
        opt.Window = TimeSpan.FromSeconds(10);   // 每 10 秒一個窗口
        opt.QueueLimit = 5;                      // 排隊上限
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:3000", "http://localhost:5173", "https://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// 註冊 EF Core SQLite 資料庫
builder.Services.AddDbContext<ChatDbContext>(options =>
{
    var dataDir = Path.Combine(builder.Environment.ContentRootPath, "data");
    Directory.CreateDirectory(dataDir);
    var dbPath = Path.Combine(dataDir, "chatroom.db");
    options.UseSqlite($"Data Source={dbPath}");
});

// 註冊 .NET 10 的 HybridCache 快取服務
#pragma warning disable EXTEXP0018 // 忽略實驗性功能警告
builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(2),       // 全域快取生命週期為 2 分鐘
        LocalCacheExpiration = TimeSpan.FromSeconds(30) // 本地 L1 快取為 30 秒
    };
});
#pragma warning restore EXTEXP0018

// 註冊記憶體快取（PKCE auth_code 暫存用）
builder.Services.AddMemoryCache();

// 註冊自訂 Token 服務
builder.Services.AddSingleton<JwtTokenService>();

// 必須在 AddSignalR() 之前註冊，才能覆寫預設 ClaimTypes.NameIdentifier 行為
builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, ChatRoom.Services.NameUserIdProvider>();

// 註冊 MVC Controllers
builder.Services.AddControllers();

// 註冊 SignalR 服務
builder.Services.AddSignalR();

var app = builder.Build();

// EnsureCreated 只在 DB 不存在時建立 Schema，不會替已存在的 DB 補欄位；Schema 異動請刪 DB 重啟
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    dbContext.Database.EnsureCreated();

    // 如果沒有使用者，塞入預設測試使用者 (Bob, Charlie, David)
    if (!dbContext.Users.Any())
    {
        var testUsers = new List<ChatRoom.Models.User>
    {
        new() { Username = "Bob", Email = "bob@example.com", Bio = "我是 Bob，熱愛 .NET 10 開發。", StatusMessage = "⚡ 寫 Code 中...", PasswordHash = ChatRoom.Services.PasswordHasher.Hash("bob123"), CreatedAt = DateTime.UtcNow },
        new() { Username = "Charlie", Email = "charlie@example.com", Bio = "我是 Charlie，喜歡手沖咖啡與前端設計。", StatusMessage = "☕ 悠閒下午茶", PasswordHash = ChatRoom.Services.PasswordHasher.Hash("charlie123"), CreatedAt = DateTime.UtcNow },
        new() { Username = "David", Email = "david@example.com", Bio = "我是 David，DevOps / SRE 工程師。", StatusMessage = "🚀 伺服器部署中", PasswordHash = ChatRoom.Services.PasswordHasher.Hash("david123"), CreatedAt = DateTime.UtcNow }
    };
        dbContext.Users.AddRange(testUsers);
        dbContext.SaveChanges();
    }
}

// 配置 HTTP 請求管道
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 本地測試關閉 HTTPS 重導向，避免 http://localhost:5158 無法連線
// app.UseHttpsRedirection();

// CSP Header：防止 XSS 竊取記憶體中的 Token
app.Use(async (context, next) =>
{
    context.Response.Headers.Append(
        "Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net https://fonts.googleapis.com; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://fonts.gstatic.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "connect-src 'self' ws: wss:; " +
        "img-src 'self' data:; " +
        "frame-ancestors 'none'");
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();

// 中介軟體順序固定：Routing → CORS → RateLimiter → Authentication → Authorization
app.UseRouting();
app.UseCors("CorsPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// AllowStatefulReconnects：短暫斷線重連時保留 Hub 群組成員資格，不需重新 JoinRoom
app.MapHub<ChatHub>("/chatHub", options =>
{
    options.AllowStatefulReconnects = true;
});

app.MapControllers();

app.Run();

public partial class Program { }
