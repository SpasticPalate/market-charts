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
    /// Implementation of the primary stock API service using Alpha Vantage
    /// </summary>
    public class PrimaryApiService : IStockApiService
    {
        private readonly ApiConfiguration _apiConfig;
        private readonly HttpClient _httpClient;
        private readonly ILogger<PrimaryApiService> _logger;
        private int _remainingApiCalls;

        /// <summary>
        /// Initializes a new instance of the PrimaryApiService class
        /// </summary>
        /// <param name="apiConfig">The API configuration</param>
        /// <param name="httpClient">The HTTP client</param>
        /// <param name="logger">The logger</param>
        public PrimaryApiService(ApiConfiguration apiConfig, HttpClient httpClient, ILogger<PrimaryApiService> logger)
        {
            _apiConfig = apiConfig ?? throw new ArgumentNullException(nameof(apiConfig));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _remainingApiCalls = apiConfig.PrimaryApi.DailyLimit;
            
            // Ensure the HttpClient has the correct base address
            if (_httpClient.BaseAddress == null)
            {
                _httpClient.BaseAddress = new Uri("https://www.alphavantage.co/");
            }
        }

        /// <inheritdoc/>
        public string ServiceName => "Alpha Vantage";

        /// <inheritdoc/>
        public int ApiCallLimit => _apiConfig.PrimaryApi.DailyLimit;

        /// <inheritdoc/>
        public async Task<List<StockIndexData>> GetHistoricalDataAsync(string indexSymbol, DateTime startDate, DateTime endDate)
        {
            try
            {
                if (_remainingApiCalls <= 0)
                {
                    _logger.LogWarning("API call limit reached for Alpha Vantage.");
                    throw new InvalidOperationException("API call limit reached for Alpha Vantage.");
                }

                var url = $"query?function=TIME_SERIES_DAILY&symbol={indexSymbol}&outputsize=full&apikey={_apiConfig.PrimaryApi.ApiKey}";
                var response = await _httpClient.GetAsync(url);

                // Check if the response was successful
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Alpha Vantage API returned error status code {StatusCode}: {ErrorContent}", 
                        (int)response.StatusCode, errorContent);
                    throw new InvalidOperationException($"Alpha Vantage API returned error status code {(int)response.StatusCode}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();

                try
                {
                    // Check for error or rate limit messages
                    var alphaVantageResponse = JsonSerializer.Deserialize<AlphaVantageResponse>(responseContent);
                    if (!string.IsNullOrEmpty(alphaVantageResponse?.ErrorMessage))
                    {
                        _logger.LogError("Alpha Vantage API error: {ErrorMessage}", alphaVantageResponse.ErrorMessage);
                        throw new InvalidOperationException($"Alpha Vantage API error: {alphaVantageResponse.ErrorMessage}");
                    }

                    if (!string.IsNullOrEmpty(alphaVantageResponse?.Note) && alphaVantageResponse.Note.Contains("API call frequency"))
                    {
                        _logger.LogWarning("Alpha Vantage API rate limit reached: {Note}", alphaVantageResponse.Note);
                        throw new InvalidOperationException($"Alpha Vantage API rate limit reached: {alphaVantageResponse.Note}");
                    }
                    
                    // Only decrement the counter if the response is valid
                    _remainingApiCalls--;

                    var stockData = ParseAlphaVantageResponse(responseContent, indexSymbol);
                    
                    // Filter by date range
                    return stockData
                        .Where(data => data.Date >= startDate && data.Date <= endDate)
                        .OrderBy(data => data.Date)
                        .ToList();
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error parsing Alpha Vantage API response for symbol {Symbol}", indexSymbol);
                    throw new InvalidOperationException($"Error parsing Alpha Vantage API response: {ex.Message}", ex);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error fetching data from Alpha Vantage API for symbol {Symbol}", indexSymbol);
                throw new InvalidOperationException($"Error fetching data from Alpha Vantage API: {ex.Message}", ex);
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
                var url = $"query?function=GLOBAL_QUOTE&symbol=^GSPC&apikey={_apiConfig.PrimaryApi.ApiKey}";
                var response = await _httpClient.GetAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Alpha Vantage API availability");
                return false;
            }
        }

        /// <inheritdoc/>
        public Task<int> GetRemainingApiCallsAsync()
        {
            return Task.FromResult(_remainingApiCalls);
        }

        /// <summary>
        /// Parses the Alpha Vantage API response into a list of StockIndexData
        /// </summary>
        /// <param name="response">The JSON response from the API</param>
        /// <param name="symbol">The stock index symbol</param>
        /// <returns>A list of stock index data points</returns>
        public List<StockIndexData> ParseAlphaVantageResponse(string response, string symbol)
        {
            var alphaVantageResponse = JsonSerializer.Deserialize<AlphaVantageResponse>(response);
            var result = new List<StockIndexData>();

            if (alphaVantageResponse?.TimeSeriesDaily == null || !alphaVantageResponse.TimeSeriesDaily.Any())
            {
                return result;
            }

            foreach (var (dateStr, data) in alphaVantageResponse.TimeSeriesDaily)
            {
                if (DateTime.TryParse(dateStr, out var date) && 
                    decimal.TryParse(data.Open, out var open) &&
                    decimal.TryParse(data.High, out var high) &&
                    decimal.TryParse(data.Low, out var low) &&
                    decimal.TryParse(data.Close, out var close) &&
                    long.TryParse(data.Volume, out var volume))
                {
                    result.Add(new StockIndexData
                    {
                        Date = date,
                        IndexName = GetIndexNameFromSymbol(symbol),
                        OpenValue = open,
                        HighValue = high,
                        LowValue = low,
                        CloseValue = close,
                        Volume = volume,
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
