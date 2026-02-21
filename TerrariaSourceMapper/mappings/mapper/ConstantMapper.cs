namespace TerrariaSourceMapper.mappings.mapper
{
    internal record ConstantMapper(string Constant, string Name, string ConstantType) : GeneratedClassMapperBase(Name)
    {

        public override string? GetReplacementData(string value, Dictionary<string, GeneratedClass> generatedClasses)
        {
            return Constant;
        }
        public override string GetConstantType(Dictionary<string, GeneratedClass> generatedClasses)
        {
            return ConstantType;
        }

        public override void Init(string path)
        {
        }
    }
}
