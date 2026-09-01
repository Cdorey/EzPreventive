using EzNutrition.Server.Data;
using EzNutrition.Server.Services.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace EzNutrition.Server.Services
{
    public class JwtService(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<JwtSettings> options,
        ILogger<JwtService> logger)
    {
        internal const string SecurityStampClaimType = "EzNutrition.SecurityStamp";

        //public async Task<string> GenerateJwtToken(string userName)
        //{
        //    return await GenerateJwtToken(await userManager.FindByNameAsync(userName));
        //}

        public async Task<string> GenerateJwtToken(IdentityUser user)
        {
            ArgumentNullException.ThrowIfNull(user);
            if (string.IsNullOrWhiteSpace(user.UserName))
            {
                throw new InvalidOperationException("A JWT cannot be generated for a user without a username.");
            }

            using var rsa = RSA.Create();
            try
            {
                var privateKeyBytes = Convert.FromBase64String(options.Value.PrivateKey);
                rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
            }
            catch (Exception ex) when (ex is FormatException or CryptographicException)
            {
                throw new InvalidOperationException(
                    "JwtSettings:PrivateKey is not a valid Base64-encoded PKCS#8 RSA private key.",
                    ex);
            }

            var privateKey = new RsaSecurityKey(rsa)
            {
                // The RSA instance is scoped to this method. Prevent the shared crypto factory from
                // caching a signature provider that would retain the disposed RSA instance.
                CryptoProviderFactory = new CryptoProviderFactory
                {
                    CacheSignatureProviders = false
                }
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var claimsList = new List<Claim>();

            // 1. 加入用户已有 Claims
            claimsList.AddRange((await userManager.GetClaimsAsync(user))
                .Where(claim => !IsReservedClaimType(claim.Type)));

            // 2. 加入稳定用户标识、Name 和安全戳指纹
            claimsList.Add(new Claim(ClaimTypes.Upn, user.Id));
            claimsList.Add(new Claim(ClaimTypes.NameIdentifier, user.Id));
            claimsList.Add(new Claim(ClaimTypes.Name, user.UserName));
            var securityStamp = await userManager.GetSecurityStampAsync(user);
            claimsList.Add(new Claim(
                SecurityStampClaimType,
                CreateSecurityStampFingerprint(securityStamp)));

            // 3. 加入角色及其 Claims
            var roleNames = await userManager.GetRolesAsync(user);
            foreach (var roleName in roleNames)
            {
                // 3.1 加入角色声明
                claimsList.Add(new Claim(ClaimTypes.Role, roleName));

                // 3.2 如果你的角色本身在 RoleClaims 表中有额外声明，也加进来
                var roleEntity = await roleManager.FindByNameAsync(roleName);
                if (roleEntity is null)
                {
                    logger.LogWarning(
                        "User {UserId} references role {RoleName}, but the role no longer exists.",
                        user.Id,
                        roleName);
                    continue;
                }

                var extraRoleClaims = await roleManager.GetClaimsAsync(roleEntity);
                claimsList.AddRange(extraRoleClaims.Where(claim => !IsReservedClaimType(claim.Type)));
            }

            claimsList = claimsList
                .DistinctBy(claim => (claim.Type, claim.Value))
                .ToList();

            // 4. 构造 tokenDescriptor
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claimsList),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(privateKey, SecurityAlgorithms.RsaSha256Signature),
                Audience = "EzNutrition",
                Issuer = "EzPreventive",
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        internal static string CreateSecurityStampFingerprint(string securityStamp)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(securityStamp);
            return Base64UrlEncoder.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(securityStamp)));
        }

        internal static bool IsSecurityStampFingerprintValid(
            string fingerprint,
            string securityStamp)
        {
            try
            {
                var actual = Base64UrlEncoder.DecodeBytes(fingerprint);
                var expected = SHA256.HashData(Encoding.UTF8.GetBytes(securityStamp));
                return actual.Length == expected.Length &&
                    CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                return false;
            }
        }

        internal static bool IsReservedClaimType(string claimType) =>
            claimType == SecurityStampClaimType ||
            claimType == ClaimTypes.NameIdentifier ||
            claimType == ClaimTypes.Upn ||
            claimType == ClaimTypes.Name ||
            claimType == ClaimTypes.Role ||
            claimType == JwtRegisteredClaimNames.Sub ||
            claimType == JwtRegisteredClaimNames.UniqueName ||
            claimType == JwtRegisteredClaimNames.Jti ||
            claimType == JwtRegisteredClaimNames.Iat ||
            claimType == JwtRegisteredClaimNames.Exp ||
            claimType == JwtRegisteredClaimNames.Nbf ||
            claimType == JwtRegisteredClaimNames.Aud ||
            claimType == JwtRegisteredClaimNames.Iss ||
            claimType is "name" or "nameid" or "role" or "roles" or "upn";
    }
}
