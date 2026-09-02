namespace EzNutrition.Domain.Dietary
{
    public class TowerLayer
    {
        public required string LayerName { get; set; }

        public string? StandardTowerValue { get; set; }

        public string? DietaryRecallTower { get; set; }

        public List<TowerLayer>? Children { get; set; }

        public TowerLayer() { }
    }
}