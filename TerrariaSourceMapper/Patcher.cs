using System.Diagnostics;
using System.Text.Json;
using TerrariaSourceMapper.report;

namespace TerrariaSourceMapper
{
    internal class Patcher
    {
        public static void Patch(string source, string destination)
        {
            var destinationProject = Path.Combine(destination, new DirectoryInfo(source).Name + "_patched");
            Console.WriteLine($"Patching from '{source}' to '{destinationProject}'");
            CopyDirectory(source, destinationProject);
            var report = JsonSerializer.Deserialize<Report>(File.ReadAllText(Path.Combine(destination, "report.json"))) ?? throw new ArgumentException($"Couldn't find report at {destination}/report.json"); ;

            Console.WriteLine($"{report.Total - report.Failed}/{report.Total} modifications found");
            Stopwatch stopwatch = Stopwatch.StartNew();
            Stopwatch lastPrint = Stopwatch.StartNew();
            var classes = new SortedDictionary<string, SortedSet<ConstantEntry>>();
            int processed = 0;
            foreach (var fileEntry in report.Files)
            {
                var file = fileEntry.Key;
                var entries = fileEntry.Value;
                var lines = new List<string>(File.ReadAllLines(Path.Combine(source, file)));
                var generatedImports = new SortedSet<string>();
                foreach (var entry in entries)
                {
                    if (entry.NewContent != null)
                    {
                        lines[entry.Line] = entry.NewContent;
                        if (entry.ConstantNamespace == null)
                        {
                            if (!classes.ContainsKey(entry.ConstantClass))
                            {
                                classes[entry.ConstantClass] = [];
                            }
                            classes[entry.ConstantClass].Add(new ConstantEntry(entry.Replacement ?? throw new InvalidOperationException(), entry.ConstantType, entry.Match));
                            generatedImports.Add("using " + GENERATED_NAMESPACE + ";");
                        }
                        else
                        {
                            generatedImports.Add("using " + entry.ConstantNamespace + ";");
                        }
                    }

                    if (lastPrint.Elapsed.TotalSeconds >= 1)
                    {
                        double percent = (processed / (double)report.Total) * 100;
                        Console.WriteLine($"Processed {processed,5}/{report.Total,5} files {processed * 100D / report.Total,5:F2}%, in {stopwatch.Elapsed.TotalSeconds:F1}s");
                        lastPrint.Restart();
                    }
                    processed++;
                }
                lines.InsertRange(0, generatedImports);
                File.WriteAllLines(Path.Combine(destinationProject, file), lines);
            }
            var generatedSource = "namespace " + GENERATED_NAMESPACE + Environment.NewLine + "{" + Environment.NewLine;
            foreach (var c in classes)
            {
                generatedSource += "    internal class " + c.Key + Environment.NewLine + "    {" + Environment.NewLine;
                foreach (var entry in c.Value)
                {
                    generatedSource += "        public static readonly " + entry.FieldType + " " + entry.FieldName + " = " + entry.Value + ";" + Environment.NewLine;
                }
                generatedSource += "    }" + Environment.NewLine;
            }
            generatedSource += "}" + Environment.NewLine;
            Directory.CreateDirectory(Path.Combine(destinationProject, GENERATED_DIRECTORY));
            File.WriteAllText(Path.Combine(destinationProject, GENERATED_FILE), generatedSource);
            Console.WriteLine($"Patching done");
            Console.WriteLine($"{classes.Count} classes created");
        }
        private static void CopyDirectory(string source, string destination)
        {
            Console.WriteLine("Copying...");
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(source, file);
                var destinationPath = Path.Combine(destination, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                File.Copy(file, destinationPath, overwrite: true);
            }
        }
        private record ConstantEntry(string FieldName, string FieldType, string Value) : IComparable<ConstantEntry>
        {
            public int CompareTo(ConstantEntry? other)
            {
                return other == null ? 1 : FieldName.CompareTo(other.FieldName);
            }
        }
        private static readonly string GENERATED_NAMESPACE_LASTNAME = "SourceMapper";
        private static readonly string GENERATED_NAMESPACE = "Terraria." + GENERATED_NAMESPACE_LASTNAME;
        private static readonly string GENERATED_FILE = Path.Combine(GENERATED_NAMESPACE, GENERATED_NAMESPACE_LASTNAME + ".cs");
        private static readonly string GENERATED_DIRECTORY = (Directory.GetParent(GENERATED_FILE) ?? throw new InvalidOperationException()).Name;
    }
}
