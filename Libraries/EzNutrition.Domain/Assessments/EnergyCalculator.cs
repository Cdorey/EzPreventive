using EzNutrition.Shared.Data.Entities;
using EzNutrition.Domain.Calculations;
using EzNutrition.Domain.Consultations;
using System.Globalization;
using System.Text;

namespace EzNutrition.Domain.Assessments
{
    /// <summary>
    /// 指定当前自动能量结果采用的计算路径。
    /// </summary>
    public enum EnergyCalculationMethod
    {
        /// <summary>
        /// 使用身高推导理想体重，再结合 BEE 与 PAL 计算。
        /// </summary>
        IdealBodyWeightBeePal = 0,

        /// <summary>
        /// 使用参考人群平均体重对应的 EER。
        /// </summary>
        PopulationAverage = 1
    }

    public class EnergyCalculator(IClient client) : ITreatment, ISoapContributor
    {
        public IClient Client => client;

        public string[] Requirements { get; } =
        [
            nameof(IClient.Gender),
            nameof(IClient.Height),
            nameof(IClient.Weight),
            nameof(IClient.Age),
            nameof(IClient.SpecialPhysiologicalPeriod)
        ];

        public List<EER> AvailableEERs { get; set; } = [];

        /// <summary>
        /// 获取最近一次自动计算得到的每日能量。
        /// </summary>
        public int? CalculatedEnergy { get; private set; }

        /// <summary>
        /// 获取最近一次自动计算采用的路径。
        /// </summary>
        public EnergyCalculationMethod? CalculationMethod { get; private set; }

        /// <summary>
        /// 获取自动计算采用的主要 EER 记录。
        /// </summary>
        public EER? SelectedEer { get; private set; }

        /// <summary>
        /// 获取自动计算中叠加的特殊生理状态能量偏移。
        /// </summary>
        public int AppliedOffsetEnergy { get; private set; }

        /// <summary>
        /// 获取当前采用能量是否经过专业人员手工核定。
        /// </summary>
        public bool IsEnergyManuallyAdjusted { get; private set; }

        public decimal? PAL { get; set; }

        public decimal? BMI { get; private set; }

        public string Summary { get; private set; } = string.Empty;

        public int? Energy { get; private set; }

        public MacronutrientAllocation? Allocation { get; private set; }

        public FoodExchangeAllocation? FoodExchangeAllocation { get; private set; }

        public bool Calculate()
        {
            if (PAL is null)
            {
                return false;
            }

            var energy = 0;
            EER? selectedEer = null;
            EnergyCalculationMethod? calculationMethod = null;
            var strBuild = new StringBuilder();
            strBuild.Append($"咨询对象的PAL：{PAL}，");

            if (Client.Height is not null)
            {
                var eerWithBEE = AvailableEERs.FirstOrDefault(x => x.BEE is not null);
                if (eerWithBEE?.BEE is not null)
                {
                    energy = IdealBodyWeightEnergyFormula.Calculate(Client.Height.Value, eerWithBEE.BEE.Value, PAL.Value);

                    if (energy != 0)
                    {
                        selectedEer = eerWithBEE;
                        calculationMethod = EnergyCalculationMethod.IdealBodyWeightBeePal;
                    }

                    var height = Client.Height.Value / 100;

                    if (height != 0 && Client.Weight != null && Client.Weight != 0)
                    {
                        BMI = Math.Round((Client.Weight.Value / height / height), 2);
                        strBuild.Append($"BMI：{BMI}；");
                    }
                }
            }

            string dependency;
            if (energy != 0)
            {
                dependency = "BW*BEE*PAL";
            }
            else
            {
                var eerWithPAL = AvailableEERs.FirstOrDefault(x => x.PAL == PAL);
                energy = eerWithPAL?.AvgBwEER ?? 0;
                selectedEer = eerWithPAL;
                calculationMethod = EnergyCalculationMethod.PopulationAverage;
                dependency = "基于人群平均体重和PAL的建议值";
            }

            var offsetEnergy = AvailableEERs.Where(x => x.OffsetEnergy != default).Select(x => x.OffsetEnergy).Sum();

            if (offsetEnergy > 0)
            {
                energy += (int)offsetEnergy;
                strBuild.Append($"基于咨询对象的特殊生理时期，总能量需求偏移{(int)offsetEnergy}kCal，已计入总能量；因此");

            }

            strBuild.Append($"自动推断总能量{energy}kCal，依据：{dependency}。");
            strBuild.Append("如有需要请根据咨询者实际情况修正总能量，如无需修正请留空：");
            CalculatedEnergy = energy;
            CalculationMethod = calculationMethod;
            SelectedEer = selectedEer;
            AppliedOffsetEnergy = offsetEnergy > 0 ? (int)offsetEnergy : 0;
            IsEnergyManuallyAdjusted = false;
            Energy = energy;
            Summary = strBuild.ToString();
            Allocation = new MacronutrientAllocation(energy);
            FoodExchangeAllocation = new FoodExchangeAllocation(Allocation);
            return true;
        }

        public bool CorrectEnergy(int newEnergy)
        {
            if (newEnergy <= 0)
            {
                return false;
            }

            var height = (Client.Height ?? 0) / 100;
            var strBuild = new StringBuilder();
            if (height != 0 && Client.Weight != null && Client.Weight != 0)
            {
                strBuild.Append($"BMI:{Math.Round((Client.Weight.Value / height / height), 2)}，");
            }
            Energy = newEnergy;
            IsEnergyManuallyAdjusted = true;
            strBuild.AppendLine($"核定总能量{newEnergy}kCal，依据营养师修正。");
            strBuild.AppendLine("如有需要，可再次修正总能量，或点击上方计算按钮自动推断：");
            Summary = strBuild.ToString();
            Allocation = new MacronutrientAllocation(newEnergy);
            FoodExchangeAllocation = new FoodExchangeAllocation(Allocation);
            return true;
        }

        public SoapContribution ToSoapContribution()
        {
            var objectiveItems = new List<string>();

            if (Client.Height is > 0)
            {
                objectiveItems.Add($"身高：{FormatNumber(Client.Height.Value)} cm");
            }

            if (Client.Weight is > 0)
            {
                objectiveItems.Add($"体重：{FormatNumber(Client.Weight.Value)} kg");
            }

            var heightInMeters = (Client.Height ?? 0) / 100;
            if (heightInMeters > 0 && Client.Weight is > 0)
            {
                var bmi = Client.Weight.Value / heightInMeters / heightInMeters;
                objectiveItems.Add($"BMI：{FormatNumber(bmi)} kg/m²");
            }

            if (PAL is > 0)
            {
                objectiveItems.Add($"身体活动水平（PAL）：{FormatNumber(PAL.Value)}");
            }

            if (Energy is > 0)
            {
                if (IsEnergyManuallyAdjusted)
                {
                    objectiveItems.Add($"专业人员核定的每日总能量：{Energy.Value} kcal");

                    if (CalculatedEnergy is > 0 && CalculatedEnergy != Energy)
                    {
                        objectiveItems.Add($"程序原始估算的每日总能量需要量：{CalculatedEnergy.Value} kcal");
                    }
                }
                else
                {
                    var calculationBasis = CalculationMethod switch
                    {
                        EnergyCalculationMethod.IdealBodyWeightBeePal =>
                            "（依据身高推算的理想体重、基础能量系数与 PAL 计算）",
                        EnergyCalculationMethod.PopulationAverage =>
                            "（采用与当前 PAL 匹配的参考人群平均体重估计能量需要量〔EER〕）",
                        _ => string.Empty
                    };
                    objectiveItems.Add(
                        $"程序估算的每日总能量需要量：{Energy.Value} kcal{calculationBasis}");
                }
            }

            if (AppliedOffsetEnergy > 0)
            {
                var estimatedValue = IsEnergyManuallyAdjusted
                    ? "程序原始估算值"
                    : "上述程序估算值";
                objectiveItems.Add(
                    $"{estimatedValue}已计入特殊生理阶段能量附加量：{AppliedOffsetEnergy} kcal");
            }

            return objectiveItems.Count == 0
                ? new SoapContribution()
                : new SoapContribution(
                    Objective: $"人体测量与能量评估：{Environment.NewLine}- "
                        + string.Join($"{Environment.NewLine}- ", objectiveItems));
        }

        private static string FormatNumber(decimal value) =>
            value.ToString("0.##", CultureInfo.InvariantCulture);
    }

}
