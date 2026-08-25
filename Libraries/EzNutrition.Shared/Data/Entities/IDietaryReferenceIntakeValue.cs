namespace EzNutrition.Shared.Data.Entities
{
    public interface IDietaryReferenceIntakeValue
    {
        string? MeasureUnit { get; }
        string? Nutrient { get; }
        DietaryReferenceIntakeType RecordType { get; }
        decimal Value { get; }
    }
}