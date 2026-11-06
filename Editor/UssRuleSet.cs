using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SuperDarkEditor
{
    internal sealed class UssRuleSet
    {
        private static readonly Regex _commentPattern = new Regex(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex _rulePattern = new Regex(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.Compiled);
        private static readonly Regex _declarationPattern = new Regex(@"(?<property>[-\w]+)\s*:\s*(?<value>[^;]+);", RegexOptions.Compiled);

        private readonly List<string> _selectors = new List<string>();
        private readonly Dictionary<string, Dictionary<string, string>> _propertiesBySelector = new Dictionary<string, Dictionary<string, string>>();

        public bool IsEmpty => _selectors.Count == 0;

        public IEnumerable<(string Selector, string Property, string Value)> Declarations =>
            _selectors.SelectMany(selector => _propertiesBySelector[selector].Select(property => (selector, property.Key, property.Value)));

        public static UssRuleSet Parse(string uss)
        {
            var ruleSet = new UssRuleSet();
            var ussWithoutComments = _commentPattern.Replace(uss, string.Empty);

            foreach (Match rule in _rulePattern.Matches(ussWithoutComments))
            {
                var selector = rule.Groups["selector"].Value.Trim();
                foreach (Match declaration in _declarationPattern.Matches(rule.Groups["body"].Value))
                    ruleSet.Set(selector, declaration.Groups["property"].Value, declaration.Groups["value"].Value.Trim());
            }

            return ruleSet;
        }

        public static UssRuleSet Combine(IEnumerable<UssRuleSet> ruleSets)
        {
            var combined = new UssRuleSet();
            foreach (var ruleSet in ruleSets)
                combined.MergeFrom(ruleSet);

            return combined;
        }

        public void Set(string selector, string property, string value)
        {
            if (!_propertiesBySelector.TryGetValue(selector, out var properties))
            {
                properties = new Dictionary<string, string>();
                _propertiesBySelector.Add(selector, properties);
                _selectors.Add(selector);
            }

            properties[property] = value;
        }

        public void MergeFrom(UssRuleSet other)
        {
            foreach (var (selector, property, value) in other.Declarations)
                Set(selector, property, value);
        }

        public UssRuleSet OnlySelector(string selector)
        {
            var subset = new UssRuleSet();
            foreach (var (_, property, value) in Declarations.Where(declaration => declaration.Selector == selector))
                subset.Set(selector, property, value);

            return subset;
        }

        public string ToUss()
        {
            var builder = new StringBuilder();
            foreach (var selector in _selectors)
            {
                builder.Append(selector).Append(" {\n");
                foreach (var property in _propertiesBySelector[selector])
                    builder.Append("    ").Append(property.Key).Append(": ").Append(property.Value).Append(";\n");
                builder.Append("}\n\n");
            }

            return builder.ToString();
        }
    }
}
