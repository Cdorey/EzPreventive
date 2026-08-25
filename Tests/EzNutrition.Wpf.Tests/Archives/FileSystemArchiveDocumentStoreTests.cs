using System.Text;
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
            new[] { $"{older.Info.DocumentId:N}.xml", $"{newer.Info.DocumentId:N}.xml" }
                .Order(StringComparer.Ordinal),
            contentNames);
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

        Assert.False(File.Exists(Path.Combine(temporary.RootPath, $"{documentId:N}.xml")));
        Assert.True(File.Exists(Path.Combine(temporary.RootPath, $"{documentId:N}.archive")));
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
        var catalog = await File.ReadAllTextAsync(catalogPath);
        catalog = catalog.Replace(
            $"{document.Info.DocumentId:N}.xml",
            $"{document.Info.DocumentId:N}-other.xml",
            StringComparison.Ordinal);
        await File.WriteAllTextAsync(catalogPath, catalog);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => store.GetAsync(document.Info.DocumentId).AsTask());
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
