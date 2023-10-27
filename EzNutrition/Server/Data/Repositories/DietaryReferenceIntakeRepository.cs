using EzNutrition.Shared.Data.Entities;
using System.Linq;

namespace EzNutrition.Server.Data.Repositories
{
    public class DietaryReferenceIntakeRepository
    {
        private readonly EzNutritionDbContext dbContext;

        public List<EER> GetEERsByPersonalInfo(decimal age, string gender)
        {
            var eers = from eer in dbContext.EERs
                       where eer.Gender == gender
                       && eer.AgeStart <= age
                       select eer;
            return eers.Where(x => x.AgeStart == eers.Max(x => x.AgeStart)).ToList() ?? new List<EER>();
        }

        public IEnumerable<DietaryReferenceIntakeValue> GetDRIsByPersonalInfo(decimal age, string gender, IEnumerable<string> specialPhysiologicalPeriod)
        {
            var query = from dri in dbContext.DRIs
                        where dri.Gender == gender || dri.Gender == null
                        where dri.AgeStart <= age || dri.AgeStart == null
                        where specialPhysiologicalPeriod.Contains(dri.SpecialPhysiologicalPeriod) || dri.SpecialPhysiologicalPeriod == null
                        group dri by dri.Nutrient into records
                        select new
                        {
                            Nutrient = records.Key,
                            DRIs = from record in records
                                   group record by (record.RecordType == DietaryReferenceIntakeType.AI ? "RNI" : record.RecordType.ToString())
                        };

            var nutrients = query.ToList();

            foreach (var nutrient in nutrients)
            {
                foreach (var dris in nutrient.DRIs)
                {
                    decimal? maxAge = null;

                    if (dris.Count(x => x.AgeStart != null) > 0)
                    {
                        maxAge = dris.Max(x => x.AgeStart);
                    }

                    foreach (var dri in dris)
                    {
                        if (dri.AgeStart == null || dri.AgeStart == maxAge)
                        {
                            yield return dri;
                            continue;
                        }
                    }

                }
            }
        }


        public DietaryReferenceIntakeRepository(EzNutritionDbContext dbContext)
        {
            this.dbContext = dbContext;
        }
    }
}
