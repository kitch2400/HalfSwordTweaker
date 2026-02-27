namespace HalfSwordTweaker.Config;

/// <summary>
/// Manages save game file operations for Half Sword.
/// </summary>
public class SaveGameManager
{
    private readonly string _saveGameDirectory;
    private readonly string _settingsSavePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveGameManager"/> class.
    /// </summary>
    public SaveGameManager()
    {
        _saveGameDirectory = GetSaveGameDirectory();
        _settingsSavePath = Path.Combine(_saveGameDirectory, "Settings.sav");
    }

    /// <summary>
    /// Gets the path to the Half Sword save game directory.
    /// </summary>
    /// <returns>The path to the save game directory.</returns>
    private static string GetSaveGameDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "HalfswordUE5", "Saved", "SaveGames");
    }

    /// <summary>
    /// Checks if the save game directory exists.
    /// </summary>
    /// <returns>True if the directory exists; otherwise, false.</returns>
    public bool SaveGameDirectoryExists()
    {
        return Directory.Exists(_saveGameDirectory);
    }

    /// <summary>
    /// Checks if the Settings.sav file exists.
    /// </summary>
    /// <returns>True if the file exists; otherwise, false.</returns>
    public bool SettingsSaveExists()
    {
        return File.Exists(_settingsSavePath);
    }

    /// <summary>
    /// Reads settings from the Settings.sav file.
    /// </summary>
    /// <returns>A dictionary of setting names and their values.</returns>
    public Dictionary<string, double> ReadSettings()
    {
        if (!SettingsSaveExists())
        {
            // Return default values if file doesn't exist
            var defaults = new Dictionary<string, double>();
            foreach (var setting in SaveGameSettingsRegistry.Settings)
            {
                defaults[setting.Name] = setting.DefaultValue;
            }
            return defaults;
        }

        try
        {
            var data = File.ReadAllBytes(_settingsSavePath);
            var parser = new GvasParser(data);
            return parser.ParseSettings();
        }
        catch
        {
            // Return defaults if parsing fails
            var defaults = new Dictionary<string, double>();
            foreach (var setting in SaveGameSettingsRegistry.Settings)
            {
                defaults[setting.Name] = setting.DefaultValue;
            }
            return defaults;
        }
    }

    /// <summary>
    /// Writes settings to the Settings.sav file.
    /// </summary>
    /// <param name="settings">The settings to write.</param>
    /// <returns>True if successful; otherwise, false.</returns>
    public bool WriteSettings(Dictionary<string, double> settings)
    {
        try
        {
            // Create backup first
            CreateBackup();

            // Read existing file data
            byte[] data;
            if (SettingsSaveExists())
            {
                data = File.ReadAllBytes(_settingsSavePath);
            }
            else
            {
                // If file doesn't exist, we can't write settings
                // A full implementation would create a new GVAS file
                return false;
            }

            // Parse and update the data
            var parser = new GvasParser(data);
            
            // Update each setting
            bool success = true;
            foreach (var setting in settings)
            {
                if (!parser.UpdateDoubleProperty(setting.Key, setting.Value))
                {
                    success = false;
                    // Continue trying to update other settings
                }
            }
            
            // Write the updated data back to file
            var updatedData = parser.SerializeSettings(settings);
            File.WriteAllBytes(_settingsSavePath, updatedData);
            
            return success;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a backup of the Settings.sav file.
    /// </summary>
    private void CreateBackup()
    {
        if (SettingsSaveExists())
        {
            var backupPath = _settingsSavePath + ".backup";
            File.Copy(_settingsSavePath, backupPath, true);
        }
    }
}