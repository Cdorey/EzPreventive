using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EzNutrition.Server.Data.Entities
{
    public class PersonalDietaryReferenceIntakeValue
    {
        public int PersonalDietaryReferenceIntakeValueId { get; set; }

        public Guid PersonId { get; set; }
        public Person? Person { get; set; }

        public int NutrientId { get; set; }
        public Nutrient? Nutrient { get; set; }

        [Required]
        public decimal Value { get; set; }

        [StringLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string? MeasureUnit { get; set; }

    }
}
