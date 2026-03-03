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
                    else
                    {
                    }
                }
                catch { }
            }
        }
        catch { }

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
    /// </summary>
    private int FindArrayProperty(int startPos, int endPos)
    {
        int arrayPropBytesPos = FindBytes(_data, Encoding.UTF8.GetBytes("ArrayProperty"), startPos, endPos);
        
        if (arrayPropBytesPos < 0)
        {
            return -1;
        }
        
        // The ArrayProperty starts with its property name, then type length, then "ArrayProperty" type
        // So we need to go backwards to find the property name
        int nameEnd = arrayPropBytesPos;
        int nameStart = nameEnd - 1;
        while (nameStart >= startPos && _data[nameStart] != 0) nameStart--;
        nameStart++; // Move past the null
        
        if (nameStart < startPos) nameStart = startPos;
        
        var arrayName = Encoding.UTF8.GetString(_data, nameStart, nameEnd - nameStart);
        
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
    /// Update inventory by replacing the raw array data.
    /// Note: Full inventory editing requires complex StructProperty/ArrayProperty serialization.
    /// For now, this method is a placeholder.
    /// </summary>
    public bool UpdateInventory(List<InventoryItem> items)
    {
        // TODO: Implement full inventory serialization
        return false;
    }

    private bool TryConvertToInt(double value, out int result)
    {
        result = (int)value;
        return true;
    }
}
