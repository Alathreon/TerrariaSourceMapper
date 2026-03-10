using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using TerrariaSourceMapper.mappings;
using TerrariaSourceMapper.report;
using static TerrariaSourceMapper.mappings.MappingsSettings;

namespace TerrariaSourceMapper
{
    internal class Analyzer
    {
        public static void Analyze(string source, string destination, bool ignoreFailed)
        {
            List<MappingsResource> mappers = ReadResources();
            int t = CountBindings(mappers);
            mappers = FilterBindings(mappers, (r, b) => !b.Ignore);
            Console.WriteLine($"Found {t} mappings, {t - CountBindings(mappers)} are ignored");

            Dictionary<string, Dictionary<string, ExistingClassData>> existingClassDataDict = [];
            foreach(var mapper in mappers)
            {
                if(mapper.MappingsSettings is ExistingSettings s)
                {
                    if(!existingClassDataDict.ContainsKey(s.Namespace))
                    {
                        existingClassDataDict.Add(s.Namespace, []);
                    }
                    var data = new ExistingClassData(s, mapper.MappingsClass, mapper.MappingsFieldType, source);
                    existingClassDataDict[s.Namespace].Add(mapper.MappingsClass, data);
                }
            }

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
                var fileMappings = FilterBindings(mappers, (r, b) =>
                       (b.Whitelist.Count == 0 || b.Whitelist.Contains(relativeFile))
                    && (b.Blacklist.Count == 0 || !b.Whitelist.Contains(relativeFile)));

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
                        memberMappings = FilterBindings(fileMappings, (r, b) => b.MethodPattern == null || Regex.IsMatch(method.Identifier.Text, b.MethodPattern));
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
                        foreach (var resource in memberMappings)
                        {
                            foreach(var entry in resource.Mappings)
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
                                            (string? replacement, ClassPath theClass) = ExecMapper(resource, value, existingClassDataDict);
                                            if (replacement == null)
                                            {
                                                if (ignoreFailed) continue;
                                                fails++;
                                            }
                                            matches.Add(new ReportMatch(entry.Pattern, capture.Index, capture.Length, value, replacement == null ? null : theClass.MemberPath + "." + replacement, theClass.FilePath, theClass.MemberPath, resource.MappingsFieldType));
                                            total++;
                                        }
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
            var options = new JsonSerializerOptions
            {
                WriteIndented = true // pretty print
            };
            File.WriteAllText(Path.Combine(destination, "report.json"), JsonSerializer.Serialize(report, options));
            Console.WriteLine($"Analyzing done");
            Console.WriteLine($"{total} matches found, {fails} fails");
        }
        private static List<MappingsResource> ReadResources()
        {
            var assembly = Assembly.GetExecutingAssembly();

            var mappers = new List<MappingsResource>();
            foreach (var resource in assembly.GetManifestResourceNames())
            {
                if (!resource.StartsWith(nameof(TerrariaSourceMapper) + ".Resources") || !resource.EndsWith(".json")) continue;
                using Stream stream = assembly.GetManifestResourceStream(resource) ?? throw new InvalidOperationException();
                using StreamReader reader = new(stream);
                string content = reader.ReadToEnd();

                var options = new JsonSerializerOptions
                {
                    IncludeFields = true,
                };
                MappingsResource mappings = JsonSerializer.Deserialize<MappingsResource>(content, options) ?? throw new InvalidOperationException();
                mappers.Add(mappings);
            }

            return mappers;
        }
        private static List<MappingsResource> FilterBindings(List<MappingsResource> resources, Func<MappingsResource, MappingsEntry, bool> filter)
        {
            return resources
                .Select(r => r with { Mappings = [.. r.Mappings.Where(m => filter(r, m))] })
                .Where(r => r.Mappings.Count > 0)
                .ToList();
        }
        private static int CountBindings(List<MappingsResource> resources)
        {
            return resources.Sum(m => m.Mappings.Count);
        }
        private static (string?, ClassPath) ExecMapper(MappingsResource resource, string value, Dictionary<string, Dictionary<string, ExistingClassData>> existingClassDataDict)
        {
            switch(resource.MappingsSettings)
            {
                case ExistingSettings e:
                    ExistingClassData existingClassData = existingClassDataDict[e.Namespace][resource.MappingsClass];
                    return (existingClassData.Mapping.TryGetValue(value, out var eResult) ? eResult : null, existingClassData.ClassPath);
                case GeneratedSettings g:
                    return (g.Entries.TryGetValue(value, out var gResult) ? gResult : null, new ClassPath(null, resource.MappingsClass));
                default:
                    throw new NotImplementedException();
            };
        } 
    }
}
