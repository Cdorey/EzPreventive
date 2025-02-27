using AntDesign;
using EzNutrition.Client.Services;
using EzNutrition.Shared.Data.Entities;
using EzNutrition.Shared.Utilities;
using Microsoft.AspNetCore.Components;
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

        public async Task FetchEersAsync(IMessageService message,
                                    HttpClient httpClient,
                                    UserSessionService userSession,
                                    NavigationManager navigationManager)
        {
            if (userSession.UserInfo == null)
            {
                navigationManager.NavigateTo("/");
                await message.Error("需要登录");
                return;
            }

            if (!string.IsNullOrEmpty(Client.Gender) && Client.Age >= 0)
            {
                try
                {
                    var postRes = await httpClient.PostAsJsonAsync($"Energy/EERs/{Client.Gender}/{Client.Age}", new List<string> { Client.SpecialPhysiologicalPeriod });

                    if (postRes.IsSuccessStatusCode)
                    {
                        AvailableEERs = await postRes.Content.ReadFromJsonAsync<List<EER>>() ?? AvailableEERs;
                    }
                }
                catch (HttpRequestException ex)
                {
                    await message.Error(ex.Message);
                }
            }
        }

        public void Calculate(IMessageService message)
        {
            if (PAL is null)
            {
                message.Error("PAL（活动强度）不应为0，请正确输入咨询对象性别、年龄等信息，并选择一个PAL");
                return;
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
        }

        public void CorrectEnergy(int newEnergy, IMessageService message)
        {
            if (newEnergy > 0)
            {
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
                message.Info($"核定总能量{newEnergy}kCal，依据营养师修正。");
            }
        }
    }

    public class AiGeneratedAdvice
    {
        public bool IsReady { get; set; } = false;

        public string ReasoningContent { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;
    }
}
