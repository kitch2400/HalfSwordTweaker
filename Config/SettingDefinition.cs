namespace HalfSwordTweaker.Config;

public enum ConfigSource
{
    EngineIni,
    GameUserSettings,
    ScalabilityGroups
}

public enum ControlType
{
    TrackBar,
    NumericUpDown,
    CheckBox,
    ComboBox,
    StringCombo,
    Resolution,
    CompositeBoolean
}

public class CompositeValue
{
    public string Section { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string EnabledValue { get; set; } = string.Empty;

    public CompositeValue() { }

    public CompositeValue(string section, string key, string enabledValue)
    {
        Section = section;
        Key = key;
        EnabledValue = enabledValue;
    }
}

public class SettingDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public ConfigSource Source { get; set; }
    public ControlType ControlType { get; set; }
    public string DefaultValue { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PerformanceImpact Impact { get; set; }

    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public int DecimalPlaces { get; set; } = 0;

    public Dictionary<int, string>? Options { get; set; }
    public Dictionary<string, string>? StringOptions { get; set; }
    public List<CompositeValue>? CompositeValues { get; set; }

    public string? DependsOnSetting { get; set; }
    public string? DependsOnValue { get; set; }
    public string? DisplayName { get; set; }
    public bool Hidden { get; set; }

    public SettingDefinition() { }

    public SettingDefinition(string name, string section, ConfigSource source, ControlType controlType,
        string defaultValue, string description, PerformanceImpact impact)
    {
        Name = name;
        Section = section;
        Source = source;
        ControlType = controlType;
        DefaultValue = defaultValue;
        Description = description;
        Impact = impact;
    }

    public static SettingDefinition Numeric(string name, string section, ConfigSource source,
        decimal min, decimal max, string defaultValue, string description, PerformanceImpact impact, int decimalPlaces = 0)
    {
        return new SettingDefinition(name, section, source, ControlType.NumericUpDown, defaultValue, description, impact)
        {
            MinValue = min,
            MaxValue = max,
            DecimalPlaces = decimalPlaces
        };
    }

    public static SettingDefinition NumericDependent(string name, string section, ConfigSource source,
        decimal min, decimal max, string defaultValue, string description, PerformanceImpact impact, int decimalPlaces,
        string dependsOnSetting, string dependsOnValue)
    {
        return new SettingDefinition(name, section, source, ControlType.NumericUpDown, defaultValue, description, impact)
        {
            MinValue = min,
            MaxValue = max,
            DecimalPlaces = decimalPlaces,
            DependsOnSetting = dependsOnSetting,
            DependsOnValue = dependsOnValue
        };
    }

    public static SettingDefinition NumericDependent(string name, string section, ConfigSource source,
        decimal min, decimal max, string defaultValue, string description, PerformanceImpact impact, int decimalPlaces,
        string dependsOnSetting, string dependsOnValue, string displayName)
    {
        return new SettingDefinition(name, section, source, ControlType.NumericUpDown, defaultValue, description, impact)
        {
            MinValue = min,
            MaxValue = max,
            DecimalPlaces = decimalPlaces,
            DependsOnSetting = dependsOnSetting,
            DependsOnValue = dependsOnValue,
            DisplayName = displayName
        };
    }

    public static SettingDefinition TrackBarInt(string name, string section, ConfigSource source,
        int min, int max, string defaultValue, string description, PerformanceImpact impact)
    {
        return new SettingDefinition(name, section, source, ControlType.TrackBar, defaultValue, description, impact)
        {
            MinValue = min,
            MaxValue = max
        };
    }

    public static SettingDefinition TrackBarIntDependent(string name, string section, ConfigSource source,
        int min, int max, string defaultValue, string description, PerformanceImpact impact,
        string dependsOnSetting, string dependsOnValue)
    {
        return new SettingDefinition(name, section, source, ControlType.TrackBar, defaultValue, description, impact)
        {
            MinValue = min,
            MaxValue = max,
            DependsOnSetting = dependsOnSetting,
            DependsOnValue = dependsOnValue
        };
    }

    public static SettingDefinition TrackBarIntDependent(string name, string section, ConfigSource source,
        int min, int max, string defaultValue, string description, PerformanceImpact impact,
        string dependsOnSetting, string dependsOnValue, string displayName)
    {
        return new SettingDefinition(name, section, source, ControlType.TrackBar, defaultValue, description, impact)
        {
            MinValue = min,
            MaxValue = max,
            DependsOnSetting = dependsOnSetting,
            DependsOnValue = dependsOnValue,
            DisplayName = displayName
        };
    }

    public static SettingDefinition TrackBarIntDependent(string name, string section, ConfigSource source,
        int min, int max, string defaultValue, string description, PerformanceImpact impact,
        string dependsOnSetting, string dependsOnValue, bool hidden)
    {
        return new SettingDefinition(name, section, source, ControlType.TrackBar, defaultValue, description, impact)
        {
            MinValue = min,
            MaxValue = max,
            DependsOnSetting = dependsOnSetting,
            DependsOnValue = dependsOnValue,
            Hidden = hidden
        };
    }

    public static SettingDefinition TrackBarIntDependent(string name, string section, ConfigSource source,
        int min, int max, string defaultValue, string description, PerformanceImpact impact,
        string dependsOnSetting, string dependsOnValue, string displayName, bool hidden)
    {
        return new SettingDefinition(name, section, source, ControlType.TrackBar, defaultValue, description, impact)
        {
            MinValue = min,
            MaxValue = max,
            DependsOnSetting = dependsOnSetting,
            DependsOnValue = dependsOnValue,
            DisplayName = displayName,
            Hidden = hidden
        };
    }

    public static SettingDefinition Bool(string name, string section, ConfigSource source,
        string defaultValue, string description, PerformanceImpact impact)
    {
        return new SettingDefinition(name, section, source, ControlType.CheckBox, defaultValue, description, impact);
    }

    public static SettingDefinition Combo(string name, string section, ConfigSource source,
        Dictionary<int, string> options, string defaultValue, string description, PerformanceImpact impact)
    {
        return new SettingDefinition(name, section, source, ControlType.ComboBox, defaultValue, description, impact)
        {
            Options = options
        };
    }

    public static SettingDefinition StringCombo(string name, string section, ConfigSource source,
        Dictionary<string, string> options, string defaultValue, string description, PerformanceImpact impact)
    {
        return new SettingDefinition(name, section, source, ControlType.StringCombo, defaultValue, description, impact)
        {
            StringOptions = options
        };
    }

    public static SettingDefinition Resolution()
    {
        return new SettingDefinition("Resolution", "", ConfigSource.GameUserSettings, ControlType.Resolution, "", "Display resolution", PerformanceImpact.Low);
    }

    public static SettingDefinition CompositeBool(string name, string section, ConfigSource source,
        List<CompositeValue> compositeValues, string description, PerformanceImpact impact)
    {
        return new SettingDefinition(name, section, source, ControlType.CompositeBoolean, "0", description, impact)
        {
            CompositeValues = compositeValues
        };
    }
}
