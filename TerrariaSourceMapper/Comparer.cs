using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TerrariaSourceMapper.report;

namespace TerrariaSourceMapper
{
    internal class Comparer
    {
        public static void CompareExternal(string destination)
        {
            var currReport = Path.Combine(destination, "report.json");
            var prevReport = Path.Combine(destination, "report.bak.json");
            if(!File.Exists(currReport)) throw new ArgumentException($"Couldn't find report at {destination}/report.json");
            if (!File.Exists(prevReport)) throw new ArgumentException($"Couldn't find backup report at {destination}/report.bak.json");
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "code",
                    Arguments = $"--diff \"{prevReport}\" \"{currReport}\"",
                    UseShellExecute = true
                }
            };

            process.Start();
        }

        public static void Compare(string destination)
        {
            var currReport = JsonSerializer.Deserialize<Report>(File.ReadAllText(Path.Combine(destination, "report.json"))) ?? throw new ArgumentException($"Couldn't find report at {destination}/report.json");
            var prevReport = JsonSerializer.Deserialize<Report>(File.ReadAllText(Path.Combine(destination, "report.bak.json"))) ?? throw new ArgumentException($"Couldn't find backup report at {destination}/report.bak.json");

            Console.WriteLine($"Found report with {currReport.Total} matches and backup report with {prevReport.Total} matches");

            var diff = Diff(prevReport, currReport);
            if (diff.Removed.Count > 0)
            {
                Console.WriteLine($"Warning: found {diff.Removed.Count} removed matches");
            }
            Console.WriteLine($"Found {diff.Added.Count} added matches");
            Console.WriteLine($"Found {diff.Modified.Count} modified matches");
            Console.WriteLine($"Saving to report.diff.json");
            var options = new JsonSerializerOptions
            {
                WriteIndented = true // pretty print
            };
            File.WriteAllText(Path.Combine(destination, "report.diff.json"), JsonSerializer.Serialize(diff, options));
        }

        private static ReportDiff Diff(Report original, Report edited)
        {
            var added = new List<ReportDiffEntry>();
            var removed = new List<ReportDiffEntry>();
            var modified = new List<ReportDiffEntry>();

            var allFiles = original.Files.Keys.Union(edited.Files.Keys);

            foreach (var file in allFiles)
            {
                original.Files.TryGetValue(file, out var entries1List);
                edited.Files.TryGetValue(file, out var entries2List);

                var entries1 = entries1List?.ToDictionary(e => e.Line, e => e) ?? [];
                var entries2 = entries2List?.ToDictionary(e => e.Line, e => e) ?? [];

                var allLines = entries1.Keys.Union(entries2.Keys);

                foreach (var line in allLines)
                {
                    entries1.TryGetValue(line, out var entry1);
                    entries2.TryGetValue(line, out var entry2);

                    var matches1 = entry1?.Matches ?? [];
                    var matches2 = entry2?.Matches ?? [];

                    foreach (var m1 in matches1)
                    {
                        var m2 = matches2.FirstOrDefault(m => ReportMatch.SectionEquals(m, m1));
                        if (m2 == null)
                        {
                            removed.Add(new ReportDiffEntry(file, RecordDiffLine.of(entry1!), m1));
                        }
                        else if (!m1.Equals(m2))
                        {
                            modified.Add(new ReportDiffEntry(file, RecordDiffLine.of(entry2!), m2));
                        }
                    }

                    foreach (var m2 in matches2)
                    {
                        var m1 = matches1.FirstOrDefault(m => ReportMatch.SectionEquals(m, m2));
                        if (m1 == null)
                        {
                            added.Add(new ReportDiffEntry(file, RecordDiffLine.of(entry2!), m2));
                        }
                    }
                }
            }

            return new ReportDiff(
                added.Count, modified.Count, removed.Count,
                added.OrderBy(e => e.File).ThenBy(e => e.Line.Number).ThenBy(e => e.Match.MatchStart).ToList(),
                modified.OrderBy(e => e.File).ThenBy(e => e.Line.Number).ThenBy(e => e.Match.MatchStart).ToList(),
                removed.OrderBy(e => e.File).ThenBy(e => e.Line.Number).ThenBy(e => e.Match.MatchStart).ToList()
            );
        }

        private record ReportDiff(
            int AddedCount,
            int ModifiedCount,
            int RemovedCount,
            List<ReportDiffEntry> Added,
            List<ReportDiffEntry> Modified,
            List<ReportDiffEntry> Removed);
        private record ReportDiffEntry(string File, RecordDiffLine Line, ReportMatch Match);
        private record RecordDiffLine(int Number, string Member, string Content)
        {
            public static RecordDiffLine of(ReportEntry entry)
            {
                return new RecordDiffLine(entry.Line, entry.Member, entry.Content);
            }
        }
    }
}
