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

    public bool GameProgressExists() => File.Exists(_gameProgressPath);
    public bool SaveGameDirectoryExists() => Directory.Exists(Path.GetDirectoryName(_gameProgressPath));

    public Dictionary<string, object> ReadProperties()
    {
        var properties = new Dictionary<string, object>();

        if (!GameProgressExists())
        {
            Console.WriteLine("[GameProgressManager] GameProgress.sav not found");
            return properties;
        }

        try
        {
            var data = File.ReadAllBytes(_gameProgressPath);
            
            // Use working direct-search parser (struct parsing not yet implemented)
            var parser = new GameProgressParser(data);
            properties = parser.ParseProperties();
            
            Console.WriteLine($"[GameProgressManager] Parsed {properties.Count} properties");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameProgressManager] Error reading properties: {ex.Message}");
        }

        return properties;
    }

    /// <summary>
    /// Flatten nested GVAS properties into a flat dictionary with full property names.
    public bool WriteProperties(Dictionary<string, object> properties)
    {
        try
        {
            if (!GameProgressExists())
            {
                Console.WriteLine("[GameProgressManager] GameProgress.sav not found");
                return false;
            }

            CreateBackup();

            var data = File.ReadAllBytes(_gameProgressPath);
            var parser = new GameProgressParser(data);

            foreach (var kvp in properties)
            {
                parser.UpdateProperty(kvp.Key, kvp.Value);
            }

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
/// Parses and serializes GameProgress.sav GVAS format with struct support.
/// </summary>
public class GameProgressParser
{
    private byte[] _data;

    public GameProgressParser(byte[] data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public Dictionary<string, object> ParseProperties()
    {
        var properties = new Dictionary<string, object>();

        try
        {
            Console.WriteLine($"[GameProgressParser] Initialized parser with {_data.Length} bytes");

            if (!VerifyHeader())
            {
                Console.WriteLine("[GameProgressParser] Invalid GVAS header");
                return properties;
            }

            // Parse all properties
            foreach (var setting in GameProgressSettingsRegistry.Settings)
            {
                try
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
                catch (Exception ex)
                {
                    Console.WriteLine($"[GameProgressParser] Error reading {setting.Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameProgressParser] Error parsing properties: {ex.Message}");
        }

        return properties;
    }

    public bool UpdateProperty(string propertyName, object value)
    {
        try
        {
            Console.WriteLine($"[GameProgressParser] Updating {propertyName} to {value}");

            var setting = GameProgressSettingsRegistry.Settings.FirstOrDefault(s => s.Name == propertyName);
            if (setting == null)
            {
                Console.WriteLine($"[GameProgressParser] No definition found for '{propertyName}'");
                return false;
            }

            // Check if property is nested in a struct
            if (setting.ParentStruct != null)
            {
                return UpdatePropertyInStruct(setting.ParentStruct, propertyName, value, setting.PropertyType);
            }

            // Global property update
            int propertyStart = FindPropertyOffset(propertyName);
            if (propertyStart < 0)
            {
                Console.WriteLine($"[GameProgressParser] Property '{propertyName}' not found");
                return false;
            }

            UpdatePropertyValue(propertyStart, setting.PropertyType, value);
            Console.WriteLine($"[GameProgressParser] Successfully updated {propertyName}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameProgressParser] Error updating property: {ex.Message}");
            return false;
        }
    }

    public byte[] GetData() => (byte[])_data.Clone();

    private bool VerifyHeader()
    {
        if (_data.Length < 48)
        {
            Console.WriteLine("[GameProgressParser] File too short");
            return false;
        }

        if (_data[0] != 0x47 || _data[1] != 0x56 || _data[2] != 0x41 || _data[3] != 0x53)
        {
            Console.WriteLine("[GameProgressParser] Invalid GVAS magic");
            return false;
        }

        Console.WriteLine("[GameProgressParser] GVAS header verified");
        return true;
    }

    /// <summary>
    /// Find property by name, checking parent struct if applicable.
    /// </summary>
    private object? FindPropertyByName(string propertyName)
    {
        var setting = GameProgressSettingsRegistry.Settings.FirstOrDefault(s => s.Name == propertyName);
        if (setting == null) return null;

        // Check if property is nested in a struct
        if (setting.ParentStruct != null)
        {
            return FindPropertyInStruct(setting.ParentStruct, propertyName, setting.PropertyType);
        }

        // Global search for non-nested properties
        return FindPropertyByNameGlobal(propertyName, setting.PropertyType);
    }

    /// <summary>
    /// Find property within Player Character struct by searching for the property name directly.
    /// </summary>
    private object? FindPropertyInStruct(string structName, string propertyName, GvasPropertyType propertyType)
    {
        try
        {
            // For Player Character struct, search directly for the nested property name
            // since the struct parsing is complex and error-prone
            if (structName == "Player Character")
            {
                // Extract just the property name part after the dot (e.g., "Height_21_..." from "Player Character_0.Height_21_...")
                int dotIndex = propertyName.IndexOf('.');
                string actualPropertyName = dotIndex > 0 ? propertyName.Substring(dotIndex + 1) : propertyName;
                return FindNestedPropertyDirectly(actualPropertyName, propertyType);
            }

            Console.WriteLine($"[GameProgressParser] Struct '{structName}' not supported for nested properties yet");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameProgressParser] Error reading from struct '{structName}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Search for a nested property by name directly in the file.
    /// </summary>
    private object? FindNestedPropertyDirectly(string propertyName, GvasPropertyType propertyType)
    {
        var nameBytes = Encoding.UTF8.GetBytes(propertyName);
        var nameWithNull = new byte[nameBytes.Length + 1];
        Array.Copy(nameBytes, nameWithNull, nameBytes.Length);

        // Search for the property name (it should be unique in the file)
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
                Console.WriteLine($"[GameProgressParser] Found '{propertyName}' at offset {i}");

                // Parse property header to get value
                int pos = i + nameWithNull.Length;

                // Read type length
                if (pos + 4 > _data.Length) return null;
                int typeLen = BitConverter.ToInt32(_data, pos);
                pos += 4;

                if (typeLen < 0 || typeLen > 100 || pos + typeLen > _data.Length) return null;
                pos += typeLen; // Skip type name

                // Skip unknown (4 bytes for nested properties)
                if (pos + 4 > _data.Length) return null;
                pos += 4;

                // Read size
                if (pos + 4 > _data.Length) return null;
                int size = BitConverter.ToInt32(_data, pos);
                pos += 4;

                // Skip array index (1 byte)
                if (pos + 1 > _data.Length) return null;
                pos += 1;

                // Read value
                return ReadValueAtPosition(pos, size, propertyType, _data.Length);
            }
        }

        Console.WriteLine($"[GameProgressParser] Property '{propertyName}' not found");
        return null;
    }

    /// <summary>
    /// Find a nested property within struct boundaries.
    /// </summary>
    private object? FindNestedProperty(int startPos, int endPos, string propertyName, GvasPropertyType propertyType)
    {
        int pos = startPos;

        while (pos < endPos - 10)
        {
            // Read nested property name
            if (pos >= _data.Length || _data[pos] == 0)
            {
                pos++;
                continue;
            }

            int nameStart = pos;
            while (pos < endPos && _data[pos] != 0) pos++;
            if (pos >= endPos) break;

            var propNameBytes = new byte[pos - nameStart];
            Array.Copy(_data, nameStart, propNameBytes, 0, propNameBytes.Length);
            var propName = Encoding.UTF8.GetString(propNameBytes);
            pos++; // Skip null

            // Check if this is the property we're looking for
            if (propName == propertyName || propName == propertyName + "_0")
            {
                Console.WriteLine($"[GameProgressParser] Found nested property '{propertyName}' at offset {pos - propName.Length - 1}");

                // Read type length
                if (pos + 4 > endPos) return null;
                int typeLen = BitConverter.ToInt32(_data, pos);
                pos += 4;

                if (pos + typeLen > endPos) return null;
                pos += typeLen; // Skip type name

                // Skip unknown (4 bytes for regular properties)
                if (pos + 4 > endPos) return null;
                pos += 4;

                // Read size
                if (pos + 4 > endPos) return null;
                int size = BitConverter.ToInt32(_data, pos);
                pos += 4;

                // Skip array index (1 byte)
                if (pos + 1 > endPos) return null;
                pos += 1;

                // Read value based on type
                return ReadValueAtPosition(pos, size, propertyType, endPos);
            }
            else
            {
                // Skip this nested property
                if (pos + 4 > endPos) break;
                int typeLen = BitConverter.ToInt32(_data, pos);
                pos += 4;

                if (pos + typeLen > endPos) break;
                pos += typeLen;

                if (pos + 4 > endPos) break;
                pos += 4;

                if (pos + 4 > endPos) break;
                int size = BitConverter.ToInt32(_data, pos);
                pos += 4;

                if (pos + 1 > endPos) break;
                pos += 1; // Skip array index

                if (size < 0 || pos + size > endPos) break;
                pos += size; // Skip value
            }
        }

        Console.WriteLine($"[GameProgressParser] Property '{propertyName}' not found in struct");
        return null;
    }

    /// <summary>
    /// Global property search (for non-nested properties).
    /// </summary>
    private object? FindPropertyByNameGlobal(string propertyName, GvasPropertyType propertyType)
    {
        int offset = FindPropertyOffset(propertyName);
        if (offset < 0)
        {
            Console.WriteLine($"[GameProgressParser] Property '{propertyName}' not found (tried with _0 suffix)");
            return null;
        }

        return ReadPropertyValue(offset, propertyType);
    }

    /// <summary>
    /// Find the offset of a property by name (global search).
    /// </summary>
    private int FindPropertyOffset(string propertyName)
    {
        var nameBytes = Encoding.UTF8.GetBytes(propertyName);
        var nameWithNull = new byte[nameBytes.Length + 1];
        Array.Copy(nameBytes, nameWithNull, nameBytes.Length);

        // Search backwards from the end
        for (int i = _data.Length - nameWithNull.Length; i >= 0; i--)
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
                int pos = i + nameWithNull.Length;
                if (pos + 4 <= _data.Length)
                {
                    int typeLength = BitConverter.ToInt32(_data, pos);
                    if (typeLength >= 5 && typeLength <= 30 && pos + typeLength + 8 <= _data.Length)
                    {
                        return i;
                    }
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// Read property value at given offset.
    /// </summary>
    private object? ReadPropertyValue(int propertyOffset, GvasPropertyType propertyType)
    {
        try
        {
            int pos = propertyOffset;

            // Skip property name + null
            while (pos < _data.Length && _data[pos] != 0) pos++;
            pos++;

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

            // Skip array index (1 byte)
            pos += 1;

            return ReadValueAtPosition(pos, size, propertyType, _data.Length);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameProgressParser] Error reading value: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Read value at position based on type and size.
    /// </summary>
    private object? ReadValueAtPosition(int pos, int size, GvasPropertyType propertyType, int maxPos)
    {
        switch (propertyType)
        {
            case GvasPropertyType.IntProperty:
                if (pos + 4 <= maxPos)
                    return BitConverter.ToInt32(_data, pos);
                return null;

            case GvasPropertyType.BoolProperty:
                if (pos + 1 <= maxPos)
                    return _data[pos] != 0;
                return null;

            case GvasPropertyType.DoubleProperty:
                if (pos + 8 <= maxPos)
                    return BitConverter.ToDouble(_data, pos);
                return null;

            case GvasPropertyType.ByteProperty:
                if (pos + 1 <= maxPos && size >= 1)
                    return _data[pos];
                return null;

            default:
                return null;
        }
    }

    /// <summary>
    /// Update property value at given offset.
    /// </summary>
    private void UpdatePropertyValue(int propertyOffset, GvasPropertyType propertyType, object value)
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

        // Skip array index (1 byte)
        pos += 1;

        // Write new value based on type
        switch (propertyType)
        {
            case GvasPropertyType.IntProperty:
                if (value is int intValue)
                {
                    byte[] valueBytes = BitConverter.GetBytes(intValue);
                    Array.Copy(valueBytes, 0, _data, pos, 4);
                }
                break;

            case GvasPropertyType.BoolProperty:
                _data[pos] = value is bool bVal ? (byte)(bVal ? 1 : 0) : (byte)((int)value != 0 ? 1 : 0);
                break;

            case GvasPropertyType.DoubleProperty:
                if (value is double doubleValue)
                {
                    byte[] valueBytes = BitConverter.GetBytes(doubleValue);
                    Array.Copy(valueBytes, 0, _data, pos, 8);
                }
                else if (value is int intVal)
                {
                    byte[] valueBytes = BitConverter.GetBytes((double)intVal);
                    Array.Copy(valueBytes, 0, _data, pos, 8);
                }
                break;

            case GvasPropertyType.ByteProperty:
                if (value is byte byteValue)
                {
                    _data[pos] = byteValue;
                }
                else if (value is int iValue && iValue >= 0 && iValue <= 255)
                {
                    _data[pos] = (byte)iValue;
                }
                break;
        }
    }

    /// <summary>
    /// Update property within Player Character struct.
    /// </summary>
    private bool UpdatePropertyInStruct(string structName, string propertyName, object value, GvasPropertyType propertyType)
    {
        // For Player Character struct, search directly for the property
        if (structName == "Player Character")
        {
            return UpdateNestedPropertyDirectly(propertyName, propertyType, value);
        }

        Console.WriteLine($"[GameProgressParser] Struct '{structName}' update not supported yet");
        return false;
    }

    /// <summary>
    /// Update a nested property by searching for it directly.
    /// </summary>
    private bool UpdateNestedPropertyDirectly(string propertyName, GvasPropertyType propertyType, object value)
    {
        var nameBytes = Encoding.UTF8.GetBytes(propertyName);
        var nameWithNull = new byte[nameBytes.Length + 1];
        Array.Copy(nameBytes, nameWithNull, nameBytes.Length);

        // Search for the property name
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
                Console.WriteLine($"[GameProgressParser] Found '{propertyName}' for update at offset {i}");

                int pos = i + nameWithNull.Length;

                // Skip type length
                if (pos + 4 > _data.Length) return false;
                int typeLen = BitConverter.ToInt32(_data, pos);
                pos += 4;

                if (typeLen < 0 || typeLen > 100 || pos + typeLen > _data.Length) return false;
                pos += typeLen;

                // Skip unknown (4 bytes)
                if (pos + 4 > _data.Length) return false;
                pos += 4;

                // Skip size
                if (pos + 4 > _data.Length) return false;
                pos += 4;

                // Skip array index (1 byte)
                if (pos + 1 > _data.Length) return false;
                pos += 1;

                // Write new value
                switch (propertyType)
                {
                    case GvasPropertyType.IntProperty:
                        if (value is int intValue)
                        {
                            byte[] valueBytes = BitConverter.GetBytes(intValue);
                            Array.Copy(valueBytes, 0, _data, pos, 4);
                        }
                        break;

                    case GvasPropertyType.DoubleProperty:
                        if (value is double doubleValue)
                        {
                            byte[] valueBytes = BitConverter.GetBytes(doubleValue);
                            Array.Copy(valueBytes, 0, _data, pos, 8);
                        }
                        else if (value is int intVal)
                        {
                            byte[] valueBytes = BitConverter.GetBytes((double)intVal);
                            Array.Copy(valueBytes, 0, _data, pos, 8);
                        }
                        break;

                    case GvasPropertyType.ByteProperty:
                        if (value is byte byteValue)
                        {
                            _data[pos] = byteValue;
                        }
                        else if (value is int iValue && iValue >= 0 && iValue <= 255)
                        {
                            _data[pos] = (byte)iValue;
                        }
                        break;
                }

                Console.WriteLine($"[GameProgressParser] Successfully updated {propertyName}");
                return true;
            }
        }

        Console.WriteLine($"[GameProgressParser] Property '{propertyName}' not found for update");
        return false;
    }

    /// <summary>
    /// Update a nested property within struct boundaries.
    /// </summary>
    private bool UpdateNestedProperty(int startPos, int endPos, string propertyName, GvasPropertyType propertyType, object value)
    {
        int pos = startPos;

        while (pos < endPos - 10)
        {
            // Read nested property name
            if (pos >= _data.Length || _data[pos] == 0)
            {
                pos++;
                continue;
            }

            int nameStart = pos;
            while (pos < endPos && _data[pos] != 0) pos++;
            if (pos >= endPos) break;

            var propNameBytes = new byte[pos - nameStart];
            Array.Copy(_data, nameStart, propNameBytes, 0, propNameBytes.Length);
            var propName = Encoding.UTF8.GetString(propNameBytes);
            pos++; // Skip null

            // Check if this is the property we're looking for
            if (propName == propertyName || propName == propertyName + "_0")
            {
                Console.WriteLine($"[GameProgressParser] Found nested property '{propertyName}' for update");

                // Skip type length
                if (pos + 4 > endPos) return false;
                int typeLen = BitConverter.ToInt32(_data, pos);
                pos += 4;

                // Skip type name
                if (pos + typeLen > endPos) return false;
                pos += typeLen;

                // Skip unknown (4 bytes)
                if (pos + 4 > endPos) return false;
                pos += 4;

                // Skip size
                if (pos + 4 > endPos) return false;
                pos += 4;

                // Skip array index (1 byte)
                if (pos + 1 > endPos) return false;
                pos += 1;

                // Write new value
                switch (propertyType)
                {
                    case GvasPropertyType.IntProperty:
                        if (value is int intValue)
                        {
                            byte[] valueBytes = BitConverter.GetBytes(intValue);
                            Array.Copy(valueBytes, 0, _data, pos, 4);
                        }
                        break;

                    case GvasPropertyType.DoubleProperty:
                        if (value is double doubleValue)
                        {
                            byte[] valueBytes = BitConverter.GetBytes(doubleValue);
                            Array.Copy(valueBytes, 0, _data, pos, 8);
                        }
                        else if (value is int intVal)
                        {
                            byte[] valueBytes = BitConverter.GetBytes((double)intVal);
                            Array.Copy(valueBytes, 0, _data, pos, 8);
                        }
                        break;

                    case GvasPropertyType.ByteProperty:
                        if (value is byte byteValue)
                        {
                            _data[pos] = byteValue;
                        }
                        else if (value is int iValue && iValue >= 0 && iValue <= 255)
                        {
                            _data[pos] = (byte)iValue;
                        }
                        break;
                }

                Console.WriteLine($"[GameProgressParser] Successfully updated {propertyName} in struct");
                return true;
            }
            else
            {
                // Skip this nested property
                if (pos + 4 > endPos) break;
                int typeLen = BitConverter.ToInt32(_data, pos);
                pos += 4;

                if (pos + typeLen > endPos) break;
                pos += typeLen;

                if (pos + 4 > endPos) break;
                pos += 4;

                if (pos + 4 > endPos) break;
                int size = BitConverter.ToInt32(_data, pos);
                pos += 4;

                if (pos + 1 > endPos) break;
                pos += 1; // Skip array index

                if (size < 0 || pos + size > endPos) break;
                pos += size; // Skip value
            }
        }

        Console.WriteLine($"[GameProgressParser] Property '{propertyName}' not found in struct for update");
        return false;
    }

    private bool TryConvertToInt(double value, out int result)
    {
        result = (int)value;
        return true;
    }
}
