using System.Globalization;
using System.Linq;
using System.Text;

namespace NajkaLib
{
    public static class Extensions
    {
        public static char RemoveDiacritics(this char ch)
        {
            var s = new string(ch, 1);
            s = s.Normalize(NormalizationForm.FormKD);
            if (s.Length == 1 && s[0] == ch) return ch;
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                var cat = char.GetUnicodeCategory(c);
                if (cat == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormKC).First();
        }

        public static string RemoveDiacritics(this string s)
        {
            s = s.Normalize(NormalizationForm.FormKD);
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                var cat = char.GetUnicodeCategory(c);
                if (cat == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormKC);
        }
    }
}