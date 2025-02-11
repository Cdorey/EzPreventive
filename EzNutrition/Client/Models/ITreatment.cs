using System.Net.Http;

namespace EzNutrition.Client.Models
{
    public interface ITreatment
    {
        IClient Client { get; }

        string[] Requirements { get; }
    }
}
