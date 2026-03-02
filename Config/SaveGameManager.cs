using System.Linq;
using System.Text.Json;

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
        var devConfig = DevConfig.Load();
        
        if (devConfig.DevelopmentMode)
        {
            var baseDir = AppContext.BaseDirectory;
            _saveGameDirectory = Path.Combine(baseDir, devConfig.SavePath);
            _settingsSavePath = Path.Combine(_saveGameDirectory, "Settings.sav");
            Console.WriteLine($"[SaveGameManager] DEV MODE: {_saveGameDirectory}");
        }
        else
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _saveGameDirectory = Path.Combine(localAppData, "HalfswordUE5", "Saved", "SaveGames");
            _settingsSavePath = Path.Combine(_saveGameDirectory, "Settings.sav");
            Console.WriteLine($"[SaveGameManager] Production mode: {_saveGameDirectory}");
        }
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
        Console.Error.WriteLine($"[SaveGameManager] Reading from: {_settingsSavePath}");
        
        if (!SettingsSaveExists())
        {
            Console.Error.WriteLine($"[SaveGameManager] Settings.sav does not exist!");
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
            Console.Error.WriteLine($"[SaveGameManager] Read {data.Length} bytes");
            var parser = new GvasParser(data);
            return parser.ParseSettings();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SaveGameManager] Exception reading settings: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
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
        Console.Error.WriteLine($"[SaveGameManager] Writing to: {_settingsSavePath}");
        Console.Error.WriteLine($"[SaveGameManager] Settings to write: {settings.Count}");
        
        try
        {
            // Create backup first
            CreateBackup();

            // Read existing file data
            byte[] data;
            if (SettingsSaveExists())
            {
                data = File.ReadAllBytes(_settingsSavePath);
                Console.Error.WriteLine($"[SaveGameManager] Read {data.Length} bytes before write");
            }
            else
            {
                Console.Error.WriteLine($"[SaveGameManager] Settings.sav does not exist, cannot write!");
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
                Console.Error.WriteLine($"[SaveGameManager] Updating '{setting.Key}' to {setting.Value}");
                if (!parser.UpdateDoubleProperty(setting.Key, setting.Value))
                {
                    Console.Error.WriteLine($"[SaveGameManager] FAILED to update '{setting.Key}' - property not found or write failed");
                    success = false;
                    // Continue trying to update other settings
                }
                else
                {
                    Console.Error.WriteLine($"[SaveGameManager] Successfully updated '{setting.Key}'");
                }
            }
            
            // Write the updated data back to file
            var updatedData = parser.GetData();
            Console.Error.WriteLine($"[SaveGameManager] Writing {updatedData.Length} bytes to file");
            File.WriteAllBytes(_settingsSavePath, updatedData);
            Console.Error.WriteLine($"[SaveGameManager] File written successfully");
            
            // VERIFY: Read back the file and confirm the values were written
            var verifyBytes = File.ReadAllBytes(_settingsSavePath);
            Console.Error.WriteLine($"[SaveGameManager] Verifying write: read back {verifyBytes.Length} bytes");
            
            if (!verifyBytes.SequenceEqual(updatedData))
            {
                Console.Error.WriteLine($"[SaveGameManager] Write verification FAILED: File contents don't match after write");
                Console.Error.WriteLine($"  Expected length: {updatedData.Length}");
                Console.Error.WriteLine($"  Actual length: {verifyBytes.Length}");
                return false;
            }
            Console.Error.WriteLine($"[SaveGameManager] Byte verification passed");
            
            // Verify each property value was written correctly
            var verifyParser = new GvasParser(verifyBytes);
            var parsedSettings = verifyParser.ParseSettings();
            
            foreach (var setting in settings)
            {
                if (parsedSettings.TryGetValue(setting.Key, out var readValue))
                {
                    Console.Error.WriteLine($"[SaveGameManager] Verified '{setting.Key}': expected {setting.Value}, read {readValue}");
                    if (Math.Abs(readValue - setting.Value) > 0.0001)
                    {
                        Console.Error.WriteLine($"[SaveGameManager] Value mismatch for '{setting.Key}':");
                        Console.Error.WriteLine($"  Expected: {setting.Value}");
                        Console.Error.WriteLine($"  Read back: {readValue}");
                        return false;
                    }
                }
                else
                {
                    Console.Error.WriteLine($"[SaveGameManager] Property '{setting.Key}' not found in verification read!");
                    return false;
                }
            }
            
            Console.Error.WriteLine($"[SaveGameManager] All values verified successfully");
            return success;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SaveGameManager] WriteSettings exception: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
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