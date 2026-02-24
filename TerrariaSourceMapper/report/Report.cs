namespace TerrariaSourceMapper.report
{
    internal record Report(int Total, int Failed, int TotalEntries, SortedDictionary<string, List<ReportEntry>> Files)
    {
    }
}
