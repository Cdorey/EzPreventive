namespace EzNutrition.Server.Services.Settings
{
    public class JwtSettings
    {
        [System.ComponentModel.DataAnnotations.Required]
        public string PublicKey { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required]
        public string PrivateKey { get; set; } = string.Empty;
    }
}
