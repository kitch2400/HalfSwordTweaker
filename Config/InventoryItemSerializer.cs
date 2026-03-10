using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HalfSwordTweaker.Config;

/// <summary>
/// JSON model for inventory item export/import.
/// </summary>
public class InventoryItemExport
{
    [JsonPropertyName("itemName")]
    public string ItemName { get; set; } = string.Empty;
    
    [JsonPropertyName("itemType")]
    public string ItemType { get; set; } = "Armor";
    
    [JsonPropertyName("objectPath")]
    public string ObjectPath { get; set; } = string.Empty;
    
    [JsonPropertyName("properties")]
    public Dictionary<string, object> Properties { get; set; } = new();
    
    [JsonPropertyName("armorCore")]
    public string? ArmorCore { get; set; }
    
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("coreRemoved")]
    public bool CoreRemoved { get; set; }
    
    [JsonPropertyName("module1")]
    public int Module1 { get; set; }
    
    [JsonPropertyName("module2")]
    public int Module2 { get; set; }
    
    [JsonPropertyName("module3")]
    public int Module3 { get; set; }
}

/// <summary>
/// GVAS serializer for inventory items. Handles all binary serialization/deserialization.
/// </summary>
public class InventoryItemSerializer
{
    private readonly byte[] _data;
    
    public InventoryItemSerializer(byte[] data)
    {
        _data = data;
    }
    
    /// <summary>
    /// Reads a big-endian 32-bit integer at the specified position.
    /// </summary>
    public int ReadBigEndian32(int pos)
    {
        if (pos + 4 > _data.Length)
            throw new InvalidOperationException($"Read past end of file at position {pos}");
        
        return (_data[pos] << 24) | (_data[pos + 1] << 16) | (_data[pos + 2] << 8) | _data[pos + 3];
    }
    
    /// <summary>
    /// Writes a big-endian 32-bit integer at the specified position.
    /// </summary>
    public void WriteBigEndian32(int pos, int value)
    {
        if (pos + 4 > _data.Length)
            throw new InvalidOperationException($"Write past end of file at position {pos}");
        
        _data[pos] = (byte)((value >> 24) & 0xFF);
        _data[pos + 1] = (byte)((value >> 16) & 0xFF);
        _data[pos + 2] = (byte)((value >> 8) & 0xFF);
        _data[pos + 3] = (byte)(value & 0xFF);
    }
    
    /// <summary>
    /// Reads a little-endian 32-bit integer at the specified position.
    /// </summary>
    public int ReadLittleEndian32(int pos)
    {
        if (pos + 4 > _data.Length)
            throw new InvalidOperationException($"Read past end of file at position {pos}");
        
        return BitConverter.ToInt32(_data, pos);
    }
    
    /// <summary>
    /// Finds an ArrayProperty by searching for its name.
    /// </summary>
    public int FindArrayProperty(string arrayName, int startPos = 0)
    {
        var searchBytes = Encoding.UTF8.GetBytes(arrayName);
        for (int i = startPos; i <= _data.Length - searchBytes.Length; i++)
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
                // Go backwards to find the start of the property name
                int pos = i;
                while (pos > 0 && _data[pos - 1] != 0) pos--;
                return pos;
            }
        }
        return -1;
    }
    
    /// <summary>
    /// Parses inventory items from an ArrayProperty.
    /// </summary>
    public List<InventoryItem> ParseInventoryArray(int arrayOffset)
    {
        try
        {
            Console.WriteLine($"[InventoryItemSerializer] ParseInventoryArray: Starting from offset {arrayOffset}");
            
            int pos = arrayOffset;
            
            // Skip property name
            while (pos < _data.Length && _data[pos] != 0) pos++;
            pos++; // Skip null
            
            // Read type length (4 bytes)
            int typeLen = ReadLittleEndian32(pos);
            pos += 4;
            
            // Skip type name "ArrayProperty\0"
            pos += typeLen;
            
            // Skip unknown (4 bytes) - Let's read it to see what it is
            int unknown1 = ReadLittleEndian32(pos);
            Console.WriteLine($"[InventoryItemSerializer] ParseInventoryArray: unknown1 at {pos} = {unknown1} (0x{unknown1:X8})");
            pos += 4;
            
            // Read element type length (4 bytes)
            int elemTypeLen = ReadLittleEndian32(pos);
            Console.WriteLine($"[InventoryItemSerializer] ParseInventoryArray: elemTypeLen at {pos} = {elemTypeLen}");
            pos += 4;
            
            // Skip element type name "StructProperty\0"
            Console.WriteLine($"[InventoryItemSerializer] ParseInventoryArray: Skipping elem type name of length {elemTypeLen}");
            pos += elemTypeLen;
            
            // Skip unknown (4 bytes, NOT 1 byte!)
            int unknown4 = ReadLittleEndian32(pos);
            Console.WriteLine($"[InventoryItemSerializer] ParseInventoryArray: unknown4 at {pos} = {unknown4}");
            pos += 4;
            
            // Read struct type string length (4 bytes)
            int structTypeStrLen = ReadLittleEndian32(pos);
            Console.WriteLine($"[InventoryItemSerializer] ParseInventoryArray: structTypeStrLen at {pos} = {structTypeStrLen}");
            pos += 4;
            
            // Skip struct type string
            Console.WriteLine($"[InventoryItemSerializer] ParseInventoryArray: Skipping struct type string of length {structTypeStrLen}");
            pos += structTypeStrLen;
            
            // Skip arrayIndex (4 bytes, LITTLE-ENDIAN)
            int arrayIndex = ReadLittleEndian32(pos);
            Console.WriteLine($"[InventoryItemSerializer] ParseInventoryArray: arrayIndex at {pos} = {arrayIndex}");
            pos += 4;
            
            // Skip struct size (4 bytes, LITTLE-ENDIAN)
            int structSize = ReadLittleEndian32(pos);
            Console.WriteLine($"[InventoryItemSerializer] ParseInventoryArray: structSize at {pos} = {structSize}");
            pos += 4;
            
            // Armor items have FIXED size of 1931 bytes
            // Scan for ArmorCore_ properties within reasonable range
            Console.WriteLine($"[InventoryItemSerializer] ParseInventoryArray: Scanning for ArmorCore_ properties...");
            
            var items = new List<InventoryItem>();
            int maxItems = 200; // Reasonable max for armor
            int scanPos = pos; // Items start here (after structSize)
            int arrayEnd = pos + (maxItems * 2000); // Don't scan beyond ~400KB
            
            while (scanPos < arrayEnd && scanPos < _data.Length && items.Count < maxItems)
            {
                // Look for next ArmorCore_ property
                int armorCorePos = FindNextValidArmorCore(scanPos);
                
                if (armorCorePos < 0 || armorCorePos > arrayEnd)
                {
                    Console.WriteLine($"[InventoryItemSerializer] ParseInventoryArray: No more ArmorCore_ found");
                    break;
                }
                
                // Parse this armor item
                try
                {
                    var item = ParseArmorItem(armorCorePos);
                    if (item != null && !string.IsNullOrEmpty(item.ObjectPath))
                    {
                        items.Add(item);
                        Console.WriteLine($"[InventoryItemSerializer] ParseInventoryArray: Parsed armor {items.Count}: {item.ItemName} (ID={item.Id})");
                        // Move to next item (1931 bytes)
                        scanPos = armorCorePos + 1931;
                    }
                    else
                    {
                        Console.WriteLine($"[InventoryItemSerializer] ParseInventoryArray: Invalid armor at {armorCorePos}");
                        scanPos = armorCorePos + 1931;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[InventoryItemSerializer] ParseInventoryArray: Error parsing armor at {armorCorePos}: {ex.Message}");
                    scanPos = armorCorePos + 1931;
                }
            }
            
            Console.WriteLine($"[InventoryItemSerializer] ParseInventoryArray: Successfully parsed {items.Count} armors");
            return items;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InventoryItemSerializer] ParseInventoryArray: EXCEPTION - {ex.Message}");
            return new List<InventoryItem>();
        }
    }
    
    /// <summary>
    /// Parses a single inventory item from a StructProperty.
    /// </summary>
    private InventoryItem? ParseInventoryItemStruct(int structOffset)
    {
        try
        {
            int pos = structOffset;
            
            // Skip property name
            while (pos < _data.Length && _data[pos] != 0) pos++;
            pos++; // Skip null
            
            // Read type length (4 bytes)
            int typeLen = ReadLittleEndian32(pos);
            pos += 4;
            
            // Skip type name "StructProperty\0"
            pos += typeLen;
            
            // Skip unknown (4 bytes)
            pos += 4;
            
            // Skip struct type string length and content
            int structTypeLen = ReadLittleEndian32(pos);
            pos += 4 + structTypeLen;
            
            // Skip array index (4 bytes, LITTLE-ENDIAN)
            int arrayIndex = ReadLittleEndian32(pos);
            pos += 4;
            
            // Skip struct size (4 bytes, LITTLE-ENDIAN)
            int structSize = ReadLittleEndian32(pos);
            pos += 4;
            
            // Skip struct content header (2 null bytes + inner size + struct type string + null bytes + GUID length + GUID + null bytes)
            pos += 2; // 2 null bytes
            pos += 4; // inner size
            while (pos < _data.Length && _data[pos] != 0) pos++; // struct type string
            pos++; // null
            while (pos < _data.Length && _data[pos] == 0) pos++; // null bytes
            int guidLen = ReadLittleEndian32(pos);
            pos += 4 + guidLen + 1; // GUID length + GUID + null
            while (pos < _data.Length && _data[pos] == 0) pos++; // more null bytes
            
            // Now parse nested properties
            var item = new InventoryItem
            {
                ItemType = "Armor"
            };
            
            // Read properties until we hit the next struct or end
            int endPos = structOffset + 2000; // Reasonable max size for an item struct
            while (pos < endPos && pos < _data.Length)
            {
                // Read property name
                int propNameStart = pos;
                while (pos < _data.Length && _data[pos] != 0) pos++;
                if (pos - propNameStart == 0) break; // Empty property name = end of struct
                pos++; // Skip null
                
                string propName = Encoding.UTF8.GetString(_data, propNameStart, pos - propNameStart - 1);
                
                // Read type length
                if (pos + 4 > _data.Length) break;
                int propTypeLen = ReadLittleEndian32(pos);
                pos += 4;
                
                // Read type name
                if (pos + propTypeLen > _data.Length) break;
                string propTypeName = Encoding.UTF8.GetString(_data, pos, propTypeLen);
                pos += propTypeLen;
                
                // Skip unknown (4 bytes)
                if (pos + 4 > _data.Length) break;
                pos += 4;
                
                // Read size
                if (pos + 4 > _data.Length) break;
                int propSize = ReadLittleEndian32(pos);
                pos += 4;
                
                // Skip array index (1 byte)
                if (pos >= _data.Length) break;
                pos += 1;
                
                if (propTypeName.Contains("ObjectProperty") && propSize > 0)
                {
                    // Read object path (null-terminated string)
                    if (pos + propSize > _data.Length) break;
                    item.ObjectPath = Encoding.UTF8.GetString(_data, pos, propSize - 1);
                    item.ItemName = ExtractItemNameFromPath(item.ObjectPath);
                    pos += propSize;
                }
                else if (propTypeName.Contains("IntProperty") && propSize == 4)
                {
                    if (pos + 4 > _data.Length) break;
                    int value = ReadLittleEndian32(pos);
                    pos += 4;
                    item.Properties[propName] = value;
                    
                    if (propName.Contains("ID")) item.Id = value;
                    if (propName.Contains("Module1")) item.Module1 = value;
                    if (propName.Contains("Module2")) item.Module2 = value;
                    if (propName.Contains("Module3")) item.Module3 = value;
                }
                else if (propTypeName.Contains("BoolProperty") && propSize == 1)
                {
                    if (pos >= _data.Length) break;
                    item.CoreRemoved = _data[pos] == 1;
                    pos += 1;
                }
                else
                {
                    // Skip unknown property
                    pos += propSize;
                }
            }
            
            return item;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InventoryItemSerializer] ParseInventoryItemStruct: EXCEPTION - {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Extracts a human-readable item name from the object path.
    /// </summary>
    /// <param name="objectPath">The object path to extract name from</param>
    /// <returns>Human-readable item name</returns>
    private string ExtractItemNameFromPath(string objectPath)
    {
        if (string.IsNullOrEmpty(objectPath))
            return "Unknown Item";
        
        // Extract the last part of the path after the last slash
        int lastSlash = objectPath.LastIndexOf('/');
        int lastDot = objectPath.LastIndexOf('.');
        
        if (lastSlash >= 0 && lastDot > lastSlash)
        {
            return objectPath.Substring(lastSlash + 1, lastDot - lastSlash - 1);
        }
        
        return objectPath;
    }
    
    /// <summary>
    /// Skips a StructProperty and returns the position after it.
    /// </summary>
    public int SkipStructProperty(int startPos)
    {
        try
        {
            int pos = startPos;
            
            // Skip property name
            while (pos < _data.Length && _data[pos] != 0) pos++;
            pos++; // Skip null
            
            // Read type length (4 bytes)
            if (pos + 4 > _data.Length) return startPos;
            int typeLen = ReadLittleEndian32(pos);
            pos += 4;
            
            // Skip type name
            pos += typeLen;
            
            // Skip unknown (4 bytes)
            pos += 4;
            
            // Skip struct type string length and content
            if (pos + 4 > _data.Length) return startPos;
            int structTypeLen = ReadLittleEndian32(pos);
            pos += 4 + structTypeLen;
            
            // Skip array index (4 bytes)
            pos += 4;
            
            // Read struct size (4 bytes, LITTLE-ENDIAN)
            if (pos + 4 > _data.Length) return startPos;
            int structSize = ReadLittleEndian32(pos);
            pos += 4;
            
            // Skip struct content
            pos += structSize;
            
            return pos;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InventoryItemSerializer] SkipStructProperty: EXCEPTION - {ex.Message}");
            return -1;
        }
    }
    
    /// <summary>
    /// Serializes an InventoryItem into GVAS format.
    /// </summary>
    public byte[] SerializeInventoryItem(InventoryItem item)
    {
        try
        {
            Console.WriteLine($"[InventoryItemSerializer] SerializeInventoryItem: Serializing '{item.ItemName}'");
            
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // Generate unique property name with GUID
                string propertyName = $"ArmorPassports_3_{GenerateGuidSuffix()}_0";
                
                // Write property name (null-terminated)
                writer.Write(Encoding.UTF8.GetBytes(propertyName));
                writer.Write((byte)0);
                
                // Write type length (4 bytes) - "StructProperty\0" = 15 bytes
                writer.Write(15);
                
                // Write type name (null-terminated)
                writer.Write(Encoding.UTF8.GetBytes("StructProperty"));
                writer.Write((byte)0);
                
                // Write unknown (4 bytes)
                writer.Write(0);
                
                // Write struct type string length (4 bytes)
                string structTypeString = "/Game/Blueprints/Structure/Passports/Str_Passport_Armor1.Str_Passport_Armor1";
                writer.Write(structTypeString.Length + 1);
                
                // Write struct type string (null-terminated)
                writer.Write(Encoding.UTF8.GetBytes(structTypeString));
                writer.Write((byte)0);
                
                // Write array index (4 bytes)
                writer.Write(0);
                
                // Calculate and write struct size (4 bytes) - placeholder
                int structSizePos = (int)ms.Position;
                writer.Write(0);
                
                // Write struct content
                // 2 null bytes
                writer.Write((byte)0);
                writer.Write((byte)0);
                
                // Write inner size (4 bytes) - placeholder
                int innerSizePos = (int)ms.Position;
                writer.Write(0);
                
                // Write struct type string (null-terminated)
                writer.Write(Encoding.UTF8.GetBytes(structTypeString));
                writer.Write((byte)0);
                
                // Write null bytes padding
                for (int i = 0; i < 4; i++) writer.Write((byte)0);
                
                // Write GUID length (4 bytes)
                string guid = GenerateNewGuid();
                writer.Write(guid.Length + 1);
                
                // Write GUID (null-terminated)
                writer.Write(Encoding.UTF8.GetBytes(guid));
                writer.Write((byte)0);
                
                // Write more null bytes padding
                for (int i = 0; i < 4; i++) writer.Write((byte)0);
                
                // Serialize ObjectProperty for ArmorCore
                SerializeObjectProperty(writer, $"ArmorCore_3_{GenerateGuidSuffix()}_0", item.ObjectPath);
                
                // Serialize other required properties with default values or from item
                SerializeIntProperty(writer, $"ID_54_{GenerateGuidSuffix()}_0", item.Id > 0 ? item.Id : GenerateItemId());
                SerializeBoolProperty(writer, $"CoreRemoved_12_{GenerateGuidSuffix()}_0", item.CoreRemoved);
                SerializeIntProperty(writer, $"Module1_5_{GenerateGuidSuffix()}_0", item.Module1);
                SerializeIntProperty(writer, $"Module2_7_{GenerateGuidSuffix()}_0", item.Module2);
                SerializeIntProperty(writer, $"Module3_9_{GenerateGuidSuffix()}_0", item.Module3);
                
                // Calculate sizes
                byte[] result = ms.ToArray();
                int structSize = result.Length - structSizePos - 4;
                int innerSize = structSize - 2 - 4 - structTypeString.Length - 1 - 4 - 4 - guid.Length - 1 - 4;
                
                // Update size fields (LITTLE-ENDIAN for struct content)
                byte[] structSizeBytes = BitConverter.GetBytes(structSize);
                Array.Copy(structSizeBytes, 0, result, structSizePos, 4);
                
                byte[] innerSizeBytes = BitConverter.GetBytes(innerSize);
                Array.Copy(innerSizeBytes, 0, result, innerSizePos, 4);
                
                return result;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InventoryItemSerializer] SerializeInventoryItem: EXCEPTION - {ex.Message}");
            return Array.Empty<byte>();
        }
    }
    
    /// <summary>
    /// Serializes an ObjectProperty.
    /// </summary>
    private void SerializeObjectProperty(BinaryWriter writer, string propertyName, string objectPath)
    {
        writer.Write(Encoding.UTF8.GetBytes(propertyName));
        writer.Write((byte)0);
        
        writer.Write(15); // "ObjectProperty\0" = 15 bytes
        
        writer.Write(Encoding.UTF8.GetBytes("ObjectProperty"));
        writer.Write((byte)0);
        
        writer.Write(0); // unknown
        
        writer.Write(objectPath.Length + 1); // size (null-terminated)
        
        writer.Write((byte)0); // array index
        
        writer.Write(Encoding.UTF8.GetBytes(objectPath));
        writer.Write((byte)0);
    }
    
    /// <summary>
    /// Serializes an IntProperty.
    /// </summary>
    private void SerializeIntProperty(BinaryWriter writer, string propertyName, int value)
    {
        writer.Write(Encoding.UTF8.GetBytes(propertyName));
        writer.Write((byte)0);
        
        writer.Write(12); // "IntProperty\0" = 12 bytes
        
        writer.Write(Encoding.UTF8.GetBytes("IntProperty"));
        writer.Write((byte)0);
        
        writer.Write(0); // unknown
        writer.Write(4); // size
        
        writer.Write((byte)0); // array index
        writer.Write(value); // value
    }
    
    /// <summary>
    /// Serializes a BoolProperty.
    /// </summary>
    private void SerializeBoolProperty(BinaryWriter writer, string propertyName, bool value)
    {
        writer.Write(Encoding.UTF8.GetBytes(propertyName));
        writer.Write((byte)0);
        
        writer.Write(13); // "BoolProperty\0" = 13 bytes
        
        writer.Write(Encoding.UTF8.GetBytes("BoolProperty"));
        writer.Write((byte)0);
        
        writer.Write(0); // unknown
        writer.Write(1); // size
        
        writer.Write((byte)0); // array index
        writer.Write((byte)(value ? 1 : 0)); // value
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
    /// Exports an inventory item to JSON format.
    /// </summary>
    public static string ExportItemToJson(InventoryItem item)
    {
        var export = new InventoryItemExport
        {
            ItemName = item.ItemName,
            ItemType = item.ItemType,
            ObjectPath = item.ObjectPath,
            Properties = item.Properties,
            ArmorCore = item.ObjectPath,
            Id = item.Id,
            CoreRemoved = item.CoreRemoved,
            Module1 = item.Module1,
            Module2 = item.Module2,
            Module3 = item.Module3
        };
        
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        
        return JsonSerializer.Serialize(export, options);
    }
    
    /// <summary>
    /// Imports an inventory item from JSON format.
    /// </summary>
    public static InventoryItem ImportItemFromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        
        var export = JsonSerializer.Deserialize<InventoryItemExport>(json, options);
        if (export == null)
            throw new InvalidOperationException("Failed to deserialize JSON");
        
        return new InventoryItem
        {
            ItemName = export.ItemName,
            ItemType = export.ItemType,
            ObjectPath = export.ObjectPath,
            Properties = export.Properties,
            Id = export.Id,
            CoreRemoved = export.CoreRemoved,
            Module1 = export.Module1,
            Module2 = export.Module2,
            Module3 = export.Module3
        };
    }

    /// <summary>
    /// Parses weapon items from the WeaponPssports ArrayProperty.
    /// </summary>
    public List<InventoryItem> ParseWeaponPassportsArray(int arrayOffset)
    {
        try
        {
            Console.WriteLine($"[InventoryItemSerializer] ParseWeaponPassportsArray: Starting from offset {arrayOffset}");
            
            int pos = arrayOffset;
            
            // Parse ArrayProperty header
            while (pos < _data.Length && _data[pos] != 0) pos++;
            pos++;
            int typeLen = ReadLittleEndian32(pos);
            pos += 4 + typeLen + 4;
            int elemTypeLen = ReadLittleEndian32(pos);
            pos += 4 + elemTypeLen + 4;
            int structTypeStrLen = ReadLittleEndian32(pos);
            pos += 4 + structTypeStrLen + 4 + 4;
            
            int firstItemStart = pos;
            Console.WriteLine($"[InventoryItemSerializer] ParseWeaponPassportsArray: First weapon item starts at offset {firstItemStart}");
            
            var items = new List<InventoryItem>();
            int maxItems = 100;
            int scanPos = firstItemStart;
            
            while (scanPos < _data.Length && items.Count < maxItems)
            {
                int weaponClassPos = FindNextValidWeaponClass(scanPos);
                if (weaponClassPos < 0) break;
                
                try
                {
                    var item = ParseWeaponItem(weaponClassPos);
                    if (item != null && !string.IsNullOrEmpty(item.ObjectPath) && item.ObjectPath.Contains("/Game/"))
                    {
                        items.Add(item);
                        Console.WriteLine($"[InventoryItemSerializer] ParseWeaponPassportsArray: Parsed weapon {items.Count}: {item.ItemName} (ID={item.Id})");
                        scanPos = weaponClassPos + 200;
                    }
                    else
                    {
                        scanPos = weaponClassPos + 1000;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[InventoryItemSerializer] ParseWeaponPassportsArray: Error: {ex.Message}");
                    scanPos = weaponClassPos + 1000;
                }
            }
            
            Console.WriteLine($"[InventoryItemSerializer] ParseWeaponPassportsArray: Successfully parsed {items.Count} weapons");
            return items;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InventoryItemSerializer] ParseWeaponPassportsArray: EXCEPTION - {ex.Message}");
            return new List<InventoryItem>();
        }
    }
    
    private int FindNextValidWeaponClass(int startPos)
    {
        byte[] searchBytes = Encoding.UTF8.GetBytes("WeaponClass_");
        
        for (int i = startPos; i <= _data.Length - searchBytes.Length; i++)
        {
            if (i > 0 && _data[i - 1] != 0) continue;
            
            bool match = true;
            for (int j = 0; j < searchBytes.Length; j++)
            {
                if (_data[i + j] != searchBytes[j]) { match = false; break; }
            }
            if (!match) continue;
            
            int nameEnd = i + 12;
            while (nameEnd < _data.Length && _data[nameEnd] != 0) nameEnd++;
            if (nameEnd >= _data.Length) continue;
            
            int typeLenPos = nameEnd + 1;
            if (typeLenPos + 4 > _data.Length) continue;
            
            int typeLen = ReadLittleEndian32(typeLenPos);
            if (typeLen != 15) continue;
            
            int typeNamePos = typeLenPos + 4;
            if (typeNamePos + 15 > _data.Length) continue;
            
            string typeName = Encoding.UTF8.GetString(_data, typeNamePos, 15);
            if (typeName != "ObjectProperty\0") continue;
            
            return i;
        }
        return -1;
    }
    
    private InventoryItem ParseWeaponItem(int weaponClassPos)
    {
        var item = new InventoryItem { ItemType = "Weapon" };
        item.ObjectPath = ReadObjectPathValue(weaponClassPos);
        item.ItemName = ExtractItemNameFromPath(item.ObjectPath);
        
        int searchEnd = weaponClassPos + 200000;
        int idPos = FindNextValidID(weaponClassPos + 100, searchEnd);
        if (idPos > 0)
        {
            item.Id = ReadIntPropertyValue(idPos);
        }
        
        return item;
    }
    
    private int FindNextValidID(int startPos, int maxPos)
    {
        byte[] searchBytes = Encoding.UTF8.GetBytes("ID_");
        
        for (int i = startPos; i <= maxPos - searchBytes.Length; i++)
        {
            if (i > 0 && _data[i - 1] != 0) continue;
            
            bool match = true;
            for (int j = 0; j < searchBytes.Length; j++)
            {
                if (_data[i + j] != searchBytes[j]) { match = false; break; }
            }
            if (!match) continue;
            
            int nameEnd = i + 3;
            while (nameEnd < _data.Length && _data[nameEnd] != 0) nameEnd++;
            if (nameEnd >= _data.Length) continue;
            
            int typeLenPos = nameEnd + 1;
            if (typeLenPos + 4 > _data.Length) continue;
            
            int typeLen = ReadLittleEndian32(typeLenPos);
            if (typeLen != 12) continue;
            
            int typeNamePos = typeLenPos + 4;
            if (typeNamePos + 12 > _data.Length) continue;
            
            string typeName = Encoding.UTF8.GetString(_data, typeNamePos, 12);
            if (typeName != "IntProperty\0") continue;
            
            return i;
        }
        return -1;
    }
    
    private string ReadObjectPathValue(int propPos)
    {
        int pos = propPos;
        
        while (pos < _data.Length && _data[pos] != 0) pos++;
        pos++;
        
        int typeLen = ReadLittleEndian32(pos);
        pos += 4 + typeLen + 4;
        
        int size = ReadLittleEndian32(pos);
        pos += 4 + 4;
        
        if (pos < _data.Length && _data[pos] == 0) pos++;
        
        int pathEnd = pos;
        while (pathEnd < pos + size && pathEnd < _data.Length && _data[pathEnd] != 0) pathEnd++;
        
        return pathEnd > pos ? Encoding.UTF8.GetString(_data, pos, pathEnd - pos) : string.Empty;
    }
    
    private int ReadIntPropertyValue(int propPos)
    {
        int pos = propPos;
        while (pos < _data.Length && _data[pos] != 0) pos++;
        pos++;
        pos += 4;
        int typeLen = ReadLittleEndian32(pos - 5);
        pos += typeLen + 4 + 4 + 4;
        return ReadLittleEndian32(pos);
    }
    
    /// <summary>
    /// Finds the next valid ArmorCore_ ObjectProperty.
    /// </summary>
    /// </summary>
    private int FindNextValidArmorCore(int startPos)
    {
        byte[] searchBytes = Encoding.UTF8.GetBytes("ArmorCore_");
        
        for (int i = startPos; i <= _data.Length - searchBytes.Length; i++)
        {
            // Must be preceded by null
            if (i > 0 && _data[i - 1] != 0)
                continue;
            
            // Check for match
            bool match = true;
            for (int j = 0; j < searchBytes.Length; j++)
            {
                if (_data[i + j] != searchBytes[j])
                {
                    match = false;
                    break;
                }
            }
            
            if (!match)
                continue;
            
            // Find end of property name
            int nameEnd = i + 10; // "ArmorCore_" = 10 chars
            while (nameEnd < _data.Length && _data[nameEnd] != 0)
                nameEnd++;
            
            if (nameEnd >= _data.Length)
                continue;
            
            // After name, should be type length
            int typeLenPos = nameEnd + 1;
            if (typeLenPos + 4 > _data.Length)
                continue;
            
            int typeLen = ReadLittleEndian32(typeLenPos);
            
            // Type length should be 15 for "ObjectProperty\0"
            if (typeLen != 15)
                continue;
            
            // Verify type name
            int typeNamePos = typeLenPos + 4;
            if (typeNamePos + 15 > _data.Length)
                continue;
            
            string typeName = Encoding.UTF8.GetString(_data, typeNamePos, 15);
            if (typeName != "ObjectProperty\0")
                continue;
            
            return i;
        }
        
        return -1;
    }
    
    /// <summary>
    /// Parses an armor item starting from ArmorCore_ property.
    /// </summary>
    private InventoryItem ParseArmorItem(int armorCorePos)
    {
        var item = new InventoryItem { ItemType = "Armor" };
        
        // Read ObjectPath from ArmorCore_
        item.ObjectPath = ReadObjectPathValue(armorCorePos);
        item.ItemName = ExtractItemNameFromPath(item.ObjectPath);
        
        // Scan forward for ID_ property (within ~2000 bytes)
        int searchEnd = armorCorePos + 2000;
        int idPos = FindNextValidID(armorCorePos + 100, searchEnd);
        if (idPos > 0)
        {
            item.Id = ReadIntPropertyValue(idPos);
        }
        
        // Scan for CoreRemoved_ BoolProperty
        int coreRemovedPos = FindNextValidBoolProperty(armorCorePos + 100, searchEnd, "CoreRemoved_");
        if (coreRemovedPos > 0)
        {
            item.CoreRemoved = ReadBoolPropertyValue(coreRemovedPos);
        }
        
        // Scan for Module properties
        int module1Pos = FindNextValidID(armorCorePos + 100, searchEnd);
        if (module1Pos > 0 && module1Pos != idPos)
        {
            // This might be Module1, but we need better detection
            // For now, skip
        }
        
        return item;
    }
    
    /// <summary>
    /// Finds the next valid BoolProperty with given name prefix.
    /// </summary>
    private int FindNextValidBoolProperty(int startPos, int maxPos, string propertyName)
    {
        byte[] searchBytes = Encoding.UTF8.GetBytes(propertyName);
        
        for (int i = startPos; i <= maxPos - searchBytes.Length; i++)
        {
            if (i > 0 && _data[i - 1] != 0)
                continue;
            
            bool match = true;
            for (int j = 0; j < searchBytes.Length; j++)
            {
                if (_data[i + j] != searchBytes[j])
                {
                    match = false;
                    break;
                }
            }
            
            if (!match)
                continue;
            
            // Find end of property name
            int nameEnd = i + searchBytes.Length;
            while (nameEnd < _data.Length && _data[nameEnd] != 0)
                nameEnd++;
            
            if (nameEnd >= _data.Length)
                continue;
            
            // After name, should be type length
            int typeLenPos = nameEnd + 1;
            if (typeLenPos + 4 > _data.Length)
                continue;
            
            int typeLen = ReadLittleEndian32(typeLenPos);
            
            // Type length should be 11 for "BoolProperty\0"
            if (typeLen != 11)
                continue;
            
            // Verify type name
            int typeNamePos = typeLenPos + 4;
            if (typeNamePos + 11 > _data.Length)
                continue;
            
            string typeName = Encoding.UTF8.GetString(_data, typeNamePos, 11);
            if (typeName != "BoolProperty\0")
                continue;
            
            return i;
        }
        
        return -1;
    }
    
    /// <summary>
    /// Reads a BoolProperty value.
    /// </summary>
    private bool ReadBoolPropertyValue(int propPos)
    {
        int pos = propPos;
        
        // Skip property name
        while (pos < _data.Length && _data[pos] != 0) pos++;
        pos++; // null
        
        // Skip type length
        pos += 4;
        
        // Skip type name "BoolProperty"
        pos += 11;
        
        // Skip unknown
        pos += 4;
        
        // Skip size
        pos += 4;
        
        // Skip array index
        pos += 4;
        
        // Read bool value
        return pos < _data.Length && _data[pos] == 1;
    }
}
