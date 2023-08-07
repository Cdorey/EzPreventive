using EzNutrition.Shared.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EzNutrition.Server.Data
{
    public class EzNutritionDbContext : DbContext
    {
        public DbSet<Food>? Foods { get; set; }

        public DbSet<Nutrient>? Nutrients { get; set; }

        public DbSet<FoodNutrientValue>? FoodNutrientValues { get; set; }

        public DbSet<Person>? People { get; set; }

        public DbSet<Advice>? Advices { get; set; }

        public DbSet<Disease>? Diseases { get; set; }

        public DbSet<EER>? EERs { get; set; }

        public DbSet<MultiDerivedPersonRelationship>? MultiDerivedPersonRelationships { get; set; }

        public DbSet<PersonalDietaryReferenceIntakeValue>? PersonalDietaryReferenceIntakes { get; set; }

        public EzNutritionDbContext(DbContextOptions<EzNutritionDbContext> options) : base(options) { }
    }
}
