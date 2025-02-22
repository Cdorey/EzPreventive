namespace EzNutrition.Server.Services.Settings
{
    public class JwtSettings
    {
        public string PublicKey { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
    }
}