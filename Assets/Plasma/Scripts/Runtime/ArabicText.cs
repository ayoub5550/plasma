using System.Collections.Generic;
using System.Text;

namespace Plasma
{
    /// <summary>
    /// Minimal Arabic shaping for Unity's legacy text (uGUI Text / our WorldText), which renders
    /// code points left-to-right without contextual forms. Converts letters to their
    /// Presentation Forms-B glyphs (isolated/final/initial/medial + lam-alef ligatures), drops
    /// diacritics, then reverses each line for right-to-left display while keeping digit / Latin
    /// runs in reading order. Needs a font with Presentation Forms-B (Lalezar has them).
    /// </summary>
    public static class ArabicText
    {
        // base letter -> (isolated, final, initial, medial); 0 = form does not exist (non-joining forward)
        static readonly Dictionary<char, char[]> Forms = new Dictionary<char, char[]>
        {
            { '\u0621', new[] { '\u0621', '\u0000', '\u0000', '\u0000' } },
            { '\u0622', new[] { '\u0622', '\uFE82', '\u0000', '\u0000' } },
            { '\u0623', new[] { '\u0623', '\uFE84', '\u0000', '\u0000' } },
            { '\u0624', new[] { '\u0624', '\uFE86', '\u0000', '\u0000' } },
            { '\u0625', new[] { '\u0625', '\uFE88', '\u0000', '\u0000' } },
            { '\u0626', new[] { '\u0626', '\uFE8A', '\uFE8B', '\uFE8C' } },
            { '\u0627', new[] { '\u0627', '\uFE8E', '\u0000', '\u0000' } },
            { '\u0628', new[] { '\u0628', '\uFE90', '\uFE91', '\uFE92' } },
            { '\u0629', new[] { '\u0629', '\uFE94', '\u0000', '\u0000' } },
            { '\u062A', new[] { '\u062A', '\uFE96', '\uFE97', '\uFE98' } },
            { '\u062B', new[] { '\u062B', '\uFE9A', '\uFE9B', '\uFE9C' } },
            { '\u062C', new[] { '\u062C', '\uFE9E', '\uFE9F', '\uFEA0' } },
            { '\u062D', new[] { '\u062D', '\uFEA2', '\uFEA3', '\uFEA4' } },
            { '\u062E', new[] { '\u062E', '\uFEA6', '\uFEA7', '\uFEA8' } },
            { '\u062F', new[] { '\u062F', '\uFEAA', '\u0000', '\u0000' } },
            { '\u0630', new[] { '\u0630', '\uFEAC', '\u0000', '\u0000' } },
            { '\u0631', new[] { '\u0631', '\uFEAE', '\u0000', '\u0000' } },
            { '\u0632', new[] { '\u0632', '\uFEB0', '\u0000', '\u0000' } },
            { '\u0633', new[] { '\u0633', '\uFEB2', '\uFEB3', '\uFEB4' } },
            { '\u0634', new[] { '\u0634', '\uFEB6', '\uFEB7', '\uFEB8' } },
            { '\u0635', new[] { '\u0635', '\uFEBA', '\uFEBB', '\uFEBC' } },
            { '\u0636', new[] { '\u0636', '\uFEBE', '\uFEBF', '\uFEC0' } },
            { '\u0637', new[] { '\u0637', '\uFEC2', '\uFEC3', '\uFEC4' } },
            { '\u0638', new[] { '\u0638', '\uFEC6', '\uFEC7', '\uFEC8' } },
            { '\u0639', new[] { '\u0639', '\uFECA', '\uFECB', '\uFECC' } },
            { '\u063A', new[] { '\u063A', '\uFECE', '\uFECF', '\uFED0' } },
            { '\u0641', new[] { '\u0641', '\uFED2', '\uFED3', '\uFED4' } },
            { '\u0642', new[] { '\u0642', '\uFED6', '\uFED7', '\uFED8' } },
            { '\u0643', new[] { '\u0643', '\uFEDA', '\uFEDB', '\uFEDC' } },
            { '\u0644', new[] { '\u0644', '\uFEDE', '\uFEDF', '\uFEE0' } },
            { '\u0645', new[] { '\u0645', '\uFEE2', '\uFEE3', '\uFEE4' } },
            { '\u0646', new[] { '\u0646', '\uFEE6', '\uFEE7', '\uFEE8' } },
            { '\u0647', new[] { '\u0647', '\uFEEA', '\uFEEB', '\uFEEC' } },
            { '\u0648', new[] { '\u0648', '\uFEEE', '\u0000', '\u0000' } },
            { '\u0649', new[] { '\u0649', '\uFEF0', '\u0000', '\u0000' } },
            { '\u064A', new[] { '\u064A', '\uFEF2', '\uFEF3', '\uFEF4' } },
        };

        static bool IsArabicLetter(char c) => Forms.ContainsKey(c) || c == '\u0640';
        static bool IsDiacritic(char c) => (c >= '\u064B' && c <= '\u065F') || c == '\u0670';
        static bool JoinsForward(char c) => c == '\u0640' || (Forms.TryGetValue(c, out var f) && f[2] != '\u0000');

        public static bool HasArabic(string s)
        {
            foreach (char c in s) if (c >= '\u0600' && c <= '\u06FF') return true;
            return false;
        }

        /// <summary>Shape + reorder for display. Safe to call on non-Arabic strings (returned unchanged).</summary>
        public static string Fix(string s)
        {
            if (string.IsNullOrEmpty(s) || !HasArabic(s)) return s;
            var lines = s.Split('\n');
            for (int i = 0; i < lines.Length; i++) lines[i] = Reorder(Shape(lines[i]));
            return string.Join("\n", lines);
        }

        static string Shape(string s)
        {
            var src = new List<char>(s.Length);
            foreach (char c in s) if (!IsDiacritic(c)) src.Add(c);
            var sb = new StringBuilder(src.Count);
            for (int i = 0; i < src.Count; i++)
            {
                char c = src[i];
                if (!Forms.TryGetValue(c, out var f)) { sb.Append(c); continue; }
                bool prev = i > 0 && JoinsForward(src[i - 1]);
                char next = i + 1 < src.Count ? src[i + 1] : '\0';
                // lam + alef ligature
                if (c == '\u0644' && (next == '\u0622' || next == '\u0623' || next == '\u0625' || next == '\u0627'))
                {
                    char iso = next == '\u0622' ? '\uFEF5' : next == '\u0623' ? '\uFEF7' : next == '\u0625' ? '\uFEF9' : '\uFEFB';
                    sb.Append(prev ? (char)(iso + 1) : iso);
                    i++;
                    continue;
                }
                bool nextJoin = f[2] != '\u0000' && IsArabicLetter(next);
                char o;
                if (prev && nextJoin) o = f[3];
                else if (prev) o = f[1] != '\u0000' ? f[1] : f[0];
                else if (nextJoin) o = f[2];
                else o = f[0];
                sb.Append(o == '\u0000' ? f[0] : o);
            }
            return sb.ToString();
        }

        static bool IsLtr(char c) => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '$' || c == '%' || c == '+' || c == '.' || c == ',' || c == ':' || c == '/';

        /// <summary>Reverses for RTL display; LTR runs (numbers, Latin) keep their internal order.</summary>
        static string Reorder(string s)
        {
            var chars = s.ToCharArray();
            System.Array.Reverse(chars);
            int i = 0;
            while (i < chars.Length)
            {
                if (!IsLtr(chars[i])) { i++; continue; }
                int j = i;
                while (j + 1 < chars.Length && (IsLtr(chars[j + 1]) || (chars[j + 1] == ' ' && j + 2 < chars.Length && IsLtr(chars[j + 2])))) j++;
                System.Array.Reverse(chars, i, j - i + 1);
                i = j + 1;
            }
            // mirror brackets
            for (int k = 0; k < chars.Length; k++)
            {
                if (chars[k] == '(') chars[k] = ')'; else if (chars[k] == ')') chars[k] = '(';
            }
            return new string(chars);
        }
    }
}
