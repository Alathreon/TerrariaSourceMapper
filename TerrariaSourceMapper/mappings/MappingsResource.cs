using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace TerrariaSourceMapper.mappings
{
    internal record MappingsResource(
        MappingsSettings MappingsSettings,
        string MappingsClass,
        string MappingsFieldType,
        List<MappingsEntry> Mappings)
    {
        private readonly string _name =
            Util.IsValidClassName(MappingsClass)
                ? MappingsClass
                : throw new ArgumentException($"Invalid class name '{MappingsClass}'");
    }
}
