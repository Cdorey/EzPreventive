using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace EzNutrition.Server.Services
{
    public class JwtService
    {
        private readonly string _privateKey;

        public JwtService(string privateKey)
        {
            _privateKey = privateKey;
        }

#warning 方法尚未完成
        public string GenerateJwtToken(string username)
        {
            var privateKeyBytes = Convert.FromBase64String(_privateKey);
            var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
            var privateKey = new RsaSecurityKey(rsa);

            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, username),
                    // Add more claims as needed
                }),
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
