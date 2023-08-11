using EzNutrition.Shared.Data.Entities;

namespace EzNutrition.Server.Data.Repositories
{
    public class AdviceRepository
    {
        private readonly EzNutritionDbContext dbContext;

        public List<Advice> GetAdviceByDiseaseID(List<int> diseaseIDs)
        {
            var advices = from a in dbContext.Advices
                          where a.Diseases.Any(x => diseaseIDs.Contains(x.DiseaseId))
                          orderby a.Priority ascending
                          select a;
            return advices.ToList();
        }

        public AdviceRepository(EzNutritionDbContext dbContext)
        {
            this.dbContext = dbContext;
        }
    }
}
