using EzNutrition.Shared.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EzNutrition.Server.Data
{
    public class EzNutritionDbContext(DbContextOptions<EzNutritionDbContext> options) : DbContext(options)
    {
        public DbSet<Food>? Foods { get; set; }

        public DbSet<Nutrient>? Nutrients { get; set; }

        public DbSet<FoodNutrientValue>? FoodNutrientValues { get; set; }

        //public DbSet<Person>? People { get; set; }

        //public DbSet<Advice>? Advices { get; set; }

        //public DbSet<Disease>? Diseases { get; set; }

        #region DRIs
        public DbSet<EER>? EERs { get; set; }

        public DbSet<DietaryReferenceIntakeValue>? DRIs { get; set; }

        #endregion
    }
}
