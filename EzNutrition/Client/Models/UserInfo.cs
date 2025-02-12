using EzNutrition.Shared.Identities;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EzNutrition.Client.Models
{
    public class UserInfo : RemoteUserAccount, IUserInfo
    {
        public string Token { get; }

        public string UserName { get; }

        public string[] Roles { get; }

        public string Email { get; }
        public DateTimeOffset? ExpiresAt { get; internal set; }

        internal IEnumerable<Claim> ParseToken()
        {
            if (!string.IsNullOrEmpty(Token))
            {
                // 使用令牌解析用户信息
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(Token);
                return jwtToken.Claims;
            }
            else
            {
                throw new InvalidOperationException("Token is null or empty.");
            }
        }


        public UserInfo(string token)
        {
            Token = token;
            var claims = ParseToken();
            UserName = claims.FirstOrDefault(x => x.Type == "unique_name")?.Value ?? string.Empty;
            Roles = claims.Where(x => x.Type == "role")?.Select(x => x.Value)?.ToArray() ?? Array.Empty<string>();
            Email = claims.FirstOrDefault(x => x.Type == "email")?.Value ?? string.Empty;
        }
    }
}
