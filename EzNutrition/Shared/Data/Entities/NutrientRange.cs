namespace EzNutrition.Shared.Data.Entities
{
    public class NutrientRange
    {
        public NutrientRange(IGrouping<string, DietaryReferenceIntakeValue> innerRecords)
        {
            Nutrient = innerRecords.Key;

            var earQuery = innerRecords.Where(x => x.RecordType == DietaryReferenceIntakeType.EAR);
            EAR = earQuery.Any() ? new RangeValue(earQuery) : default;

            var ulQuery = innerRecords.Where(x => x.RecordType == DietaryReferenceIntakeType.UL);
            UL = ulQuery.Any() ? new RangeValue(ulQuery) : default;

            var rniQuery = innerRecords.Where(x => x.RecordType == DietaryReferenceIntakeType.AI || x.RecordType == DietaryReferenceIntakeType.RNI);
            RNI = rniQuery.Any() ? new RangeValue(rniQuery) : default;
        }

        public string Nutrient { get; }

        public IRangeValue? EAR { get; }

        public IRangeValue? RNI { get; }

        public IRangeValue? UL { get; }


        private class RangeValue : IRangeValue
        {
            private IEnumerable<DietaryReferenceIntakeValue> ConflictAutoHandler(IEnumerable<DietaryReferenceIntakeValue> values)
            {
                //尝试处理多绝对值问题
                var absoluteQuery = values.Where(x => !x.IsOffset);
                var shouldBeResolved = absoluteQuery.Count() == 2 && absoluteQuery.Where(x => x.SpecialPhysiologicalPeriod != null).Count() == 1;
                foreach (var value in values)
                {
                    if (shouldBeResolved && !value.IsOffset && value.SpecialPhysiologicalPeriod == default)
                    {
                        continue;
                    }
                    else
                    {
                        yield return value;
                    }
                }
            }

            public string? MeasureUnit => InnerRecords.First().MeasureUnit;
            public string? Nutrient => InnerRecords.First().Nutrient;
            public DietaryReferenceIntakeType RecordType => InnerRecords.First(x => !x.IsOffset).RecordType;

            public decimal Value
            {
                get
                {
                    if (InnerRecords.Where(x => !x.IsOffset).Count() != 1)
                    {
                        //必须只有一个绝对值时，才能自动核算
                        return default;
                    }
                    else
                    {
                        return InnerRecords.Select(x => x.Value).Sum();
                    }
                }
            }

            public List<DietaryReferenceIntakeValue> InnerRecords { get; }

            public override string? ToString()
            {
                return Value == default ? "数据冲突自动核算失败，需手工核定" : $"{((Value % 1 == 0) ? (int)Value : Value)} {MeasureUnit} " + (RecordType == DietaryReferenceIntakeType.AI ? "(AI)" : "");
            }

            public RangeValue(IEnumerable<DietaryReferenceIntakeValue> innerRecords)
            {
                //参数逻辑检查
                if (!innerRecords.Any())
                    throw new ArgumentException("empty inner record", nameof(innerRecords));

                if (innerRecords.GroupBy(x => x.Nutrient).Count() != 1)
                    throw new ArgumentException("multi different nutrient", nameof(innerRecords));

                if (innerRecords.GroupBy(x => x.MeasureUnit).Count() != 1)
                    throw new ArgumentException("multi different measure unit", nameof(innerRecords));

                if (innerRecords.GroupBy(x => x.RecordType).Count() != 1 && innerRecords.Where(x => x.RecordType != DietaryReferenceIntakeType.AI && x.RecordType != DietaryReferenceIntakeType.RNI).Any())
                    throw new ArgumentException("multi different DRIs type", nameof(innerRecords));

                InnerRecords = ConflictAutoHandler(innerRecords).ToList();
            }
        }
    }
}
