using System.Diagnostics;
using System.Text;

namespace HalfSwordTweaker.Config;

/// <summary>
/// Provides methods for parsing and serializing GVAS (Game Variable and Attribute System) files.
/// Currently supports only DoubleProperty values.
/// NOTE: Future work needed to support other property types (BoolProperty, IntProperty, StructProperty, etc.)
/// </summary>
public class GvasParser
{
    private readonly byte[] _data;
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

            // Parse each known setting
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
                    Log($"Property '{setting.Name}' not found, using default");
                    settings[setting.Name] = setting.DefaultValue;
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
    /// Updates a DoubleProperty value in the GVAS data.
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
            // Last byte is already 0 (null terminator)

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

            // Skip type name + null terminator
            offset += typeNameLength + 1;

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
        // GVAS files have specific patterns in the header
        // After the "GVAS" magic (4 bytes), there should be version info
        // A heavily encrypted file would have random-looking data
        
        // Check for common encryption/compression signatures
        // This is a simplified check - a full implementation would check more thoroughly
        
        // If bytes after "GVAS" look too random (high entropy), it might be encrypted
        // For now, we'll do a simple check: look for the game name string which should be present
        
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
    /// Finds a DoubleProperty value by name in the GVAS data.
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

            // Find the property
            int propertyStart = FindPropertyByName(nameWithNull);
            if (propertyStart < 0)
            {
                return null;
            }

            int offset = propertyStart + nameWithNull.Length; // Skip property name + null

            // Read type name length (4 bytes, little-endian int32)
            if (offset + 4 > _data.Length)
            {
                Log("Error: Not enough data to read type name length");
                return null;
            }
            int typeNameLength = BitConverter.ToInt32(_data, offset);
            Log($"Type name length: {typeNameLength}");
            offset += 4;

            // Skip type name + null terminator (typeNameLength + 1)
            offset += typeNameLength + 1;
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