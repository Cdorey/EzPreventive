using System.ComponentModel.DataAnnotations;
using System.Resources;

namespace EzNutrition.Shared.Data.DietaryRecallSurvey
{
    public enum MealOccasion
    {
        [Display(Name ="早餐")]
        Breakfast = 1,
        [Display(Name = "上午")]
        MorningSnack = 2,
        [Display(Name = "午餐")]
        Lunch = 4,
        [Display(Name = "下午")]
        AfternoonSnack = 8,
        [Display(Name = "晚餐")]
        Dinner = 16,
        [Display(Name = "宵夜")]
        LateNightSnack = 32,
    }
}
