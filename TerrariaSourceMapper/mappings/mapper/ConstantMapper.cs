namespace TerrariaSourceMapper.mappings.mapper
{
    internal record ConstantMapper(string Constant, string Name) : GeneratedClassMapperBase(Name)
    {
        public override string? GetReplacementData(string value)
        {
            return Constant;
        }

        public override void Init(string path)
        {
        }
    }
}
