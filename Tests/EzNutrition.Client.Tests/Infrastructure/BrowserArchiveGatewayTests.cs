using EzNutrition.Application.Archives;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Client.Infrastructure;
using Microsoft.JSInterop;

namespace EzNutrition.Client.Tests.Infrastructure;

public sealed class BrowserArchiveGatewayTests
{
    [Fact]
    public async Task Browser_store_exposes_and_forwards_destructive_capabilities()
    {
        var module = new RecordingModule();
        await using var gateway = new BrowserArchiveGateway(new ModuleRuntime(module));
        var documentId = Guid.NewGuid();

        await gateway.DeleteAsync(documentId);
        await gateway.ClearAsync();

        Assert.True(gateway.Capabilities.HasFlag(ArchiveDocumentStoreCapabilities.Delete));
        Assert.True(gateway.Capabilities.HasFlag(ArchiveDocumentStoreCapabilities.Clear));
        Assert.Collection(
            module.Calls,
            call =>
            {
                Assert.Equal("deleteDocument", call.Identifier);
                Assert.Equal(documentId.ToString("D"), Assert.Single(call.Arguments));
            },
            call =>
            {
                Assert.Equal("clearDocuments", call.Identifier);
                Assert.Empty(call.Arguments);
            });
    }

    [Fact]
    public async Task Browser_transport_uses_format_metadata_for_the_download()
    {
        var module = new RecordingModule();
        await using var gateway = new BrowserArchiveGateway(new ModuleRuntime(module));
        var content = new byte[] { 1, 2, 3 };

        var saved = await gateway.SaveAsync(new ArchiveDocumentExport
        {
            SuggestedFileNameStem = "eznutrition-safe-id",
            Format = new ArchiveFormatDescriptor(
                new Uri("https://example.invalid/formats/test"),
                "1.0",
                "application/x-archive-test",
                "测试档案",
                ".archive-test"),
            Content = content
        });

        Assert.True(saved);
        var call = Assert.Single(module.Calls);
        Assert.Equal("downloadDocument", call.Identifier);
        Assert.Equal("eznutrition-safe-id.archive-test", call.Arguments[0]);
        Assert.Equal("application/x-archive-test", call.Arguments[1]);
        Assert.Equal(content, Assert.IsType<byte[]>(call.Arguments[2]));
    }

    private sealed class ModuleRuntime(IJSObjectReference module) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            Assert.Equal("import", identifier);
            return ValueTask.FromResult((TValue)(object)module);
        }
    }

    private sealed class RecordingModule : IJSObjectReference
    {
        public List<Invocation> Calls { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            Calls.Add(new Invocation(identifier, args ?? []));
            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed record Invocation(string Identifier, IReadOnlyList<object?> Arguments);
}
