using EzNutrition.Presentation.Services;
using EzNutrition.Shared.Data.DTO;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace EzNutrition.Client.Tests.Services;

public sealed class AccountServiceTests
{
    private static readonly Uri BaseAddress = new("https://client.example.test/");

    [Fact]
    public async Task Public_account_flows_use_the_anonymous_client_and_expected_routes()
    {
        var factory = CreateSuccessfulFactory();
        var service = CreateService(factory);

        await service.ResendEmailConfirmationAsync("person@example.test");
        await service.RequestPasswordResetAsync("person@example.test");
        await service.ResetPasswordAsync(new ResetPasswordDto
        {
            UserId = "user-1",
            Token = "reset-token",
            NewPassword = "new-password",
            ConfirmPassword = "new-password"
        });
        await service.ConfirmEmailAsync(new ConfirmEmailDto
        {
            UserId = "user-1",
            Token = "confirm-token"
        });
        await service.ConfirmEmailChangeAsync(new ConfirmEmailChangeDto
        {
            UserId = "user-1",
            NewEmail = "new@example.test",
            Token = "change-token"
        });

        Assert.Equal(Enumerable.Repeat("Anonymous", 5), factory.ClientNames);
        Assert.Collection(
            factory.Handler.Requests,
            request => AssertRequest(request, HttpMethod.Post, "Auth/ResendEmailConfirmation"),
            request => AssertRequest(request, HttpMethod.Post, "Auth/ForgotPassword"),
            request => AssertRequest(request, HttpMethod.Post, "Auth/ResetPassword"),
            request => AssertRequest(request, HttpMethod.Post, "Auth/ConfirmEmail"),
            request => AssertRequest(request, HttpMethod.Post, "Auth/ConfirmEmailChange"));
        Assert.Contains(
            "person@example.test",
            Assert.IsType<string>(factory.Handler.Requests[0].Body),
            StringComparison.Ordinal);
        Assert.Contains(
            "reset-token",
            Assert.IsType<string>(factory.Handler.Requests[2].Body),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Signed_in_account_flows_use_the_authorized_client_and_correct_methods()
    {
        var factory = CreateSuccessfulFactory();
        var service = CreateService(factory);

        await service.ChangePasswordAsync(new ChangePasswordDto
        {
            CurrentPassword = "current-password",
            NewPassword = "new-password",
            ConfirmPassword = "new-password"
        });
        await service.RequestEmailChangeAsync(new RequestEmailChangeDto
        {
            CurrentPassword = "current-password",
            NewEmail = "new@example.test"
        });
        await service.ChangePhoneNumberAsync(new ChangePhoneNumberDto
        {
            CurrentPassword = "current-password",
            PhoneNumber = "+86 13800000000"
        });

        Assert.Equal(Enumerable.Repeat("Authorize", 3), factory.ClientNames);
        Assert.Collection(
            factory.Handler.Requests,
            request => AssertRequest(request, HttpMethod.Post, "User/ChangePassword"),
            request => AssertRequest(request, HttpMethod.Post, "User/RequestEmailChange"),
            request => AssertRequest(request, HttpMethod.Put, "User/PhoneNumber"));
    }

    [Fact]
    public async Task Server_validation_message_is_preserved_for_the_user()
    {
        var factory = new RecordingHttpClientFactory((_, _) => Task.FromResult(
            JsonResponse(
                HttpStatusCode.BadRequest,
                new AccountOperationResultDto
                {
                    Success = false,
                    Message = "当前密码不正确。"
                })));
        var service = CreateService(factory);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ChangePasswordAsync(new ChangePasswordDto()));

        Assert.Equal("当前密码不正确。", exception.Message);
    }

    [Fact]
    public async Task Rate_limit_without_json_uses_a_safe_fallback_message()
    {
        var factory = new RecordingHttpClientFactory((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent(string.Empty)
            }));
        var service = CreateService(factory);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RequestPasswordResetAsync("person@example.test"));

        Assert.Equal("请求过于频繁，请稍后再试。", exception.Message);
    }

    [Fact]
    public async Task Profile_uses_authorized_client_and_reads_confirmation_states()
    {
        var expected = new UserInfoDto
        {
            UserId = "user-1",
            UserName = "person",
            Email = "person@example.test",
            EmailConfirmed = true,
            PhoneNumber = "+86 13800000000",
            PhoneNumberConfirmed = false,
            Roles = ["Student"],
            Claims = []
        };
        var factory = new RecordingHttpClientFactory((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expected)
            }));
        var service = CreateService(factory);

        var actual = await service.GetProfileAsync();

        Assert.Equal("Authorize", Assert.Single(factory.ClientNames));
        var request = Assert.Single(factory.Handler.Requests);
        AssertRequest(request, HttpMethod.Get, "User/Profile");
        Assert.True(actual.EmailConfirmed);
        Assert.False(actual.PhoneNumberConfirmed);
    }

    [Fact]
    public void Password_and_email_dtos_enforce_confirmation_and_format()
    {
        var reset = new ResetPasswordDto
        {
            UserId = "user-1",
            Token = "token",
            NewPassword = "password-one",
            ConfirmPassword = "password-two"
        };
        var email = new RequestEmailChangeDto
        {
            CurrentPassword = "current-password",
            NewEmail = "not-an-email"
        };

        Assert.Contains(
            Validate(reset),
            result => result.MemberNames.Contains(nameof(ResetPasswordDto.ConfirmPassword)));
        Assert.Contains(
            Validate(email),
            result => result.MemberNames.Contains(nameof(RequestEmailChangeDto.NewEmail)));
    }

    private static AccountService CreateService(IHttpClientFactory factory) =>
        new(factory, Microsoft.Extensions.Logging.Abstractions.NullLogger<AccountService>.Instance);

    private static RecordingHttpClientFactory CreateSuccessfulFactory() =>
        new((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            new AccountOperationResultDto
            {
                Success = true,
                Message = "ok"
            })));

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        AccountOperationResultDto result) =>
        new(statusCode) { Content = JsonContent.Create(result) };

    private static void AssertRequest(
        RecordedRequest request,
        HttpMethod method,
        string relativeUri)
    {
        Assert.Equal(method, request.Method);
        Assert.Equal(new Uri(BaseAddress, relativeUri), request.RequestUri);
    }

    private static IReadOnlyList<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            results,
            validateAllProperties: true);
        return results;
    }

    private sealed class RecordingHttpClientFactory(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        : IHttpClientFactory
    {
        internal RecordingHandler Handler { get; } = new(responseFactory);

        internal List<string> ClientNames { get; } = [];

        public HttpClient CreateClient(string name)
        {
            ClientNames.Add(name);
            return new HttpClient(Handler, disposeHandler: false)
            {
                BaseAddress = BaseAddress
            };
        }
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        : HttpMessageHandler
    {
        internal List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, body));
            return await responseFactory(request, cancellationToken);
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, Uri RequestUri, string? Body);
}
