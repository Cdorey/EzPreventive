namespace EzNutrition.Shared.Data.DietaryRecallSurvey
{
    public abstract class DietaryTower
    {
        protected static string[] LayerName { get; } =
        [
            "盐", "油", "奶及奶制品", "奶类", "大豆及坚果类", "大豆", "坚果", "动物性食品", "蛋类", "畜禽肉鱼类", "畜禽肉", "水产品", "蔬菜类", "水果类", "母乳",
            "谷类", "全谷物和杂豆", "薯类", "水",
        ];

        protected static int[] ParentIndex { get; } = [-1, -1, -1, 2, -1, 4, 4, 0, 7, 7, 9, 9, -1, -1, -1, -1, 15, -1, -1,];

        public abstract TowerLayer[] RenderTower();
    }
}