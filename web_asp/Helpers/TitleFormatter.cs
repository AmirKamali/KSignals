using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Html;

namespace web_asp.Helpers;

public static class TitleFormatter
{
    private static readonly Regex BoldRegex = new(@"\*\*(.+?)\*\*|__(.+?)__", RegexOptions.Compiled);
    private static readonly Regex ItalicRegex = new(@"\*(.+?)\*|_(.+?)_", RegexOptions.Compiled);
    private static readonly Regex CodeRegex = new(@"`(.+?)`", RegexOptions.Compiled);

    public static HtmlString FormatTitle(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new HtmlString(string.Empty);
        }

        var encoded = HtmlEncoder.Default.Encode(text);
        var formatted = ApplyInlineFormatting(encoded);
        return new HtmlString(formatted);
    }

    private static string ApplyInlineFormatting(string input)
    {
        var builder = new StringBuilder(input);

        builder = new StringBuilder(BoldRegex.Replace(builder.ToString(), m =>
        {
            var value = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            return $"<strong>{value}</strong>";
        }));

        builder = new StringBuilder(ItalicRegex.Replace(builder.ToString(), m =>
        {
            var value = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            return $"<em>{value}</em>";
        }));

        builder = new StringBuilder(CodeRegex.Replace(builder.ToString(), m => $"<code>{m.Groups[1].Value}</code>"));

        return builder.ToString();
    }
}
