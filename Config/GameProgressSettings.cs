namespace HalfSwordTweaker.Config;

/// <summary>
/// Property types supported by GameProgress.sav parser.
/// </summary>
public enum GvasPropertyType
{
    IntProperty,
    BoolProperty,
    DoubleProperty,
    StrProperty,
    NameProperty,
    ByteProperty
}

/// <summary>
/// Represents a game progress setting definition.
/// </summary>
public class GameProgressSetting
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
    /// Gets or sets the property type.
    /// </summary>
    public GvasPropertyType PropertyType { get; set; }

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
    /// Gets or sets the default boolean value.
    /// </summary>
    public bool DefaultBoolValue { get; set; } = false;
}

/// <summary>
/// Provides registry of game progress settings.
/// </summary>
public static class GameProgressSettingsRegistry
{
    /// <summary>
    /// Gets the list of game progress settings.
    /// </summary>
    public static List<GameProgressSetting> Settings { get; } = new()
    {
        new GameProgressSetting
        {
            Name = "Baron Defeated Times",
            DisplayName = "Baron Defeated Times",
            Description = "Number of times the Baron has been defeated (0-9999)",
            PropertyType = GvasPropertyType.IntProperty,
            MinValue = 0,
            MaxValue = 9999,
            DefaultValue = 0
        },
        new GameProgressSetting
        {
            Name = "First Start Intro Played",
            DisplayName = "First Start Intro Played",
            Description = "Whether the intro cinematic has been played",
            PropertyType = GvasPropertyType.BoolProperty,
            DefaultBoolValue = false
        },
        new GameProgressSetting
        {
            Name = "First Start Tutorial Played",
            DisplayName = "First Start Tutorial Played",
            Description = "Whether the tutorial has been completed",
            PropertyType = GvasPropertyType.BoolProperty,
            DefaultBoolValue = false
        },
        new GameProgressSetting
        {
            Name = "First Start Innkeeper Greeted",
            DisplayName = "First Start Innkeeper Greeted",
            Description = "Whether the innkeeper has been greeted",
            PropertyType = GvasPropertyType.BoolProperty,
            DefaultBoolValue = false
        },
        new GameProgressSetting
        {
            Name = "Height_21_0EB204DF4978B92AD0ED188FD32EEC7B",
            DisplayName = "Character Height",
            Description = "Character height (0.50 - 2.50)",
            PropertyType = GvasPropertyType.DoubleProperty,
            MinValue = 0.50,
            MaxValue = 2.50,
            DefaultValue = 1.00
        },
        new GameProgressSetting
        {
            Name = "Weight_23_65E4C6534D14653F96EB739F159E58CD",
            DisplayName = "Character Weight",
            Description = "Character weight/muscle mass (0.50 - 2.50)",
            PropertyType = GvasPropertyType.DoubleProperty,
            MinValue = 0.50,
            MaxValue = 2.50,
            DefaultValue = 1.00
        }
    };
}
