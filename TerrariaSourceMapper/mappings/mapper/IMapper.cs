using System.Text.Json;
using System.Text.Json.Serialization;

namespace TerrariaSourceMapper.mappings.mapper
{
    internal interface IMapper
    {
        public static readonly string MAPPER_GENERATED_NAMESPACE = "TerrariaSourceMapper";
        string? GetReplacementData(string value, Dictionary<string, GeneratedClass> generatedClasses);
        ClassPath GetClass();
        string GetConstantType(Dictionary<string, GeneratedClass> generatedClasses);
        void Init(string path);

    }
    internal class MapperConverter : JsonConverter<IMapper>
    {
        public override IMapper? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            if (!root.TryGetProperty("@type", out var typeProp))
                throw new JsonException("Missing @type property.");

            string type = typeProp.GetString()!;

            return type switch
            {
                nameof(ClassConstantsMapper) => JsonSerializer.Deserialize<ClassConstantsMapper>(root.GetRawText(), options),
                nameof(ConstantMapper) => JsonSerializer.Deserialize<ConstantMapper>(root.GetRawText(), options),
                nameof(SelectionMapper) => JsonSerializer.Deserialize<SelectionMapper>(root.GetRawText(), options),
                _ => throw new NotSupportedException($"Unknown type {type}")
            };
        }

        public override void Write(Utf8JsonWriter writer, IMapper value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, (object)value, options);
        }
    }
}
