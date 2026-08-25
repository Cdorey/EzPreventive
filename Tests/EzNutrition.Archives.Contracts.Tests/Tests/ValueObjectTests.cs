using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Repositories;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.Validation;
using EzNutrition.Archives.Contracts.ValueObjects;
using System.Reflection;

namespace EzNutrition.Archives.Contracts.Tests.Tests;

/// <summary>
/// 验证格式无关值对象已经实现的局部不变量和规范化行为。
/// </summary>
public sealed class ValueObjectTests
{
    private static readonly Uri UcumSystem = new("http://unitsofmeasure.org");

    /// <summary>
    /// 验证所有 UUID 型公开身份都拒绝空 UUID。
    /// </summary>
    [Fact]
    public void Uuid_identifiers_reject_empty_values()
    {
        Assert.Throws<ArgumentException>(() => new ResourceId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new ResourceVersionId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new ArchiveBundleId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new ArchiveChangeSetId(Guid.Empty));
    }

    /// <summary>
    /// 验证强类型身份没有可产生无效实例的公共无参构造入口。
    /// </summary>
    [Fact]
    public void Strong_identifiers_have_no_invalid_default_instances()
    {
        ResourceId? defaultResourceId = default;

        Assert.Null(defaultResourceId);
        Assert.Throws<MissingMethodException>(() => Activator.CreateInstance<ResourceId>());
        Assert.Throws<MissingMethodException>(() => Activator.CreateInstance<ResourceVersionId>());
        Assert.Throws<MissingMethodException>(() => Activator.CreateInstance<RevisionNumber>());
        Assert.Throws<MissingMethodException>(() => Activator.CreateInstance<LocalIdentifier>());
        Assert.Throws<MissingMethodException>(() => Activator.CreateInstance<ArchiveBundleId>());
        Assert.Throws<MissingMethodException>(() => Activator.CreateInstance<ArchiveChangeSetId>());
        Assert.Throws<MissingMethodException>(() => Activator.CreateInstance<ResourceTypeCode>());
    }

    /// <summary>
    /// 验证资源引用在运行时拒绝空身份对象。
    /// </summary>
    [Fact]
    public void Resource_references_reject_null_identifiers()
    {
        var resourceId = new ResourceId(Guid.Parse("4C80CB20-34D2-4C82-92E1-E50D8D0635B6"));
        var versionId = new ResourceVersionId(Guid.Parse("AA0FA202-5D1C-4E58-86B6-D97C14B10506"));

        Assert.Throws<ArgumentNullException>(() => new LogicalResourceReference(null!));
        Assert.Throws<ArgumentNullException>(() => new VersionedResourceReference(null!, versionId));
        Assert.Throws<ArgumentNullException>(() => new VersionedResourceReference(resourceId, null!));
    }

    /// <summary>
    /// 验证修订号从一开始。
    /// </summary>
    [Fact]
    public void Revision_number_must_start_at_one()
    {
        Assert.Equal(1, new RevisionNumber(1).Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => new RevisionNumber(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RevisionNumber(-1));
    }

    /// <summary>
    /// 验证资源类型代码的字符集合和首字符约束。
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("9Patient")]
    [InlineData("Patient_Type")]
    [InlineData("Patient Type")]
    [InlineData("患者")]
    public void Resource_type_code_rejects_unstable_machine_codes(string value)
    {
        Assert.Throws<ArgumentException>(() => new ResourceTypeCode(value));
    }

    /// <summary>
    /// 验证合法资源类型代码会去除首尾空白并保持机器值。
    /// </summary>
    [Fact]
    public void Resource_type_code_normalizes_surrounding_whitespace()
    {
        Assert.Equal("Patient-v2", new ResourceTypeCode("  Patient-v2  ").Value);
    }

    /// <summary>
    /// 验证部分日期不会用虚构月份或日期补齐未知精度。
    /// </summary>
    [Fact]
    public void Partial_date_preserves_year_month_and_day_precision()
    {
        var year = new PartialDate(2025);
        var month = new PartialDate(2025, 7);
        var day = new PartialDate(2025, 7, 16);

        Assert.Equal(PartialDatePrecision.Year, year.Precision);
        Assert.Equal("2025", year.ToString());
        Assert.Null(year.Month);
        Assert.Null(year.Day);

        Assert.Equal(PartialDatePrecision.Month, month.Precision);
        Assert.Equal("2025-07", month.ToString());
        Assert.Null(month.Day);

        Assert.Equal(PartialDatePrecision.Day, day.Precision);
        Assert.Equal("2025-07-16", day.ToString());
    }

    /// <summary>
    /// 验证部分日期拒绝不存在或结构不完整的日期。
    /// </summary>
    [Fact]
    public void Partial_date_rejects_invalid_calendar_values()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PartialDate(0));
        Assert.Throws<ArgumentException>(() => new PartialDate(2025, day: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PartialDate(2025, 2, 29));
        Assert.Equal("2024-02-29", new PartialDate(2024, 2, 29).ToString());
    }

    /// <summary>
    /// 验证时间段不能以早于开始时刻的结束时刻收尾。
    /// </summary>
    [Fact]
    public void Period_rejects_an_end_before_its_start()
    {
        var start = new DateTimeOffset(2025, 1, 1, 10, 0, 0, TimeSpan.FromHours(8));

        Assert.Throws<ArgumentException>(() => new Period(start, start.AddTicks(-1)));
        Assert.Equal(start, new Period(start, start).End);
    }

    /// <summary>
    /// 验证编码身份忽略显示文字，但包含体系、代码和版本。
    /// </summary>
    [Fact]
    public void Coding_identity_ignores_display_but_includes_version()
    {
        var system = new Uri("https://example.invalid/codes/test");
        var first = new Coding(system, "protein", "1", "蛋白质");
        var renamed = new Coding(system, "protein", "1", "Protein");
        var otherVersion = new Coding(system, "protein", "2", "蛋白质");

        Assert.True(first.HasSameIdentity(renamed));
        Assert.Equal(first, renamed);
        Assert.Equal(first.GetHashCode(), renamed.GetHashCode());
        Assert.False(first.HasSameIdentity(otherVersion));
        Assert.False(first.HasSameIdentity(null));
    }

    /// <summary>
    /// 验证数量范围至少具有一个边界、单位一致且常规下界不高于上界。
    /// </summary>
    [Fact]
    public void Quantity_range_enforces_compatible_bounds()
    {
        var grams = new Coding(UcumSystem, "g", display: "克");
        var kilograms = new Coding(UcumSystem, "kg", display: "千克");
        var low = new Quantity(10, grams);
        var high = new Quantity(20, new Coding(UcumSystem, "g", display: "gram"));

        Assert.Equal(low, new QuantityRange(low, high).Low);
        Assert.Throws<ArgumentException>(() => new QuantityRange(null, null));
        Assert.Throws<ArgumentException>(() => new QuantityRange(low, new Quantity(20, kilograms)));
        Assert.Throws<ArgumentException>(() => new QuantityRange(high, low));
    }

    /// <summary>
    /// 验证数量范围接受方向正确的比较符，并拒绝反向或空区间表达。
    /// </summary>
    [Fact]
    public void Quantity_range_enforces_comparator_semantics()
    {
        var grams = new Coding(UcumSystem, "g");
        var lowerExpression = new Quantity(10, grams, QuantityComparator.GreaterThan);
        var upperExpression = new Quantity(20, grams, QuantityComparator.LessThanOrEqual);

        var range = new QuantityRange(lowerExpression, upperExpression);

        Assert.Equal(QuantityComparator.GreaterThan, range.Low?.Comparator);
        Assert.Equal(QuantityComparator.LessThanOrEqual, range.High?.Comparator);
        Assert.Throws<ArgumentException>(() => new QuantityRange(
            new Quantity(10, grams, QuantityComparator.LessThan),
            upperExpression));
        Assert.Throws<ArgumentException>(() => new QuantityRange(
            lowerExpression,
            new Quantity(20, grams, QuantityComparator.GreaterThan)));
        Assert.Throws<ArgumentException>(() => new QuantityRange(
            new Quantity(20, grams, QuantityComparator.GreaterThan),
            new Quantity(10, grams, QuantityComparator.LessThan)));
        Assert.Throws<ArgumentException>(() => new QuantityRange(
            new Quantity(10, grams, QuantityComparator.GreaterThan),
            new Quantity(10, grams, QuantityComparator.LessThanOrEqual)));
    }

    /// <summary>
    /// 验证公开列表保存赋值时的只读快照。
    /// </summary>
    [Fact]
    public void Contract_collections_are_defensive_snapshots()
    {
        var given = new List<string> { "样本名" };
        var name = new HumanName { Given = given };

        given[0] = "已修改";
        given.Add("新增部分");

        Assert.Equal(new[] { "样本名" }, name.Given);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)name.Given).Add("外部写入"));
    }

    /// <summary>
    /// 验证报告产物身份拒绝空媒体类型或空内容指纹。
    /// </summary>
    [Fact]
    public void Report_artifact_identity_requires_media_type_and_fingerprint()
    {
        var fingerprint = new ContentFingerprint(
            new Coding(new Uri("https://example.invalid/codes/fingerprint"), "sha-256"),
            new string('a', 64));

        var artifact = new ReportArtifactIdentity(" application/pdf ", fingerprint);

        Assert.Equal("application/pdf", artifact.MediaType);
        Assert.Same(fingerprint, artifact.Fingerprint);
        Assert.Throws<ArgumentException>(() => new ReportArtifactIdentity(" ", fingerprint));
        Assert.Throws<ArgumentNullException>(() => new ReportArtifactIdentity("application/pdf", null!));
    }

    /// <summary>
    /// 验证量表自身版本独立于代码体系版本，并规范化可选文本。
    /// </summary>
    [Fact]
    public void Assessment_instrument_version_has_independent_normalized_semantics()
    {
        var code = new Coding(
            new Uri("https://example.invalid/codes/assessment-instrument"),
            "synthetic-scale",
            "code-system-release");
        var instrument = new AssessmentInstrumentIdentity
        {
            Code = code,
            Version = "  instrument-edition  "
        };
        var unversioned = instrument with { Version = " " };

        Assert.Equal("code-system-release", instrument.Code.Version);
        Assert.Equal("instrument-edition", instrument.Version);
        Assert.Null(unversioned.Version);
    }

    /// <summary>
    /// 验证通用扩展值的判别联合仅由 Contracts 程序集扩展。
    /// </summary>
    [Fact]
    public void Archive_value_union_is_closed_to_external_derivation()
    {
        var constructors = typeof(ArchiveValue).GetConstructors(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotEmpty(constructors);
        Assert.DoesNotContain(
            constructors,
            constructor => constructor.IsPublic || constructor.IsFamily || constructor.IsFamilyOrAssembly);
    }

    /// <summary>
    /// 验证通用档案值保留类型判别与结构相等语义。
    /// </summary>
    [Fact]
    public void Archive_values_have_stable_kind_and_value_equality()
    {
        ArchiveValue first = new TextArchiveValue("虚构值");
        ArchiveValue same = new TextArchiveValue("虚构值");
        ArchiveValue different = new TextArchiveValue("另一虚构值");

        Assert.Equal(ArchiveValueKind.Text, first.Kind);
        Assert.Equal(first, same);
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(first, different);
    }

    /// <summary>
    /// 验证校验路径接受属性和索引路径，并拒绝无根或无效转义。
    /// </summary>
    [Fact]
    public void Archive_element_path_enforces_stable_path_syntax()
    {
        var path = new ArchiveElementPath("/Meals/0/Entries/1/ReportedAmount");

        Assert.Equal("/Meals/0/Entries/1/ReportedAmount", path.Value);
        Assert.Throws<ArgumentException>(() => new ArchiveElementPath("Meals/0"));
        Assert.Throws<ArgumentException>(() => new ArchiveElementPath("/Meals//Entries"));
        Assert.Throws<ArgumentException>(() => new ArchiveElementPath("/Extensions/~2"));
    }

    /// <summary>
    /// 验证规范标识、代码体系和扩展定义均拒绝相对 URI。
    /// </summary>
    [Fact]
    public void Canonical_identifiers_require_absolute_uris()
    {
        var relative = new Uri("relative/path", UriKind.Relative);

        Assert.Throws<ArgumentException>(() => new Coding(relative, "code"));
        Assert.Throws<ArgumentException>(() => new BusinessIdentifier(relative, "value"));
        Assert.Throws<ArgumentException>(() => new CanonicalReference(relative));
        Assert.Throws<ArgumentException>(() => new ArchiveExtension(relative));
        Assert.Throws<ArgumentException>(() => new ApplicationIdentity(relative, "app", "1"));
        Assert.Throws<ArgumentException>(() => new ReferenceDataIdentity(relative, "dataset"));
    }

    /// <summary>
    /// 验证格式实现可以声明安全的展示名称和文件扩展名，而不把具体格式固化到调用方。
    /// </summary>
    [Fact]
    public void Archive_format_descriptor_normalizes_file_metadata()
    {
        var format = new ArchiveFormatDescriptor(
            new Uri("https://example.invalid/formats/test"),
            "1",
            "application/x-test",
            "  测试档案  ",
            "  .test  ");

        Assert.Equal("测试档案", format.DisplayName);
        Assert.Equal(".test", format.PreferredFileExtension);
        var exception = Assert.Throws<ArgumentException>(() => new ArchiveFormatDescriptor(
            new Uri("https://example.invalid/formats/test"),
            "1",
            preferredFileExtension: "test"));
        Assert.Equal("preferredFileExtension", exception.ParamName);
    }

    /// <summary>
    /// 验证结构化年龄保留未知精度，并拒绝不规范的年月日组成。
    /// </summary>
    [Fact]
    public void Chronological_age_preserves_precision_and_component_ranges()
    {
        var reportedYears = new ChronologicalAge(25);
        var exactAge = new ChronologicalAge(1, 4, 23);

        Assert.Null(reportedYears.Months);
        Assert.Null(reportedYears.Days);
        Assert.Equal("1岁4个月23天", exactAge.ToString());
        Assert.Throws<ArgumentException>(() => new ChronologicalAge(1, null, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChronologicalAge(1, 12));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChronologicalAge(1, 1, 31));
    }

    /// <summary>
    /// 验证面向保存流程的错误和严重错误会使校验结果失败，提示和警告不会。
    /// </summary>
    [Theory]
    [InlineData(ArchiveValidationSeverity.Information, false)]
    [InlineData(ArchiveValidationSeverity.Warning, false)]
    [InlineData(ArchiveValidationSeverity.Error, true)]
    [InlineData(ArchiveValidationSeverity.Fatal, true)]
    public void Validation_result_reports_blocking_severities(
        ArchiveValidationSeverity severity,
        bool expectedHasErrors)
    {
        var result = new ArchiveValidationResult
        {
            Issues = new[]
            {
                new ArchiveValidationIssue
                {
                    Code = "synthetic-test-issue",
                    Severity = severity,
                    Category = ArchiveValidationCategory.Integrity,
                    Message = "不包含患者资料的虚构校验消息。"
                }
            }
        };

        Assert.Equal(expectedHasErrors, result.HasErrors);
    }
}
