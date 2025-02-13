using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EzNutrition.Shared.Data.Entities
{
    public class Nutrient : RecordBase
    {
        public int NutrientId { get; set; }

        [Required(ErrorMessage = $"{nameof(DefaultMeasureUnit)}is required")]
        [StringLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string? DefaultMeasureUnit { get; set; }

        [JsonIgnore]
        public List<FoodNutrientValue>? FoodNutrientValues { get; set; }
    }

}
