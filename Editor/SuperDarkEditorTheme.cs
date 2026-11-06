#nullable disable
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SuperDarkEditor
{
    [InitializeOnLoad]
    internal static class SuperDarkEditorTheme
    {
        private const double ScanIntervalSeconds = 0.5;

        private static readonly Dictionary<string, StyleSheet> _overrideBySourceSheetName = new Dictionary<string, StyleSheet>();
        private static readonly HashSet<StyleSheet> _overrideSheets = new HashSet<StyleSheet>();
        private static double _nextScanTime;

        static SuperDarkEditorTheme()
        {
            EditorApplication.update += AttachOverridesPeriodically;
            OverrideRegeneration.Regenerated += ForgetLoadedOverrides;
        }

        private static IEnumerable<VisualElement> OpenPanelRoots =>
            Resources.FindObjectsOfTypeAll<EditorWindow>()
                .Where(window => window != null && window.rootVisualElement.panel != null)
                .Select(window => window.rootVisualElement.panel.visualTree)
                .Distinct();

        private static void AttachOverridesPeriodically()
        {
            if (EditorApplication.timeSinceStartup < _nextScanTime)
                return;

            _nextScanTime = EditorApplication.timeSinceStartup + ScanIntervalSeconds;

            var elementsWithStyleSheets = OpenPanelRoots
                .SelectMany(SelfAndDescendants)
                .Where(element => element.styleSheets.count > 0);

            foreach (var element in elementsWithStyleSheets)
                AttachOverridesAfterSources(element.styleSheets);
        }

        private static void AttachOverridesAfterSources(VisualElementStyleSheetSet styleSheets)
        {
            var attachedSheets = Enumerable.Range(0, styleSheets.count)
                .Select(index => styleSheets[index])
                .Where(sheet => sheet != null)
                .ToList();

            var sourceSheets = attachedSheets.Where(sheet => !_overrideSheets.Contains(sheet)).ToList();
            foreach (var sourceSheet in sourceSheets)
            {
                var overrideSheet = FindOverride(sourceSheet.name);
                if (overrideSheet == null || attachedSheets.IndexOf(overrideSheet) > attachedSheets.IndexOf(sourceSheet))
                    continue;

                styleSheets.Remove(overrideSheet);
                styleSheets.Add(overrideSheet);
                attachedSheets.Remove(overrideSheet);
                attachedSheets.Add(overrideSheet);
            }
        }

        private static StyleSheet FindOverride(string sourceSheetName)
        {
            if (_overrideBySourceSheetName.TryGetValue(sourceSheetName, out var cachedOverride) && IsKnownMissingOrAlive(cachedOverride))
                return cachedOverride;

            var loadedOverride = AssetDatabase.LoadAssetAtPath<StyleSheet>(OverrideRegeneration.UIToolkitOverrideAssetPath(sourceSheetName));
            _overrideBySourceSheetName[sourceSheetName] = loadedOverride;
            if (loadedOverride != null)
                _overrideSheets.Add(loadedOverride);

            return loadedOverride;
        }

        private static bool IsKnownMissingOrAlive(StyleSheet cachedOverride) =>
            ReferenceEquals(cachedOverride, null) || cachedOverride != null;

        private static void ForgetLoadedOverrides()
        {
            _overrideBySourceSheetName.Clear();
            _overrideSheets.Clear();
        }

        private static IEnumerable<VisualElement> SelfAndDescendants(VisualElement root)
        {
            var pending = new Stack<VisualElement>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                var element = pending.Pop();
                yield return element;

                for (var childIndex = 0; childIndex < element.hierarchy.childCount; childIndex++)
                    pending.Push(element.hierarchy[childIndex]);
            }
        }
    }
}
