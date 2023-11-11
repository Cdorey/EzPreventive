using EzNutrition.Shared.Data.Entities;

namespace EzNutrition.Client.Models
{
    public class ClientInfo
    {
        private List<DietaryReferenceIntakeValue> availableDRIs = new();

        public List<EER> AvailableEERs { get; set; } = new();

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

        public List<NutrientRange> NutrientRanges { get; private set; } = new();

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

        public string? Name { get; set; }

        public string? Gender { get; set; }
        public int Age { get; set; } = 25;

        public decimal? PAL { get; set; }
        public decimal? Height { get; set; }
        public decimal? Weight { get; set; }

        public string SpecialPhysiologicalPeriod { get; set; } = string.Empty;

    }

}
