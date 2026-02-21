namespace TerrariaSourceMapper.mappings.mapper
{
    internal record SelectionMapper(string Name) : GeneratedClassMapperBase(Name)
    {

        public override string? GetReplacementData(string value, Dictionary<string, GeneratedClass> generatedClasses)
        {
            return generatedClasses[Name].Entries.TryGetValue(value, out var result) ? result : null;
        }
        public override string GetConstantType(Dictionary<string, GeneratedClass> generatedClasses)
        {
            return generatedClasses[Name].ConstantType;
        }

        public override void Init(string path)
        {
        }
    }
}
