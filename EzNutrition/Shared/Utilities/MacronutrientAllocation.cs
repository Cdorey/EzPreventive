namespace EzNutrition.Shared.Utilities
{
    public class MacronutrientAllocation
    {
        public double BreakfastProteinCalorie { get; set; }
        public double BreakfastCarbohydrateCalorie { get; set; }
        public double BreakfastFatCalorie { get; set; }

        public double LunchProteinCalorie { get; set; }
        public double LunchCarbohydrateCalorie { get; set; }
        public double LunchFatCalorie { get; set; }

        public double DinnerProteinCalorie { get; set; }
        public double DinnerCarbohydrateCalorie { get; set; }
        public double DinnerFatCalorie { get; set; }



        public double BreakfastProteinContent => Math.Round(BreakfastProteinCalorie / 4);
        public double BreakfastCarbohydrateContent => Math.Round(BreakfastCarbohydrateCalorie / 4);
        public double BreakfastFatContent => Math.Round(BreakfastFatCalorie / 9);

        public double LunchProteinContent => Math.Round(LunchProteinCalorie / 4);
        public double LunchCarbohydrateContent => Math.Round(LunchCarbohydrateCalorie / 4);
        public double LunchFatContent => Math.Round(LunchFatCalorie / 9);

        public double DinnerProteinContent => Math.Round(DinnerProteinCalorie / 4);
        public double DinnerCarbohydrateContent => Math.Round(DinnerCarbohydrateCalorie / 4);
        public double DinnerFatContent => Math.Round(DinnerFatCalorie / 9);

        public double BreakfastProteinPortions => Math.Round(BreakfastProteinCalorie * 2 / 90) / 2;
        public double BreakfastCarbohydratePortions => Math.Round(BreakfastCarbohydrateCalorie * 2 / 90) / 2;
        public double BreakfastFatPortions => Math.Round(BreakfastFatCalorie * 2 / 90) / 2;

        public double LunchProteinPortions => Math.Round(LunchProteinCalorie * 2 / 90) / 2;
        public double LunchCarbohydratePortions => Math.Round(LunchCarbohydrateCalorie * 2 / 90) / 2;
        public double LunchFatPortionst => Math.Round(LunchFatCalorie * 2 / 90) / 2;

        public double DinnerProteinPortions => Math.Round(DinnerProteinCalorie * 2 / 90) / 2;
        public double DinnerCarbohydratePortions => Math.Round(DinnerCarbohydrateCalorie * 2 / 90) / 2;
        public double DinnerFatPortions => Math.Round(DinnerFatCalorie * 2 / 90) / 2;

        public double TotalProteinContent => BreakfastProteinContent + LunchProteinContent + DinnerProteinContent;
        public double TotalCarbohydrateContent => BreakfastCarbohydrateContent + LunchCarbohydrateContent + DinnerCarbohydrateContent;
        public double TotalFatContent => BreakfastFatContent + LunchFatContent + DinnerFatContent;

        public MacronutrientAllocation() { }

        public MacronutrientAllocation(int energy)
        {
            BreakfastProteinCalorie = Math.Round(energy * 0.3 * 0.15);
            BreakfastCarbohydrateCalorie = Math.Round(energy * 0.3 * 0.6);
            BreakfastFatCalorie = Math.Round(energy * 0.3 * 0.25);

            LunchProteinCalorie = Math.Round(energy * 0.4 * 0.15);
            LunchCarbohydrateCalorie = Math.Round(energy * 0.4 * 0.6);
            LunchFatCalorie = Math.Round(energy * 0.4 * 0.25);

            DinnerProteinCalorie = Math.Round(energy * 0.3 * 0.15);
            DinnerCarbohydrateCalorie = Math.Round(energy * 0.3 * 0.6);
            DinnerFatCalorie = Math.Round(energy * 0.3 * 0.25);
        }
    }
}
