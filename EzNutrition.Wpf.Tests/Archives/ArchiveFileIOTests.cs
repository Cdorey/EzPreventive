using System.Text;
using EzNutrition.Wpf.Archives;

namespace EzNutrition.Wpf.Tests.Archives;

public sealed class ArchiveFileIOTests
{
    [Fact]
    public async Task Atomic_write_can_create_and_replace_a_document()
    {
        using var temporary = new TempDirectory();
        var path = Path.Combine(temporary.RootPath, "nested", "archive.xml");
        var first = Encoding.UTF8.GetBytes("<archive version=\"1\" />");
        var second = Encoding.UTF8.GetBytes("<archive version=\"2\" />");

        await ArchiveFileIO.WriteAtomicallyAsync(path, first, CancellationToken.None);
        await ArchiveFileIO.WriteAtomicallyAsync(path, second, CancellationToken.None);
        var actual = await ArchiveFileIO.ReadAllBytesAsync(path, CancellationToken.None);

        Assert.Equal(second, actual);
        Assert.Empty(Directory.EnumerateFiles(
            Path.GetDirectoryName(path)!,
            "*.tmp",
            SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public async Task Empty_and_oversized_documents_are_rejected_before_loading()
    {
        using var temporary = new TempDirectory();
        var emptyPath = Path.Combine(temporary.RootPath, "empty.xml");
        var oversizedPath = Path.Combine(temporary.RootPath, "oversized.xml");
        await File.WriteAllBytesAsync(emptyPath, []);
        await using (var stream = new FileStream(oversizedPath, FileMode.CreateNew, FileAccess.Write))
        {
            stream.SetLength(ArchiveFileIO.MaximumDocumentBytes + 1L);
        }

        await Assert.ThrowsAsync<InvalidDataException>(
            () => ArchiveFileIO.ReadAllBytesAsync(emptyPath, CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<InvalidDataException>(
            () => ArchiveFileIO.ReadAllBytesAsync(oversizedPath, CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<InvalidDataException>(
            () => ArchiveFileIO.WriteAtomicallyAsync(
                Path.Combine(temporary.RootPath, "new.xml"),
                ReadOnlyMemory<byte>.Empty,
                CancellationToken.None).AsTask());
    }
}
