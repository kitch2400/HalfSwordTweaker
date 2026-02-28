using HalfSwordTweaker.Config;
using System.ComponentModel;

namespace HalfSwordTweaker.UI;

/// <summary>
/// Represents a simplified setting control for save game settings.
/// </summary>
public class SaveGameSettingControl : Panel
{
    private readonly Label _label;
    private readonly NumericUpDown _numericUpDown;
    private readonly Label _notSetLabel;

    public event EventHandler? ValueChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Value
    {
        get => _numericUpDown.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        set
        {
            if (decimal.TryParse(value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var numericValue))
            {
                _numericUpDown.Value = Math.Clamp(numericValue, _numericUpDown.Minimum, _numericUpDown.Maximum);
            }
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsNotSet
    {
        get => _notSetLabel.Visible;
        set => _notSetLabel.Visible = value;
    }

    public SaveGameSettingControl(string name, string description, PerformanceImpact impact, decimal minValue, decimal maxValue, decimal defaultValue)
    {
        Padding = new Padding(3);
        Margin = new Padding(2);
        Width = 520;
        Height = 65;
        Anchor = AnchorStyles.Left | AnchorStyles.Top;

        _label = new Label
        {
            Text = $"{name} ({minValue:0.00} - {maxValue:0.00})",
            AutoSize = true,
            Location = new Point(3, 3),
            Font = new Font(FontFamily.GenericSansSerif, 9F, FontStyle.Bold)
        };

        _notSetLabel = new Label
        {
            Text = "Not set",
            AutoSize = true,
            ForeColor = Color.OrangeRed,
            Font = new Font(FontFamily.GenericSansSerif, 7.5F, FontStyle.Italic),
            Visible = false
        };

        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 20
        };
        headerPanel.Controls.Add(_label);
        headerPanel.Controls.Add(_notSetLabel);

        headerPanel.Layout += (s, e) =>
        {
            var labelRight = _label.Right + 5;
            _notSetLabel.Location = new Point(labelRight, 4);
        };

        var descriptionLabel = new Label
        {
            Text = description,
            AutoSize = true,
            MaximumSize = new Size(510, 0),
            Dock = DockStyle.Top,
            ForeColor = Color.Gray,
            Font = new Font(FontFamily.GenericSansSerif, 7.5F),
            Padding = new Padding(3, 0, 3, 0)
        };

        _numericUpDown = new NumericUpDown
        {
            Dock = DockStyle.Top,
            Minimum = minValue,
            Maximum = maxValue,
            DecimalPlaces = 2,
            Increment = 0.01m,
            Value = Math.Clamp(defaultValue, minValue, maxValue)
        };

        _numericUpDown.ValueChanged += (s, e) => ValueChanged?.Invoke(this, EventArgs.Empty);

        Controls.Add(_numericUpDown);
        Controls.Add(descriptionLabel);
        Controls.Add(headerPanel);
    }
}

/// <summary>
/// Represents a blood/gore preset selector control.
/// </summary>
public class BloodGorePresetControl : Panel
{
    private readonly ComboBox _comboBox;
    private readonly Label _presetLabel;

    public event EventHandler? SelectedPresetChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string SelectedPreset
    {
        get => _comboBox.SelectedItem?.ToString() ?? "Gentle";
        set => _comboBox.SelectedItem = value;
    }

    public BloodGorePresetControl()
    {
        Padding = new Padding(3);
        Margin = new Padding(2);
        Width = 520;
        Height = 65;
        Anchor = AnchorStyles.Left | AnchorStyles.Top;

        _presetLabel = new Label
        {
            Text = "Blood/Gore Preset",
            AutoSize = true,
            Location = new Point(3, 3),
            Font = new Font(FontFamily.GenericSansSerif, 9F, FontStyle.Bold)
        };

        _comboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            AutoSize = true,
            Location = new Point(3, 25),
            Width = 514
        };

        _comboBox.Items.AddRange(new[] { "Gentle", "Gruesome", "Grotesque", "Custom" });
        _comboBox.SelectedItem = "Gentle";

        _comboBox.SelectedIndexChanged += (s, e) => SelectedPresetChanged?.Invoke(this, EventArgs.Empty);

        Controls.Add(_comboBox);
        Controls.Add(_presetLabel);
    }

    public void ApplyPreset(decimal bloodValue, decimal goreValue)
    {
        _comboBox.SelectedItem = "Custom";
        SelectedPresetChanged?.Invoke(this, EventArgs.Empty);
    }

    public ComboBox GetComboBox() => _comboBox;
}

/// <summary>
/// Represents a tab for editing save game settings.
/// </summary>
public class SaveGameTab : TabPage
{
    private readonly SaveGameManager _saveGameManager;
    private readonly Dictionary<string, SaveGameSettingControl> _settingControls = new();
    private readonly BloodGorePresetControl? _presetControl;
    private readonly FlowLayoutPanel _flowLayoutPanel = new();

    // Blood/Gore presets with their values
    private static readonly Dictionary<string, (decimal Blood, decimal Gore)> Presets = new()
    {
        { "Gentle", (0.00m, 0.00m) },
        { "Gruesome", (0.75m, 0.75m) },
        { "Grotesque", (1.50m, 2.00m) }
    };

    /// <summary>
    /// Gets the category name for this tab.
    /// </summary>
    public string CategoryName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveGameTab"/> class.
    /// </summary>
    /// <param name="categoryName">The name of the category.</param>
    public SaveGameTab(string categoryName) : base(categoryName)
    {
        CategoryName = categoryName;
        _saveGameManager = new SaveGameManager();
        _presetControl = new BloodGorePresetControl();
        
        InitializeComponent();
        LoadSettings();
    }

    /// <summary>
    /// Initializes the component.
    /// </summary>
    private void InitializeComponent()
    {
        // Setup FlowLayoutPanel
        _flowLayoutPanel.FlowDirection = FlowDirection.TopDown;
        _flowLayoutPanel.WrapContents = false;
        _flowLayoutPanel.AutoScroll = true;
        _flowLayoutPanel.Dock = DockStyle.Fill;
        _flowLayoutPanel.Padding = new Padding(10);

        // Setup preset control
        _presetControl!.SelectedPresetChanged += PresetControl_SelectedPresetChanged;

        // Add controls to tab
        Controls.Add(_flowLayoutPanel);

        // Add settings to the panel
        bool mouseSensitivityAdded = false;
        foreach (var setting in SaveGameSettingsRegistry.Settings)
        {
            AddSetting(setting);
            
            // Insert preset dropdown after Mouse Sensitivity
            if (!mouseSensitivityAdded && setting.Name == "Mouse Sensitivity")
            {
                mouseSensitivityAdded = true;
                _flowLayoutPanel.Controls.Add(_presetControl);
            }
        }
    }

    /// <summary>
    /// Handles preset selection changes.
    /// </summary>
    private void PresetControl_SelectedPresetChanged(object? sender, EventArgs e)
    {
        if (_presetControl == null) return;
        
        var selectedPreset = _presetControl.SelectedPreset;
        
        // Only apply if it's a predefined preset (not Custom)
        if (Presets.TryGetValue(selectedPreset, out var values))
        {
            var bloodControl = GetSettingControl("Blood Rate");
            var goreControl = GetSettingControl("Gore Rate");
            
            if (bloodControl != null)
                bloodControl.Value = values.Blood.ToString();
                
            if (goreControl != null)
                goreControl.Value = values.Gore.ToString();
        }
    }

    /// <summary>
    /// Adds a setting to the tab.
    /// </summary>
    /// <param name="setting">The setting to add.</param>
    public void AddSetting(SaveGameSetting setting)
    {
        var settingControl = new SaveGameSettingControl(
            setting.DisplayName, 
            setting.Description, 
            setting.Impact,
            (decimal)setting.MinValue,
            (decimal)setting.MaxValue,
            (decimal)setting.DefaultValue);
        
        // Wire up value changed handler for Blood and Gore to detect custom values
        if (setting.Name == "Blood Rate" || setting.Name == "Gore Rate")
        {
            settingControl.ValueChanged += (s, e) => HandleBloodGoreValueChanged();
        }
        
        _flowLayoutPanel.Controls.Add(settingControl);
        _settingControls[setting.Name] = settingControl;
    }

    /// <summary>
    /// Handles value changes in Blood or Gore settings.
    /// </summary>
    private void HandleBloodGoreValueChanged()
    {
        if (_presetControl == null) return;
        
        // Check if current values match any preset
        DetectAndSetPreset();
    }

    /// <summary>
    /// Loads settings from the save game file.
    /// </summary>
    public void LoadSettings()
    {
        if (!_saveGameManager.SaveGameDirectoryExists())
        {
            ShowMessage("Save game directory not found.", "Directory Missing", MessageBoxIcon.Warning);
            return;
        }

        if (!_saveGameManager.SettingsSaveExists())
        {
            ShowMessage("Settings.sav file not found. Default values will be used.", "File Missing", MessageBoxIcon.Information);
        }

        var settings = _saveGameManager.ReadSettings();
        
        foreach (var kvp in settings)
        {
            if (_settingControls.TryGetValue(kvp.Key, out var control))
            {
                control.Value = kvp.Value.ToString();
                control.IsNotSet = false;
            }
        }

        // Detect and set the appropriate preset
        DetectAndSetPreset();
    }

    /// <summary>
    /// Detects the current blood/gore values and sets the matching preset.
    /// </summary>
    private void DetectAndSetPreset()
    {
        if (_presetControl == null) return;
        
        var bloodControl = GetSettingControl("Blood Rate");
        var goreControl = GetSettingControl("Gore Rate");
        
        if (bloodControl == null || goreControl == null)
            return;

        // Parse current values (use InvariantCulture to handle dot decimal separator)
        if (!decimal.TryParse(bloodControl.Value, 
            System.Globalization.NumberStyles.Any, 
            System.Globalization.CultureInfo.InvariantCulture, 
            out var bloodValue) ||
            !decimal.TryParse(goreControl.Value, 
            System.Globalization.NumberStyles.Any, 
            System.Globalization.CultureInfo.InvariantCulture, 
            out var goreValue))
        {
            // Default to Gentle if we can't parse values
            _presetControl.GetComboBox().SelectedItem = "Gentle";
            return;
        }

        // Round to 2 decimal places to handle floating-point precision issues
        bloodValue = Math.Round(bloodValue, 2);
        goreValue = Math.Round(goreValue, 2);

        // Try to match against known presets
        string? matchedPreset = null;

        foreach (var preset in Presets)
        {
            var presetBlood = Math.Round(preset.Value.Blood, 2);
            var presetGore = Math.Round(preset.Value.Gore, 2);
            
            if (bloodValue == presetBlood && goreValue == presetGore)
            {
                matchedPreset = preset.Key;
                break;
            }
        }

        // Set to Custom if no preset matches, otherwise set to matched preset
        _presetControl.GetComboBox().SelectedItem = matchedPreset ?? "Custom";
    }

    /// <summary>
    /// Applies settings to the save game file.
    /// </summary>
    public void ApplySettings()
    {
        var settings = new Dictionary<string, double>();
        
        foreach (var kvp in _settingControls)
        {
            // Use InvariantCulture to ensure consistent decimal separator (.)
            if (double.TryParse(kvp.Value.Value, System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                settings[kvp.Key] = value;
            }
        }

        if (_saveGameManager.WriteSettings(settings))
        {
            // Silent success - no popup needed
        }
        else
        {
            ShowMessage("Failed to save settings. Check debug output for details.", "Error", MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Gets a setting control by name.
    /// </summary>
    /// <param name="name">The name of the setting.</param>
    /// <returns>The setting control, or null if not found.</returns>
    public SaveGameSettingControl? GetSettingControl(string name)
    {
        return _settingControls.TryGetValue(name, out var control) ? control : null;
    }

    /// <summary>
    /// Shows a message to the user.
    /// </summary>
    /// <param name="message">The message to show.</param>
    /// <param name="title">The title of the message box.</param>
    /// <param name="icon">The icon to display.</param>
    private void ShowMessage(string message, string title, MessageBoxIcon icon)
    {
        MessageBox.Show(this, message, title, MessageBoxButtons.OK, icon);
    }

}