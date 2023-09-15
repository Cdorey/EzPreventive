using System.ComponentModel.DataAnnotations.Schema;

namespace EzNutrition.Shared.Data.Entities
{
    public class MultiDerivedPersonRelationship
    {
        public Guid MultiDerivedPersonRelationshipId { get; set; }

        [ForeignKey(nameof(Person))]
        public Guid? ChildModelId { get; set; }
        public Person? ChildModel { get; set; }

        [ForeignKey(nameof(Person))]
        public Guid? ParentModelId { get; set; }
        public Person? ParentModel { get; set; }
    }

}
