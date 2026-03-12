using TerrariaSourceMapper;

class Program
{
    public static readonly Random RNG = new Random();

    private static readonly string HELP = """
        Usage: [options] [source folder]

        Required action (you may choose several):
          -a,  --analyze         Analyze the source code and build a report.
          -p,  --patch           Patch the source code from the report.
          -c, --compare          Compare the current report with the previous backup report and output the result in a report diff file
          -x, --compare-external Compare the current report with the previous backup report in VSCode

        Other options:
          -d,  --destination     Destination of the report or patch.
                                 Defaults to the parent folder of the target.
          -i   --ignore-failed   When Analyzing, omit failed replacements in the report
          -h,  --help            Print this help.
        """;

    static void Main(string[] args)
    {
        if (args.Contains("-h") || args.Contains("--help"))
        {
            Console.WriteLine(HELP);
            return;
        }

        bool analyze = args.Contains("-a") || args.Contains("--analyze");
        bool patch = args.Contains("-p") || args.Contains("--patch");
        bool compare = args.Contains("-c") || args.Contains("--compare");
        bool compareExternal = args.Contains("-x") || args.Contains("--compare-external");

        bool ignoreFailed = args.Contains("-i") || args.Contains("--ignore-failed");

        if (!analyze && !patch && !compare && !compareExternal)
            throw new ArgumentException("You must specify at least one of -a -p -c -x.");

        string? destination = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-d" || args[i] == "--destination")
            {
                if (i + 1 >= args.Length)
                    throw new ArgumentException("Missing value for -d / --destination");

                destination = args[i + 1];
            }
        }

        int count = (analyze ? 1 : 0) + (patch ? 1 : 0) + (destination != null ? 2 : 0);

        if (count == args.Length)
        {
            throw new ArgumentException("Missing source path directory");
        }

        string sourcePath = args[args.Length - 1];
        if (!Directory.Exists(sourcePath))
        {
            throw new ArgumentException($"Directory {sourcePath} doesn't exist");
        }

        destination = destination ??
                                  Directory.GetParent(sourcePath)?.FullName
                                  ?? throw new InvalidOperationException("Unable to determine parent directory.");

        if (analyze)
        {
            Console.WriteLine("Analyzing starting");
            Analyzer.Analyze(sourcePath, destination, ignoreFailed);
            Console.WriteLine();
        }
        if (compare)
        {
            Console.WriteLine("Comparing starting");
            Comparer.Compare(destination);
            Console.WriteLine();
        }
        if (compareExternal)
        {
            Console.WriteLine("Comparing external starting");
            Comparer.CompareExternal(destination);
            Console.WriteLine();
        }
        if (patch)
        {
            Console.WriteLine("Patching starting");
            Patcher.Patch(sourcePath, destination);
            Console.WriteLine();
        }
    }
}
