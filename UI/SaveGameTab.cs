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
/// Represents a tab for editing save game settings.
/// </summary>
public class SaveGameTab : TabPage
{
    private readonly SaveGameManager _saveGameManager;
    private readonly Dictionary<string, SaveGameSettingControl> _settingControls = new();
    private readonly Button _applyButton = new();
    private readonly Button _refreshButton = new();
    private readonly FlowLayoutPanel _flowLayoutPanel = new();

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

        // Setup buttons
        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            Padding = new Padding(10)
        };

        _applyButton.Text = "Apply";
        _applyButton.Width = 100;
        _applyButton.Dock = DockStyle.Right;
        _applyButton.Click += ApplyButton_Click;

        _refreshButton.Text = "Refresh";
        _refreshButton.Width = 100;
        _refreshButton.Dock = DockStyle.Right;
        _refreshButton.Click += RefreshButton_Click;

        buttonPanel.Controls.Add(_applyButton);
        buttonPanel.Controls.Add(_refreshButton);

        // Add controls to tab
        Controls.Add(_flowLayoutPanel);
        Controls.Add(buttonPanel);

        // Add settings to the panel
        foreach (var setting in SaveGameSettingsRegistry.Settings)
        {
            AddSetting(setting);
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
        
        _flowLayoutPanel.Controls.Add(settingControl);
        _settingControls[setting.Name] = settingControl;
    }

    /// <summary>
    /// Loads settings from the save game file.
    /// </summary>
    private void LoadSettings()
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
    }

    /// <summary>
    /// Applies settings to the save game file.
    /// </summary>
    private void ApplySettings()
    {
        var settings = new Dictionary<string, double>();
        
        foreach (var kvp in _settingControls)
        {
            if (double.TryParse(kvp.Value.Value, out var value))
            {
                settings[kvp.Key] = value;
            }
        }

        if (_saveGameManager.WriteSettings(settings))
        {
            ShowMessage("Settings saved successfully.", "Success", MessageBoxIcon.Information);
        }
        else
        {
            ShowMessage("Failed to save settings.", "Error", MessageBoxIcon.Error);
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

    /// <summary>
    /// Handles the Apply button click event.
    /// </summary>
    private void ApplyButton_Click(object? sender, EventArgs e)
    {
        ApplySettings();
    }

    /// <summary>
    /// Handles the Refresh button click event.
    /// </summary>
    private void RefreshButton_Click(object? sender, EventArgs e)
    {
        LoadSettings();
    }
}