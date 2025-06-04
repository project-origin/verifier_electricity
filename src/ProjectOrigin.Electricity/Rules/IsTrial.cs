namespace ProjectOrigin.Electricity.Rules;

public static class IsTrial
{
    public static bool Match(string? left, string? right)
    {
        if (left == right)
            return true;

        if (left is null && right == "false"
            || left == "false" && right is null)
        {
            return true;
        }

        return false;
    }
}
