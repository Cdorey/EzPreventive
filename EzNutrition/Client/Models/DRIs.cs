using AntDesign;
using EzNutrition.Client.Services;
using EzNutrition.Shared.Data.Entities;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace EzNutrition.Client.Models
{
    public class DRIs(IClient client) : ITreatment
    {
        private List<DietaryReferenceIntakeValue> availableDRIs = [];

        private IEnumerable<NutrientRange> RangeCastForDRIs()
        {
            foreach (var rangeInfo in AvailableDRIs.GroupBy(x => x.Nutrient ?? string.Empty))
            {
                NutrientRange result;
                try
                {
                    result = new NutrientRange(rangeInfo);
                }
                catch (ArgumentException)
                {
#warning 这里直接丢弃不能解析的值，缺少正确的处理逻辑
                    continue;
                }
                yield return result;
            }
        }

        public IClient Client => client;

        public List<DietaryReferenceIntakeValue> AvailableDRIs
        {
            get
            {
                return availableDRIs;
            }

            set
            {
                availableDRIs = value;
                NutrientRanges = RangeCastForDRIs().ToList();
            }
        }

        public List<NutrientRange> NutrientRanges { get; private set; } = [];

        public string[] Requirements { get; } = [nameof(IClient.Gender), nameof(IClient.Age), nameof(IClient.SpecialPhysiologicalPeriod)];

        public async Task FetchDRIsAsync(IMessageService message,
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
                    var postRes = await httpClient.PostAsJsonAsync($"Energy/DRIs/{Client.Gender}/{Client.Age}", new List<string> { Client.SpecialPhysiologicalPeriod });

                    if (postRes.IsSuccessStatusCode)
                    {
                        AvailableDRIs = await postRes.Content.ReadFromJsonAsync<List<DietaryReferenceIntakeValue>>() ?? AvailableDRIs;
                    }
                }
                catch (HttpRequestException ex)
                {
                    await message.Error(ex.Message);
                }
            }
        }
    }
}
