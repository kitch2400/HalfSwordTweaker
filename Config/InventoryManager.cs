using System.Text;

namespace HalfSwordTweaker.Config;

/// <summary>
/// High-level inventory manager for handling player and insured inventories.
/// </summary>
public class InventoryManager
{
    private readonly string _gameProgressPath;
    private readonly bool _isDevMode;
    
    public string GameProgressPath => _gameProgressPath;
    
    public InventoryManager(string gameProgressPath, bool isDevMode = false)
    {
        _gameProgressPath = gameProgressPath;
        _isDevMode = isDevMode;
    }
    
    /// <summary>
    /// Checks if the save game directory exists.
    /// </summary>
    public bool SaveGameDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_gameProgressPath);
        return !string.IsNullOrEmpty(directory) && Directory.Exists(directory);
    }
    
    /// <summary>
    /// Gets the player inventory items.
    /// </summary>
    public List<InventoryItem>? GetPlayerInventory()
    {
        try
        {
            if (!File.Exists(_gameProgressPath))
            {
                Console.WriteLine($"[InventoryManager] GetPlayerInventory: File not found: {_gameProgressPath}");
                return null;
            }
            
            Console.WriteLine($"[InventoryManager] GetPlayerInventory: Reading file");
            var bytes = File.ReadAllBytes(_gameProgressPath);
            var serializer = new InventoryItemSerializer(bytes);
            
            var allItems = new List<InventoryItem>();
            
            // Parse ArmorPassports
            int playerInventoryOffset = FindPlayerInventoryOffset(bytes);
            if (playerInventoryOffset >= 0)
            {
                Console.WriteLine($"[InventoryManager] GetPlayerInventory: Found Player Inventory at offset {playerInventoryOffset}");
                
                int arrayOffset = FindInventoryArrayProperty(bytes, playerInventoryOffset);
                if (arrayOffset >= 0)
                {
                    Console.WriteLine($"[InventoryManager] GetPlayerInventory: Found ArmorPassports at offset {arrayOffset}");
                    var armorItems = serializer.ParseInventoryArray(arrayOffset);
                    allItems.AddRange(armorItems);
                    Console.WriteLine($"[InventoryManager] GetPlayerInventory: Parsed {armorItems.Count} armor items");
                }
            }
            
            // Parse WeaponPssports
            int weaponOffset = FindWeaponPassportsOffset(bytes);
            if (weaponOffset >= 0)
            {
                var weaponItems = serializer.ParseWeaponPassportsArray(weaponOffset);
                allItems.AddRange(weaponItems);
                Console.WriteLine($"[InventoryManager] GetPlayerInventory: Parsed {weaponItems.Count} weapon items");
            }
            
            Console.WriteLine($"[InventoryManager] GetPlayerInventory: Total items = {allItems.Count}");
            return allItems;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InventoryManager] GetPlayerInventory: EXCEPTION - {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Gets the insured items inventory.
    /// </summary>
    public List<InventoryItem>? GetInsuredItems()
    {
        try
        {
            if (!File.Exists(_gameProgressPath))
            {
                Console.WriteLine($"[InventoryManager] GetInsuredItems: File not found: {_gameProgressPath}");
                return null;
            }
            
            Console.WriteLine($"[InventoryManager] GetInsuredItems: Reading file");
            var bytes = File.ReadAllBytes(_gameProgressPath);
            var serializer = new InventoryItemSerializer(bytes);
            
            // Find Insured Items struct
            int insuredItemsOffset = FindInsuredItemsOffset(bytes);
            if (insuredItemsOffset < 0)
            {
                Console.WriteLine("[InventoryManager] GetInsuredItems: Insured Items not found");
                return null;
            }
            
            Console.WriteLine($"[InventoryManager] GetInsuredItems: Found Insured Items at offset {insuredItemsOffset}");
            
            // Parse inventory array from struct
            int arrayOffset = FindInventoryArrayProperty(bytes, insuredItemsOffset);
            if (arrayOffset < 0)
            {
                Console.WriteLine("[InventoryManager] GetInsuredItems: ArmorPassports ArrayProperty not found");
                return null;
            }
            
            Console.WriteLine($"[InventoryManager] GetInsuredItems: Found ArrayProperty at offset {arrayOffset}");
            return serializer.ParseInventoryArray(arrayOffset);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InventoryManager] GetInsuredItems: EXCEPTION - {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Finds the Player Inventory struct offset in the file.
    /// Validates that it's actually a StructProperty header before returning.
    /// </summary>
    private int FindPlayerInventoryOffset(byte[] data)
    {
        var searchBytes = Encoding.UTF8.GetBytes("Player Inventory");
        for (int i = 0; i <= data.Length - searchBytes.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < searchBytes.Length; j++)
            {
                if (data[i + j] != searchBytes[j])
                {
                    match = false;
                    break;
                }
            }
            
            if (match)
            {
                // "Player Inventory" should be the property name at the StructProperty header start
                // Validate the header structure before returning
                int pos = i + searchBytes.Length; // Position after "Player Inventory"
                
                // Check bounds
                if (pos + 20 > data.Length) continue;
                
                // Verify null terminator
                if (data[pos] != 0) continue;
                pos++;
                
                // Verify type length = 15 (for "StructProperty\0")
                int typeLen = BitConverter.ToInt32(data, pos);
                if (typeLen != 15) continue;
                pos += 4;
                
                // Verify type name "StructProperty\0"
                string typeName = Encoding.UTF8.GetString(data, pos, typeLen);
                if (typeName != "StructProperty\0") continue;
                
                // Valid StructProperty header found
                return i;
            }
        }
        return -1;
    }
    
    /// <summary>
    /// Finds the Insured Items struct offset in the file.
    /// Validates that it's actually a StructProperty header before returning.
    /// </summary>
    private int FindInsuredItemsOffset(byte[] data)
    {
        var searchBytes = Encoding.UTF8.GetBytes("Insured Items");
        for (int i = 0; i <= data.Length - searchBytes.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < searchBytes.Length; j++)
            {
                if (data[i + j] != searchBytes[j])
                {
                    match = false;
                    break;
                }
            }
            
            if (match)
            {
                // "Insured Items" should be the property name at the StructProperty header start
                // Validate the header structure before returning
                int pos = i + searchBytes.Length; // Position after "Insured Items"
                
                // Check bounds
                if (pos + 20 > data.Length) continue;
                
                // Verify null terminator
                if (data[pos] != 0) continue;
                pos++;
                
                // Verify type length = 15 (for "StructProperty\0")
                int typeLen = BitConverter.ToInt32(data, pos);
                if (typeLen != 15) continue;
                pos += 4;
                
                // Verify type name "StructProperty\0"
                string typeName = Encoding.UTF8.GetString(data, pos, typeLen);
                if (typeName != "StructProperty\0") continue;
                
                // Valid StructProperty header found
                return i;
            }
        }
        return -1;
    }
    
    /// <summary>
    /// Finds the WeaponPssports ArrayProperty offset in the file.
    /// Note: "WeaponPssports" appears to be a typo in the save file (missing 'a' in Passports).
    /// Validates that it's actually an ArrayProperty before returning.
    /// </summary>
    private int FindWeaponPassportsOffset(byte[] data)
    {
        var searchBytes = Encoding.UTF8.GetBytes("WeaponPssports");
        for (int i = 0; i <= data.Length - searchBytes.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < searchBytes.Length; j++)
            {
                if (data[i + j] != searchBytes[j])
                {
                    match = false;
                    break;
                }
            }
            
            if (match)
            {
                // "WeaponPssports" should be the property name at the ArrayProperty header start
                // Validate the header structure before returning
                int pos = i + searchBytes.Length; // Position after "WeaponPssports..."
                
                // Find null terminator for property name
                while (pos < data.Length && data[pos] != 0) pos++;
                pos++; // Skip null
                
                // Check bounds
                if (pos + 20 > data.Length) continue;
                
                // Verify type length = 14 (for "ArrayProperty\0")
                int typeLen = BitConverter.ToInt32(data, pos);
                if (typeLen != 14) continue;
                pos += 4;
                
                // Verify type name "ArrayProperty\0"
                string typeName = Encoding.UTF8.GetString(data, pos, typeLen);
                if (typeName != "ArrayProperty\0") continue;
                
                // Valid ArrayProperty header found
                Console.WriteLine($"[InventoryManager] FindWeaponPassportsOffset: Found WeaponPssports at offset {i}");
                return i;
            }
        }
        
        Console.WriteLine("[InventoryManager] FindWeaponPassportsOffset: WeaponPssports not found");
        return -1;
    }
    
    /// <summary>
    /// Finds the ArmorPassports ArrayProperty within a struct.
    /// </summary>
    private int FindInventoryArrayProperty(byte[] data, int structOffset)
    {
        try
        {
            Console.WriteLine($"[InventoryManager] FindInventoryArrayProperty: Searching from offset {structOffset}");
            var serializer = new InventoryItemSerializer(data);
            return serializer.FindArrayProperty("ArmorPassports", structOffset);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InventoryManager] FindInventoryArrayProperty: EXCEPTION - {ex.Message}");
            return -1;
        }
    }
    
    /// <summary>
    /// Insures all items from player inventory that are not already insured.
    /// </summary>
    public bool InsureAllItems()
    {
        try
        {
            Console.WriteLine("[InventoryManager] InsureAllItems: STARTED");
            
            // Get both inventories
            var playerInventory = GetPlayerInventory();
            var insuredInventory = GetInsuredItems();
            
            if (playerInventory == null || playerInventory.Count == 0)
            {
                Console.WriteLine("[InventoryManager] InsureAllItems: Player inventory is empty");
                return false;
            }
            
            if (insuredInventory == null)
            {
                insuredInventory = new List<InventoryItem>();
            }
            
            Console.WriteLine($"[InventoryManager] InsureAllItems: Player has {playerInventory.Count} items, Insured has {insuredInventory.Count} items");
            
            // Create set of insured item paths
            var insuredPaths = new HashSet<string>();
            foreach (var item in insuredInventory)
            {
                if (!string.IsNullOrEmpty(item.ObjectPath))
                    insuredPaths.Add(item.ObjectPath);
            }
            
            // Find items to insure
            var itemsToInsure = new List<InventoryItem>();
            foreach (var item in playerInventory)
            {
                if (!string.IsNullOrEmpty(item.ObjectPath) && !insuredPaths.Contains(item.ObjectPath))
                {
                    itemsToInsure.Add(item);
                }
            }
            
            Console.WriteLine($"[InventoryManager] InsureAllItems: Found {itemsToInsure.Count} items to insure");
            
            if (itemsToInsure.Count == 0)
            {
                Console.WriteLine("[InventoryManager] InsureAllItems: All items already insured");
                return true;
            }
            
            // Read the file
            var bytes = File.ReadAllBytes(_gameProgressPath);
            var serializer = new InventoryItemSerializer(bytes);
            
            // Find Insured Items struct
            int insuredItemsOffset = FindInsuredItemsOffset(bytes);
            if (insuredItemsOffset < 0)
            {
                Console.WriteLine("[InventoryManager] InsureAllItems: Could not find Insured Items struct");
                return false;
            }
            
            // Find ArmorPassports ArrayProperty
            int arrayPropertyOffset = FindInventoryArrayProperty(bytes, insuredItemsOffset);
            if (arrayPropertyOffset < 0)
            {
                Console.WriteLine("[InventoryManager] InsureAllItems: Could not find ArmorPassports ArrayProperty");
                return false;
            }
            
            // Get current array size
            int currentArraySize = GetArrayPropertySize(bytes, arrayPropertyOffset);
            Console.WriteLine($"[InventoryManager] InsureAllItems: Current array size = {currentArraySize}");
            
            if (currentArraySize < 0)
            {
                Console.WriteLine("[InventoryManager] InsureAllItems: Could not get array size");
                return false;
            }
            
            // Serialize new items
            Console.WriteLine($"[InventoryManager] InsureAllItems: Serializing {itemsToInsure.Count} items...");
            var serializedItems = new List<byte[]>();
            foreach (var item in itemsToInsure)
            {
                var serializedItem = serializer.SerializeInventoryItem(item);
                if (serializedItem != null && serializedItem.Length > 0)
                {
                    serializedItems.Add(serializedItem);
                }
            }
            
            Console.WriteLine($"[InventoryManager] InsureAllItems: Successfully serialized {serializedItems.Count} items");
            
            if (serializedItems.Count == 0)
            {
                Console.WriteLine("[InventoryManager] InsureAllItems: No items were serialized");
                return false;
            }
            
            // Find insert position
            int insertPosition = FindEndOfArrayItems(bytes, arrayPropertyOffset);
            Console.WriteLine($"[InventoryManager] InsureAllItems: Insert position = {insertPosition}");
            
            if (insertPosition < 0)
            {
                Console.WriteLine("[InventoryManager] InsureAllItems: Could not find insert position");
                return false;
            }
            
            // Insert items
            Console.WriteLine($"[InventoryManager] InsureAllItems: Inserting {serializedItems.Count} items...");
            bytes = InsertItemsAtPosition(bytes, insertPosition, serializedItems);
            
            // Update array size
            int newArraySize = currentArraySize + serializedItems.Count;
            Console.WriteLine($"[InventoryManager] InsureAllItems: Updating array size to {newArraySize}");
            bytes = UpdateArrayPropertySize(bytes, arrayPropertyOffset, newArraySize);
            
            // Write back to file
            Console.WriteLine("[InventoryManager] InsureAllItems: Writing to file...");
            File.WriteAllBytes(_gameProgressPath, bytes);
            
            Console.WriteLine($"[InventoryManager] InsureAllItems: SUCCESS - Insured {serializedItems.Count} items");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InventoryManager] InsureAllItems: EXCEPTION - {ex.Message}");
            Console.WriteLine($"[InventoryManager] InsureAllItems: Stack trace: {ex.StackTrace}");
            return false;
        }
    }
    
    /// <summary>
    /// Gets the current size of an ArrayProperty.
    /// </summary>
    private int GetArrayPropertySize(byte[] data, int arrayOffset)
    {
        try
        {
            int pos = arrayOffset;
            var serializer = new InventoryItemSerializer(data);
            
            // Skip property name
            while (pos < data.Length && data[pos] != 0) pos++;
            pos++; // Skip null
            
            // Read type length (4 bytes)
            int typeLen = serializer.ReadLittleEndian32(pos);
            pos += 4;
            
            // Skip type name "ArrayProperty\0"
            pos += typeLen;
            
            // Skip unknown (4 bytes, LITTLE-ENDIAN)
            pos += 4;
            
            // Read element type length (4 bytes)
            int elemTypeLen = serializer.ReadLittleEndian32(pos);
            pos += 4;
            
            // Skip element type name "StructProperty\0"
            pos += elemTypeLen;
            
            // Skip struct size (4 bytes, LITTLE-ENDIAN)
            pos += 4;
            
            // NO arraySize field exists! Return -1 to indicate we need dynamic counting
            Console.WriteLine("[InventoryManager] GetArrayPropertySize: ArraySize field does not exist, returning -1");
            return -1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InventoryManager] GetArrayPropertySize: EXCEPTION - {ex.Message}");
            return -1;
        }
    }
    
    /// <summary>
    /// Finds the position where new items should be inserted.
    /// </summary>
    private int FindEndOfArrayItems(byte[] data, int arrayOffset)
    {
        try
        {
            int pos = arrayOffset;
            var serializer = new InventoryItemSerializer(data);
            
            // Skip property name
            while (pos < data.Length && data[pos] != 0) pos++;
            pos++; // Skip null
            
            // Read type length (4 bytes)
            int typeLen = serializer.ReadLittleEndian32(pos);
            pos += 4;
            
            // Skip type name "ArrayProperty\0"
            pos += typeLen;
            
            // Skip unknown (4 bytes, LITTLE-ENDIAN)
            pos += 4;
            
            // Read element type length (4 bytes)
            int elemTypeLen = serializer.ReadLittleEndian32(pos);
            pos += 4;
            
            // Skip element type name "StructProperty\0"
            pos += elemTypeLen;
            
            // Skip arrayIndex (4 bytes, LITTLE-ENDIAN)
            pos += 4;
            
            // Read struct size (4 bytes, LITTLE-ENDIAN)
            int structSize = serializer.ReadLittleEndian32(pos);
            pos += 4;
            
            // NO arraySize field! We need to skip items dynamically until we hit end of struct
            int structEnd = pos + structSize;
            int itemIndex = 0;
            while (pos < structEnd && itemIndex < 1000)
            {
                int nextPos = serializer.SkipStructProperty(pos);
                if (nextPos <= pos || nextPos > structEnd)
                {
                    Console.WriteLine($"[InventoryManager] FindEndOfArrayItems: Reached end at item {itemIndex}");
                    break;
                }
                pos = nextPos;
                itemIndex++;
            }
            
            Console.WriteLine($"[InventoryManager] FindEndOfArrayItems: Found {itemIndex} items, insert position = {pos}");
            return pos;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InventoryManager] FindEndOfArrayItems: EXCEPTION - {ex.Message}");
            return -1;
        }
    }
    
    /// <summary>
    /// Inserts serialized items at the specified position.
    /// </summary>
    private byte[] InsertItemsAtPosition(byte[] data, int position, List<byte[]> items)
    {
        try
        {
            Console.WriteLine($"[InventoryManager] InsertItemsAtPosition: Inserting {items.Count} items at position {position}");
            
            // Calculate total size needed
            int totalSize = 0;
            foreach (var item in items)
            {
                totalSize += item.Length;
            }
            
            // Create new data array
            byte[] newData = new byte[data.Length + totalSize];
            
            // Copy data before insertion point
            Array.Copy(data, 0, newData, 0, position);
            
            // Insert new items
            int currentPos = position;
            foreach (var item in items)
            {
                Array.Copy(item, 0, newData, currentPos, item.Length);
                currentPos += item.Length;
            }
            
            // Copy data after insertion point
            Array.Copy(data, position, newData, currentPos, data.Length - position);
            
            Console.WriteLine($"[InventoryManager] InsertItemsAtPosition: SUCCESS");
            return newData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InventoryManager] InsertItemsAtPosition: EXCEPTION - {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Updates the size field of an ArrayProperty.
    /// </summary>
    private byte[] UpdateArrayPropertySize(byte[] data, int arrayOffset, int newSize)
    {
        try
        {
            Console.WriteLine($"[InventoryManager] UpdateArrayPropertySize: Updating array at offset {arrayOffset} to size {newSize}");
            int pos = arrayOffset;
            var serializer = new InventoryItemSerializer(data);
            
            // Skip property name
            while (pos < data.Length && data[pos] != 0) pos++;
            pos++; // Skip null
            
            // Read type length (4 bytes)
            int typeLen = serializer.ReadLittleEndian32(pos);
            pos += 4;
            
            // Skip type name "ArrayProperty\0"
            pos += typeLen;
            
            // Skip unknown (4 bytes, LITTLE-ENDIAN)
            pos += 4;
            
            // Read element type length (4 bytes)
            int elemTypeLen = serializer.ReadLittleEndian32(pos);
            pos += 4;
            
            // Skip element type name "StructProperty\0"
            pos += elemTypeLen;
            
            // Skip arrayIndex (4 bytes, LITTLE-ENDIAN)
            pos += 4;
            
            // Skip struct size (4 bytes, LITTLE-ENDIAN)
            pos += 4;
            
            // NO arraySize field to update! The array size is implicit (count of items).
            // We don't need to update anything here.
            Console.WriteLine("[InventoryManager] UpdateArrayPropertySize: ArraySize field does not exist, skipping update");
            return data;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InventoryManager] UpdateArrayPropertySize: EXCEPTION - {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Exports an inventory item to a JSON file.
    /// </summary>
    public bool ExportItemToFile(InventoryItem item, string filePath)
    {
        try
        {
            var json = InventoryItemSerializer.ExportItemToJson(item);
            File.WriteAllText(filePath, json);
            Console.WriteLine($"[InventoryManager] ExportItemToFile: Exported to {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InventoryManager] ExportItemToFile: EXCEPTION - {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Imports an inventory item from a JSON file.
    /// </summary>
    public InventoryItem? ImportItemFromFile(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var item = InventoryItemSerializer.ImportItemFromJson(json);
            Console.WriteLine($"[InventoryManager] ImportItemFromFile: Imported from {filePath}");
            return item;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InventoryManager] ImportItemFromFile: EXCEPTION - {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Adds an imported item to the player inventory.
    /// </summary>
    public bool AddItemToPlayerInventory(InventoryItem item)
    {
        try
        {
            Console.WriteLine($"[InventoryManager] AddItemToPlayerInventory: Adding item '{item.ItemName}'");
            
            // Read the file
            var bytes = File.ReadAllBytes(_gameProgressPath);
            var serializer = new InventoryItemSerializer(bytes);
            
            // Find Player Inventory struct
            int playerInventoryOffset = FindPlayerInventoryOffset(bytes);
            if (playerInventoryOffset < 0)
            {
                Console.WriteLine("[InventoryManager] AddItemToPlayerInventory: Could not find Player Inventory struct");
                return false;
            }
            
            // Find ArmorPassports ArrayProperty
            int arrayPropertyOffset = FindInventoryArrayProperty(bytes, playerInventoryOffset);
            if (arrayPropertyOffset < 0)
            {
                Console.WriteLine("[InventoryManager] AddItemToPlayerInventory: Could not find ArmorPassports ArrayProperty");
                return false;
            }
            
            // Get current array size
            int currentArraySize = GetArrayPropertySize(bytes, arrayPropertyOffset);
            if (currentArraySize < 0)
            {
                Console.WriteLine("[InventoryManager] AddItemToPlayerInventory: Could not get array size");
                return false;
            }
            
            // Serialize the new item
            var serializedItem = serializer.SerializeInventoryItem(item);
            if (serializedItem == null || serializedItem.Length == 0)
            {
                Console.WriteLine("[InventoryManager] AddItemToPlayerInventory: Failed to serialize item");
                return false;
            }
            
            // Find insert position (end of array)
            int insertPosition = FindEndOfArrayItems(bytes, arrayPropertyOffset);
            if (insertPosition < 0)
            {
                Console.WriteLine("[InventoryManager] AddItemToPlayerInventory: Could not find insert position");
                return false;
            }
            
            // Insert item
            bytes = InsertItemsAtPosition(bytes, insertPosition, new List<byte[]> { serializedItem });
            
            // Update array size
            int newArraySize = currentArraySize + 1;
            bytes = UpdateArrayPropertySize(bytes, arrayPropertyOffset, newArraySize);
            
            // Write back to file
            File.WriteAllBytes(_gameProgressPath, bytes);
            
            Console.WriteLine($"[InventoryManager] AddItemToPlayerInventory: SUCCESS - Added item");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InventoryManager] AddItemToPlayerInventory: EXCEPTION - {ex.Message}");
            return false;
        }
    }
}
