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

    /// <summary>访问令牌或本地空闲期限快照过期时，界面身份保留到宿主确认会话结果。</summary>
    [Theory]
    [InlineData(16)]
    [InlineData(8 * 24 * 60)]
    public async Task Cached_expiration_keeps_ui_identity_until_refresh_can_run(int elapsedMinutes)
    {
        using var context = new SessionTestContext();
        var original = await context.SignInAsync();
        context.Clock.Advance(TimeSpan.FromMinutes(elapsedMinutes));
        Assert.False(context.Session.TryGetAccessToken(out _));
        Assert.True((await context.Session.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated);
        var replacement = await context.Session.GetValidAccessTokenAsync();
        Assert.NotEqual(original.AccessToken, replacement);
        Assert.Equal(original.SessionId, context.Session.UserInfo?.SessionId);
    }

    /// <summary>其他窗口在第 5 天续期后，休眠窗口在第 8 天仍通过宿主恢复同一会话。</summary>
    [Fact]
    public async Task A_resumed_window_refreshes_after_another_window_extended_the_idle_deadline()
    {
        using var context = new SessionTestContext();
        var original = await context.SignInAsync();
        context.Clock.Advance(TimeSpan.FromDays(5));
        var renewedElsewhere = await context.Authentication.RefreshAsync(original.SessionId);
        context.Clock.Advance(TimeSpan.FromDays(3));
        Assert.True(original.RefreshExpiresAtUtc <= context.Clock.GetUtcNow());
        Assert.True(renewedElsewhere.RefreshExpiresAtUtc > context.Clock.GetUtcNow());
        var refreshed = SessionTestContext.CreateTokens(context.Clock.GetUtcNow(), sessionId: original.SessionId)
            with { SessionExpiresAtUtc = original.SessionExpiresAtUtc };
        context.Authentication.Refresh = id =>
        {
            Assert.Equal(renewedElsewhere.SessionId, id);
            return Task.FromResult(refreshed);
        };

        var token = await context.Session.GetValidAccessTokenAsync();

        Assert.Equal(refreshed.AccessToken, token);
        Assert.Equal(original.SessionId, context.Session.UserInfo?.SessionId);
        Assert.True((await context.Session.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated);
        Assert.Equal(2, context.Authentication.RefreshCount);
        Assert.Equal(0, context.Authentication.SignOutCount);
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

    /// <summary>本地空闲期限是否过期，都由宿主的明确拒绝清除身份并保留错误码。</summary>
    [Theory]
    [InlineData(0, AuthenticationErrorCodes.SessionInvalid)]
    [InlineData(8, AuthenticationErrorCodes.SessionInvalid)]
    [InlineData(8, AuthenticationErrorCodes.SessionChanged)]
    public async Task Host_rejection_clears_identity_without_hiding_failure(int elapsedDays, string errorCode)
    {
        using var context = new SessionTestContext();
        await context.SignInAsync(lifetime: TimeSpan.FromSeconds(30));
        context.Clock.Advance(TimeSpan.FromDays(elapsedDays));
        context.Authentication.Refresh = _ => throw new SessionAuthenticationException(errorCode, "rejected");
        var error = await Assert.ThrowsAsync<SessionAuthenticationException>(() => context.Session.GetValidAccessTokenAsync());
        Assert.Equal(errorCode, error.Code);
        Assert.Equal(1, context.Authentication.RefreshCount);
        Assert.Equal(0, context.Authentication.SignOutCount);
        Assert.Null(context.Session.UserInfo);
        Assert.False((await context.Session.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated);
    }

    /// <summary>无法确认缓存空闲期限时保留身份，网络恢复后允许继续刷新。</summary>
    [Fact]
    public async Task An_offline_resumed_window_keeps_identity_until_refresh_can_be_confirmed()
    {
        using var context = new SessionTestContext();
        var original = await context.SignInAsync();
        context.Clock.Advance(TimeSpan.FromDays(8));
        context.Authentication.Refresh = _ => throw new HttpRequestException("offline");

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Session.GetValidAccessTokenAsync());

        Assert.Equal(original.SessionId, context.Session.UserInfo?.SessionId);
        Assert.True((await context.Session.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated);
        Assert.Equal(1, context.Authentication.RefreshCount);
        Assert.Equal(0, context.Authentication.SignOutCount);
        context.Clock.Advance(TimeSpan.FromSeconds(5));
        var refreshed = SessionTestContext.CreateTokens(context.Clock.GetUtcNow(), sessionId: original.SessionId)
            with { SessionExpiresAtUtc = original.SessionExpiresAtUtc };
        context.Authentication.Refresh = _ => Task.FromResult(refreshed);
        Assert.Equal(refreshed.AccessToken, await context.Session.GetValidAccessTokenAsync());
        Assert.Equal(2, context.Authentication.RefreshCount);
    }

    /// <summary>绝对期限不会被其他窗口延长，到期后可在本地终止会话。</summary>
    [Fact]
    public async Task Absolute_expiration_clears_identity_without_attempting_refresh()
    {
        using var context = new SessionTestContext();
        await context.SignInAsync();
        context.Clock.Advance(TimeSpan.FromDays(30));
        Assert.False((await context.Session.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated);
        var error = await Assert.ThrowsAsync<SessionAuthenticationException>(() => context.Session.GetValidAccessTokenAsync());
        Assert.Equal(AuthenticationErrorCodes.SessionInvalid, error.Code);
        Assert.Null(context.Session.UserInfo);
        Assert.Equal(0, context.Authentication.RefreshCount);
        Assert.Equal(0, context.Authentication.SignOutCount);
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
