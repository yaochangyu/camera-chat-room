using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatRoom.Controllers;

[ApiController]
[Route("api/turn")]
[Authorize]
public class TurnController(IConfiguration configuration, IHttpClientFactory httpClientFactory) : ControllerBase
{
    [HttpGet("credentials")]
    public async Task<IActionResult> GetCredentials()
    {
        var apiKey = configuration["Turn:ApiKey"];
        var host = configuration["Turn:Host"];

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(host))
            return StatusCode(503, new { message = "TURN 服務尚未設定" });

        var client = httpClientFactory.CreateClient();
        var url = $"https://{host}/api/v1/turn/credentials?apiKey={apiKey}";

        try
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return Content(json, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { message = $"無法取得 TURN 憑證: {ex.Message}" });
        }
    }
}
