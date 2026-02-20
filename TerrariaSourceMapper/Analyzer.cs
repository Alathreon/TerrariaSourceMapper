using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using TerrariaSourceMapper.mappings;
using TerrariaSourceMapper.mappings.mapper;
using TerrariaSourceMapper.report;

namespace TerrariaSourceMapper
{
    internal class Analyzer
    {
        public static void Analyze(string source, string destination, bool ignoreFailed)
        {
            var assembly = Assembly.GetExecutingAssembly();

            using Stream stream = assembly.GetManifestResourceStream("TerrariaSourceMapper.Resources.Mappings.json") ?? throw new InvalidOperationException();
            using StreamReader reader = new(stream);
            string content = reader.ReadToEnd();

            var options = new JsonSerializerOptions
            {
                IncludeFields = true,
                Converters = { new MapperConverter(), new ClassConstantsMapperConverter() },
                WriteIndented = true // pretty print
            };
            Mappings mappings = JsonSerializer.Deserialize<Mappings>(content, options) ?? throw new InvalidOperationException();
            mappings.Entries.ForEach(m => m.Mapper.Init(source));

            long totalFiles = Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories).LongCount();
            Console.WriteLine($"{totalFiles} files found");
            Stopwatch stopwatch = Stopwatch.StartNew();
            Stopwatch lastPrint = Stopwatch.StartNew();
            int processedFiles = 0;
            int total = 0;
            int fails = 0;
            var reportDict = new SortedDictionary<string, List<ReportEntry>>();
            foreach (string file in Directory.EnumerateFiles(
             source,
             "*.cs",
             SearchOption.AllDirectories))
            {
                var relativeFile = Path.GetRelativePath(source, file);
                var reportEntries = new List<ReportEntry>();
                int lineNumber = 0;
                foreach (var line in File.ReadLines(file))
                {
                    foreach (var entry in mappings.Entries)
                    {
                        if (entry.Whitelist.Count > 0 && !entry.Whitelist.Contains(relativeFile)) continue;
                        if (entry.Blacklist.Count > 0 && entry.Blacklist.Contains(relativeFile)) continue;
                        Match match = Regex.Match(line, entry.Pattern);
                        if (match.Success)
                        {
                            var group = match.Groups[MappingsEntry.GROUP_NAME];
                            var value = group.Value;
                            var replacement = entry.Mapper.GetReplacementData(value);
                            var theClass = entry.Mapper.GetClass();
                            string? newContent = null;
                            if (replacement == null && ignoreFailed) continue;
                            if (replacement == null)
                            {
                                fails++;
                            }
                            else
                            {
                                newContent = string.Concat(line.AsSpan(0, group.Index), theClass.MemberPath + "." + replacement, line.AsSpan(group.Index + group.Length));
                            }
                            var reportEntry = new ReportEntry(lineNumber, entry.Pattern, line, newContent, value, replacement, theClass.FilePath, theClass.MemberPath);
                            reportEntries.Add(reportEntry);
                            total++;
                        }

                        if (lastPrint.Elapsed.TotalSeconds >= 1)
                        {
                            double percent = (processedFiles / (double)totalFiles) * 100;
                            Console.WriteLine($"Processed {processedFiles,5}/{totalFiles,5} files {processedFiles * 100D / totalFiles,5:F2}%, in {stopwatch.Elapsed.TotalSeconds:F1}s");
                            lastPrint.Restart();
                        }
                    }
                    lineNumber++;
                }
                if (reportEntries.Count > 0)
                {
                    reportDict.Add(relativeFile, reportEntries);
                }
                processedFiles++;
            }

            var report = new Report(total, fails, reportDict);
            options = new JsonSerializerOptions
            {
                WriteIndented = true // pretty print
            };
            File.WriteAllText(Path.Combine(destination, "report.json"), JsonSerializer.Serialize(report, options));
            Console.WriteLine($"Analyzing done");
            Console.WriteLine($"{total} matches found, {fails} fails");
        }
    }
}
