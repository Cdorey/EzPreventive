using EzNutrition.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EzNutrition.Server.Data
{
    public class EzNutritionDbContext : DbContext
    {
        public DbSet<Food>? Foods { get; set; }

        public DbSet<Nutrient>? Nutrients { get; set; }

        public DbSet<FoodNutrientValue>? FoodNutrientValues { get; set; }

        public DbSet<Person>? People { get; set; }

        public DbSet<MultiDerivedPersonRelationship>? MultiDerivedPersonRelationships { get; set; }

        public DbSet<PersonalDietaryReferenceIntakeValue>? PersonalDietaryReferenceIntakes { get; set; }

        public EzNutritionDbContext(DbContextOptions<EzNutritionDbContext> options) : base(options) { }
    }
}
