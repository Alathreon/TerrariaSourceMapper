namespace TerrariaSourceMapper.mappings.mapper
{
    internal record SelectionMapper(Dictionary<string, string> Mappings, string Name) : GeneratedClassMapperBase(Name)
    {
        public override string? GetReplacementData(string value)
        {
            return Mappings.TryGetValue(value, out var result) ? result : null;
        }

        public override void Init(string path)
        {
        }
    }
}
