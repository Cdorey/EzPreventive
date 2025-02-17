using EzNutrition.Shared.Data.Entities;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace EzNutrition.Server.Data.Repositories
{
    public class DietaryReferenceIntakeRepository
    {
        private readonly EzNutritionDbContext dbContext;

        public IEnumerable<EER> GetEERsByPersonalInfo(decimal age, string gender, IEnumerable<string> specialPhysiologicalPeriod)
        {
            var eers = from eer in dbContext.EERs
                       where (eer.Gender == gender || eer.Gender == null) && (eer.AgeStart <= age || eer.AgeStart == null) && (specialPhysiologicalPeriod.Contains(eer.SpecialPhysiologicalPeriod) || eer.SpecialPhysiologicalPeriod == null)
                       select eer;
            var maxAge = eers.Max(x => x.AgeStart);

            foreach (var eer in eers)
            {
                if (eer.AgeStart != default && eer.AgeStart != maxAge)
                {
                    continue;
                }
                else
                {
                    yield return eer;
                }
            }
        }

        public IEnumerable<DietaryReferenceIntakeValue> GetDRIsByPersonalInfo(decimal age, string gender, IEnumerable<string> specialPhysiologicalPeriod)
        {
            var query = from dri in (from dri in dbContext.DRIs
                                     where (dri.Gender == gender || dri.Gender == null) && (dri.AgeStart <= age || dri.AgeStart == null) && (specialPhysiologicalPeriod.Contains(dri.SpecialPhysiologicalPeriod) || dri.SpecialPhysiologicalPeriod == null)
                                     select dri).AsEnumerable()
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

                    if (dris.Any(x => x.AgeStart != null))
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
