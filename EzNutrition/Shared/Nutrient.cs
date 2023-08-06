using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace EzNutrition.Shared
{
    public class Nutrient : RecordBase
    {
        public int NutrientId { get; set; }

        [Required(ErrorMessage = $"{nameof(DefaultMeasureUnit)}is required")]
        [StringLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string? DefaultMeasureUnit { get; set; }

        public List<FoodNutrientValue>? FoodNutrientValues { get; set; }
    }

}
