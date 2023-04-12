using EzNutrition.Server.Data.Entities;

namespace EzNutrition.Server.Data.Repositories
{
    /// <summary>
    /// 膳食参考摄入量表
    /// </summary>
    public class PersonalModelRespository
    {
        private readonly EzNutritionDbContext dbContext;

        public Person GetPersonalModelByConditions()
        {
            throw new NotImplementedException();
        }

        public Person GetPersonalModelById(Guid id)
        {
            throw new NotImplementedException();
        }

        public Person CreatePersonalModelByConditions()
        {
            throw new NotImplementedException();
        }

        public PersonalDietaryReferenceIntakeValue AddDrisRecord()
        {
            throw new NotImplementedException();
        }

        public PersonalDietaryReferenceIntakeValue UpdateDrisRecord()
        {
            throw new NotImplementedException();
        }

        public PersonalModelRespository(EzNutritionDbContext dbContext)
        {
            this.dbContext = dbContext;
        }
    }
}
