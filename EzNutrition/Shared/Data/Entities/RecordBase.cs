using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace EzNutrition.Shared.Data.Entities
{
    public abstract class RecordBase
    {

        public string? Details { get; set; }

        [Required(ErrorMessage = $"{nameof(FriendlyName)}is required")]
        [StringLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string? FriendlyName { get; set; }
    }

}
