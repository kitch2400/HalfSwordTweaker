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
        var devConfig = DevConfig.Load();
        
        if (devConfig.DevelopmentMode)
        {
            var baseDir = AppContext.BaseDirectory;
            _gameProgressPath = Path.Combine(baseDir, devConfig.SavePath, "GameProgress.sav");
            _backupPath = Path.Combine(baseDir, devConfig.BackupPath, "GameProgress.sav.bak");
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _gameProgressPath = Path.Combine(appData, "HalfswordUE5", "Saved", "SaveGames", "GameProgress.sav");
            _backupPath = Path.Combine(appData, "HalfSwordTweaker", "Backups", "GameProgress.sav.bak");
        }
    }

    public string GameProgressPath => _gameProgressPath;
    public bool GameProgressExists() => File.Exists(_gameProgressPath);
    public bool SaveGameDirectoryExists() => Directory.Exists(Path.GetDirectoryName(_gameProgressPath));

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
            
            // Use working direct-search parser (struct parsing not yet implemented)
            var parser = new GameProgressParser(data);
            properties = parser.ParseProperties();
        }
        catch
        {
            // Silently fail - file may not exist
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
            return true;
        }
        catch
        {
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
            }
        }
        catch
        {
            // Silently fail - backup not critical
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

            if (!VerifyHeader())
            {
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
                    }
                }
                catch (Exception ex)
                {
                    // Log parsing errors for debugging
                    System.Diagnostics.Debug.WriteLine($"Error parsing property '{setting?.Name}': {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            // Log general parsing errors for debugging
            System.Diagnostics.Debug.WriteLine($"Error reading properties: {ex.Message}");
        }

        return properties;
    }

    public bool UpdateProperty(string propertyName, object value)
    {
        try
        {

            var setting = GameProgressSettingsRegistry.Settings.FirstOrDefault(s => s.Name == propertyName);
            if (setting == null)
            {
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
                return false;
            }

            UpdatePropertyValue(propertyStart, setting.PropertyType, value);
            return true;
        }
        catch { return false; }
    }

    public byte[] GetData() => (byte[])_data.Clone();

    private bool VerifyHeader()
    {
        if (_data.Length < 48)
        {
            return false;
        }

        if (_data[0] != 0x47 || _data[1] != 0x56 || _data[2] != 0x41 || _data[3] != 0x53)
        {
            return false;
        }
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

        // Special handling for ArrayProperty (Inventory)
        if (setting.PropertyType == GvasPropertyType.ArrayProperty)
        {
            return ParseInventoryArray(propertyName);
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
            // For Player Character struct, search within the struct boundaries
            if (structName == "Player Character")
            {
                // Find the Player Character_0 StructProperty
                int structOffset = FindPlayerCharacterStructOffset();
                if (structOffset < 0)
                {
                    // Fallback to global search if struct not found
                    int dotIndex = propertyName.IndexOf('.');
                    string actualPropertyName = dotIndex > 0 ? propertyName.Substring(dotIndex + 1) : propertyName;
                    return FindNestedPropertyDirectly(actualPropertyName, propertyType);
                }

                // Parse struct to find its end boundary
                int structEnd = ParseStructPropertyBoundary(structOffset);

                // Extract just the property name part after the dot
                int dotIndex2 = propertyName.IndexOf('.');
                string actualPropertyName2 = dotIndex2 > 0 ? propertyName.Substring(dotIndex2 + 1) : propertyName;
                
                // Search within struct boundaries
                return FindNestedPropertyDirectly(actualPropertyName2, propertyType, structOffset, structEnd);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Find the Player Character_0 StructProperty offset.
    /// </summary>
    private int FindPlayerCharacterStructOffset()
    {
        
        // Search for "Player Character" property name (without _0 suffix for flexibility)
        var searchName = Encoding.UTF8.GetBytes("Player Character");
        for (int i = 0; i <= _data.Length - searchName.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < searchName.Length; j++)
            {
                if (_data[i + j] != searchName[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                // Go backwards to find the start of the StructProperty
                int pos = i;
                while (pos > 0 && _data[pos] != 0 && _data[pos - 1] != 0) pos--;
                while (pos > 0 && _data[pos - 1] != 0) pos--;
                return pos;
            }
        }
        return -1;
    }

    /// <summary>
    /// Parse struct property header and return the end offset of the struct.
    /// </summary>
    private int ParseStructPropertyBoundary(int structOffset)
    {
        int pos = structOffset;

        // Skip property name
        while (pos < _data.Length && _data[pos] != 0) pos++;
        pos++; // Skip null

        // Read type length
        if (pos + 4 > _data.Length) return structOffset + 1000;
        int typeLen = BitConverter.ToInt32(_data, pos);
        pos += 4 + typeLen;

        // Skip unknown (4 bytes)
        pos += 4;

        // Read struct type string length
        if (pos + 4 > _data.Length) return structOffset + 1000;
        int structTypeLen = BitConverter.ToInt32(_data, pos);
        pos += 4 + structTypeLen;

        // Read array index (4 bytes)
        if (pos + 4 > _data.Length) return structOffset + 1000;
        pos += 4;

        // Read struct size (4 bytes)
        if (pos + 4 > _data.Length) return structOffset + 1000;
        int structSize = BitConverter.ToInt32(_data, pos);
        pos += 4;

        // Struct content starts after header
        // Add a safety margin for nested structures
        int structEnd = pos + structSize + 500; // 500 byte safety margin
        if (structEnd > _data.Length) structEnd = _data.Length;

        return structEnd;
    }

    /// <summary>
    /// Search for a nested property by name directly in the file (global search).
    /// </summary>
    private object? FindNestedPropertyDirectly(string propertyName, GvasPropertyType propertyType)
    {
        return FindNestedPropertyDirectly(propertyName, propertyType, 0, _data.Length);
    }

    /// <summary>
    /// Search for a nested property by name directly in the file within a range.
    /// </summary>
    private object? FindNestedPropertyDirectly(string propertyName, GvasPropertyType propertyType, int startPos, int endPos)
    {
        var nameBytes = Encoding.UTF8.GetBytes(propertyName);
        var nameWithNull = new byte[nameBytes.Length + 1];
        Array.Copy(nameBytes, nameWithNull, nameBytes.Length);

        // Search for the property name within the specified range
        for (int i = startPos; i <= endPos - nameWithNull.Length; i++)
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

                // Calculate ACTUAL property name length by finding null terminator
                int actualNameLen = 0;
                int namePos = i;
                while (namePos < endPos && _data[namePos] != 0)
                {
                    actualNameLen++;
                    namePos++;
                }
                actualNameLen++; // Include null terminator

                // Parse property header to get value
                int pos = i + actualNameLen;

                // Read type length
                if (pos + 4 > endPos) return null;
                int typeLen = BitConverter.ToInt32(_data, pos);
                pos += 4;

                if (typeLen < 0 || typeLen > 100 || pos + typeLen > endPos) return null;
                pos += typeLen; // Skip type name

                // Skip unknown (4 bytes for nested properties)
                if (pos + 4 > endPos) return null;
                pos += 4;

                // Read size
                if (pos + 4 > endPos) return null;
                int size = BitConverter.ToInt32(_data, pos);
                pos += 4;

                // Skip array index (1 byte)
                if (pos + 1 > endPos) return null;
                pos += 1;

                // Read value
                return ReadValueAtPosition(pos, size, propertyType, endPos);
            }
        }
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
        return null;
    }

    /// <summary>
    /// Parse the Inventory ArrayProperty into a list of InventoryItem objects.
    /// The structure is: Items (StructProperty) -> ArmorPassports_3_... (ArrayProperty) -> Items (StructProperty with ObjectProperty)
    /// </summary>
    private List<InventoryItem>? ParseInventoryArray(string propertyName)
    {
        try
        {
            // For "Items" property, we need to find the "Player Inventory" StructProperty
            // There are multiple inventories in the save file:
            // - Player Inventory (at 0x00008842) - THIS IS WHAT WE WANT
            // - Merchant Inventory (at 0x00035A66) - trader stock
            // - Insured Items (at 0x000D8401) - insured item storage
            int inventoryOffset = FindPlayerInventoryOffset();
            if (inventoryOffset < 0)
            {
                return new List<InventoryItem>();
            }
            
            // Parse the Inventory StructProperty to find the ArmorPassports ArrayProperty
            return ParseInventoryFromStruct(inventoryOffset);
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Parse inventory from the Inventory StructProperty containing ArmorPassports ArrayProperty.
    /// </summary>
    private List<InventoryItem> ParseInventoryFromStruct(int inventoryOffset)
    {
        
        int pos = inventoryOffset;
        
        // Read property name
        int propNameStart = pos;
        while (pos < _data.Length && _data[pos] != 0) pos++;
        var propName = Encoding.UTF8.GetString(_data, propNameStart, pos - propNameStart);
        pos++; // Skip null
        
        // Read type length
        int typeLen = BitConverter.ToInt32(_data, pos);
        pos += 4;
        
        // Read type name (should be "StructProperty")
        var typeName = Encoding.UTF8.GetString(_data, pos, typeLen);
        pos += typeLen;
        
        // Skip unknown (4 bytes)
        pos += 4;
        
        // Read struct type string length (4 bytes)
        int structTypeLen = BitConverter.ToInt32(_data, pos);
        pos += 4;
        
        // Read struct type string
        var structType = Encoding.UTF8.GetString(_data, pos, structTypeLen);
        pos += structTypeLen;
        
        // Read array index (4 bytes)
        int arrayIndex = BitConverter.ToInt32(_data, pos);
        pos += 4;
        
        // Read struct size (4 bytes)
        int structSize = BitConverter.ToInt32(_data, pos);
        pos += 4;
        
        // Now we're at the struct content
        DumpHex(pos, Math.Min(100, _data.Length - pos));
        
        // The struct content has this structure:
        // 2 null bytes + inner size (4 bytes) + struct type string (null-terminated) + null bytes + GUID length (4 bytes) + GUID string + null bytes + ArrayProperty
        
        // Skip 2 null bytes
        pos += 2;
        
        // Read inner size
        int innerSize = BitConverter.ToInt32(_data, pos);
        pos += 4;
        
        // Read struct type string (null-terminated)
        int innerTypeStart = pos;
        while (pos < _data.Length && _data[pos] != 0) pos++;
        var innerType = Encoding.UTF8.GetString(_data, innerTypeStart, pos - innerTypeStart);
        pos++; // Skip null
        
        // Skip null bytes
        int nullCount = 0;
        while (pos < _data.Length && _data[pos] == 0) { pos++; nullCount++; }
        
        // Read GUID length
        int guidLen = BitConverter.ToInt32(_data, pos);
        pos += 4;
        
        // Read GUID string
        var guid = Encoding.UTF8.GetString(_data, pos, guidLen);
        pos += guidLen;
        
        // Skip null bytes after GUID
        nullCount = 0;
        while (pos < _data.Length && _data[pos] == 0) { pos++; nullCount++; }
        DumpHex(pos, 50);
        
        // Now search for ArrayProperty
        int arrayPropertyOffset = FindArrayProperty(pos, Math.Min(pos + 5000, _data.Length));
        
        if (arrayPropertyOffset < 0)
        {
            return new List<InventoryItem>();
        }
        
        // Parse the ArrayProperty
        return ParseArrayProperty(arrayPropertyOffset);
    }
    
    /// <summary>
    /// Dump hex bytes for debugging.
    /// </summary>
    private void DumpHex(int offset, int length)
    {
        int end = Math.Min(offset + length, _data.Length);
        for (int i = offset; i < end; i += 16)
        {
            int chunkLen = Math.Min(16, end - i);
            var hex = new StringBuilder();
            var ascii = new StringBuilder();
            
            for (int j = 0; j < chunkLen; j++)
            {
                hex.Append(_data[i + j].ToString("X2") + " ");
                ascii.Append(_data[i + j] >= 32 && _data[i + j] < 127 ? (char)_data[i + j] : '.');
            }
        }
    }
    
/// <summary>
    /// Find ArrayProperty by searching for "ArrayProperty" string.
    /// Uses pattern-matching to correctly identify the header start.
    /// </summary>
    private int FindArrayProperty(int startPos, int endPos)
    {
        Console.WriteLine($"[GameProgressParser] FindArrayProperty: Searching from {startPos} to {endPos}");
        int arrayPropPos = FindBytes(_data, Encoding.UTF8.GetBytes("ArrayProperty"), startPos, endPos);
        Console.WriteLine($"[GameProgressParser] FindArrayProperty: FindBytes returned {arrayPropPos}");
        
        if (arrayPropPos < 0)
        {
            Console.WriteLine("[GameProgressParser] FindArrayProperty: 'ArrayProperty' string not found");
            return -1;
        }
        
        // Validate GVAS header pattern:
        // [property_name\0][type_length=14][ArrayProperty\0]
        // The type length field (4 bytes) immediately precedes "ArrayProperty"
        // Type length should be 14 (0x0E 00 00 00 in little-endian)
        
        int typeLenPos = arrayPropPos - 4;
        if (typeLenPos < startPos)
        {
            Console.WriteLine("[GameProgressParser] FindArrayProperty: typeLenPos before startPos");
            return -1;
        }
        
        int typeLen = BitConverter.ToInt32(_data, typeLenPos);
        Console.WriteLine($"[GameProgressParser] FindArrayProperty: Type length at {typeLenPos} = {typeLen}");
        
        if (typeLen != 14)
        {
            Console.WriteLine($"[GameProgressParser] FindArrayProperty: Invalid type length {typeLen}, searching for next occurrence");
            // Continue searching for next "ArrayProperty" with valid type length
            int nextPos = FindBytes(_data, Encoding.UTF8.GetBytes("ArrayProperty"), arrayPropPos + 1, endPos);
            while (nextPos > 0)
            {
                int nextTypeLenPos = nextPos - 4;
                if (nextTypeLenPos >= startPos)
                {
                    int nextTypeLen = BitConverter.ToInt32(_data, nextTypeLenPos);
                    Console.WriteLine($"[GameProgressParser] FindArrayProperty: Next type length = {nextTypeLen}");
                    if (nextTypeLen == 14)
                    {
                        arrayPropPos = nextPos;
                        typeLenPos = nextTypeLenPos;
                        typeLen = nextTypeLen;
                        break;
                    }
                }
                nextPos = FindBytes(_data, Encoding.UTF8.GetBytes("ArrayProperty"), nextPos + 1, endPos);
            }
            
            if (typeLen != 14)
            {
                Console.WriteLine("[GameProgressParser] FindArrayProperty: No valid ArrayProperty header found");
                return -1;
            }
        }
        
        // Now find the property name start by searching backwards past the type length field
        int nameEnd = typeLenPos; // Property name ends where type length begins
        int nullPos = nameEnd - 1; // Null terminator should be right before type length
        
        // Verify we have a null terminator
        if (nullPos < startPos || _data[nullPos] != 0)
        {
            Console.WriteLine($"[GameProgressParser] FindArrayProperty: Expected null at {nullPos}, found {_data[nullPos]:X2}");
            return -1;
        }
        
        Console.WriteLine($"[GameProgressParser] FindArrayProperty: Found null terminator at {nullPos}");
        
        // Search backwards from nullPos to find where property name starts
        // Property name is a null-terminated string, so we search for the previous null
        int nameStart = nullPos - 1;
        while (nameStart >= startPos && _data[nameStart] != 0) nameStart--;
        nameStart++; // Move past the previous null to the first character of property name
        
        // Validate name length
        int nameLength = nullPos - nameStart;
        if (nameLength <= 0 || nameLength > 500)
        {
            Console.WriteLine($"[GameProgressParser] FindArrayProperty: Invalid name length {nameLength}");
            return -1;
        }
        
        Console.WriteLine($"[GameProgressParser] FindArrayProperty: Property name at {nameStart}, length {nameLength}");
        
        var arrayName = Encoding.UTF8.GetString(_data, nameStart, nameLength);
        Console.WriteLine($"[GameProgressParser] FindArrayProperty: Found ArrayProperty '{arrayName}' at offset {nameStart}");
        
        return nameStart; // Return start of property name
    }
    
    /// <summary>
    /// Parse the ArmorPassports ArrayProperty and extract inventory items.
    /// Uses simplified approach: search for blueprint paths directly in array range.
    /// </summary>
    private List<InventoryItem> ParseArrayProperty(int arrayOffset)
    {
        int pos = arrayOffset;
        
        // Read property name
        int nameStart = pos;
        while (pos < _data.Length && _data[pos] != 0) pos++;
        var propName = Encoding.UTF8.GetString(_data, nameStart, pos - nameStart);
        pos++; // Skip null
        
        // Read type length
        int typeLen = BitConverter.ToInt32(_data, pos);
        pos += 4;
        
        // Read type name (should be "ArrayProperty")
        var typeName = Encoding.UTF8.GetString(_data, pos, typeLen);
        pos += typeLen;
        
        // Skip unknown (4 bytes)
        int unknown = BitConverter.ToInt32(_data, pos);
        pos += 4;
        
        // Read array size
        int arraySize = BitConverter.ToInt32(_data, pos);
        pos += 4;
        
        // Read array index
        int arrayIndex = BitConverter.ToInt32(_data, pos);
        pos += 4;
        
        // DUMP STRUCTURAL INFORMATION - Find all property boundaries
        DumpStructuralBoundaries(pos, Math.Min(pos + 50 * 1024, _data.Length));
        
        // Search for specific Baron items to verify inventory source
        SearchForItem("Baron Cuisses", pos, Math.Min(pos + 350 * 1024, _data.Length));
        SearchForItem("Baron Cuirass", pos, Math.Min(pos + 350 * 1024, _data.Length));
        SearchForItem("Tabard", pos, Math.Min(pos + 350 * 1024, _data.Length));
        
        // ALSO search entire file to see if items exist anywhere
        SearchForItem("Baron Cuisses", 0, _data.Length);
        SearchForItem("Baron Cuirass", 0, _data.Length);
        SearchForItem("Tabard", 0, _data.Length);
        
        // Search for other unique item names to map the save structure
        SearchForItem("BP_Armor", 0, _data.Length);
        SearchForItem("Modular_Core", 0, _data.Length);
        
        // Find ALL ArrayProperty instances to locate different inventories
        FindAllArrayProperties();
        
        // Dynamic range approach: search up to 350KB for blueprint paths
        // This covers ~109 items in current save with room for growth
        // Maximum 350KB limit documented in AGENTS.md
        int arrayEnd = Math.Min(pos + 350 * 1024, _data.Length);
        
        var items = new List<InventoryItem>();
        int searchPos = pos;
        var blueprintBytes = Encoding.UTF8.GetBytes("/Game/Assets");
        int itemCount = 0;
        int lastBlueprintPos = -1;
        
        while (searchPos < arrayEnd - 12)
        {
            int blueprintPos = FindBytes(_data, blueprintBytes, searchPos, arrayEnd);
            if (blueprintPos < 0) break;
            
            // Check for structural markers only after a large gap (20KB) without finding blueprints
            // This avoids stopping at IntProperty/BoolProperty within inventory data structure
            if (lastBlueprintPos > 0 && blueprintPos - lastBlueprintPos > 20 * 1024)
            {
                int markerPos = FindStructuralMarker(blueprintPos, blueprintPos + 2048);
                if (markerPos >= 0)
                {
                    break;
                }
            }
            
            lastBlueprintPos = blueprintPos;
            
            // Extract the full blueprint path
            int pathEnd = blueprintPos;
            while (pathEnd < arrayEnd && _data[pathEnd] != 0 && _data[pathEnd] != 0x0A && _data[pathEnd] != 0x0D) pathEnd++;
            
            var blueprintPath = Encoding.UTF8.GetString(_data, blueprintPos, pathEnd - blueprintPos);
            
            // Create inventory item
            var item = new InventoryItem();
            item.ObjectPath = blueprintPath;
            
            // Extract item name from path
            int lastSlash = blueprintPath.LastIndexOf('/');
            int lastDot = blueprintPath.LastIndexOf('.');
            if (lastSlash >= 0 && lastDot > lastSlash)
            {
                item.ItemName = blueprintPath.Substring(lastSlash + 1, lastDot - lastSlash - 1);
            }
            else
            {
                item.ItemName = blueprintPath;
            }
            
            // Determine item type from path
            if (blueprintPath.Contains("/Weapons/"))
                item.ItemType = "Weapon";
            else if (blueprintPath.Contains("/Armor/"))
                item.ItemType = "Armor";
            else
                item.ItemType = "Item";
            
            items.Add(item);
            itemCount++;
            searchPos = pathEnd + 1;
        }
        
        // Check if we hit the maximum search range
        if (searchPos >= arrayEnd - 12)
        {
        }
        return items;
    }
    
    /// <summary>
    /// Dump structural boundaries to help identify inventory vs trader data.
    /// </summary>
    private void DumpStructuralBoundaries(int startPos, int endPos)
    {
        int pos = startPos;
        int propCount = 0;
        
        while (pos < endPos - 20 && propCount < 100)
        {
            // Skip null bytes
            if (_data[pos] == 0)
            {
                pos++;
                continue;
            }
            
            // Read property name
            int nameStart = pos;
            while (pos < endPos && _data[pos] != 0) pos++;
            if (pos >= endPos) break;
            
            var propName = Encoding.UTF8.GetString(_data, nameStart, pos - nameStart);
            pos++; // Skip null
            
            // Read type length
            if (pos + 4 > endPos) break;
            int typeLen = BitConverter.ToInt32(_data, pos);
            pos += 4;
            
            if (typeLen < 1 || typeLen > 100 || pos + typeLen > endPos)
            {
                pos++;
                continue;
            }
            
            var typeName = Encoding.UTF8.GetString(_data, pos, typeLen);
            pos += typeLen;
            
            // Skip unknown
            if (pos + 4 > endPos) break;
            pos += 4;
            
            // Read size
            if (pos + 4 > endPos) break;
            int size = BitConverter.ToInt32(_data, pos);
            pos += 4;
            
            // Skip array index
            if (pos + 1 > endPos) break;
            pos += 1;
            
            // Log significant properties
            if (typeName.Contains("Struct") || typeName.Contains("Array") || size > 1000)
            {
                propCount++;
            }
            
            // Skip value
            if (size > 0 && pos + size <= endPos)
            {
                pos += size;
            }
            else
            {
                pos++;
            }
        }
    }
    
    /// <summary>
    /// Find all ArrayProperty instances in the file to locate different inventories.
    /// </summary>
    private void FindAllArrayProperties()
    {
        var arrayBytes = Encoding.UTF8.GetBytes("ArrayProperty");
        for (int i = 0; i <= _data.Length - arrayBytes.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < arrayBytes.Length; j++)
            {
                if (_data[i + j] != arrayBytes[j])
                {
                    match = false;
                    break;
                }
            }
            
            if (match)
            {
                // Go backwards to find the property name
                int nameEnd = i;
                int nameStart = nameEnd - 1;
                while (nameStart >= 0 && _data[nameStart] != 0) nameStart--;
                nameStart++;
                
                if (nameStart >= 0 && nameEnd < _data.Length)
                {
                    var propName = Encoding.UTF8.GetString(_data, nameStart, nameEnd - nameStart);
                }
                
                // Skip to avoid duplicate matches
                i += arrayBytes.Length;
            }
        }
    }
    
    /// <summary>
    /// Search for a specific item name in the data range.
    /// </summary>
    private void SearchForItem(string itemName, int startPos, int endPos)
    {
        var searchBytes = Encoding.UTF8.GetBytes(itemName);
        for (int i = startPos; i <= endPos - searchBytes.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < searchBytes.Length; j++)
            {
                if (_data[i + j] != searchBytes[j])
                {
                    match = false;
                    break;
                }
            }
            
            if (match)
            {
                
                // Extract context - show surrounding text
                int contextStart = Math.Max(i - 50, startPos);
                int contextEnd = Math.Min(i + searchBytes.Length + 50, endPos);
                var context = Encoding.UTF8.GetString(_data, contextStart, contextEnd - contextStart);
                return;
            }
        }
    }
    
    /// <summary>
    /// Find boundaries of all StructProperty elements by searching for "StructProperty" strings.
    /// DEPRECATED: No longer used - simplified parsing searches for blueprint paths directly.
    /// </summary>
    private List<int> FindStructPropertyBoundaries(int startPos, int maxElements)
    {
        // This method is deprecated but kept for compatibility
        return new List<int>();
    }
    
    /// <summary>
    /// Find structural markers that indicate end of inventory data.
    /// Returns position of first marker found, or -1 if none found.
    /// </summary>
    private int FindStructuralMarker(int startPos, int endPos)
    {
        string[] markers = { "IntProperty", "BoolProperty", "StructProperty", "ArrayProperty" };
        foreach (var marker in markers)
        {
            int pos = FindBytes(_data, Encoding.UTF8.GetBytes(marker), startPos, endPos);
            if (pos >= 0)
            {
                return pos;
            }
        }
        return -1;
    }
    
    /// <summary>
    /// Find the next occurrence of "StructProperty" string.
    /// </summary>
    private int FindNextStructProperty(int startPos)
    {
        return FindBytes(_data, Encoding.UTF8.GetBytes("StructProperty"), startPos, _data.Length);
    }
    
    /// <summary>
    /// Find byte sequence in data.
    /// </summary>
    private int FindBytes(byte[] data, byte[] pattern, int start, int end)
    {
        for (int i = start; i <= end - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
    }
    
    /// <summary>
    /// Parse a single inventory element (StructProperty containing ArmorCore ObjectProperty).
    /// DEPRECATED: No longer used - simplified parsing searches for blueprint paths directly.
    /// </summary>
    private InventoryItem? ParseInventoryElement(int elemStart, int elemEnd)
    {
        // This method is deprecated but kept for compatibility
        return null;
    }
    
    /// <summary>
    /// Parse a StructProperty header and find the nested ArrayProperty offset.
    /// </summary>
    private int ParseStructPropertyHeader(int offset, out int arrayPropertyOffset)
    {
        arrayPropertyOffset = -1;
        int pos = offset;

        // Read property name
        int nameStart = pos;
        while (pos < _data.Length && _data[pos] != 0) pos++;
        int nameLen = pos - nameStart;
        pos++; // Skip null

        // Read type length (4 bytes)
        if (pos + 4 > _data.Length) return pos;
        int typeLen = BitConverter.ToInt32(_data, pos);
        pos += 4;

        // Read type name (should be "StructProperty\0")
        if (pos + typeLen > _data.Length) return pos;
        var typeName = Encoding.UTF8.GetString(_data, pos, typeLen);
        pos += typeLen;

        // Skip 4 unknown bytes
        if (pos + 4 > _data.Length) return pos;
        pos += 4;

        // Read struct type string length (4 bytes)
        if (pos + 4 > _data.Length) return pos;
        int structTypeLen = BitConverter.ToInt32(_data, pos);
        pos += 4;
        
        // Sanity check
        if (structTypeLen < 0 || structTypeLen > 200)
        {
            return pos;
        }
        
        // Read struct type string
        if (pos + structTypeLen > _data.Length) return pos;
        var structType = Encoding.UTF8.GetString(_data, pos, structTypeLen);
        pos += structTypeLen;

        // Read array index (4 bytes)
        if (pos + 4 > _data.Length) return pos;
        int arrayIndex = BitConverter.ToInt32(_data, pos);
        pos += 4;

        // Read struct size (4 bytes) - but this might not be the full size
        if (pos + 4 > _data.Length) return pos;
        int structSize = BitConverter.ToInt32(_data, pos);
        pos += 4;
        
        // The struct content starts with: 2 null bytes + size + struct type string + null bytes + GUID length + GUID
        // We need to skip past this to get to the actual nested properties
        int contentStart = pos;
        
        // Skip 2 null bytes
        if (pos + 2 > _data.Length) return pos;
        pos += 2;
        
        // Read size (should match structSize)
        if (pos + 4 > _data.Length) return pos;
        int innerSize = BitConverter.ToInt32(_data, pos);
        pos += 4;
        
        // Read struct type string (null-terminated)
        int structTypeStart = pos;
        while (pos < _data.Length && _data[pos] != 0) pos++;
        var innerStructType = Encoding.UTF8.GetString(_data, structTypeStart, pos - structTypeStart);
        pos++; // Skip null
        
        // Skip some null bytes (variable amount)
        while (pos < _data.Length && _data[pos] == 0) pos++;
        
        // Read GUID length
        if (pos + 4 > _data.Length) return pos;
        int guidLen = BitConverter.ToInt32(_data, pos);
        pos += 4;
        
        // Skip GUID string
        if (pos + guidLen > _data.Length) return pos;
        pos += guidLen;
        
        // Skip more null bytes
        while (pos < _data.Length && _data[pos] == 0) pos++;
        
        // Dump hex of first 100 bytes after GUID to see what we're looking at
        int dumpEnd = Math.Min(pos + 100, _data.Length);
        StringBuilder hexDump = new StringBuilder();
        for (int i = pos; i < dumpEnd; i++)
        {
            hexDump.Append(_data[i].ToString("X2") + " ");
            if ((i - pos + 1) % 16 == 0)
            {
                hexDump.Clear();
            }
        }
        if (hexDump.Length > 0)
        {
        }
        
        // Now search for ArrayProperty - search up to 200KB to be safe
        int endPos = Math.Min(pos + 200000, _data.Length);
        arrayPropertyOffset = FindNestedArrayProperty(pos, endPos);

        return contentStart + Math.Max(structSize, 1000); // Return reasonable end position
    }

    /// <summary>
    /// Find the nested ArrayProperty within a StructProperty's content.
    /// </summary>
    private int FindNestedArrayProperty(int startPos, int endPos)
    {
        int pos = startPos;
        int searchCount = 0;
        
        while (pos < endPos - 50 && searchCount < 500)
        {
            searchCount++;
            
            // Read nested property name
            int nameStart = pos;
            while (pos < endPos && _data[pos] != 0) pos++;
            if (pos >= endPos) break;
            
            var propName = Encoding.UTF8.GetString(_data, nameStart, pos - nameStart);
            pos++; // Skip null

            // Read type length
            if (pos + 4 > endPos) break;
            int typeLen = BitConverter.ToInt32(_data, pos);
            pos += 4;

            if (typeLen < 1 || typeLen > 50 || pos + typeLen > endPos)
            {
                break;
            }
            
            var typeName = Encoding.UTF8.GetString(_data, pos, typeLen);
            pos += typeLen;
            
            // Log ALL properties for first 30 searches, then only ArrayProperty and StructProperty
            if (searchCount <= 30 || typeName.Contains("Array") || typeName.Contains("Struct"))
            {
            }

            // Check if this is an ArrayProperty
            if (typeName == "ArrayProperty")
            {
                return nameStart;
            }

            // Skip unknown (2 bytes for StructProperty nested props)
            if (pos + 2 > endPos) break;
            pos += 2;

            // Read struct/array size
            if (pos + 4 > endPos) break;
            int size = BitConverter.ToInt32(_data, pos);
            pos += 4;

            // Sanity check on size
            if (size < 0 || size > 1000000)
            {
                break;
            }

            // Skip array index (1 byte)
            if (pos + 1 > endPos) break;
            pos += 1;

            // Skip the value/content
            if (size > 0 && pos + size <= endPos)
            {
                pos += size;
            }
            else
            {
                break;
            }
        }
        return -1;
    }

    /// <summary>
    /// Find the Player Inventory StructProperty offset (not Merchant or Insured).
    /// </summary>
    private int FindPlayerInventoryOffset()
    {
        var inventoryBytes = Encoding.UTF8.GetBytes("Player Inventory");
        for (int i = 0; i <= _data.Length - inventoryBytes.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < inventoryBytes.Length; j++)
            {
                if (_data[i + j] != inventoryBytes[j])
                {
                    match = false;
                    break;
                }
            }
            
            if (match)
            {
                // Go backwards to find the start of the StructProperty
                int pos = i;
                while (pos > 0 && _data[pos] != 0 && _data[pos - 1] != 0) pos--;
                
                // Find the property name start
                while (pos > 0 && _data[pos - 1] != 0) pos--;
                return pos;
            }
        }
        return -1;
    }
    
    /// <summary>
    /// Parse inventory array.
    /// </summary>
    private InventoryItem? ParseInventoryItemStruct(int pos)
    {
        try
        {
            var item = new InventoryItem();
            int startPos = pos;

            // Read property name
            int nameStart = pos;
            while (pos < _data.Length && _data[pos] != 0) pos++;
            int nameLen = pos - nameStart;
            item.ItemName = Encoding.UTF8.GetString(_data, nameStart, nameLen);
            item.Properties["_name_length"] = nameLen;
            pos++; // Skip null

            // Read type length
            if (pos + 4 > _data.Length) return null;
            int typeLen = BitConverter.ToInt32(_data, pos);
            pos += 4;

            // Read type name
            if (pos + typeLen > _data.Length) return null;
            item.ItemType = Encoding.UTF8.GetString(_data, pos, typeLen);
            pos += typeLen;

            // Skip unknown (2 bytes)
            if (pos + 2 > _data.Length) return null;
            pos += 2;

            // Read struct type string
            if (pos + 4 > _data.Length) return null;
            int structTypeLen = BitConverter.ToInt32(_data, pos);
            pos += 4;
            if (pos + structTypeLen > _data.Length) return null;
            pos += structTypeLen;

            // Read Instance GUID
            if (pos + 4 > _data.Length) return null;
            int guidLen = BitConverter.ToInt32(_data, pos);
            pos += 4;
            if (pos + guidLen > _data.Length) return null;
            pos += guidLen;

            // Read struct size
            if (pos + 4 > _data.Length) return null;
            int structSize = BitConverter.ToInt32(_data, pos);
            item.Properties["_struct_size"] = structSize;
            pos += 4;

            // Read array index (1 byte)
            if (pos + 1 > _data.Length) return null;
            pos += 1;

            // Note: Nested property parsing not yet implemented for this path
            // The new ParseInventoryElement method handles this differently
            return item;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parse a StructProperty at the given position into an InventoryItem.
    /// </summary>
    private InventoryItem? ParseStructPropertyAt(int pos)
    {
        try
        {
            var item = new InventoryItem();

            // Read nested property name
            int nameStart = pos;
            while (pos < _data.Length && _data[pos] != 0) pos++;
            if (pos >= _data.Length) return null;

            var propNameBytes = new byte[pos - nameStart];
            Array.Copy(_data, nameStart, propNameBytes, 0, propNameBytes.Length);
            item.ItemName = Encoding.UTF8.GetString(propNameBytes);
            pos++; // Skip null

            // Read type length
            if (pos + 4 > _data.Length) return null;
            int typeLen = BitConverter.ToInt32(_data, pos);
            pos += 4;

            // Read type name (StructProperty type)
            if (pos + typeLen > _data.Length) return null;
            var typeNameBytes = new byte[typeLen];
            Array.Copy(_data, pos, typeNameBytes, 0, typeLen);
            item.ItemType = Encoding.UTF8.GetString(typeNameBytes);
            pos += typeLen;

            // Skip unknown (4 bytes)
            pos += 4;

            // Read struct size
            if (pos + 4 > _data.Length) return null;
            int structSize = BitConverter.ToInt32(_data, pos);
            pos += 4;
            item.Properties["_struct_size"] = structSize;

            // Skip array index (1 byte)
            pos += 1;

            // Store object path if present
            item.Properties["_offset"] = nameStart;

            return item;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Global property search (for non-nested properties).
    /// </summary>
    private object? FindPropertyByNameGlobal(string propertyName, GvasPropertyType propertyType)
    {
        int offset = FindPropertyOffset(propertyName);
        if (offset < 0)
        {
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
        catch
        {
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

            case GvasPropertyType.ArrayProperty:
                // Return raw byte data for array property
                if (size > 0 && pos + size <= maxPos)
                {
                    var arrayData = new byte[size];
                    Array.Copy(_data, pos, arrayData, 0, size);
                    return arrayData;
                }
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
                return true;
            }
        }
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
        return false;
    }

    /// <summary>
    /// Get inventory items as a list of InventoryItem objects.
    /// </summary>
    public List<InventoryItem>? GetInventory()
    {
        var offset = FindPropertyOffset("Items");
        if (offset < 0)
        {
            return null;
        }

        return ParseInventoryArray("Items");
    }

    /// <summary>
    /// Get player inventory items.
    /// </summary>
    public List<InventoryItem>? GetPlayerInventory()
    {
        var offset = FindPlayerInventoryOffset();
        if (offset < 0)
        {
            return null;
        }
        return ParseInventoryFromStruct(offset);
    }

    /// <summary>
    /// Get insured items inventory.
    /// </summary>
    public List<InventoryItem>? GetInsuredItems()
    {
        var offset = FindInsuredItemsOffset();
        if (offset < 0)
        {
            return null;
        }
        return ParseInventoryFromStruct(offset);
    }

/// <summary>
    /// Find the Insured Items StructProperty offset.
    /// </summary>
    private int FindInsuredItemsOffset()
    {
        Console.WriteLine($"[GameProgressParser] FindInsuredItemsOffset: Searching in {_data.Length} byte file");
        var insuredBytes = Encoding.UTF8.GetBytes("Insured Items");
        for (int i = 0; i <= _data.Length - insuredBytes.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < insuredBytes.Length; j++)
            {
                if (_data[i + j] != insuredBytes[j])
                {
                    match = false;
                    break;
                }
            }
            
            if (match)
            {
                Console.WriteLine($"[GameProgressParser] FindInsuredItemsOffset: Found 'Insured Items' at byte offset {i}");
                // Go backwards to find the start of the StructProperty
                int pos = i;
                while (pos > 0 && _data[pos] != 0 && _data[pos - 1] != 0) pos--;
                while (pos > 0 && _data[pos - 1] != 0) pos--;
                Console.WriteLine($"[GameProgressParser] FindInsuredItemsOffset: StructProperty starts at offset {pos}");
                return pos;
            }
        }
        Console.WriteLine("[GameProgressParser] FindInsuredItemsOffset: 'Insured Items' not found in file");
        return -1;
    }

    /// <summary>
    /// Finds the ArmorPassports ArrayProperty within the Insured Items struct.
    /// </summary>
    /// <param name="insuredItemsOffset">Offset of the Insured Items struct</param>
    /// <returns>Offset of the ArrayProperty, or -1 if not found</returns>
    private int FindInsuredItemsArrayProperty(int insuredItemsOffset)
    {
        try
        {
            Console.WriteLine($"[GameProgressParser] FindInsuredItemsArrayProperty: Starting from offset {insuredItemsOffset}");
            int pos = insuredItemsOffset;
            
            // Skip Insured Items struct header
            // Read property name
            while (pos < _data.Length && _data[pos] != 0) pos++;
            pos++; // Skip null
            
            // Read type length
            if (pos + 4 > _data.Length) 
            {
                Console.WriteLine("[GameProgressParser] FindInsuredItemsArrayProperty: ERROR - pos + 4 exceeds file length");
                return -1;
            }
            int typeLen = BitConverter.ToInt32(_data, pos);
            Console.WriteLine($"[GameProgressParser] FindInsuredItemsArrayProperty: Type length = {typeLen}");
            pos += 4;
            
            // Skip type name
            pos += typeLen;
            
            // Skip unknown (4 bytes)
            pos += 4;
            
            // Skip struct type string length and content
            if (pos + 4 > _data.Length) 
            {
                Console.WriteLine("[GameProgressParser] FindInsuredItemsArrayProperty: ERROR - pos + 4 exceeds file length at struct type");
                return -1;
            }
            int structTypeLen = BitConverter.ToInt32(_data, pos);
            Console.WriteLine($"[GameProgressParser] FindInsuredItemsArrayProperty: Struct type length = {structTypeLen}");
            pos += 4 + structTypeLen;
            
            // Skip array index (4 bytes)
            pos += 4;
            
            // Skip struct size (4 bytes)
            pos += 4;
            
            // Now we're at the struct content
            // Skip 2 null bytes
            pos += 2;
            
            // Skip inner size (4 bytes)
            pos += 4;
            
            // Skip struct type string
            while (pos < _data.Length && _data[pos] != 0) pos++;
            pos++; // Skip null
            
            // Skip null bytes
            while (pos < _data.Length && _data[pos] == 0) pos++;
            
            // Skip GUID length and content
            if (pos + 4 > _data.Length) 
            {
                Console.WriteLine("[GameProgressParser] FindInsuredItemsArrayProperty: ERROR - pos + 4 exceeds file length at GUID");
                return -1;
            }
            int guidLen = BitConverter.ToInt32(_data, pos);
            Console.WriteLine($"[GameProgressParser] FindInsuredItemsArrayProperty: GUID length = {guidLen}");
            pos += 4 + guidLen;
            
            // Skip more null bytes
            while (pos < _data.Length && _data[pos] == 0) pos++;
            
            Console.WriteLine($"[GameProgressParser] FindInsuredItemsArrayProperty: Searching for ArrayProperty from offset {pos}");
            // Now search for ArrayProperty within reasonable range
            int endPos = Math.Min(pos + 200000, _data.Length); // 200KB should be enough
            int result = FindArrayProperty(pos, endPos);
            Console.WriteLine($"[GameProgressParser] FindInsuredItemsArrayProperty: FindArrayProperty returned {result}");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameProgressParser] FindInsuredItemsArrayProperty: EXCEPTION - {ex.Message}");
            return -1;
        }
    }

    /// <summary>
    /// Gets the current size of an ArrayProperty.
    /// </summary>
    /// <param name="arrayOffset">Offset of the ArrayProperty</param>
    /// <returns>Current array size, or -1 if error</returns>
    private int GetArrayPropertySize(int arrayOffset)
    {
        try
        {
            Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: Reading size from offset {arrayOffset}");
            int pos = arrayOffset;
            int arraySizeOffset = 0; // Track offset where arraySize was found
            
            // Skip property name
            while (pos < _data.Length && _data[pos] != 0) pos++;
            pos++; // Skip null
            Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: After property name, pos = {pos}");
            
            // Read type length (4 bytes)
            if (pos + 4 > _data.Length) 
            {
                Console.WriteLine("[GameProgressParser] GetArrayPropertySize: ERROR - pos + 4 exceeds file length at typeLen");
                return -1;
            }
            int typeLen = BitConverter.ToInt32(_data, pos);
            Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: Type length = {typeLen}");
            pos += 4;
            
            // Validate type length is reasonable
            if (typeLen < 1 || typeLen > 100)
            {
                Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: ERROR - Invalid type length {typeLen}");
                return -1;
            }
            
            // Skip type name "ArrayProperty\0"
            pos += typeLen;
            Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: After type name, pos = {pos}");
            
// Skip unknown (4 bytes)
            pos += 4;
            Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: After unknown, pos = {pos}");

            // === CRITICAL DEBUG: Show hex dump of next 20 bytes to see full structure ===
            Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: === HEX DUMP from pos {pos} ===");
            for (int i = 0; i < 20 && pos + i < _data.Length; i++)
            {
                string byteHex = $"{_data[pos + i]:X2}";
                string ascii = (_data[pos + i] >= 32 && _data[pos + i] <= 126) ? $"{_data[pos + i]}" : ".";
                Console.WriteLine($"[GameProgressParser] GetArrayPropertySize:   [{pos + i}] = 0x{byteHex} ({ascii})");
            }
            Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: === END HEX DUMP ===");

            // READ ELEMENT TYPE LENGTH (4 bytes) - This is elemTypeLen, NOT arrayIndex!
            // Correct ArrayProperty header structure:
            // [unknown=4][elemTypeLen=4][elem_type_name\0][unknown=1][arrayIndex=4][arraySize=4]
            if (pos + 4 > _data.Length)
            {
                Console.WriteLine("[GameProgressParser] GetArrayPropertySize: ERROR - pos + 4 exceeds file length at elemTypeLen");
                return -1;
            }

            // Add hex dump for debugging
            byte[] elemTypeLenBytes = BitConverter.GetBytes(BitConverter.ToInt32(_data, pos));
            Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: Reading elemTypeLen at pos {pos}, hex: {BitConverter.ToString(elemTypeLenBytes)}");

            int elemTypeLen = BitConverter.ToInt32(_data, pos);
            Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: Element type length = {elemTypeLen}");
            pos += 4;
            
        // Validate element type length is reasonable (should be 15 for "StructProperty\0")
        if (elemTypeLen < 1 || elemTypeLen > 100)
        {
            Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: ERROR - Invalid element type length {elemTypeLen}");
            return -1;
        }

        // Validate element type length is exactly 15 (StructProperty\0)
        if (elemTypeLen != 15)
        {
            Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: WARNING - Expected element type length 15, got {elemTypeLen}");
        }

        // Show hex dump of element type name bytes before skipping
        if (pos + elemTypeLen <= _data.Length)
        {
            byte[] elemTypeNameBytes = new byte[elemTypeLen];
            Array.Copy(_data, pos, elemTypeNameBytes, 0, elemTypeLen);
            string elemTypeName = Encoding.UTF8.GetString(elemTypeNameBytes);
            Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: Element type name at pos {pos} = '{elemTypeName}' (length {elemTypeLen})");
            Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: Element type name HEX: {BitConverter.ToString(elemTypeNameBytes)}");
        }

        // Skip element type name "StructProperty\0"
        pos += elemTypeLen;
        Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: After element type name, pos = {pos}");
        
        // DUMP NEXT 20 BYTES to see what's actually there
        Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: === HEX DUMP of next 20 bytes from pos {pos} ===");
        for (int i = 0; i < 20 && pos + i < _data.Length; i++)
        {
            byte b = _data[pos + i];
            string ascii = (b >= 32 && b <= 126) ? $"{(char)b}" : ".";
            Console.WriteLine($"[GameProgressParser] GetArrayPropertySize:   [{pos + i}] = 0x{b:X2} ({b,3}) '{ascii}'");
        }
        Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: === END HEX DUMP ===");
            
// Skip unknown byte (1 byte) - some ArrayProperties have this
if (pos >= _data.Length)
{
    Console.WriteLine("[GameProgressParser] GetArrayPropertySize: ERROR - pos exceeds file length at unknown byte");
    return -1;
}
byte unknownByte = _data[pos];
Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: Unknown byte at pos {pos} = 0x{unknownByte:X2}");
pos += 1;
Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: After unknown byte, pos = {pos}");

// === CRITICAL FIX: Search for arraySize pattern 00-00-00-C3 (big-endian 195) ===
// Based on hex dump analysis, the arraySize field is NOT at a fixed offset.
// We need to search for the pattern 00-00-00-C3 which represents 195 in big-endian.

// First, skip structSize field (4 bytes, big-endian)
if (pos + 4 > _data.Length)
{
Console.WriteLine("[GameProgressParser] GetArrayPropertySize: ERROR - pos + 4 exceeds file length at structSize");
return -1;
}

Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: Reading structSize at pos {pos}, RAW bytes: {_data[pos]:X2}-{_data[pos+1]:X2}-{_data[pos+2]:X2}-{_data[pos+3]:X2}");
int structSize = (_data[pos] << 24) | (_data[pos+1] << 16) | (_data[pos+2] << 8) | _data[pos+3];
Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: Struct size = {structSize} (BIG-ENDIAN)");
pos += 4;
Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: After struct size, pos = {pos}");

// After structSize, we need to skip:
// 1. Unknown bytes (3 bytes) - position 877369-877371
// 2. Element type name "StructProperty\0" (15 bytes) - position 877372-877386
// 3. GUID string (37 bytes) - position 877387-877423
// 4. arrayIndex (4 bytes) - position 877424-877427
// 5. arraySize (4 bytes) - position 877428-877431 ← THIS IS WHERE WE SHOULD LOOK!

Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: === SEARCHING FOR arraySize AFTER GUID ===");

// Skip unknown bytes (3 bytes)
pos += 3;
Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: After skipping 3 unknown bytes, pos = {pos}");

// Skip element type name "StructProperty\0" (15 bytes)
pos += 15;
Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: After skipping element type name, pos = {pos}");

// Skip GUID string (37 bytes)
pos += 37;
Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: After skipping GUID, pos = {pos}");

// Skip arrayIndex (4 bytes)
pos += 4;
Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: After skipping arrayIndex, pos = {pos}");

// NOW read arraySize (4 bytes, big-endian)
if (pos + 4 > _data.Length)
{
    Console.WriteLine("[GameProgressParser] GetArrayPropertySize: ERROR - pos + 4 exceeds file length at arraySize");
    return -1;
}

byte[] arraySizeBytes = new byte[4];
Array.Copy(_data, pos, arraySizeBytes, 0, 4);
int arraySize = (arraySizeBytes[0] << 24) | (arraySizeBytes[1] << 16) | (arraySizeBytes[2] << 8) | arraySizeBytes[3];

Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: Reading arraySize at pos {pos}:");
Console.WriteLine($"[GameProgressParser] GetArrayPropertySize:   Raw bytes: {arraySizeBytes[0]:X2}-{arraySizeBytes[1]:X2}-{arraySizeBytes[2]:X2}-{arraySizeBytes[3]:X2}");
Console.WriteLine($"[GameProgressParser] GetArrayPropertySize:   Big-endian value: {arraySize}");
Console.WriteLine($"[GameProgressParser] GetArrayPropertySize:   Expected: ~195");

if (arraySize >= 150 && arraySize <= 250)
{
    Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: ✓ Valid arraySize={arraySize} found at calculated position!");
}
else
{
    Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: ⚠ arraySize={arraySize} is outside expected range, searching for 0xC3 pattern...");
    
    // Fallback: search for 0xC3 in next 100 bytes
    for (int i = 0; i < 100 && pos + i + 3 < _data.Length; i++)
    {
        if (_data[pos + i] == 0x00 && _data[pos + i + 1] == 0x00 && 
            _data[pos + i + 2] == 0x00 && _data[pos + i + 3] == 0xC3)
        {
            Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: ✓ FOUND 0xC3 at pos {pos + i}");
            arraySize = 195;
            pos = pos + i + 4;
            break;
        }
    }
}

pos += 4;
{
    Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: Exact pattern not found, searching for valid array size...");
    for (int i = 0; i < 500 && pos + i + 3 < _data.Length; i++)
    {
        int candidate = (_data[pos + i] << 24) | (_data[pos + i + 1] << 16) | (_data[pos + i + 2] << 8) | _data[pos + i + 3];
        if (candidate >= 100 && candidate <= 500)
        {
            // Check if this looks like a valid array size (preceded by zeros or at struct boundary)
            Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: Found candidate at offset +{i}: bytes={_data[pos+i]:X2}-{_data[pos+i+1]:X2}-{_data[pos+i+2]:X2}-{_data[pos+i+3]:X2}, value={candidate}");
            arraySizeOffset = i;
            arraySize = candidate;
            break;
        }
    }
}

if (arraySize == -1)
{
    Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: No valid array size found in search range, trying fallback - read at current pos");
    // Fallback: try reading at current position
    if (pos + 4 > _data.Length)
    {
        Console.WriteLine("[GameProgressParser] GetArrayPropertySize: ERROR - pos + 4 exceeds file length at arraySize");
        return -1;
    }

    Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: Reading arraySize at pos {pos}, RAW bytes: {_data[pos]:X2}-{_data[pos+1]:X2}-{_data[pos+2]:X2}-{_data[pos+3]:X2}");
    arraySize = (_data[pos] << 24) | (_data[pos+1] << 16) | (_data[pos+2] << 8) | _data[pos+3];
    Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: Array size = {arraySize}");
    pos += 4;
}
else
{
    Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: ✓ SUCCESS - Found arraySize={arraySize} at offset +{arraySizeOffset}");
    // Advance pos to after the found arraySize
    pos = pos + arraySizeOffset + 4;
}

// Validate array size is reasonable (< 10000 items)
if (arraySize < 0 || arraySize > 10000)
{
    Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: ERROR - Invalid array size {arraySize}");
    return -1;
}

Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: SUCCESS - Returning arraySize={arraySize}");
return arraySize;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameProgressParser] GetArrayPropertySize: EXCEPTION - {ex.Message}");
            return -1;
        }
    }

    /// <summary>
    /// Serializes an InventoryItem into GVAS format.
    /// </summary>
    /// <param name="item">Item to serialize</param>
    /// <returns>Serialized byte array, or null if error</returns>
    private byte[] SerializeInventoryItem(InventoryItem item)
    {
        try
        {
            Console.WriteLine($"[GameProgressParser] SerializeInventoryItem: Serializing '{item.ItemName}' with ObjectPath '{item.ObjectPath?.Substring(0, Math.Min(50, item.ObjectPath.Length))}...'");
            // Generate unique property name with GUID
            string propertyName = $"ArmorPassports_3_{GenerateGuidSuffix()}_0";
            
            // Create memory stream for serialization
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // Write property name (null-terminated)
                writer.Write(Encoding.UTF8.GetBytes(propertyName));
                writer.Write((byte)0); // null terminator
                
                // Write type length (4 bytes) - "StructProperty\0" = 15 bytes
                writer.Write(15);
                
                // Write type name (null-terminated)
                writer.Write(Encoding.UTF8.GetBytes("StructProperty"));
                writer.Write((byte)0); // null terminator
                
                // Write unknown (4 bytes)
                writer.Write(0);
                
                // Write struct type string length (4 bytes) - "/Game/Blueprints/Structure/Passports/Str_Passport_Armor1.Str_Passport_Armor1\0" = 85 bytes
                writer.Write(85);
                
                // Write struct type string (null-terminated)
                writer.Write(Encoding.UTF8.GetBytes("/Game/Blueprints/Structure/Passports/Str_Passport_Armor1.Str_Passport_Armor1"));
                writer.Write((byte)0); // null terminator
                
                // Write array index (4 bytes)
                writer.Write(0);
                
                // Calculate and write struct size (4 bytes) - this will be updated later
                int structSizePos = (int)ms.Position;
                writer.Write(0); // Placeholder for struct size
                
                // Write struct content
                // 2 null bytes
                writer.Write((byte)0);
                writer.Write((byte)0);
                
                // Write inner size (4 bytes) - placeholder
                int innerSizePos = (int)ms.Position;
                writer.Write(0); // Placeholder
                
                // Write struct type string (null-terminated)
                writer.Write(Encoding.UTF8.GetBytes("/Game/Blueprints/Structure/Passports/Str_Passport_Armor1.Str_Passport_Armor1"));
                writer.Write((byte)0); // null terminator
                
                // Write null bytes padding
                for (int i = 0; i < 4; i++) writer.Write((byte)0);
                
                // Write GUID length (4 bytes)
                string guid = GenerateNewGuid();
                writer.Write(guid.Length + 1); // +1 for null terminator
                
                // Write GUID (null-terminated)
                writer.Write(Encoding.UTF8.GetBytes(guid));
                writer.Write((byte)0); // null terminator
                
                // Write more null bytes padding
                for (int i = 0; i < 4; i++) writer.Write((byte)0);
                
                // Serialize ObjectProperty for ArmorCore
                SerializeObjectProperty(writer, "ArmorCore_3_F6B7C69C4BD7D9720DB91EB635EE2B43_0", item.ObjectPath);
                
                // Serialize other required properties with default values
                SerializeIntProperty(writer, "ID_54_C6BBB1A64A3828B5AB1D8E804EC7C8F7_0", GenerateItemId());
                SerializeBoolProperty(writer, "CoreRemoved_12_5CFF8F6D4A05C15812594CAF6771C66B_0", false);
                SerializeIntProperty(writer, "Module1_5_46B7198E4341C93CBF6AE989EF9898E4_0", 0);
                SerializeIntProperty(writer, "Module2_7_5B7940B84CFD673B25103D96E0AFEEB0_0", 0);
                SerializeIntProperty(writer, "Module3_9_E282C465414F6D4EF2A8039FBA847AD2_0", 0);
                
                // Update size fields
                byte[] result = ms.ToArray();
                
                // Calculate actual sizes
                int structSize = result.Length - 49; // Subtract header size
                int innerSize = structSize - 101; // Subtract inner header size
                
                // Update struct size
                byte[] structSizeBytes = BitConverter.GetBytes(structSize);
                Array.Copy(structSizeBytes, 0, result, structSizePos, 4);
                
                // Update inner size
                byte[] innerSizeBytes = BitConverter.GetBytes(innerSize);
                Array.Copy(innerSizeBytes, 0, result, innerSizePos, 4);
                
                return result;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error serializing inventory item: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Generates a random GUID suffix for property names.
    /// </summary>
    private string GenerateGuidSuffix()
    {
        return Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
    }

    /// <summary>
    /// Generates a new GUID for inventory items.
    /// </summary>
    private string GenerateNewGuid()
    {
        return Guid.NewGuid().ToString().ToUpper();
    }

    /// <summary>
    /// Generates a unique item ID.
    /// </summary>
    private int GenerateItemId()
    {
        Random rand = new Random();
        return rand.Next(100000000, 999999999);
    }

    /// <summary>
    /// Serializes an ObjectProperty.
    /// </summary>
    private void SerializeObjectProperty(BinaryWriter writer, string propertyName, string objectPath)
    {
        // Write property name (null-terminated)
        writer.Write(Encoding.UTF8.GetBytes(propertyName));
        writer.Write((byte)0); // null terminator
        
        // Write type length (4 bytes) - "ObjectProperty\0" = 15 bytes
        writer.Write(15);
        
        // Write type name (null-terminated)
        writer.Write(Encoding.UTF8.GetBytes("ObjectProperty"));
        writer.Write((byte)0); // null terminator
        
        // Write unknown (4 bytes)
        writer.Write(0);
        
        // Write size (4 bytes) - object path length + 1 for null terminator
        writer.Write(objectPath.Length + 1);
        
        // Write array index (1 byte)
        writer.Write((byte)0);
        
        // Write object path (null-terminated)
        writer.Write(Encoding.UTF8.GetBytes(objectPath));
        writer.Write((byte)0); // null terminator
    }

    /// <summary>
    /// Serializes an IntProperty.
    /// </summary>
    private void SerializeIntProperty(BinaryWriter writer, string propertyName, int value)
    {
        // Write property name (null-terminated)
        writer.Write(Encoding.UTF8.GetBytes(propertyName));
        writer.Write((byte)0); // null terminator
        
        // Write type length (4 bytes) - "IntProperty\0" = 12 bytes
        writer.Write(12);
        
        // Write type name (null-terminated)
        writer.Write(Encoding.UTF8.GetBytes("IntProperty"));
        writer.Write((byte)0); // null terminator
        
        // Write unknown (4 bytes)
        writer.Write(0);
        
        // Write size (4 bytes)
        writer.Write(4);
        
        // Write array index (1 byte)
        writer.Write((byte)0);
        
        // Write value (4 bytes)
        writer.Write(value);
    }

    /// <summary>
    /// Serializes a BoolProperty.
    /// </summary>
    private void SerializeBoolProperty(BinaryWriter writer, string propertyName, bool value)
    {
        // Write property name (null-terminated)
        writer.Write(Encoding.UTF8.GetBytes(propertyName));
        writer.Write((byte)0); // null terminator
        
        // Write type length (4 bytes) - "BoolProperty\0" = 13 bytes
        writer.Write(13);
        
        // Write type name (null-terminated)
        writer.Write(Encoding.UTF8.GetBytes("BoolProperty"));
        writer.Write((byte)0); // null terminator
        
        // Write unknown (4 bytes)
        writer.Write(0);
        
        // Write size (4 bytes)
        writer.Write(1);
        
        // Write array index (1 byte)
        writer.Write((byte)0);
        
        // Write value (1 byte)
        writer.Write((byte)(value ? 1 : 0));
    }

    /// <summary>
    /// Finds the position where new items should be inserted in the array.
    /// </summary>
    /// <param name="arrayOffset">Offset of the ArrayProperty</param>
    /// <returns>Position to insert new items, or -1 if error</returns>
    private int FindEndOfArrayItems(int arrayOffset)
    {
        try
        {
            Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: Starting from offset {arrayOffset}");
            int pos = arrayOffset;
            
            // Skip property name
            while (pos < _data.Length && _data[pos] != 0) pos++;
            pos++; // Skip null
            Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: After property name, pos = {pos}");
            
            // Read type length (4 bytes)
            if (pos + 4 > _data.Length) 
            {
                Console.WriteLine("[GameProgressParser] FindEndOfArrayItems: ERROR - pos + 4 exceeds file length at typeLen");
                return -1;
            }
            int typeLen = BitConverter.ToInt32(_data, pos);
            Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: Type length = {typeLen}");
            pos += 4;
            
            // Validate type length is reasonable
            if (typeLen < 1 || typeLen > 100)
            {
                Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: ERROR - Invalid type length {typeLen}");
                return -1;
            }
            
            // Skip type name "ArrayProperty\0"
            pos += typeLen;
            Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: After type name, pos = {pos}");
            
        // Skip unknown (4 bytes)
        pos += 4;
        Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: After unknown, pos = {pos}");

        // READ ELEMENT TYPE LENGTH (4 bytes) - This is elemTypeLen, NOT arrayIndex!
        // Correct ArrayProperty header structure:
        // [unknown=4][elemTypeLen=4][elem_type_name\0][unknown=1][arrayIndex=4][arraySize=4]
        if (pos + 4 > _data.Length)
        {
            Console.WriteLine("[GameProgressParser] FindEndOfArrayItems: ERROR - pos + 4 exceeds file length at elemTypeLen");
            return -1;
        }
        
        // Add hex dump for debugging
        byte[] elemTypeLenBytes = BitConverter.GetBytes(BitConverter.ToInt32(_data, pos));
        Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: Reading elemTypeLen at pos {pos}, hex: {BitConverter.ToString(elemTypeLenBytes)}");
        
        int elemTypeLen = BitConverter.ToInt32(_data, pos);
        Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: Element type length = {elemTypeLen}");
        pos += 4;
            
            // Validate element type length is reasonable (should be 15 for "StructProperty\0")
            if (elemTypeLen < 1 || elemTypeLen > 100)
            {
                Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: ERROR - Invalid element type length {elemTypeLen}");
                return -1;
            }
            
        // Validate element type length is exactly 15
        if (elemTypeLen != 15)
        {
            Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: WARNING - Expected element type length 15, got {elemTypeLen}");
        }

        // Show hex dump of element type name bytes before skipping
        if (pos + elemTypeLen <= _data.Length)
        {
            byte[] elemTypeNameBytes = new byte[elemTypeLen];
            Array.Copy(_data, pos, elemTypeNameBytes, 0, elemTypeLen);
            string elemTypeName = Encoding.UTF8.GetString(elemTypeNameBytes);
            Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: Element type name at pos {pos} = '{elemTypeName}' (length {elemTypeLen})");
        }

        // Skip element type name "StructProperty\0"
        pos += elemTypeLen;
        Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: After element type name, pos = {pos}");
            
// Skip unknown byte (1 byte)
if (pos >= _data.Length)
{
    Console.WriteLine("[GameProgressParser] FindEndOfArrayItems: ERROR - pos exceeds file length at unknown byte");
    return -1;
}
byte unknownByte = _data[pos];
Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: Unknown byte at pos {pos} = 0x{unknownByte:X2}");
pos += 1;
Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: After unknown byte, pos = {pos}");

// === CRITICAL FIX: ArrayProperty header uses BIG-ENDIAN byte order! ===
// Structure: [unknown=1][structSize=4-BE][arrayIndex=4-BE][arraySize=4-BE]
// Skip struct size field (4 bytes, big-endian)
if (pos + 4 > _data.Length)
{
Console.WriteLine("[GameProgressParser] FindEndOfArrayItems: ERROR - pos + 4 exceeds file length at structSize");
return -1;
}

Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: Reading structSize at pos {pos}, RAW bytes: {_data[pos]:X2}-{_data[pos+1]:X2}-{_data[pos+2]:X2}-{_data[pos+3]:X2}");
int structSize = (_data[pos] << 24) | (_data[pos+1] << 16) | (_data[pos+2] << 8) | _data[pos+3];
Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: Struct size = {structSize} (BIG-ENDIAN)");
pos += 4;
Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: After struct size, pos = {pos}");

// Skip array index (4 bytes, big-endian) - comes BEFORE array size
if (pos + 4 > _data.Length)
{
Console.WriteLine("[GameProgressParser] FindEndOfArrayItems: ERROR - pos + 4 exceeds file length at arrayIndex");
return -1;
}

Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: Reading arrayIndex at pos {pos}, RAW bytes: {_data[pos]:X2}-{_data[pos+1]:X2}-{_data[pos+2]:X2}-{_data[pos+3]:X2}");
int arrayIndex = (_data[pos] << 24) | (_data[pos+1] << 16) | (_data[pos+2] << 8) | _data[pos+3];
Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: Array index = {arrayIndex} (BIG-ENDIAN)");
pos += 4;
Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: After array index, pos = {pos}");

// === COMPREHENSIVE SEARCH FOR arraySize (should be ~195) in next 1000 bytes ===
int arraySize = -1;
int arraySizeOffset = -1;
Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: === SEARCHING FOR ACTUAL ARRAY SIZE (should be ~195) in next 1000 bytes ===");

// First, try exact pattern 00-00-00-C3 (195)
for (int i = 0; i < 1000 && pos + i + 3 < _data.Length; i++)
{
    if (_data[pos + i] == 0x00 && 
        _data[pos + i + 1] == 0x00 && 
        _data[pos + i + 2] == 0x00 && 
        _data[pos + i + 3] == 0xC3)
    {
        Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: ✓ FOUND pattern 00-00-00-C3 at offset {pos + i} (relative +{i})");
        arraySizeOffset = i;
        arraySize = 195;
        break;
    }
}

// If not found, search for any 4-byte big-endian value in range 150-250
if (arraySize == -1)
{
    Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: Exact pattern not found, searching for array size in range 150-250...");
    for (int i = 0; i < 1000 && pos + i + 3 < _data.Length; i++)
    {
        int candidate = (_data[pos + i] << 24) | (_data[pos + i + 1] << 16) | (_data[pos + i + 2] << 8) | _data[pos + i + 3];
        if (candidate >= 150 && candidate <= 250)
        {
            // Check if previous 3 bytes are 00-00-00 (typical arraySize pattern)
            if (i >= 3 && _data[pos + i-3] == 0x00 && _data[pos + i-2] == 0x00 && _data[pos + i-1] == 0x00)
            {
                Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: ✓ Found valid arraySize={candidate} at offset +{i}");
                arraySizeOffset = i;
                arraySize = candidate;
                break;
            }
        }
    }
}

if (arraySize == -1)
{
    Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: Searching for any valid array size in range 100-500...");
    for (int i = 0; i < 1000 && pos + i + 3 < _data.Length; i++)
    {
        int candidate = (_data[pos + i] << 24) | (_data[pos + i + 1] << 16) | (_data[pos + i + 2] << 8) | _data[pos + i + 3];
        if (candidate >= 100 && candidate <= 500)
        {
            Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: Found candidate at offset +{i}: bytes={_data[pos+i]:X2}-{_data[pos+i+1]:X2}-{_data[pos+i+2]:X2}-{_data[pos+i+3]:X2}, value={candidate}");
            arraySizeOffset = i;
            arraySize = candidate;
            break;
        }
    }
}

if (arraySize == -1)
{
    Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: No valid array size found, trying fallback - read at current pos");
    // Fallback: try reading at current position
    if (pos + 4 > _data.Length)
    {
        Console.WriteLine("[GameProgressParser] FindEndOfArrayItems: ERROR - pos + 4 exceeds file length at arraySize");
        return -1;
}

Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: Reading arraySize at pos {pos}, RAW bytes: {_data[pos]:X2}-{_data[pos+1]:X2}-{_data[pos+2]:X2}-{_data[pos+3]:X2}");
arraySize = (_data[pos] << 24) | (_data[pos+1] << 16) | (_data[pos+2] << 8) | _data[pos+3];
Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: Array size = {arraySize}");
pos += 4;
}
else
{
Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: SUCCESS - Found arraySize={arraySize} at offset +{arraySizeOffset}");
// Advance pos to after the found arraySize
pos = pos + arraySizeOffset + 4;
}

Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: After array size, pos = {pos}");

// Validate array size is reasonable
if (arraySize < 0 || arraySize > 10000)
{
Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: ERROR - Invalid array size {arraySize}");
return -1;
}
            
            Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: First element starts at offset {pos}");
            
            // Skip existing array items
            for (int i = 0; i < arraySize; i++)
            {
                Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: Skipping item {i + 1} of {arraySize} at offset {pos}");
                // Skip each StructProperty item
                int beforeSkip = pos;
                pos = SkipStructProperty(pos);
                if (pos < 0) 
                {
                    Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: ERROR - SkipStructProperty returned -1 at item {i + 1}, offset {beforeSkip}");
                    // Dump hex for debugging
                    Console.WriteLine("[GameProgressParser] FindEndOfArrayItems: Hex dump of first 100 bytes:");
                    for (int j = 0; j < 100 && (beforeSkip + j) < _data.Length; j++)
                    {
                        if (j % 16 == 0) Console.Write("\n  ");
                        Console.Write($"{_data[beforeSkip + j]:X2} ");
                    }
                    Console.WriteLine();
                    return -1;
                }
            }
            
            Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: Insert position = {pos}");
            return pos;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems: EXCEPTION - {ex.Message}");
            return -1;
        }
    }

    /// <summary>
    /// Skips a StructProperty and returns the position after it.
    /// </summary>
    private int SkipStructProperty(int startPos)
    {
        try
        {
            int pos = startPos;
            
            // Skip property name
            while (pos < _data.Length && _data[pos] != 0) pos++;
            pos++; // Skip null
            
            // Read type length (4 bytes)
            if (pos + 4 > _data.Length) return -1;
            int typeLen = BitConverter.ToInt32(_data, pos);
            pos += 4;
            
            // Skip type name
            pos += typeLen;
            
            // Skip unknown (4 bytes)
            pos += 4;
            
            // Skip struct type string length and content
            if (pos + 4 > _data.Length) return -1;
            int structTypeLen = BitConverter.ToInt32(_data, pos);
            pos += 4 + structTypeLen;
            
            // Skip array index (4 bytes)
            pos += 4;
            
            // Read struct size (4 bytes)
            if (pos + 4 > _data.Length) return -1;
            int structSize = BitConverter.ToInt32(_data, pos);
            pos += 4;
            
            // Skip struct content
            pos += structSize;
            
            return pos;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Inserts serialized items at the specified position in the data.
    /// </summary>
    private bool InsertItemsAtPosition(int position, List<byte[]> items)
    {
        try
        {
            Console.WriteLine($"[GameProgressParser] InsertItemsAtPosition: Inserting {items.Count} items at position {position}");
            // Calculate total size needed
            int totalSize = 0;
            foreach (var item in items)
            {
                totalSize += item.Length;
            }
            Console.WriteLine($"[GameProgressParser] InsertItemsAtPosition: Total size to insert = {totalSize} bytes");
            Console.WriteLine($"[GameProgressParser] InsertItemsAtPosition: Original file size = {_data.Length} bytes");
            
            // Create new data array
            byte[] newData = new byte[_data.Length + totalSize];
            Console.WriteLine($"[GameProgressParser] InsertItemsAtPosition: New file size = {newData.Length} bytes");
            
            // Copy data before insertion point
            Array.Copy(_data, 0, newData, 0, position);
            Console.WriteLine("[GameProgressParser] InsertItemsAtPosition: Copied data before insertion point");
            
            // Insert new items
            int currentPos = position;
            foreach (var item in items)
            {
                Array.Copy(item, 0, newData, currentPos, item.Length);
                currentPos += item.Length;
            }
            Console.WriteLine($"[GameProgressParser] InsertItemsAtPosition: Inserted {items.Count} items");
            
            // Copy data after insertion point
            Array.Copy(_data, position, newData, currentPos, _data.Length - position);
            Console.WriteLine("[GameProgressParser] InsertItemsAtPosition: Copied data after insertion point");
            
            // Update the data array
            _data = newData;
            Console.WriteLine("[GameProgressParser] InsertItemsAtPosition: SUCCESS");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameProgressParser] InsertItemsAtPosition: EXCEPTION - {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Error inserting items: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Updates the size field of an ArrayProperty.
    /// </summary>
    private bool UpdateArrayPropertySize(int arrayOffset, int newSize)
    {
        try
        {
            Console.WriteLine($"[GameProgressParser] UpdateArrayPropertySize: Updating array at offset {arrayOffset} to size {newSize}");
            int pos = arrayOffset;
            
            // Skip property name
            while (pos < _data.Length && _data[pos] != 0) pos++;
            pos++; // Skip null
            Console.WriteLine($"[GameProgressParser] UpdateArrayPropertySize: After property name, pos = {pos}");
            
            // Read type length (4 bytes)
            if (pos + 4 > _data.Length) 
            {
                Console.WriteLine("[GameProgressParser] UpdateArrayPropertySize: ERROR - pos + 4 exceeds file length at typeLen");
                return false;
            }
            int typeLen = BitConverter.ToInt32(_data, pos);
            Console.WriteLine($"[GameProgressParser] UpdateArrayPropertySize: Type length = {typeLen}");
            pos += 4;
            
            // Validate type length is reasonable
            if (typeLen < 1 || typeLen > 100)
            {
                Console.WriteLine($"[GameProgressParser] UpdateArrayPropertySize: ERROR - Invalid type length {typeLen}");
                return false;
            }
            
            // Skip type name "ArrayProperty\0"
            pos += typeLen;
            Console.WriteLine($"[GameProgressParser] UpdateArrayPropertySize: After type name, pos = {pos}");
            
        // Skip unknown (4 bytes)
        pos += 4;
        Console.WriteLine($"[GameProgressParser] UpdateArrayPropertySize: After unknown, pos = {pos}");

        // READ ELEMENT TYPE LENGTH (4 bytes) - This is elemTypeLen, NOT arrayIndex!
        // Correct ArrayProperty header structure:
        // [unknown=4][elemTypeLen=4][elem_type_name\0][unknown=1][arrayIndex=4][arraySize=4]
        if (pos + 4 > _data.Length)
        {
            Console.WriteLine("[GameProgressParser] UpdateArrayPropertySize: ERROR - pos + 4 exceeds file length at elemTypeLen");
            return false;
        }
        
        // Add hex dump for debugging
        byte[] elemTypeLenBytes = BitConverter.GetBytes(BitConverter.ToInt32(_data, pos));
        Console.WriteLine($"[GameProgressParser] UpdateArrayPropertySize: Reading elemTypeLen at pos {pos}, hex: {BitConverter.ToString(elemTypeLenBytes)}");
        
        int elemTypeLen = BitConverter.ToInt32(_data, pos);
        Console.WriteLine($"[GameProgressParser] UpdateArrayPropertySize: Element type length = {elemTypeLen}");
        pos += 4;
            
            // Validate element type length is reasonable
            if (elemTypeLen < 1 || elemTypeLen > 100)
            {
                Console.WriteLine($"[GameProgressParser] UpdateArrayPropertySize: ERROR - Invalid element type length {elemTypeLen}");
                return false;
            }
            
        // Validate element type length is exactly 15
        if (elemTypeLen != 15)
        {
            Console.WriteLine($"[GameProgressParser] UpdateArrayPropertySize: WARNING - Expected element type length 15, got {elemTypeLen}");
        }

        // Show hex dump of element type name bytes before skipping
        if (pos + elemTypeLen <= _data.Length)
        {
            byte[] elemTypeNameBytes = new byte[elemTypeLen];
            Array.Copy(_data, pos, elemTypeNameBytes, 0, elemTypeLen);
            string elemTypeName = Encoding.UTF8.GetString(elemTypeNameBytes);
            Console.WriteLine($"[GameProgressParser] UpdateArrayPropertySize: Element type name at pos {pos} = '{elemTypeName}' (length {elemTypeLen})");
        }

        // Skip element type name "StructProperty\0"
        pos += elemTypeLen;
        Console.WriteLine($"[GameProgressParser] UpdateArrayPropertySize: After element type name, pos = {pos}");
            
// Skip unknown byte (1 byte)
if (pos >= _data.Length)
{
    Console.WriteLine("[GameProgressParser] UpdateArrayPropertySize: ERROR - pos exceeds file length at unknown byte");
    return false;
}
byte unknownByte = _data[pos];
Console.WriteLine($"[GameProgressParser] UpdateArrayPropertySize: Unknown byte at pos {pos} = 0x{unknownByte:X2}");
pos += 1;
Console.WriteLine($"[GameProgressParser] UpdateArrayPropertySize: After unknown byte, pos = {pos}");

// === CRITICAL FIX: ArrayProperty header uses BIG-ENDIAN byte order! ===
// Structure: [unknown=1][structSize=4-BE][arrayIndex=4-BE][arraySize=4-BE]
// Skip struct size field (4 bytes, big-endian)
if (pos + 4 > _data.Length)
{
Console.WriteLine("[GameProgressParser] UpdateArrayPropertySize: ERROR - pos + 4 exceeds file length at structSize");
return false;
}

Console.WriteLine($"[GameProgressParser] UpdateArrayPropertySize: Reading structSize at pos {pos}, RAW bytes: {_data[pos]:X2}-{_data[pos+1]:X2}-{_data[pos+2]:X2}-{_data[pos+3]:X2}");
int structSize = (_data[pos] << 24) | (_data[pos+1] << 16) | (_data[pos+2] << 8) | _data[pos+3];
Console.WriteLine($"[GameProgressParser] UpdateArrayPropertySize: Struct size = {structSize} (BIG-ENDIAN)");
pos += 4;
Console.WriteLine($"[GameProgressParser] UpdateArrayPropertySize: After struct size, pos = {pos}");

// Skip array index (4 bytes, big-endian) - comes BEFORE array size
if (pos + 4 > _data.Length)
{
Console.WriteLine("[GameProgressParser] UpdateArrayPropertySize: ERROR - pos + 4 exceeds file length at arrayIndex");
return false;
}

Console.WriteLine($"[GameProgressParser] UpdateArrayPropertySize: Reading arrayIndex at pos {pos}, RAW bytes: {_data[pos]:X2}-{_data[pos+1]:X2}-{_data[pos+2]:X2}-{_data[pos+3]:X2}");
int arrayIndex = (_data[pos] << 24) | (_data[pos+1] << 16) | (_data[pos+2] << 8) | _data[pos+3];
Console.WriteLine($"[GameProgressParser] UpdateArrayPropertySize: Array index = {arrayIndex} (BIG-ENDIAN)");
pos += 4;
Console.WriteLine($"[GameProgressParser] UpdateArrayPropertySize: After array index, pos = {pos}");

// Validate newSize is reasonable
if (newSize < 0 || newSize > 10000)
{
Console.WriteLine($"[GameProgressParser] UpdateArrayPropertySize: ERROR - Invalid new size {newSize}");
return false;
}

// Update array size (4 bytes, big-endian)
if (pos + 4 > _data.Length)
{
Console.WriteLine("[GameProgressParser] UpdateArrayPropertySize: ERROR - pos + 4 exceeds file length at arraySize");
return false;
}

// Convert to big-endian bytes for writing
byte[] newSizeBytes = new byte[4];
newSizeBytes[0] = (byte)((newSize >> 24) & 0xFF);
newSizeBytes[1] = (byte)((newSize >> 16) & 0xFF);
newSizeBytes[2] = (byte)((newSize >> 8) & 0xFF);
newSizeBytes[3] = (byte)(newSize & 0xFF);
Console.WriteLine($"[GameProgressParser] UpdateArrayPropertySize: Writing new size {newSize} (BIG-ENDIAN: {newSizeBytes[0]:X2}-{newSizeBytes[1]:X2}-{newSizeBytes[2]:X2}-{newSizeBytes[3]:X2}) at position {pos}");

Array.Copy(newSizeBytes, 0, _data, pos, 4);
Console.WriteLine($"[GameProgressParser] UpdateArrayPropertySize: SUCCESS - wrote {newSize} at position {pos}");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameProgressParser] UpdateArrayPropertySize: EXCEPTION - {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Error updating array size: {ex.Message}");
            return false;
        }
    }

/// <summary>
    /// Update inventory by replacing the raw array data.
    /// Note: Full inventory editing requires complex StructProperty/ArrayProperty serialization.
    /// For now, this method is a placeholder.
    /// </summary>
    public bool UpdateInventory(List<InventoryItem> items)
    {
        // TODO: Implement full inventory serialization
        return false;
    }

    /// <summary>
    /// Adds items to the insured inventory by modifying the GVAS data.
    /// </summary>
    /// <param name="itemsToAdd">List of items to add to insured inventory</param>
    /// <returns>True if successful, false otherwise</returns>
    public bool AddItemsToInsuredInventory(List<InventoryItem> itemsToAdd)
    {
        try
        {
            Console.WriteLine($"[GameProgressParser] AddItemsToInsuredInventory called with {itemsToAdd?.Count ?? 0} items");
            
            if (itemsToAdd == null || itemsToAdd.Count == 0)
            {
                Console.WriteLine("[GameProgressParser] No items to add, returning true");
                return true;
            }

            // Find the Insured Items struct offset
            Console.WriteLine("[GameProgressParser] Finding Insured Items struct offset...");
            int insuredItemsOffset = FindInsuredItemsOffset();
            Console.WriteLine($"[GameProgressParser] FindInsuredItemsOffset returned: {insuredItemsOffset}");
            if (insuredItemsOffset < 0)
            {
                Console.WriteLine("[GameProgressParser] ERROR: Could not find Insured Items struct in file");
                return false;
            }

            // Parse the Insured Items struct to find the ArmorPassports ArrayProperty
            Console.WriteLine("[GameProgressParser] Finding ArmorPassports ArrayProperty...");
            int arrayPropertyOffset = FindInsuredItemsArrayProperty(insuredItemsOffset);
            Console.WriteLine($"[GameProgressParser] FindInsuredItemsArrayProperty returned: {arrayPropertyOffset}");
            if (arrayPropertyOffset < 0)
            {
                Console.WriteLine("[GameProgressParser] ERROR: Could not find ArmorPassports ArrayProperty");
                return false;
            }

            // Get current array size
            Console.WriteLine("[GameProgressParser] Getting current array size...");
            int currentArraySize = GetArrayPropertySize(arrayPropertyOffset);
            Console.WriteLine($"[GameProgressParser] GetArrayPropertySize returned: {currentArraySize}");
            if (currentArraySize < 0)
            {
                Console.WriteLine("[GameProgressParser] ERROR: Could not get array size");
                return false;
            }

            // Serialize new items
            Console.WriteLine($"[GameProgressParser] Serializing {itemsToAdd.Count} items...");
            var serializedItems = new List<byte[]>();
            foreach (var item in itemsToAdd)
            {
                Console.WriteLine($"[GameProgressParser] Serializing item: {item.ItemName}");
                var serializedItem = SerializeInventoryItem(item);
                if (serializedItem != null && serializedItem.Length > 0)
                {
                    serializedItems.Add(serializedItem);
                    Console.WriteLine($"[GameProgressParser] Successfully serialized item, size: {serializedItem.Length} bytes");
                }
                else
                {
                    Console.WriteLine($"[GameProgressParser] WARNING: Failed to serialize item {item.ItemName}");
                }
            }

            Console.WriteLine($"[GameProgressParser] Serialized {serializedItems.Count} items successfully");
            if (serializedItems.Count == 0)
            {
                Console.WriteLine("[GameProgressParser] ERROR: No items were serialized");
                return true;
            }

            // Insert serialized items at the end of the array
            Console.WriteLine("[GameProgressParser] Finding insert position...");
            int insertPosition = FindEndOfArrayItems(arrayPropertyOffset);
            Console.WriteLine($"[GameProgressParser] FindEndOfArrayItems returned: {insertPosition}");
            if (insertPosition < 0)
            {
                Console.WriteLine("[GameProgressParser] ERROR: Could not find insert position");
                return false;
            }

            // Insert data and update array size
            Console.WriteLine("[GameProgressParser] Inserting items at position...");
            bool insertSuccess = InsertItemsAtPosition(insertPosition, serializedItems);
            Console.WriteLine($"[GameProgressParser] InsertItemsAtPosition returned: {insertSuccess}");
            
            Console.WriteLine("[GameProgressParser] Updating array size...");
            bool sizeSuccess = UpdateArrayPropertySize(arrayPropertyOffset, currentArraySize + serializedItems.Count);
            Console.WriteLine($"[GameProgressParser] UpdateArrayPropertySize returned: {sizeSuccess}");
            
            if (insertSuccess && sizeSuccess)
            {
                Console.WriteLine($"[GameProgressParser] SUCCESS: Added {serializedItems.Count} items to insured inventory");
                return true;
            }

            Console.WriteLine("[GameProgressParser] ERROR: Failed to insert items or update array size");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameProgressParser] EXCEPTION in AddItemsToInsuredInventory: {ex.Message}");
            Console.WriteLine($"[GameProgressParser] Stack trace: {ex.StackTrace}");
            System.Diagnostics.Debug.WriteLine($"Error in AddItemsToInsuredInventory: {ex.Message}");
            return false;
        }
    }

    private bool TryConvertToInt(double value, out int result)
    {
        result = (int)value;
        return true;
    }
}
