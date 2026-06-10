using Monster.WebApp.Shared;
using Xunit;

namespace Monster.WebApp.Tests;

public class PasswordValidatorTests
{
    [Fact]
    public void Validate_AcceptsCompliantPassword()
    {
        var (isValid, _) = PasswordValidator.Validate("Abcd123!");
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("", "비밀번호를 입력하세요.")]
    [InlineData("Ab1!", "비밀번호는 최소 8자 이상이어야 합니다.")]      // 8자 미만
    [InlineData("abcd123!", "대문자가 1개 이상 포함되어야 합니다.")]    // 대문자 없음
    [InlineData("ABCD123!", "소문자가 1개 이상 포함되어야 합니다.")]    // 소문자 없음
    [InlineData("Abcdefg!", "숫자가 1개 이상 포함되어야 합니다.")]      // 숫자 없음
    [InlineData("Abcd1234", "특수문자가 1개 이상 포함되어야 합니다.")]  // 특수문자 없음
    public void Validate_RejectsNonCompliantPassword(string password, string expectedMessage)
    {
        var (isValid, message) = PasswordValidator.Validate(password);
        Assert.False(isValid);
        Assert.Equal(expectedMessage, message);
    }

    [Fact]
    public void GetPolicyChecklist_AllMetForCompliantPassword()
    {
        var checklist = PasswordValidator.GetPolicyChecklist("Abcd123!");
        Assert.All(checklist, item => Assert.True(item.IsMet));
    }

    [Fact]
    public void GetPolicyChecklist_HandlesNullWithoutThrowing()
    {
        var checklist = PasswordValidator.GetPolicyChecklist(null!);
        Assert.All(checklist, item => Assert.False(item.IsMet));
    }
}
