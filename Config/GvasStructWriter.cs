using System.Text;

namespace HalfSwordTweaker.Config;

/// <summary>
/// Writes and updates GVAS properties with support for nested structures.
/// </summary>
public class GvasStructWriter
{
    private byte[] _data;
    private readonly StringBuilder _logBuilder = new();

    public string Logs => _logBuilder.ToString();

    public GvasStructWriter(byte[] data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        Log($"Initialized writer with {_data.Length} bytes");
    }

    /// <summary>
    /// Update a property value by its full name (including parent struct path).
    /// </summary>
    public bool UpdateProperty(string propertyName, object value)
    {
        try
        {
            Log($"Updating {propertyName} to {value}");

            // Check if property is nested in a struct
            if (propertyName.Contains('.'))
            {
                return UpdateNestedProperty(propertyName, value);
            }

            // Global property update
            return UpdateGlobalProperty(propertyName, value);
        }
        catch (Exception ex)
        {
            Log($"Error updating property: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Update a global (non-nested) property.
    /// </summary>
    private bool UpdateGlobalProperty(string propertyName, object value)
    {
        int propertyStart = FindPropertyOffset(propertyName);
        if (propertyStart < 0)
        {
            Log($"Property '{propertyName}' not found");
            return false;
        }

        UpdatePropertyValue(propertyStart, value);
        Log($"Successfully updated {propertyName}");
        return true;
    }

    /// <summary>
    /// Update a nested property within a struct.
    /// </summary>
    private bool UpdateNestedProperty(string fullPropertyName, object value)
    {
        // Split the property path (e.g., "Player Character_0.Height_...")
        var parts = fullPropertyName.Split('.');
        if (parts.Length < 2) return false;

        string structName = parts[0];
        string nestedPropertyName = parts[1];

        Log($"Updating nested property: {structName}.{nestedPropertyName}");

        // Find the struct property
        int structOffset = FindPropertyOffset(structName);
        if (structOffset < 0)
        {
            Log($"Struct '{structName}' not found");
            return false;
        }

        // Parse struct to find nested property offset
        int nestedOffset = FindNestedPropertyInStruct(structOffset, nestedPropertyName);
        if (nestedOffset < 0)
        {
            Log($"Nested property '{nestedPropertyName}' not found in struct");
            return false;
        }

        UpdatePropertyValue(nestedOffset, value);
        Log($"Successfully updated {fullPropertyName}");
        return true;
    }

    /// <summary>
    /// Find the offset of a property by name.
    /// </summary>
    private int FindPropertyOffset(string propertyName)
    {
        var nameBytes = Encoding.UTF8.GetBytes(propertyName);
        var nameWithNull = new byte[nameBytes.Length + 1];
        Array.Copy(nameBytes, nameWithNull, nameBytes.Length);

        // Search backwards from the end (find last occurrence)
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
                    if (typeLength >= 5 && typeLength <= 50 && pos + typeLength + 8 <= _data.Length)
                    {
                        return i;
                    }
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// Find a nested property within a struct by parsing the struct header.
    /// </summary>
    private int FindNestedPropertyInStruct(int structOffset, string propertyName)
    {
        try
        {
            // Skip property name
            int pos = structOffset;
            while (pos < _data.Length && _data[pos] != 0) pos++;
            pos++;

            // Skip type length
            if (pos + 4 > _data.Length) return -1;
            int typeLength = BitConverter.ToInt32(_data, pos);
            pos += 4;

            // Skip type name
            if (pos + typeLength > _data.Length) return -1;
            pos += typeLength;

            // StructProperty: skip 2 unknown bytes
            if (pos + 2 > _data.Length) return -1;
            pos += 2;

            // Read struct type string length
            if (pos + 4 > _data.Length) return -1;
            int structTypeLength = BitConverter.ToInt32(_data, pos);
            pos += 4;

            if (structTypeLength < 0 || structTypeLength > 500 || pos + structTypeLength > _data.Length)
                return -1;
            pos += structTypeLength;

            // Read GUID length
            if (pos + 4 > _data.Length) return -1;
            int guidLength = BitConverter.ToInt32(_data, pos);
            pos += 4;

            if (guidLength < 0 || guidLength > 100 || pos + guidLength > _data.Length)
                return -1;
            pos += guidLength;

            // Read struct size
            if (pos + 4 > _data.Length) return -1;
            int structSize = BitConverter.ToInt32(_data, pos);
            pos += 4;

            // Read array index (1 byte)
            if (pos + 1 > _data.Length) return -1;
            pos++;

            // Now pos points to nested properties
            int structEndPos = pos + structSize;
            return FindNestedPropertyByName(pos, structEndPos, propertyName);
        }
        catch (Exception ex)
        {
            Log($"Error finding nested property: {ex.Message}");
            return -1;
        }
    }

    /// <summary>
    /// Find a nested property by name within struct boundaries.
    /// </summary>
    private int FindNestedPropertyByName(int startPos, int endPos, string propertyName)
    {
        int pos = startPos;

        while (pos < endPos - 10 && pos < _data.Length - 10)
        {
            // Check for end of nested properties
            if (_data[pos] == 0)
            {
                if (pos + 1 < _data.Length && _data[pos + 1] == 0)
                {
                    break; // End marker
                }
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
            if (propName == propertyName)
            {
                Log($"Found nested property '{propertyName}' at offset {nameStart}");
                return nameStart;
            }

            // Skip this nested property
            if (pos + 4 > endPos) break;
            int typeLen = BitConverter.ToInt32(_data, pos);
            pos += 4;

            if (pos + typeLen > endPos) break;
            pos += typeLen;

            // Check property type to determine unknown bytes
            if (pos + 4 > endPos) break;
            
            // Read type name to check if it's a struct
            int typeStart = pos;
            int unknownBytes = 4; // Default for most properties
            if (typeLen > 0 && typeStart + typeLen <= endPos)
            {
                var typeName = Encoding.UTF8.GetString(_data, typeStart, typeLen);
                if (typeName.Contains("StructProperty"))
                {
                    unknownBytes = 2; // StructProperty uses 2 unknown bytes
                }
            }
            
            pos += unknownBytes;

            if (pos + 4 > endPos) break;
            int size = BitConverter.ToInt32(_data, pos);
            pos += 4;

            if (pos + 1 > endPos) break;
            pos++; // Skip array index

            if (size < 0 || pos + size > endPos) break;
            pos += size; // Skip value
        }

        Log($"Property '{propertyName}' not found in struct");
        return -1;
    }

    /// <summary>
    /// Update property value at given offset.
    /// </summary>
    private void UpdatePropertyValue(int propertyOffset, object value)
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

        // Determine unknown bytes based on property type
        int unknownBytes = 4; // Default
        if (typeLen > 0 && pos - typeLen >= 0)
        {
            var typeName = Encoding.UTF8.GetString(_data, pos - typeLen, typeLen);
            if (typeName.Contains("StructProperty"))
            {
                unknownBytes = 2;
            }
        }
        pos += unknownBytes;

        // Skip size field (4 bytes)
        pos += 4;

        // Skip array index (1 byte)
        pos += 1;

        // Write new value based on type
        if (value is int intValue)
        {
            byte[] valueBytes = BitConverter.GetBytes(intValue);
            Array.Copy(valueBytes, 0, _data, pos, 4);
        }
        else if (value is double doubleValue)
        {
            byte[] valueBytes = BitConverter.GetBytes(doubleValue);
            Array.Copy(valueBytes, 0, _data, pos, 8);
        }
        else if (value is bool boolValue)
        {
            _data[pos] = boolValue ? (byte)1 : (byte)0;
        }
        else if (value is byte byteValue)
        {
            _data[pos] = byteValue;
        }
        else if (value is float floatValue)
        {
            byte[] valueBytes = BitConverter.GetBytes(floatValue);
            Array.Copy(valueBytes, 0, _data, pos, 4);
        }
    }

    public byte[] GetData() => (byte[])_data.Clone();

    private void Log(string message)
    {
        var fullMessage = $"[GvasStructWriter] {message}";
        _logBuilder.AppendLine(fullMessage);
        Console.WriteLine(fullMessage);
    }
}
