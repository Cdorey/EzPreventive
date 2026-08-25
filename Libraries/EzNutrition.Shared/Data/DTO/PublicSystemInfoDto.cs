namespace EzNutrition.Shared.Data.DTO;

/// <summary>
/// 表示无需身份认证即可读取的服务端部署信息。
/// </summary>
/// <param name="CaseNumber">可选的站点备案编号。</param>
/// <param name="ServerVersion">可选的当前服务端产品发行版本。</param>
public sealed record PublicSystemInfoDto(string? CaseNumber, string? ServerVersion);
