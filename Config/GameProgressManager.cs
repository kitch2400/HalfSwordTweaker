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
            Console.WriteLine($"[GameProgressManager] DEV MODE: {_gameProgressPath}");
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _gameProgressPath = Path.Combine(appData, "HalfswordUE5", "Saved", "SaveGames", "GameProgress.sav");
            _backupPath = Path.Combine(appData, "HalfSwordTweaker", "Backups", "GameProgress.sav.bak");
            Console.WriteLine($"[GameProgressManager] Production mode: {_gameProgressPath}");
        }
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
    /// Parse the Inventory ArrayProperty into a list of InventoryItem objects.
    /// The structure is: Items (StructProperty) -> ArmorPassports_3_... (ArrayProperty) -> Items (StructProperty with ObjectProperty)
    /// </summary>
    private List<InventoryItem>? ParseInventoryArray(string propertyName)
    {
        try
        {
            Console.WriteLine($"[GameProgressParser] Parsing inventory array: {propertyName}");
            
            // For "Items" property, we need to find the "Player Inventory" StructProperty
            // There are multiple inventories in the save file:
            // - Player Inventory (at 0x00008842) - THIS IS WHAT WE WANT
            // - Merchant Inventory (at 0x00035A66) - trader stock
            // - Insured Items (at 0x000D8401) - insured item storage
            int inventoryOffset = FindPlayerInventoryOffset();
            if (inventoryOffset < 0)
            {
                Console.WriteLine($"[GameProgressParser] Player Inventory StructProperty not found");
                return new List<InventoryItem>();
            }
            
            Console.WriteLine($"[GameProgressParser] Found PLAYER Inventory StructProperty at 0x{inventoryOffset:X8}");
            
            // Parse the Inventory StructProperty to find the ArmorPassports ArrayProperty
            return ParseInventoryFromStruct(inventoryOffset);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameProgressParser] Error parsing inventory: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return null;
        }
    }
    
    /// <summary>
    /// Parse inventory from the Inventory StructProperty containing ArmorPassports ArrayProperty.
    /// </summary>
    private List<InventoryItem> ParseInventoryFromStruct(int inventoryOffset)
    {
        Console.WriteLine($"[GameProgressParser] === Starting Inventory Parsing ===");
        Console.WriteLine($"[GameProgressParser] Inventory StructProperty at offset 0x{inventoryOffset:X8}");
        
        int pos = inventoryOffset;
        
        // Read property name
        int propNameStart = pos;
        while (pos < _data.Length && _data[pos] != 0) pos++;
        var propName = Encoding.UTF8.GetString(_data, propNameStart, pos - propNameStart);
        Console.WriteLine($"[GameProgressParser] Property name: '{propName}'");
        pos++; // Skip null
        
        // Read type length
        int typeLen = BitConverter.ToInt32(_data, pos);
        Console.WriteLine($"[GameProgressParser] Type length: {typeLen}");
        pos += 4;
        
        // Read type name (should be "StructProperty")
        var typeName = Encoding.UTF8.GetString(_data, pos, typeLen);
        Console.WriteLine($"[GameProgressParser] Type name: '{typeName}'");
        pos += typeLen;
        
        // Skip unknown (4 bytes)
        Console.WriteLine($"[GameProgressParser] Skipping 4 unknown bytes at 0x{pos:X8}");
        pos += 4;
        
        // Read struct type string length (4 bytes)
        int structTypeLen = BitConverter.ToInt32(_data, pos);
        Console.WriteLine($"[GameProgressParser] Struct type string length: {structTypeLen}");
        pos += 4;
        
        // Read struct type string
        var structType = Encoding.UTF8.GetString(_data, pos, structTypeLen);
        Console.WriteLine($"[GameProgressParser] Struct type string: '{structType}'");
        pos += structTypeLen;
        
        // Read array index (4 bytes)
        int arrayIndex = BitConverter.ToInt32(_data, pos);
        Console.WriteLine($"[GameProgressParser] Array index: {arrayIndex}");
        pos += 4;
        
        // Read struct size (4 bytes)
        int structSize = BitConverter.ToInt32(_data, pos);
        Console.WriteLine($"[GameProgressParser] Struct size field: {structSize}");
        pos += 4;
        
        // Now we're at the struct content
        Console.WriteLine($"[GameProgressParser] Struct content starts at 0x{pos:X8}");
        Console.WriteLine($"[GameProgressParser] === Hex dump of first 100 bytes of struct content ===");
        DumpHex(pos, Math.Min(100, _data.Length - pos));
        
        // The struct content has this structure:
        // 2 null bytes + inner size (4 bytes) + struct type string (null-terminated) + null bytes + GUID length (4 bytes) + GUID string + null bytes + ArrayProperty
        
        // Skip 2 null bytes
        Console.WriteLine($"[GameProgressParser] Skipping 2 null bytes...");
        pos += 2;
        
        // Read inner size
        int innerSize = BitConverter.ToInt32(_data, pos);
        Console.WriteLine($"[GameProgressParser] Inner size: {innerSize}");
        pos += 4;
        
        // Read struct type string (null-terminated)
        int innerTypeStart = pos;
        while (pos < _data.Length && _data[pos] != 0) pos++;
        var innerType = Encoding.UTF8.GetString(_data, innerTypeStart, pos - innerTypeStart);
        Console.WriteLine($"[GameProgressParser] Inner struct type: '{innerType}'");
        pos++; // Skip null
        
        // Skip null bytes
        int nullCount = 0;
        while (pos < _data.Length && _data[pos] == 0) { pos++; nullCount++; }
        Console.WriteLine($"[GameProgressParser] Skipped {nullCount} null bytes");
        
        // Read GUID length
        int guidLen = BitConverter.ToInt32(_data, pos);
        Console.WriteLine($"[GameProgressParser] GUID length: {guidLen}");
        pos += 4;
        
        // Read GUID string
        var guid = Encoding.UTF8.GetString(_data, pos, guidLen);
        Console.WriteLine($"[GameProgressParser] GUID: '{guid}'");
        pos += guidLen;
        
        // Skip null bytes after GUID
        nullCount = 0;
        while (pos < _data.Length && _data[pos] == 0) { pos++; nullCount++; }
        Console.WriteLine($"[GameProgressParser] Skipped {nullCount} null bytes after GUID");
        
        Console.WriteLine($"[GameProgressParser] Now at offset 0x{pos:X8}, searching for ArrayProperty...");
        Console.WriteLine($"[GameProgressParser] === Hex dump of bytes at current position ===");
        DumpHex(pos, 50);
        
        // Now search for ArrayProperty
        int arrayPropertyOffset = FindArrayProperty(pos, Math.Min(pos + 5000, _data.Length));
        
        if (arrayPropertyOffset < 0)
        {
            Console.WriteLine($"[GameProgressParser] ERROR: Could not find ArrayProperty in Inventory struct");
            return new List<InventoryItem>();
        }
        
        Console.WriteLine($"[GameProgressParser] Found ArrayProperty at offset 0x{arrayPropertyOffset:X8}");
        
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
            
            Console.WriteLine($"[GameProgressParser] 0x{i:X8}  {hex,-48}  {ascii}");
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
            Console.WriteLine($"[GameProgressParser] 'ArrayProperty' string not found between 0x{startPos:X8} and 0x{endPos:X8}");
            return -1;
        }
        
        Console.WriteLine($"[GameProgressParser] Found 'ArrayProperty' string at 0x{arrayPropBytesPos:X8}");
        
        // The ArrayProperty starts with its property name, then type length, then "ArrayProperty" type
        // So we need to go backwards to find the property name
        int nameEnd = arrayPropBytesPos;
        int nameStart = nameEnd - 1;
        while (nameStart >= startPos && _data[nameStart] != 0) nameStart--;
        nameStart++; // Move past the null
        
        if (nameStart < startPos) nameStart = startPos;
        
        var arrayName = Encoding.UTF8.GetString(_data, nameStart, nameEnd - nameStart);
        Console.WriteLine($"[GameProgressParser] ArrayProperty name: '{arrayName}'");
        
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
        Console.WriteLine($"[GameProgressParser] ArrayProperty name: '{propName}'");
        pos++; // Skip null
        
        // Read type length
        int typeLen = BitConverter.ToInt32(_data, pos);
        Console.WriteLine($"[GameProgressParser] Type length: {typeLen}");
        pos += 4;
        
        // Read type name (should be "ArrayProperty")
        var typeName = Encoding.UTF8.GetString(_data, pos, typeLen);
        Console.WriteLine($"[GameProgressParser] Type name: '{typeName}'");
        pos += typeLen;
        
        // Skip unknown (4 bytes)
        int unknown = BitConverter.ToInt32(_data, pos);
        Console.WriteLine($"[GameProgressParser] Unknown: 0x{unknown:X8}");
        pos += 4;
        
        // Read array size
        int arraySize = BitConverter.ToInt32(_data, pos);
        Console.WriteLine($"[GameProgressParser] Array size: {arraySize} elements");
        pos += 4;
        
        // Read array index
        int arrayIndex = BitConverter.ToInt32(_data, pos);
        Console.WriteLine($"[GameProgressParser] Array index: {arrayIndex}");
        pos += 4;
        
        Console.WriteLine($"[GameProgressParser] Array elements start at 0x{pos:X8}");
        
        // DUMP STRUCTURAL INFORMATION - Find all property boundaries
        Console.WriteLine($"[GameProgressParser] === DUMPING STRUCTURAL BOUNDARIES ===");
        DumpStructuralBoundaries(pos, Math.Min(pos + 50 * 1024, _data.Length));
        
        // Search for specific Baron items to verify inventory source
        Console.WriteLine($"[GameProgressParser] === SEARCHING FOR BARON ITEMS IN INVENTORY RANGE ===");
        SearchForItem("Baron Cuisses", pos, Math.Min(pos + 350 * 1024, _data.Length));
        SearchForItem("Baron Cuirass", pos, Math.Min(pos + 350 * 1024, _data.Length));
        SearchForItem("Tabard", pos, Math.Min(pos + 350 * 1024, _data.Length));
        
        // ALSO search entire file to see if items exist anywhere
        Console.WriteLine($"[GameProgressParser] === SEARCHING ENTIRE FILE FOR BARON ITEMS ===");
        SearchForItem("Baron Cuisses", 0, _data.Length);
        SearchForItem("Baron Cuirass", 0, _data.Length);
        SearchForItem("Tabard", 0, _data.Length);
        
        // Search for other unique item names to map the save structure
        Console.WriteLine($"[GameProgressParser] === SEARCHING FOR OTHER UNIQUE ITEMS ===");
        SearchForItem("BP_Armor", 0, _data.Length);
        SearchForItem("Modular_Core", 0, _data.Length);
        
        // Find ALL ArrayProperty instances to locate different inventories
        Console.WriteLine($"[GameProgressParser] === FINDING ALL ARRAYPROPERTIES IN FILE ===");
        FindAllArrayProperties();
        
        // Dynamic range approach: search up to 350KB for blueprint paths
        // This covers ~109 items in current save with room for growth
        // Maximum 350KB limit documented in AGENTS.md
        int arrayEnd = Math.Min(pos + 350 * 1024, _data.Length);
        Console.WriteLine($"[GameProgressParser] Searching for blueprint paths from 0x{pos:X8} to 0x{arrayEnd:X8} (max 350KB)");
        
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
                    Console.WriteLine($"[GameProgressParser] Structural marker detected at 0x{markerPos:X8}, stopping inventory search");
                    break;
                }
            }
            
            lastBlueprintPos = blueprintPos;
            
            // Extract the full blueprint path
            int pathEnd = blueprintPos;
            while (pathEnd < arrayEnd && _data[pathEnd] != 0 && _data[pathEnd] != 0x0A && _data[pathEnd] != 0x0D) pathEnd++;
            
            var blueprintPath = Encoding.UTF8.GetString(_data, blueprintPos, pathEnd - blueprintPos);
            Console.WriteLine($"[GameProgressParser] Found blueprint path: '{blueprintPath}'");
            
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
            Console.WriteLine($"[GameProgressParser] WARNING: Hit 500KB search limit! Inventory may contain more items.");
        }
        
        Console.WriteLine($"[GameProgressParser] === Parsed {items.Count} inventory items ===");
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
                Console.WriteLine($"[GameProgressParser] 0x{nameStart:X8}: '{propName}' ({typeName}) size={size}");
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
                    Console.WriteLine($"[GameProgressParser] ArrayProperty '{propName}' at offset 0x{i:X8}");
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
                Console.WriteLine($"[GameProgressParser] FOUND '{itemName}' at offset 0x{i:X8}");
                
                // Extract context - show surrounding text
                int contextStart = Math.Max(i - 50, startPos);
                int contextEnd = Math.Min(i + searchBytes.Length + 50, endPos);
                var context = Encoding.UTF8.GetString(_data, contextStart, contextEnd - contextStart);
                Console.WriteLine($"[GameProgressParser] Context: ...{context}...");
                return;
            }
        }
        
        Console.WriteLine($"[GameProgressParser] '{itemName}' NOT found in range 0x{startPos:X8} - 0x{endPos:X8}");
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
                Console.WriteLine($"[GameProgressParser] Found '{marker}' at 0x{pos:X8}");
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

        Console.WriteLine($"[GameProgressParser] StructProperty name: {Encoding.UTF8.GetString(_data, nameStart, nameLen)}");

        // Read type length (4 bytes)
        if (pos + 4 > _data.Length) return pos;
        int typeLen = BitConverter.ToInt32(_data, pos);
        pos += 4;

        // Read type name (should be "StructProperty\0")
        if (pos + typeLen > _data.Length) return pos;
        var typeName = Encoding.UTF8.GetString(_data, pos, typeLen);
        pos += typeLen;
        Console.WriteLine($"[GameProgressParser] StructProperty type: {typeName.TrimEnd('\0')}");

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
            Console.WriteLine($"[GameProgressParser] ERROR: Invalid struct type length: {structTypeLen}");
            return pos;
        }
        
        // Read struct type string
        if (pos + structTypeLen > _data.Length) return pos;
        var structType = Encoding.UTF8.GetString(_data, pos, structTypeLen);
        pos += structTypeLen;
        Console.WriteLine($"[GameProgressParser] Struct type string: {structType.TrimEnd('\0')}");

        // Read array index (4 bytes)
        if (pos + 4 > _data.Length) return pos;
        int arrayIndex = BitConverter.ToInt32(_data, pos);
        pos += 4;
        Console.WriteLine($"[GameProgressParser] Array index: {arrayIndex}");

        // Read struct size (4 bytes) - but this might not be the full size
        if (pos + 4 > _data.Length) return pos;
        int structSize = BitConverter.ToInt32(_data, pos);
        pos += 4;
        Console.WriteLine($"[GameProgressParser] Struct size field: {structSize}");
        
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
        Console.WriteLine($"[GameProgressParser] Inner size: {innerSize}");
        
        // Read struct type string (null-terminated)
        int structTypeStart = pos;
        while (pos < _data.Length && _data[pos] != 0) pos++;
        var innerStructType = Encoding.UTF8.GetString(_data, structTypeStart, pos - structTypeStart);
        pos++; // Skip null
        Console.WriteLine($"[GameProgressParser] Inner struct type: {innerStructType}");
        
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
        
        Console.WriteLine($"[GameProgressParser] Now at offset 0x{pos:X8}, searching for ArrayProperty");
        Console.WriteLine($"[GameProgressParser] Struct size field: {structSize}, searching up to 200KB from here");
        
        // Dump hex of first 100 bytes after GUID to see what we're looking at
        Console.WriteLine("[GameProgressParser] Hex dump of struct content (first 100 bytes):");
        int dumpEnd = Math.Min(pos + 100, _data.Length);
        StringBuilder hexDump = new StringBuilder();
        for (int i = pos; i < dumpEnd; i++)
        {
            hexDump.Append(_data[i].ToString("X2") + " ");
            if ((i - pos + 1) % 16 == 0)
            {
                Console.WriteLine($"[GameProgressParser]   {hexDump.ToString()}");
                hexDump.Clear();
            }
        }
        if (hexDump.Length > 0)
        {
            Console.WriteLine($"[GameProgressParser]   {hexDump.ToString()}");
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
        
        Console.WriteLine($"[GameProgressParser] Starting search at 0x{startPos:X8}, ending at 0x{endPos:X8}");
        
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
                Console.WriteLine($"[GameProgressParser] Invalid type length {typeLen} at property '{propName}', aborting search");
                break;
            }
            
            var typeName = Encoding.UTF8.GetString(_data, pos, typeLen);
            pos += typeLen;
            
            // Log ALL properties for first 30 searches, then only ArrayProperty and StructProperty
            if (searchCount <= 30 || typeName.Contains("Array") || typeName.Contains("Struct"))
            {
                Console.WriteLine($"[GameProgressParser] Property #{searchCount}: '{propName}' (Type: '{typeName}') at offset 0x{nameStart:X8}");
            }

            // Check if this is an ArrayProperty
            if (typeName == "ArrayProperty")
            {
                Console.WriteLine($"[GameProgressParser] >>> FOUND ArrayProperty: {propName} at offset 0x{nameStart:X8}");
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
                Console.WriteLine($"[GameProgressParser] Invalid size {size} for property '{propName}', stopping search");
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
                Console.WriteLine($"[GameProgressParser] Size {size} exceeds remaining data for '{propName}', stopping");
                break;
            }
        }

        Console.WriteLine($"[GameProgressParser] Searched {searchCount} properties, did not find ArrayProperty");
        Console.WriteLine($"[GameProgressParser] Final position: 0x{pos:X8}");
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
                
                Console.WriteLine($"[GameProgressParser] Found 'Player Inventory' marker at 0x{i:X8}, property starts at 0x{pos:X8}");
                return pos;
            }
        }
        
        Console.WriteLine($"[GameProgressParser] 'Player Inventory' not found");
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

            Console.WriteLine($"[GameProgressParser] Item: {item.ItemName} -> {item.ObjectPath}");
            return item;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameProgressParser] Error parsing inventory item: {ex.Message}");
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

            Console.WriteLine($"[GameProgressParser] Parsed item: {item.ItemName} ({item.ItemType}) at offset 0x{nameStart:X8}");

            return item;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameProgressParser] Error parsing struct: {ex.Message}");
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

    /// <summary>
    /// Get inventory items as a list of InventoryItem objects.
    /// </summary>
    public List<InventoryItem>? GetInventory()
    {
        var offset = FindPropertyOffset("Items");
        if (offset < 0)
        {
            Console.WriteLine("[GameProgressParser] Inventory not found");
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
            Console.WriteLine("[GameProgressParser] Player Inventory not found");
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
            Console.WriteLine("[GameProgressParser] Insured Items not found");
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
                
                Console.WriteLine($"[GameProgressParser] Found 'Insured Items' marker at 0x{i:X8}, property starts at 0x{pos:X8}");
                return pos;
            }
        }
        
        Console.WriteLine($"[GameProgressParser] 'Insured Items' not found");
        return -1;
    }

    /// <summary>
    /// Update inventory by replacing the raw array data.
    /// Note: Full inventory editing requires complex StructProperty/ArrayProperty serialization.
    /// For now, this method is a placeholder.
    /// </summary>
    public bool UpdateInventory(List<InventoryItem> items)
    {
        Console.WriteLine("[GameProgressParser] Inventory update not fully implemented yet");
        Console.WriteLine($"[GameProgressParser] Would update {items.Count} items");
        // TODO: Implement full inventory serialization
        return false;
    }

    private bool TryConvertToInt(double value, out int result)
    {
        result = (int)value;
        return true;
    }
}
