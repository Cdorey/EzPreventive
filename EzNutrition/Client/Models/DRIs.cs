using EzNutrition.Shared.Data.Entities;
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

        public async Task FetchDRIsAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(Client.Gender) || Client.Age < 0)
            {
                throw new InvalidOperationException("性别和年龄信息无效。");
            }

            using var response = await httpClient.PostAsJsonAsync(
                $"Energy/DRIs/{Uri.EscapeDataString(Client.Gender)}/{Client.Age}",
                new[] { Client.SpecialPhysiologicalPeriod },
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var dris = await response.Content.ReadFromJsonAsync<List<DietaryReferenceIntakeValue>>(cancellationToken);
            if (dris is null || dris.Count == 0)
            {
                throw new InvalidDataException("服务器没有返回可用的膳食参考摄入量记录。");
            }

            cancellationToken.ThrowIfCancellationRequested();
            AvailableDRIs = dris;
        }
    }
}
