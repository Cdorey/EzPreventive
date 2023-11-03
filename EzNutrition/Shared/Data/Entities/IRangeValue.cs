namespace EzNutrition.Shared.Data.Entities
{
    public interface IRangeValue : IDietaryReferenceIntakeValue
    {
        public List<DietaryReferenceIntakeValue> InnerRecords { get; }
    }
}