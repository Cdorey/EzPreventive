using EzNutrition.Shared.Data.Entities;
using System.Linq;

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
                       select eer;
            return eers.Where(x => x.AgeStart == eers.Max(x => x.AgeStart)).ToList() ?? new List<EER>();
        }


        public EnergyRepository(EzNutritionDbContext dbContext)
        {
            this.dbContext = dbContext;
        }
    }
}
