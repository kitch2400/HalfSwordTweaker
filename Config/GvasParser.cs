using System.Text;

namespace HalfSwordTweaker.Config;

/// <summary>
/// Provides methods for parsing and serializing GVAS (Game Variable and Attribute System) files.
/// </summary>
public class GvasParser
{
    private readonly byte[] _data;

    /// <summary>
    /// Initializes a new instance of the <see cref="GvasParser"/> class.
    /// </summary>
    /// <param name="data">The GVAS file data.</param>
    public GvasParser(byte[] data)
    {
        _data = data;
    }

    /// <summary>
    /// Parses the GVAS data and extracts save game settings.
    /// </summary>
    /// <returns>A dictionary of setting names and their values.</returns>
    public Dictionary<string, double> ParseSettings()
    {
        var settings = new Dictionary<string, double>();
        
        // Check GVAS header
        if (!CheckHeader())
        {
            throw new InvalidOperationException("Invalid GVAS file header");
        }
        
        // Skip to the settings data (simplified approach for Half Sword)
        // In a real implementation, we would parse the full GVAS structure
        // For now, we'll use a simplified approach to find our known settings
        
        try
        {
            // Look for our known settings in the file
            foreach (var setting in SaveGameSettingsRegistry.Settings)
            {
                var value = FindDoublePropertyValue(setting.Name);
                if (value.HasValue)
                {
                    settings[setting.Name] = value.Value;
                }
                else
                {
                    // Use default value if not found
                    settings[setting.Name] = setting.DefaultValue;
                }
            }
        }
        catch
        {
            // If parsing fails, return defaults
            foreach (var setting in SaveGameSettingsRegistry.Settings)
            {
                settings[setting.Name] = setting.DefaultValue;
            }
        }
        
        return settings;
    }

    /// <summary>
    /// Serializes the settings back to GVAS format.
    /// </summary>
    /// <param name="settings">The settings to serialize.</param>
    /// <returns>The updated GVAS data.</returns>
    public byte[] SerializeSettings(Dictionary<string, double> settings)
    {
        // For a minimal implementation, we'll return the original data
        // A full implementation would modify the appropriate sections
        // This is a placeholder for the serialization logic
        return (byte[])_data.Clone();
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
            // Convert property name to bytes
            var nameBytes = Encoding.UTF8.GetBytes(propertyName);
            nameBytes = nameBytes.Concat(new byte[] { 0 }).ToArray(); // Add null terminator
            
            // Search for the property name in the data
            for (int i = 0; i <= _data.Length - nameBytes.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < nameBytes.Length; j++)
                {
                    if (_data[i + j] != nameBytes[j])
                    {
                        match = false;
                        break;
                    }
                }
                
                if (match)
                {
                    // Found the property name, now look for the DoubleProperty marker
                    // and then the 8-byte double value that follows
                    int valueOffset = i + nameBytes.Length + 19; // Offset to the double value
                    
                    if (valueOffset + 8 <= _data.Length)
                    {
                        // Convert the double value to bytes (little-endian)
                        byte[] valueBytes = BitConverter.GetBytes(value);
                        
                        // Update the data
                        Array.Copy(valueBytes, 0, _data, valueOffset, 8);
                        return true;
                    }
                }
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if the file has a valid GVAS header.
    /// </summary>
    /// <returns>True if the header is valid; otherwise, false.</returns>
    private bool CheckHeader()
    {
        if (_data.Length < 4)
            return false;
            
        // Check for "GVAS" magic bytes
        return _data[0] == 0x47 && _data[1] == 0x56 && _data[2] == 0x41 && _data[3] == 0x53;
    }

    /// <summary>
    /// Finds a DoubleProperty value by name in the GVAS data.
    /// </summary>
    /// <param name="propertyName">The name of the property to find.</param>
    /// <returns>The property value, or null if not found.</returns>
    private double? FindDoublePropertyValue(string propertyName)
    {
        // Convert property name to bytes
        var nameBytes = Encoding.UTF8.GetBytes(propertyName);
        nameBytes = nameBytes.Concat(new byte[] { 0 }).ToArray(); // Add null terminator
        
        // Search for the property name in the data
        for (int i = 0; i <= _data.Length - nameBytes.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < nameBytes.Length; j++)
            {
                if (_data[i + j] != nameBytes[j])
                {
                    match = false;
                    break;
                }
            }
            
            if (match)
            {
                // Found the property name, now look for the DoubleProperty marker
                // and then the 8-byte double value that follows
                int valueOffset = i + nameBytes.Length + 19; // Offset to the double value
                
                if (valueOffset + 8 <= _data.Length)
                {
                    // Read the 8-byte double value (little-endian)
                    Span<byte> valueBytes = _data.AsSpan(valueOffset, 8);
                    double value = BitConverter.ToDouble(valueBytes);
                    return value;
                }
            }
        }
        
        return null;
    }
}