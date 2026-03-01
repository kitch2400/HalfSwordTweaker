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

    /// <summary>
    /// Gets or sets the parent struct name (if property is nested inside a struct).
    /// </summary>
    public string? ParentStruct { get; set; }
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
        // Progress tracking
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
            Name = "Day Count",
            DisplayName = "Days Played",
            Description = "Total number of days played in the game",
            PropertyType = GvasPropertyType.IntProperty,
            MinValue = 0,
            MaxValue = 9999,
            DefaultValue = 0
        },
        new GameProgressSetting
        {
            Name = "Day Time",
            DisplayName = "Day Time",
            Description = "Current time of day (0=Morning, 1=Afternoon, 2=Evening, 3=Night)",
            PropertyType = GvasPropertyType.ByteProperty,
            MinValue = 0,
            MaxValue = 3,
            DefaultValue = 0
        },
        
        // Starting flags
        new GameProgressSetting
        {
            Name = "First Start Intro Played",
            DisplayName = "Intro Played",
            Description = "Whether the intro cinematic has been played",
            PropertyType = GvasPropertyType.BoolProperty,
            DefaultBoolValue = false
        },
        new GameProgressSetting
        {
            Name = "First Start Tutorial Played",
            DisplayName = "Tutorial Played",
            Description = "Whether the tutorial has been completed",
            PropertyType = GvasPropertyType.BoolProperty,
            DefaultBoolValue = false
        },
        new GameProgressSetting
        {
            Name = "First Start Innkeeper Greeted",
            DisplayName = "Innkeeper Greeted",
            Description = "Whether the innkeeper has been greeted",
            PropertyType = GvasPropertyType.BoolProperty,
            DefaultBoolValue = false
        },
        
        // Character stats (inside Player Character_0 struct)
        new GameProgressSetting
        {
            Name = "Player Character_0.Height_21_0EB204DF4978B92AD0ED188FD32EEC7B",
            DisplayName = "Character Height",
            Description = "Character height (0.50 - 2.50)",
            PropertyType = GvasPropertyType.DoubleProperty,
            MinValue = 0.50,
            MaxValue = 2.50,
            DefaultValue = 1.00,
            ParentStruct = "Player Character"
        },
        new GameProgressSetting
        {
            Name = "Player Character_0.Weight_23_65E4C6534D14653F96EB739F159E58CD",
            DisplayName = "Character Weight",
            Description = "Character weight/muscle mass (0.50 - 2.50)",
            PropertyType = GvasPropertyType.DoubleProperty,
            MinValue = 0.50,
            MaxValue = 2.50,
            DefaultValue = 1.00,
            ParentStruct = "Player Character"
        },
        
        // Player stats
        new GameProgressSetting
        {
            Name = "Player Funds",
            DisplayName = "Player Funds (Gold)",
            Description = "Current amount of gold/money",
            PropertyType = GvasPropertyType.IntProperty,
            MinValue = 0,
            MaxValue = 1000000,
            DefaultValue = 0
        },
        new GameProgressSetting
        {
            Name = "Player Tier",
            DisplayName = "Player Tier (Rank)",
            Description = "Player's current tier/rank (0-5)",
            PropertyType = GvasPropertyType.ByteProperty,
            MinValue = 0,
            MaxValue = 5,
            DefaultValue = 0
        },
        
        // Unlocks
        new GameProgressSetting
        {
            Name = "Available Weapons 1H",
            DisplayName = "1H Weapons Unlocked",
            Description = "Number of one-handed weapons unlocked",
            PropertyType = GvasPropertyType.IntProperty,
            MinValue = 0,
            MaxValue = 100,
            DefaultValue = 0
        },
        new GameProgressSetting
        {
            Name = "Available Weapons 2H",
            DisplayName = "2H Weapons Unlocked",
            Description = "Number of two-handed weapons unlocked",
            PropertyType = GvasPropertyType.IntProperty,
            MinValue = 0,
            MaxValue = 100,
            DefaultValue = 0
        },
        new GameProgressSetting
        {
            Name = "Available Custom Armor",
            DisplayName = "Custom Armor Unlocked",
            Description = "Number of custom armor pieces unlocked",
            PropertyType = GvasPropertyType.IntProperty,
            MinValue = 0,
            MaxValue = 100,
            DefaultValue = 0
        },
        
        // Level progression
        new GameProgressSetting
        {
            Name = "Last Open Level",
            DisplayName = "Last Open Level",
            Description = "Name of the last level opened",
            PropertyType = GvasPropertyType.NameProperty,
            MinValue = 0,
            MaxValue = 0,
            DefaultValue = 0
        },
        new GameProgressSetting
        {
            Name = "Last Open Level Path",
            DisplayName = "Last Level Path",
            Description = "Path to the last level opened",
            PropertyType = GvasPropertyType.StrProperty,
            MinValue = 0,
            MaxValue = 0,
            DefaultValue = 0
        },
        
        // World state
        new GameProgressSetting
        {
            Name = "Weaponsmith Present",
            DisplayName = "Weaponsmith Present",
            Description = "Whether the weaponsmith is currently present",
            PropertyType = GvasPropertyType.BoolProperty,
            DefaultBoolValue = true
        },
        new GameProgressSetting
        {
            Name = "Player Character In Hell",
            DisplayName = "In Hell",
            Description = "Whether the player character is currently in Hell",
            PropertyType = GvasPropertyType.BoolProperty,
            DefaultBoolValue = false
        },
        
        // Free mode
        new GameProgressSetting
        {
            Name = "Free Mode Player Character 1",
            DisplayName = "Free Mode PC 1",
            Description = "Free mode player character 1 unlocked",
            PropertyType = GvasPropertyType.BoolProperty,
            DefaultBoolValue = false
        },
        new GameProgressSetting
        {
            Name = "Free Mode Player Character 2",
            DisplayName = "Free Mode PC 2",
            Description = "Free mode player character 2 unlocked",
            PropertyType = GvasPropertyType.BoolProperty,
            DefaultBoolValue = false
        },
        
        // Equipment budget
        new GameProgressSetting
        {
            Name = "EquipmentBudget_26_4FC29A0544CA0B009EAD408C59D5F8A6",
            DisplayName = "Equipment Budget",
            Description = "Current equipment budget for character creation",
            PropertyType = GvasPropertyType.DoubleProperty,
            MinValue = 0,
            MaxValue = 1000,
            DefaultValue = 0
        }
    };
}
