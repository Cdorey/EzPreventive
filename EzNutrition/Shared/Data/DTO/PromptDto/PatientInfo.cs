namespace EzNutrition.Shared.Data.DTO.PromptDto
{
    /// <summary>
    /// Carries patient context disclosed for AI advice. Nullable properties are optional
    /// and may be omitted by the compact transport representation.
    /// </summary>
    public class PatientInfo
    {
        public string? Gender { get; set; }

        /// <summary>
        /// 获取供建议生成使用的结构化实足年龄。
        /// </summary>
        public required PatientAge Age { get; set; }

        public decimal? BMI { get; set; }

        public decimal? PAL { get; set; }

        public decimal? Height { get; set; }

        public decimal? Weight { get; set; }

        public int? TotalBalanceEnergyViaCalculation { get; set; }

        public string? SpecialPhysiologicalPeriod { get; set; }
    }
}
