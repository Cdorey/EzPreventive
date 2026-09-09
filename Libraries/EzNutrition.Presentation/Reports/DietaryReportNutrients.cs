using System.Globalization;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Domain.Dietary;

namespace EzNutrition.Presentation.Reports;

internal sealed record DietaryReportNutrientRow(string Name, string Amount, string Marker,
    string ReferenceRange, string ReferenceTypes, bool Indented);

internal sealed record DietaryReportNutrientGroup(string Title, IReadOnlyList<DietaryReportNutrientRow> Rows);

/// <summary>组织纸面营养素顺序、分项层级与参考值文字；比较结论采用已有核算结果。</summary>
internal static class DietaryReportNutrients
{
    private sealed record Position(int Group, int Order, string? Parent = null);

    private static readonly string[] GroupNames = ["宏量营养素与能量", "矿物质", "维生素", "水", "其他成分"];
    private static readonly Dictionary<string, Position> Positions = CreatePositions();

    public static string CompositionName(string name) => name switch
    {
        "总能量" => "能量", "总脂肪" => "脂肪", "维生素B1" => "硫胺素", "维生素B2" => "核黄素", _ => name
    };

    public static IReadOnlyList<DietaryReportNutrientGroup> Create(
        IReadOnlyList<DietaryNutrientAssessment> assessments, IReadOnlyList<NutrientAmount> totals)
    {
        var amounts = totals.ToDictionary(value => value.Nutrient.Display ?? value.Nutrient.Code);
        var represented = assessments.Select(value => CompositionName(value.FriendlyName)).ToHashSet();
        var rows = assessments.Select(value =>
        {
            var unit = value.Unit;
            if (string.IsNullOrWhiteSpace(unit) && amounts.TryGetValue(CompositionName(value.FriendlyName), out var total))
                unit = total.Amount.Unit.Display ?? total.Amount.Unit.Code;
            var lower = value.LowerReference;
            var upper = value.UpperReference;
            return new DietaryReportNutrientRow(value.FriendlyName, Format(value.Value, unit),
                value.ReferenceStatus switch
                {
                    DietaryReferenceStatus.BelowRange => "↓", DietaryReferenceStatus.AboveRange => "↑", _ => ""
                },
                lower is not null && upper is not null ? $"{Reference(lower)}-{Reference(upper)}"
                    : lower is not null ? $"≥{Reference(lower)}" : upper is not null ? $"≤{Reference(upper)}" : "—",
                value.HasReference ? $"{lower?.Type}-{upper?.Type}" : "—", false);
        }).Concat(totals.Where(value => !represented.Contains(value.Nutrient.Display ?? value.Nutrient.Code))
            .Select(value => new DietaryReportNutrientRow(value.Nutrient.Display ?? value.Nutrient.Code,
                Format(value.Amount.Value, value.Amount.Unit.Display ?? value.Amount.Unit.Code), "", "—", "—", false))).ToArray();
        var names = rows.Select(row => Key(row.Name)).ToHashSet();
        return rows.Select(row => (Row: row, Position: Locate(row.Name)))
            .OrderBy(item => item.Position.Group).ThenBy(item => item.Position.Order)
            .ThenBy(item => item.Row.Name, StringComparer.Ordinal)
            .GroupBy(item => item.Position.Group)
            .Select(group => new DietaryReportNutrientGroup(GroupNames[group.Key], group.Select(item => item.Row with
            {
                Indented = item.Position.Parent is { } parent && names.Contains(Key(parent))
            }).ToArray())).ToArray();
    }

    private static string Reference(DietaryNutrientReference? value) => value is null ? "" : Format(value.Value, value.Unit);
    private static string Format(decimal value, string unit) => value.ToString("0.##", CultureInfo.InvariantCulture)
        + (string.IsNullOrWhiteSpace(unit) ? "（单位未记录）" : " " + (unit.Equals("kcal", StringComparison.OrdinalIgnoreCase) ? "kcal" : unit));

    private static string Key(string name) => CompositionName(name).Replace(" ", "").Replace("-", "")
        .Replace("总维生素", "维生素").Replace("总膳食纤维", "膳食纤维")
        .Replace("α", "alpha").Replace("β", "beta").Replace("γ", "gamma").Replace("δ", "delta").ToLowerInvariant();

    private static Position Locate(string name)
    {
        if (Positions.TryGetValue(Key(name), out var position)) return position;
        // 保留资料中的分项名称；未登记的生育酚分项仍随总维生素 E 展示。
        if (name.Contains("生育酚", StringComparison.Ordinal) || name.Contains("tocopherol", StringComparison.OrdinalIgnoreCase))
            return new(2, 83, "总维生素E");
        if (name.Contains("维生素", StringComparison.Ordinal)) return new(2, 100);
        return new(4, 0);
    }

    private static Dictionary<string, Position> CreatePositions()
    {
        var result = new Dictionary<string, Position>();
        void Add(int group, int order, string? parent, params string[] names)
        {
            foreach (var name in names) result[Key(name)] = new(group, order, parent);
        }
        Add(0, 0, null, "总能量");
        Add(0, 10, null, "蛋白质");
        Add(0, 11, "蛋白质", "蛋白质供能比");
        Add(0, 20, null, "总脂肪");
        Add(0, 21, "总脂肪", "脂肪供能比");
        Add(0, 22, "总脂肪", "饱和脂肪酸", "总饱和脂肪酸");
        Add(0, 23, "总脂肪", "单不饱和脂肪酸", "总单不饱和脂肪酸");
        Add(0, 24, "总脂肪", "多不饱和脂肪酸", "总多不饱和脂肪酸");
        Add(0, 30, null, "碳水化合物");
        Add(0, 31, "碳水化合物", "碳水化合物供能比");
        Add(0, 40, null, "膳食纤维", "总膳食纤维");
        Add(0, 41, "膳食纤维", "可溶性膳食纤维", "不溶性膳食纤维");
        Add(0, 50, null, "胆固醇");
        var minerals = new[] { "钙", "磷", "钾", "钠", "镁", "铁", "锌", "硒", "铜", "锰", "碘", "铬", "钼", "氟", "氯" };
        for (var index = 0; index < minerals.Length; index++) Add(1, index, null, minerals[index]);
        Add(2, 0, null, "总维生素A", "维生素A");
        Add(2, 1, "总维生素A", "视黄醇");
        Add(2, 2, "总维生素A", "胡萝卜素", "β胡萝卜素");
        Add(2, 10, null, "维生素B1");
        Add(2, 20, null, "维生素B2");
        Add(2, 30, null, "烟酸", "维生素B3");
        Add(2, 40, null, "泛酸", "维生素B5");
        Add(2, 50, null, "维生素B6");
        Add(2, 55, null, "生物素", "维生素B7");
        Add(2, 60, null, "叶酸", "维生素B9");
        Add(2, 65, null, "维生素B12");
        Add(2, 70, null, "维生素C");
        Add(2, 75, null, "维生素D", "总维生素D");
        Add(2, 80, null, "总维生素E", "维生素E");
        Add(2, 81, "总维生素E", "α生育酚", "alpha生育酚", "alpha-tocopherol");
        Add(2, 82, "总维生素E", "β生育酚", "beta生育酚");
        Add(2, 83, "总维生素E", "γ生育酚", "gamma生育酚");
        Add(2, 84, "总维生素E", "δ生育酚", "delta生育酚");
        Add(2, 90, null, "维生素K");
        Add(3, 0, null, "水", "水分");
        return result;
    }
}
