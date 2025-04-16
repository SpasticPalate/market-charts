using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MarketCharts.Client.Interfaces;
using MarketCharts.Client.Models;
using MarketCharts.Client.Models.ApiResponse;
using MarketCharts.Client.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace MarketCharts.Client.Services.ApiServices
{
    /// <summary>
    /// Implementation of the backup stock API service using StockData.org
    /// </summary>
    public class BackupApiService : IStockApiService
    {
        private readonly ApiConfiguration _apiConfig;
        private readonly HttpClient _httpClient;
        private readonly ILogger<BackupApiService> _logger;
        private int _remainingApiCalls;

        /// <summary>
        /// Initializes a new instance of the BackupApiService class
        /// </summary>
        /// <param name="apiConfig">The API configuration</param>
        /// <param name="httpClient">The HTTP client</param>
        /// <param name="logger">The logger</param>
        public BackupApiService(ApiConfiguration apiConfig, HttpClient httpClient, ILogger<BackupApiService> logger)
        {
            _apiConfig = apiConfig ?? throw new ArgumentNullException(nameof(apiConfig));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _remainingApiCalls = apiConfig.BackupApi.DailyLimit;
            
            // Ensure the HttpClient has the correct base address
            if (_httpClient.BaseAddress == null)
            {
                _httpClient.BaseAddress = new Uri("https://api.stockdata.org/");
            }
        }

        /// <inheritdoc/>
        public string ServiceName => "StockData.org";

        /// <inheritdoc/>
        public int ApiCallLimit => _apiConfig.BackupApi.DailyLimit;

        /// <inheritdoc/>
        public async Task<List<StockIndexData>> GetHistoricalDataAsync(string indexSymbol, DateTime startDate, DateTime endDate)
        {
            try
            {
                if (_remainingApiCalls <= 0)
                {
                    _logger.LogWarning("API call limit reached for StockData.org.");
                    throw new InvalidOperationException("API call limit reached for StockData.org.");
                }

                // StockData.org doesn't support historical data in the free tier, so we'd need to make multiple calls
                // This is a simplified implementation for testing purposes
                var url = $"v1/data/quote?symbols={indexSymbol}&api_token={_apiConfig.BackupApi.ApiToken}";
                var response = await _httpClient.GetAsync(url);

                // Check if the response was successful
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("StockData.org API returned error status code {StatusCode}: {ErrorContent}", 
                        (int)response.StatusCode, errorContent);
                    throw new InvalidOperationException($"StockData.org API returned error status code {(int)response.StatusCode}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();

                try
                {
                    // Check for error messages
                    var stockDataResponse = JsonSerializer.Deserialize<StockDataOrgResponse>(responseContent);
                    if (stockDataResponse?.Error != null)
                    {
                        _logger.LogError("StockData.org API error: {ErrorMessage}", stockDataResponse.Error.Message);
                        throw new InvalidOperationException($"StockData.org API error: {stockDataResponse.Error.Message}");
                    }
                    
                    // Only decrement the counter if the response is valid
                    _remainingApiCalls--;

                    var stockData = ParseStockDataOrgResponse(responseContent, indexSymbol);
                    
                    // Filter by date range
                    return stockData
                        .Where(data => data.Date >= startDate && data.Date <= endDate)
                        .OrderBy(data => data.Date)
                        .ToList();
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error parsing StockData.org API response for symbol {Symbol}", indexSymbol);
                    throw new InvalidOperationException($"Error parsing StockData.org API response: {ex.Message}", ex);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error fetching data from StockData.org API for symbol {Symbol}", indexSymbol);
                throw new InvalidOperationException($"Error fetching data from StockData.org API: {ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public async Task<StockIndexData> GetLatestDataAsync(string indexSymbol)
        {
            var endDate = DateTime.Today;
            var startDate = endDate.AddDays(-7); // Get a week of data to ensure we have the latest

            var data = await GetHistoricalDataAsync(indexSymbol, startDate, endDate);
            return data.OrderByDescending(d => d.Date).FirstOrDefault() ?? 
                   throw new InvalidOperationException($"No data available for {indexSymbol}");
        }

        /// <inheritdoc/>
        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                // Simple check to see if the API is responding
                var url = $"v1/data/quote?symbols=^GSPC&api_token={_apiConfig.BackupApi.ApiToken}";
                var response = await _httpClient.GetAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking StockData.org API availability");
                return false;
            }
        }

        /// <inheritdoc/>
        public Task<int> GetRemainingApiCallsAsync()
        {
            return Task.FromResult(_remainingApiCalls);
        }

        /// <summary>
        /// Parses the StockData.org API response into a list of StockIndexData
        /// </summary>
        /// <param name="response">The JSON response from the API</param>
        /// <param name="symbol">The stock index symbol</param>
        /// <returns>A list of stock index data points</returns>
        public List<StockIndexData> ParseStockDataOrgResponse(string response, string symbol)
        {
            var stockDataResponse = JsonSerializer.Deserialize<StockDataOrgResponse>(response);
            var result = new List<StockIndexData>();

            if (stockDataResponse?.Data == null || !stockDataResponse.Data.Any())
            {
                return result;
            }

            foreach (var quote in stockDataResponse.Data)
            {
                if (DateTime.TryParse(quote.Date, out var date))
                {
                    result.Add(new StockIndexData
                    {
                        Date = date,
                        IndexName = GetIndexNameFromSymbol(symbol),
                        OpenValue = quote.Open ?? 0,
                        HighValue = quote.High ?? 0,
                        LowValue = quote.Low ?? 0,
                        CloseValue = quote.Close ?? 0,
                        Volume = quote.Volume ?? 0,
                        FetchedAt = DateTime.Now
                    });
                }
            }

            return result.OrderBy(d => d.Date).ToList();
        }

        /// <summary>
        /// Gets the index name from the symbol
        /// </summary>
        /// <param name="symbol">The stock index symbol</param>
        /// <returns>The index name</returns>
        private string GetIndexNameFromSymbol(string symbol)
        {
            return symbol switch
            {
                "^GSPC" => "S&P 500",
                "^DJI" => "Dow Jones",
                "^IXIC" => "NASDAQ",
                _ => symbol
            };
        }
    }
}
