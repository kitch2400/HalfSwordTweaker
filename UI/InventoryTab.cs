using HalfSwordTweaker.Config;
using System.Windows.Forms;

namespace HalfSwordTweaker.UI;

/// <summary>
/// Represents a tab for editing inventory items.
/// </summary>
public class InventoryTab : TabPage
{
    private GameProgressManager _gameProgressManager = null!;
    private DataGridView _inventoryDataGridView = null!;
    private Button _refreshButton = null!;
    private Button _applyButton = null!;
    private Label _statusLabel = null!;
    private Panel _buttonPanel = null!;
    private Label _itemCountLabel = null!;
    private ComboBox _inventoryTypeComboBox = null!;
    private Panel _topPanel = null!;
    private List<InventoryItem> _inventoryItems = new();
    private bool _hasChanges;
    private enum InventoryType { Player, Insured }
    private InventoryType _currentInventoryType = InventoryType.Player;

    public string CategoryName { get; } = "Inventory & Equipment";
    public bool HasChanges => _hasChanges;

    public InventoryTab() : base("Inventory & Equipment")
    {
        _gameProgressManager = new GameProgressManager();
        
        InitializeComponent();
        LoadInventory();
    }

    private void InitializeComponent()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.White;

        // Main TableLayoutPanel for proper layout management
        var mainTableLayoutPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10),
            RowStyles = {
                new RowStyle(SizeType.AutoSize),  // Top panel - auto height
                new RowStyle(SizeType.Percent, 100F), // DataGridView - fills remaining space
                new RowStyle(SizeType.AutoSize)   // Button panel - auto height
            }
        };

        // Top panel for status label and ComboBox
        _topPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            MinimumSize = new Size(0, 40)
        };

        // Status label
        _statusLabel = new Label
        {
            Text = "Loading inventory...",
            AutoSize = true,
            Location = new Point(0, 8),
            ForeColor = Color.Gray,
            Font = new Font(FontFamily.GenericSansSerif, 9F)
        };

        // Inventory type ComboBox
        _inventoryTypeComboBox = new ComboBox
        {
            Location = new Point(200, 5),
            Size = new Size(130, 30),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font(FontFamily.GenericSansSerif, 9F, FontStyle.Bold)
        };
        _inventoryTypeComboBox.Items.Add("Player");
        _inventoryTypeComboBox.Items.Add("Insured");
        _inventoryTypeComboBox.SelectedIndex = 0;
        _inventoryTypeComboBox.SelectedIndexChanged += InventoryTypeComboBox_SelectedIndexChanged;

        _topPanel.Controls.Add(_statusLabel);
        _topPanel.Controls.Add(_inventoryTypeComboBox);

        // Inventory DataGridView
        _inventoryDataGridView = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.LightYellow },
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { Font = new Font(FontFamily.GenericSansSerif, 9F, FontStyle.Bold) },
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None
        };

        _inventoryDataGridView.Columns.Add("Index", "#");
        _inventoryDataGridView.Columns.Add("ItemName", "Item Name");
        _inventoryDataGridView.Columns.Add("ItemType", "Type");
        _inventoryDataGridView.Columns.Add("ObjectPath", "Object Path");

        // Button panel
        _buttonPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 50,
            Padding = new Padding(0, 10, 0, 0)
        };

        _refreshButton = new Button
        {
            Text = "Refresh",
            Size = new Size(100, 35),
            Location = new Point(0, 7),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _refreshButton.Click += RefreshButton_Click;

        _applyButton = new Button
        {
            Text = "Apply Changes",
            Size = new Size(120, 35),
            Location = new Point(110, 7),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            Enabled = false
        };
        _applyButton.Click += ApplyButton_Click;

        var itemCountLabel = new Label
        {
            Text = "Items: 0",
            AutoSize = true,
            Location = new Point(240, 12),
            ForeColor = Color.Green,
            Font = new Font(FontFamily.GenericSansSerif, 9F, FontStyle.Bold),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _itemCountLabel = itemCountLabel;

        _buttonPanel.Controls.Add(_refreshButton);
        _buttonPanel.Controls.Add(_applyButton);
        _buttonPanel.Controls.Add(itemCountLabel);

        // Add controls to TableLayoutPanel
        mainTableLayoutPanel.Controls.Add(_topPanel, 0, 0);
        mainTableLayoutPanel.Controls.Add(_inventoryDataGridView, 0, 1);
        mainTableLayoutPanel.Controls.Add(_buttonPanel, 0, 2);

        Controls.Add(mainTableLayoutPanel);
    }

    private void InventoryTypeComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        _currentInventoryType = _inventoryTypeComboBox.SelectedIndex == 0 
            ? InventoryType.Player 
            : InventoryType.Insured;
        LoadInventory();
    }

    public void LoadInventory()
    {
        try
        {
            if (!_gameProgressManager.SaveGameDirectoryExists())
            {
                _statusLabel.Text = "Save game directory not found.";
                _statusLabel.ForeColor = Color.Red;
                return;
            }

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var path = Path.Combine(appData, "HalfswordUE5", "Saved", "SaveGames", "GameProgress.sav");
            
            if (!File.Exists(path))
            {
                _statusLabel.Text = "GameProgress.sav not found.";
                _statusLabel.ForeColor = Color.Orange;
                _inventoryDataGridView.Rows.Clear();
                return;
            }

            var bytes = File.ReadAllBytes(path);
            var parser = new GameProgressParser(bytes);
            var inventory = _currentInventoryType == InventoryType.Player 
                ? parser.GetPlayerInventory() 
                : parser.GetInsuredItems();

            if (inventory == null || inventory.Count == 0)
            {
                _statusLabel.Text = $"{(_currentInventoryType == InventoryType.Player ? "Player" : "Insured")} inventory is empty or not found.";
                _statusLabel.ForeColor = Color.Orange;
                _itemCountLabel.Text = "Items: 0";
                _inventoryDataGridView.Rows.Clear();
                return;
            }

            _inventoryItems = inventory;
            _statusLabel.Text = $"{(_currentInventoryType == InventoryType.Player ? "Player" : "Insured")}: {inventory.Count} items";
            _statusLabel.ForeColor = Color.Green;
            _itemCountLabel.Text = $"{(_currentInventoryType == InventoryType.Player ? "Player" : "Insured")}: {inventory.Count} items";

            _inventoryDataGridView.Rows.Clear();
            for (int i = 0; i < inventory.Count; i++)
            {
                var item = inventory[i];
                // Extract just the item name from the object path
                string displayName = item.ItemName;
                if (!string.IsNullOrEmpty(item.ObjectPath))
                {
                    int lastSlash = item.ObjectPath.LastIndexOf('/');
                    int lastDot = item.ObjectPath.LastIndexOf('.');
                    if (lastSlash >= 0 && lastDot > lastSlash)
                    {
                        displayName = item.ObjectPath.Substring(lastSlash + 1, lastDot - lastSlash - 1);
                    }
                }
                
                _inventoryDataGridView.Rows.Add(
                    i + 1,
                    displayName,
                    item.ItemType,
                    item.ObjectPath
                );
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Error loading inventory: {ex.Message}";
            _statusLabel.ForeColor = Color.Red;
        }
    }

    public bool ApplyChanges()
    {
        if (!_hasChanges)
        {
            MessageBox.Show("No changes to apply.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }

        try
        {
            // TODO: Implement full inventory serialization
            MessageBox.Show("Full inventory editing is not yet implemented. This would require implementing complete GVAS ArrayProperty/StructProperty serialization.", 
                "Not Implemented", MessageBoxButtons.OK, MessageBoxIcon.Information);
            
            _hasChanges = false;
            _applyButton.Enabled = false;
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error applying changes: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private void RefreshButton_Click(object? sender, EventArgs e)
    {
        LoadInventory();
    }

    private void ApplyButton_Click(object? sender, EventArgs e)
    {
        ApplyChanges();
    }

    public void MarkAsModified()
    {
        _hasChanges = true;
        _applyButton.Enabled = true;
    }
}
