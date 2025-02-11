namespace EzNutrition.Shared.Utilities
{
    public class FoodExchangeAllocation(MacronutrientAllocation macroAlloc)
    {
        private double TotalProteinPortions => macroAlloc.BreakfastProteinPortions + macroAlloc.LunchProteinPortions + macroAlloc.DinnerProteinPortions;

        private double TotalCarbohydratePortions => macroAlloc.BreakfastCarbohydratePortions + macroAlloc.LunchCarbohydratePortions + macroAlloc.DinnerCarbohydratePortions;

        private double TotalFatPortions => macroAlloc.BreakfastFatPortions + macroAlloc.LunchFatPortionst + macroAlloc.DinnerFatPortions;

        public double GrainsAndStarchyFoods => TotalCarbohydratePortions - Vegetables - Fruits;

        public double Fruits { get; set; } = 1;

        public double Vegetables { get; set; } = 1;

        public double MeatsAndEggs => TotalProteinPortions + TotalFatPortions - LegumesAndDairyAlternatives - EnergyFoodsOrFats;

        public double LegumesAndDairyAlternatives { get; set; } = 2;

        public double EnergyFoodsOrFats { get; set; } = 2;
    }
}
