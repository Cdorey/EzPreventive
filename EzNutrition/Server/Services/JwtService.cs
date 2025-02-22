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
    public class JwtService(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, IOptions<JwtSettings> options)
    {
        public async Task<string> GenerateJwtToken(string userName)
        {
            return await GenerateJwtToken(await userManager.FindByNameAsync(userName));
        }

        public async Task<string> GenerateJwtToken(IdentityUser user)
        {
            var privateKeyBytes = Convert.FromBase64String(options.Value.PrivateKey);
            var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
            var privateKey = new RsaSecurityKey(rsa);

            var tokenHandler = new JwtSecurityTokenHandler();
            var claimsList = new List<Claim>();

            // 1. 加入用户已有 Claims
            claimsList.AddRange(await userManager.GetClaimsAsync(user));

            // 2. 加入 Upn、Name
            claimsList.Add(new Claim(ClaimTypes.Upn, user.Id));
            claimsList.Add(new Claim(ClaimTypes.Name, user.UserName!));

            // 3. 加入角色及其 Claims
            var roleNames = await userManager.GetRolesAsync(user);
            foreach (var roleName in roleNames)
            {
                // 3.1 加入角色声明
                claimsList.Add(new Claim(ClaimTypes.Role, roleName));

                // 3.2 如果你的角色本身在 RoleClaims 表中有额外声明，也加进来
                var roleEntity = await roleManager.FindByNameAsync(roleName);
                var extraRoleClaims = await roleManager.GetClaimsAsync(roleEntity!);
                claimsList.AddRange(extraRoleClaims);
            }

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
    }
}