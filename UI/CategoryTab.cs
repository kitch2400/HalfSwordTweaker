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
            WrapContents = false,
            FlowDirection = FlowDirection.TopDown
        };

        _settingControls = new Dictionary<string, SettingControl>();

        Controls.Add(_settingsPanel);
    }

    public void AddSetting(SettingDefinition setting)
    {
        Settings.Add(setting);

        var control = new SettingControl(setting);
        _settingControls[setting.Name] = control;

        if (!setting.Hidden)
        {
            _settingsPanel.Controls.Add(control);
        }
    }

    public SettingControl? GetSettingControl(string settingName)
    {
        return _settingControls.TryGetValue(settingName, out var control) ? control : null;
    }

    public void UpdateSettings(ConfigManager configManager)
    {
        // When AA is not TSR or TSR[Kitch], delete TSR-specific keys from engine.ini
        var aaControl = GetSettingControl("r.AntiAliasingMethod");
        if (aaControl != null)
        {
            var newValue = aaControl.Value;
            var ini = new Config.IniFile(Config.FileHelper.GetEngineIniPath());
            ini.Read();
            
            // Delete all TSR keys when going to Off/FXAA/TAA
            if (newValue != "4" && newValue != "5")
            {
                ini.DeleteValue("SystemSettings", "r.TSR.Enable");
                ini.DeleteValue("SystemSettings", "r.ScreenPercentage");
                ini.DeleteValue("SystemSettings", "r.TSR.History.ScreenPercentage");
            }

            // Delete r.Tonemapper.Sharpen when AA is not Off
            if (newValue != "0")
            {
                ini.DeleteValue("SystemSettings", "r.Tonemapper.Sharpen");
            }

            // Delete r.TSR.History.ScreenPercentage when AA is not TSR[Kitch]
            if (newValue != "5")
            {
                ini.DeleteValue("SystemSettings", "r.TSR.History.ScreenPercentage");
            }
            
            ini.Write();
        }

        foreach (var setting in Settings)
        {
            // Skip dependent settings where parent value doesn't match DependsOnValue
            if (!string.IsNullOrEmpty(setting.DependsOnSetting))
            {
                var parentControl = GetSettingControl(setting.DependsOnSetting);
                if (parentControl == null || parentControl.Value != setting.DependsOnValue)
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
