using EzNutrition.Server.Data.Entities;

namespace EzNutrition.Server.Data.Repositories
{
    public class DiseaseRepository
    {
        private EzNutritionDbContext dbContext;

        public List<Disease> GetDiseases()
        {
            return dbContext.Diseases?.ToList() ?? new List<Disease>();
        }

        public DiseaseRepository(EzNutritionDbContext dbContext)
        {
            this.dbContext = dbContext;
        }
    }
}
