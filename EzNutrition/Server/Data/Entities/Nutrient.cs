using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EzNutrition.Server.Data.Entities
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
