namespace Hexalith.Parties.Contracts;

public static class PartiesTextHeuristics
{
    public static bool ContainsTenant(string? value)
        => value?.Contains("tenant", StringComparison.OrdinalIgnoreCase) == true;
}
