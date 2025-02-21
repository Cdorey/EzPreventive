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
        public DateTimeOffset? ExpiresAt
        {
            get
            {
                var expClaim = Claims.FirstOrDefault(c => c.Type == "exp")?.Value;
                if (expClaim == null)
                {
                    return null;
                }

                // 将 Unix 时间戳字符串转换为 long
                if (!long.TryParse(expClaim, out var expSeconds))
                {
                    return null;
                }

                // Unix 时间戳通常以秒为单位，转换为 DateTimeOffset
                return DateTimeOffset.FromUnixTimeSeconds(expSeconds);
            }
        }

        public IEnumerable<Claim> Claims => ParseToken();

        private IEnumerable<Claim> ParseToken()
        {
            if (!string.IsNullOrEmpty(Token))
            {
                // 使用令牌解析用户信息
                var tokenHandler = new JwtSecurityTokenHandler();
                JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap.Clear();
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
            UserName = Claims.FirstOrDefault(x => x.Type == "unique_name")?.Value ?? string.Empty;
            Roles = Claims.Where(x => x.Type == "role")?.Select(x => x.Value)?.ToArray() ?? [];
            Email = Claims.FirstOrDefault(x => x.Type == "email")?.Value ?? string.Empty;
        }
    }
}
