using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace EzNutrition.Shared
{
    public class FoodNutrientValue
    {
        public int FoodNutrientValueId { get; set; }

        [Required]
        public decimal Value { get; set; }

        [StringLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string? MeasureUnit { get; set; }

        public string? Details { get; set; }

        public Guid FoodId { get; set; }
        public Food? Food { get; set; }

        public int NutrientId { get; set; }
        public Nutrient? Nutrient { get; set; }
    }

}
