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
                                      NavigationManager navigationManager,
                                      ILogger<ArchiveManageService> logger) : ConcurrentDictionary<Guid, Archive>()
    {
        private readonly HttpClient _httpClient = httpClientFactory.CreateClient("Authorize");

        public event EventHandler? ClientNameChanged;

        public Guid NewArchive()
        {
            var client = new ClientInfo();
            this[client.ClientId] = new Archive(client);
            client.NameChanged += HandleClientNameChanged;
            return client.ClientId;
        }

        public async Task ClientInfoConfirmed(Archive archive, CancellationToken cancellationToken = default)
        {
            if (archive.IsLoading)
            {
                return;
            }

            if (userSession.UserInfo is null || userSession.UserInfo.IsExpired)
            {
                navigationManager.NavigateTo("/");
                await message.ErrorAsync("需要登录");
                return;
            }

            if (string.IsNullOrEmpty(archive.Client.Gender))
            {
                await message.ErrorAsync("性别不能为空");
                return;
            }

            if (archive.Client.Age < 0)
            {
                await message.ErrorAsync("年龄不符合逻辑");
                return;
            }

            try
            {
                archive.IsLoading = true;
                var energyCalculator = new EnergyCalculator(archive.Client);
                var dris = new DRIs(archive.Client);
                await dris.FetchDRIsAsync(_httpClient, cancellationToken);

                var foodsTask = _httpClient.GetFromJsonAsync<List<Food>>(
                    "FoodComposition/Foods",
                    cancellationToken);
                var nutrientsTask = _httpClient.GetFromJsonAsync<List<Nutrient>>(
                    "FoodComposition/Nutrients",
                    cancellationToken);
                await Task.WhenAll(foodsTask, nutrientsTask);

                var foods = await foodsTask;
                var nutrients = await nutrientsTask;
                if (foods is null || foods.Count == 0 || nutrients is null || nutrients.Count == 0)
                {
                    throw new InvalidDataException("Food composition metadata is empty.");
                }

                var dietaryRecallSurvey = new DietaryRecallSurvey(archive.Client, foods, nutrients, dris);
                dietaryRecallSurvey.OnCalculate += (sender, e) =>
                {
                    var standardTower = StandardTower.GetStandardTower(archive.Client.Age);
                    archive.DietaryTower = standardTower is null
                        ? null
                        : new DietaryRecallTower(dietaryRecallSurvey.RecallEntries, standardTower);
                };

                archive.CurrentEnergyCalculator = energyCalculator;
                archive.DRIs = dris;
                archive.DietaryRecallSurvey = dietaryRecallSurvey;
                archive.DietaryTower = StandardTower.GetStandardTower(archive.Client.Age);
                archive.SubjectiveObjectiveAssessmentPlanInformation = new();
                archive.ClientInfoFormEnabled = false;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex) when (ex is HttpRequestException or NotSupportedException or System.Text.Json.JsonException or InvalidDataException)
            {
                logger.LogWarning(ex, "Unable to initialize archive for nutrition assessment.");
                await message.ErrorAsync("初始化营养评估失败，请检查网络后重试。");
                archive.CurrentEnergyCalculator = null;
                archive.DRIs = null;
                archive.DietaryRecallSurvey = null;
                archive.DietaryTower = null;
                archive.ClientInfoFormEnabled = true;
                archive.SubjectiveObjectiveAssessmentPlanInformation = null;
            }
            finally
            {
                archive.IsLoading = false;
            }
        }

        public async Task ClientInfoConfirmed(Guid archiveId, CancellationToken cancellationToken = default)
        {
            if (!TryGetValue(archiveId, out var archive))
            {
                throw new KeyNotFoundException($"Archive {archiveId} does not exist.");
            }

            await ClientInfoConfirmed(archive, cancellationToken);
        }

        private void HandleClientNameChanged(object? sender, EventArgs e) =>
            ClientNameChanged?.Invoke(sender, e);
    }
}
