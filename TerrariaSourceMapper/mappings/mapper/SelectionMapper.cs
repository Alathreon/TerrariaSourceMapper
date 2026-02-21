namespace TerrariaSourceMapper.mappings.mapper
{
    internal record SelectionMapper(string Name) : GeneratedClassMapperBase(Name)
    {
        public override string? GetReplacementData(string value, Dictionary<string, Dictionary<string, string>> generatedClasses)
        {
            return generatedClasses[Name].TryGetValue(value, out var result) ? result : null;
        }

        public override void Init(string path)
        {
        }
    }
}
