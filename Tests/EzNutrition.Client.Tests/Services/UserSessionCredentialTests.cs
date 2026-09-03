using EzNutrition.Presentation.Models;
using EzNutrition.Presentation.Services;
using EzNutrition.Shared.Data.DTO;
using EzNutrition.Shared.Identities;

namespace EzNutrition.Client.Tests.Services;

/// <summary>覆盖恢复、并发续期及退出竞争，保证短令牌更换不改变业务会话。</summary>
public sealed class UserSessionCredentialTests
{
    [Fact]
    public void User_info_exposes_stable_and_optional_professional_identity()
    {
        var tokens = SessionTestContext.CreateTokens(DateTimeOffset.UtcNow, "professional", additionalClaims:
        [
            new(UserClaimTypes.RealName, "  测试医师  "),
            new(UserClaimTypes.InstitutionName, "  测试医疗机构  ")
        ]);
        var user = new UserInfo(tokens.AccessToken);
        Assert.Equal("professional-id", user.UserId);
        Assert.Equal(tokens.SessionId, user.SessionId);
        Assert.Equal("测试医师", user.RealName);
        Assert.Equal("测试医疗机构", user.InstitutionName);
        var ordinary = new UserInfo(SessionTestContext.CreateTokens(DateTimeOffset.UtcNow).AccessToken);
        Assert.Null(ordinary.RealName);
        Assert.Null(ordinary.InstitutionName);
    }

    [Fact]
    public async Task Sign_in_passes_normalized_username_and_remember_choice_to_host()
    {
        using var context = new SessionTestContext();
        LoginRequestDto? received = null;
        context.Authentication.SignIn = request =>
        {
            received = request;
            return Task.FromResult(SessionTestContext.CreateTokens(context.Clock.GetUtcNow()));
        };
        await context.Session.SignInAsync("  test-user  ", "password", rememberLogin: true);
        Assert.Equal("test-user", received?.UserName);
        Assert.Equal("password", received?.Password);
        Assert.True(received?.RememberLogin);
        Assert.True(context.Session.TryGetAccessToken(out _));
    }

    [Fact]
    public async Task Concurrent_initialization_restores_once_and_loads_public_information()
    {
        using var context = new SessionTestContext();
        var tokens = SessionTestContext.CreateTokens(context.Clock.GetUtcNow());
        context.Authentication.Restore = () => Task.FromResult<AuthenticationTokensDto?>(tokens);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => context.Session.InitializeAsync()));
        Assert.Equal(1, context.Authentication.RestoreCount);
        Assert.Equal(tokens.AccessToken, await context.Session.GetValidAccessTokenAsync());
        Assert.Equal("test-case", context.Session.CaseNumber);
        Assert.Equal("2.2.0.0", context.Session.ServerVersion);
        Assert.Equal("test-notice", context.Session.PrivacyPolicy);
        Assert.False(context.Session.HasVersionCompatibilityWarning);
    }

    [Theory]
    [InlineData("2.2.0.0", "2.2.99.42", false)]
    [InlineData("2.1.0.0", "2.2.0.0", true)]
    [InlineData("2.2.0.0", "3.2.0.0", true)]
    [InlineData("", "2.2.0.0", false)]
    [InlineData("2.2.0.0", "invalid", false)]
    public void Compatibility_warning_compares_product_and_contract_segments(string client, string server, bool expected) =>
        Assert.Equal(expected, UserSessionService.IsCompatibilityMismatch(client, server));

    [Fact]
    public async Task Concurrent_requests_share_one_refresh_and_one_cancelled_waiter_does_not_abort_it()
    {
        using var context = new SessionTestContext();
        var original = await context.SignInAsync(lifetime: TimeSpan.FromSeconds(30));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource<AuthenticationTokensDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Authentication.Refresh = _ => { entered.SetResult(); return released.Task; };
        using var cancellation = new CancellationTokenSource();
        var cancelled = context.Session.GetValidAccessTokenAsync(cancellation.Token);
        await entered.Task;
        var waiting = Enumerable.Range(0, 12).Select(_ => context.Session.GetValidAccessTokenAsync()).ToArray();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        var replacement = SessionTestContext.CreateTokens(context.Clock.GetUtcNow(), sessionId: original.SessionId);
        released.SetResult(replacement);
        Assert.All(await Task.WhenAll(waiting), token => Assert.Equal(replacement.AccessToken, token));
        Assert.Equal(1, context.Authentication.RefreshCount);
    }

    [Fact]
    public async Task Access_expiration_keeps_ui_identity_until_refresh_can_run()
    {
        using var context = new SessionTestContext();
        var original = await context.SignInAsync();
        context.Clock.Advance(TimeSpan.FromMinutes(16));
        Assert.False(context.Session.TryGetAccessToken(out _));
        Assert.True((await context.Session.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated);
        var replacement = await context.Session.GetValidAccessTokenAsync();
        Assert.NotEqual(original.AccessToken, replacement);
        Assert.Equal(original.SessionId, context.Session.UserInfo?.SessionId);
    }

    [Fact]
    public async Task Temporary_refresh_failure_keeps_identity_and_limits_retry_frequency()
    {
        using var context = new SessionTestContext();
        var original = await context.SignInAsync(lifetime: TimeSpan.FromSeconds(30));
        context.Authentication.Refresh = _ => throw new HttpRequestException("offline");
        Assert.Equal(original.AccessToken, await context.Session.GetValidAccessTokenAsync());
        Assert.Equal(original.AccessToken, await context.Session.GetValidAccessTokenAsync());
        Assert.Equal(1, context.Authentication.RefreshCount);
        context.Clock.Advance(TimeSpan.FromSeconds(31));
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Session.GetValidAccessTokenAsync());
        Assert.NotNull(context.Session.UserInfo);
        Assert.Equal(0, context.Authentication.SignOutCount);
    }

    [Fact]
    public async Task Revoked_refresh_clears_identity_without_hiding_failure()
    {
        using var context = new SessionTestContext();
        await context.SignInAsync(lifetime: TimeSpan.FromSeconds(30));
        context.Authentication.Refresh = _ => throw new SessionAuthenticationException(
            AuthenticationErrorCodes.SessionInvalid, "expired");
        await Assert.ThrowsAsync<SessionAuthenticationException>(() => context.Session.GetValidAccessTokenAsync());
        Assert.Null(context.Session.UserInfo);
        Assert.False((await context.Session.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated);
    }

    [Fact]
    public async Task Expired_session_is_not_extended_by_a_still_valid_access_token()
    {
        using var context = new SessionTestContext();
        var tokens = SessionTestContext.CreateTokens(context.Clock.GetUtcNow());
        context.Authentication.SignIn = _ => Task.FromResult(tokens with { RefreshExpiresAtUtc = context.Clock.GetUtcNow().AddMinutes(1) });
        await context.Session.SignInAsync("test-user", "password");
        context.Clock.Advance(TimeSpan.FromMinutes(2));
        await Assert.ThrowsAsync<SessionAuthenticationException>(() => context.Session.GetValidAccessTokenAsync());
        Assert.Null(context.Session.UserInfo);
        Assert.Equal(0, context.Authentication.RefreshCount);
    }

    [Fact]
    public async Task Logout_during_refresh_immediately_clears_identity_and_discards_the_late_response()
    {
        using var context = new SessionTestContext();
        var original = await context.SignInAsync(lifetime: TimeSpan.FromSeconds(30));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource<AuthenticationTokensDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Authentication.Refresh = _ => { entered.SetResult(); return released.Task; };
        Guid? revoked = null;
        context.Authentication.SignOut = id => { revoked = id; return Task.CompletedTask; };
        var refresh = context.Session.GetValidAccessTokenAsync();
        await entered.Task;
        var logout = context.Session.SignOutAsync();
        Assert.Null(context.Session.UserInfo);
        released.SetResult(SessionTestContext.CreateTokens(context.Clock.GetUtcNow(), sessionId: original.SessionId));
        await Assert.ThrowsAsync<SessionAuthenticationException>(() => refresh);
        await logout;
        Assert.Equal(original.SessionId, revoked);
        Assert.Null(context.Session.UserInfo);
    }

    [Fact]
    public async Task An_old_request_cannot_refresh_or_reject_a_new_account()
    {
        using var context = new SessionTestContext();
        var old = await context.SignInAsync("old");
        var current = await context.SignInAsync("new");
        var error = await Assert.ThrowsAsync<SessionAuthenticationException>(() =>
            context.Session.GetValidAccessTokenAsync(rejectedToken: old.AccessToken));
        Assert.Equal(AuthenticationErrorCodes.SessionChanged, error.Code);
        await context.Session.RejectAccessTokenAsync(old.AccessToken);
        Assert.Equal(current.AccessToken, await context.Session.GetValidAccessTokenAsync());
        Assert.Equal(0, context.Authentication.RefreshCount);
        Assert.Equal(0, context.Authentication.SignOutCount);
    }

    [Fact]
    public async Task Restore_failure_is_visible_and_does_not_attempt_password_login()
    {
        using var context = new SessionTestContext();
        context.Authentication.Restore = () => throw new HttpRequestException("offline");
        await context.Session.InitializeAsync();
        Assert.NotNull(context.Session.AutomaticSignInError);
        Assert.Equal(0, context.Authentication.SignOutCount);
        Assert.Null(context.Session.UserInfo);
    }

    [Fact]
    public async Task Malformed_token_cannot_become_an_authenticated_session()
    {
        using var context = new SessionTestContext();
        context.Authentication.SignIn = _ => Task.FromResult(
            SessionTestContext.CreateTokens(context.Clock.GetUtcNow()) with { AccessToken = "malformed" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Session.SignInAsync("test", "password"));
        Assert.Null(context.Session.UserInfo);
    }
}
