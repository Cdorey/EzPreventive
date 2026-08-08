using EzNutrition.Shared.Data.Entities;
using System.Net.Http.Json;

namespace EzNutrition.Client.Models
{
    /// <summary>
    /// 表示一组 DRIs 记录无法形成单一聚合值的问题。
    /// </summary>
    public sealed record DriAggregationIssue
    {
        /// <summary>
        /// 获取相关营养素名称。
        /// </summary>
        public required string Nutrient { get; init; }

        /// <summary>
        /// 获取不包含原始病史内容的错误说明。
        /// </summary>
        public required string Message { get; init; }
    }

    public class DRIs(IClient client) : ITreatment
    {
        private List<DietaryReferenceIntakeValue> availableDRIs = [];

        private void RebuildNutrientRanges()
        {
            var ranges = new List<NutrientRange>();
            var issues = new List<DriAggregationIssue>();
            foreach (var rangeInfo in AvailableDRIs.GroupBy(x => x.Nutrient ?? string.Empty))
            {
                try
                {
                    var range = new NutrientRange(rangeInfo);
                    ranges.Add(range);
                    foreach (var value in new[] { range.EAR, range.RNI, range.UL }.Where(value => value is not null))
                    {
                        if (value!.InnerRecords.Count(record => !record.IsOffset) != 1)
                        {
                            issues.Add(new DriAggregationIssue
                            {
                                Nutrient = rangeInfo.Key,
                                Message = "参考值包含无法自动确定的基础记录。"
                            });
                        }
                    }
                }
                catch (ArgumentException ex)
                {
                    issues.Add(new DriAggregationIssue
                    {
                        Nutrient = rangeInfo.Key,
                        Message = ex.Message
                    });
                }
            }

            NutrientRanges = ranges;
            AggregationIssues = issues;
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
                availableDRIs = value ?? [];
                RebuildNutrientRanges();
            }
        }

        public List<NutrientRange> NutrientRanges { get; private set; } = [];

        /// <summary>
        /// 获取 DRIs 聚合过程中识别到的问题。
        /// </summary>
        public IReadOnlyList<DriAggregationIssue> AggregationIssues { get; private set; } = [];

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
