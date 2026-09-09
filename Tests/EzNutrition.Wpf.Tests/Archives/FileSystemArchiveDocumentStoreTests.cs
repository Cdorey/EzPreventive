using System.Text;
using System.Text.Json.Nodes;
using EzNutrition.Application.Archives;
using EzNutrition.Wpf.Archives;
using Microsoft.Extensions.Logging.Abstractions;

namespace EzNutrition.Wpf.Tests.Archives;

public sealed class FileSystemArchiveDocumentStoreTests
{
    [Fact]
    public async Task Save_list_and_get_use_non_identifying_document_file_names()
    {
        using var temporary = new TempDirectory();
        using var store = CreateStore(temporary.RootPath);
        var older = CreateDocument(
            Guid.NewGuid(),
            "较早的虚构对象",
            new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero));
        var newer = CreateDocument(
            Guid.NewGuid(),
            "较新的虚构对象",
            new DateTimeOffset(2026, 8, 21, 9, 0, 0, TimeSpan.Zero));

        await store.SaveAsync(older);
        await store.SaveAsync(newer);
        var records = await store.ListAsync();
        var restored = await store.GetAsync(older.Info.DocumentId);

        Assert.Equal([newer.Info.DocumentId, older.Info.DocumentId], records.Select(record => record.DocumentId));
        Assert.Equal(older.Info, restored?.Info);
        Assert.Equal(older.Content.ToArray(), restored?.Content.ToArray());

        var contentNames = Directory
            .EnumerateFiles(temporary.RootPath, "*.xml", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            new[] { older.Info.DocumentId.ToString("N"), newer.Info.DocumentId.ToString("N") }
                .Order(StringComparer.Ordinal),
            contentNames.Select(name => name!.Split('.')[0]));
        Assert.DoesNotContain(contentNames, name => name!.Contains("虚构对象", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Replacing_a_document_removes_the_superseded_format_file()
    {
        using var temporary = new TempDirectory();
        using var store = CreateStore(temporary.RootPath);
        var documentId = Guid.NewGuid();
        var first = CreateDocument(documentId, "格式迁移对象", DateTimeOffset.UtcNow);
        var replacement = CreateDocument(
            documentId,
            "格式迁移对象",
            DateTimeOffset.UtcNow.AddMinutes(1),
            preferredExtension: ".archive",
            content: "replacement");

        await store.SaveAsync(first);
        await store.SaveAsync(replacement);

        Assert.Empty(Directory.EnumerateFiles(temporary.RootPath, $"{documentId:N}*.xml"));
        Assert.Single(Directory.EnumerateFiles(temporary.RootPath, $"{documentId:N}*.archive"));
        Assert.Equal(
            Encoding.UTF8.GetBytes("replacement"),
            (await store.GetAsync(documentId))?.Content.ToArray());
    }

    [Fact]
    public async Task Malformed_catalog_entry_does_not_hide_valid_documents()
    {
        using var temporary = new TempDirectory();
        using var store = CreateStore(temporary.RootPath);
        var valid = CreateDocument(Guid.NewGuid(), "有效虚构对象", DateTimeOffset.UtcNow);
        await store.SaveAsync(valid);
        var catalogPath = Path.Combine(temporary.RootPath, ".catalog");
        await File.WriteAllTextAsync(
            Path.Combine(catalogPath, $"{Guid.NewGuid():N}.json"),
            "{ malformed json");

        var records = await store.ListAsync();

        Assert.Equal(valid.Info.DocumentId, Assert.Single(records).DocumentId);
    }

    [Fact]
    public async Task Delete_removes_only_the_selected_managed_document()
    {
        using var temporary = new TempDirectory();
        using var store = CreateStore(temporary.RootPath);
        var selected = CreateDocument(Guid.NewGuid(), "待删除虚构对象", DateTimeOffset.UtcNow);
        var retained = CreateDocument(Guid.NewGuid(), "保留虚构对象", DateTimeOffset.UtcNow);
        await store.SaveAsync(selected);
        await store.SaveAsync(retained);
        var unrelatedPath = Path.Combine(temporary.RootPath, "operator-notes.txt");
        await File.WriteAllTextAsync(unrelatedPath, "not managed by EzNutrition");

        await store.DeleteAsync(selected.Info.DocumentId);

        Assert.Null(await store.GetAsync(selected.Info.DocumentId));
        Assert.NotNull(await store.GetAsync(retained.Info.DocumentId));
        Assert.True(File.Exists(unrelatedPath));
    }

    [Fact]
    public async Task Clear_removes_managed_orphans_but_preserves_unrelated_files()
    {
        using var temporary = new TempDirectory();
        using var store = CreateStore(temporary.RootPath);
        var valid = CreateDocument(Guid.NewGuid(), "已索引虚构对象", DateTimeOffset.UtcNow);
        await store.SaveAsync(valid);

        var orphanId = Guid.NewGuid();
        var orphanPath = Path.Combine(temporary.RootPath, $"{orphanId:N}.xml");
        await File.WriteAllTextAsync(orphanPath, "orphaned document");
        var catalogPath = Path.Combine(temporary.RootPath, ".catalog");
        await File.WriteAllTextAsync(
            Path.Combine(catalogPath, $"{orphanId:N}.json"),
            "{ interrupted write");
        var unrelatedPath = Path.Combine(temporary.RootPath, "operator-notes.txt");
        var similarButUnmanagedPath = Path.Combine(temporary.RootPath, $"{Guid.NewGuid():N}.backup.xml");
        await File.WriteAllTextAsync(unrelatedPath, "retain me");
        await File.WriteAllTextAsync(similarButUnmanagedPath, "retain me too");

        await store.ClearAsync();

        Assert.Empty(await store.ListAsync());
        Assert.False(File.Exists(orphanPath));
        Assert.True(File.Exists(unrelatedPath));
        Assert.True(File.Exists(similarButUnmanagedPath));
        Assert.Empty(Directory.EnumerateFiles(catalogPath, "*.json", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public async Task Catalog_cannot_redirect_a_document_to_a_similarly_prefixed_file()
    {
        using var temporary = new TempDirectory();
        using var store = CreateStore(temporary.RootPath);
        var document = CreateDocument(Guid.NewGuid(), "索引篡改虚构对象", DateTimeOffset.UtcNow);
        await store.SaveAsync(document);
        var catalogPath = Path.Combine(
            temporary.RootPath,
            ".catalog",
            $"{document.Info.DocumentId:N}.json");
        var catalog = JsonNode.Parse(await File.ReadAllTextAsync(catalogPath))!;
        catalog["contentFileName"] = $"{document.Info.DocumentId:N}-other.xml";
        await File.WriteAllTextAsync(catalogPath, catalog.ToJsonString());

        await Assert.ThrowsAsync<InvalidDataException>(
            () => store.GetAsync(document.Info.DocumentId).AsTask());
    }

    /// <summary>核对条件提交拒绝旧预览覆盖已经更新的本机版本。</summary>
    [Fact]
    public async Task Compare_exchange_requires_the_exact_previous_content()
    {
        using var temporary = new TempDirectory();
        using var store = CreateStore(temporary.RootPath);
        var original = CreateDocument(Guid.NewGuid(), "并发提交样本", DateTimeOffset.UtcNow);
        var replacement = original with { Content = Encoding.UTF8.GetBytes("updated") };

        Assert.True(await store.CompareExchangeAsync(original, null));
        Assert.False(await store.CompareExchangeAsync(replacement, null));
        Assert.False(await store.CompareExchangeAsync(replacement, "wrong"u8.ToArray()));
        Assert.True(await store.CompareExchangeAsync(replacement, original.Content));
        Assert.False(await store.CompareExchangeAsync(original, original.Content));
        Assert.Equal(replacement.Content.ToArray(), (await store.GetAsync(original.Info.DocumentId))!.Content.ToArray());
    }

    /// <summary>模拟索引提交失败；新正文写完也不能破坏旧索引所对应的正文。</summary>
    [Fact]
    public async Task Failed_catalog_commit_keeps_the_previous_document_readable()
    {
        using var temporary = new TempDirectory();
        using var store = CreateStore(temporary.RootPath);
        var original = CreateDocument(Guid.NewGuid(), "中断提交样本", DateTimeOffset.UtcNow);
        var replacement = original with { Content = Encoding.UTF8.GetBytes("new content") };
        await store.SaveAsync(original);
        var catalogPath = Path.Combine(temporary.RootPath, ".catalog", $"{original.Info.DocumentId:N}.json");
        using (var heldCatalog = new FileStream(catalogPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var failure = await Record.ExceptionAsync(() => store.CompareExchangeAsync(replacement, original.Content).AsTask());
            Assert.True(failure is IOException or UnauthorizedAccessException);
        }

        var retained = await store.GetAsync(original.Info.DocumentId);
        Assert.Equal(original.Info, retained!.Info);
        Assert.Equal(original.Content.ToArray(), retained.Content.ToArray());
        Assert.True(await store.CompareExchangeAsync(replacement, original.Content));
        Assert.Single(Directory.EnumerateFiles(temporary.RootPath, "*.xml"));
    }

    /// <summary>独立存储实例共享目录时，竞争提交只能产生一个获胜版本。</summary>
    [Fact]
    public async Task Separate_instances_cannot_both_commit_against_the_same_previous_content()
    {
        using var temporary = new TempDirectory();
        using var first = CreateStore(temporary.RootPath);
        using var second = CreateStore(temporary.RootPath);
        var original = CreateDocument(Guid.NewGuid(), "双窗口样本", DateTimeOffset.UtcNow);
        await first.SaveAsync(original);
        var left = original with { Content = "left"u8.ToArray() };
        var right = original with { Content = "right"u8.ToArray() };

        var attempts = await Task.WhenAll(Attempt(first, left), Attempt(second, right));
        Assert.Single(attempts.Where(success => success));
        var current = await first.GetAsync(original.Info.DocumentId);
        Assert.Equal((attempts[0] ? left : right).Content.ToArray(), current!.Content.ToArray());
        Assert.False(await second.CompareExchangeAsync(original, original.Content));

        async Task<bool> Attempt(FileSystemArchiveDocumentStore target, StoredArchiveDocument document)
        {
            try { return await target.CompareExchangeAsync(document, original.Content); }
            catch (IOException) { return false; } // 另一个写入者仍持有目录锁，提示重试亦属于明确拒绝。
        }
    }

    /// <summary>已有不带内容指纹的文档文件仍可读取并迁移，索引格式不需重写升级。</summary>
    [Fact]
    public async Task Legacy_document_file_names_remain_readable()
    {
        using var temporary = new TempDirectory();
        using var store = CreateStore(temporary.RootPath);
        var original = CreateDocument(Guid.NewGuid(), "旧档案样本", DateTimeOffset.UtcNow);
        await store.SaveAsync(original);
        var catalogPath = Path.Combine(temporary.RootPath, ".catalog", $"{original.Info.DocumentId:N}.json");
        var catalog = JsonNode.Parse(await File.ReadAllTextAsync(catalogPath))!;
        var currentPath = Path.Combine(temporary.RootPath, catalog["contentFileName"]!.GetValue<string>());
        var legacyName = $"{original.Info.DocumentId:N}.xml";
        File.Move(currentPath, Path.Combine(temporary.RootPath, legacyName));
        catalog["contentFileName"] = legacyName;
        await File.WriteAllTextAsync(catalogPath, catalog.ToJsonString());

        Assert.Equal(original.Content.ToArray(), (await store.GetAsync(original.Info.DocumentId))!.Content.ToArray());
        Assert.True(await store.CompareExchangeAsync(original with { Content = "revision"u8.ToArray() }, original.Content));
        Assert.False(File.Exists(Path.Combine(temporary.RootPath, legacyName)));
    }

    private static FileSystemArchiveDocumentStore CreateStore(string rootPath) =>
        new(
            ArchiveStorageDirectory.Create(rootPath),
            NullLogger<FileSystemArchiveDocumentStore>.Instance);

    private static StoredArchiveDocument CreateDocument(
        Guid documentId,
        string subject,
        DateTimeOffset lastSavedAt,
        string preferredExtension = ".xml",
        string content = "<archive version=\"1.0\" />") =>
        new()
        {
            Info = new StoredArchiveDocumentInfo
            {
                DocumentId = documentId,
                PatientId = Guid.NewGuid(),
                Title = $"{subject}的营养咨询",
                SubjectDisplay = subject,
                ConsultationStartedAt = lastSavedAt.AddHours(-1),
                LastSavedAt = lastSavedAt,
                FormatIdentifier = "https://eznutrition.cdorey.net/formats/archive-xml",
                FormatVersion = "1.0",
                MediaType = "application/vnd.eznutrition.archive+xml",
                FormatDisplayName = "EzNutrition XML 档案",
                PreferredFileExtension = preferredExtension
            },
            Content = Encoding.UTF8.GetBytes(content)
        };
}
