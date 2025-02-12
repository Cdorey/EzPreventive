using EzNutrition.Shared.Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EzNutrition.Shared.Data.DietaryRecallSurvey
{
    public class DietaryRecallEntry()
    {
        public required Food Food { get; set; }

        public required decimal Weight { get; set; }

        public MealOccasion MealOccasion { get; set; } = MealOccasion.Breakfast;

        public bool IsAllEdible { get; set; } = true;
    }
}
