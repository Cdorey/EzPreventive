namespace EzNutrition.Server.Services.Settings;

public sealed class AuthBootstrapSettings
{
    public const string SectionName = "AuthBootstrap";

    public string? AdminPassword { get; set; }
}
