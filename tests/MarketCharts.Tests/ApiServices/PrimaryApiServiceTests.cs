using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MarketCharts.Client.Interfaces;
using MarketCharts.Client.Models;
using MarketCharts.Client.Models.ApiResponse;
using MarketCharts.Client.Models.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace MarketCharts.Tests.ApiServices
{
    public class PrimaryApiServiceTests
    {
        private readonly ApiConfiguration _apiConfig;
        private readonly Mock<ILogger<PrimaryApiService>> _loggerMock;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private readonly HttpClient _httpClient;

        public PrimaryApiServiceTests()
        {
            // Setup configuration
            _apiConfig = new ApiConfiguration
            {
                PrimaryApi = new AlphaVantageConfig
                {
                    ApiKey = "test_api_key",
                    BaseUrl = "https://www.alphavantage.co/query",
                    SP500Symbol = "^GSPC",
                    DowJonesSymbol = "^DJI",
                    NasdaqSymbol = "^IXIC",
                    DailyLimit = 25
                }
            };

            // Setup logger mock
            _loggerMock = new Mock<ILogger<PrimaryApiService>>();

            // Setup HTTP handler mock
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("https://www.alphavantage.co/")
            };
        }

        [Fact]
        public async Task Should_ReturnHistoricalData_When_ValidDateRangeProvided()
        {
            // Arrange
            var startDate = new DateTime(2025, 1, 20);
            var endDate = new DateTime(2025, 2, 20);
            var symbol = "^GSPC";

            var mockResponse = CreateMockAlphaVantageResponse(symbol, startDate, endDate);
            SetupMockHttpResponse(mockResponse);

            var service = new PrimaryApiService(_apiConfig, _httpClient, _loggerMock.Object);

            // Act
            var result = await service.GetHistoricalDataAsync(symbol, startDate, endDate);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.Equal(3, result.Count); // We created 3 data points in our mock
            Assert.All(result, data => Assert.Equal("S&P 500", data.IndexName));
            Assert.All(result, data => Assert.True(data.Date >= startDate && data.Date <= endDate));
        }

        [Fact]
        public async Task Should_ReturnLatestData_When_ValidSymbolProvided()
        {
            // Arrange
            var symbol = "^DJI";
            var today = DateTime.Today;

            var mockResponse = CreateMockAlphaVantageResponse(symbol, today.AddDays(-1), today);
            SetupMockHttpResponse(mockResponse);

            var service = new PrimaryApiService(_apiConfig, _httpClient, _loggerMock.Object);

            // Act
            var result = await service.GetLatestDataAsync(symbol);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Dow Jones", result.IndexName);
            Assert.Equal(today, result.Date);
        }

        [Fact]
        public async Task Should_ThrowException_When_InvalidSymbolProvided()
        {
            // Arrange
            var invalidSymbol = "INVALID";
            var errorResponse = new AlphaVantageResponse
            {
                ErrorMessage = "Invalid API call. Please retry or visit the documentation for TIME_SERIES_DAILY."
            };

            SetupMockHttpResponse(JsonSerializer.Serialize(errorResponse));

            var service = new PrimaryApiService(_apiConfig, _httpClient, _loggerMock.Object);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.GetHistoricalDataAsync(invalidSymbol, DateTime.Today.AddDays(-10), DateTime.Today));
            
            Assert.Contains("Invalid API call", exception.Message);
        }

        [Fact]
        public async Task Should_ThrowException_When_ApiLimitReached()
        {
            // Arrange
            var symbol = "^GSPC";
            var limitResponse = new AlphaVantageResponse
            {
                Note = "Thank you for using Alpha Vantage! Our standard API call frequency is 25 calls per day."
            };

            SetupMockHttpResponse(JsonSerializer.Serialize(limitResponse));

            var service = new PrimaryApiService(_apiConfig, _httpClient, _loggerMock.Object);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.GetHistoricalDataAsync(symbol, DateTime.Today.AddDays(-10), DateTime.Today));
            
            Assert.Contains("API call frequency", exception.Message);
        }

        [Fact]
        public void Should_ParseCorrectly_When_ValidJsonReceived()
        {
            // Arrange
            var symbol = "^IXIC";
            var date = DateTime.Today;
            var jsonResponse = $@"{{
                ""Meta Data"": {{
                    ""1. Information"": ""Daily Prices for the NASDAQ Composite Index"",
                    ""2. Symbol"": ""{symbol}"",
                    ""3. Last Refreshed"": ""{date:yyyy-MM-dd}"",
                    ""4. Output Size"": ""Compact"",
                    ""5. Time Zone"": ""US/Eastern""
                }},
                ""Time Series (Daily)"": {{
                    ""{date:yyyy-MM-dd}"": {{
                        ""1. open"": ""15000.00"",
                        ""2. high"": ""15200.00"",
                        ""3. low"": ""14800.00"",
                        ""4. close"": ""15100.00"",
                        ""5. volume"": ""1000000""
                    }}
                }}
            }}";

            SetupMockHttpResponse(jsonResponse);

            var service = new PrimaryApiService(_apiConfig, _httpClient, _loggerMock.Object);

            // Act
            var result = service.ParseAlphaVantageResponse(jsonResponse, symbol);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            var data = result[0];
            Assert.Equal("NASDAQ", data.IndexName);
            Assert.Equal(date.Date, data.Date.Date);
            Assert.Equal(15100.00m, data.CloseValue);
            Assert.Equal(15000.00m, data.OpenValue);
            Assert.Equal(15200.00m, data.HighValue);
            Assert.Equal(14800.00m, data.LowValue);
            Assert.Equal(1000000, data.Volume);
        }

        [Fact]
        public async Task Should_HandleEmptyResponse_When_NoDataAvailable()
        {
            // Arrange
            var symbol = "^GSPC";
            var emptyResponse = new AlphaVantageResponse
            {
                Metadata = new AlphaVantageMetadata
                {
                    Symbol = symbol,
                    Information = "Daily Prices for the S&P 500 Index"
                },
                TimeSeriesDaily = new Dictionary<string, AlphaVantageDailyData>()
            };

            SetupMockHttpResponse(JsonSerializer.Serialize(emptyResponse));

            var service = new PrimaryApiService(_apiConfig, _httpClient, _loggerMock.Object);

            // Act
            var result = await service.GetHistoricalDataAsync(symbol, DateTime.Today.AddDays(-10), DateTime.Today);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task Should_ReturnConsistentFormat_When_DifferentSymbolsRequested()
        {
            // Arrange
            var startDate = new DateTime(2025, 1, 20);
            var endDate = new DateTime(2025, 1, 22);
            
            // Test with S&P 500
            var sp500Response = CreateMockAlphaVantageResponse("^GSPC", startDate, endDate);
            SetupMockHttpResponse(sp500Response);
            var service = new PrimaryApiService(_apiConfig, _httpClient, _loggerMock.Object);
            var sp500Result = await service.GetHistoricalDataAsync("^GSPC", startDate, endDate);
            
            // Test with Dow Jones
            var dowResponse = CreateMockAlphaVantageResponse("^DJI", startDate, endDate);
            SetupMockHttpResponse(dowResponse);
            var dowResult = await service.GetHistoricalDataAsync("^DJI", startDate, endDate);
            
            // Test with NASDAQ
            var nasdaqResponse = CreateMockAlphaVantageResponse("^IXIC", startDate, endDate);
            SetupMockHttpResponse(nasdaqResponse);
            var nasdaqResult = await service.GetHistoricalDataAsync("^IXIC", startDate, endDate);

            // Assert
            Assert.Equal(3, sp500Result.Count);
            Assert.Equal(3, dowResult.Count);
            Assert.Equal(3, nasdaqResult.Count);
            
            // Check that all results have the same structure
            Assert.All(sp500Result, data => Assert.Equal("S&P 500", data.IndexName));
            Assert.All(dowResult, data => Assert.Equal("Dow Jones", data.IndexName));
            Assert.All(nasdaqResult, data => Assert.Equal("NASDAQ", data.IndexName));
            
            // Check that dates are consistent across all results
            for (int i = 0; i < 3; i++)
            {
                var date = startDate.AddDays(i);
                Assert.Equal(date.Date, sp500Result[i].Date.Date);
                Assert.Equal(date.Date, dowResult[i].Date.Date);
                Assert.Equal(date.Date, nasdaqResult[i].Date.Date);
            }
        }

        [Fact]
        public async Task Should_HandleRateLimiting_When_TooManyRequestsMade()
        {
            // Arrange
            var symbol = "^GSPC";
            
            // First call succeeds
            var successResponse = CreateMockAlphaVantageResponse(symbol, DateTime.Today.AddDays(-1), DateTime.Today);
            SetupMockHttpResponse(successResponse);
            
            var service = new PrimaryApiService(_apiConfig, _httpClient, _loggerMock.Object);
            
            // First call should succeed
            var firstResult = await service.GetHistoricalDataAsync(symbol, DateTime.Today.AddDays(-1), DateTime.Today);
            Assert.NotNull(firstResult);
            Assert.NotEmpty(firstResult);
            
            // Second call hits rate limit
            var limitResponse = new AlphaVantageResponse
            {
                Note = "Thank you for using Alpha Vantage! Our standard API call frequency is 25 calls per day."
            };
            SetupMockHttpResponse(JsonSerializer.Serialize(limitResponse));
            
            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.GetHistoricalDataAsync(symbol, DateTime.Today.AddDays(-10), DateTime.Today));
            
            Assert.Contains("API call frequency", exception.Message);
            
            // Verify remaining calls is updated (should be 24 after one successful call)
            var remainingCalls = await service.GetRemainingApiCallsAsync();
            Assert.Equal(24, remainingCalls);
        }

        [Fact]
        public async Task Should_ReturnPartialData_When_DateRangePartiallyAvailable()
        {
            // Arrange
            var symbol = "^GSPC";
            var startDate = new DateTime(2025, 1, 20);
            var endDate = new DateTime(2025, 1, 25);
            
            // Create response with only 3 days of data even though 6 days were requested
            var partialResponse = new AlphaVantageResponse
            {
                Metadata = new AlphaVantageMetadata
                {
                    Symbol = symbol,
                    Information = "Daily Prices for the S&P 500 Index",
                    LastRefreshed = endDate.ToString("yyyy-MM-dd")
                },
                TimeSeriesDaily = new Dictionary<string, AlphaVantageDailyData>
                {
                    [endDate.ToString("yyyy-MM-dd")] = CreateMockDailyData(4800, 4850, 4750, 4820, 1000000),
                    [endDate.AddDays(-1).ToString("yyyy-MM-dd")] = CreateMockDailyData(4780, 4830, 4760, 4800, 950000),
                    [endDate.AddDays(-2).ToString("yyyy-MM-dd")] = CreateMockDailyData(4750, 4800, 4700, 4780, 900000)
                }
            };
            
            SetupMockHttpResponse(JsonSerializer.Serialize(partialResponse));
            
            var service = new PrimaryApiService(_apiConfig, _httpClient, _loggerMock.Object);
            
            // Act
            var result = await service.GetHistoricalDataAsync(symbol, startDate, endDate);
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count); // Only 3 days of data available
            Assert.All(result, data => Assert.Equal("S&P 500", data.IndexName));
            
            // Verify the dates are correct
            Assert.Contains(result, data => data.Date.Date == endDate.Date);
            Assert.Contains(result, data => data.Date.Date == endDate.AddDays(-1).Date);
            Assert.Contains(result, data => data.Date.Date == endDate.AddDays(-2).Date);
            
            // Verify we don't have data for earlier dates
            Assert.DoesNotContain(result, data => data.Date.Date == startDate.Date);
        }

        [Fact]
        public async Task Should_HandleApiKeyAuthentication_When_MakingRequests()
        {
            // Arrange
            var symbol = "^GSPC";
            var startDate = new DateTime(2025, 1, 20);
            var endDate = new DateTime(2025, 1, 22);
            
            var mockResponse = CreateMockAlphaVantageResponse(symbol, startDate, endDate);
            
            // Setup handler to verify API key is included in request
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => 
                        req.RequestUri.ToString().Contains("apikey=test_api_key")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(mockResponse)
                });
            
            var service = new PrimaryApiService(_apiConfig, _httpClient, _loggerMock.Object);
            
            // Act
            var result = await service.GetHistoricalDataAsync(symbol, startDate, endDate);
            
            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            
            // Verify the API key was included in the request
            _httpMessageHandlerMock
                .Protected()
                .Verify<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Times.Once(),
                    ItExpr.Is<HttpRequestMessage>(req => 
                        req.RequestUri.ToString().Contains("apikey=test_api_key")),
                    ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task Should_LogApiErrors_When_ApiResponseContainsErrorCodes()
        {
            // Arrange
            var symbol = "^GSPC";
            var errorResponse = new AlphaVantageResponse
            {
                ErrorMessage = "Invalid API call. Please retry or visit the documentation for TIME_SERIES_DAILY."
            };
            
            SetupMockHttpResponse(JsonSerializer.Serialize(errorResponse));
            
            var service = new PrimaryApiService(_apiConfig, _httpClient, _loggerMock.Object);
            
            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.GetHistoricalDataAsync(symbol, DateTime.Today.AddDays(-10), DateTime.Today));
            
            // Verify error was logged
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Invalid API call")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Should_ThrowException_When_NetworkErrorOccurs()
        {
            // Arrange
            var symbol = "^GSPC";
            
            // Setup HTTP handler to simulate a network error
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error occurred"));
            
            var service = new PrimaryApiService(_apiConfig, _httpClient, _loggerMock.Object);
            
            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.GetHistoricalDataAsync(symbol, DateTime.Today.AddDays(-10), DateTime.Today));
            
            Assert.Contains("Network error", exception.Message);
            
            // Verify error was logged
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.Is<Exception>(ex => ex.Message.Contains("Network error")),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Should_ThrowException_When_HttpErrorResponseReceived()
        {
            // Arrange
            var symbol = "^GSPC";
            
            // Setup HTTP handler to return a 500 Internal Server Error
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Content = new StringContent("Internal Server Error")
                });
            
            var service = new PrimaryApiService(_apiConfig, _httpClient, _loggerMock.Object);
            
            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.GetHistoricalDataAsync(symbol, DateTime.Today.AddDays(-10), DateTime.Today));
            
            Assert.Contains("500", exception.Message);
            
            // Verify error was logged
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Should_ThrowException_When_MalformedJsonResponseReceived()
        {
            // Arrange
            var symbol = "^GSPC";
            
            // Setup HTTP handler to return malformed JSON
            SetupMockHttpResponse("{ This is not valid JSON }");
            
            var service = new PrimaryApiService(_apiConfig, _httpClient, _loggerMock.Object);
            
            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.GetHistoricalDataAsync(symbol, DateTime.Today.AddDays(-10), DateTime.Today));
            
            Assert.Contains("Error parsing", exception.Message);
            
            // Verify error was logged
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #region Helper Methods

        private void SetupMockHttpResponse(string content)
        {
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(content)
                });
        }

        private string CreateMockAlphaVantageResponse(string symbol, DateTime startDate, DateTime endDate)
        {
            var response = new AlphaVantageResponse
            {
                Metadata = new AlphaVantageMetadata
                {
                    Symbol = symbol,
                    Information = GetInformationForSymbol(symbol),
                    LastRefreshed = endDate.ToString("yyyy-MM-dd"),
                    OutputSize = "Compact",
                    TimeZone = "US/Eastern"
                },
                TimeSeriesDaily = new Dictionary<string, AlphaVantageDailyData>()
            };

            // Add only 3 data points regardless of the date range
            for (int i = 0; i < 3; i++)
            {
                var date = startDate.AddDays(i);
                if (date <= endDate) // Make sure we don't go beyond the end date
                {
                    response.TimeSeriesDaily[date.ToString("yyyy-MM-dd")] = CreateMockDailyData(
                        baseValue: GetBaseValueForSymbol(symbol),
                        dayOffset: i
                    );
                }
            }

            return JsonSerializer.Serialize(response);
        }

        private AlphaVantageDailyData CreateMockDailyData(decimal open, decimal high, decimal low, decimal close, long volume)
        {
            return new AlphaVantageDailyData
            {
                Open = open.ToString(),
                High = high.ToString(),
                Low = low.ToString(),
                Close = close.ToString(),
                Volume = volume.ToString()
            };
        }

        private AlphaVantageDailyData CreateMockDailyData(decimal baseValue, int dayOffset = 0)
        {
            // Create some realistic looking data with slight variations
            var open = baseValue + (dayOffset * 10);
            var high = open + 50;
            var low = open - 50;
            var close = open + 20;
            var volume = 1000000 - (dayOffset * 50000);

            return CreateMockDailyData(open, high, low, close, volume);
        }

        private string GetInformationForSymbol(string symbol)
        {
            return symbol switch
            {
                "^GSPC" => "Daily Prices for the S&P 500 Index",
                "^DJI" => "Daily Prices for the Dow Jones Industrial Average",
                "^IXIC" => "Daily Prices for the NASDAQ Composite Index",
                _ => "Daily Prices"
            };
        }

        private decimal GetBaseValueForSymbol(string symbol)
        {
            return symbol switch
            {
                "^GSPC" => 4800m, // S&P 500
                "^DJI" => 38000m, // Dow Jones
                "^IXIC" => 15000m, // NASDAQ
                _ => 1000m
            };
        }

        #endregion
    }

    // Mock implementation of PrimaryApiService for testing
    public class PrimaryApiService : IStockApiService
    {
        private readonly ApiConfiguration _apiConfig;
        private readonly HttpClient _httpClient;
        private readonly ILogger<PrimaryApiService> _logger;
        private int _remainingApiCalls;

        public PrimaryApiService(ApiConfiguration apiConfig, HttpClient httpClient, ILogger<PrimaryApiService> logger)
        {
            _apiConfig = apiConfig;
            _httpClient = httpClient;
            _logger = logger;
            _remainingApiCalls = apiConfig.PrimaryApi.DailyLimit;
        }

        public string ServiceName => "Alpha Vantage";

        public int ApiCallLimit => _apiConfig.PrimaryApi.DailyLimit;

        public async Task<List<StockIndexData>> GetHistoricalDataAsync(string indexSymbol, DateTime startDate, DateTime endDate)
        {
            try
            {
                if (_remainingApiCalls <= 0)
                {
                    throw new InvalidOperationException("API call limit reached for Alpha Vantage.");
                }

                var url = $"query?function=TIME_SERIES_DAILY&symbol={indexSymbol}&outputsize=full&apikey={_apiConfig.PrimaryApi.ApiKey}";
                var response = await _httpClient.GetAsync(url);
                _remainingApiCalls--;

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

        public async Task<StockIndexData> GetLatestDataAsync(string indexSymbol)
        {
            var endDate = DateTime.Today;
            var startDate = endDate.AddDays(-7); // Get a week of data to ensure we have the latest

            var data = await GetHistoricalDataAsync(indexSymbol, startDate, endDate);
            return data.OrderByDescending(d => d.Date).FirstOrDefault() ?? 
                   throw new InvalidOperationException($"No data available for {indexSymbol}");
        }

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                // Simple check to see if the API is responding
                var url = $"query?function=GLOBAL_QUOTE&symbol=^GSPC&apikey={_apiConfig.PrimaryApi.ApiKey}";
                var response = await _httpClient.GetAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public Task<int> GetRemainingApiCallsAsync()
        {
            return Task.FromResult(_remainingApiCalls);
        }

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
