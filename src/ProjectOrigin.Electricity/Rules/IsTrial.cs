namespace ProjectOrigin.Electricity.Rules;

public static class IsTrial
{
    public static bool Match(string? left, string? right) =>
        (left == right)
        || (left is null && right == "false")
        || (left == "false" && right is null);
}
