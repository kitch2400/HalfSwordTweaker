namespace HalfSwordTweaker.Config;

/// <summary>
/// Represents a save game setting definition.
/// </summary>
public class SaveGameSetting
{
    /// <summary>
    /// Gets or sets the name of the setting.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the setting.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the setting.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the minimum value for numeric settings.
    /// </summary>
    public double MinValue { get; set; } = 0.0;

    /// <summary>
    /// Gets or sets the maximum value for numeric settings.
    /// </summary>
    public double MaxValue { get; set; } = 100.0;

    /// <summary>
    /// Gets or sets the default value for the setting.
    /// </summary>
    public double DefaultValue { get; set; } = 0.0;

    /// <summary>
    /// Gets or sets the performance impact of the setting.
    /// </summary>
    public PerformanceImpact Impact { get; set; } = PerformanceImpact.Low;

    /// <summary>
    /// Gets or sets whether missing properties should be auto-created in Settings.sav.
    /// </summary>
    public bool AutoCreate { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this setting should be hidden from the UI.
    /// </summary>
    public bool Hidden { get; set; } = false;
}

/// <summary>
/// Provides registry of save game settings.
/// </summary>
public static class SaveGameSettingsRegistry
{
    /// <summary>
    /// Gets the list of save game settings.
    /// </summary>
    public static List<SaveGameSetting> Settings { get; } = new()
    {
        // === AUDIO GROUP ===
        new SaveGameSetting
        {
            Name = "Sound Volume",
            DisplayName = "Sound Volume",
            Description = "Controls the volume of sound effects",
            MinValue = 0.0,
            MaxValue = 1.0,
            DefaultValue = 1.0,
            Impact = PerformanceImpact.Low,
            AutoCreate = true
        },
        new SaveGameSetting
        {
            Name = "Music Volume",
            DisplayName = "Music Volume",
            Description = "Controls the volume of background music",
            MinValue = 0.0,
            MaxValue = 1.0,
            DefaultValue = 1.0,
            Impact = PerformanceImpact.Low,
            AutoCreate = true
        },

        // === INPUT GROUP ===
        new SaveGameSetting
        {
            Name = "Mouse Sensitivity",
            DisplayName = "Mouse Sensitivity",
            Description = "Controls how sensitive the mouse is during gameplay",
            MinValue = 0.0,
            MaxValue = 2.0,
            DefaultValue = 1.0,
            Impact = PerformanceImpact.Low,
            AutoCreate = true
        },

        // === COMBAT GROUP ===
        new SaveGameSetting
        {
            Name = "Blood Rate",
            DisplayName = "Blood Effects Intensity",
            Description = "Controls the intensity of blood effects in combat",
            MinValue = 0.0,
            MaxValue = 1.0,
            DefaultValue = 1.0,
            Impact = PerformanceImpact.Low,
            AutoCreate = true
        },
        new SaveGameSetting
        {
            Name = "Gore Rate",
            DisplayName = "Gore Effects Intensity",
            Description = "Controls the intensity of gore effects in combat",
            MinValue = 0.0,
            MaxValue = 2.0,
            DefaultValue = 1.0,
            Impact = PerformanceImpact.Low,
            AutoCreate = true
        },
        new SaveGameSetting
        {
            Name = "Lock On Strength",
            DisplayName = "Lock On Strength",
            Description = "Controls the strength/duration of lock-on targeting",
            MinValue = 0.0,
            MaxValue = 2.0,
            DefaultValue = 1.0,
            Impact = PerformanceImpact.Low,
            AutoCreate = true
        },
        new SaveGameSetting
        {
            Name = "Damage to Player",
            DisplayName = "Damage to Player",
            Description = "Controls damage multiplier received by player",
            MinValue = 0.0,
            MaxValue = 2.0,
            DefaultValue = 1.0,
            Impact = PerformanceImpact.Low,
            AutoCreate = true
        },
        new SaveGameSetting
        {
            Name = "Damage to NPC",
            DisplayName = "Damage to NPC",
            Description = "Controls damage multiplier dealt to NPCs",
            MinValue = 0.0,
            MaxValue = 2.0,
            DefaultValue = 1.0,
            Impact = PerformanceImpact.Low,
            AutoCreate = true
        },
        // Hidden property - exists in game file but not exposed in UI
        new SaveGameSetting
        {
            Name = "Voice Volume",
            DisplayName = "Voice Volume",
            Description = "Controls the volume of voice audio (hidden property)",
            MinValue = 0.0,
            MaxValue = 1.0,
            DefaultValue = 0.0,
            Impact = PerformanceImpact.Low,
            AutoCreate = true,
            Hidden = true
        }
    };
}
