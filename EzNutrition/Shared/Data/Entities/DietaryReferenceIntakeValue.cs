using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace EzNutrition.Shared.Data.Entities
{
    public class DietaryReferenceIntakeValue
    {
        public int DietaryReferenceIntakeValueId { get; set; }

        public decimal? AgeStart { get; set; }

        public string? Gender { get; set; }

        public string? SpecialPhysiologicalPeriod { get; set; }

        [Required(ErrorMessage = $"{nameof(Nutrient)}is required")]
        [StringLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string? Nutrient { get; set; }

        public DietaryReferenceIntakeType RecordType { get; set; }

        public bool IsOffset { get; set; } = false;

        public decimal Value { get; set; }

        [Required(ErrorMessage = $"{nameof(MeasureUnit)}is required")]
        [StringLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string? MeasureUnit { get; set; }

        public string? Detail { get; set; }

        public override string? ToString()
        {
            return $"{((Value % 1 == 0) ? (int)Value : Value)} {MeasureUnit} " + (RecordType == DietaryReferenceIntakeType.AI ? "(AI)" : "");
        }
    }
}
