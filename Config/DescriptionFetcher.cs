using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HalfSwordTweaker.Config;

public class DescriptionFetcher
{
    private static readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string _cacheFilePath;
    private static readonly HttpClient _httpClient;
    private const string DocsUrl = "https://dev.epicgames.com/documentation/en-us/unreal-engine/unreal-engine-console-variables-reference?application_version=5.4";
    private static bool _isInitialized = false;
    private static readonly object _lock = new();

    static DescriptionFetcher()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HalfSwordTweaker");
        Directory.CreateDirectory(appDataPath);
        _cacheFilePath = Path.Combine(appDataPath, "descriptions.json");

        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "HalfSwordTweaker/1.0");
    }

    public static void LoadCache()
    {
        try
        {
            if (File.Exists(_cacheFilePath))
            {
                var json = File.ReadAllText(_cacheFilePath);
                var cached = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (cached != null)
                {
                    foreach (var kvp in cached)
                    {
                        _cache[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
        catch
        {
            // Ignore cache load errors
        }
    }

    public static async Task FetchAllAsync(IProgress<string>? progress = null)
    {
        if (_isInitialized) return;

        lock (_lock)
        {
            if (_isInitialized) return;
            _isInitialized = true;
        }

        try
        {
            progress?.Report("Fetching UE5 console variable reference...");

            var html = await _httpClient.GetStringAsync(DocsUrl);
            
            // Debug: show some stats about the HTML
            var trCount = Regex.Matches(html, "<tr").Count;
            var codeCount = Regex.Matches(html, "<code").Count;
            progress?.Report($"HTML: {trCount} rows, {codeCount} code elements");
            
            var descriptions = ParseHtml(html);
            
            // Debug: show how many r.* variables we found
            var rCount = descriptions.Keys.Count(k => k.StartsWith("r."));
            progress?.Report($"Parsed {descriptions.Count} total, {rCount} r.* variables");

            lock (_lock)
            {
                foreach (var kvp in descriptions)
                {
                    _cache[kvp.Key] = kvp.Value;
                }
            }

            // Save to file
            var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_cacheFilePath, json);

            progress?.Report($"Loaded {_cache.Count} console variable descriptions");
        }
        catch (Exception ex)
        {
            progress?.Report($"Failed to fetch descriptions: {ex.Message}");
            // If we have cached data, still mark as initialized
            if (_cache.Count > 0)
            {
                progress?.Report($"Using {_cache.Count} cached descriptions");
            }
        }
    }

    public static string GetDescription(string settingName)
    {
        if (string.IsNullOrEmpty(settingName)) return string.Empty;

        // Check if initialized, if not try to load from file
        if (!_isInitialized && _cache.Count == 0)
        {
            LoadCache();
        }

        if (_cache.TryGetValue(settingName, out var description))
        {
            return description;
        }

        // Try with r. prefix if not found
        if (!settingName.StartsWith("r.") && !settingName.StartsWith("sg."))
        {
            var withPrefix = "r." + settingName;
            if (_cache.TryGetValue(withPrefix, out description))
            {
                return description;
            }
        }

        return string.Empty;
    }

    private static Dictionary<string, string> ParseHtml(string html)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Try multiple patterns to catch different table formats
        
        // Pattern 1: Strict 3-column format
        var pattern1 = new Regex(
            @"<tr[^>]*>\s*<td[^>]*><code>([^<]+)</code></td>\s*<td[^>]*>([^<]*)</td>\s*<td[^>]*>([^<]*)</td>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Pattern 2: Look for any tr with code and description (more lenient)
        var pattern2 = new Regex(
            @"<tr[^>]*>.*?<code>([^<]+)</code>.*?<td[^>]*>([^<]+?)</td>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Try pattern 1 first
        int count1 = 0;
        foreach (Match match in pattern1.Matches(html))
        {
            var name = match.Groups[1].Value.Trim();
            var description = match.Groups[3].Value.Trim();
            description = CleanHtml(description);

            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(description))
            {
                result[name] = description;
                count1++;
            }
        }

        // Try pattern 2 for additional entries
        foreach (Match match in pattern2.Matches(html))
        {
            var name = match.Groups[1].Value.Trim();
            var description = match.Groups[2].Value.Trim();
            description = CleanHtml(description);

            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(description) && !result.ContainsKey(name))
            {
                result[name] = description;
            }
        }

        return result;
    }

    private static string CleanHtml(string html)
    {
        // Remove all HTML tags
        html = Regex.Replace(html, "<[^>]+>", " ");
        // Collapse whitespace
        html = Regex.Replace(html, @"\s+", " ").Trim();
        return html;
    }
}
