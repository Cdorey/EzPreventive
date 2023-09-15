using EzNutrition.Server.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace EzNutrition.Server.Services
{
    public class JwtService
    {
        private readonly string _privateKey;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;

        public JwtService(ApplicationDbContext dbContext, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, IConfiguration configuration)
        {
            _context = dbContext;
            _userManager = userManager;
            _roleManager = roleManager;
            _configuration = configuration;
            _privateKey = configuration.GetSection("PrivateKey").Value;
        }

        public async Task<string> GenerateJwtToken(string userName)
        {
            return await GenerateJwtToken(await _userManager.FindByNameAsync(userName));
        }

        public async Task<string> GenerateJwtToken(IdentityUser user)
        {
            var privateKeyBytes = Convert.FromBase64String(_privateKey);
            var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
            var privateKey = new RsaSecurityKey(rsa);

            var tokenHandler = new JwtSecurityTokenHandler();

            var userClaims = await _userManager.GetClaimsAsync(user);

            var roles = from roleName in await _userManager.GetRolesAsync(user) select new Claim(ClaimTypes.Role, roleName);

            var claims = userClaims.Union(roles).Append(new Claim(ClaimTypes.Upn, user.Id)).Append(new Claim(ClaimTypes.Name, user.UserName));

            if (roles.Any())
            {
                foreach (var role in roles)
                {
                    claims = claims.Union(await _roleManager.GetClaimsAsync(await _roleManager.FindByNameAsync(role.Value)));
                }
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(privateKey, SecurityAlgorithms.RsaSha256Signature),
                Audience = "EzNutrition",
                Issuer = "EzPreventive"
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

    }
}
