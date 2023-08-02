namespace EzNutrition.Server.Data.Repositories
{
    public class AdviceRepository
    {
        private readonly EzNutritionDbContext dbContext;

        public List<IGrouping<int, string>> GetAdviceByDiseaseID(List<int> diseaseIDs)
        {
            var advices = from d in dbContext.Advices
                          where d.Diseases != null && d.Diseases.Any(x => diseaseIDs.Contains(x.DiseaseId))
                          group d.Content by d.Priority;
            return advices.OrderBy(x => x.Key).ToList();
        }

        public AdviceRepository(EzNutritionDbContext dbContext)
        {
            this.dbContext = dbContext;
        }
    }
}
