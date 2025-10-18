using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StarResonanceDpsAnalysis.Core
{
    /// <summary>
    /// Discord bot service for fetching guild members and roles
    /// </summary>
    public class DiscordService
    {
        private readonly DiscordSocketClient _client;
        private bool _isConnected = false;

        public DiscordService()
        {
            var config = new DiscordSocketConfig()
            {
                // Enable required intents for guild members
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMembers,
                // Set message cache size to 0 since we don't need messages
                MessageCacheSize = 0,
                // Enable user cache for member data
                AlwaysDownloadUsers = true,
                // Add logging for debugging
                LogLevel = LogSeverity.Info
            };
            _client = new DiscordSocketClient(config);
            
            // Add event handlers for debugging
            _client.Log += OnLog;
            _client.Connected += OnConnected;
            _client.Disconnected += OnDisconnected;
        }

        /// <summary>
        /// Starts the Discord bot with the provided token
        /// </summary>
        /// <param name="token">Discord bot token</param>
        /// <returns>True if connection was successful</returns>
        public async Task<bool> StartAsync(string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("Discord bot token is required");
                    return false;
                }

                Console.WriteLine("Attempting to connect to Discord...");
                Console.WriteLine($"Token length: {token.Length} characters");
                Console.WriteLine($"Token starts with: {token.Substring(0, Math.Min(10, token.Length))}...");
                Console.WriteLine($"Full token: {token}");

                await _client.LoginAsync(TokenType.Bot, token);
                Console.WriteLine("Login successful, starting client...");
                
                await _client.StartAsync();
                Console.WriteLine("Client started, waiting for ready event...");

                // Wait for the bot to be ready
                _client.Ready += OnReady;
                
                // Wait longer for connection to establish
                for (int i = 0; i < 10; i++)
                {
                    if (_isConnected)
                    {
                        Console.WriteLine("Bot connected successfully!");
                        return true;
                    }
                    await Task.Delay(1000);
                    Console.WriteLine($"Waiting for connection... ({i + 1}/10)");
                }
                
                Console.WriteLine("Connection timeout - bot did not connect within 10 seconds");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting Discord bot: {ex.Message}");
                Console.WriteLine($"Exception type: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                return false;
            }
        }

        /// <summary>
        /// Stops the Discord bot
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                if (_client != null)
                {
                    await _client.StopAsync();
                    await _client.LogoutAsync();
                    _isConnected = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping Discord bot: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets all guilds the bot is connected to
        /// </summary>
        /// <returns>List of Discord guilds</returns>
        public List<DiscordGuild> GetGuilds()
        {
            if (!_isConnected)
            {
                return new List<DiscordGuild>();
            }

            return _client.Guilds.Select(guild => new DiscordGuild
            {
                Id = guild.Id.ToString(),
                Name = guild.Name,
                Owner = guild.OwnerId == _client.CurrentUser.Id
            }).ToList();
        }

        /// <summary>
        /// Gets all members from a specific guild
        /// </summary>
        /// <param name="guildId">Discord guild ID</param>
        /// <returns>List of Discord members</returns>
        public async Task<List<DiscordMember>> GetGuildMembersAsync(string guildId)
        {
            if (!_isConnected)
            {
                throw new InvalidOperationException("Discord bot is not connected");
            }

            try
            {
                var guild = _client.GetGuild(ulong.Parse(guildId));
                if (guild == null)
                {
                    throw new ArgumentException($"Guild with ID {guildId} not found");
                }

                // Download all users for this guild
                await guild.DownloadUsersAsync();

                var members = new List<DiscordMember>();
                foreach (var user in guild.Users)
                {
                    // Debug: Check specific members we're having issues with
                    bool isDebugMember = user.Username.Contains("fleurentine", StringComparison.OrdinalIgnoreCase) ||
                                       user.Username.Contains("TKPStefan", StringComparison.OrdinalIgnoreCase) ||
                                       user.Username.Contains("peanuts", StringComparison.OrdinalIgnoreCase);
                    
                    if (isDebugMember)
                    {
                        Console.WriteLine($"\n=== DEBUG: DiscordService processing user ===");
                        Console.WriteLine($"user.Username: '{user.Username}'");
                        Console.WriteLine($"user.Nickname: '{user.Nickname}'");
                        Console.WriteLine($"user.DisplayName: '{user.DisplayName}'");
                        Console.WriteLine($"user.Id: '{user.Id}'");
                        Console.WriteLine("=== END DEBUG ===\n");
                    }
                    
                    var member = new DiscordMember
                    {
                        Id = user.Id.ToString(),
                        Username = user.Username,
                        DisplayName = user.DisplayName, // Server nickname or username
                        Discriminator = user.Discriminator,
                        AvatarUrl = user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl(),
                        IsBot = user.IsBot,
                        JoinedAt = user.JoinedAt?.DateTime ?? DateTime.MinValue
                    };

                    // Get user's roles
                    var guildUser = guild.GetUser(user.Id);
                    if (guildUser != null)
                    {
                        member.Roles = guildUser.Roles
                            .Where(r => r.Id != guild.EveryoneRole.Id)
                            .Select(r => new DiscordRole
                            {
                                Id = r.Id.ToString(),
                                Name = r.Name,
                                Color = r.Color.ToString()
                            })
                            .ToList();
                    }

                    members.Add(member);
                }

                Console.WriteLine($"Fetched {members.Count} Discord guild members");
                return members;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching Discord guild members: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets all roles from a specific guild
        /// </summary>
        /// <param name="guildId">Discord guild ID</param>
        /// <returns>List of Discord roles</returns>
        public async Task<List<DiscordRole>> GetGuildRolesAsync(string guildId)
        {
            if (!_isConnected)
            {
                throw new InvalidOperationException("Discord bot is not connected");
            }

            try
            {
                var guild = _client.GetGuild(ulong.Parse(guildId));
                if (guild == null)
                {
                    throw new ArgumentException($"Guild with ID {guildId} not found");
                }

                var roles = guild.Roles
                    .Where(r => r.Id != guild.EveryoneRole.Id)
                    .Select(r => new DiscordRole
                    {
                        Id = r.Id.ToString(),
                        Name = r.Name,
                        Color = r.Color.ToString(),
                        Position = r.Position,
                        Permissions = r.Permissions.ToString()
                    })
                    .OrderByDescending(r => r.Position)
                    .ToList();

                Console.WriteLine($"Fetched {roles.Count} Discord guild roles");
                return roles;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching Discord guild roles: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets members with a specific role
        /// </summary>
        /// <param name="guildId">Discord guild ID</param>
        /// <param name="roleId">Discord role ID</param>
        /// <returns>List of member names with the role</returns>
        public async Task<List<string>> GetMembersWithRoleAsync(string guildId, string roleId)
        {
            if (!_isConnected)
            {
                throw new InvalidOperationException("Discord bot is not connected");
            }

            try
            {
                var guild = _client.GetGuild(ulong.Parse(guildId));
                if (guild == null)
                {
                    throw new ArgumentException($"Guild with ID {guildId} not found");
                }

                var role = guild.GetRole(ulong.Parse(roleId));
                if (role == null)
                {
                    throw new ArgumentException($"Role with ID {roleId} not found");
                }

                // Download all users for this guild
                await guild.DownloadUsersAsync();

                var members = role.Members
                    .Select(user => user.Nickname ?? user.Username)
                    .ToList();

                Console.WriteLine($"Found {members.Count} members with role '{role.Name}'");
                return members;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching members with role: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get members who have a specific role (by name, with fuzzy matching)
        /// </summary>
        public async Task<List<DiscordMember>> GetMembersWithRoleAsync(string guildId, string roleName, int matchThreshold = 80)
        {
            var allMembers = await GetGuildMembersAsync(guildId);
            var matchingMembers = new List<DiscordMember>();

            foreach (var member in allMembers)
            {
                foreach (var role in member.Roles)
                {
                    // Check for exact match first
                    if (string.Equals(role.Name, roleName, StringComparison.OrdinalIgnoreCase))
                    {
                        matchingMembers.Add(member);
                        break;
                    }

                    // Check for substring match
                    if (role.Name.Contains(roleName, StringComparison.OrdinalIgnoreCase))
                    {
                        matchingMembers.Add(member);
                        break;
                    }

                    // Check for fuzzy match (simple Levenshtein distance)
                    if (CalculateSimilarity(role.Name, roleName) >= matchThreshold)
                    {
                        matchingMembers.Add(member);
                        break;
                    }
                }
            }

            Console.WriteLine($"Found {matchingMembers.Count} members with role matching '{roleName}'");
            return matchingMembers;
        }

        /// <summary>
        /// Calculate similarity between two strings using Levenshtein distance
        /// </summary>
        private int CalculateSimilarity(string source, string target)
        {
            if (string.IsNullOrEmpty(source)) return string.IsNullOrEmpty(target) ? 100 : 0;
            if (string.IsNullOrEmpty(target)) return 0;

            var sourceLength = source.Length;
            var targetLength = target.Length;
            var distance = new int[sourceLength + 1, targetLength + 1];

            // Initialize
            for (int i = 0; i <= sourceLength; distance[i, 0] = i++) { }
            for (int j = 0; j <= targetLength; distance[0, j] = j++) { }

            // Calculate
            for (int i = 1; i <= sourceLength; i++)
            {
                for (int j = 1; j <= targetLength; j++)
                {
                    var cost = target[j - 1] == source[i - 1] ? 0 : 1;
                    distance[i, j] = Math.Min(
                        Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                        distance[i - 1, j - 1] + cost);
                }
            }

            var maxLength = Math.Max(sourceLength, targetLength);
            return maxLength == 0 ? 100 : (int)((1.0 - (double)distance[sourceLength, targetLength] / maxLength) * 100);
        }

        /// <summary>
        /// Checks if the bot is connected
        /// </summary>
        /// <returns>True if connected</returns>
        public bool IsConnected()
        {
            return _isConnected && _client?.ConnectionState == ConnectionState.Connected;
        }

        /// <summary>
        /// Bot ready event handler
        /// </summary>
        private Task OnReady()
        {
            _isConnected = true;
            Console.WriteLine($"Discord bot connected as {_client.CurrentUser.Username}#{_client.CurrentUser.Discriminator}");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Log event handler for debugging
        /// </summary>
        private Task OnLog(LogMessage message)
        {
            Console.WriteLine($"[Discord] {message.Severity}: {message.Message}");
            if (message.Exception != null)
            {
                Console.WriteLine($"[Discord] Exception: {message.Exception}");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Connected event handler
        /// </summary>
        private Task OnConnected()
        {
            Console.WriteLine("[Discord] Bot connected to Discord gateway");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Disconnected event handler
        /// </summary>
        private Task OnDisconnected(Exception exception)
        {
            Console.WriteLine($"[Discord] Bot disconnected: {exception?.Message ?? "Unknown reason"}");
            _isConnected = false;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Discord guild data structure
    /// </summary>
    public class DiscordGuild
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool Owner { get; set; } = false;
    }

    /// <summary>
    /// Discord member data structure
    /// </summary>
    public class DiscordMember
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Discriminator { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public bool IsBot { get; set; } = false;
        public DateTime JoinedAt { get; set; }
        public List<DiscordRole> Roles { get; set; } = new List<DiscordRole>();
    }

    /// <summary>
    /// Discord role data structure
    /// </summary>
    public class DiscordRole
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public int Position { get; set; } = 0;
        public string Permissions { get; set; } = string.Empty;
    }
}