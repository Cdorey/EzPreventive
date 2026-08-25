using EzNutrition.Wpf.Archives;

namespace EzNutrition.Wpf.Tests.Archives;

public sealed class ArchiveStorageDirectoryTests
{
    [Fact]
    public void Default_directory_is_scoped_to_the_current_users_local_application_data()
    {
        var storage = ArchiveStorageDirectory.Create(null);

        Assert.Equal(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EzSuit",
                "EzNutrition",
                "Archives"),
            storage.RootPath);
    }

    [Fact]
    public void Configured_directory_is_normalized_and_created_only_on_demand()
    {
        using var temporary = new TempDirectory();
        var configuredPath = Path.Combine(temporary.RootPath, "custom", "archives") + Path.DirectorySeparatorChar;

        var storage = ArchiveStorageDirectory.Create(configuredPath);

        Assert.Equal(Path.TrimEndingDirectorySeparator(configuredPath), storage.RootPath);
        Assert.False(Directory.Exists(storage.RootPath));

        storage.EnsureCreated();

        Assert.True(Directory.Exists(storage.RootPath));
    }

    [Theory]
    [InlineData("archives")]
    [InlineData(".\\archives")]
    public void Relative_directory_is_rejected(string configuredPath)
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = ArchiveStorageDirectory.Create(configuredPath);
        });
    }

    [Fact]
    public void Drive_root_is_rejected()
    {
        using var temporary = new TempDirectory();
        var driveRoot = Path.GetPathRoot(temporary.RootPath);

        Assert.NotNull(driveRoot);
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = ArchiveStorageDirectory.Create(driveRoot!);
        });
    }
}
