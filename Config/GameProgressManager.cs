using System.Text;

namespace HalfSwordTweaker.Config;

/// <summary>
/// Manages reading and writing GameProgress.sav files.
/// </summary>
public class GameProgressManager
{
    private readonly string _gameProgressPath;
    private readonly string _backupPath;

    public GameProgressManager()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _gameProgressPath = Path.Combine(appData, "HalfswordUE5", "Saved", "SaveGames", "GameProgress.sav");
        _backupPath = Path.Combine(appData, "HalfSwordTweaker", "Backups", "GameProgress.sav.bak");
    }

    /// <summary>
    /// Checks if the GameProgress.sav file exists.
    /// </summary>
    public bool GameProgressExists()
    {
        return File.Exists(_gameProgressPath);
    }

    /// <summary>
    /// Checks if the save game directory exists.
    /// </summary>
    public bool SaveGameDirectoryExists()
    {
        var dir = Path.GetDirectoryName(_gameProgressPath);
        return dir != null && Directory.Exists(dir);
    }

    /// <summary>
    /// Reads all properties from GameProgress.sav.
    /// </summary>
    public Dictionary<string, object> ReadProperties()
    {
        var properties = new Dictionary<string, object>();

        if (!GameProgressExists())
        {
            return properties;
        }

        try
        {
            var data = File.ReadAllBytes(_gameProgressPath);
            var parser = new GameProgressParser(data);
            properties = parser.ParseProperties();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameProgressManager] Error reading properties: {ex.Message}");
        }

        return properties;
    }

    /// <summary>
    /// Writes updated properties to GameProgress.sav.
    /// </summary>
    public bool WriteProperties(Dictionary<string, object> properties)
    {
        try
        {
            if (!GameProgressExists())
            {
                Console.WriteLine("[GameProgressManager] GameProgress.sav not found");
                return false;
            }

            // Create backup before modifying
            CreateBackup();

            var data = File.ReadAllBytes(_gameProgressPath);
            var parser = new GameProgressParser(data);

            // Update each property
            foreach (var kvp in properties)
            {
                parser.UpdateProperty(kvp.Key, kvp.Value);
            }

            // Write back to file
            File.WriteAllBytes(_gameProgressPath, parser.GetData());
            Console.WriteLine($"[GameProgressManager] Successfully wrote GameProgress.sav");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameProgressManager] Error writing properties: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Creates a backup of the GameProgress.sav file.
    /// </summary>
    private void CreateBackup()
    {
        try
        {
            var backupDir = Path.GetDirectoryName(_backupPath);
            if (backupDir != null && !Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            if (File.Exists(_gameProgressPath))
            {
                File.Copy(_gameProgressPath, _backupPath, true);
                Console.WriteLine($"[GameProgressManager] Backup created at {_backupPath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameProgressManager] Error creating backup: {ex.Message}");
        }
    }
}

/// <summary>
/// Parses and serializes GameProgress.sav GVAS format.
/// </summary>
public class GameProgressParser
{
    private byte[] _data;

    public GameProgressParser(byte[] data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    /// <summary>
    /// Parses all properties from the GVAS data.
    /// </summary>
    public Dictionary<string, object> ParseProperties()
    {
        var properties = new Dictionary<string, object>();

        try
        {
            // Verify GVAS header
            if (!VerifyHeader())
            {
                Console.WriteLine("[GameProgressParser] Invalid GVAS header");
                return properties;
            }

            // Parse known properties
            foreach (var setting in GameProgressSettingsRegistry.Settings)
            {
                var value = FindPropertyByName(setting.Name);
                if (value != null)
                {
                    properties[setting.Name] = value;
                    Console.WriteLine($"[GameProgressParser] Found {setting.Name} = {value}");
                }
                else
                {
                    Console.WriteLine($"[GameProgressParser] Property '{setting.Name}' not found");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameProgressParser] Error parsing properties: {ex.Message}");
        }

        return properties;
    }

    /// <summary>
    /// Updates a property value in the GVAS data.
    /// </summary>
    public bool UpdateProperty(string propertyName, object value)
    {
        try
        {
            Console.WriteLine($"[GameProgressParser] Updating {propertyName} to {value}");

            // Find the property
            int propertyStart = FindPropertyOffset(propertyName);
            if (propertyStart < 0)
            {
                Console.WriteLine($"[GameProgressParser] Property '{propertyName}' not found");
                return false;
            }

            // Get property type
            var setting = GameProgressSettingsRegistry.Settings.FirstOrDefault(s => s.Name == propertyName);
            if (setting == null)
            {
                Console.WriteLine($"[GameProgressParser] No definition found for '{propertyName}'");
                return false;
            }

            // Update based on property type
            switch (setting.PropertyType)
            {
                case GvasPropertyType.IntProperty:
                    if (value is int intValue || value is double dValue && TryConvertToInt(dValue, out intValue))
                    {
                        UpdateIntProperty(propertyStart, intValue);
                    }
                    break;

                case GvasPropertyType.BoolProperty:
                    if (value is bool boolValue)
                    {
                        UpdateBoolProperty(propertyStart, boolValue);
                    }
                    else if (value is int iValue)
                    {
                        UpdateBoolProperty(propertyStart, iValue != 0);
                    }
                    break;

                case GvasPropertyType.DoubleProperty:
                    if (value is double doubleValue)
                    {
                        UpdateDoubleProperty(propertyStart, doubleValue);
                    }
                    else if (value is int iValue)
                    {
                        UpdateDoubleProperty(propertyStart, (double)iValue);
                    }
                    break;
            }

            Console.WriteLine($"[GameProgressParser] Successfully updated {propertyName}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameProgressParser] Error updating property: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Returns the modified data.
    /// </summary>
    public byte[] GetData()
    {
        return (byte[])_data.Clone();
    }

    /// <summary>
    /// Verifies the GVAS header.
    /// </summary>
    private bool VerifyHeader()
    {
        if (_data.Length < 48)
        {
            Console.WriteLine("[GameProgressParser] File too short");
            return false;
        }

        // Check for "GVAS" magic
        if (_data[0] != 0x47 || _data[1] != 0x56 || _data[2] != 0x41 || _data[3] != 0x53)
        {
            Console.WriteLine("[GameProgressParser] Invalid GVAS magic");
            return false;
        }

        Console.WriteLine("[GameProgressParser] GVAS header verified");
        return true;
    }

    /// <summary>
    /// Finds a property by name and returns its value.
    /// </summary>
    private object? FindPropertyByName(string propertyName)
    {
        int offset = FindPropertyOffset(propertyName);
        if (offset < 0) return null;

        var setting = GameProgressSettingsRegistry.Settings.FirstOrDefault(s => s.Name == propertyName);
        if (setting == null) return null;

        return ReadPropertyValue(offset, setting.PropertyType);
    }

    /// <summary>
    /// Finds the offset of a property by name.
    /// </summary>
    private int FindPropertyOffset(string propertyName)
    {
        var nameBytes = Encoding.UTF8.GetBytes(propertyName);
        var nameWithNull = new byte[nameBytes.Length + 1];
        Array.Copy(nameBytes, nameWithNull, nameBytes.Length);

        for (int i = 0; i <= _data.Length - nameWithNull.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < nameWithNull.Length; j++)
            {
                if (_data[i + j] != nameWithNull[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Reads a property value at the given offset.
    /// </summary>
    private object? ReadPropertyValue(int propertyOffset, GvasPropertyType propertyType)
    {
        try
        {
            int pos = propertyOffset;

            // Skip property name + null terminator
            while (pos < _data.Length && _data[pos] != 0) pos++;
            pos++; // Skip null

            // Read type length
            if (pos + 4 > _data.Length) return null;
            int typeLen = BitConverter.ToInt32(_data, pos);
            pos += 4;

            // Skip type name
            pos += typeLen;

            // Skip unknown (4 bytes)
            pos += 4;

            // Read size
            if (pos + 4 > _data.Length) return null;
            int size = BitConverter.ToInt32(_data, pos);
            pos += 4;

            // Read value based on type
            switch (propertyType)
            {
                case GvasPropertyType.IntProperty:
                    if (pos + 4 <= _data.Length)
                        return BitConverter.ToInt32(_data, pos);
                    return null;

                case GvasPropertyType.BoolProperty:
                    if (pos + 1 <= _data.Length)
                        return _data[pos] != 0;
                    return null;

                case GvasPropertyType.DoubleProperty:
                    if (pos + 8 <= _data.Length)
                        return BitConverter.ToDouble(_data, pos);
                    return null;

                default:
                    return null;
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameProgressParser] Error reading value: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Updates an IntProperty value.
    /// </summary>
    private void UpdateIntProperty(int propertyOffset, int value)
    {
        int pos = propertyOffset;

        // Skip property name + null
        while (pos < _data.Length && _data[pos] != 0) pos++;
        pos++;

        // Skip type length (4 bytes)
        pos += 4;

        // Read type length to skip type name
        int typeLen = BitConverter.ToInt32(_data, pos - 4);
        pos += typeLen;

        // Skip unknown (4 bytes)
        pos += 4;

        // Skip size field (4 bytes)
        pos += 4;

        // Write new int value
        byte[] valueBytes = BitConverter.GetBytes(value);
        Array.Copy(valueBytes, 0, _data, pos, 4);
    }

    /// <summary>
    /// Updates a BoolProperty value.
    /// </summary>
    private void UpdateBoolProperty(int propertyOffset, bool value)
    {
        int pos = propertyOffset;

        // Skip property name + null
        while (pos < _data.Length && _data[pos] != 0) pos++;
        pos++;

        // Skip type length (4 bytes)
        pos += 4;

        // Read type length to skip type name
        int typeLen = BitConverter.ToInt32(_data, pos - 4);
        pos += typeLen;

        // Skip unknown (4 bytes)
        pos += 4;

        // Skip size field (4 bytes)
        pos += 4;

        // Write new bool value
        _data[pos] = value ? (byte)1 : (byte)0;
    }

    /// <summary>
    /// Updates a DoubleProperty value.
    /// </summary>
    private void UpdateDoubleProperty(int propertyOffset, double value)
    {
        int pos = propertyOffset;

        // Skip property name + null
        while (pos < _data.Length && _data[pos] != 0) pos++;
        pos++;

        // Skip type length (4 bytes)
        pos += 4;

        // Read type length to skip type name
        int typeLen = BitConverter.ToInt32(_data, pos - 4);
        pos += typeLen;

        // Skip unknown (4 bytes)
        pos += 4;

        // Skip size field (4 bytes)
        pos += 4;

        // Write new double value
        byte[] valueBytes = BitConverter.GetBytes(value);
        Array.Copy(valueBytes, 0, _data, pos, 8);
    }

    private bool TryConvertToInt(double value, out int result)
    {
        result = (int)value;
        return true;
    }
}
