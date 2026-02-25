namespace HalfSwordTweaker.Config;

/// <summary>
/// Manages configuration files for Half Sword.
/// </summary>
public class ConfigManager
{
    private readonly string _configDirectory;
    private readonly string _engineIniPath;
    private readonly string _gameUserSettingsIniPath;

    private readonly IniFile _gameUserSettingsIni;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigManager"/> class.
    /// </summary>
    public ConfigManager()
    {
        _configDirectory = FileHelper.GetConfigDirectory();
        _engineIniPath = FileHelper.GetEngineIniPath();
        _gameUserSettingsIniPath = FileHelper.GetGameUserSettingsIniPath();

        _gameUserSettingsIni = new IniFile(_gameUserSettingsIniPath);
    }

    /// <summary>
    /// Reads GameUserSettings.ini.
    /// </summary>
    /// <returns>True if file was read successfully; otherwise, false.</returns>
    public bool ReadAll()
    {
        return _gameUserSettingsIni?.Read() ?? false;
    }

    /// <summary>
    /// Writes GameUserSettings.ini.
    /// </summary>
    /// <returns>True if file was written successfully; otherwise, false.</returns>
    public bool WriteAll()
    {
        if (_gameUserSettingsIni == null)
        {
            return false;
        }

        try
        {
            _gameUserSettingsIni.Write();
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
        // Map internal names to actual engine.ini keys
        string actualKey = definition.Name switch
        {
            "r.TemporalAA.Quality_TSR" => "r.TemporalAA.Quality",
            "r.TemporalAA.Quality_TSRKitch" => "r.TemporalAA.Quality",
            "r.TemporalAA.Upsampling_TSR" => "r.TemporalAA.Upsampling",
            "r.TemporalAA.Upsampling_TSRKitch" => "r.TemporalAA.Upsampling",
            "r.TSR.Enable_TSRKitch" => "r.TSR.Enable",
            "r.TSR.History.ScreenPercentage_TSRKitch" => "r.TSR.History.ScreenPercentage",
            "r.ScreenPercentage_TSR" => "r.ScreenPercentage",
            "r.ScreenPercentage_TSRKitch" => "r.ScreenPercentage",
            _ => definition.Name
        };

        return definition.Source switch
        {
            ConfigSource.EngineIni => GetEngineIniSettingDirect(definition.Section, actualKey, defaultValue),
            ConfigSource.GameUserSettings => _gameUserSettingsIni?.GetValueAuto(definition.Section, definition.Name, defaultValue),
            ConfigSource.ScalabilityGroups => _gameUserSettingsIni?.GetValue("ScalabilityGroups", definition.Name, defaultValue),
            _ => defaultValue
        };
    }

    /// <summary>
    /// Gets a single engine.ini setting directly.
    /// </summary>
    /// <param name="section">The section name.</param>
    /// <param name="key">The setting name.</param>
    /// <param name="defaultValue">The default value if not found.</param>
    /// <returns>The setting value, or the default value if not found.</returns>
    public string? GetEngineIniSettingDirect(string section, string key, string? defaultValue = null)
    {
        var tempIni = new IniFile(_engineIniPath);
        tempIni.Read();
        return tempIni.GetValueAuto(section, key, defaultValue);
    }

    /// <summary>
    /// Sets a setting value based on the config source.
    /// </summary>
    /// <param name="definition">The setting definition.</param>
    /// <param name="value">The value to set.</param>
    public void SetSetting(SettingDefinition definition, string value)
    {
        if (definition.Name == "r.AntiAliasingMethod")
        {
            SetAntiAliasingMethod(value);
            return;
        }

        // Map internal names to actual engine.ini keys
        string actualKey = definition.Name switch
        {
            "r.TemporalAA.Quality_TSR" => "r.TemporalAA.Quality",
            "r.TemporalAA.Quality_TSRKitch" => "r.TemporalAA.Quality",
            "r.TemporalAA.Upsampling_TSR" => "r.TemporalAA.Upsampling",
            "r.TemporalAA.Upsampling_TSRKitch" => "r.TemporalAA.Upsampling",
            "r.TSR.Enable_TSRKitch" => "r.TSR.Enable",
            "r.TSR.History.ScreenPercentage_TSRKitch" => "r.TSR.History.ScreenPercentage",
            "r.ScreenPercentage_TSR" => "r.ScreenPercentage",
            "r.ScreenPercentage_TSRKitch" => "r.ScreenPercentage",
            _ => definition.Name
        };

        if (!string.IsNullOrEmpty(definition.DependsOnSetting))
        {
            SetEngineIniSettingDirect(definition.Section, actualKey, value);
            return;
        }

        switch (definition.Source)
        {
            case ConfigSource.EngineIni:
                SetEngineIniSettingDirect(definition.Section, actualKey, value);
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
    /// Writes a single setting to engine.ini (read-modify-write pattern).
    /// </summary>
    private void SetEngineIniSettingDirect(string section, string key, string value)
    {
        if (IsEngineIniReadOnly())
        {
            RemoveEngineIniReadOnly();
        }

        var iniFile = new IniFile(_engineIniPath);
        iniFile.Read();
        iniFile.SetValue(section, key, value);
        iniFile.Write();
        FileHelper.SetFileReadOnly(_engineIniPath, true);
    }

    private void SetAntiAliasingMethod(string value)
    {
        if (IsEngineIniReadOnly())
        {
            RemoveEngineIniReadOnly();
        }

        var iniFile = new IniFile(_engineIniPath);
        iniFile.Read();

        // For TSR[Kitch], set r.AntiAliasingMethod to 4 (internal value)
        if (value == "5")
        {
            iniFile.SetValue("SystemSettings", "r.AntiAliasingMethod", "4");
        }
        else
        {
            iniFile.SetValue("SystemSettings", "r.AntiAliasingMethod", value);
        }

        // Delete ALL TAA/TSR keys when NOT using TAA, TSR, or TSR[Kitch] (i.e., Off or FXAA)
        if (value != "2" && value != "4" && value != "5")
        {
            iniFile.DeleteValue("SystemSettings", "r.TemporalAA.Upsampling");
            iniFile.DeleteValue("SystemSettings", "r.TemporalAA.Quality");
            iniFile.DeleteValue("SystemSettings", "r.TSR.Enable");
            iniFile.DeleteValue("SystemSettings", "r.ScreenPercentage");
            iniFile.DeleteValue("SystemSettings", "r.TSR.History.ScreenPercentage");
        }
        // Delete TSR-only keys when using TAA
        else if (value == "2")
        {
            iniFile.DeleteValue("SystemSettings", "r.TSR.Enable");
            iniFile.DeleteValue("SystemSettings", "r.ScreenPercentage");
            iniFile.DeleteValue("SystemSettings", "r.TSR.History.ScreenPercentage");
        }
        // When TSR (4) or TSR[Kitch] (5): don't delete anything, let dependent settings write their values

        iniFile.Write();
        FileHelper.SetFileReadOnly(_engineIniPath, true);
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

        var tempIni = new IniFile(_engineIniPath);
        tempIni.Read();

        foreach (var cv in definition.CompositeValues)
        {
            var currentValue = tempIni.GetValue(cv.Section, cv.Key);
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

        if (IsEngineIniReadOnly())
        {
            RemoveEngineIniReadOnly();
        }

        var iniFile = new IniFile(_engineIniPath);
        iniFile.Read();

        foreach (var cv in definition.CompositeValues)
        {
            if (enabled)
            {
                iniFile.SetValue(cv.Section, cv.Key, cv.EnabledValue);
            }
            else
            {
                iniFile.DeleteValue(cv.Section, cv.Key);
            }
        }

        iniFile.Write();
        FileHelper.SetFileReadOnly(_engineIniPath, true);
    }
}
