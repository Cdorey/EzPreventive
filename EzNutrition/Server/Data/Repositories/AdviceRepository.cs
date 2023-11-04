using EzNutrition.Shared.Data.Entities;
using static OneOf.Types.TrueFalseOrNull;

namespace EzNutrition.Server.Data.Repositories
{
    public class AdviceRepository
    {
        private readonly EzNutritionDbContext dbContext;

        public List<Advice> GetAdviceByDiseaseID(List<int> diseaseIDs)
        {
#pragma warning disable CS8604 // 引用类型参数可能为 null。
            var advices = from a in dbContext.Advices
                          where a.Diseases.Any(x => diseaseIDs.Contains(x.DiseaseId))
                          orderby a.Priority ascending
                          select a;
#pragma warning restore CS8604 // 引用类型参数可能为 null。
            return advices.ToList();
        }

        public AdviceRepository(EzNutritionDbContext dbContext)
        {
            this.dbContext = dbContext;
        }
    }
}
