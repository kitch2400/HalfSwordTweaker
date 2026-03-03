using System.Text;

namespace HalfSwordTweaker.Config;

/// <summary>
/// Represents a parsed GVAS property with full type information.
/// </summary>
public class GvasProperty
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public object? Value { get; set; }
    public string? StructType { get; set; }
    public byte[]? Guid { get; set; }
    public List<GvasProperty> NestedProperties { get; set; } = new();
}

/// <summary>
/// Comprehensive GVAS parser based on GvasViewer implementation.
/// </summary>
public class GvasStructParser
{
    private readonly byte[] _data;
    private readonly StringBuilder _logBuilder = new();

    public string Logs => _logBuilder.ToString();

    public GvasStructParser(byte[] data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        Log($"Initialized parser with {_data.Length} bytes");
    }

    /// <summary>
    /// Parse all root-level properties from GVAS data.
    /// </summary>
    public List<GvasProperty> ParseAllProperties()
    {
        var properties = new List<GvasProperty>();
        
        try
        {
            // Use BinaryReader for easier reading
            using var reader = new BinaryReader(new MemoryStream(_data));
            
            // Read GVAS header
            var header = Encoding.UTF8.GetString(reader.ReadBytes(4));
            if (header != "GVAS")
            {
                Log($"Invalid GVAS header: {header}");
                return properties;
            }
            Log($"GVAS header verified");

            // Read version
            var version = reader.ReadUInt32();
            Log($"Version: {version}");

            // Skip engine version info (14 bytes)
            reader.BaseStream.Position += 14;
            
            // If version == 3, skip 4 more bytes
            if (version == 3)
            {
                reader.BaseStream.Position += 4;
            }

            // Read save game type name
            var saveGameType = ReadString(reader);
            Log($"Save game type: {saveGameType}");

            // Skip 4 bytes
            reader.BaseStream.Position += 4;

            // Read GUID count
            uint guidCount = reader.ReadUInt32();
            Log($"GUID count: {guidCount}");

            // Skip GUIDs
            for (uint i = 0; i < guidCount; i++)
            {
                reader.ReadBytes(16);
                reader.ReadInt32();
            }

            // Read properties section header
            ReadString(reader);

            Log($"Starting property parsing at offset 0x{reader.BaseStream.Position:X8}");

            // Parse properties until "None"
            int propertyCount = 0;
            while (reader.BaseStream.Position < reader.BaseStream.Length - 10)
            {
                var property = ReadProperty(reader);
                if (property == null)
                {
                    Log("Failed to read property, stopping");
                    break;
                }

                // Check for "None" property (end marker)
                if (property.Name == "None")
                {
                    Log("Found 'None' property - end of properties");
                    break;
                }

                properties.Add(property);
                propertyCount++;
                if (propertyCount % 10 == 0)
                {
                    Log($"Parsed {propertyCount} properties, offset: 0x{reader.BaseStream.Position:X8}");
                }
            }

            Log($"Total properties parsed: {propertyCount}");
        }
        catch (Exception ex)
        {
            Log($"Error parsing properties: {ex.Message}");
            Log($"Stack: {ex.StackTrace}");
        }

        return properties;
    }

    /// <summary>
    /// Read a length-prefixed string.
    /// </summary>
    private string ReadString(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length <= 0)
        {
            throw new ArgumentException($"Invalid string length: {length}");
        }

        var buffer = reader.ReadBytes(length - 1);
        reader.ReadByte(); // Skip null terminator
        return Encoding.UTF8.GetString(buffer);
    }

    /// <summary>
    /// Read a nested property within a struct.
    /// Nested properties use a simpler format: Name + Type + Size (Int32) + Unknown (1 byte) + ArrayIndex (1 byte) + Value
    /// </summary>
    private GvasProperty? ReadNestedProperty(BinaryReader reader)
    {
        try
        {
            var property = new GvasProperty();

            // Read nested property name (simpler format - just null-terminated string)
            var nameBytes = new List<byte>();
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                byte b = reader.ReadByte();
                if (b == 0) break;
                nameBytes.Add(b);
            }
            property.Name = Encoding.UTF8.GetString(nameBytes.ToArray());
            
            if (string.IsNullOrEmpty(property.Name))
            {
                Log($"ReadNestedProperty: Empty property name at pos 0x{reader.BaseStream.Position:X8}");
                return null;
            }
            
            Log($"ReadNestedProperty: {property.Name}");

            // Read type length (Int32)
            int typeLen = reader.ReadInt32();
            if (typeLen <= 0 || typeLen > 100)
            {
                Log($"ReadNestedProperty: Invalid type length {typeLen} for {property.Name}");
                return null;
            }
            
            // Read type name
            var typeBytes = reader.ReadBytes(typeLen - 1);
            reader.ReadByte(); // Skip null terminator
            property.Type = Encoding.UTF8.GetString(typeBytes);
            Log($"ReadNestedProperty type: {property.Type}");

            // Read size (Int32 for nested properties)
            int size = reader.ReadInt32();
            Log($"ReadNestedProperty size: {size}");

            // Read unknown byte (1 byte)
            reader.ReadByte();

            // Read array index (1 byte) - for nested properties only
            reader.ReadByte();

            // Read value based on type
            switch (property.Type)
            {
                case "BoolProperty":
                    property.Value = reader.ReadBoolean();
                    Log($"BoolProperty: {property.Value}");
                    break;
                case "ByteProperty":
                    // ByteProperty has an extra string before the value
                    var propNameBytes = new List<byte>();
                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        byte b = reader.ReadByte();
                        if (b == 0) break;
                        propNameBytes.Add(b);
                    }
                    reader.ReadByte(); // Skip null
                    var propName = Encoding.UTF8.GetString(propNameBytes.ToArray());
                    Log($"ByteProperty inner name: {propName}");
                    property.Value = reader.ReadByte();
                    Log($"ByteProperty: {property.Value}");
                    break;
                case "IntProperty":
                    property.Value = reader.ReadInt32();
                    Log($"IntProperty: {property.Value}");
                    break;
                case "DoubleProperty":
                    property.Value = reader.ReadDouble();
                    Log($"DoubleProperty: {property.Value}");
                    break;
                case "FloatProperty":
                    property.Value = reader.ReadSingle();
                    Log($"FloatProperty: {property.Value}");
                    break;
                case "StrProperty":
                    // Read string length (Int32)
                    int strLen = reader.ReadInt32();
                    if (strLen > 0 && strLen < 1000)
                    {
                        var strBytes = reader.ReadBytes(strLen - 1);
                        reader.ReadByte(); // Skip null
                        property.Value = Encoding.UTF8.GetString(strBytes);
                    }
                    Log($"StrProperty: {property.Value}");
                    break;
                case "NameProperty":
                    // Read name length (Int32)
                    int nameLen = reader.ReadInt32();
                    if (nameLen > 0 && nameLen < 100)
                    {
                        var nameBytes2 = reader.ReadBytes(nameLen - 1);
                        reader.ReadByte(); // Skip null
                        property.Value = Encoding.UTF8.GetString(nameBytes2);
                    }
                    Log($"NameProperty: {property.Value}");
                    break;
                case "StructProperty":
                    // Read struct type, GUID, and nested properties
                    var structTypeBytes = new List<byte>();
                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        byte b = reader.ReadByte();
                        if (b == 0) break;
                        structTypeBytes.Add(b);
                    }
                    reader.ReadByte(); // Skip null
                    property.StructType = Encoding.UTF8.GetString(structTypeBytes.ToArray());
                    property.Guid = reader.ReadBytes(16);
                    reader.ReadByte(); // Unknown
                    
                    Log($"StructProperty nested: {property.StructType}");
                    
                    // Read nested properties within this struct
                    int nestedStart = (int)reader.BaseStream.Position;
                    int nestedEnd = nestedStart + size - 20; // Approximate
                    ReadNestedProperties(reader, property, nestedEnd);
                    break;
                case "ObjectProperty":
                    // Read object reference as string
                    int objLen = reader.ReadInt32();
                    if (objLen > 0 && objLen < 1000)
                    {
                        var objBytes = reader.ReadBytes(objLen - 1);
                        reader.ReadByte(); // Skip null
                        property.Value = Encoding.UTF8.GetString(objBytes);
                    }
                    Log($"ObjectProperty: {property.Value}");
                    break;
                case "ArrayProperty":
                    // Skip array for now
                    Log($"ArrayProperty: skipping {size} bytes");
                    reader.BaseStream.Position += size;
                    break;
                case "MapProperty":
                    // Skip map for now
                    Log($"MapProperty: skipping {size} bytes");
                    reader.BaseStream.Position += size;
                    break;
                default:
                    Log($"Unknown nested property type: {property.Type}, skipping {size} bytes");
                    reader.BaseStream.Position += size;
                    break;
            }

            return property;
        }
        catch (Exception ex)
        {
            Log($"Error reading nested property: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Read a property.
    /// Format: Name (length-prefixed) + Type (length-prefixed) + Size (UInt64) + Unknown (1 byte) + Value
    /// </summary>
    private GvasProperty? ReadProperty(BinaryReader reader)
    {
        try
        {
            var property = new GvasProperty();

            // Read property name
            property.Name = ReadString(reader);
            Log($"Reading property: {property.Name}");

            // Read property type
            property.Type = ReadString(reader);
            Log($"Type: {property.Type}");

            // Read size (UInt64 - 8 bytes)
            var size = reader.ReadUInt64();
            Log($"Size: {size}");

            // Read unknown byte (1 byte)
            var unknown = reader.ReadByte();
            Log($"Unknown: 0x{unknown:X2}");

            // Read based on type
            switch (property.Type)
            {
                case "BoolProperty":
                    property.Value = reader.ReadBoolean();
                    Log($"BoolProperty: {property.Value}");
                    break;
                case "ByteProperty":
                    // ByteProperty has an extra string before the value
                    var propName = ReadString(reader);
                    Log($"ByteProperty inner name: {propName}");
                    property.Value = reader.ReadByte();
                    Log($"ByteProperty: {property.Value}");
                    break;
                case "IntProperty":
                    property.Value = reader.ReadInt32();
                    Log($"IntProperty: {property.Value}");
                    break;
                case "DoubleProperty":
                    property.Value = reader.ReadDouble();
                    Log($"DoubleProperty: {property.Value}");
                    break;
                case "FloatProperty":
                    property.Value = reader.ReadSingle();
                    Log($"FloatProperty: {property.Value}");
                    break;
                case "StrProperty":
                    property.Value = ReadString(reader);
                    Log($"StrProperty: {property.Value}");
                    break;
                case "NameProperty":
                    property.Value = ReadString(reader);
                    Log($"NameProperty: {property.Value}");
                    break;
                case "StructProperty":
                    ReadStructProperty(reader, property, (int)size);
                    break;
                case "ArrayProperty":
                    ReadArrayProperty(reader, property);
                    break;
                case "MapProperty":
                    ReadMapProperty(reader, property);
                    break;
                case "ObjectProperty":
                    ReadObjectProperty(reader, property);
                    break;
                case "NoneProperty":
                    // End marker
                    break;
                default:
                    Log($"Unknown property type: {property.Type}, skipping {size} bytes");
                    // Try to skip it
                    reader.BaseStream.Position += (long)size;
                    break;
            }

            return property;
        }
        catch (Exception ex)
        {
            Log($"Error reading property: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Read BoolProperty.
    /// </summary>
    private void ReadBoolProperty(BinaryReader reader, GvasProperty property)
    {
        var size = reader.ReadUInt64(); // 8 bytes
        property.Value = reader.ReadBoolean();
        reader.ReadByte(); // ???
        Log($"BoolProperty: {property.Value}");
    }

    /// <summary>
    /// Read ByteProperty.
    /// </summary>
    private void ReadByteProperty(BinaryReader reader, GvasProperty property)
    {
        var size = reader.ReadUInt64(); // 8 bytes
        var propName = ReadString(reader);
        reader.ReadByte(); // ???
        property.Value = reader.ReadByte();
        Log($"ByteProperty: {property.Value}");
    }



    /// <summary>
    /// Read DoubleProperty.
    /// </summary>
    private void ReadDoubleProperty(BinaryReader reader, GvasProperty property)
    {
        var size = reader.ReadUInt64(); // 8 bytes
        reader.ReadByte(); // ???
        property.Value = reader.ReadDouble();
        Log($"DoubleProperty: {property.Value}");
    }

    /// <summary>
    /// Read FloatProperty.
    /// </summary>
    private void ReadFloatProperty(BinaryReader reader, GvasProperty property)
    {
        var size = reader.ReadUInt64(); // 8 bytes
        reader.ReadByte(); // ???
        property.Value = reader.ReadSingle();
        Log($"FloatProperty: {property.Value}");
    }

    /// <summary>
    /// Read StrProperty.
    /// </summary>
    private void ReadStrProperty(BinaryReader reader, GvasProperty property)
    {
        var size = reader.ReadUInt64(); // 8 bytes
        reader.ReadByte(); // ???
        property.Value = ReadString(reader);
        Log($"StrProperty: {property.Value}");
    }

    /// <summary>
    /// Read NameProperty.
    /// </summary>
    private void ReadNameProperty(BinaryReader reader, GvasProperty property)
    {
        var size = reader.ReadUInt64(); // 8 bytes
        reader.ReadByte(); // ???
        property.Value = ReadString(reader);
        Log($"NameProperty: {property.Value}");
    }

    /// <summary>
    /// Read StructProperty.
    /// Size and unknown byte already read by ReadProperty.
    /// </summary>
    private void ReadStructProperty(BinaryReader reader, GvasProperty property, int size)
    {
        property.StructType = ReadString(reader);
        property.Guid = reader.ReadBytes(16);
        reader.ReadByte(); // ???

        Log($"StructProperty: {property.StructType}, size: {size}");

        // Read nested properties until we hit the struct size limit
        int startPos = (int)reader.BaseStream.Position;
        int endPos = startPos + size;
        Log($"StructProperty nested range: 0x{startPos:X8} to 0x{endPos:X8}");
        ReadNestedProperties(reader, property, endPos);
        Log($"StructProperty finished, final position: 0x{reader.BaseStream.Position:X8}");
    }

    /// <summary>
    /// Read nested properties within a struct.
    /// Nested properties use a simpler format without the UInt64 size field.
    /// </summary>
    private void ReadNestedProperties(BinaryReader reader, GvasProperty parent, int endPos)
    {
        int startPos = (int)reader.BaseStream.Position;
        int propCount = 0;
        
        while (reader.BaseStream.Position < endPos - 10)
        {
            var nestedProp = ReadNestedProperty(reader);
            if (nestedProp == null) 
            {
                Log($"ReadNestedProperties: ReadNestedProperty returned null at pos 0x{reader.BaseStream.Position:X8}");
                break;
            }

            // Check for "None" property (end marker)
            if (nestedProp.Name == "None")
            {
                Log($"ReadNestedProperties: Found 'None' marker after {propCount} properties");
                break;
            }

            parent.NestedProperties.Add(nestedProp);
            propCount++;
            if (propCount % 5 == 0)
            {
                Log($"ReadNestedProperties: {propCount} properties, pos 0x{reader.BaseStream.Position:X8}, endPos 0x{endPos:X8}");
            }
        }
        
        Log($"ReadNestedProperties finished: {propCount} total nested properties");
    }

    /// <summary>
    /// Read ArrayProperty.
    /// </summary>
    private void ReadArrayProperty(BinaryReader reader, GvasProperty property)
    {
        var elemType = ReadString(reader);
        var size = reader.ReadInt32();
        reader.ReadByte(); // ???

        Log($"ArrayProperty: {elemType}, size: {size}");

        // For now, skip array elements
        // TODO: Implement proper array element reading
    }

    /// <summary>
    /// Read MapProperty.
    /// </summary>
    private void ReadMapProperty(BinaryReader reader, GvasProperty property)
    {
        var keyType = ReadString(reader);
        var valueType = ReadString(reader);
        var size = reader.ReadInt32();
        reader.ReadByte(); // ???

        Log($"MapProperty: {keyType} -> {valueType}, size: {size}");

        // For now, skip map entries
        // TODO: Implement proper map entry reading
    }

    /// <summary>
    /// Read ObjectProperty.
    /// </summary>
    private void ReadObjectProperty(BinaryReader reader, GvasProperty property)
    {
        var size = reader.ReadUInt64(); // 8 bytes
        reader.ReadByte(); // ???
        property.Value = ReadString(reader);
        Log($"ObjectProperty: {property.Value}");
    }

    /// <summary>
    /// Skip an unknown property.
    /// </summary>
    private void SkipProperty(BinaryReader reader)
    {
        var size = reader.ReadUInt64(); // 8 bytes
        reader.ReadByte(); // ???
        
        // Skip the value bytes
        if (size > 0 && size < 10000)
        {
            reader.BaseStream.Position += (long)size;
        }
    }

    private void Log(string message)
    {
        // Disabled - too verbose
        // var fullMessage = $"[GvasStructParser] {message}";
        // _logBuilder.AppendLine(fullMessage);
        // Console.WriteLine(fullMessage);
    }
}
