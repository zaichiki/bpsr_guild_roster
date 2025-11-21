using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BlueProto;

namespace StarResonanceDpsAnalysis.Core
{
    /// <summary>
    /// Collects Master Score data (player info) from TYPE3 messages during automation
    /// </summary>
    public class MasterScoreCollector
    {
        private static readonly object _lock = new object();
        private static bool _isCollecting = false;
        private static readonly Dictionary<int, MasterScoreData> _collectedData = new Dictionary<int, MasterScoreData>();

        /// <summary>
        /// Start collecting Master Score data
        /// </summary>
        public static void StartCollection()
        {
            lock (_lock)
            {
                _isCollecting = true;
                _collectedData.Clear();
                Console.WriteLine("[MASTER SCORE] Collection started");
            }
        }

        /// <summary>
        /// Send collected data to REST API
        /// </summary>
        public static async System.Threading.Tasks.Task<bool> SendToApiAsync(string apiEndpoint)
        {
            try
            {
                List<MasterScoreData> dataToSend;
                lock (_lock)
                {
                    if (_collectedData.Count == 0)
                    {
                        Console.WriteLine("[MASTER SCORE] No data to send");
                        return false;
                    }
                    dataToSend = _collectedData.Values.ToList();
                }

                Console.WriteLine($"[MASTER SCORE] Sending {dataToSend.Count} records to API: {apiEndpoint}");

                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    
                    // Convert to JSON
                    var json = System.Text.Json.JsonSerializer.Serialize(dataToSend, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                        WriteIndented = false
                    });

                    var content = new System.Net.Http.StringContent(json, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(apiEndpoint, content);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[MASTER SCORE] Successfully sent {dataToSend.Count} records to API");
                        return true;
                    }
                    else
                    {
                        var errorBody = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"[MASTER SCORE] API error: {response.StatusCode} - {errorBody}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MASTER SCORE] Error sending to API: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get all collected data (for display)
        /// </summary>
        public static List<MasterScoreData> GetAllData()
        {
            lock (_lock)
            {
                return _collectedData.Values.OrderBy(p => p.PlayerName).ToList();
            }
        }

        /// <summary>
        /// Clear collected data
        /// </summary>
        public static void ClearData()
        {
            lock (_lock)
            {
                int count = _collectedData.Count;
                _collectedData.Clear();
                Console.WriteLine($"[MASTER SCORE] Cleared {count} records");
            }
        }

        /// <summary>
        /// Process a TYPE3 message and extract player data
        /// </summary>
        public static void ProcessType3Message(byte[] protobufData)
        {
            if (!_isCollecting) return;

            try
            {
                // Extract player data - look for master score value
                ExtractPlayerData(protobufData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MASTER SCORE] Error processing TYPE3: {ex.Message}");
            }
        }

        /// <summary>
        /// Extract player data from protobuf bytes using Blueprotobuf
        /// Master Score is at path: Field[1][2][22][1]
        /// </summary>
        private static void ExtractPlayerData(byte[] data)
        {
            try
            {
                // Decode top level: Field[1]
                var topLevel = Blueprotobuf.Decode(data);
                if (!topLevel.ContainsKey(1)) return;

                // Extract Field[1] bytes
                byte[]? field1Bytes = null;
                if (topLevel[1] is byte[] bytes1) field1Bytes = bytes1;
                else if (topLevel[1] is ProtoValue pv1) field1Bytes = pv1.Raw;
                if (field1Bytes == null) return;

                // Extract player name from Field[1] raw bytes
                string? playerName = ExtractPlayerNameFromRaw(field1Bytes);

                // Decode Field[1] → Field[2]
                var level1 = Blueprotobuf.Decode(field1Bytes);
                if (!level1.ContainsKey(2)) return;

                // Extract Field[2] bytes
                byte[]? field2Bytes = null;
                if (level1[2] is byte[] bytes2) field2Bytes = bytes2;
                else if (level1[2] is ProtoValue pv2) field2Bytes = pv2.Raw;
                if (field2Bytes == null) return;

                // Decode Field[2] → Field[22]
                var level2 = Blueprotobuf.Decode(field2Bytes);
                if (!level2.ContainsKey(22)) return;

                // Extract Field[22] bytes (master score container)
                byte[]? field22Bytes = null;
                if (level2[22] is byte[] bytes22) field22Bytes = bytes22;
                else if (level2[22] is ProtoValue pv22) field22Bytes = pv22.Raw;
                else if (level2[22] is List<object> directList)
                {
                    if (directList.Count > 1 && directList[1] is ulong ms)
                    {
                        ExtractPlayerInfo(level2, (int)ms, playerName);
                    }
                    return;
                }
                if (field22Bytes == null) return;

                // Decode Field[22] → [1] (master score value)
                var level22 = Blueprotobuf.Decode(field22Bytes);
                if (level22.ContainsKey(1) && level22[1] is ulong masterScore)
                {
                    ExtractPlayerInfo(level2, (int)masterScore, playerName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MASTER SCORE] ExtractPlayerData error: {ex.Message}");
                Console.WriteLine($"[MASTER SCORE] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Extract player name from raw protobuf bytes
        /// Player name is in field 3 (tag 0x1A) early in the data
        /// </summary>
        private static string? ExtractPlayerNameFromRaw(byte[] data)
        {
            try
            {
                // Look for field tag 0x1A (field 3, wire type 2) in first 200 bytes
                for (int i = 0; i < Math.Min(200, data.Length - 10); i++)
                {
                    if (data[i] == 0x1A) // Field 3, wire type 2
                    {
                        int length = data[i + 1]; // Next byte is length
                        if (length > 0 && length < 50 && i + 2 + length <= data.Length)
                        {
                            try
                            {
                                string possibleName = System.Text.Encoding.UTF8.GetString(data, i + 2, length);
                                // Check if it's a valid player name (alphanumeric + spaces)
                                if (possibleName.All(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)) && 
                                    possibleName.Any(c => char.IsLetter(c)))
                                {
                                    return possibleName;
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Extract player ID and name from the decoded protobuf structure
        /// Looking for UID 26669 with master score 2011
        /// </summary>
        private static void ExtractPlayerInfo(Dictionary<int, object> data, int masterScore, string? playerNameFromRaw = null)
        {
            try
            {
                int? playerId = null;
                string? playerName = playerNameFromRaw;
                int? level = null;

                // Extract player ID from Field[1] (first ulong value)
                if (data.ContainsKey(1) && data[1] is ulong ul)
                {
                    playerId = (int)ul;
                }

                // Store the collected data
                if (playerId.HasValue && !string.IsNullOrEmpty(playerName))
                {
                    lock (_lock)
                    {
                        _collectedData[playerId.Value] = new MasterScoreData
                        {
                            PlayerId = playerId.Value,
                            PlayerName = playerName,
                            Level = level ?? 0,
                            MasterScore = masterScore
                        };
                        Console.WriteLine($"[MASTER SCORE] ✓ Collected: {playerName} (ID: {playerId}) - Master Score: {masterScore}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MASTER SCORE] ExtractPlayerInfo error: {ex.Message}");
            }
        }


        /// <summary>
        /// Get current collection status
        /// </summary>
        public static (bool isCollecting, int count) GetStatus()
        {
            lock (_lock)
            {
                return (_isCollecting, _collectedData.Count);
            }
        }
    }

    /// <summary>
    /// Master Score data for a single player
    /// </summary>
    public class MasterScoreData
    {
        public int PlayerId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public int Level { get; set; }
        public int MasterScore { get; set; }
    }
}


