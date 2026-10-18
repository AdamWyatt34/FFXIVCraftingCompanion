using System.Text.RegularExpressions;
using System.Globalization;

namespace FFXIV.Export;

// Every icon the sync stores follows XIVAPI's asset URL for ui/icon/<folder>/<id>.tex; the
// search index keeps only the id and the frontend rebuilds the URL with the same template.
public static partial class IconUrl
{
    [GeneratedRegex(@"^https://v2\.xivapi\.com/api/asset\?path=ui%2Ficon%2F(\d{6})%2F(\d{6})\.tex&format=png$")]
    private static partial Regex Template();

    public static string FromId(int icon) =>
        $"https://v2.xivapi.com/api/asset?path=ui%2Ficon%2F{icon / 1000 * 1000:D6}%2F{icon:D6}.tex&format=png";

    public static int ToId(string url)
    {
        var match = Template().Match(url);
        if (!match.Success)
            throw new InvalidOperationException($"Icon URL does not follow the XIVAPI template: {url}");
        var icon = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        if (FromId(icon) != url)
            throw new InvalidOperationException($"Icon URL folder does not match its id: {url}");
        return icon;
    }
}
