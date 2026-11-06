using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.UIElements;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace SuperDarkEditor
{
    [InitializeOnLoad]
    internal static class OverrideRegeneration
    {
        private const int GeneratorVersion = 4;
        private const string PackageRoot = "Packages/com.blind.superdark";
        private const string UIToolkitOverridesFolder = PackageRoot + "/Editor/Generated/UIToolkit";
        private const string CatalogOverridesAssetPath = PackageRoot + "/Editor/StyleSheets/Extensions/Generated/dark.uss";
        private const string FingerprintPath = "Library/SuperDarkEditor/fingerprint.txt";
        private const string RootSelector = ":root";

        private static readonly Regex _unsafeFileNameCharacters = new Regex(@"[^A-Za-z0-9_-]");

        static OverrideRegeneration()
        {
            EditorApplication.delayCall += RegenerateIfStale;
        }

        public static event Action Regenerated;

        public static string UIToolkitOverrideAssetPath(string sourceSheetName) =>
            $"{UIToolkitOverridesFolder}/{_unsafeFileNameCharacters.Replace(sourceSheetName, "_")}.uss";

        private static void RegenerateIfStale()
        {
            if (!EditorGUIUtility.isProSkin || !IsPackageWritable())
                return;

            try
            {
                var fingerprint = ComputeFingerprint();
                if (AreOverridesUpToDate(fingerprint))
                    return;

                Regenerate();

                Directory.CreateDirectory(Path.GetDirectoryName(FingerprintPath));
                File.WriteAllText(FingerprintPath, fingerprint);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Super Dark Editor: regenerating overrides failed, colors stay as they were.\n{exception}");
            }
        }

        private static bool IsPackageWritable()
        {
            var source = PackageInfo.FindForAssembly(typeof(OverrideRegeneration).Assembly).source;
            return source == PackageSource.Local || source == PackageSource.Embedded;
        }

        private static bool AreOverridesUpToDate(string fingerprint) =>
            File.Exists(FingerprintPath)
            && File.ReadAllText(FingerprintPath) == fingerprint
            && File.Exists(ToFilePath(CatalogOverridesAssetPath));

        private static string ComputeFingerprint() =>
            string.Join("\n", new[] { GeneratorVersion.ToString(), Application.unityVersion }
                .Concat(PackageInfo.GetAllRegisteredPackages()
                    .Select(package => package.name + "@" + package.version)
                    .OrderBy(packageId => packageId, StringComparer.Ordinal)));

        private static void Regenerate()
        {
            var uiToolkitChanged = UnityThemeSources.LoadUIToolkitDarkSheets(PackageRoot)
                .Select(sheet => (Sheet: sheet, Overrides: DarkOverrideBuilder.Build(ParseSheet(sheet))))
                .Where(entry => !entry.Overrides.IsEmpty)
                .Select(entry => WriteMergedOverrides(UIToolkitOverrideAssetPath(entry.Sheet.name), entry.Overrides))
                .ToList()
                .Any(changed => changed);

            var catalogSheets = UnityThemeSources.BuiltInCatalogPaths
                .Select(path => EditorGUIUtility.Load(path) as StyleSheet)
                .Concat(UnityThemeSources.FindPackageCatalogPaths(PackageRoot).Select(AssetDatabase.LoadAssetAtPath<StyleSheet>))
                .Where(styleSheet => styleSheet != null);

            var catalogOverrides = DarkOverrideBuilder.Build(UssRuleSet.Combine(catalogSheets.Select(ParseSheet)));
            catalogOverrides.MergeFrom(DarkOverrideBuilder.Build(ParseSheet(UnityThemeSources.LoadUIToolkitDarkTheme())).OnlySelector(RootSelector));
            var catalogChanged = WriteMergedOverrides(CatalogOverridesAssetPath, catalogOverrides);

            if (!uiToolkitChanged && !catalogChanged)
                return;

            AssetDatabase.Refresh();
            Regenerated?.Invoke();
        }

        private static UssRuleSet ParseSheet(StyleSheet styleSheet) =>
            UssRuleSet.Parse(UnityThemeSources.ToUssSkippingUnexportableRules(styleSheet));

        private static bool WriteMergedOverrides(string assetPath, UssRuleSet overrides)
        {
            var filePath = ToFilePath(assetPath);
            var existingUss = File.Exists(filePath) ? File.ReadAllText(filePath) : string.Empty;

            var merged = UssRuleSet.Parse(existingUss);
            merged.MergeFrom(overrides);
            var mergedUss = merged.ToUss();

            if (mergedUss == existingUss)
                return false;

            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, mergedUss);
            return true;
        }

        private static string ToFilePath(string assetPath)
        {
            var packageDirectory = PackageInfo.FindForAssembly(typeof(OverrideRegeneration).Assembly).resolvedPath;
            return Path.Combine(packageDirectory, assetPath.Substring(PackageRoot.Length + 1));
        }
    }
}
