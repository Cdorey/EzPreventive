using EzNutrition.Shared.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EzNutrition.Server.Data.Repositories
{
    public class DietaryReferenceIntakeRepository
    {
        private readonly EzNutritionDbContext dbContext;

        public async Task<IReadOnlyList<EER>> GetEERsByPersonalInfoAsync(
            decimal age,
            string gender,
            IEnumerable<string>? specialPhysiologicalPeriod,
            CancellationToken cancellationToken)
        {
            var periods = NormalizePeriods(specialPhysiologicalPeriod);
            var eers = await dbContext.EERs!
                .AsNoTracking()
                .Where(eer =>
                    (eer.Gender == gender || eer.Gender == null) &&
                    (eer.AgeStart <= age || eer.AgeStart == null) &&
                    (eer.SpecialPhysiologicalPeriod == null || periods.Contains(eer.SpecialPhysiologicalPeriod)))
                .ToListAsync(cancellationToken);

            if (eers.Count == 0)
            {
                return [];
            }

            var maxAge = eers.Max(eer => eer.AgeStart);
            return eers
                .Where(eer => eer.AgeStart == null || eer.AgeStart == maxAge)
                .ToArray();
        }

        public async Task<IReadOnlyList<DietaryReferenceIntakeValue>> GetDRIsByPersonalInfoAsync(
            decimal age,
            string gender,
            IEnumerable<string>? specialPhysiologicalPeriod,
            CancellationToken cancellationToken)
        {
            var periods = NormalizePeriods(specialPhysiologicalPeriod);
            var records = await dbContext.DRIs!
                .AsNoTracking()
                .Where(dri =>
                    (dri.Gender == gender || dri.Gender == null) &&
                    (dri.AgeStart <= age || dri.AgeStart == null) &&
                    (dri.SpecialPhysiologicalPeriod == null || periods.Contains(dri.SpecialPhysiologicalPeriod)))
                .ToListAsync(cancellationToken);

            if (records.Count == 0)
            {
                return [];
            }

            return records
                .GroupBy(record => record.Nutrient)
                .SelectMany(nutrient => nutrient.GroupBy(record =>
                    record.RecordType == DietaryReferenceIntakeType.AI
                        ? "RNI"
                        : record.RecordType.ToString()))
                .SelectMany(group =>
                {
                    var maxAge = group.Max(record => record.AgeStart);
                    return group.Where(record => record.AgeStart == null || record.AgeStart == maxAge);
                })
                .ToArray();
        }

        private static string[] NormalizePeriods(IEnumerable<string>? periods) =>
            periods?
                .Where(period => !string.IsNullOrWhiteSpace(period))
                .Select(period => period.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray() ?? [];

        public DietaryReferenceIntakeRepository(EzNutritionDbContext dbContext)
        {
            this.dbContext = dbContext;
        }
    }
}
