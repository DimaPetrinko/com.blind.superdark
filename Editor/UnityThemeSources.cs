using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine.UIElements;

namespace SuperDarkEditor
{
    internal static class UnityThemeSources
    {
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string ExtensionsFolderMarker = "/StyleSheets/Extensions/";
        private const string EditorResourcesPath = "Library/unity editor resources";
        private const string PackagesFolder = "Packages";

        private static readonly Type _editorResourcesType = EditorType("UnityEditor.Experimental.EditorResources");
        private static readonly Type _ussExportOptionsType = EditorType("UnityEditor.StyleSheets.UssExportOptions");
        private static readonly string[] _skippedSheetNameParts = { "light", "inlinestyle" };

        public static IEnumerable<string> BuiltInCatalogPaths =>
            ((IEnumerable)_editorResourcesType.GetMethod("GetDefaultStyleCatalogPaths", AnyStatic).Invoke(null, null)).Cast<string>();

        public static StyleSheet LoadUIToolkitDarkTheme() =>
            (StyleSheet)EditorType("UnityEditor.UIElements.UIElementsEditorUtility")
                .GetMethod("GetCommonDarkStyleSheet", AnyStatic)
                .Invoke(null, null);

        public static IEnumerable<StyleSheet> LoadUIToolkitDarkSheets(string excludedPackageRoot)
        {
            var builtInSheets = AssetDatabase.LoadAllAssetsAtPath(EditorResourcesPath).OfType<StyleSheet>();
            var packageSheets = AssetDatabase.FindAssets("t:StyleSheet", new[] { PackagesFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !path.StartsWith(excludedPackageRoot, StringComparison.Ordinal) && !path.Contains(ExtensionsFolderMarker))
                .Select(AssetDatabase.LoadAssetAtPath<StyleSheet>);

            return builtInSheets
                .Concat(packageSheets)
                .Append(LoadUIToolkitDarkTheme())
                .Where(sheet => sheet != null && !IsSkippedSheet(sheet))
                .Distinct();
        }

        public static IEnumerable<string> FindPackageCatalogPaths(string excludedPackageRoot)
        {
            var isEditorStyleSheet = _editorResourcesType.GetMethod("IsEditorStyleSheet", AnyStatic);

            return AssetDatabase.GetAllAssetPaths()
                .Where(path => path.Contains(ExtensionsFolderMarker) && !path.StartsWith(excludedPackageRoot, StringComparison.Ordinal))
                .Where(path => (bool)isEditorStyleSheet.Invoke(null, new object[] { path }))
                .OrderBy(path => path, StringComparer.Ordinal);
        }

        public static string ToUssSkippingUnexportableRules(StyleSheet styleSheet)
        {
            var complexSelectorToUss = EditorType("UnityEditor.StyleSheets.StyleSheetToUss")
                .GetMethods(AnyStatic)
                .Single(IsComplexSelectorToUss);
            var exportOptions = Activator.CreateInstance(_ussExportOptionsType, nonPublic: true);

            var uss = new StringBuilder();
            foreach (var complexSelector in ComplexSelectorsOf(styleSheet))
            {
                var ruleUss = new StringBuilder();
                try
                {
                    complexSelectorToUss.Invoke(null, new[] { styleSheet, exportOptions, complexSelector, ruleUss });
                    uss.Append(ruleUss).Append('\n');
                }
                catch (TargetInvocationException)
                {
                }
            }

            return uss.ToString();
        }

        private static IEnumerable<object> ComplexSelectorsOf(StyleSheet styleSheet)
        {
            var rules = (IEnumerable)typeof(StyleSheet).GetProperty("rules", AnyInstance).GetValue(styleSheet);

            return rules.Cast<object>()
                .SelectMany(rule => ((IEnumerable)rule.GetType().GetProperty("complexSelectors", AnyInstance).GetValue(rule)).Cast<object>());
        }

        private static bool IsComplexSelectorToUss(MethodInfo method)
        {
            var parameters = method.GetParameters();

            return method.Name == "ToUssString"
                && parameters.Length == 4
                && parameters[0].ParameterType == typeof(StyleSheet)
                && parameters[2].ParameterType.Name == "StyleComplexSelector"
                && parameters[3].ParameterType == typeof(StringBuilder);
        }

        private static bool IsSkippedSheet(StyleSheet sheet)
        {
            var lowerCaseName = sheet.name.ToLowerInvariant();
            return _skippedSheetNameParts.Any(lowerCaseName.Contains);
        }

        private static Type EditorType(string fullName) => typeof(EditorWindow).Assembly.GetType(fullName, throwOnError: true);
    }
}
