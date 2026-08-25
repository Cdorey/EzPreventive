using EzNutrition.Shared.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EzNutrition.Server.Data.Repositories
{
    /// <summary>
    /// 食物成分表
    /// </summary>
    public class FoodNutritionValueRepository(EzNutritionDbContext dbContext)
    {
        public Task<Food?> FoodNutritionValueByFriendlyCodeAsync(
            string friendlyCode,
            CancellationToken cancellationToken)
        {
            return dbContext.Foods!
                .AsNoTracking()
                .Include(f => f.FoodNutrientValues)!
                .ThenInclude(fnv => fnv.Nutrient)
                .FirstOrDefaultAsync(x => x.FriendlyCode == friendlyCode, cancellationToken);
        }

        public Task<Food[]> GetFoodsAsync(CancellationToken cancellationToken) =>
            dbContext.Foods!.AsNoTracking().ToArrayAsync(cancellationToken);

        public Task<Nutrient[]> GetNutrientsAsync(CancellationToken cancellationToken) =>
            dbContext.Nutrients!.AsNoTracking().ToArrayAsync(cancellationToken);
    }
}
