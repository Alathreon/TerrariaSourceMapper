using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TerrariaSourceMapper.mappings.mapper
{
    internal class ClassConstantsMapper : IMapper
    {
        private readonly string FilePath;
        private readonly List<string> MemberPath;
        private readonly string ConstantType;
        private readonly Dictionary<string, string> Mapping;
        private string? classNamespace;

        public ClassConstantsMapper(string FilePath, string ConstantType, List<string>? MemberPath = null)
        {
            this.FilePath = FilePath.Replace('\\', '/');
            if (!this.FilePath.EndsWith(".cs"))
            {
                this.FilePath += ".cs";
            }
            this.MemberPath = MemberPath ?? new List<string>();
            this.ConstantType = ConstantType;
            this.Mapping = [];
            int start = this.FilePath.LastIndexOf('/') + 1;
            int end = this.FilePath.LastIndexOf('.');
            this.MemberPath.Add(this.FilePath[start..end]);
        }

        public ClassPath GetClass()
        {
            return new ClassPath(classNamespace, string.Join('.', MemberPath));
        }

        public string GetConstantType(Dictionary<string, GeneratedClass> generatedClasses)
        {
            return ConstantType;
        }

        public string? GetReplacementData(string value, Dictionary<string, GeneratedClass> generatedClasses)
        {
            return Mapping.TryGetValue(value, out var result) ? result : null;
        }


        public void Init(string path)
        {
            SyntaxKind type = ConstantType switch
            {
                "int" => SyntaxKind.IntKeyword,
                "short" => SyntaxKind.ShortKeyword,
                "ushort" => SyntaxKind.UShortKeyword,
                _ => throw new ArgumentException($"Invalid type: {ConstantType}")
            };

            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(path, FilePath)));
            var root = tree.GetRoot();

            var topClass = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text == MemberPath[0]);

            this.classNamespace = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().First().Name.ToString();

            var targetClass = FindNestedClass(topClass, MemberPath, 1) ?? throw new ArgumentException($"Invalid class path: {string.Join(",", MemberPath)}");

            var fields = targetClass.Members
                .OfType<FieldDeclarationSyntax>()
                .Where(f =>
                    f.Modifiers.Any(SyntaxKind.PublicKeyword) &&
                    f.Modifiers.Any(SyntaxKind.ConstKeyword) &&
                    f.Declaration.Type is PredefinedTypeSyntax pts &&
                    pts.Keyword.IsKind(type)
                );
            foreach (var field in fields)
            {
                foreach (var variable in field.Declaration.Variables)
                {
                    string name = variable.Identifier.Text;
                    var valueExpression = variable.Initializer?.Value;
                    long value;
                    if (valueExpression is LiteralExpressionSyntax literal)
                    {
                        value = Convert.ToInt64(literal.Token.Value!);
                    }
                    else if(valueExpression is PrefixUnaryExpressionSyntax prefix && prefix.OperatorToken.IsKind(SyntaxKind.MinusToken) &&
                            prefix.Operand is LiteralExpressionSyntax innerLiteral)
                    {
                        value = -Convert.ToInt64(innerLiteral.Token.Value!);
                    } else
                    {
                        throw new ArgumentException($"Couldn't find constant value for constant: {MemberPath}.{name}");
                    }
                    Mapping[value.ToString()] = name;
                }
            }
        }

        private ClassDeclarationSyntax? FindNestedClass(ClassDeclarationSyntax? current, List<string> path, int index)
        {
            if (current == null || index >= path.Count())
                return current;

            var next = current.Members
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text == path[index]);

            return FindNestedClass(next, path, index + 1);
        }
    }
    class ClassConstantsMapperConverter : JsonConverter<ClassConstantsMapper>
    {
        public override ClassConstantsMapper Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            string FilePath = root.GetProperty("FilePath").GetString() ?? throw new JsonException("ClassPath cannot be null");
            string ConstantType = root.GetProperty("ConstantType").GetString() ?? throw new JsonException("ConstantType cannot be null");
            List<string>? memberList = null;
            if (root.TryGetProperty("MemberList", out var memberListProp) && memberListProp.ValueKind == JsonValueKind.Array)
            {
                memberList = new List<string>();
                foreach (var item in memberListProp.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                        memberList.Add(item.GetString()!);
                }
            }

            return new ClassConstantsMapper(FilePath, ConstantType);
        }

        public override void Write(Utf8JsonWriter writer, ClassConstantsMapper value, JsonSerializerOptions options)
        {
            throw new NotSupportedException("Serialization of ClassConstantsMapper is not supported.");
        }
    }
}
