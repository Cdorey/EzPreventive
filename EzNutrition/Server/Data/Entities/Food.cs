using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EzNutrition.Server.Data.Entities
{
    public class Food : RecordBase
    {
        public Guid FoodId { get; set; }

        [Required(ErrorMessage = $"{nameof(FriendlyCode)}is required")]
        [StringLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string? FriendlyCode { get; set; }

        public string? Cite { get; set; }

        public List<FoodNutrientValue>? FoodNutrientValues { get; set; }
    }
}
