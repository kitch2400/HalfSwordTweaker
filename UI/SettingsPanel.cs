using System.ComponentModel;
using System.Globalization;
using HalfSwordTweaker.Config;

namespace HalfSwordTweaker.UI;

public class PerformanceIndicator : Control
{
    private PerformanceImpact _impact = PerformanceImpact.Medium;
    private Color _indicatorColor = Color.Yellow;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public PerformanceImpact Impact
    {
        get => _impact;
        set
        {
            _impact = value;
            UpdateColor();
            Invalidate();
        }
    }

    public PerformanceIndicator()
    {
        Width = 14;
        Height = 14;
        UpdateColor();
    }

    private void UpdateColor()
    {
        _indicatorColor = _impact switch
        {
            PerformanceImpact.Low => Color.FromArgb(100, 200, 100),
            PerformanceImpact.Medium => Color.FromArgb(230, 200, 50),
            PerformanceImpact.High => Color.FromArgb(230, 130, 50),
            PerformanceImpact.Epic => Color.FromArgb(220, 80, 80),
            _ => Color.Gray
        };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        
        using var brush = new SolidBrush(_indicatorColor);
        using var pen = new Pen(Color.FromArgb(100, 100, 100), 1);
        
        var rect = new Rectangle(1, 1, Width - 3, Height - 3);
        g.FillEllipse(brush, rect);
        g.DrawEllipse(pen, rect);
    }
}

public class SettingControl : Panel
{
    private readonly Label _label;
    private readonly Control _inputControl;
    private readonly PerformanceIndicator _impactIndicator;
    private readonly Label _notSetLabel;
    private readonly SettingDefinition? _definition;
    private readonly Dictionary<int, string>? _options;
    private readonly Dictionary<string, string>? _stringOptions;
    private Label? _valueLabel;

    public event EventHandler? ValueChanged;

    public SettingDefinition? Definition => _definition;

    public Control InputControl => _inputControl;

    public Control LabelControl => _label;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Value
    {
        get => _inputControl switch
        {
            TrackBar tb => tb.Value.ToString(),
            CheckBox cb => cb.Checked ? "1" : "0",
            NumericUpDown nud => nud.Value.ToString(CultureInfo.InvariantCulture),
            ComboBox cb => GetComboBoxValue(cb),
            ResolutionControl rc => $"{rc.SelectedWidth}x{rc.SelectedHeight}",
            _ => string.Empty
        };
        set
        {
            try
            {
                var normalizedValue = value.Replace(',', '.');

                if (_inputControl is TrackBar tb && double.TryParse(normalizedValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var trackValue))
                {
                    tb.Value = Math.Clamp((int)Math.Round(trackValue), tb.Minimum, tb.Maximum);
                    if (_valueLabel != null)
                    {
                        _valueLabel.Text = tb.Value.ToString();
                    }
                }
                else if (_inputControl is CheckBox cb)
                {
                    cb.Checked = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
                }
                else if (_inputControl is NumericUpDown nud && decimal.TryParse(normalizedValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var numericValue))
                {
                    nud.Value = Math.Clamp(numericValue, nud.Minimum, nud.Maximum);
                }
                else if (_inputControl is ComboBox comboBox)
                {
                    SetComboBoxValue(comboBox, normalizedValue);
                }
                else if (_inputControl is ResolutionControl rc && value.Contains('x'))
                {
                    var parts = value.Split('x');
                    if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
                    {
                        rc.SetResolution(w, h);
                    }
                }
            }
            catch
            {
            }
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsNotSet
    {
        get => _notSetLabel.Visible;
        set => _notSetLabel.Visible = value;
    }

    protected virtual void OnValueChanged()
    {
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private string GetComboBoxValue(ComboBox cb)
    {
        if (_stringOptions != null && cb.SelectedItem is string displayText)
        {
            var kvp = _stringOptions.FirstOrDefault(x => x.Value == displayText);
            return kvp.Key;
        }
        if (_options != null && cb.SelectedItem is string displayText2)
        {
            var kvp = _options.FirstOrDefault(x => x.Value == displayText2);
            return kvp.Key.ToString();
        }
        return cb.SelectedItem?.ToString() ?? string.Empty;
    }

    private void SetComboBoxValue(ComboBox cb, string value)
    {
        if (_stringOptions != null)
        {
            if (_stringOptions.TryGetValue(value, out var displayText))
            {
                cb.SelectedItem = displayText;
                return;
            }
        }
        if (_options != null && int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var intValue))
        {
            if (_options.TryGetValue(intValue, out var displayText))
            {
                cb.SelectedItem = displayText;
                return;
            }
        }
        cb.SelectedItem = value;
    }

    public SettingControl(SettingDefinition definition) : this(definition.Name, definition.Description, definition.Impact, definition)
    {
        _definition = definition;
    }

    private SettingControl(string name, string description, PerformanceImpact impact, SettingDefinition? definition = null)
    {
        Padding = new Padding(3);
        Margin = new Padding(2);
        Width = 520;
        Height = 65;
        Anchor = AnchorStyles.Left | AnchorStyles.Top;

        _label = new Label
        {
            Text = name,
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

        _impactIndicator = new PerformanceIndicator
        {
            Impact = impact,
            Location = new Point(500, 3)
        };

        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 20
        };
        headerPanel.Controls.Add(_label);
        headerPanel.Controls.Add(_notSetLabel);
        headerPanel.Controls.Add(_impactIndicator);

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

        _inputControl = CreateInputControl(definition, out var valueLabel);
        _inputControl.Dock = DockStyle.Top;
        _inputControl.Tag = definition?.Name ?? "";
        _options = definition?.Options;
        _stringOptions = definition?.StringOptions;
        _valueLabel = valueLabel;

        WireUpInputControlEvents();

        if (valueLabel != null)
        {
            var trackBarPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 30
            };
            _inputControl.Dock = DockStyle.Fill;
            valueLabel.Dock = DockStyle.Right;
            
            trackBarPanel.Controls.Add(_inputControl);
            trackBarPanel.Controls.Add(valueLabel);
            Controls.Add(trackBarPanel);
        }
        else
        {
            Controls.Add(_inputControl);
        }

        Controls.Add(descriptionLabel);
        Controls.Add(headerPanel);

        Dock = DockStyle.Top;
    }

    private void WireUpInputControlEvents()
    {
        switch (_inputControl)
        {
            case TrackBar tb:
                tb.Scroll += (s, e) => OnValueChanged();
                break;
            case CheckBox cb:
                cb.CheckedChanged += (s, e) => OnValueChanged();
                break;
            case ComboBox combo:
                combo.SelectedIndexChanged += (s, e) => OnValueChanged();
                break;
            case NumericUpDown nud:
                nud.ValueChanged += (s, e) => OnValueChanged();
                break;
        }
    }

    private static Control CreateInputControl(SettingDefinition? definition, out Label? valueLabel)
    {
        valueLabel = null;

        if (definition == null)
        {
            return new TextBox { Dock = DockStyle.Top, Enabled = false };
        }

        switch (definition.ControlType)
        {
            case ControlType.CheckBox:
            case ControlType.CompositeBoolean:
                var isChecked = definition.DefaultValue == "1" || 
                                definition.DefaultValue.Equals("true", StringComparison.OrdinalIgnoreCase);
                return new CheckBox
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    Checked = isChecked
                };

            case ControlType.NumericUpDown:
                var minVal = definition.MinValue ?? 0m;
                var maxVal = definition.MaxValue ?? 100m;
                var decimalPlaces = definition.DecimalPlaces > 0 ? definition.DecimalPlaces : 0;
                return new NumericUpDown
                {
                    Dock = DockStyle.Top,
                    Minimum = minVal,
                    Maximum = maxVal,
                    DecimalPlaces = decimalPlaces,
                    Increment = decimalPlaces == 0 ? 1m : (decimalPlaces == 1 ? 0.1m : 0.01m),
                    Value = decimal.TryParse(definition.DefaultValue.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var dv) ? Math.Clamp(dv, minVal, maxVal) : minVal
                };

            case ControlType.StringCombo:
                var stringComboBox = new ComboBox
                {
                    Dock = DockStyle.Top,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    AutoSize = true
                };

                if (definition.StringOptions != null)
                {
                    foreach (var kvp in definition.StringOptions)
                    {
                        stringComboBox.Items.Add(kvp.Value);
                    }
                    if (definition.StringOptions.Count > 0)
                    {
                        if (definition.StringOptions.TryGetValue(definition.DefaultValue, out var defaultText))
                        {
                            stringComboBox.SelectedItem = defaultText;
                        }
                        else
                        {
                            stringComboBox.SelectedIndex = 0;
                        }
                    }
                }
                return stringComboBox;

            case ControlType.ComboBox:
                var comboBox = new ComboBox
                {
                    Dock = DockStyle.Top,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    AutoSize = true
                };

                if (definition.Options != null)
                {
                    foreach (var kvp in definition.Options)
                    {
                        comboBox.Items.Add(kvp.Value);
                    }
                    if (definition.Options.Count > 0)
                    {
                        if (int.TryParse(definition.DefaultValue, out var defaultKey) && definition.Options.TryGetValue(defaultKey, out var defaultText))
                        {
                            comboBox.SelectedItem = defaultText;
                        }
                        else
                        {
                            comboBox.SelectedIndex = 0;
                        }
                    }
                }
                return comboBox;

            case ControlType.Resolution:
                return new ResolutionControl { Dock = DockStyle.Top };

            case ControlType.TrackBar:
            default:
                var minInt = (int)(definition.MinValue ?? 0);
                var maxInt = (int)(definition.MaxValue ?? 100);
                var initialValue = int.TryParse(definition.DefaultValue, out var intVal) ? Math.Clamp(intVal, minInt, maxInt) : minInt;
                
                var trackBar = new TrackBar
                {
                    Dock = DockStyle.Fill,
                    Minimum = minInt,
                    Maximum = maxInt,
                    Value = initialValue,
                    LargeChange = Math.Max(1, (maxInt - minInt) / 5),
                    SmallChange = 1
                };

                var lbl = new Label
                {
                    Text = initialValue.ToString(),
                    Width = 50,
                    TextAlign = ContentAlignment.MiddleRight,
                    Font = new Font(FontFamily.GenericSansSerif, 8F),
                    Padding = new Padding(5, 5, 5, 0)
                };

                trackBar.Scroll += (s, e) =>
                {
                    if (s is TrackBar tb)
                    {
                        lbl.Text = tb.Value.ToString();
                    }
                };

                valueLabel = lbl;
                return trackBar;
        }
    }
}

public class ResolutionControl : Panel
{
    private readonly ComboBox _comboBox;
    private readonly List<(int Width, int Height, string Display)> _resolutions;

    public int SelectedWidth { get; private set; }
    public int SelectedHeight { get; private set; }

    public ResolutionControl()
    {
        _resolutions = GetResolutions();
        
        _comboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        foreach (var res in _resolutions)
        {
            _comboBox.Items.Add(res.Display);
        }

        _comboBox.SelectedIndex = 0;
        _comboBox.SelectedIndexChanged += (s, e) =>
        {
            if (_comboBox.SelectedIndex >= 0 && _comboBox.SelectedIndex < _resolutions.Count)
            {
                var selected = _resolutions[_comboBox.SelectedIndex];
                SelectedWidth = selected.Width;
                SelectedHeight = selected.Height;
            }
        };

        Controls.Add(_comboBox);
        Height = 30;

        if (_resolutions.Count > 0)
        {
            SelectedWidth = _resolutions[0].Width;
            SelectedHeight = _resolutions[0].Height;
        }
    }

    public void SetResolution(int width, int height)
    {
        for (int i = 0; i < _resolutions.Count; i++)
        {
            if (_resolutions[i].Width == width && _resolutions[i].Height == height)
            {
                _comboBox.SelectedIndex = i;
                SelectedWidth = width;
                SelectedHeight = height;
                return;
            }
        }
        
        _comboBox.SelectedIndex = 0;
    }

    private static List<(int Width, int Height, string Display)> GetResolutions()
    {
        var resolutions = new List<(int, int, string)>();

        var currentWidth = System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Width ?? 1920;
        var currentHeight = System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Height ?? 1080;
        resolutions.Add((currentWidth, currentHeight, $"Current ({currentWidth}x{currentHeight})"));

        var presets = new[]
        {
            (1280, 720, "720p"),
            (1600, 900, "900p"),
            (1920, 1080, "1080p"),
            (2560, 1440, "1440p"),
            (3440, 1440, "Ultrawide 1440p"),
            (3840, 2160, "4K"),
            (5120, 2880, "5K"),
            (7680, 4320, "8K")
        };

        foreach (var preset in presets)
        {
            if (preset.Item1 != currentWidth || preset.Item2 != currentHeight)
            {
                resolutions.Add((preset.Item1, preset.Item2, $"{preset.Item3} ({preset.Item1}x{preset.Item2})"));
            }
        }

        return resolutions;
    }
}
