namespace EzNutrition.Server.Services.Settings
{
    public class EmailSettings
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.Url]
        public string ClientUrl { get; set; } = "https://localhost:5001";

        [System.ComponentModel.DataAnnotations.Required]
        public string SmtpServer { get; set; } = "smtp.example.com";

        [System.ComponentModel.DataAnnotations.Range(1, 65535)]
        public int SmtpPort { get; set; } = 999;

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.EmailAddress]
        public string SenderEmail { get; set; } = "noreply@example.com";

        [System.ComponentModel.DataAnnotations.Required]
        public string SenderName { get; set; } = "YourAppName";

        [System.ComponentModel.DataAnnotations.Required]
        public string UserName { get; set; } = "smtp_username";

        [System.ComponentModel.DataAnnotations.Required]
        public string Password { get; set; } = "smtp_password";
    }
}
