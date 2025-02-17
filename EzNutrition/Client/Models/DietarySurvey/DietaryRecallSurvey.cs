using EzNutrition.Shared.Data.DietaryRecallSurvey;
using EzNutrition.Shared.Data.Entities;
using System.Data;
using System.Text.Json.Serialization;

namespace EzNutrition.Client.Models.DietarySurvey
{
    public class DietaryRecallSurvey(IClient client, IEnumerable<Food> foods, IEnumerable<Nutrient> nutrients, DRIs dRIs) : ITreatment
    {
        private void GenerateSummaryRows()
        {
            if (SummaryCalculationTable is null)
            {
                return;
            }

            SummaryRows.Clear();
            var energy = new DietarySurveySummaryRow
            {
                Abbreviation = "E",
                FriendlyName = "总能量",
                ValueString = SummaryCalculationTable.TotalEnergy.ToString("0") ?? "0",
                Unit = "kCal",
                Expandable = true
            };

            var breakFastEnergy = SummaryCalculationTable[MealOccasion.Breakfast].FirstOrDefault(x => x.Nutrient?.FriendlyName == "能量")?.Value ?? 0;
            var breakFastPercentage = Math.Round(((breakFastEnergy / SummaryCalculationTable.TotalEnergy * 100)), 0).ToString("0");

            var morningSnackEnergy = SummaryCalculationTable[MealOccasion.MorningSnack].FirstOrDefault(x => x.Nutrient?.FriendlyName == "能量")?.Value ?? 0;
            var morningSnackPercentage = Math.Round(((morningSnackEnergy / SummaryCalculationTable.TotalEnergy * 100)), 0).ToString("0");

            var lunchEnergy = SummaryCalculationTable[MealOccasion.Lunch].FirstOrDefault(x => x.Nutrient?.FriendlyName == "能量")?.Value ?? 0;
            var lunchPercentage = Math.Round(((lunchEnergy / SummaryCalculationTable.TotalEnergy * 100)), 0).ToString("0");

            var afternoonSnackEnergy = SummaryCalculationTable[MealOccasion.AfternoonSnack].FirstOrDefault(x => x.Nutrient?.FriendlyName == "能量")?.Value ?? 0;
            var afternoonSnackPercentage = Math.Round(((afternoonSnackEnergy / SummaryCalculationTable.TotalEnergy * 100)), 0).ToString("0");

            var dinnerEnergy = SummaryCalculationTable[MealOccasion.Dinner].FirstOrDefault(x => x.Nutrient?.FriendlyName == "能量")?.Value ?? 0;
            var dinnerPercentage = Math.Round(((dinnerEnergy / SummaryCalculationTable.TotalEnergy * 100)), 0).ToString("0");

            var lateNightSnackEnergy = SummaryCalculationTable[MealOccasion.LateNightSnack].FirstOrDefault(x => x.Nutrient?.FriendlyName == "能量")?.Value ?? 0;
            var lateNightSnackPercentage = Math.Round(((lateNightSnackEnergy / SummaryCalculationTable.TotalEnergy * 100)), 0).ToString("0");


            energy.ExpendDescriptions = [("早餐供能", $"{breakFastEnergy}kCal，{breakFastPercentage}%E"), ("上午供能", $"{morningSnackEnergy}kCal，{morningSnackPercentage}%E"), ("午餐供能", $"{lunchEnergy}kCal，{lunchPercentage}%E"), ("下午供能", $"{afternoonSnackEnergy}kCal，{afternoonSnackPercentage}%E"), ("晚餐供能", $"{dinnerEnergy}kCal，{dinnerPercentage}%E"), ("宵夜供能", $"{lateNightSnackEnergy}kCal，{lateNightSnackPercentage}%E")];
            SummaryRows.Add(energy);

            //蛋白质
            var proteinDris = DRIs.NutrientRanges.FirstOrDefault(x => x.Nutrient == "蛋白质");
            var proteinValue = SummaryCalculationTable["蛋白质"];
            var protein = new DietarySurveySummaryRow
            {
                FriendlyName = "蛋白质",
                ValueString = proteinValue.ToString("0.00"),
                Unit = "g",
                ReferenceRange = $"{proteinDris?.RNI?.Value.ToString("0.00") ?? string.Empty}~{proteinDris?.UL?.Value.ToString("0.00") ?? string.Empty}",
                Flag = CompareWithDris(proteinValue, proteinDris),
                Expandable = true,
                ExpendTitle = "蛋白质供应顺位",
                ExpendDescriptions = SummaryCalculationTable.ProteinRank.Select((x) => { return (x.Food?.FriendlyName ?? string.Empty, x.Value.ToString("0.00") + "g"); }).ToArray()
            };

            SummaryRows.Add(protein);

            var proteinPercentage = Math.Round(((SummaryCalculationTable.ProteinEnergy / SummaryCalculationTable.TotalEnergy * 100)), 0);
            var proteinAmdrL = proteinDris?.OtherRecords.FirstOrDefault(x => x.RecordType == DietaryReferenceIntakeType.AMDR_L);
            var proteinAmdrH = proteinDris?.OtherRecords.FirstOrDefault(x => x.RecordType == DietaryReferenceIntakeType.AMDR_H);
            SummaryRows.Add(new DietarySurveySummaryRow
            {
                FriendlyName = "蛋白质供能比",
                ValueString = proteinPercentage.ToString("0"),
                Unit = "%E",
                ReferenceRange = $"{proteinAmdrL?.Value.ToString("0.00") ?? string.Empty}~{proteinAmdrH?.Value.ToString("0.00") ?? string.Empty}",
                Flag = CompareWithDris(proteinPercentage, proteinAmdrL, proteinAmdrH)
            });

            //脂肪
            var fatDris = DRIs.NutrientRanges.FirstOrDefault(x => x.Nutrient == "总脂肪");
            var fatValue = SummaryCalculationTable["脂肪"];
            var fat = new DietarySurveySummaryRow
            {
                FriendlyName = "总脂肪",
                ValueString = fatValue.ToString("0.00"),
                Unit = "g",
                ReferenceRange = $"{fatDris?.RNI?.Value.ToString("0.00") ?? string.Empty}~{fatDris?.UL?.Value.ToString("0.00") ?? string.Empty}",
                Flag = CompareWithDris(fatValue, fatDris),
                Expandable = true,
                ExpendTitle = "脂肪供应顺位",
                ExpendDescriptions = SummaryCalculationTable.FatRank.Select((x) => { return (x.Food?.FriendlyName ?? string.Empty, x.Value.ToString("0.00") + "g"); }).ToArray()
            };

            SummaryRows.Add(fat);

            var fatPercentage = Math.Round(((SummaryCalculationTable.FatEnergy / SummaryCalculationTable.TotalEnergy * 100)), 0);
            var fatAmdrL = fatDris?.OtherRecords.FirstOrDefault(x => x.RecordType == DietaryReferenceIntakeType.AMDR_L);
            var fatAmdrH = fatDris?.OtherRecords.FirstOrDefault(x => x.RecordType == DietaryReferenceIntakeType.AMDR_H);
            SummaryRows.Add(new DietarySurveySummaryRow
            {
                FriendlyName = "脂肪供能比",
                ValueString = fatPercentage.ToString("0"),
                Unit = "%E",
                ReferenceRange = $"{fatAmdrL?.Value.ToString("0.00") ?? string.Empty}~{fatAmdrH?.Value.ToString("0.00") ?? string.Empty}",
                Flag = CompareWithDris(fatPercentage, fatAmdrL, fatAmdrH)
            });

            //碳水化合物
            var carbohydrateDris = DRIs.NutrientRanges.FirstOrDefault(x => x.Nutrient == "碳水化合物");
            var carbohydrateValue = SummaryCalculationTable["碳水化合物"];
            var carbohydrate = new DietarySurveySummaryRow
            {
                FriendlyName = "碳水化合物",
                ValueString = carbohydrateValue.ToString("0.00"),
                Unit = "g",
                ReferenceRange = $"{carbohydrateDris?.RNI?.Value.ToString("0.00") ?? string.Empty}~{carbohydrateDris?.UL?.Value.ToString("0.00") ?? string.Empty}",
                Flag = CompareWithDris(carbohydrateValue, carbohydrateDris),
                Expandable = true,
                ExpendTitle = "碳水化合物供应顺位",
                ExpendDescriptions = SummaryCalculationTable.CarbohydrateRank.Select((x) => { return (x.Food?.FriendlyName ?? string.Empty, x.Value.ToString("0.00") + "g"); }).ToArray()
            };

            SummaryRows.Add(carbohydrate);

            var carbohydratePercentage = Math.Round(((SummaryCalculationTable.CarbohydrateEnergy / SummaryCalculationTable.TotalEnergy * 100)), 0);
            var carbohydrateAmdrL = carbohydrateDris?.OtherRecords.FirstOrDefault(x => x.RecordType == DietaryReferenceIntakeType.AMDR_L);
            var carbohydrateAmdrH = carbohydrateDris?.OtherRecords.FirstOrDefault(x => x.RecordType == DietaryReferenceIntakeType.AMDR_H);
            SummaryRows.Add(new DietarySurveySummaryRow
            {
                FriendlyName = "碳水化合物供能比",
                ValueString = carbohydratePercentage.ToString("0"),
                Unit = "%E",
                ReferenceRange = $"{carbohydrateAmdrL?.Value.ToString("0.00") ?? string.Empty}~{carbohydrateAmdrH?.Value.ToString("0.00") ?? string.Empty}",
                Flag = CompareWithDris(carbohydratePercentage, carbohydrateAmdrL, carbohydrateAmdrH)
            });

            //矿物质
            SummaryRows.Add(GenerateSummaryRow("钾", "K"));
            SummaryRows.Add(GenerateSummaryRow("钠", "Na"));
            SummaryRows.Add(GenerateSummaryRow("镁", "Mg"));
            SummaryRows.Add(GenerateSummaryRow("铁", "Fe"));
            SummaryRows.Add(GenerateSummaryRow("锰", "Mn"));
            SummaryRows.Add(GenerateSummaryRow("锌", "Zn"));
            SummaryRows.Add(GenerateSummaryRow("磷", "P"));
            SummaryRows.Add(GenerateSummaryRow("硒", "Se"));
            SummaryRows.Add(GenerateSummaryRow("铜", "Cu"));

            //维生素
            //VitA只能自己写
            //totalVitA有RNI
            //视黄醇有UL
            //类胡萝卜素没有基础DRIs
            var vitADris = DRIs.NutrientRanges.FirstOrDefault(x => x.Nutrient == "VitA");
            var totalVitA = SummaryCalculationTable["总维生素A"];
            SummaryRows.Add(new DietarySurveySummaryRow
            {
                Abbreviation = "VitA",
                FriendlyName = "总维生素A",
                ValueString = totalVitA.ToString("0.00"),
                Unit = vitADris?.RNI?.MeasureUnit ?? string.Empty,
                ReferenceRange = $"{vitADris?.RNI?.Value.ToString("0") ?? string.Empty}~",
                Flag = CompareWithDris(totalVitA, vitADris?.RNI, null)
            });
            var retinol = SummaryCalculationTable["视黄醇"];
            SummaryRows.Add(new DietarySurveySummaryRow
            {
                FriendlyName = "视黄醇",
                ValueString = retinol.ToString("0.00"),
                Unit = vitADris?.UL?.MeasureUnit ?? string.Empty,
                ReferenceRange = $"~{vitADris?.UL?.Value.ToString("0") ?? string.Empty}",
                Flag = CompareWithDris(retinol, null, vitADris?.UL)
            });
            SummaryRows.Add(GenerateSummaryRow("胡萝卜素"));
            //VitB
            SummaryRows.Add(GenerateSummaryRow("维生素B1", "VitB1", "硫胺素", "VitB1"));
            SummaryRows.Add(GenerateSummaryRow("维生素B2", "VitB2", "核黄素", "VitB2"));
            var niacin = GenerateSummaryRow("烟酸", "VitB3");
            niacin.Unit = "mg";
            SummaryRows.Add(niacin);
            SummaryRows.Add(GenerateSummaryRow("维生素C", "VitC", null, "VitC"));
            SummaryRows.Add(GenerateSummaryRow("总维生素E", "VitE", null, "VitE"));
        }

        private static string? CompareWithDris(decimal actualValue,
                                               IDietaryReferenceIntakeValue? lowerThan,
                                               IDietaryReferenceIntakeValue? higherThan)
        {
            string? result = null;
            if (lowerThan is not null && actualValue < lowerThan.Value)
            {
                result = Flags.Lower;
            }
            else if (higherThan is not null && actualValue > higherThan.Value)
            {
                result = Flags.Higher;
            }
            return result;
        }

        private static string? CompareWithDris(decimal actualValue, NutrientRange? range) => CompareWithDris(actualValue, range?.RNI, range?.UL);

        private DietarySurveySummaryRow GenerateSummaryRow(string friendlyName,
                                                           string? abbreviation = null,
                                                           string? friendlyNameInFoodComposition = null,
                                                           string? friendlyNameInDRIs = null)
        {
            ArgumentNullException.ThrowIfNull(SummaryCalculationTable);

            var dris = DRIs.NutrientRanges.FirstOrDefault(x => x.Nutrient == (friendlyNameInDRIs ?? friendlyName));
            var actualValue = SummaryCalculationTable[(friendlyNameInFoodComposition ?? friendlyName)];
            var rni = string.Empty;
            var rniValue = dris?.RNI?.Value;
            if (rniValue is not null)
            {
                rni = ((rniValue % 1 == 0) ? (int)rniValue : rniValue).ToString();
            }
            var ul = string.Empty;
            var ulValue = dris?.UL?.Value;
            if (ulValue is not null)
            {
                ul = ((ulValue % 1 == 0) ? (int)ulValue : ulValue).ToString();
            }
            return new DietarySurveySummaryRow
            {
                Abbreviation = abbreviation ?? string.Empty,
                FriendlyName = friendlyName,
                ValueString = actualValue.ToString("0.00"),
                Unit = dris?.RNI?.MeasureUnit ?? string.Empty,
                ReferenceRange = $"{rni}~{ul}",
                Flag = CompareWithDris(actualValue, dris)
            };

        }

        public event EventHandler<EventArgs>? OnCalculate;

        [JsonIgnore]
        public string[] Requirements { get; } = [];

        public IClient Client => client;

        [JsonIgnore]
        public IEnumerable<Food> Foods => foods;

        [JsonIgnore]
        public IEnumerable<Nutrient> Nutrients => nutrients;

        [JsonIgnore]
        public DRIs DRIs => dRIs;

        public List<DietaryRecallEntry> RecallEntries { get; } = [];

        public SummaryCalculationTable? SummaryCalculationTable { get; set; }

        public async Task CalculateAsync()
        {
            await Task.Run(async () =>
            {
                SummaryCalculationTable = new SummaryCalculationTable(RecallEntries, Nutrients.ToList());
                GenerateSummaryRows();
                CalculateProgress = await SummaryCalculationTable.ToCalculateDataTableAsync();
                OnCalculate?.Invoke(this, EventArgs.Empty);
            });
        }

        public DataTable? CalculateProgress { get; private set; }

        public List<DietarySurveySummaryRow> SummaryRows = [];
    }
}