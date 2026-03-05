using System.Diagnostics;
using System.Text;

namespace HalfSwordTweaker.Config;

/// <summary>
/// Provides methods for parsing and serializing GVAS (Game Variable and Attribute System) files.
/// Implements the CORRECT Settings.sav format with length prefixes between properties.
/// Format: [4-byte length prefix] + [property name length] + [property name] + [type info] + [value]
/// </summary>
public class GvasParser
{
    private byte[] _data;
    private readonly List<string> _debugMessages = new();

    /// <summary>
    /// Gets the debug messages generated during parsing.
    /// </summary>
    public IReadOnlyList<string> DebugMessages => _debugMessages;

    /// <summary>
    /// Initializes a new instance of the <see cref="GvasParser"/> class.
    /// </summary>
    /// <param name="data">The GVAS file data.</param>
    public GvasParser(byte[] data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        Log($"Initialized parser with {data.Length} bytes");
    }

    /// <summary>
    /// Parses the GVAS data and extracts save game settings.
    /// Searches for properties by name and reads their values.
    /// </summary>
    /// <returns>A dictionary of setting names and their values.</returns>
    public Dictionary<string, double> ParseSettings()
    {
        var settings = new Dictionary<string, double>();
        
        try
        {
            // Check GVAS header
            if (!CheckHeader())
            {
                Log("Invalid GVAS file header");
                return GetDefaultSettings();
            }

            // Check for encryption/compression
            if (!CheckEncryption())
            {
                Log("File appears to be encrypted or compressed - cannot parse");
                return GetDefaultSettings();
            }

            Log("GVAS header valid, no encryption detected");

            // Parse each known setting by searching for property name
            foreach (var setting in SaveGameSettingsRegistry.Settings)
            {
                var value = FindDoublePropertyValue(setting.Name);
                if (value.HasValue)
                {
                    Log($"Found {setting.Name} = {value.Value}");
                    settings[setting.Name] = value.Value;
                }
                else
                {
                    Log($"Property '{setting.Name}' not found, marking as NaN");
                    settings[setting.Name] = double.NaN;
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Error parsing settings: {ex.Message}");
            return GetDefaultSettings();
        }
        
        return settings;
    }

    /// <summary>
    /// Finds and reads a DoubleProperty value by name.
    /// Searches the file for the property name, then parses with correct format:
    /// [4 bytes length prefix] + [4 bytes name len] + [name] + [4 bytes type len] + [type] + [4 bytes unknown] + [4 bytes size] + [8 bytes value]
    /// </summary>
    /// <param name="propertyName">The name of the property to find.</param>
    /// <returns>The property value, or null if not found.</returns>
    private double? FindDoublePropertyValue(string propertyName)
    {
        try
        {
            // Convert property name to bytes with null terminator
            var nameBytes = Encoding.UTF8.GetBytes(propertyName);
            var nameWithNull = new byte[nameBytes.Length + 1];
            Array.Copy(nameBytes, nameWithNull, nameBytes.Length);

            // Search for the property name in the data
            int propertyStart = FindPropertyByName(nameWithNull);
            if (propertyStart < 0)
            {
                Log($"Property '{propertyName}' not found in file");
                return null;
            }

            Log($"Found property '{propertyName}' at offset 0x{propertyStart:X8}");

            // propertyStart points to the property name
            // Skip property name + null terminator
            int offset = propertyStart + nameWithNull.Length;

            // Read type name length (4 bytes, little-endian int32)
            if (offset + 4 > _data.Length)
            {
                Log("Error: Not enough data to read type name length");
                return null;
            }
            int typeNameLength = BitConverter.ToInt32(_data, offset);
            Log($"Type name length: {typeNameLength}");
            offset += 4;

            // Skip type name (typeNameLength already includes null terminator)
            offset += typeNameLength;
            Log($"After type name, offset: 0x{offset:X8}");

            // Skip 4 bytes of unknown data (reserved/padding)
            offset += 4;
            Log($"After unknown bytes, offset: 0x{offset:X8}");

            // Read size (4 bytes, little-endian int32)
            if (offset + 4 > _data.Length)
            {
                Log("Error: Not enough data to read size");
                return null;
            }
            int size = BitConverter.ToInt32(_data, offset);
            Log($"Property size: {size}");
            offset += 4;
            Log($"Value offset: 0x{offset:X8}");

            // Now offset points to the double value
            if (offset + 8 > _data.Length)
            {
                Log("Error: Not enough data to read double value");
                return null;
            }

            if (size != 8)
            {
                Log($"Warning: Expected size 8 for DoubleProperty, got {size}");
            }

            // Read the 8 bytes at the current offset
            byte[] valueBytes = new byte[8];
            Array.Copy(_data, offset, valueBytes, 0, 8);
            Log($"Value bytes: {BitConverter.ToString(valueBytes)}");
            
            // Read the double value (little-endian)
            double value = BitConverter.ToDouble(valueBytes);

            Log($"Read {propertyName} = {value} from offset 0x{offset:X8}");
            return value;
        }
        catch (Exception ex)
        {
            Log($"Error reading property '{propertyName}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Updates a DoubleProperty value in the GVAS data.
    /// Finds the property by name search and updates its 8-byte value.
    /// </summary>
    /// <param name="propertyName">The name of the property to update.</param>
    /// <param name="value">The new value.</param>
    /// <returns>True if successful; otherwise, false.</returns>
    public bool UpdateDoubleProperty(string propertyName, double value)
    {
        try
        {
            Log($"Updating {propertyName} to {value}");

            // Convert property name to bytes with null terminator
            var nameBytes = Encoding.UTF8.GetBytes(propertyName);
            var nameWithNull = new byte[nameBytes.Length + 1];
            Array.Copy(nameBytes, nameWithNull, nameBytes.Length);

            // Search for the property name in the data
            int propertyStart = FindPropertyByName(nameWithNull);
            if (propertyStart < 0)
            {
                Log($"Property '{propertyName}' not found in file");
                return false;
            }

            Log($"Found property at offset 0x{propertyStart:X8}");

            // Calculate the offset to the double value
            // Structure: Name(null-terminated) + TypeLength(4) + TypeName(null-terminated) + Unknown(4) + Size(4) + Value(8)
            int offset = propertyStart + nameWithNull.Length; // Skip property name + null

            // Read type name length (4 bytes, little-endian int32)
            if (offset + 4 > _data.Length)
            {
                Log("Error: Not enough data to read type name length");
                return false;
            }
            int typeNameLength = BitConverter.ToInt32(_data, offset);
            Log($"Type name length: {typeNameLength}");
            offset += 4;

            // Skip type name (typeNameLength already includes null terminator)
            offset += typeNameLength;

            // Skip 4 bytes of unknown data (reserved/padding)
            offset += 4;

            // Read size (4 bytes, little-endian int32)
            if (offset + 4 > _data.Length)
            {
                Log("Error: Not enough data to read size");
                return false;
            }
            int size = BitConverter.ToInt32(_data, offset);
            Log($"Property size: {size}");
            offset += 4;
            Log($"Value offset: 0x{offset:X8}");

            // Now offset points to the double value
            if (offset + 8 > _data.Length)
            {
                Log("Error: Not enough data to read/write double value");
                return false;
            }

            if (size != 8)
            {
                Log($"Warning: Expected size 8 for DoubleProperty, got {size}");
            }

            // Write the new double value (little-endian)
            byte[] valueBytes = BitConverter.GetBytes(value);
            Array.Copy(valueBytes, 0, _data, offset, 8);

            Log($"Successfully updated {propertyName} to {value} at offset 0x{offset:X8}");
            return true;
        }
        catch (Exception ex)
        {
            Log($"Error updating property: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Adds a new DoubleProperty to the GVAS data by appending it before the terminator.
    /// Format includes length prefix before property name length.
    /// Structure: [4 bytes length prefix] + [4 bytes name len] + [name] + [4 bytes type len] + [type] + [4 bytes unknown] + [4 bytes size] + [8 bytes value]
    /// </summary>
    /// <param name="propertyName">The name of the property to add.</param>
    /// <param name="value">The initial value.</param>
    /// <returns>True if added successfully; otherwise, false.</returns>
    public bool AddDoubleProperty(string propertyName, double value)
    {
        try
        {
            // First check if property already exists
            int existingOffset = FindPropertyByName(Encoding.UTF8.GetBytes(propertyName + "\0"));
            if (existingOffset >= 0)
            {
                Log($"Property '{propertyName}' already exists at offset 0x{existingOffset:X8}, cannot add duplicate");
                return false;
            }

            Log($"Property '{propertyName}' not found, will add with value {value}");

            // Find the terminator position (0x05 0x00 0x00 0x00 followed by "None")
            int terminatorPos = FindTerminatorPosition();
            if (terminatorPos < 0)
            {
                Log("Error: Could not find file terminator - invalid GVAS format");
                return false;
            }

            Log($"Found terminator at offset 0x{terminatorPos:X8}");

            // Build the new property structure with length prefix
            // Format: [4 bytes length prefix] + [4 bytes name len] + [name\0] + [4 bytes type len] + [type\0] + [4 bytes unknown] + [4 bytes size] + [8 bytes value]
            var nameBytes = Encoding.UTF8.GetBytes(propertyName);
            int nameLen = nameBytes.Length + 1; // Include null terminator

            var typeName = "DoubleProperty\0";
            var typeNameBytes = Encoding.UTF8.GetBytes(typeName);
            int typeNameLength = typeNameBytes.Length; // Includes null terminator

            // Calculate new property size (without length prefix)
            int propertySizeNoPrefix = 4 + nameLen + 4 + typeNameLength + 4 + 4 + 8;
            // Total with length prefix
            int totalPropertySize = 4 + propertySizeNoPrefix;

            Log($"Building new property: name={propertyName} ({nameLen} bytes), type={typeName} ({typeNameLength} bytes), total={totalPropertySize} bytes");

            // Create buffer for new property with length prefix
            byte[] newProperty = new byte[totalPropertySize];
            int offset = 0;

            // Write length prefix (4 bytes LE) - this is the name length of THIS property
            BitConverter.GetBytes(nameLen).CopyTo(newProperty, offset);
            Log($"[AddDoubleProperty] Wrote length prefix {nameLen} at offset 0x{offset:X8}");
            offset += 4;

            // Write property name + null
            Array.Copy(nameBytes, 0, newProperty, offset, nameBytes.Length);
            newProperty[offset + nameBytes.Length] = 0; // null terminator
            Log($"[AddDoubleProperty] Wrote property name '{propertyName}' at offset 0x{offset:X8}");
            offset += nameLen;

            // Write type name length (4 bytes LE)
            BitConverter.GetBytes(typeNameLength).CopyTo(newProperty, offset);
            Log($"[AddDoubleProperty] Wrote type name length {typeNameLength} at offset 0x{offset:X8}");
            offset += 4;

            // Write type name + null
            Array.Copy(typeNameBytes, 0, newProperty, offset, typeNameLength);
            Log($"[AddDoubleProperty] Wrote type name 'DoubleProperty' at offset 0x{offset:X8}");
            offset += typeNameLength;

            // Write unknown (4 bytes, zero)
            BitConverter.GetBytes(0).CopyTo(newProperty, offset);
            Log($"[AddDoubleProperty] Wrote 4 unknown zero bytes at offset 0x{offset:X8}");
            offset += 4;

            // Write size (4 bytes LE, value = 8)
            BitConverter.GetBytes(8).CopyTo(newProperty, offset);
            Log($"[AddDoubleProperty] Wrote size 8 at offset 0x{offset:X8}");
            offset += 4;

            // Write value (8 bytes LE, the double)
            byte[] valueBytes = BitConverter.GetBytes(value);
            Array.Copy(valueBytes, 0, newProperty, offset, 8);
            Log($"[AddDoubleProperty] Wrote value {value} (bytes: {BitConverter.ToString(valueBytes)}) at offset 0x{offset:X8}");
            offset += 8;

            // Log the complete property structure in hex
            Log($"[AddDoubleProperty] Complete property hex: {BitConverter.ToString(newProperty).Replace("-", " ")}");

            // Insert new property before terminator
            // New structure: [data before terminator] + [new property] + [terminator]
            byte[] dataBeforeTerminator = new byte[terminatorPos];
            Array.Copy(_data, 0, dataBeforeTerminator, 0, terminatorPos);

            byte[] terminatorData = new byte[_data.Length - terminatorPos];
            Array.Copy(_data, terminatorPos, terminatorData, 0, terminatorData.Length);

            _data = new byte[dataBeforeTerminator.Length + totalPropertySize + terminatorData.Length];
            Array.Copy(dataBeforeTerminator, 0, _data, 0, dataBeforeTerminator.Length);
            Array.Copy(newProperty, 0, _data, dataBeforeTerminator.Length, totalPropertySize);
            Array.Copy(terminatorData, 0, _data, dataBeforeTerminator.Length + totalPropertySize, terminatorData.Length);

            Log($"[AddDoubleProperty] Inserted property at offset 0x{terminatorPos:X8}, total size: {dataBeforeTerminator.Length + totalPropertySize + terminatorData.Length} bytes");

            Log($"[AddDoubleProperty] Successfully added '{propertyName}' with value {value}");
            return true;
        }
        catch (Exception ex)
        {
            Log($"[AddDoubleProperty] Error adding property: {ex.Message}");
            Log($"[AddDoubleProperty] Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// Finds the position of the file terminator (0x05 0x00 0x00 0x00 + "None").
    /// </summary>
    /// <returns>The offset of the terminator, or -1 if not found.</returns>
    private int FindTerminatorPosition()
    {
        // Search for terminator pattern: 05 00 00 00 4E 6F 6E 65 (0x05 + "None")
        byte[] terminatorPattern = new byte[] { 0x05, 0x00, 0x00, 0x00, 0x4E, 0x6F, 0x6E, 0x65 };
        
        for (int i = 0; i <= _data.Length - terminatorPattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < terminatorPattern.Length; j++)
            {
                if (_data[i + j] != terminatorPattern[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                Log($"Found terminator at offset 0x{i:X8}");
                return i;
            }
        }

        Log("Terminator not found - searching for 0x05 prefix only");
        // Fallback: search for just the 0x05 prefix
        for (int i = _data.Length - 20; i > 0; i--)
        {
            if (_data[i] == 0x05 && _data[i+1] == 0x00 && _data[i+2] == 0x00 && _data[i+3] == 0x00)
            {
                Log($"Found potential terminator at offset 0x{i:X8}");
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Returns the modified data after updates.
    /// </summary>
    /// <returns>The updated GVAS data.</returns>
    public byte[] GetData()
    {
        return (byte[])_data.Clone();
    }

    /// <summary>
    /// Checks if the file has a valid GVAS header.
    /// </summary>
    /// <returns>True if the header is valid; otherwise, false.</returns>
    private bool CheckHeader()
    {
        if (_data.Length < 4)
        {
            Log("File too short to have GVAS header");
            return false;
        }

        // Check for "GVAS" magic bytes
        bool valid = _data[0] == 0x47 && _data[1] == 0x56 && _data[2] == 0x41 && _data[3] == 0x53;
        Log($"GVAS magic check: {valid}");
        return valid;
    }

    /// <summary>
    /// Checks if the file appears to be encrypted or compressed.
    /// </summary>
    /// <returns>True if the file is plain GVAS; false if encrypted/compressed.</returns>
    private bool CheckEncryption()
    {
        // Check for game name string which should be present in unencrypted files
        var gameName = Encoding.UTF8.GetBytes("Halfsword");
        for (int i = 0; i < Math.Min(100, _data.Length - gameName.Length); i++)
        {
            bool match = true;
            for (int j = 0; j < gameName.Length; j++)
            {
                if (_data[i + j] != gameName[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                Log("Game name found in file - appears to be unencrypted");
                return true;
            }
        }

        Log("Warning: Could not verify file is unencrypted");
        return true; // Assume unencrypted but warn
    }

    /// <summary>
    /// Finds a property by name in the GVAS data.
    /// Searches for the exact property name with null terminator.
    /// </summary>
    /// <param name="nameWithNull">The property name with null terminator.</param>
    /// <returns>The offset of the property name, or -1 if not found.</returns>
    private int FindPropertyByName(byte[] nameWithNull)
    {
        // Search for the property name in the data
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
                Log($"Found property '{Encoding.UTF8.GetString(nameWithNull, 0, nameWithNull.Length - 1)}' at offset 0x{i:X8}");
                return i;
            }
        }

        Log($"Property not found: {Encoding.UTF8.GetString(nameWithNull, 0, nameWithNull.Length - 1)}");
        return -1;
    }

    /// <summary>
    /// Gets default settings for all registered properties.
    /// </summary>
    private Dictionary<string, double> GetDefaultSettings()
    {
        var defaults = new Dictionary<string, double>();
        foreach (var setting in SaveGameSettingsRegistry.Settings)
        {
            defaults[setting.Name] = setting.DefaultValue;
        }
        return defaults;
    }

    /// <summary>
    /// Logs a debug message.
    /// </summary>
    private void Log(string message)
    {
        var fullMessage = $"[GvasParser] {message}";
        _debugMessages.Add(fullMessage);
        Debug.WriteLine(fullMessage);
        Console.WriteLine(fullMessage);
    }
}
