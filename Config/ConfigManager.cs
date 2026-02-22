namespace HalfSwordTweaker.Config;

/// <summary>
/// Manages configuration files for Half Sword.
/// </summary>
public class ConfigManager
{
    private const string BackupFileName = "engine.ini.backup";

    private readonly string _configDirectory;
    private readonly string _engineIniPath;
    private readonly string _gameUserSettingsIniPath;
    private readonly string _backupPath;

    private IniFile? _engineIni;
    private IniFile? _gameUserSettingsIni;
    private bool _engineIniLoaded = false;

    /// <summary>
    /// Whether engine.ini has been loaded.
    /// </summary>
    public bool EngineIniLoaded => _engineIniLoaded;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigManager"/> class.
    /// </summary>
    public ConfigManager()
    {
        _configDirectory = FileHelper.GetConfigDirectory();
        _engineIniPath = FileHelper.GetEngineIniPath();
        _gameUserSettingsIniPath = FileHelper.GetGameUserSettingsIniPath();
        _backupPath = Path.Combine(_configDirectory, BackupFileName);

        // Only load GameUserSettings.ini initially - engine.ini is lazy-loaded
        _gameUserSettingsIni = new IniFile(_gameUserSettingsIniPath);
        _engineIni = null;
    }

    /// <summary>
    /// Ensures engine.ini is loaded (lazy loading).
    /// </summary>
    public void EnsureEngineIniLoaded()
    {
        if (!_engineIniLoaded)
        {
            LoadEngineIni();
        }
    }

    /// <summary>
    /// Loads engine.ini explicitly.
    /// </summary>
    public void LoadEngineIni()
    {
        _engineIni = new IniFile(_engineIniPath);
        _engineIni.Read();
        _engineIniLoaded = true;
    }

    /// <summary>
    /// Ensures engine.ini data is loaded without marking it as "loaded for writing".
    /// Used for reading composite settings like TSR without triggering full write.
    /// </summary>
    private void EnsureEngineIniDataLoaded()
    {
        if (_engineIni == null)
        {
            _engineIni = new IniFile(_engineIniPath);
            _engineIni.Read();
        }
    }

    /// <summary>
    /// Reads GameUserSettings.ini (and optionally engine.ini).
    /// </summary>
    /// <param name="loadEngineIni">Whether to also load engine.ini.</param>
    /// <returns>True if files were read successfully; otherwise, false.</returns>
    public bool ReadAll(bool loadEngineIni = false)
    {
        var gameUserSettingsRead = _gameUserSettingsIni?.Read() ?? false;
        
        if (loadEngineIni && !_engineIniLoaded)
        {
            LoadEngineIni();
        }
        
        if (loadEngineIni)
        {
            return gameUserSettingsRead && _engineIniLoaded;
        }
        
        return gameUserSettingsRead;
    }

    /// <summary>
    /// Writes all configuration files.
    /// </summary>
    /// <param name="createBackup">Whether to create a backup before writing.</param>
    /// <param name="setReadOnly">Whether to set engine.ini to read-only after writing.</param>
    /// <returns>True if all files were written successfully; otherwise, false.</returns>
    public bool WriteAll(bool createBackup = false, bool setReadOnly = true)
    {
        if (_gameUserSettingsIni == null)
        {
            return false;
        }

        try
        {
            // Write engine.ini only if it was loaded/modified
            if (_engineIni != null)
            {
                // Create backup if needed (only if source file exists)
                if (createBackup && File.Exists(_engineIniPath) && !File.Exists(_backupPath))
                {
                    FileHelper.CreateBackup(_engineIniPath, _backupPath);
                }

                // Write engine.ini
                _engineIni.Write(createBackup);

                // Set read-only flag for engine.ini
                if (setReadOnly)
                {
                    FileHelper.SetFileReadOnly(_engineIniPath, true);
                }
            }

            // Write GameUserSettings.ini (no backup needed)
            _gameUserSettingsIni.Write(false);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if engine.ini is read-only.
    /// </summary>
    /// <returns>True if engine.ini is read-only; otherwise, false.</returns>
    public bool IsEngineIniReadOnly()
    {
        return FileHelper.IsFileReadOnly(_engineIniPath);
    }

    /// <summary>
    /// Removes the read-only flag from engine.ini.
    /// </summary>
    public void RemoveEngineIniReadOnly()
    {
        FileHelper.SetFileReadOnly(_engineIniPath, false);
    }

    /// <summary>
    /// Checks if the configuration directory exists.
    /// </summary>
    /// <returns>True if the directory exists; otherwise, false.</returns>
    public bool ConfigDirectoryExists()
    {
        return Directory.Exists(_configDirectory);
    }

    /// <summary>
    /// Creates the configuration directory if it doesn't exist.
    /// </summary>
    public void EnsureConfigDirectoryExists()
    {
        FileHelper.EnsureDirectoryExists(_configDirectory);
    }

    /// <summary>
    /// Gets a setting value based on the config source.
    /// </summary>
    /// <param name="definition">The setting definition.</param>
    /// <param name="defaultValue">The default value if not found.</param>
    /// <returns>The setting value, or the default value if not found.</returns>
    public string? GetSetting(SettingDefinition definition, string? defaultValue = null)
    {
        return definition.Source switch
        {
            ConfigSource.EngineIni => _engineIni?.GetValueAuto(definition.Section, definition.Name, defaultValue),
            ConfigSource.GameUserSettings => _gameUserSettingsIni?.GetValueAuto(definition.Section, definition.Name, defaultValue),
            ConfigSource.ScalabilityGroups => _gameUserSettingsIni?.GetValue("ScalabilityGroups", definition.Name, defaultValue),
            _ => defaultValue
        };
    }

    /// <summary>
    /// Sets a setting value based on the config source.
    /// </summary>
    /// <param name="definition">The setting definition.</param>
    /// <param name="value">The value to set.</param>
    public void SetSetting(SettingDefinition definition, string value)
    {
        switch (definition.Source)
        {
            case ConfigSource.EngineIni:
                EnsureEngineIniLoaded();
                _engineIni?.SetValue(definition.Section, definition.Name, value);
                break;
            case ConfigSource.GameUserSettings:
                _gameUserSettingsIni?.SetValue(definition.Section, definition.Name, value);
                break;
            case ConfigSource.ScalabilityGroups:
                _gameUserSettingsIni?.SetValue("ScalabilityGroups", definition.Name, value);
                break;
        }
    }

    /// <summary>
    /// Gets resolution from GameUserSettings.ini.
    /// </summary>
    /// <returns>Tuple of (width, height) or null if not found.</returns>
    public (int Width, int Height)? GetResolution()
    {
        var widthStr = _gameUserSettingsIni?.GetValueAuto("/Script/Engine.GameUserSettings", "ResolutionSizeX");
        var heightStr = _gameUserSettingsIni?.GetValueAuto("/Script/Engine.GameUserSettings", "ResolutionSizeY");
        
        if (int.TryParse(widthStr, out var width) && int.TryParse(heightStr, out var height))
        {
            return (width, height);
        }
        
        return null;
    }

    /// <summary>
    /// Sets resolution in GameUserSettings.ini.
    /// </summary>
    /// <param name="width">The screen width.</param>
    /// <param name="height">The screen height.</param>
    public void SetResolution(int width, int height)
    {
        const string section = "/Script/Engine.GameUserSettings";
        _gameUserSettingsIni?.SetValue(section, "ResolutionSizeX", width.ToString());
        _gameUserSettingsIni?.SetValue(section, "ResolutionSizeY", height.ToString());
        _gameUserSettingsIni?.SetValue(section, "DesiredScreenWidth", width.ToString());
        _gameUserSettingsIni?.SetValue(section, "DesiredScreenHeight", height.ToString());
    }

    /// <summary>
    /// Gets a composite setting value. Returns "1" if ALL composite values match, "0" otherwise.
    /// </summary>
    /// <param name="definition">The setting definition with CompositeValues.</param>
    /// <returns>"1" if all composite values match, "0" otherwise.</returns>
    public string GetCompositeSetting(SettingDefinition definition)
    {
        if (definition.CompositeValues == null || definition.CompositeValues.Count == 0)
        {
            return "0";
        }

        EnsureEngineIniDataLoaded();

        foreach (var cv in definition.CompositeValues)
        {
            var currentValue = _engineIni?.GetValue(cv.Section, cv.Key);
            if (currentValue != cv.EnabledValue)
            {
                return "0";
            }
        }

        return "1";
    }

    /// <summary>
    /// Sets a composite setting value. Writes all values when enabled, removes all keys when disabled.
    /// </summary>
    /// <param name="definition">The setting definition with CompositeValues.</param>
    /// <param name="enabled">Whether to enable or disable the composite setting.</param>
    public void SetCompositeSetting(SettingDefinition definition, bool enabled)
    {
        if (definition.CompositeValues == null || definition.CompositeValues.Count == 0)
        {
            return;
        }

        EnsureEngineIniDataLoaded();

        foreach (var cv in definition.CompositeValues)
        {
            if (enabled)
            {
                _engineIni?.SetValue(cv.Section, cv.Key, cv.EnabledValue);
            }
            else
            {
                _engineIni?.DeleteValue(cv.Section, cv.Key);
            }
        }
    }
}
