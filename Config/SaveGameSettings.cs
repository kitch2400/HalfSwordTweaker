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
        new SaveGameSetting
        {
            Name = "Mouse Sensitivity",
            DisplayName = "Mouse Sensitivity",
            Description = "Controls how sensitive the mouse is during gameplay",
            MinValue = 0.0,
            MaxValue = 2.0,
            DefaultValue = 1.0,
            Impact = PerformanceImpact.Low
        },
        new SaveGameSetting
        {
            Name = "Blood Rate",
            DisplayName = "Blood Effects Intensity",
            Description = "Controls the intensity of blood effects in combat",
            MinValue = 0.0,
            MaxValue = 2.0,
            DefaultValue = 1.0,
            Impact = PerformanceImpact.Low
        },
        new SaveGameSetting
        {
            Name = "Gore Rate",
            DisplayName = "Gore Effects Intensity",
            Description = "Controls the intensity of gore effects in combat",
            MinValue = 0.0,
            MaxValue = 2.0,
            DefaultValue = 1.0,
            Impact = PerformanceImpact.Low
        },
        new SaveGameSetting
        {
            Name = "Lock On Strength",
            DisplayName = "Lock-On Strength",
            Description = "Controls how strong the camera lock-on effect is",
            MinValue = 0.0,
            MaxValue = 1.0,
            DefaultValue = 1.0,
            Impact = PerformanceImpact.Low
        }
    };
}