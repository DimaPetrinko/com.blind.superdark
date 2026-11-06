#nullable disable
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
        private const int GeneratorVersion = 5;
        private const string PackageRoot = "Packages/com.blind.superdark";
        private const string UIToolkitOverridesFolder = PackageRoot + "/Editor/Generated/UIToolkit";
        private const string CatalogOverridesAssetPath = PackageRoot + "/Editor/StyleSheets/Extensions/Generated/dark.uss";
        private const string FingerprintPath = "Library/SuperDarkEditor/fingerprint.txt";
        private const string RootSelector = ":root";

        private static readonly Regex _unsafeFileNameCharacters = new Regex(@"[^A-Za-z0-9_-]");

        static OverrideRegeneration()
        {
            ScheduleStalenessCheck();
            Events.registeredPackages += _ => ScheduleStalenessCheck();
        }

        public static event Action Regenerated;

        public static string UIToolkitOverrideAssetPath(string sourceSheetName) =>
            $"{UIToolkitOverridesFolder}/{_unsafeFileNameCharacters.Replace(sourceSheetName, "_")}.uss";

        private static PackageInfo OwnPackage => PackageInfo.FindForAssembly(typeof(OverrideRegeneration).Assembly);

        private static void ScheduleStalenessCheck()
        {
            EditorApplication.update -= RegenerateIfStaleOnceEditorIsIdle;
            EditorApplication.update += RegenerateIfStaleOnceEditorIsIdle;
        }

        private static void RegenerateIfStaleOnceEditorIsIdle()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || OwnPackage == null)
                return;

            EditorApplication.update -= RegenerateIfStaleOnceEditorIsIdle;
            RegenerateIfStale();
        }

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
            var source = OwnPackage.source;
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

            var catalogSheets = UnityThemeSources.LoadBuiltInCatalogSheets()
                .Concat(UnityThemeSources.FindPackageCatalogPaths(PackageRoot).Select(AssetDatabase.LoadAssetAtPath<StyleSheet>))
                .Where(styleSheet => styleSheet != null);

            var uiToolkitRootVariables = DarkOverrideBuilder.Build(ParseSheet(UnityThemeSources.LoadUIToolkitDarkTheme()))
                .Where(declaration => declaration.Selector == RootSelector);
            var catalogOverrides = DarkOverrideBuilder.Build(UssRuleSet.Combine(catalogSheets.Select(ParseSheet)));
            catalogOverrides.MergeFrom(uiToolkitRootVariables);
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
            var validOverrides = merged.Where(declaration => UnityThemeSources.IsValidUssProperty(declaration.Property));

            if (validOverrides.IsEmpty)
                return DeleteWithMeta(filePath);

            var validUss = validOverrides.ToUss();
            if (validUss == existingUss)
                return false;

            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, validUss);
            return true;
        }

        private static bool DeleteWithMeta(string filePath)
        {
            if (!File.Exists(filePath))
                return false;

            File.Delete(filePath);
            File.Delete(filePath + ".meta");
            return true;
        }

        private static string ToFilePath(string assetPath)
        {
            var packageDirectory = OwnPackage.resolvedPath;
            return Path.Combine(packageDirectory, assetPath.Substring(PackageRoot.Length + 1));
        }
    }
}
