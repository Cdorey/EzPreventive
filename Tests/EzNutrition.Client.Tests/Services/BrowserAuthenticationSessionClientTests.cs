using EzNutrition.Client.Infrastructure;
using EzNutrition.Presentation.Services;
using EzNutrition.Shared.Data.DTO;
using Microsoft.JSInterop;
using System.Text.Json;

namespace EzNutrition.Client.Tests.Services;

/// <summary>验证浏览器互操作 DTO 的实际序列化和稳定错误码，避免桥接成功却丢失会话字段。</summary>
public sealed class BrowserAuthenticationSessionClientTests
{
    [Fact]
    public async Task Browser_response_deserializes_and_module_is_loaded_once()
    {
        var tokens = SessionTestContext.CreateTokens(DateTimeOffset.UtcNow) with { RefreshToken = null };
        var js = new TestJsRuntime(JsonSerializer.Serialize(new { status = 200, tokens }, JsonSerializerOptions.Web));
        await using var client = new BrowserAuthenticationSessionClient(js, new ApplicationServerEndpoint(SessionTestContext.BaseAddress));
        var login = await client.SignInAsync(new LoginRequestDto { UserName = "test", Password = "password" });
        var refresh = await client.RefreshAsync(login.SessionId);
        Assert.Equal(tokens, login);
        Assert.Equal(tokens, refresh);
        Assert.Equal(1, js.ImportCount);
        Assert.Equal(new[] { "subscribe", "login", "refresh" }, js.Calls);
    }

    [Theory]
    [InlineData(401, AuthenticationErrorCodes.SessionInvalid)]
    [InlineData(409, AuthenticationErrorCodes.SessionChanged)]
    public async Task Known_authentication_failures_keep_the_server_error_code(int status, string code)
    {
        var js = new TestJsRuntime(JsonSerializer.Serialize(new { status, error = new { code, message = "test error" } }));
        await using var client = new BrowserAuthenticationSessionClient(js, new ApplicationServerEndpoint(SessionTestContext.BaseAddress));
        var error = await Assert.ThrowsAsync<SessionAuthenticationException>(() => client.RefreshAsync(Guid.NewGuid()));
        Assert.Equal(code, error.Code);
    }

    [Fact]
    public async Task Transient_module_import_failure_can_be_retried_without_losing_the_cookie()
    {
        var js = new TestJsRuntime("{\"status\":204}") { FailImport = true };
        await using var client = new BrowserAuthenticationSessionClient(js, new ApplicationServerEndpoint(SessionTestContext.BaseAddress));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.RestoreAsync());
        js.FailImport = false;
        Assert.Null(await client.RestoreAsync());
        Assert.Equal(2, js.ImportCount);
    }

    private sealed class TestJsRuntime(string response) : IJSRuntime, IJSObjectReference
    {
        internal int ImportCount { get; private set; }
        internal bool FailImport { get; set; }
        internal List<string> Calls { get; } = [];
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "import")
            {
                ImportCount++;
                if (FailImport) throw new JSException("simulated module failure");
                return ValueTask.FromResult((TValue)(object)this);
            }
            Calls.Add(identifier);
            if (identifier is "subscribe" or "unsubscribe") return ValueTask.FromResult(default(TValue)!);
            return ValueTask.FromResult(JsonSerializer.Deserialize<TValue>(response, JsonSerializerOptions.Web)!);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
