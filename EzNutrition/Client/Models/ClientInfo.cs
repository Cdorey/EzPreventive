using EzNutrition.Shared.Data.Entities;

namespace EzNutrition.Client.Models
{
    public class ClientInfo
    {
        public List<EER> AvailableEERs { get; set; } = new();
        public List<DietaryReferenceIntakeValue> AvailableDRIs { get; set; } = new();

        public dynamic RangeCastForDRIs()
        {
            var rangeInfos = from dris in AvailableDRIs
                             where dris.RecordType == DietaryReferenceIntakeType.AI || dris.RecordType == DietaryReferenceIntakeType.RNI || dris.RecordType == DietaryReferenceIntakeType.UL || dris.RecordType == DietaryReferenceIntakeType.EAR
                             group dris by dris.Nutrient;

            foreach (var rangeInfo in rangeInfos)
            {
                var ears = rangeInfo.Where(dris => dris.RecordType == DietaryReferenceIntakeType.EAR);
                var rnis = rangeInfo.Where(dris => dris.RecordType == DietaryReferenceIntakeType.AI || dris.RecordType == DietaryReferenceIntakeType.RNI);
                var uls = rangeInfo.Where(dris => dris.RecordType == DietaryReferenceIntakeType.UL);
            }
        }

        public string? Name { get; set; }

        public string? Gender { get; set; }
        public int Age { get; set; } = 25;

        public decimal? PAL { get; set; }
        public decimal? Height { get; set; }
        public decimal? Weight { get; set; }
    }

}
