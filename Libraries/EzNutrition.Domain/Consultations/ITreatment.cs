namespace EzNutrition.Domain.Consultations
{
    public interface ITreatment
    {
        IClient Client { get; }

        string[] Requirements { get; }
    }
}