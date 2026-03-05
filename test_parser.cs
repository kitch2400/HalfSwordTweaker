using HalfSwordTweaker.Config;
using System.Text;

Console.WriteLine("=== Testing GvasParser with game file ===\n");

// Test 1: Parse game file
Console.WriteLine("Test 1: Parsing Settings.sav.ingameHIGHEST");
byte[] gameData = File.ReadAllBytes("sample_saves/Settings.sav.ingameHIGHEST");
var parser1 = new GvasParser(gameData);
var settings1 = parser1.ParseSettings();

Console.WriteLine("\nParsed settings:");
foreach (var kvp in settings1)
{
    Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
}

// Test 2: Update a value
Console.WriteLine("\n\nTest 2: Updating Sound Volume to 0.5");
byte[] testData = (byte[])gameData.Clone();
var parser2 = new GvasParser(testData);
bool success = parser2.UpdateDoubleProperty("Sound Volume", 0.5);
Console.WriteLine($"Update result: {success}");

// Verify the update
var settings2 = parser2.ParseSettings();
Console.WriteLine($"Sound Volume after update: {settings2["Sound Volume"]}");

// Test 3: Check file structure
Console.WriteLine("\n\nTest 3: Verifying file structure");
Console.WriteLine($"File size: {gameData.Length} bytes");

// Find terminator
int terminatorOffset = -1;
byte[] terminator = { 0x05, 0x00, 0x00, 0x00, 0x4E, 0x6F, 0x6E, 0x65 };
for (int i = gameData.Length - terminator.Length; i >= 0; i--)
{
    bool match = true;
    for (int j = 0; j < terminator.Length; j++)
    {
        if (gameData[i + j] != terminator[j])
        {
            match = false;
            break;
        }
    }
    if (match)
    {
        terminatorOffset = i;
        Console.WriteLine($"Found terminator at offset 0x{i:X8} ({i})");
        break;
    }
}

// Check length prefixes
Console.WriteLine("\nChecking length prefixes:");
string[] propertyNames = {
    "Sound Volume",
    "Music Volume", 
    "Mouse Sensitivity",
    "Blood Rate",
    "Gore Rate",
    "Lock On Strength",
    "Damage to Player",
    "Damage to NPC",
    "Voice Volume"
};

foreach (var propName in propertyNames)
{
    int propOffset = FindPropertyOffset(gameData, propName);
    if (propOffset >= 0)
    {
        int lengthPrefixOffset = propOffset - 4;
        if (lengthPrefixOffset >= 0)
        {
            int lengthPrefix = BitConverter.ToInt32(gameData, lengthPrefixOffset);
            Console.WriteLine($"  {propName}: offset=0x{propOffset:X8}, length_prefix={lengthPrefix}");
        }
    }
}

int FindPropertyOffset(byte[] data, string propertyName)
{
    var nameBytes = Encoding.UTF8.GetBytes(propertyName + "\0");
    for (int i = 0; i <= data.Length - nameBytes.Length; i++)
    {
        bool match = true;
        for (int j = 0; j < nameBytes.Length; j++)
        {
            if (data[i + j] != nameBytes[j])
            {
                match = false;
                break;
            }
        }
        if (match) return i;
    }
    return -1;
}

Console.WriteLine("\n=== All tests completed ===");
