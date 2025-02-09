using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Repositories;
using EzNutrition.Shared.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;

namespace EzNutrition.DataInserter
{
    /// <summary>
    /// 一些简单的脚本，用于向数据库追加原始数据集
    /// </summary>
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("ConnectionString:");
            var connectionString = Console.ReadLine();
            var optBuilder = new DbContextOptionsBuilder<EzNutritionDbContext>();
            optBuilder.UseSqlServer(connectionString).EnableSensitiveDataLogging().LogTo(Console.WriteLine, LogLevel.Information);
            var db = new EzNutritionDbContext(optBuilder.Options);
            var repo = new FoodNutritionValueRepository(db);
            var x = repo.GetFoods();
            Console.WriteLine("FilePath:");
            var filePath = Console.ReadLine();
            Values(db, filePath!);
        }

        public static void Foods(EzNutritionDbContext db, string filePath)
        {
            using Workbook workbook = new Workbook(filePath!);
            foreach (var row in workbook.Rows)
            {
                var rec = new Food
                {
                    FriendlyCode = row[0].Trim(),
                    FriendlyName = row[1].Trim(),
                    EdiblePortion = string.IsNullOrWhiteSpace(row[2].Trim()) ? 100 : int.Parse(row[2].Trim()),
                    Details = row[3].Trim(),
                    FoodGroups = row[4].Trim(),
                    FoodId = Guid.NewGuid()
                };
                db.Foods!.Add(rec);
            }
            db.SaveChanges();
        }
        public static void Nutrients(EzNutritionDbContext db, string filePath)
        {
            using Workbook workbook = new Workbook(filePath!);
            foreach (var row in workbook.Rows)
            {
                var rec = new Nutrient
                {
                    FriendlyName = row[1].Trim(),
                    DefaultMeasureUnit = row[2].Trim(),
                    NutrientId = int.Parse(row[0].Trim())
                };
                db.Nutrients!.Add(rec);
            }
            db.SaveChanges();
        }
        public static void Values(EzNutritionDbContext db, string filePath)
        {
            using Workbook workbook = new Workbook(filePath!);
            var foods = db.Foods!.ToArray();
            foreach (var row in workbook.Rows)
            {
                var rec = new FoodNutrientValue
                {
                    NutrientId = int.Parse(row[1].Trim()),
                    Value = decimal.Parse(row[2].Trim()),
                    FoodId = foods.First(x => x.FriendlyCode == row[0].Trim()).FoodId,
                    FoodNutrientValueId = int.Parse(row[3].Trim())
                };
                db.FoodNutrientValues!.Add(rec);
            }
            db.SaveChanges();
        }
    }
}