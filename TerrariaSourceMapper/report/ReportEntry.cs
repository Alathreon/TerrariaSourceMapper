namespace TerrariaSourceMapper.report
{
    internal record ReportEntry(int Line, string Pattern, string OldContent, string? NewContent, string Match, string? Replacement, string? ConstantNamespace, string ConstantClass, string ConstantType)
    {
    }
}
