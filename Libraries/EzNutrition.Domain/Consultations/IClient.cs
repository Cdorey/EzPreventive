namespace EzNutrition.Domain.Consultations
{
    public interface IClient
    {
        string? Name { get; set; }
        string? Gender { get; set; }
        /// <summary>获取或设置咨询时采用的实足年龄。</summary>
        ChronologicalAge? Age { get; set; }

        /// <summary>获取或设置可选完整出生日期。</summary>
        DateOnly? BirthDate { get; set; }
        decimal? Height { get; set; }
        decimal? Weight { get; set; }
        string SpecialPhysiologicalPeriod { get; set; }
    }
}
