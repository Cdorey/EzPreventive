namespace EzNutrition.Shared.Utilities
{
    public class FoodExchangeAllocation
    {
        private readonly double totalProteinPortions;

        private readonly double totalCarbohydratePortions;

        private readonly double totalFatPortions;

        public double GrainsAndStarchyFoods => totalCarbohydratePortions - Vegetables - Fruits;

        public double Fruits { get; set; }

        public double Vegetables { get; set; }

        public double MeatsAndEggs => totalProteinPortions + totalFatPortions - LegumesAndDairyAlternatives - EnergyFoodsOrFats;

        public double LegumesAndDairyAlternatives { get; set; }

        public double EnergyFoodsOrFats { get; set; }

        public FoodExchangeAllocation() { }

        public FoodExchangeAllocation(MacronutrientAllocation macroAlloc)
        {
            totalProteinPortions = macroAlloc.BreakfastProteinPortions + macroAlloc.LunchProteinPortions + macroAlloc.DinnerProteinPortions;
            totalCarbohydratePortions = macroAlloc.BreakfastCarbohydratePortions + macroAlloc.LunchCarbohydratePortions + macroAlloc.DinnerCarbohydratePortions;
            totalFatPortions = macroAlloc.BreakfastFatPortions + macroAlloc.LunchFatPortionst + macroAlloc.DinnerFatPortions;
            Vegetables = 1;
            Fruits = 1;
            LegumesAndDairyAlternatives = 2;
            EnergyFoodsOrFats = 2;
        }
    }
}
