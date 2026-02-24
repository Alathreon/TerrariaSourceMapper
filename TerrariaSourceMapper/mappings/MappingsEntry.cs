using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TerrariaSourceMapper.mappings.mapper;

namespace TerrariaSourceMapper.mappings
{
    internal class MappingsEntry
    {
        public static readonly string GROUP_NAME = "match";
        public readonly List<string> Whitelist;
        public readonly List<string> Blacklist;
        public readonly string Pattern;
        public readonly string? MethodPattern;
        public readonly IMapper Mapper;
        public readonly bool Ignore;

        [JsonConstructor]
        public MappingsEntry(string Pattern, IMapper Mapper, List<string>? Whitelist = null, List<string>? Blacklist = null, string? MethodPattern = null, bool Ignore = false)
        {
            if (!new Regex(Pattern).GetGroupNames().Contains(GROUP_NAME))
            {
                throw new ArgumentException($"Pattern must contain the group '{GROUP_NAME}', but does not: {Pattern}");
            }
            if (MethodPattern != null)
            {
                new Regex(MethodPattern);
            }
            Whitelist ??= [];
            Blacklist ??= [];
            if (Whitelist.Where(e => Blacklist.Contains(e)).Count() > 0)
            {
                throw new ArgumentException($"Whitelist and Blacklist cannot have shared values: {Whitelist} and {Blacklist}");
            }
            this.Whitelist = [.. Whitelist.Select(p => p.Replace('/', '\\'))];
            this.Blacklist = [.. Blacklist.Select(p => p.Replace('/', '\\'))]; ;
            this.Pattern = Pattern;
            this.Mapper = Mapper;
            this.MethodPattern = MethodPattern;
            this.Ignore = Ignore;
        }
    }
}
