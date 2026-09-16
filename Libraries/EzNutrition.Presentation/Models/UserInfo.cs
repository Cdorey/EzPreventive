using EzNutrition.Shared.Identities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EzNutrition.Presentation.Models
{
    /// <summary>
    /// 表示由 EzNutrition 访问令牌解析出的当前交互式客户端用户。
    /// </summary>
    public sealed class UserInfo : IUserInfo
    {
        private readonly IReadOnlyList<Claim> claims;

        public string Token { get; }

        /// <summary>获取访问令牌所属的稳定登录会话标识。</summary>
        public Guid SessionId { get; }

        public string UserId { get; }

        public string UserName { get; }

        public string[] Roles { get; }

        public string Email { get; }

        public string? RealName { get; }

        public string? InstitutionName { get; }

        public DateTimeOffset? ExpiresAt { get; }

        public bool IsExpired => ExpiresAt is null || ExpiresAt <= DateTimeOffset.UtcNow;

        public IEnumerable<Claim> Claims => claims;

        public UserInfo(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException("Token cannot be empty.", nameof(token));
            }

            Token = token.Trim();
            var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(Token);
            claims = jwtToken.Claims.ToArray();
            SessionId = Guid.TryParse(FindClaimValue("sid"), out var sessionId) ? sessionId : Guid.Empty;

            UserId = FindClaimValue(
                JwtRegisteredClaimNames.Sub,
                "nameid",
                "upn",
                ClaimTypes.NameIdentifier,
                ClaimTypes.Upn);
            UserName = FindClaimValue(JwtRegisteredClaimNames.UniqueName, ClaimTypes.Name);
            Roles = claims
                .Where(claim => claim.Type is "role" || claim.Type == ClaimTypes.Role)
                .Select(claim => claim.Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            Email = FindClaimValue(JwtRegisteredClaimNames.Email, ClaimTypes.Email);
            RealName = FindOptionalClaimValue(UserClaimTypes.RealName);
            InstitutionName = FindOptionalClaimValue(UserClaimTypes.InstitutionName);
            ExpiresAt = jwtToken.ValidTo == DateTime.MinValue
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(jwtToken.ValidTo, DateTimeKind.Utc));

            if (string.IsNullOrWhiteSpace(UserId)
                || string.IsNullOrWhiteSpace(UserName)
                || ExpiresAt is null)
            {
                throw new ArgumentException("Token is missing required identity claims.", nameof(token));
            }
        }

        private string FindClaimValue(params string[] claimTypes) =>
            claims.FirstOrDefault(claim => claimTypes.Contains(claim.Type, StringComparer.Ordinal))?.Value
            ?? string.Empty;

        private string? FindOptionalClaimValue(params string[] claimTypes)
        {
            var value = claims.FirstOrDefault(claim =>
                claimTypes.Contains(claim.Type, StringComparer.Ordinal))?.Value;
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
