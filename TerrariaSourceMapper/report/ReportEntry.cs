namespace TerrariaSourceMapper.report
{
    internal record ReportEntry(
        int Line,
        string Member,
        string Content,
        List<ReportMatch> Matches)
    {
    }
}
