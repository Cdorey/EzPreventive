namespace EzNutrition.Server.Services.Settings
{
    public class EmailSettings
    {
        public string SmtpServer { get; set; } = "smtp.example.com";
        public int SmtpPort { get; set; } = 587;
        public string SenderEmail { get; set; } = "noreply@example.com";
        public string SenderName { get; set; } = "YourAppName";
        public string UserName { get; set; } = "smtp_username";
        public string Password { get; set; } = "smtp_password";
    }
}