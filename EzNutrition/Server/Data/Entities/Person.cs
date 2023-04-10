using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EzNutrition.Server.Data.Entities
{
    public class Person : RecordBase
    {
        public Guid PersonId { get; set; }

        public decimal? AgeStart { get; set; }

        public decimal? AgeEnd { get; set; }

        public string? Gender { get; set; }

        public string? SpecialPhysiologicalPeriod { get; set; }

        public string? Illness { get; set; }

        public string? Cite { get; set; }

        public string? BodySize { get; set; }

        public string? PhysicalActivityLevel { get; set; }

        /// <summary>
        /// 树形派生导航属性
        /// </summary>
        public Guid? DerivedFromPersonId { get; set; }
        public Person? DerivedFromPerson { get; set; }


        /// <summary>
        /// 菱形派生导航属性
        /// </summary>
        [InverseProperty(nameof(MultiDerivedPersonRelationship.ChildModel))]
        public List<MultiDerivedPersonRelationship>? MultiDerivedFrom { get; set; }

        [InverseProperty(nameof(MultiDerivedPersonRelationship.ParentModel))]
        public List<MultiDerivedPersonRelationship>? MultiDerivedTo { get; set; }
    }
}
