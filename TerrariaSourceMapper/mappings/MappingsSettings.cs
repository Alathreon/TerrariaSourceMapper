using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static TerrariaSourceMapper.mappings.MappingsSettings;

namespace TerrariaSourceMapper.mappings
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
    [JsonDerivedType(typeof(ExistingSettings), "Existing")]
    [JsonDerivedType(typeof(GeneratedSettings), "Generated")]
    internal record MappingsSettings(string Kind)
    {
        public record ExistingSettings(string Kind, string Namespace, List<string>? MemberPath = null) : MappingsSettings(Kind);
        public record GeneratedSettings(string Kind, Dictionary<string, string> Entries) : MappingsSettings(Kind);
    }

    internal class ExistingClassData
    {
        public readonly ClassPath ClassPath;
        public readonly Dictionary<string, string> Mapping;

        public ExistingClassData(ExistingSettings settings, string MappingsClass, string MappingsFieldType, string path)
        {
            var memberPath = (settings.MemberPath ?? []).Prepend(MappingsClass).ToList();

            SyntaxKind type = MappingsFieldType switch
            {
                "int" => SyntaxKind.IntKeyword,
                "short" => SyntaxKind.ShortKeyword,
                "ushort" => SyntaxKind.UShortKeyword,
                _ => throw new ArgumentException($"Invalid type: {MappingsFieldType}")
            };

            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(path, settings.Namespace, MappingsClass + ".cs")));
            var root = tree.GetRoot();

            var topClass = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text == MappingsClass);

            var classNamespace = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().First().Name.ToString();
            this.ClassPath = new ClassPath(classNamespace, string.Join('.', memberPath));

            var targetClass = FindNestedClass(topClass, memberPath, 1) ?? throw new ArgumentException($"Invalid class path: {string.Join(".", memberPath)}");

            var fields = targetClass.Members
                .OfType<FieldDeclarationSyntax>()
                .Where(f =>
                    f.Modifiers.Any(SyntaxKind.PublicKeyword) &&
                    f.Modifiers.Any(SyntaxKind.ConstKeyword) &&
                    f.Declaration.Type is PredefinedTypeSyntax pts &&
                    pts.Keyword.IsKind(type)
                );

            this.Mapping = [];
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
                    else if (valueExpression is PrefixUnaryExpressionSyntax prefix && prefix.OperatorToken.IsKind(SyntaxKind.MinusToken) &&
                            prefix.Operand is LiteralExpressionSyntax innerLiteral)
                    {
                        value = -Convert.ToInt64(innerLiteral.Token.Value!);
                    }
                    else
                    {
                        throw new ArgumentException($"Couldn't find constant value for constant: {memberPath}.{name}");
                    }
                    Mapping[value.ToString()] = name;
                }
            }
        }

        private static ClassDeclarationSyntax? FindNestedClass(ClassDeclarationSyntax? current, List<string> path, int index)
        {
            if (current == null || index >= path.Count())
                return current;

            var next = current.Members
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text == path[index]);

            return FindNestedClass(next, path, index + 1);
        }
    }
}
