using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
            int t = mappings.Entries.Count;
            mappings.Entries.RemoveAll(e => e.Ignore);
            Console.WriteLine($"Found {t} mappings, {t - mappings.Entries.Count} are ignored");
            mappings.Entries.ForEach(m => m.Mapper.Init(source));

            long totalFiles = Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories).LongCount();
            Console.WriteLine($"{totalFiles} files found");
            Stopwatch stopwatch = Stopwatch.StartNew();
            Stopwatch lastPrint = Stopwatch.StartNew();
            int processedFiles = 0;
            int total = 0;
            int fails = 0;
            int totalEntries = 0;
            var reportDict = new SortedDictionary<string, List<ReportEntry>>();
            foreach (string file in Directory.EnumerateFiles(
             source,
             "*.cs",
             SearchOption.AllDirectories))
            {
                var relativeFile = Path.GetRelativePath(source, file);
                var fileMappings = mappings.Entries
                    .Where(e =>
                       (e.Whitelist.Count == 0 || e.Whitelist.Contains(relativeFile))
                    && (e.Blacklist.Count == 0 || !e.Whitelist.Contains(relativeFile)));

                var reportEntries = new List<ReportEntry>();

                var code = File.ReadAllText(file);
                var lines = code.Split(
                    ["\r\n", "\r", "\n"],
                    StringSplitOptions.None
                );
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = tree.GetRoot();
                var members = root.DescendantNodes().Where(node =>
                    node is MethodDeclarationSyntax ||
                    node is ConstructorDeclarationSyntax ||
                    node is AccessorDeclarationSyntax
                );
                foreach (var member in members)
                {
                    var memberMappings = fileMappings;
                    if (member is MethodDeclarationSyntax method)
                    {
                        memberMappings = fileMappings.Where(e => e.MethodPattern == null || Regex.IsMatch(method.Identifier.Text, e.MethodPattern));
                    }
                    var bodySpan = member.GetLocation().GetMappedLineSpan();
                    int bodyStart = bodySpan.StartLinePosition.Line;
                    int bodyEnd = bodySpan.EndLinePosition.Line;
                    int lineNumber = bodyStart;
                    foreach (var line in lines.AsSpan(bodyStart, bodyEnd + 1 - bodyStart))
                    {
                        var memberName = member switch
                        {
                            MethodDeclarationSyntax m => m.Identifier.Text,
                            ConstructorDeclarationSyntax c => c.Identifier.Text,
                            AccessorDeclarationSyntax a => a.Keyword.Text,
                            _ => throw new InvalidOperationException()
                        };
                        var matches = new List<ReportMatch>();
                        foreach (var entry in memberMappings)
                        {
                            if (lastPrint.Elapsed.TotalSeconds >= 1)
                            {
                                double percent = (processedFiles / (double)totalFiles) * 100;
                                Console.WriteLine($"Processed {processedFiles,5}/{totalFiles,5} files {processedFiles * 100D / totalFiles,5:F2}%, in {stopwatch.Elapsed.TotalSeconds:F1}s");
                                lastPrint.Restart();
                            }
                            foreach (Match match in Regex.Matches(line, entry.Pattern))
                            {
                                if (match.Success)
                                {
                                    var group = match.Groups[MappingsEntry.GROUP_NAME];
                                    foreach (Capture capture in group.Captures)
                                    {
                                        var value = capture.Value;
                                        var replacement = entry.Mapper.GetReplacementData(value, mappings.GeneratedClasses);
                                        var theClass = entry.Mapper.GetClass();
                                        if (replacement == null)
                                        {
                                            if (ignoreFailed) continue;
                                            fails++;
                                        }
                                        matches.Add(new ReportMatch(entry.Pattern, capture.Index, capture.Length, value, replacement == null ? null : theClass.MemberPath + "." + replacement, theClass.FilePath, theClass.MemberPath, entry.Mapper.GetConstantType(mappings.GeneratedClasses)));
                                        total++;
                                    }
                                }
                            }
                        }
                        if (matches.Count > 0)
                        {
                            matches.Sort((a, b) => a.MatchStart.CompareTo(b.MatchStart));
                            reportEntries.Add(new ReportEntry(lineNumber, memberName, line, matches));
                            totalEntries++;
                        }
                        lineNumber++;
                    }
                }
                if (reportEntries.Count > 0)
                {
                    reportDict.Add(relativeFile, reportEntries);
                }
                processedFiles++;
            }

            var report = new Report(total, fails, totalEntries, reportDict);
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
