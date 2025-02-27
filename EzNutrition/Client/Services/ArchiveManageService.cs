using AntDesign;
using EzNutrition.Client.Models;
using EzNutrition.Client.Models.DietarySurvey;
using EzNutrition.Shared.Data.DietaryRecallSurvey;
using EzNutrition.Shared.Data.Entities;
using Microsoft.AspNetCore.Components;
using System.Collections.Concurrent;
using System.Net.Http.Json;

namespace EzNutrition.Client.Services
{
    public class ArchiveManageService(IMessageService message,
                                      IHttpClientFactory httpClientFactory,
                                      UserSessionService userSession,
                                      NavigationManager navigationManager) : ConcurrentDictionary<Guid, Archive>()
    {
        private readonly HttpClient _httpClient = httpClientFactory.CreateClient("Authorize");

        public event EventHandler? ClientNameChanged;

        public bool Loading { get; set; } = false;


        public Guid NewArchive()
        {
            var client = new ClientInfo();
            this[client.ClientId] = new Archive(client);
            client.NameChanged += ClientNameChanged;
            return client.ClientId;
        }

        public async Task ClientInfoConfirmed(Archive archive)
        {
            if (userSession.UserInfo == null)
            {
                navigationManager.NavigateTo("/");
                await message.Error("需要登录");
                return;
            }

            if (string.IsNullOrEmpty(archive.Client.Gender))
            {
                await message.Error("性别不能为空");
                return;
            }

            if (archive.Client.Age < 0)
            {
                await message.Error("年龄不符合逻辑");
                return;
            }

            try
            {
                Loading = true;
                archive.CurrentEnergyCalculator = new EnergyCalculator(archive.Client);
                archive.DRIs = new DRIs(archive.Client);
                await archive.DRIs.FetchDRIsAsync(message, _httpClient, userSession, navigationManager);
                List<Food>? foods = await _httpClient.GetFromJsonAsync<List<Food>>("FoodComposition/Foods");
                List<Nutrient>? nutrients = await _httpClient.GetFromJsonAsync<List<Nutrient>>("FoodComposition/Nutrients");

                if (foods is not null && nutrients is not null)
                {
                    archive.DietaryRecallSurvey = new DietaryRecallSurvey(archive.Client, foods, nutrients, archive.DRIs);
                    archive.DietaryRecallSurvey.OnCalculate += (sender, e) =>
                    {
                        var stdTower = StandardTower.GetStandardTower(archive.Client.Age);
                        if (stdTower is not null)
                        {
                            archive.DietaryTower = new DietaryRecallTower(archive.DietaryRecallSurvey.RecallEntries, stdTower);
                        }
                    };
                }

                archive.DietaryTower = StandardTower.GetStandardTower(archive.Client.Age);
                archive.SubjectiveObjectiveAssessmentPlanInformation = new SubjectiveObjectiveAssessmentPlanInformation();
                archive.ClientInfoFormEnabled = false;
            }
            catch (HttpRequestException ex)
            {
                await message.Error(ex.Message);
                archive.CurrentEnergyCalculator = null;
                archive.DRIs = null;
                archive.DietaryRecallSurvey = null;
                archive.DietaryTower = null;
                archive.ClientInfoFormEnabled = true;
                archive.SubjectiveObjectiveAssessmentPlanInformation = null;
            }
            finally
            {
                Loading = false;
            }
        }

        public async Task ClientInfoConfirmed(Guid archiveId)
        {
            await ClientInfoConfirmed(this[archiveId]);
        }
    }
}
