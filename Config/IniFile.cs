namespace HalfSwordTweaker.Config;

/// <summary>
/// Represents an INI file and provides methods to read and write settings.
/// </summary>
public class IniFile
{
    private readonly string _filePath;
    private readonly Dictionary<string, Dictionary<string, string>> _sections;

    /// <summary>
    /// The path to the INI file.
    /// </summary>
    public string FilePath => _filePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="IniFile"/> class.
    /// </summary>
    /// <param name="filePath">The path to the INI file.</param>
    public IniFile(string filePath)
    {
        _filePath = filePath;
        _sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads the INI file from disk.
    /// </summary>
    /// <returns>True if the file was read successfully or doesn't exist yet; otherwise, false.</returns>
    public bool Read()
    {
        _sections.Clear();
        
        if (!File.Exists(_filePath))
        {
            return true;
        }

        string currentSection = string.Empty;

        try
        {
            foreach (var line in File.ReadLines(_filePath))
            {
                var trimmedLine = line.Trim();

                // Skip empty lines and comments
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";") || trimmedLine.StartsWith("//"))
                {
                    continue;
                }

                // Check for section header
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    currentSection = trimmedLine.Trim('[', ']');
                    _sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    continue;
                }

                // Parse key=value pairs
                if (!string.IsNullOrEmpty(currentSection) && TryParseKeyValue(trimmedLine, out var key, out var value))
                {
                    _sections[currentSection][key] = value;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Writes the INI file to disk.
    /// </summary>
    /// <param name="createBackup">Whether to create a backup before writing.</param>
    /// <returns>True if the file was written successfully; otherwise, false.</returns>
    public bool Write(bool createBackup = false)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (createBackup && File.Exists(_filePath))
            {
                CreateBackup();
            }

            var lines = new List<string>();

            foreach (var section in _sections)
            {
                lines.Add($"[{section.Key}]");

                foreach (var setting in section.Value)
                {
                    lines.Add($"{setting.Key}={setting.Value}");
                }

                lines.Add(string.Empty);
            }

            File.WriteAllLines(_filePath, lines);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets a setting value.
    /// </summary>
    /// <param name="section">The section name.</param>
    /// <param name="key">The setting name.</param>
    /// <param name="defaultValue">The default value if the setting is not found.</param>
    /// <returns>The setting value, or the default value if not found.</returns>
    public string? GetValue(string section, string key, string? defaultValue = null)
    {
        if (_sections.TryGetValue(section, out var settings) && settings.TryGetValue(key, out var value))
        {
            return value;
        }
        return defaultValue;
    }

    /// <summary>
    /// Gets a setting value, searching all sections if not found in the specified section.
    /// </summary>
    /// <param name="section">The preferred section name.</param>
    /// <param name="key">The setting name.</param>
    /// <param name="defaultValue">The default value if the setting is not found.</param>
    /// <returns>The setting value, or the default value if not found.</returns>
    public string? GetValueAuto(string section, string key, string? defaultValue = null)
    {
        if (_sections.TryGetValue(section, out var settings) && settings.TryGetValue(key, out var value))
        {
            return value;
        }

        foreach (var sec in _sections)
        {
            if (sec.Value.TryGetValue(key, out var foundValue))
            {
                return foundValue;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// Sets a setting value.
    /// </summary>
    /// <param name="section">The section name.</param>
    /// <param name="key">The setting name.</param>
    /// <param name="value">The value to set.</param>
    public void SetValue(string section, string key, string value)
    {
        if (!_sections.ContainsKey(section))
        {
            _sections[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        _sections[section][key] = value;
    }

    /// <summary>
    /// Deletes a setting.
    /// </summary>
    /// <param name="section">The section name.</param>
    /// <param name="key">The setting name.</param>
    public void DeleteValue(string section, string key)
    {
        if (_sections.TryGetValue(section, out var settings))
        {
            settings.Remove(key);
        }
    }

    /// <summary>
    /// Gets all sections in the INI file.
    /// </summary>
    /// <returns>A list of section names.</returns>
    public List<string> GetSections()
    {
        return _sections.Keys.ToList();
    }

    /// <summary>
    /// Gets all settings in a section.
    /// </summary>
    /// <param name="section">The section name.</param>
    /// <returns>A dictionary of settings.</returns>
    public Dictionary<string, string> GetSettings(string section)
    {
        return _sections.TryGetValue(section, out var settings) ? settings : new Dictionary<string, string>();
    }

    /// <summary>
    /// Creates a backup of the INI file.
    /// </summary>
    /// <param name="backupPath">The path for the backup file. If null, a default path is used.</param>
    /// <returns>The path to the backup file.</returns>
    public string CreateBackup(string? backupPath = null)
    {
        if (string.IsNullOrEmpty(backupPath))
        {
            backupPath = $"{_filePath}.backup_{DateTime.Now:yyyyMMdd_HHmmss}";
        }

        File.Copy(_filePath, backupPath, overwrite: true);
        return backupPath;
    }

    /// <summary>
    /// Tries to parse a key=value pair from a line.
    /// </summary>
    /// <param name="line">The line to parse.</param>
    /// <param name="key">The parsed key.</param>
    /// <param name="value">The parsed value.</param>
    /// <returns>True if parsing was successful; otherwise, false.</returns>
    private static bool TryParseKeyValue(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        var equalsIndex = line.IndexOf('=');
        if (equalsIndex <= 0)
        {
            return false;
        }

        key = line.Substring(0, equalsIndex).Trim();
        value = line.Substring(equalsIndex + 1).Trim();

        // Remove quotes if present
        if (value.StartsWith("\"") && value.EndsWith("\""))
        {
            value = value.Trim('"');
        }

        return !string.IsNullOrEmpty(key);
    }
}
