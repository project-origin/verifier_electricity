using Xunit;

namespace ProjectOrigin.Electricity.Tests;

public class IsTrialTests
{
    [Theory]
    [InlineData(null, null, true)]
    [InlineData("true", "true", true)]
    [InlineData("false", "false", true)]
    [InlineData("false", null, true)]
    [InlineData(null, "false", true)]
    [InlineData("true", null, false)]
    [InlineData(null, "true", false)]
    [InlineData("true", "false", false)]
    [InlineData("false", "true", false)]
    public void IsTrialMatch_ReturnsExpected(string? left, string? right, bool expected)
    {
        Assert.Equal(expected, Rules.IsTrial.Match(left, right));
    }
}
