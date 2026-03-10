using System.Text.Json;

namespace HalfSwordTweaker.Config;

/// <summary>
/// Development configuration for switching between dev/prod modes.
/// When DevelopmentMode is true, the app uses local sample_saves folder instead of AppData.
/// </summary>
public class DevConfig
{
    /// <summary>
    /// If true, uses local sample_saves folder for development/testing.
    /// If false, uses live AppData game saves (production mode).
    /// </summary>
    public bool DevelopmentMode { get; set; } = false;
    
    /// <summary>
    /// Relative path to save files (only used when DevelopmentMode=true).
    /// </summary>
    public string SavePath { get; set; } = "sample_saves";
    
    /// <summary>
    /// Relative path for backups (only used when DevelopmentMode=true).
    /// </summary>
    public string BackupPath { get; set; } = "sample_saves/backups";
    
    private const string ConfigFileName = "dev.config.json";
    private static string ConfigPath => Path.Combine(AppContext.BaseDirectory, ConfigFileName);
    
    /// <summary>
    /// Load configuration from dev.config.json. Returns defaults if file missing/corrupted.
    /// </summary>
    public static DevConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            Console.WriteLine($"[DevConfig] Config not found at {ConfigPath}, using defaults (production mode)");
            return new DevConfig();
        }
        
        try
        {
            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<DevConfig>(json);
            if (config != null)
            {
                Console.WriteLine($"[DevConfig] Loaded config from {ConfigPath}");
                Console.WriteLine($"[DevConfig] DevelopmentMode = {config.DevelopmentMode}");
                return config;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DevConfig] Error loading config: {ex.Message}, using defaults");
        }
        
        return new DevConfig();
    }
    
    /// <summary>
    /// Save configuration to dev.config.json.
    /// </summary>
    public static void Save(DevConfig config)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(ConfigPath, json);
            Console.WriteLine($"[DevConfig] Config saved to {ConfigPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DevConfig] Error saving config: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Quick check if dev mode is active without loading full config.
    /// </summary>
    public static bool IsDevMode()
    {
        if (!File.Exists(ConfigPath))
            return false;
        
        try
        {
            var json = File.ReadAllText(ConfigPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("DevelopmentMode", out var prop))
            {
                return prop.GetBoolean();
            }
        }
        catch { }
        
        return false;
    }
}
