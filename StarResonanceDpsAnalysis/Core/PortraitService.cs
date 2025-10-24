using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StarResonanceDpsAnalysis.Core
{
    /// <summary>
    /// Service for sending guild member portraits to an external service
    /// </summary>
    public class PortraitService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;
        private readonly string _apiKey;
        private bool _disposed = false;

        public PortraitService(string apiUrl, string apiKey)
        {
            _apiUrl = apiUrl ?? throw new ArgumentNullException(nameof(apiUrl));
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Configuration for the Portrait Service
        /// </summary>
        public class PortraitServiceConfig
        {
            public string Url { get; set; } = string.Empty;
            public string ApiKey { get; set; } = string.Empty;
        }

        /// <summary>
        /// Data structure for sending portrait information
        /// </summary>
        public class PortraitData
        {
            public string PlayerId { get; set; } = string.Empty;
            public string PlayerName { get; set; } = string.Empty;
            public string SquareImageUrl { get; set; } = string.Empty;
            public string VerticalImageUrl { get; set; } = string.Empty;
        }

        /// <summary>
        /// Send guild member portraits to the service
        /// </summary>
        /// <param name="portraitData">List of portrait data to send</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> SendPortraitsAsync(List<PortraitData> portraitData)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PortraitService));

            try
            {
                if (portraitData == null || !portraitData.Any())
                {
                    MessageBox.Show("No portrait data to send.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                // Prepare the JSON payload
                var jsonPayload = JsonSerializer.Serialize(portraitData, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });

                // Create the HTTP request
                var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl);
                request.Headers.Add("X-API-Key", _apiKey);
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                Console.WriteLine($"Sending {portraitData.Count} portraits to {_apiUrl}");
                Console.WriteLine($"Payload: {jsonPayload}");

                // Send the request
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Portrait service response: {responseContent}");
                    MessageBox.Show($"Successfully sent {portraitData.Count} portraits to the service.", 
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorMessage = $"Failed to send portraits. Status: {response.StatusCode}\nError: {errorContent}";
                    Console.WriteLine(errorMessage);
                    MessageBox.Show(errorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error sending portraits: {ex.Message}";
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
