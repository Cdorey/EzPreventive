namespace EzNutrition.Shared.Utilities
{
    public class MacronutrientAllocation
    {
        public int TotalEnergy { get; private set; }

        public double ProteinPercentage { get; set; }

        public double CarbohydratePercentage => 1 - ProteinPercentage - FatPercentage;

        public double FatPercentage { get; set; }

        public double BreakfastProteinCalorie => Math.Round(TotalEnergy * 0.3 * ProteinPercentage);
        public double BreakfastCarbohydrateCalorie => Math.Round(TotalEnergy * 0.3 * CarbohydratePercentage);
        public double BreakfastFatCalorie => Math.Round(TotalEnergy * 0.3 * FatPercentage);

        public double LunchProteinCalorie => Math.Round(TotalEnergy * 0.4 * ProteinPercentage);
        public double LunchCarbohydrateCalorie => Math.Round(TotalEnergy * 0.4 * CarbohydratePercentage);
        public double LunchFatCalorie => Math.Round(TotalEnergy * 0.4 * FatPercentage);

        public double DinnerProteinCalorie => Math.Round(TotalEnergy * 0.3 * ProteinPercentage);
        public double DinnerCarbohydrateCalorie => Math.Round(TotalEnergy * 0.3 * CarbohydratePercentage);
        public double DinnerFatCalorie => Math.Round(TotalEnergy * 0.3 * FatPercentage);



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

        public MacronutrientAllocation(int energy, double proteinPercentage = 0.15d, double fatPercentage = 0.25d)
        {
            TotalEnergy = energy;
            ProteinPercentage = proteinPercentage;
            FatPercentage = fatPercentage;
        }
    }
}
