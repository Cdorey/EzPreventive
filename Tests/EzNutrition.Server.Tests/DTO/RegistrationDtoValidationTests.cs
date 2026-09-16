using EzNutrition.Shared.Data.DTO;
using System.ComponentModel.DataAnnotations;

namespace EzNutrition.Server.Tests.DTO;

public sealed class RegistrationDtoValidationTests
{
    [Theory]
    [InlineData("User123")]
    [InlineData("A")]
    [InlineData("123456")]
    public void User_name_accepts_only_ascii_letters_and_numbers(string userName)
    {
        Assert.DoesNotContain(Validate(userName), result =>
            result.MemberNames.Contains(nameof(RegistrationDto.UserName)));
    }

    [Theory]
    [InlineData("张三")]
    [InlineData("user-name")]
    [InlineData("user_name")]
    [InlineData("user name")]
    [InlineData("user@example")]
    public void User_name_rejects_chinese_spaces_and_symbols(string userName)
    {
        var error = Assert.Single(Validate(userName), result =>
            result.MemberNames.Contains(nameof(RegistrationDto.UserName)));

        Assert.Equal("用户名只能包含英文字母和数字，不支持中文、空格或符号", error.ErrorMessage);
    }

    private static IReadOnlyList<ValidationResult> Validate(string userName)
    {
        var registration = new RegistrationDto
        {
            UserName = userName,
            Password = "valid-password",
            Email = "person@example.test"
        };
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(
            registration,
            new ValidationContext(registration),
            results,
            validateAllProperties: true);
        return results;
    }
}
