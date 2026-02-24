namespace TerrariaSourceMapper.report
{
    internal record ReportMatch(
        string Pattern,
        int MatchStart,
        int MatchLength,
        string Match,
        string? Replacement,        // Null if couldn't find replacement AKA failed
        string? ConstantNamespace,  // Null if class exists
        string ConstantClass,
        string ConstantType)
    {
    }
}
