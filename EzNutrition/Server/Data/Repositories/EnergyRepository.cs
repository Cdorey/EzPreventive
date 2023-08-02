using EzNutrition.Server.Data.Entities;

namespace EzNutrition.Server.Data.Repositories
{
    public class EnergyRepository
    {
        private readonly EzNutritionDbContext dbContext;

        public List<EER> GetEERsByPersonalInfo(decimal age, string gender)
        {
            var eers = from eer in dbContext.EERs
                       where eer.Gender == gender
                       && eer.AgeStart <= age
                       group eer by eer.AgeStart;
            return eers.Any() ? eers.OrderBy(x => x.Key).First().ToList() : new List<EER>();
        }


        public EnergyRepository(EzNutritionDbContext dbContext)
        {
            this.dbContext = dbContext;
        }
    }
}
