namespace EzNutrition.Domain.Calculations
{
    /// <summary>
    /// 提供基于理想体重、基础能量系数和 PAL 的能量计算。
    /// </summary>
    public static class IdealBodyWeightEnergyFormula
    {
        /// <summary>
        /// 计算每日能量值。
        /// </summary>
        /// <param name="height">身高，单位为厘米。</param>
        /// <param name="bee">每千克理想体重的基础能量系数。</param>
        /// <param name="pal">身体活动水平。</param>
        /// <returns>取整后的每日能量值。</returns>
        public static int Calculate(decimal height, decimal bee, decimal pal)
        {
            var bw = height - 105;
            return (int)Math.Round(bee * bw * pal);
        }
    }
}
