using EzNutrition.Shared.Data.Entities;

namespace EzNutrition.Server.Data.Repositories
{
    /// <summary>
    /// 食物成分表
    /// </summary>
    public class FoodNutritionValueRepository
    {
        private readonly EzNutritionDbContext dbContext;

        #region 食材表
        /// <summary>
        /// 创建一条食材记录
        /// </summary>
        /// <param name="friendlyCode">食物成分表原始代码</param>
        /// <param name="cite">数据来源</param>
        /// <param name="details">备注</param>
        /// <param name="friendlyName">友好名称</param>
        /// <returns></returns>
        public Food CreateFoodRecord(string friendlyCode, string cite, string details, string friendlyName)
        {
            var food = dbContext.Foods?.Add(new Food { Cite = cite, Details = details, FriendlyCode = friendlyCode, FriendlyName = friendlyName }) ?? throw new Exception("Invalid DbSet.");
            dbContext.SaveChanges();
            return food.Entity;
        }

        public List<Food> GetFoodRecordsByCode(string friendlyCode)
        {
            var records = from food in dbContext.Foods
                          where food.FriendlyCode == friendlyCode
                          select food;
            return records.ToList();
        }

        public Food GetFoodRecordById(Guid foodId)
        {
            var record = from food in dbContext.Foods
                         where food.FoodId == foodId
                         select food;
            return record.First();
        }

        public List<Food> GetFoodRecordsByName(string friendlyName)
        {
            var records = from food in dbContext.Foods
                          where food.FriendlyName == friendlyName
                          select food;
            return records.ToList();
        }

        public Food UpdateFoodRecordById(Guid foodId, Food record)
        {
            if (dbContext.Foods?.FirstOrDefault(x => x.FoodId == foodId) is Food target)
            {
                target.FriendlyName = record.FriendlyName ?? target.FriendlyName;
                target.FriendlyCode = record.FriendlyCode ?? target.FriendlyCode;
                target.Details = record.Details ?? target.Details;
                target.Cite = record.Cite ?? target.Cite;
                dbContext.SaveChanges();
                return target;
            }
            else
            {
                throw new Exception("Invalid Food Id.");
            }
        }
        #endregion


        #region 营养素参数表

        public Nutrient CreateNutrientRecord(string friendlyName, string? details, string defaultMeasureUnit)
        {
            var nutrient = dbContext.Nutrients?.Add(new Nutrient { FriendlyName = friendlyName, Details = details, DefaultMeasureUnit = defaultMeasureUnit }) ?? throw new Exception("Invalid DbSet.");
            dbContext.SaveChanges();
            return nutrient.Entity;
        }

        public Nutrient GetNutrientRecordById(int id)
        {
            var record = from nutrient in dbContext.Nutrients
                         where nutrient.NutrientId == id
                         select nutrient;
            return record.First();
        }

        public List<Nutrient> GetNutrientRecords()
        {
            return dbContext.Nutrients?.ToList() ?? throw new Exception("Invalid DbSet.");
        }

        public Nutrient UpdateNutrientRecordById(int id, Nutrient nutrient)
        {
            if (dbContext.Nutrients?.First(x => x.NutrientId == id) is Nutrient target)
            {
                target.FriendlyName = nutrient.FriendlyName ?? target.FriendlyName;
                target.Details = nutrient.Details ?? target.Details;
                target.DefaultMeasureUnit = nutrient.DefaultMeasureUnit ?? target.DefaultMeasureUnit;
                dbContext.SaveChanges();
                return target;
            }
            else
            {
                throw new Exception("Invalid Nutrient Id.");
            }
        }
        #endregion

        #region 食物成分表
        public FoodNutrientValue CreateOrUpdateFoodNutrientValueRecord(Guid foodId, int nutrientId, decimal value, string? measureUnit, string? details)
        {
            var target = from record in dbContext.FoodNutrientValues
                         where record.FoodId == foodId
                         where record.NutrientId == nutrientId
                         select record;
            if (target.Any())
            {
                var foodNutrientValue = target.First();
                foodNutrientValue.Value = value;
                foodNutrientValue.MeasureUnit = measureUnit ?? foodNutrientValue.MeasureUnit;
                foodNutrientValue.Details = details ?? foodNutrientValue.Details;
                dbContext.SaveChanges();
                return foodNutrientValue;
            }
            else
            {
                var foodNutrientValue = dbContext.FoodNutrientValues?.Add(new FoodNutrientValue { Details = details, FoodId = foodId, NutrientId = nutrientId, Value = value, MeasureUnit = measureUnit }) ?? throw new Exception("Invalid DbSet.");
                dbContext.SaveChanges();
                return foodNutrientValue.Entity;
            }
        }
        #endregion

        public FoodNutritionValueRepository(EzNutritionDbContext dbContext)
        {
            this.dbContext = dbContext;
        }
    }
}
