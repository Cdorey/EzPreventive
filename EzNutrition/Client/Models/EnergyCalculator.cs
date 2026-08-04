using EzNutrition.Shared.Data.Entities;
using EzNutrition.Shared.Utilities;
using System.Net.Http.Json;
using System.Text;

namespace EzNutrition.Client.Models
{
    public class EnergyCalculator(IClient client) : ITreatment
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

        public decimal? PAL { get; set; }

        public decimal? BMI { get; private set; }

        public string Summary { get; private set; } = string.Empty;

        public int? Energy { get; private set; }

        public MacronutrientAllocation? Allocation { get; private set; }

        public FoodExchangeAllocation? FoodExchangeAllocation { get; private set; }

        public async Task FetchEersAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(Client.Gender) || Client.Age < 0)
            {
                throw new InvalidOperationException("性别和年龄信息无效。");
            }

            using var response = await httpClient.PostAsJsonAsync(
                $"Energy/EERs/{Uri.EscapeDataString(Client.Gender)}/{Client.Age}",
                new[] { Client.SpecialPhysiologicalPeriod },
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var eers = await response.Content.ReadFromJsonAsync<List<EER>>(cancellationToken);
            if (eers is null || eers.Count == 0)
            {
                throw new InvalidDataException("服务器没有返回可用的能量参考记录。");
            }

            AvailableEERs = eers;
        }

        public bool Calculate()
        {
            if (PAL is null)
            {
                return false;
            }

            var energy = 0;
            var strBuild = new StringBuilder();
            strBuild.Append($"咨询对象的PAL：{PAL}，");

            if (Client.Height is not null)
            {
                var eerWithBEE = AvailableEERs.FirstOrDefault(x => x.BEE is not null);
                if (eerWithBEE?.BEE is not null)
                {
                    energy = EzNutrition.Shared.Utilities.EnergyCalculator.GetEnergy(Client.Height.Value, eerWithBEE.BEE.Value, PAL.Value);

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
            strBuild.AppendLine($"核定总能量{newEnergy}kCal，依据营养师修正。");
            strBuild.AppendLine("如有需要，可再次修正总能量，或点击上方计算按钮自动推断：");
            Summary = strBuild.ToString();
            Allocation = new MacronutrientAllocation(newEnergy);
            FoodExchangeAllocation = new FoodExchangeAllocation(Allocation);
            return true;
        }
    }

    public class AiGeneratedAdvice
    {
        public bool IsReady { get; set; } = false;

        public bool Sending { get; set; } = false;

        public string ReasoningContent { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;
    }
}
