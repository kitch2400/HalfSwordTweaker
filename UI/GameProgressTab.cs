using HalfSwordTweaker.Config;
using System.ComponentModel;

namespace HalfSwordTweaker.UI;

/// <summary>
/// Represents a control for editing an integer game progress setting.
/// </summary>
public class GameProgressIntControl : Panel
{
    private readonly Label _label;
    private readonly NumericUpDown _numericUpDown;

    public event EventHandler? ValueChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => (int)_numericUpDown.Value;
        set => _numericUpDown.Value = Math.Clamp(value, (int)_numericUpDown.Minimum, (int)_numericUpDown.Maximum);
    }

    public GameProgressIntControl(string name, string description, int minValue, int maxValue, int defaultValue)
    {
        Padding = new Padding(3);
        Margin = new Padding(2);
        Width = 520;
        Height = 65;
        Anchor = AnchorStyles.Left | AnchorStyles.Top;

        _label = new Label
        {
            Text = $"{name} ({minValue} - {maxValue})",
            AutoSize = true,
            Location = new Point(3, 3),
            Font = new Font(FontFamily.GenericSansSerif, 9F, FontStyle.Bold)
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
            DecimalPlaces = 0,
            Increment = 1,
            Value = Math.Clamp(defaultValue, minValue, maxValue)
        };

        _numericUpDown.ValueChanged += (s, e) => ValueChanged?.Invoke(this, EventArgs.Empty);

        Controls.Add(_numericUpDown);
        Controls.Add(descriptionLabel);
        Controls.Add(_label);
    }

    public Control GetInputControl() => _numericUpDown;
}

/// <summary>
/// Represents a control for editing a boolean game progress setting.
/// </summary>
public class GameProgressBoolControl : Panel
{
    private readonly Label _label;
    private readonly CheckBox _checkBox;

    public event EventHandler? ValueChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Value
    {
        get => _checkBox.Checked;
        set => _checkBox.Checked = value;
    }

    public GameProgressBoolControl(string name, string description, bool defaultValue)
    {
        Padding = new Padding(3);
        Margin = new Padding(2);
        Width = 520;
        Height = 60;
        Anchor = AnchorStyles.Left | AnchorStyles.Top;

        _label = new Label
        {
            Text = name,
            AutoSize = true,
            Location = new Point(3, 3),
            Font = new Font(FontFamily.GenericSansSerif, 9F, FontStyle.Bold)
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

        _checkBox = new CheckBox
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Checked = defaultValue,
            Margin = new Padding(3, 5, 3, 5)
        };

        _checkBox.CheckedChanged += (s, e) => ValueChanged?.Invoke(this, EventArgs.Empty);

        Controls.Add(_checkBox);
        Controls.Add(descriptionLabel);
        Controls.Add(_label);
    }

    public Control GetInputControl() => _checkBox;
}

/// <summary>
/// Represents a control for editing a double game progress setting.
/// </summary>
public class GameProgressDoubleControl : Panel
{
    private readonly Label _label;
    private readonly NumericUpDown _numericUpDown;

    public event EventHandler? ValueChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double Value
    {
        get => (double)_numericUpDown.Value;
        set => _numericUpDown.Value = (decimal)Math.Clamp(value, (double)_numericUpDown.Minimum, (double)_numericUpDown.Maximum);
    }

    public GameProgressDoubleControl(string name, string description, double minValue, double maxValue, double defaultValue)
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
            Minimum = (decimal)minValue,
            Maximum = (decimal)maxValue,
            DecimalPlaces = 4,
            Increment = 0.01m,
            Value = (decimal)Math.Clamp(defaultValue, minValue, maxValue)
        };

        _numericUpDown.ValueChanged += (s, e) => ValueChanged?.Invoke(this, EventArgs.Empty);

        Controls.Add(_numericUpDown);
        Controls.Add(descriptionLabel);
        Controls.Add(_label);
    }

    public Control GetInputControl() => _numericUpDown;
}

/// <summary>
/// Represents a tab for editing game progress settings.
/// </summary>
public class GameProgressTab : TabPage
{
    private readonly GameProgressManager _gameProgressManager;
    private readonly Dictionary<string, Control> _settingControls = new();
    private readonly FlowLayoutPanel _flowLayoutPanel = new();
    private bool _hasChanges;

    /// <summary>
    /// Gets the category name for this tab.
    /// </summary>
    public string CategoryName { get; }

    /// <summary>
    /// Gets whether there are unsaved changes.
    /// </summary>
    public bool HasChanges => _hasChanges;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameProgressTab"/> class.
    /// </summary>
    public GameProgressTab() : base("Save/Stats/Progress")
    {
        CategoryName = "Game Progress";
        _gameProgressManager = new GameProgressManager();
        
        InitializeComponent();
        LoadSettings();
    }

    /// <summary>
    /// Initializes the component.
    /// </summary>
    private void InitializeComponent()
    {
        _flowLayoutPanel.FlowDirection = FlowDirection.TopDown;
        _flowLayoutPanel.WrapContents = false;
        _flowLayoutPanel.AutoScroll = true;
        _flowLayoutPanel.Dock = DockStyle.Fill;
        _flowLayoutPanel.Padding = new Padding(10);

        Controls.Add(_flowLayoutPanel);

        // Add settings
        foreach (var setting in GameProgressSettingsRegistry.Settings)
        {
            AddSetting(setting);
        }
    }

    /// <summary>
    /// Adds a setting to the tab.
    /// </summary>
    private void AddSetting(GameProgressSetting setting)
    {
        Control? control = null;

        switch (setting.PropertyType)
        {
            case GvasPropertyType.IntProperty:
                control = new GameProgressIntControl(
                    setting.DisplayName,
                    setting.Description,
                    (int)setting.MinValue,
                    (int)setting.MaxValue,
                    (int)setting.DefaultValue);
                ((GameProgressIntControl)control).ValueChanged += Setting_ValueChanged;
                break;

            case GvasPropertyType.BoolProperty:
                control = new GameProgressBoolControl(
                    setting.DisplayName,
                    setting.Description,
                    setting.DefaultBoolValue);
                ((GameProgressBoolControl)control).ValueChanged += Setting_ValueChanged;
                break;

            case GvasPropertyType.DoubleProperty:
                control = new GameProgressDoubleControl(
                    setting.DisplayName,
                    setting.Description,
                    setting.MinValue,
                    setting.MaxValue,
                    setting.DefaultValue);
                ((GameProgressDoubleControl)control).ValueChanged += Setting_ValueChanged;
                break;
        }

        if (control != null)
        {
            _flowLayoutPanel.Controls.Add(control);
            _settingControls[setting.Name] = control;
        }
    }

    /// <summary>
    /// Handles value changes in settings.
    /// </summary>
    private void Setting_ValueChanged(object? sender, EventArgs e)
    {
        _hasChanges = true;
    }

    /// <summary>
    /// Loads settings from the GameProgress.sav file.
    /// </summary>
    public void LoadSettings()
    {
        if (!_gameProgressManager.SaveGameDirectoryExists())
        {
            ShowMessage("Save game directory not found.", "Directory Missing", MessageBoxIcon.Warning);
            return;
        }

        if (!_gameProgressManager.GameProgressExists())
        {
            ShowMessage("GameProgress.sav file not found. Default values will be used.", "File Missing", MessageBoxIcon.Information);
        }

        var properties = _gameProgressManager.ReadProperties();

        foreach (var kvp in properties)
        {
            if (_settingControls.TryGetValue(kvp.Key, out var control))
            {
                switch (control)
                {
                    case GameProgressIntControl intControl:
                        if (kvp.Value is int intValue)
                            intControl.Value = intValue;
                        break;

                    case GameProgressBoolControl boolControl:
                        if (kvp.Value is bool boolValue)
                            boolControl.Value = boolValue;
                        break;

                    case GameProgressDoubleControl doubleControl:
                        if (kvp.Value is double doubleValue)
                            doubleControl.Value = doubleValue;
                        break;
                }
            }
        }

        _hasChanges = false;
    }

    /// <summary>
    /// Applies settings to the GameProgress.sav file.
    /// </summary>
    public bool ApplySettings()
    {
        var properties = new Dictionary<string, object>();

        foreach (var kvp in _settingControls)
        {
            switch (kvp.Value)
            {
                case GameProgressIntControl intControl:
                    properties[kvp.Key] = intControl.Value;
                    break;

                case GameProgressBoolControl boolControl:
                    properties[kvp.Key] = boolControl.Value;
                    break;

                case GameProgressDoubleControl doubleControl:
                    properties[kvp.Key] = doubleControl.Value;
                    break;
            }
        }

        if (_gameProgressManager.WriteProperties(properties))
        {
            _hasChanges = false;
            return true;
        }
        else
        {
            ShowMessage("Failed to save settings. Check debug output for details.", "Error", MessageBoxIcon.Error);
            return false;
        }
    }

    /// <summary>
    /// Gets a setting control by name.
    /// </summary>
    public Control? GetSettingControl(string name)
    {
        return _settingControls.TryGetValue(name, out var control) ? control : null;
    }

    /// <summary>
    /// Shows a message to the user.
    /// </summary>
    private void ShowMessage(string message, string title, MessageBoxIcon icon)
    {
        MessageBox.Show(this, message, title, MessageBoxButtons.OK, icon);
    }
}
