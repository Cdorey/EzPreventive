using AntDesign;
using EzNutrition.Client.Services;
using EzNutrition.Shared.Data.Entities;
using EzNutrition.Shared.Utilities;
using Microsoft.AspNetCore.Components;
using System.Text;

namespace EzNutrition.Client.Models
{
    public class MainTreatmentViewModel : ViewModelBase
    {
        public ClientInfo UserInfo { get; private set; }

        public string? Summary { get; private set; }

        public MacronutrientAllocation? Allocation { get; private set; }

        public FoodExchangeAllocation? FoodExchangeAllocation { get; private set; }

        public List<Advice>? Advices { get; set; }

        public async Task FetchEERs()
        {
            if (_userSession.UserInfo == null)
            {
                _navigationManager.NavigateTo("/");
                await _message.Error("需要登录");
                return;
            }

            if (!string.IsNullOrEmpty(UserInfo.Gender) && UserInfo.Age >= 0)
            {
                try
                {
                    UserInfo.AvailableEERs = await _httpClient.GetAuthorizedJsonAsync<List<EER>>(_userSession.UserInfo.Token, $"Energy/EERs/{UserInfo.Gender}/{UserInfo.Age}") ?? UserInfo.AvailableEERs;
                }
                catch (HttpRequestException ex)
                {
                    await _message.Error(ex.Message);
                }
            }
        }

        public void Calculate()
        {
            int CalculateEnergyFromBEE()
            {
                if (UserInfo.PAL is not null && UserInfo.Height is not null)
                {
                    var eerWithBEE = UserInfo.AvailableEERs.FirstOrDefault(x => x.BEE != null);
                    if (eerWithBEE?.BEE is not null)
                    {
                        return EnergyCalculator.GetEnergy(UserInfo.Height.Value, eerWithBEE.BEE.Value, UserInfo.PAL.Value);
                    }
                }
                return 0;
            }

            int CalculateEnergyFromPAL()
            {
                if (UserInfo.PAL is not null)
                {
                    var eerWithPAL = UserInfo.AvailableEERs.FirstOrDefault(x => x.PAL == UserInfo.PAL);
                    return eerWithPAL?.AvgBwEER ?? 0;
                }
                return 0;
            }

            if (UserInfo.PAL is not null)
            {
                var beeResult = CalculateEnergyFromBEE();
                var energy = beeResult == 0 ? CalculateEnergyFromPAL() : beeResult;
                //输出能量模型
                if (energy > 0)
                {
                    var height = (UserInfo.Height ?? 0) / 100;
                    var strBuild = new StringBuilder();
                    if (height != 0 && UserInfo.Weight != null && UserInfo.Weight != 0)
                    {
                        strBuild.Append($"BMI:{Math.Round((UserInfo.Weight.Value / height / height), 2)}，");
                    }
                    strBuild.AppendLine($"自动推断总能量{energy}kCal，依据：{(beeResult == 0 ? "基于人群平均体重和PAL的建议值" : "BW*BEE*PAL")}。");
                    strBuild.AppendLine("如有需要请根据咨询者实际情况修正总能量，如无需修正请留空：");
                    Summary = strBuild.ToString();
                    Allocation = new MacronutrientAllocation(energy);
                    FoodExchangeAllocation = new FoodExchangeAllocation(Allocation);
                }
            }
            else
            {
                _message.Error("PAL（活动强度）不应为0，请正确输入咨询对象性别、年龄等信息，并选择一个PAL");
            }

        }

        public void Reset()
        {
            //重置全部信息
            UserInfo = new();
            Summary = null;
            Allocation = null;
            FoodExchangeAllocation = null;
            Advices = null;
            _message.Info("清空当前信息，开始下一个咨询对象");
        }

        public void CorrectEnergy(int newEnergy)
        {
            if (newEnergy > 0)
            {
                var height = (UserInfo.Height ?? 0) / 100;
                var strBuild = new StringBuilder();
                if (height != 0 && UserInfo.Weight != null && UserInfo.Weight != 0)
                {
                    strBuild.Append($"BMI:{Math.Round((UserInfo.Weight.Value / height / height), 2)}，");
                }
                strBuild.AppendLine($"核定总能量{newEnergy}kCal，依据营养师修正。");
                strBuild.AppendLine("如有需要，可再次修正总能量，或点击上方计算按钮自动推断：");
                Summary = strBuild.ToString();
                Allocation = new MacronutrientAllocation(newEnergy);
                FoodExchangeAllocation = new FoodExchangeAllocation(Allocation);
                _message.Info($"核定总能量{newEnergy}kCal，依据营养师修正。");
            }
        }

        public MainTreatmentViewModel(IMessageService message, HttpClient httpClient, UserSessionService userSession, NavigationManager navigationManager) : base(message, httpClient, userSession, navigationManager)
        {
            UserInfo = new();
        }
    }
}
