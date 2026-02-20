namespace TerrariaSourceMapper.report
{
    internal record Report(int Total, int Failed, SortedDictionary<string, List<ReportEntry>> Files)
    {
    }
}
