using HalfSwordTweaker.Config;

namespace HalfSwordTweaker.UI;

public class CategoryTab : TabPage
{
    private readonly FlowLayoutPanel _settingsPanel;
    private readonly Dictionary<string, SettingControl> _settingControls;

    public List<SettingDefinition> Settings { get; } = new List<SettingDefinition>();

    public string CategoryName { get; }

    public CategoryTab(string categoryName)
    {
        CategoryName = categoryName;
        Text = categoryName;

        _settingsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            AutoScroll = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight
        };

        _settingControls = new Dictionary<string, SettingControl>();

        Controls.Add(_settingsPanel);
    }

    public void AddSetting(SettingDefinition setting)
    {
        Settings.Add(setting);

        var control = new SettingControl(setting);
        _settingControls[setting.Name] = control;
        _settingsPanel.Controls.Add(control);
    }

    public SettingControl? GetSettingControl(string settingName)
    {
        return _settingControls.TryGetValue(settingName, out var control) ? control : null;
    }

    public void UpdateSettings(ConfigManager configManager)
    {
        foreach (var setting in Settings)
        {
            // Skip engine.ini settings if not loaded (lazy loading)
            // Exception: CompositeBoolean settings handle their own loading
            if (setting.Source == ConfigSource.EngineIni && !configManager.EngineIniLoaded)
            {
                if (setting.ControlType != ControlType.CompositeBoolean)
                {
                    continue;
                }
            }

            if (_settingControls.TryGetValue(setting.Name, out var control))
            {
                if (setting.ControlType == ControlType.Resolution)
                {
                    var parts = control.Value.Split('x');
                    if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
                    {
                        configManager.SetResolution(w, h);
                    }
                }
                else if (setting.ControlType == ControlType.CompositeBoolean)
                {
                    var enabled = control.Value == "1";
                    configManager.SetCompositeSetting(setting, enabled);
                }
                else
                {
                    configManager.SetSetting(setting, control.Value);
                }
            }
        }
    }
}
