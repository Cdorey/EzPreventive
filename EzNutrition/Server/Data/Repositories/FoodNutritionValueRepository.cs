using EzNutrition.Shared.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EzNutrition.Server.Data.Repositories
{
    /// <summary>
    /// 食物成分表
    /// </summary>
    public class FoodNutritionValueRepository(EzNutritionDbContext dbContext)
    {
        public Food? FoodNutritionValueByFriendlyCode(string friendlyCode)
        {
            return dbContext.Foods!
                .AsNoTracking()
                .Include(f => f.FoodNutrientValues)!
                .ThenInclude(fnv => fnv.Nutrient)
                .FirstOrDefault(x => x.FriendlyCode == friendlyCode);
        }

        public Food[] GetFoods()
        {
            return [.. dbContext.Foods!.AsNoTracking()];
        }

        public  Nutrient[] GetNutrients()
        {
            return [.. dbContext.Nutrients!.AsNoTracking()];
        }
    }
}
