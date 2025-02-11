using EzNutrition.Shared.Data.Entities;

namespace EzNutrition.Client.Models
{
    public class ClientInfo : IClient
    {
        ////        private List<DietaryReferenceIntakeValue> availableDRIs = [];

        ////        public List<EER> AvailableEERs { get; set; } = [];

        ////        public List<DietaryReferenceIntakeValue> AvailableDRIs
        ////        {
        ////            get
        ////            {
        ////                return availableDRIs;
        ////            }

        ////            set
        ////            {
        ////                availableDRIs = value;
        ////                NutrientRanges = RangeCastForDRIs().ToList();
        ////            }
        ////        }

        ////        public List<NutrientRange> NutrientRanges { get; private set; } = [];

        ////        private IEnumerable<NutrientRange> RangeCastForDRIs()
        ////        {
        ////            foreach (var rangeInfo in AvailableDRIs.GroupBy(x => x.Nutrient ?? string.Empty))
        ////            {
        ////                NutrientRange result;
        ////                try
        ////                {
        ////                    result = new NutrientRange(rangeInfo);
        ////                }
        ////                catch (ArgumentException)
        ////                {
        ////#warning 这里直接丢弃不能解析的值，缺少正确的处理逻辑
        ////                    continue;
        ////                }

        ////                yield return result;
        ////            }
        ////        }

        public string? Name { get; set; }

        public string? Gender { get; set; }

        public int Age { get; set; } = 25;

        ////public decimal? PAL { get; set; }

        public decimal? Height { get; set; }

        public decimal? Weight { get; set; }

        public Guid ClientId { get; set; } = new Guid();

        public string SpecialPhysiologicalPeriod { get; set; } = string.Empty;

    }

}
