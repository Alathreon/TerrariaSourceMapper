namespace TerrariaSourceMapper.mappings.mapper
{
    internal abstract record GeneratedClassMapperBase(string Name) : IMapper
    {
        private readonly string _name =
            Util.IsValidClassName(Name)
                ? Name
                : throw new ArgumentException($"Invalid class name '{Name}'");

        public ClassPath GetClass()
        {
            return new ClassPath(null, _name);
        }

        public abstract string? GetReplacementData(string value, Dictionary<string, Dictionary<string, string>> generatedClasses);
        public abstract void Init(string path);
    }
}
