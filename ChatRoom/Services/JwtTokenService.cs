using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ChatRoom.Services
{
    public class JwtTokenService
    {
        private readonly string _secret = "ThisIsAVerySecretKeyForChatRoomProject2026!!!";
        private readonly string _issuer = "ChatRoomServer";
        private readonly string _apiAudience = "ChatRoomClients";
        private readonly string _hubAudience = "ChatRoomSignalRHub";

        // 產生長效 Token (API 呼叫用，壽命 1 小時)
        public string GenerateApiToken(string username)
        {
            return GenerateToken(username, _apiAudience, TimeSpan.FromHours(1));
        }

        // 產生短效 Token (SignalR Hub 連線用，壽命 30 秒)
        public string GenerateHubToken(string username)
        {
            return GenerateToken(username, _hubAudience, TimeSpan.FromSeconds(30));
        }

        public string? ValidateApiToken(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_secret);
            try
            {
                var principal = handler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = _issuer,
                    ValidAudience = _apiAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ClockSkew = TimeSpan.Zero
                }, out _);
                return UsernameNormalizer.NormalizeNullable(principal.Identity?.Name);
            }
            catch
            {
                return null;
            }
        }

        private string GenerateToken(string username, string audience, TimeSpan expiry)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_secret);
            
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, username),
                    new Claim(JwtRegisteredClaimNames.Sub, username),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                }),
                Expires = DateTime.UtcNow.Add(expiry),
                Issuer = _issuer,
                Audience = audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
