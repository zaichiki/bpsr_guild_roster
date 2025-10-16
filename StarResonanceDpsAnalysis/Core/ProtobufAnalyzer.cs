using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml.Spreadsheet;

namespace StarResonanceDpsAnalysis.Core
{
    /// <summary>
    /// Protobuf binary data analyzer for unknown message structures
    /// </summary>
    public class ProtobufAnalyzer
    {
        /// <summary>
        /// Analyze unknown protobuf data and provide detailed breakdown
        /// </summary>
        /// <param name="data">Raw protobuf binary data</param>
        /// <param name="context">Context description for logging</param>
        public static void AnalyzeUnknownData(byte[] data, string context = "")
        {
            AnalyzeProtobufFields(data);
        }

        /// <summary>
        /// Analyze protobuf fields in the data
        /// </summary>
        private static void AnalyzeProtobufFields(byte[] data)
        {

            try
            {
                var result = Blueprotobuf.Decode(data);
                Console.WriteLine(result.Count);
            } catch (Exception ex)
            {
                Console.WriteLine($" Error trying Blueprotobuf.Decode: {ex.Message}");
            }

            Dictionary<int, GuildMemberActivityData> activityData = new Dictionary<int, GuildMemberActivityData>();
            Dictionary<int, GuildMemberData> memberData = new Dictionary<int, GuildMemberData>();

            try
            {
                var stream = new MemoryStream(data);
                var reader = new BinaryReader(stream);

                int fieldCount = 0;
                while (stream.Position < stream.Length && fieldCount < 50) // Limit to prevent infinite loops
                {
                    long fieldStart = stream.Position;

                    // Debug: Show current position
                    if (fieldCount < 5) // Only show first few for debugging
                    {
                        Console.WriteLine($"  [DEBUG] Position: {stream.Position}/{stream.Length}");
                    }

                    // Read the tag (varint)
                    var tag = ReadVarint(reader);
                    if (tag == 0)
                    {
                        Console.WriteLine("End of message reached");
                        break;
                    }

                    var fieldNumber = tag >> 3;
                    var wireType = tag & 0x07;

                    // Debug: Show the raw tag value
                    if (fieldCount < 5)
                    {
                        Console.WriteLine($"  [DEBUG] Raw tag: 0x{tag:X} (field {fieldNumber}, wire type {wireType})");
                    }

                    Console.WriteLine($"Field {fieldNumber}, Wire Type {wireType} (at offset {fieldStart:X4})");

                    try
                    {
                        switch (wireType)
                        {
                            case 0: // Varint
                                var varintValue = ReadVarint(reader);
                                Console.WriteLine($"  Varint: {varintValue} (0x{varintValue:X})");
                                break;

                            case 1: // 64-bit
                                if (stream.Position + 8 <= stream.Length)
                                {
                                    var fixed64 = reader.ReadUInt64();
                                    Console.WriteLine($"  64-bit: {fixed64} (0x{fixed64:X16})");
                                }
                                else
                                {
                                    Console.WriteLine("  64-bit: Insufficient data");
                                    return;
                                }
                                break;

                            case 2: // Length-delimited
                                var length = ReadVarint(reader);
                                Console.WriteLine($"  Length-delimited: {length} bytes");

                                // Debug: Show length parsing
                                if (fieldCount < 3)
                                {
                                    Console.WriteLine($"  [DEBUG] Length varint: 0x{length:X} = {length}");
                                }

                                if (length > 0 && stream.Position + (long)length <= stream.Length)
                                {
                                    var bytes = reader.ReadBytes((int)length);
                                    Console.WriteLine($"  Data: {BitConverter.ToString(bytes.Take(32).ToArray()).Replace("-", " ")}");

                                    // Try to interpret as string
                                    if (IsValidUtf8(bytes))
                                    {
                                        var str = Encoding.UTF8.GetString(bytes);
                                        if (str.All(c => char.IsControl(c) || char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c)))
                                        {
                                            Console.WriteLine($"  As string: \"{str}\"");
                                        }
                                    }

                                    // Try to parse as nested message if reasonable size
                                    if (length > 2 && length < 10000) // Increased limit for larger messages
                                    {
                                        Console.WriteLine("  Attempting nested message analysis:");
                                        var nestedData = bytes;
                                        var nestedStream = new MemoryStream(nestedData);
                                        var nestedReader = new BinaryReader(nestedStream);

                                        int nestedFields = 0;
                                        while (nestedStream.Position < nestedStream.Length && nestedFields < 20) // Increased limit
                                        {
                                            try
                                            {
                                                var nestedTag = ReadVarint(nestedReader);
                                                if (nestedTag == 0) break;

                                                var nestedFieldNumber = nestedTag >> 3;
                                                var nestedWireType = nestedTag & 0x07;
                                                Console.WriteLine($"    Nested Field {nestedFieldNumber}, Wire Type {nestedWireType}");

                                                // Skip the value for now to avoid infinite recursion
                                                switch (nestedWireType)
                                                {
                                                    case 0:
                                                        ReadVarint(nestedReader);
                                                        break;
                                                    case 1:
                                                        if (nestedStream.Position + 8 <= nestedStream.Length)
                                                            nestedReader.ReadUInt64();
                                                        break;
                                                    case 2:
                                                        var nestedLength = ReadVarint(nestedReader);
                                                        if (nestedStream.Position + (long)nestedLength <= nestedStream.Length)
                                                            nestedReader.ReadBytes((int)nestedLength);
                                                        break;
                                                    case 5:
                                                        if (nestedStream.Position + 4 <= nestedStream.Length)
                                                            nestedReader.ReadUInt32();
                                                        break;
                                                }
                                                nestedFields++;
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"    Error parsing nested field: {ex.Message}");
                                                break;
                                            }
                                        }
                                    }
                                    else if (length >= 10000)
                                    {
                                        Console.WriteLine($"  Large field ({length} bytes) - attempting nested analysis...");

                                        // For very large fields, try to analyze them as nested messages
                                        var nestedData = bytes;
                                        var nestedStream = new MemoryStream(nestedData);
                                        var nestedReader = new BinaryReader(nestedStream);

                                        int nestedFields = 0;
                                        while (nestedStream.Position < nestedStream.Length) // No limit - process all guild members
                                        {
                                            try
                                            {
                                                var nestedTag = ReadVarint(nestedReader);
                                                if (nestedTag == 0) break;

                                                var nestedFieldNumber = nestedTag >> 3;
                                                var nestedWireType = nestedTag & 0x07;
                                                Console.WriteLine($"    Nested Field {nestedFieldNumber}, Wire Type {nestedWireType} (tag: 0x{nestedTag:X})");

                                                // Parse the value and show details
                                                switch (nestedWireType)
                                                {
                                                    case 0: // Varint
                                                        var nestedVarintValue = ReadVarint(nestedReader);
                                                        Console.WriteLine($"      Varint: {nestedVarintValue} (0x{nestedVarintValue:X})");
                                                        break;
                                                    case 1: // 64-bit
                                                        if (nestedStream.Position + 8 <= nestedStream.Length)
                                                        {
                                                            var fixed64 = nestedReader.ReadUInt64();
                                                            Console.WriteLine($"      64-bit: {fixed64} (0x{fixed64:X16})");
                                                        }
                                                        break;
                                                    case 2: // Length-delimited
                                                        var nestedLength = ReadVarint(nestedReader);
                                                        Console.WriteLine($"      Length-delimited: {nestedLength} bytes");
                                                        if (nestedStream.Position + (long)nestedLength <= nestedStream.Length)
                                                        {
                                                            var nestedBytes = nestedReader.ReadBytes((int)nestedLength);
                                                            Console.WriteLine($"      Data: {BitConverter.ToString(nestedBytes.Take(16).ToArray()).Replace("-", " ")}");

                                                            // Special analysis for different data structures
                                                            if (nestedLength >= 12 && nestedLength <= 18)
                                                            {
                                                                Console.WriteLine($"      *** GUILD MEMBER BASIC DATA ***");
                                                                GuildMemberActivityData guildMemberActivityData = new GuildMemberActivityData();
                                                                try
                                                                {
                                                                    var dataStream = new MemoryStream(nestedBytes);
                                                                    var dataReader = new BinaryReader(dataStream);

                                                                    int fieldNum = 1;
                                                                    while (dataStream.Position < dataStream.Length)
                                                                    {
                                                                        var dataTag = ReadVarint(dataReader);
                                                                        if (dataTag == 0) break;

                                                                        var dataFieldNumber = dataTag >> 3;
                                                                        var dataWireType = dataTag & 0x07;

                                                                        if (dataWireType == 0) // Varint
                                                                        {
                                                                            var value = ReadVarint(dataReader);

                                                                            // Label fields based on your analysis
                                                                            string fieldLabel = dataFieldNumber switch
                                                                            {
                                                                                1 => "Guild Role ID",
                                                                                2 => "User ID",
                                                                                3 => "Last Online Timestamp",
                                                                                4 => "Activity Merit 1",
                                                                                5 => "Activity Merit 2",
                                                                                _ => $"Field {dataFieldNumber}"
                                                                            };

                                                                            switch (dataFieldNumber)
                                                                            {
                                                                                case 1:
                                                                                    guildMemberActivityData.RoleId = value;
                                                                                    break;
                                                                                case 2:
                                                                                    guildMemberActivityData.UserId = (int)value;
                                                                                    break;
                                                                                case 3:
                                                                                    guildMemberActivityData.JoinTS = value;
                                                                                    break;
                                                                                case 4:
                                                                                    guildMemberActivityData.Activity1 = value;
                                                                                    break;
                                                                                case 5:
                                                                                    guildMemberActivityData.Activity2 = value;
                                                                                    break;
                                                                            }

                                                                            Console.WriteLine($"        {fieldLabel}: {value} (0x{value:X})");

                                                                            // Special handling for timestamp (field 3)
                                                                            if (dataFieldNumber == 3 && value > 1000000000 && value < 2000000000)
                                                                            {
                                                                                var dateTime = DateTimeOffset.FromUnixTimeSeconds((long)value);
                                                                                Console.WriteLine($"          -> Last Online: {dateTime:yyyy-MM-dd HH:mm:ss}");
                                                                            }
                                                                        }
                                                                        fieldNum++;
                                                                    }
                                                                }
                                                                catch (Exception ex)
                                                                {
                                                                    Console.WriteLine($"      Error parsing guild member basic data: {ex.Message}");
                                                                }
                                                                activityData[guildMemberActivityData.UserId] = guildMemberActivityData;
                                                                Console.WriteLine($"      *** END GUILD MEMBER BASIC DATA ***");
                                                            }
                                                            else if (nestedLength >= 50 && nestedLength <= 400)
                                                            {
                                                                Console.WriteLine($"      *** GUILD MEMBER DETAILED DATA ***");
                                                                try
                                                                {

                                                                    GuildMemberData guildMemberData = new GuildMemberData();

                                                                    var dataStream = new MemoryStream(nestedBytes);
                                                                    var dataReader = new BinaryReader(dataStream);



                                                                    int fieldNum = 1;
                                                                    while (dataStream.Position < dataStream.Length)
                                                                    {
                                                                        var dataTag = ReadVarint(dataReader);
                                                                        if (dataTag == 0) break;

                                                                        var dataFieldNumber = dataTag >> 3;
                                                                        var dataWireType = dataTag & 0x07;

                                                                        if (dataWireType == 0) // Varint
                                                                        {
                                                                            var value = ReadVarint(dataReader);

                                                                            // Label fields based on your analysis
                                                                            string fieldLabel = dataFieldNumber switch
                                                                            {
                                                                                5 => "Gear Score",
                                                                                6 => "Unknown Numeric",
                                                                                8 => "Unknown Numeric",
                                                                                _ => $"Field {dataFieldNumber}"
                                                                            };
                                                                            switch (dataFieldNumber)
                                                                            {
                                                                                case 5:
                                                                                    guildMemberData.GearScore = (int)value;
                                                                                    break;
                                                                            }

                                                                            Console.WriteLine($"        {fieldLabel}: {value} (0x{value:X})");
                                                                        }
                                                                        else if (dataWireType == 2) // Length-delimited
                                                                        {
                                                                            var subLength = ReadVarint(dataReader);

                                                                            // Label fields based on your analysis
                                                                            string fieldLabel = dataFieldNumber switch
                                                                            {
                                                                                1 => "Player Basic Info",
                                                                                2 => "Avatar/Photo URLs",
                                                                                4 => "Unknown Data (7 bytes)",
                                                                                7 => "Guild Name",
                                                                                9 => "Timestamp Data",
                                                                                _ => $"Field {dataFieldNumber}"
                                                                            };

                                                                            Console.WriteLine($"        {fieldLabel}: Length-delimited ({subLength} bytes)");

                                                                            if (dataStream.Position + (long)subLength <= dataStream.Length)
                                                                            {
                                                                                var subBytes = dataReader.ReadBytes((int)subLength);
                                                                                Console.WriteLine($"          Data: {BitConverter.ToString(subBytes.Take(32).ToArray()).Replace("-", " ")}");

                                                                                // Try to extract strings
                                                                                var strings = ExtractStrings(subBytes);
                                                                                if (strings.Any())
                                                                                {
                                                                                    Console.WriteLine($"          Strings found: {string.Join(", ", strings.Take(3))}");
                                                                                    if (dataFieldNumber == 2)
                                                                                    {
                                                                                        guildMemberData.PlayerPhotoRaw = strings;
                                                                                    }
                                                                                }

                                                                                // Deep nested analysis for detailed data fields
                                                                                if (subLength > 10 && subLength < 500) // Reasonable size for nested protobuf
                                                                                {
                                                                                    Console.WriteLine($"          *** DEEP NESTED ANALYSIS ***");
                                                                                    try
                                                                                    {
                                                                                        var deepStream = new MemoryStream(subBytes);
                                                                                        var deepReader = new BinaryReader(deepStream);

                                                                                        int deepFieldCount = 0;
                                                                                        while (deepStream.Position < deepStream.Length && deepFieldCount < 20)
                                                                                        {
                                                                                            var deepTag = ReadVarint(deepReader);
                                                                                            if (deepTag == 0) break;

                                                                                            var deepFieldNumber = deepTag >> 3;
                                                                                            var deepWireType = deepTag & 0x07;

                                                                                            Console.WriteLine($"            Deep Field {deepFieldNumber}, Wire Type {deepWireType}");

                                                                                            if (deepWireType == 0) // Varint
                                                                                            {
                                                                                                var deepValue = ReadVarint(deepReader);

                                                                                                // Label fields based on your analysis
                                                                                                string deepFieldLabel = deepFieldNumber switch
                                                                                                {
                                                                                                    1 => "User ID (Primary)",
                                                                                                    2 => "User ID (Duplicate)",
                                                                                                    4 => "Unknown Small Value",
                                                                                                    5 => "Unknown Small Value",
                                                                                                    6 => "Character Level",
                                                                                                    7 => "Unknown Value",
                                                                                                    11 => "Timestamp 1",
                                                                                                    15 => "Unknown Small Value",
                                                                                                    16 => "Timestamp 2",
                                                                                                    _ => $"Field {deepFieldNumber}"
                                                                                                };
                                                                                                Console.WriteLine($"              {deepFieldLabel}: {deepValue} (0x{deepValue:X})");

                                                                                                switch (deepFieldNumber)
                                                                                                {
                                                                                                    case 1:
                                                                                                        if (guildMemberData.UserId == 0)
                                                                                                        {
                                                                                                            guildMemberData.UserId = (int)deepValue;
                                                                                                        }
                                                                                                        break;
                                                                                                    case 2:
                                                                                                    if (guildMemberData.UserIdSecondary == 0)
                                                                                                        {
                                                                                                            guildMemberData.UserIdSecondary = (int)deepValue;
                                                                                                        }                                                                                                        
                                                                                                        break;
                                                                                                    case 6:
                                                                                                        guildMemberData.CharacterLevel = (int)deepValue;
                                                                                                        break;
                                                                                                    case 16:
                                                                                                        guildMemberData.LastLoginTS = deepValue/1000;
                                                                                                        break;
                                                                                                }
                                                                                            }
                                                                                            else if (deepWireType == 2) // Length-delimited
                                                                                            {
                                                                                                var deepLength = ReadVarint(deepReader);

                                                                                                // Label length-delimited fields
                                                                                                string deepLengthLabel = deepFieldNumber switch
                                                                                                {
                                                                                                    3 => "Player Name",
                                                                                                    8 => "Unknown Data (2 bytes)",
                                                                                                    10 => "UUID/GUID",
                                                                                                    _ => $"Field {deepFieldNumber}"
                                                                                                };

                                                                                                if (deepStream.Position + (long)deepLength <= deepStream.Length)
                                                                                                {
                                                                                                    var deepBytes = deepReader.ReadBytes((int)deepLength);

                                                                                                    // Extract strings from deep nested data
                                                                                                    var deepStrings = ExtractStrings(deepBytes);

                                                                                                    // Special handling for player name (field 3)
                                                                                                    if (deepFieldNumber == 3 && deepStrings.Any())
                                                                                                    {
                                                                                                        Console.WriteLine($"              {deepLengthLabel}: {deepStrings.First()}");
                                                                                                        string _value = deepStrings.First();
                                                                                                        if (_value.IndexOf("https://") == -1)
                                                                                                        {
                                                                                                            guildMemberData.PlayerName = deepStrings.First();
                                                                                                        }
                                                                                                        else
                                                                                                        {

                                                                                                        }
                                                                                                    }
                                                                                                    else
                                                                                                    {
                                                                                                        Console.WriteLine($"              {deepLengthLabel}: Length-delimited ({deepLength} bytes)");
                                                                                                        Console.WriteLine($"              Data: {BitConverter.ToString(deepBytes.Take(16).ToArray()).Replace("-", " ")}");

                                                                                                        if (deepStrings.Any())
                                                                                                        {
                                                                                                            Console.WriteLine($"              Deep strings: {string.Join(", ", deepStrings.Take(3))}");
                                                                                                        }
                                                                                                    }
                                                                                                }
                                                                                            }
                                                                                            else if (deepWireType == 5) // 32-bit
                                                                                            {
                                                                                                if (deepStream.Position + 4 <= deepStream.Length)
                                                                                                {
                                                                                                    var deepFixed32 = deepReader.ReadUInt32();
                                                                                                    Console.WriteLine($"              32-bit: {deepFixed32} (0x{deepFixed32:X8})");
                                                                                                }
                                                                                            }

                                                                                            deepFieldCount++;
                                                                                        }
                                                                                    }
                                                                                    catch (Exception ex)
                                                                                    {
                                                                                        Console.WriteLine($"          Deep analysis error: {ex.Message}");
                                                                                    }
                                                                                }

                                                                                // Special handling for class data (field 4, 7 bytes)
                                                                                if (dataFieldNumber == 4 && subLength == 7)
                                                                                {
                                                                                    Console.WriteLine($"          *** CLASS DATA ANALYSIS ***");
                                                                                    try
                                                                                    {
                                                                                        var classStream = new MemoryStream(subBytes);
                                                                                        var classReader = new BinaryReader(classStream);

                                                                                        int classFieldCount = 0;
                                                                                        while (classStream.Position < classStream.Length && classFieldCount < 5)
                                                                                        {
                                                                                            var classTag = ReadVarint(classReader);
                                                                                            if (classTag == 0) break;

                                                                                            var classFieldNumber = classTag >> 3;
                                                                                            var classWireType = classTag & 0x07;

                                                                                            if (classWireType == 0) // Varint
                                                                                            {
                                                                                                var classValue = ReadVarint(classReader);
                                                                                                string classFieldLabel = classFieldNumber switch
                                                                                                {
                                                                                                    1 => "Class ID",
                                                                                                    2 => "Class Variant/Subclass",
                                                                                                    _ => $"Field {classFieldNumber}"
                                                                                                };
                                                                                                Console.WriteLine($"            {classFieldLabel}: {classValue} (0x{classValue:X})");
                                                                                                switch (classFieldNumber)
                                                                                                {
                                                                                                    case 1:
                                                                                                        guildMemberData.ClassId = (int)classValue;
                                                                                                        break;
                                                                                                    case 2:
                                                                                                        guildMemberData.ClassVariant = (int)classValue;
                                                                                                        break;
                                                                                                }
                                                                                            }
                                                                                            classFieldCount++;
                                                                                        }
                                                                                    }
                                                                                    catch (Exception ex)
                                                                                    {
                                                                                        Console.WriteLine($"          Class data parsing error: {ex.Message}");
                                                                                    }
                                                                                }

                                                                                // Special handling for timestamp data (field 9)
                                                                                if (dataFieldNumber == 9 && subLength == 6)
                                                                                {
                                                                                    try
                                                                                    {
                                                                                        // Try to parse as timestamp
                                                                                        var timestampBytes = subBytes.Skip(1).Take(5).ToArray(); // Skip first byte, take 5 bytes
                                                                                        if (timestampBytes.Length >= 4)
                                                                                        {
                                                                                            var timestamp = BitConverter.ToUInt32(timestampBytes, 0);
                                                                                            if (timestamp > 1000000000 && timestamp < 2000000000)
                                                                                            {
                                                                                                var dateTime = DateTimeOffset.FromUnixTimeSeconds(timestamp);
                                                                                                Console.WriteLine($"          -> Parsed Timestamp: {dateTime:yyyy-MM-dd HH:mm:ss}");
                                                                                            }
                                                                                        }
                                                                                    }
                                                                                    catch (Exception ex)
                                                                                    {
                                                                                        Console.WriteLine($"          -> Timestamp parsing failed: {ex.Message}");
                                                                                    }
                                                                                }
                                                                            }
                                                                        }
                                                                        fieldNum++;
                                                                    }
                                                                    Console.WriteLine(guildMemberData.PlayerName);
                                                                    memberData[guildMemberData.UserIdSecondary] = guildMemberData;
                                                                }
                                                                catch (Exception ex)
                                                                {
                                                                    Console.WriteLine($"      Error parsing guild member detailed data: {ex.Message}");
                                                                }
                                                                Console.WriteLine($"      *** END OF GUILD MEMBER DETAILED DATA ***");
                                                            }

                                                            // Try to interpret as string if reasonable size
                                                            if (nestedLength > 0 && nestedLength < 100 && IsValidUtf8(nestedBytes))
                                                            {
                                                                var str = Encoding.UTF8.GetString(nestedBytes);
                                                                if (str.All(c => char.IsControl(c) || char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c)))
                                                                {
                                                                    Console.WriteLine($"      As string: \"{str}\"");
                                                                }
                                                            }
                                                        }
                                                        break;
                                                    case 5: // 32-bit
                                                        if (nestedStream.Position + 4 <= nestedStream.Length)
                                                        {
                                                            var fixed32 = nestedReader.ReadUInt32();
                                                            Console.WriteLine($"      32-bit: {fixed32} (0x{fixed32:X8})");
                                                        }
                                                        break;
                                                }
                                                nestedFields++;
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"    Error parsing nested field: {ex.Message}");
                                                break;
                                            }
                                        }

                                        Console.WriteLine($"    Processed {nestedFields} guild members total");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("  Length-delimited: Invalid length or insufficient data");
                                    return;
                                }
                                break;

                            case 5: // 32-bit
                                if (stream.Position + 4 <= stream.Length)
                                {
                                    var fixed32 = reader.ReadUInt32();
                                    Console.WriteLine($"  32-bit: {fixed32} (0x{fixed32:X8})");
                                }
                                else
                                {
                                    Console.WriteLine("  32-bit: Insufficient data");
                                    return;
                                }
                                break;

                            default:
                                Console.WriteLine($"  Unknown wire type: {wireType}");
                                return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Error parsing field: {ex.Message}");
                        return;
                    }

                    fieldCount++;
                }

                if (fieldCount >= 50)
                {
                    Console.WriteLine("Reached field limit (50), stopping analysis");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in protobuf field analysis: {ex.Message}");
            }
            if(memberData.Count > 0 && activityData.Count > 0)
            {
                //this means this is the roster data, proceed
                Console.WriteLine($"Member data count: {memberData.Count}");
                Console.WriteLine($"Activity data count: {activityData.Count}");
                
                // Update joined guild data and export to TSV
                try
                {
                    GuildRosterExporter.OnRosterProtobufProcessed(memberData, activityData);
                    Console.WriteLine($"Guild roster OnRosterProtobufProcessed");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to export guild roster: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Read a varint from the stream
        /// </summary>
        private static ulong ReadVarint(BinaryReader reader)
        {
            ulong result = 0;
            int shift = 0;
            while (true)
            {
                if (reader.BaseStream.Position >= reader.BaseStream.Length)
                    throw new EndOfStreamException("Unexpected end of stream while reading varint");
                    
                byte b = reader.ReadByte();
                result |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) break;
                shift += 7;
                if (shift >= 64) throw new InvalidDataException("Varint too long");
            }
            return result;
        }

        /// <summary>
        /// Extract readable strings from binary data
        /// </summary>
        private static List<string> ExtractStrings(byte[] data)
        {
            var strings = new List<string>();
            var current = new List<byte>();
            
            foreach (byte b in data)
            {
                if (b >= 32 && b <= 126) // Printable ASCII
                {
                    current.Add(b);
                }
                else
                {
                    if (current.Count >= 3) // Minimum 3 chars
                    {
                        try
                        {
                            var str = Encoding.UTF8.GetString(current.ToArray());
                            if (str.All(c => char.IsControl(c) || char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c)))
                            {
                                strings.Add(str);
                            }
                        }
                        catch
                        {
                            // Ignore invalid UTF-8 sequences
                        }
                    }
                    current.Clear();
                }
            }
            
            if (current.Count >= 3)
            {
                try
                {
                    var str = Encoding.UTF8.GetString(current.ToArray());
                    if (str.All(c => char.IsControl(c) || char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c)))
                    {
                        strings.Add(str);
                    }
                }
                catch
                {
                    // Ignore invalid UTF-8 sequences
                }
            }
            
            return strings;
        }

        /// <summary>
        /// Check if byte array contains valid UTF-8
        /// </summary>
        private static bool IsValidUtf8(byte[] data)
        {
            try
            {
                Encoding.UTF8.GetString(data);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class GuildMemberData
    {
        public int UserId { get; set; }
        public int UserIdSecondary { get; set; }
        public string PlayerName { get; set; } = string.Empty;        
        public int CharacterLevel { get; set; }
        public string GuildName { get; set; } = string.Empty;
        public int ClassId { get; set; }
        public int ClassVariant { get; set; }
        public int GearScore { get; set; }
        public List<string> PlayerPhotoRaw { get; internal set; }
        public ulong LastLoginTS { get; set; }
        public DateTime? ParsedLastLoginTS => LastLoginTS > 1000000000 && LastLoginTS < 2000000000
            ? DateTimeOffset.FromUnixTimeSeconds((long)LastLoginTS).DateTime
            : null;
    }
    
    public class GuildMemberActivityData
    {        
        public int UserId { get; set; }
        public ulong RoleId { get; set; }
        public ulong Activity1 { get; set; }
        public ulong Activity2 { get; set; }
        public ulong JoinTS { get; set; }
        public DateTime? ParsedJoinTS => JoinTS > 1000000000 && JoinTS < 2000000000
            ? DateTimeOffset.FromUnixTimeSeconds((long)JoinTS).DateTime
            : null;

    }
}
