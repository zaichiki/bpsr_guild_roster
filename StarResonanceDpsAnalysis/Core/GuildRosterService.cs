using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StarResonanceDpsAnalysis.Core
{
    /// <summary>
    /// Service for sending guild member data to an external API
    /// </summary>
    public class GuildRosterService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;
        private readonly string _apiKey;
        private bool _disposed = false;

        public GuildRosterService(string apiUrl, string apiKey)
        {
            _apiUrl = apiUrl ?? throw new ArgumentNullException(nameof(apiUrl));
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Configuration for the Guild Roster Service
        /// </summary>
        public class GuildRosterServiceConfig
        {
            public string Url { get; set; } = string.Empty;
            public string ApiKey { get; set; } = string.Empty;
        }

        /// <summary>
        /// Send guild member data to the service
        /// </summary>
        /// <param name="guildMembers">List of guild members to send</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> SendGuildDataAsync(List<JoinedGuildMemberData> guildMembers)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GuildRosterService));

            try
            {
                if (guildMembers == null || !guildMembers.Any())
                {
                    MessageBox.Show("No guild data to send.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                // Convert JoinedGuildMemberData to API format
                var apiData = guildMembers.Select(m => new
                {
                    userId = m.UserId,
                    userIdSecondary = m.UserIdSecondary,
                    playerName = m.PlayerName,
                    characterLevel = m.CharacterLevel,
                    guildName = "Defiance",
                    classId = m.ClassId,
                    classVariant = m.ClassVariant,
                    gearScore = m.GearScore,
                    lastLoginTS = m.LastLoginTS,
                    roleId = m.RoleId,
                    activity1 = m.Activity1,
                    activity2 = m.Activity2,
                    joinTS = m.JoinTS,
                    discordIsMember = m.DiscordIsMember ?? "no",
                    serverNickname = m.DiscordServerNickname ?? "",
                    nickname = m.DiscordNickname ?? "",
                    discordHasRole = m.DiscordHasRole ?? "false",
                    squareImageUrl = m.SquareImageUrl ?? "",
                    verticalImageUrl = m.VerticalImageUrl ?? ""
                }).ToList();

                // Prepare the JSON payload
                var jsonPayload = JsonSerializer.Serialize(apiData, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                // Create the HTTP request
                var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl);
                request.Headers.Add("X-API-Key", _apiKey);
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                Console.WriteLine($"Sending {guildMembers.Count} guild members to {_apiUrl}");
                Console.WriteLine($"Payload: {jsonPayload}");

                // Send the request
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Guild roster service response: {responseContent}");
                    MessageBox.Show($"Successfully sent {guildMembers.Count} guild members to the service.", 
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorMessage = $"Failed to send guild data. Status: {response.StatusCode}\nError: {errorContent}";
                    Console.WriteLine(errorMessage);
                    MessageBox.Show(errorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error sending guild data: {ex.Message}";
                Console.WriteLine(errorMessage);
                MessageBox.Show(errorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// Dispose of the HTTP client
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of Dispose pattern
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _httpClient?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}

