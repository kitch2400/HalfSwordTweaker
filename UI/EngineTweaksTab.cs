using HalfSwordTweaker.Config;

namespace HalfSwordTweaker.UI;

public class EngineTweaksTab : TabPage
{
    private readonly Dictionary<string, CheckBox> _tweakCheckboxes = new();
    private readonly FlowLayoutPanel _panel;
    private bool _hasChanges;
    private readonly ToolTip? _tooltip;

    public EngineTweaksTab(ToolTip? tooltip = null) : base("Engine Tweaks")
    {
        _tooltip = tooltip;
        _panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            AutoScroll = true,
            WrapContents = false,
            FlowDirection = FlowDirection.TopDown
        };

        InitializeTweaks();
        Controls.Add(_panel);
    }

    private void InitializeTweaks()
    {
        // Multithreaded animations
        var tweakPanel = CreateTweakPanel(
            "a.ForceParallelAnimUpdate",
            "Multithreaded animations",
            "Enables parallel animation updates for better CPU utilization"
        );
        _tweakCheckboxes["a.ForceParallelAnimUpdate"] = (CheckBox)tweakPanel.Controls[0];
        _panel.Controls.Add(tweakPanel);
    }

    private Panel CreateTweakPanel(string name, string displayName, string description)
    {
        var panel = new Panel
        {
            Width = 550,
            Height = 60,
            Margin = new Padding(0, 5, 0, 5)
        };

        var label = new Label
        {
            Text = displayName,
            AutoSize = true,
            Location = new Point(0, 0),
            Font = new Font(FontFamily.GenericSansSerif, 9F, FontStyle.Bold)
        };

        var descLabel = new Label
        {
            Text = description,
            AutoSize = true,
            Location = new Point(0, 20),
            Font = new Font(FontFamily.GenericSansSerif, 7.5F),
            ForeColor = Color.Gray,
            MaximumSize = new Size(550, 0)
        };

        var checkBox = new CheckBox
        {
            Location = new Point(420, 0),
            AutoSize = true
        };

        checkBox.CheckedChanged += (s, e) => _hasChanges = true;

        if (_tooltip != null)
        {
            _tooltip.SetToolTip(checkBox, description);
            _tooltip.SetToolTip(label, description);
        }

        panel.Controls.Add(checkBox);
        panel.Controls.Add(label);
        panel.Controls.Add(descLabel);

        return panel;
    }

    public void LoadCurrentSettings()
    {
        var engineIniPath = Config.FileHelper.GetEngineIniPath();
        var ini = new IniFile(engineIniPath);
        ini.Read();

        foreach (var kvp in _tweakCheckboxes)
        {
            var consoleVar = kvp.Key;
            var checkBox = kvp.Value;
            var value = ini.GetValue("ConsoleVariables", consoleVar, "0");
            checkBox.Checked = value == "1";
        }

        _hasChanges = false;
    }

    public void ApplyChanges()
    {
        if (!_hasChanges)
            return;

        var engineIniPath = Config.FileHelper.GetEngineIniPath();
        
        // Ensure config directory exists
        Config.FileHelper.EnsureDirectoryExists(Path.GetDirectoryName(engineIniPath)!);

        var ini = new IniFile(engineIniPath);
        _ = ini.Read();  // Ignore return value - Write() will handle missing sections gracefully

        foreach (var kvp in _tweakCheckboxes)
        {
            var consoleVar = kvp.Key;
            var checkBox = kvp.Value;

            if (checkBox.Checked)
            {
                ini.SetValue("ConsoleVariables", consoleVar, "1");
            }
            else
            {
                ini.DeleteValue("ConsoleVariables", consoleVar);
            }
        }

        ini.Write();
        Config.FileHelper.SetFileReadOnly(engineIniPath, true);

        _hasChanges = false;
    }

    public bool HasChanges => _hasChanges;
}
