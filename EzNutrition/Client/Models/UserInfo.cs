using EzNutrition.Shared.Identities;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EzNutrition.Client.Models
{
    public sealed class UserInfo : RemoteUserAccount, IUserInfo
    {
        private readonly IReadOnlyList<Claim> claims;

        public string Token { get; }

        public string UserName { get; }

        public string[] Roles { get; }

        public string Email { get; }

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

            UserName = FindClaimValue(JwtRegisteredClaimNames.UniqueName, ClaimTypes.Name);
            Roles = claims
                .Where(claim => claim.Type is "role" || claim.Type == ClaimTypes.Role)
                .Select(claim => claim.Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            Email = FindClaimValue(JwtRegisteredClaimNames.Email, ClaimTypes.Email);
            ExpiresAt = jwtToken.ValidTo == DateTime.MinValue
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(jwtToken.ValidTo, DateTimeKind.Utc));

            if (string.IsNullOrWhiteSpace(UserName) || ExpiresAt is null)
            {
                throw new ArgumentException("Token is missing required identity claims.", nameof(token));
            }
        }

        private string FindClaimValue(params string[] claimTypes) =>
            claims.FirstOrDefault(claim => claimTypes.Contains(claim.Type, StringComparer.Ordinal))?.Value
            ?? string.Empty;
    }
}
