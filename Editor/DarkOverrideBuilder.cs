#nullable disable
using System;
using System.Linq;

namespace SuperDarkEditor
{
    internal static class DarkOverrideBuilder
    {
        private const string CustomPropertyPrefix = "--";

        private static readonly string[] _surfaceKeywords = { "background", "border", "separator", "hover", "selected" };
        private static readonly string[] _excludedKeywords = { "text", "tint", "font" };

        public static UssRuleSet Build(UssRuleSet theme)
        {
            var overrides = new UssRuleSet();
            foreach (var (selector, property, value) in theme.Declarations.Where(declaration => IsSurfaceColorProperty(declaration.Property)))
            {
                if (GreyDarkening.TryDarkenValue(value, out var darkenedValue))
                    overrides.Set(selector, property, darkenedValue);
            }

            return overrides;
        }

        private static bool IsSurfaceColorProperty(string property) =>
            !_excludedKeywords.Any(property.Contains)
            && (_surfaceKeywords.Any(property.Contains) || IsCustomColorProperty(property));

        private static bool IsCustomColorProperty(string property) =>
            property.StartsWith(CustomPropertyPrefix, StringComparison.Ordinal) && property.Contains("color");
    }
}
