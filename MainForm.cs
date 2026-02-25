using HalfSwordTweaker.Config;
using HalfSwordTweaker.UI;

namespace HalfSwordTweaker;

public class MainForm : Form
{
    private readonly ConfigManager _configManager;
    private readonly ProfileManager _profileManager;
    private readonly TabControl _tabControl = new();
    private readonly Button _applyButton = new();
    private readonly Button _resetButton = new();
    private readonly Button _saveProfileButton = new();
    private readonly Button _loadProfileButton = new();
    private ToolStripStatusLabel _statusLabel = new();
    private readonly ComboBox _profileComboBox = new();
    private readonly Button _refreshButton = new();
    private readonly ToolTip _tooltip = new();

    private bool _hasScalabilityGroupChanges;
    private bool _isLoadingProfile;
    private bool _msaaWarningShown;

    public MainForm()
    {
        _configManager = new ConfigManager();
        _profileManager = new ProfileManager();
        DescriptionFetcher.LoadCache();

        InitializeComponent();
        LoadSettings();
        InitializeTooltips();
    }

    private async void InitializeTooltips()
    {
        _statusLabel.Text = "Loading descriptions...";
        
        var progress = new Progress<string>(msg => _statusLabel.Text = msg);
        await DescriptionFetcher.FetchAllAsync(progress);

        // Configure tooltip
        _tooltip.AutoPopDelay = 10000;
        _tooltip.InitialDelay = 500;
        _tooltip.ReshowDelay = 200;
        _tooltip.ShowAlways = true;

        // Wire up tooltips to all setting controls
        foreach (TabPage tab in _tabControl.TabPages)
        {
            if (tab is CategoryTab categoryTab)
            {
                foreach (var setting in categoryTab.Settings)
                {
                    if (categoryTab.GetSettingControl(setting.Name) is SettingControl control)
                    {
                        var inputControl = control.InputControl;
                        var labelControl = control.LabelControl;
                        var settingName = setting.Name;
                        
                        // Set tooltip immediately with description
                        var description = DescriptionFetcher.GetDescription(settingName);
                        if (!string.IsNullOrEmpty(description))
                        {
                            _tooltip.SetToolTip(inputControl, description);
                            _tooltip.SetToolTip(labelControl, description);
                        }
                    }
                }
            }
        }
        
        _statusLabel.Text = "Ready";
    }

    private void InitializeComponent()
    {
        Text = "HalfSwordTweaker - By Kitch2400";
        Size = new Size(1080, 700);
        StartPosition = FormStartPosition.CenterScreen;

        _tabControl.Dock = DockStyle.Fill;

        var statusStrip = new StatusStrip();
        _statusLabel.Text = "Ready";
        statusStrip.Items.Add(_statusLabel);

        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            Padding = new Padding(10)
        };

        _applyButton.Text = "Apply";
        _applyButton.Width = 100;
        _applyButton.Dock = DockStyle.Right;
        _applyButton.Click += ApplyButton_Click;

        _resetButton.Text = "Reset";
        _resetButton.Width = 100;
        _resetButton.Dock = DockStyle.Right;
        _resetButton.Click += ResetButton_Click;

        _saveProfileButton.Text = "Save Profile";
        _saveProfileButton.Width = 100;
        _saveProfileButton.Dock = DockStyle.Right;
        _saveProfileButton.Click += SaveProfileButton_Click;

        _loadProfileButton.Text = "Load Profile";
        _loadProfileButton.Width = 100;
        _loadProfileButton.Dock = DockStyle.Right;
        _loadProfileButton.Click += LoadProfileButton_Click;

        _refreshButton.Text = "Refresh";
        _refreshButton.Width = 100;
        _refreshButton.Dock = DockStyle.Right;
        _refreshButton.Click += RefreshButton_Click;

        buttonPanel.Controls.Add(_applyButton);
        buttonPanel.Controls.Add(_resetButton);
        buttonPanel.Controls.Add(_saveProfileButton);
        buttonPanel.Controls.Add(_loadProfileButton);
        buttonPanel.Controls.Add(_refreshButton);

        var profilePanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(10)
        };

        var profileLabel = new Label
        {
            Text = "Profile:",
            AutoSize = true,
            Dock = DockStyle.Left
        };

        _profileComboBox.Dock = DockStyle.Left;
        _profileComboBox.Width = 200;
        _profileComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _profileComboBox.SelectedIndexChanged += ProfileComboBox_SelectedIndexChanged;

        profilePanel.Controls.Add(profileLabel);
        profilePanel.Controls.Add(_profileComboBox);

        Controls.Add(_tabControl);
        Controls.Add(profilePanel);
        Controls.Add(buttonPanel);
        Controls.Add(statusStrip);

        InitializeTabs();
    }

    private void InitializeTabs()
    {
        foreach (var tabKvp in SettingsRegistry.Tabs)
        {
            var tab = new CategoryTab(tabKvp.Key);
            
            foreach (var setting in tabKvp.Value)
            {
                tab.AddSetting(setting);
            }
            
            _tabControl.TabPages.Add(tab);
        }

        WireUpScalabilityGroupChangeTracking();
        WireUpAADependentSettings();
    }

    private void WireUpAADependentSettings()
    {
        SettingControl? aaControl = null;

        foreach (TabPage tab in _tabControl.TabPages)
        {
            if (tab is CategoryTab categoryTab)
            {
                foreach (var setting in categoryTab.Settings)
                {
                    if (categoryTab.GetSettingControl(setting.Name) is SettingControl control)
                    {
                        if (setting.Name == "r.AntiAliasingMethod")
                        {
                            aaControl = control;
                        }
                    }
                }
            }
        }

        if (aaControl == null)
            return;

        foreach (TabPage tab in _tabControl.TabPages)
        {
            if (tab is CategoryTab categoryTab)
            {
                foreach (var setting in categoryTab.Settings)
                {
                    if (!string.IsNullOrEmpty(setting.DependsOnSetting) && 
                        setting.DependsOnSetting == "r.AntiAliasingMethod")
                    {
                        if (categoryTab.GetSettingControl(setting.Name) is SettingControl control)
                        {
                            control.SetDependentVisibility(aaControl);
                        }
                    }
                }
            }
        }
    }

    private void WireUpScalabilityGroupChangeTracking()
    {
        foreach (TabPage tab in _tabControl.TabPages)
        {
            if (tab is CategoryTab categoryTab && categoryTab.CategoryName == "Scalability Groups")
            {
                foreach (var setting in categoryTab.Settings)
                {
                    if (categoryTab.GetSettingControl(setting.Name) is SettingControl control)
                    {
                        control.ValueChanged += (s, e) =>
                        {
                            if (!_isLoadingProfile)
                            {
                                _hasScalabilityGroupChanges = true;
                            }
                        };
                    }
                }
                break;
            }
        }
    }

    private void PopulateSettingsFromConfig()
    {
        foreach (TabPage tab in _tabControl.TabPages)
        {
            if (tab is CategoryTab categoryTab)
            {
                foreach (var setting in categoryTab.Settings)
                {
                    if (categoryTab.GetSettingControl(setting.Name) is SettingControl control)
                    {
                        // EngineIni settings use direct read
                        if (setting.Source == ConfigSource.EngineIni)
                        {
                            if (setting.ControlType == ControlType.CompositeBoolean)
                            {
                                // GetCompositeSetting() reads engine.ini directly
                            }
                            else
                            {
                                var value = _configManager.GetEngineIniSettingDirect(
                                    setting.Section, setting.Name, setting.DefaultValue);
                                control.IsNotSet = string.IsNullOrEmpty(value);

                                // Detect TSR[Kitch] by checking r.TSR.History.ScreenPercentage
                                if (value == "4")
                                {
                                    var historyScreenPct = _configManager.GetEngineIniSettingDirect(
                                        "SystemSettings", "r.TSR.History.ScreenPercentage", "");
                                    if (historyScreenPct == "100")
                                    {
                                        // TSR[Kitch] has r.TSR.History.ScreenPercentage=100
                                        control.Value = "5"; // TSR[Kitch] internal value
                                    }
                                    else
                                    {
                                        control.Value = value;
                                    }
                                }
                                else
                                {
                                    control.Value = value ?? setting.DefaultValue;
                                }

                                // Warn if MSAA (value 3) is in use
                                if (setting.Name == "r.AntiAliasingMethod" && value == "3" && !_msaaWarningShown)
                                {
                                    MessageBox.Show(
                                        "Your current setting uses MSAA (value 3), which is no longer supported by this tool. " +
                                        "Please select a different anti-aliasing method (Off, FXAA, TAA, or TSR).",
                                        "Unsupported Setting", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    _msaaWarningShown = true;
                                }

                                continue;
                            }
                        }

                        if (setting.ControlType == ControlType.Resolution)
                        {
                            var resolution = _configManager.GetResolution();
                            if (resolution.HasValue)
                            {
                                control.IsNotSet = false;
                                control.Value = $"{resolution.Value.Width}x{resolution.Value.Height}";
                            }
                            else
                            {
                                control.IsNotSet = true;
                            }
                        }
                        else if (setting.ControlType == ControlType.CompositeBoolean)
                        {
                            var currentValue = _configManager.GetCompositeSetting(setting);
                            control.IsNotSet = false;
                            control.Value = currentValue;
                        }
                        else
                        {
                            var currentValue = _configManager.GetSetting(setting);
                            if (currentValue == null)
                            {
                                control.IsNotSet = true;
                            }
                            else
                            {
                                control.IsNotSet = false;
                                control.Value = currentValue;
                            }
                        }
                    }
                }
            }
        }
    }

    private void LoadSettings()
    {
        try
        {
            if (!_configManager.ConfigDirectoryExists())
            {
                _configManager.EnsureConfigDirectoryExists();
                _statusLabel.Text = "Config directory created.";
            }

            // Only load GameUserSettings.ini
            if (_configManager.ReadAll())
            {
                // Auto-detect matching profile from current ScalabilityGroups settings
                var currentGroups = GetCurrentScalabilityGroups();
                var detectedProfile = _profileManager.FindMatchingProfile(currentGroups);
                _profileManager.SetActiveProfile(detectedProfile);

                _statusLabel.Text = "Settings loaded successfully.";
                LoadProfiles();
                RestoreActiveProfile();
            }
            else
            {
                _statusLabel.Text = "Warning: Could not read configuration files.";
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Error loading settings: {ex.Message}";
            MessageBox.Show($"Error loading settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            PopulateSettingsFromConfig();
        }
    }

    private Dictionary<string, string> GetCurrentScalabilityGroups()
    {
        var groups = new Dictionary<string, string>();
        
        if (SettingsRegistry.Tabs.TryGetValue("Scalability Groups", out var sgSettings))
        {
            foreach (var setting in sgSettings)
            {
                var value = _configManager.GetSetting(setting);
                if (value != null)
                {
                    groups[setting.Name] = value;
                }
            }
        }
        
        return groups;
    }

    private void RestoreActiveProfile()
    {
        var activeProfile = _profileManager.ActiveProfileName;
        var profiles = _profileComboBox.Items.Cast<string>().ToList();
        var activeItem = profiles.FirstOrDefault(p => 
            ProfileManager.StripActivePrefix(p).Equals(activeProfile, StringComparison.OrdinalIgnoreCase));
        
        if (activeItem != null)
        {
            _profileComboBox.SelectedItem = activeItem;
        }
        else if (profiles.Count > 0)
        {
            _profileComboBox.SelectedIndex = 0;
        }
    }

    private void LoadProfiles()
    {
        var profiles = _profileManager.GetProfiles();
        _profileComboBox.Items.Clear();
        _profileComboBox.Items.AddRange(profiles.ToArray());

        if (profiles.Count > 0)
        {
            _profileComboBox.SelectedIndex = 0;
        }
    }

    private void ApplySettings()
    {
        try
        {
            if (_configManager.IsEngineIniReadOnly())
            {
                _configManager.RemoveEngineIniReadOnly();
            }

            foreach (TabPage tab in _tabControl.TabPages)
            {
                if (tab is CategoryTab categoryTab)
                {
                    categoryTab.UpdateSettings(_configManager);
                }
            }

            if (_configManager.WriteAll())
            {
                if (_hasScalabilityGroupChanges)
                {
                    _profileManager.SetActiveProfile(ProfileManager.CustomProfileName);
                }
                _statusLabel.Text = "Settings applied successfully.";
                _hasScalabilityGroupChanges = false;
                LoadProfiles();
                RestoreActiveProfile();
            }
            else
            {
                _statusLabel.Text = "Error applying settings.";
                MessageBox.Show("Error applying settings. Please check the configuration files.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Error applying settings: {ex.Message}";
            MessageBox.Show($"Error applying settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ResetSettings()
    {
        var result = MessageBox.Show(
            "Are you sure you want to reset all settings to their defaults?",
            "Reset Settings",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result == DialogResult.Yes)
        {
            if (_configManager.ReadAll())
            {
                PopulateSettingsFromConfig();
                _statusLabel.Text = "Settings reset to file values.";
                _hasScalabilityGroupChanges = false;
            }
        }
    }

    private void SaveProfile()
    {
        var inputForm = new InputForm("Save Profile", "Enter profile name:");
        if (inputForm.ShowDialog() == DialogResult.OK)
        {
            var profileName = inputForm.InputText;

            if (string.IsNullOrWhiteSpace(profileName))
            {
                MessageBox.Show("Please enter a valid profile name.", "Invalid Name", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (profileName.Equals(ProfileManager.CustomProfileName, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("'Current' is a reserved profile name. Please choose a different name.", "Reserved Name", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (_profileManager.IsBundledProfile(profileName))
            {
                MessageBox.Show($"Cannot overwrite bundled profile '{profileName}'. Please choose a different name.", "Bundled Profile", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var engineSettings = new Dictionary<string, Dictionary<string, string>>();
            var gameUserSettings = new Dictionary<string, Dictionary<string, string>>();
            var scalabilityGroups = new Dictionary<string, string>();

            foreach (TabPage tab in _tabControl.TabPages)
            {
                if (tab is CategoryTab categoryTab)
                {
                    foreach (var setting in categoryTab.Settings)
                    {
                        if (categoryTab.GetSettingControl(setting.Name) is SettingControl control)
                        {
                            switch (setting.Source)
                            {
                                case ConfigSource.EngineIni:
                                    if (!engineSettings.ContainsKey(setting.Section))
                                    {
                                        engineSettings[setting.Section] = new Dictionary<string, string>();
                                    }
                                    engineSettings[setting.Section][setting.Name] = control.Value;
                                    break;
                                case ConfigSource.GameUserSettings:
                                    if (!gameUserSettings.ContainsKey(setting.Section))
                                    {
                                        gameUserSettings[setting.Section] = new Dictionary<string, string>();
                                    }
                                    gameUserSettings[setting.Section][setting.Name] = control.Value;
                                    break;
                                case ConfigSource.ScalabilityGroups:
                                    scalabilityGroups[setting.Name] = control.Value;
                                    break;
                            }
                        }
                    }
                }
            }

            if (_profileManager.SaveProfile(profileName, engineSettings, gameUserSettings, scalabilityGroups))
            {
                _profileManager.SetActiveProfile(profileName);
                _statusLabel.Text = $"Profile '{profileName}' saved successfully.";
                LoadProfiles();
                RestoreActiveProfile();
            }
            else
            {
                _statusLabel.Text = "Error saving profile.";
                MessageBox.Show("Error saving profile.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void LoadProfile()
    {
        if (_profileComboBox.SelectedItem == null)
        {
            return;
        }

        _isLoadingProfile = true;
        try
        {
            var displayName = _profileComboBox.SelectedItem.ToString();
            var profileName = ProfileManager.StripActivePrefix(displayName);

            if (profileName.Equals(ProfileManager.CustomProfileName, StringComparison.OrdinalIgnoreCase))
            {
                if (_configManager.ReadAll())
                {
                    PopulateSettingsFromConfig();
                    _profileManager.SetActiveProfile(ProfileManager.CustomProfileName);
                    _statusLabel.Text = "Custom settings loaded from config files.";
                    _hasScalabilityGroupChanges = false;
                    LoadProfiles();
                    RestoreActiveProfile();
                }
                else
                {
                    _statusLabel.Text = "Error loading custom settings.";
                }
                return;
            }

            var profile = _profileManager.LoadProfile(profileName);

            if (profile == null)
            {
                MessageBox.Show($"Could not load profile '{profileName}'.", "Profile Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            foreach (TabPage tab in _tabControl.TabPages)
            {
                if (tab is CategoryTab categoryTab)
                {
                    foreach (var setting in categoryTab.Settings)
                    {
                        if (categoryTab.GetSettingControl(setting.Name) is SettingControl control)
                        {
                            string? value = null;

                            switch (setting.Source)
                            {
                                case ConfigSource.EngineIni:
                                    if (profile.EngineSettings.TryGetValue(setting.Section, out var engineSection) &&
                                        engineSection.TryGetValue(setting.Name, out var engineValue))
                                    {
                                        value = engineValue;
                                    }
                                    break;
                                case ConfigSource.GameUserSettings:
                                    if (profile.GameUserSettings.TryGetValue(setting.Section, out var gameUserSection) &&
                                        gameUserSection.TryGetValue(setting.Name, out var gameUserValue))
                                    {
                                        value = gameUserValue;
                                    }
                                    break;
                                case ConfigSource.ScalabilityGroups:
                                    if (profile.ScalabilityGroups.TryGetValue(setting.Name, out var sgValue))
                                    {
                                        value = sgValue;
                                    }
                                    break;
                            }

                            if (value != null)
                            {
                                control.Value = value;
                                control.IsNotSet = false;
                            }
                        }
                    }
                }
            }

            _profileManager.SetActiveProfile(profileName);
            _statusLabel.Text = $"Profile '{profileName}' loaded successfully.";
            _hasScalabilityGroupChanges = false;
            LoadProfiles();
            RestoreActiveProfile();
        }
        finally
        {
            _isLoadingProfile = false;
        }
    }

    private void RefreshSettings()
    {
        if (_configManager.ReadAll())
        {
            PopulateSettingsFromConfig();
            _statusLabel.Text = "Settings refreshed.";
            _hasScalabilityGroupChanges = false;
        }
        else
        {
            _statusLabel.Text = "Error refreshing settings.";
        }
    }

    private void ApplyButton_Click(object? sender, EventArgs e)
    {
        ApplySettings();
    }

    private void ResetButton_Click(object? sender, EventArgs e)
    {
        ResetSettings();
    }

    private void SaveProfileButton_Click(object? sender, EventArgs e)
    {
        SaveProfile();
    }

    private void LoadProfileButton_Click(object? sender, EventArgs e)
    {
        LoadProfile();
    }

    private void RefreshButton_Click(object? sender, EventArgs e)
    {
        RefreshSettings();
    }

    private void ProfileComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_profileComboBox.SelectedItem != null && _profileComboBox.Focused && !_isLoadingProfile)
        {
            LoadProfile();
        }
    }
}

public class InputForm : Form
{
    private readonly TextBox _inputTextBox;
    private readonly Button _okButton;
    private readonly Button _cancelButton;

    public string InputText => _inputTextBox.Text;

    public InputForm(string title, string prompt)
    {
        Text = title;
        Size = new Size(300, 150);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;

        var promptLabel = new Label
        {
            Text = prompt,
            AutoSize = true,
            Location = new Point(10, 10)
        };

        _inputTextBox = new TextBox
        {
            Location = new Point(10, 35),
            Width = 260
        };

        _okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(120, 70),
            Width = 75
        };

        _cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(205, 70),
            Width = 75
        };

        Controls.Add(promptLabel);
        Controls.Add(_inputTextBox);
        Controls.Add(_okButton);
        Controls.Add(_cancelButton);

        AcceptButton = _okButton;
        CancelButton = _cancelButton;
    }
}
