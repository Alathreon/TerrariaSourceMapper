using System.Text.RegularExpressions;

namespace TerrariaSourceMapper
{
    internal partial class Util
    {
        public static bool IsValidClassName(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            return CLASS_NAME_REGEX().IsMatch(input);
        }

        [GeneratedRegex(@"^[A-Z][A-Za-z0-9_]*$")]
        private static partial Regex CLASS_NAME_REGEX();
    }
}
