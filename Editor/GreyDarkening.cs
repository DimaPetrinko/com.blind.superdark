#nullable disable
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SuperDarkEditor
{
    internal static class GreyDarkening
    {
        private const int MaxDarkenedChannel = 110;
        private const int MaxTintSpread = 6;
        private const int GreyRedGreenReduction = 1;
        private const int GreyBlueAboveRed = 4;
        private const float MaxByte = 255f;

        private static readonly Regex _colorLiteralPattern = new Regex(
            @"rgba?\(\s*(?<red>\d+)\s*,\s*(?<green>\d+)\s*,\s*(?<blue>\d+)\s*(?:,\s*(?<alpha>[\d.]+)\s*)?\)|#(?<hex>[0-9a-fA-F]{8}|[0-9a-fA-F]{6})\b",
            RegexOptions.Compiled);

        public static bool TryDarkenValue(string value, out string darkenedValue)
        {
            var changed = false;
            darkenedValue = _colorLiteralPattern.Replace(value, match =>
            {
                var darkened = DarkenLiteral(match);
                changed |= darkened != match.Value;
                return darkened;
            });

            return changed;
        }

        private static string DarkenLiteral(Match match)
        {
            var (red, green, blue, alpha) = ParseChannels(match);

            return TryDarken(red, green, blue, out var darkened)
                ? FormatColor(darkened.Red, darkened.Green, darkened.Blue, alpha)
                : match.Value;
        }

        private static (int Red, int Green, int Blue, string Alpha) ParseChannels(Match match)
        {
            var hex = match.Groups["hex"].Value;
            if (hex.Length == 0)
            {
                var alpha = match.Groups["alpha"].Success ? match.Groups["alpha"].Value : null;
                return (ParseInt(match.Groups["red"].Value), ParseInt(match.Groups["green"].Value), ParseInt(match.Groups["blue"].Value), alpha);
            }

            var hexAlpha = hex.Length == 8
                ? (ParseHexByte(hex, 6) / MaxByte).ToString("0.###", CultureInfo.InvariantCulture)
                : null;

            return (ParseHexByte(hex, 0), ParseHexByte(hex, 2), ParseHexByte(hex, 4), hexAlpha);
        }

        private static bool TryDarken(int red, int green, int blue, out (int Red, int Green, int Blue) darkened)
        {
            var brightest = Math.Max(red, Math.Max(green, blue));
            var darkest = Math.Min(red, Math.Min(green, blue));
            var isDarkNeutral = brightest > 0 && brightest <= MaxDarkenedChannel && brightest - darkest <= MaxTintSpread;

            if (!isDarkNeutral)
            {
                darkened = default;
                return false;
            }

            if (red == green && green == blue)
            {
                var redGreen = Math.Max(red / 2 - GreyRedGreenReduction, 0);
                darkened = (redGreen, redGreen, redGreen + GreyBlueAboveRed);
                return true;
            }

            darkened = (red / 2, green / 2, blue / 2);
            return true;
        }

        private static string FormatColor(int red, int green, int blue, string alpha) =>
            alpha == null
                ? $"rgb({red}, {green}, {blue})"
                : $"rgba({red}, {green}, {blue}, {alpha})";

        private static int ParseInt(string value) => int.Parse(value, CultureInfo.InvariantCulture);

        private static int ParseHexByte(string hex, int startIndex) =>
            int.Parse(hex.Substring(startIndex, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }
}
