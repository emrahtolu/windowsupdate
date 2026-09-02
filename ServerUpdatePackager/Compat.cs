using System.Text;

namespace ServerUpdatePackager;

internal static class StringCompatExtensions
{
    public static bool Contains(this string source, string value, StringComparison comparison) =>
        source.IndexOf(value, comparison) >= 0;

    public static string Replace(this string source, string oldValue, string newValue, StringComparison comparison)
    {
        if (comparison == StringComparison.Ordinal) return source.Replace(oldValue, newValue);
        var sb = new StringBuilder();
        var start = 0;
        while (true)
        {
            var index = source.IndexOf(oldValue, start, comparison);
            if (index < 0) break;
            sb.Append(source, start, index - start);
            sb.Append(newValue);
            start = index + oldValue.Length;
        }
        sb.Append(source, start, source.Length - start);
        return sb.ToString();
    }

    public static string ReplaceOrdinalIgnoreCase(this string source, string oldValue, string newValue) =>
        Replace(source, oldValue, newValue, StringComparison.OrdinalIgnoreCase);
}
