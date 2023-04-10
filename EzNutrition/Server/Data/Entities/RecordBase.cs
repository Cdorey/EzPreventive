using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EzNutrition.Server.Data.Entities
{
    public class RecordBase
    {

        public string? Details { get; set; }

        [Required(ErrorMessage = $"{nameof(FriendlyName)}is required")]
        [StringLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string? FriendlyName { get; set; }
    }
}