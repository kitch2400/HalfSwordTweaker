using HalfSwordTweaker.Config;
using System.Windows.Forms;
using System.IO;

namespace HalfSwordTweaker.UI;

/// <summary>
/// Clean, simplified Inventory & Equipment tab with full GVAS serialization support.
/// </summary>
public class InventoryTab : TabPage
{
    private InventoryManager _inventoryManager = null!;
    private DataGridView _inventoryDataGridView = null!;
    private Button _refreshButton = null!;
    private Button _insureAllButton = null!;
    private Button _importButton = null!;
    private Label _statusLabel = null!;
    private Panel _buttonPanel = null!;
    private Label _itemCountLabel = null!;
    private ComboBox _inventoryTypeComboBox = null!;
    private Panel _topPanel = null!;
    private List<InventoryItem> _inventoryItems = new();
    private bool _hasChanges;
    private enum InventoryType { Player, Insured }
    private InventoryType _currentInventoryType = InventoryType.Player;
    private DataGridViewButtonColumn _actionsColumn = null!;
    private DataGridViewButtonColumn _exportColumn = null!;

    public string CategoryName { get; } = "Inventory & Equipment";
    public bool HasChanges => _hasChanges;

    public InventoryTab() : base("Inventory & Equipment")
    {
        var devConfig = DevConfig.Load();
        var gameProgressPath = devConfig.DevelopmentMode 
            ? Path.Combine(AppContext.BaseDirectory, devConfig.SavePath, "GameProgress.sav")
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HalfSwordUE5", "Saved", "SaveGames", "GameProgress.sav");
        
        Console.WriteLine($"[InventoryTab] GameProgress.sav path: {gameProgressPath}");
        Console.WriteLine($"[InventoryTab] Dev mode: {devConfig.DevelopmentMode}");
        Console.WriteLine($"[InventoryTab] File exists: {File.Exists(gameProgressPath)}");
        
        _inventoryManager = new InventoryManager(gameProgressPath, devConfig.DevelopmentMode);
        
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
            ReadOnly = false,
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
        
        // Export button column
        _exportColumn = new DataGridViewButtonColumn
        {
            HeaderText = "Export",
            Text = "Export",
            UseColumnTextForButtonValue = true,
            Name = "ExportColumn"
        };
        _inventoryDataGridView.Columns.Add(_exportColumn);

        // Actions button column
        _actionsColumn = new DataGridViewButtonColumn
        {
            HeaderText = "Actions",
            Text = "Actions",
            UseColumnTextForButtonValue = true,
            Name = "ActionsColumn"
        };
        _inventoryDataGridView.Columns.Add(_actionsColumn);
        
        // Handle CellClick event on the DataGridView instead
        _inventoryDataGridView.CellClick += DataGridView_CellClick;

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

        _insureAllButton = new Button
        {
            Text = "Insure All",
            Size = new Size(100, 35),
            Location = new Point(110, 7),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _insureAllButton.Click += InsureAllButton_Click;

        _importButton = new Button
        {
            Text = "Import Item",
            Size = new Size(110, 35),
            Location = new Point(220, 7),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _importButton.Click += ImportButton_Click;

        var itemCountLabel = new Label
        {
            Text = "Items: 0",
            AutoSize = true,
            Location = new Point(350, 12),
            ForeColor = Color.Green,
            Font = new Font(FontFamily.GenericSansSerif, 9F, FontStyle.Bold),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _itemCountLabel = itemCountLabel;

        _buttonPanel.Controls.Add(_refreshButton);
        _buttonPanel.Controls.Add(_insureAllButton);
        _buttonPanel.Controls.Add(_importButton);
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
            if (!File.Exists(_inventoryManager.GameProgressPath))
            {
                _statusLabel.Text = "GameProgress.sav not found.";
                _statusLabel.ForeColor = Color.Red;
                return;
            }

            List<InventoryItem>? inventory = null;
            if (_currentInventoryType == InventoryType.Player)
                inventory = _inventoryManager.GetPlayerInventory();
            else
                inventory = _inventoryManager.GetInsuredItems();

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
                string displayName = item.ItemName;
                
                _inventoryDataGridView.Rows.Add(
                    i + 1,
                    displayName,
                    item.ItemType,
                    item.ObjectPath,
                    "Export",
                    "Edit"
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
            return true;
        }

        try
        {
            _hasChanges = false;
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

    private void ImportButton_Click(object? sender, EventArgs e)
    {
        ImportItem();
    }

    private void InsureAllButton_Click(object? sender, EventArgs e)
    {
        InsureAllItems();
    }

    public void MarkAsModified()
    {
        _hasChanges = true;
    }

    /// <summary>
    /// Opens file dialog to import an item from JSON.
    /// </summary>
    private void ImportItem()
    {
        using (var openFileDialog = new OpenFileDialog())
        {
            openFileDialog.Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*";
            openFileDialog.Title = "Import Inventory Item";
            
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var item = _inventoryManager.ImportItemFromFile(openFileDialog.FileName);
                    if (item == null)
                    {
                        MessageBox.Show("Failed to import item. Check the file format.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    
                    if (_currentInventoryType == InventoryType.Player)
                    {
                        if (_inventoryManager.AddItemToPlayerInventory(item))
                        {
                            MessageBox.Show($"Successfully imported '{item.ItemName}' to Player Inventory.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            LoadInventory();
                        }
                        else
                        {
                            MessageBox.Show("Failed to add item to player inventory.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        // For insured inventory, we need to implement AddItemToInsuredInventory
                        MessageBox.Show("Import to Insured Inventory is not yet implemented.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error importing item: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    /// <summary>
    /// Exports the selected item to a JSON file.
    /// </summary>
    private void ExportButton_Click(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        
        var item = _inventoryItems[e.RowIndex];
        
        using (var saveFileDialog = new SaveFileDialog())
        {
            saveFileDialog.Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*";
            saveFileDialog.Title = "Export Inventory Item";
            saveFileDialog.FileName = $"{item.ItemName}.json";
            
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    if (_inventoryManager.ExportItemToFile(item, saveFileDialog.FileName))
                    {
                        MessageBox.Show($"Successfully exported '{item.ItemName}' to {saveFileDialog.FileName}.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Failed to export item.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting item: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    /// <summary>
    /// Handles DataGridView cell clicks for Export and Actions buttons.
    /// </summary>
    private void DataGridView_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        
        // Check if clicked column is Export column (index 4)
        if (e.ColumnIndex == 4)
        {
            ExportButton_Click(sender, e);
        }
        // Check if clicked column is Actions column (index 5)
        else if (e.ColumnIndex == 5)
        {
            ActionsButton_Click(sender, e);
        }
    }
    
    /// <summary>
    /// Handles the Actions button click (Edit functionality).
    /// </summary>
    private void ActionsButton_Click(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        
        var item = _inventoryItems[e.RowIndex];
        
        // For now, show item details in a message box
        // Future: Implement a proper edit dialog
        string message = $"Item Name: {item.ItemName}\n" +
                        $"Type: {item.ItemType}\n" +
                        $"Object Path: {item.ObjectPath}\n" +
                        $"ID: {item.Id}\n" +
                        $"Core Removed: {item.CoreRemoved}\n" +
                        $"Module 1: {item.Module1}\n" +
                        $"Module 2: {item.Module2}\n" +
                        $"Module 3: {item.Module3}";
        
        MessageBox.Show(message, $"Edit: {item.ItemName}", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>
    /// Insures all items from player inventory that are not already insured.
    /// </summary>
    private void InsureAllItems()
    {
        try
        {
            Console.WriteLine("[InventoryTab] InsureAllItems: STARTED");
            
            // Show progress dialog
            var progressForm = new Form
            {
                Text = "Insuring Items...",
                Width = 300,
                Height = 150,
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog
            };
            
            var progressBar = new ProgressBar
            {
                Location = new Point(20, 40),
                Size = new Size(240, 25),
                Style = ProgressBarStyle.Marquee
            };
            
            var progressLabel = new Label
            {
                Location = new Point(20, 75),
                Text = "Processing...",
                AutoSize = true
            };
            
            progressForm.Controls.Add(progressBar);
            progressForm.Controls.Add(progressLabel);
            progressForm.Show();
            progressForm.Refresh();
            
            // Perform the insurance operation
            bool success = _inventoryManager.InsureAllItems();
            
            progressForm.Close();
            
            if (success)
            {
                MessageBox.Show("Successfully insured all items!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadInventory();
            }
            else
            {
                MessageBox.Show("Failed to insure items. Check the console logs for details.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error insuring items: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
