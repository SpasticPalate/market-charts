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
    public class BackupApiServiceTests
    {
        private readonly ApiConfiguration _apiConfig;
        private readonly Mock<ILogger<BackupApiService>> _loggerMock;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private readonly HttpClient _httpClient;

        public BackupApiServiceTests()
        {
            // Setup configuration
            _apiConfig = new ApiConfiguration
            {
                BackupApi = new StockDataOrgConfig
                {
                    ApiToken = "test_api_token",
                    BaseUrl = "https://api.stockdata.org/v1/data/quote",
                    SP500Symbol = "^GSPC",
                    DowJonesSymbol = "^DJI",
                    NasdaqSymbol = "^IXIC",
                    DailyLimit = 100
                }
            };

            // Setup logger mock
            _loggerMock = new Mock<ILogger<BackupApiService>>();

            // Setup HTTP handler mock
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("https://api.stockdata.org/")
            };
        }

        [Fact]
        public async Task Should_ReturnHistoricalData_When_ValidDateRangeProvided()
        {
            // Arrange
            var startDate = new DateTime(2025, 1, 20);
            var endDate = new DateTime(2025, 2, 20);
            var symbol = "^GSPC";

            var mockResponse = CreateMockStockDataOrgResponse(symbol, startDate, endDate);
            SetupMockHttpResponse(mockResponse);

            var service = new BackupApiService(_apiConfig, _httpClient, _loggerMock.Object);

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

            // Create a response with today's date specifically
            var response = new StockDataOrgResponse
            {
                Meta = new StockDataOrgMeta
                {
                    Requested = 1,
                    Returned = 1,
                    Status = 200,
                    Message = "Success"
                },
                Data = new List<StockDataOrgQuote>
                {
                    new StockDataOrgQuote
                    {
                        Symbol = symbol,
                        Name = "Dow Jones Industrial Average",
                        Exchange = "NYSE",
                        Open = 38000m,
                        High = 38050m,
                        Low = 37950m,
                        Close = 38020m,
                        Volume = 1000000,
                        Date = today.ToString("yyyy-MM-dd"),
                        PreviousClose = 38000m,
                        Change = 20m,
                        ChangePercent = 0.05m
                    }
                }
            };
            
            var mockResponse = JsonSerializer.Serialize(response);
            SetupMockHttpResponse(mockResponse);

            var service = new BackupApiService(_apiConfig, _httpClient, _loggerMock.Object);

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
            var errorResponse = new StockDataOrgResponse
            {
                Error = new StockDataOrgError
                {
                    Code = "invalid_symbol",
                    Message = "The symbol provided is not valid or not supported."
                }
            };

            SetupMockHttpResponse(JsonSerializer.Serialize(errorResponse));

            var service = new BackupApiService(_apiConfig, _httpClient, _loggerMock.Object);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.GetHistoricalDataAsync(invalidSymbol, DateTime.Today.AddDays(-10), DateTime.Today));
            
            Assert.Contains("not valid", exception.Message);
        }

        [Fact]
        public void Should_ParseCorrectly_When_ValidJsonReceived()
        {
            // Arrange
            var symbol = "^IXIC";
            var date = DateTime.Today;
            var jsonResponse = $@"{{
                ""meta"": {{
                    ""requested"": 1,
                    ""returned"": 1,
                    ""status"": 200,
                    ""message"": ""Success""
                }},
                ""data"": [
                    {{
                        ""symbol"": ""{symbol}"",
                        ""name"": ""NASDAQ Composite"",
                        ""exchange"": ""NASDAQ"",
                        ""open"": 15000.00,
                        ""high"": 15200.00,
                        ""low"": 14800.00,
                        ""close"": 15100.00,
                        ""volume"": 1000000,
                        ""date"": ""{date:yyyy-MM-dd}"",
                        ""previous_close"": 14900.00,
                        ""change"": 200.00,
                        ""change_percent"": 1.34
                    }}
                ]
            }}";

            var service = new BackupApiService(_apiConfig, _httpClient, _loggerMock.Object);

            // Act
            var result = service.ParseStockDataOrgResponse(jsonResponse, symbol);

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
            var emptyResponse = new StockDataOrgResponse
            {
                Meta = new StockDataOrgMeta
                {
                    Requested = 1,
                    Returned = 0,
                    Status = 200,
                    Message = "No data available for the requested symbol and date range."
                },
                Data = new List<StockDataOrgQuote>()
            };

            SetupMockHttpResponse(JsonSerializer.Serialize(emptyResponse));

            var service = new BackupApiService(_apiConfig, _httpClient, _loggerMock.Object);

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
            var sp500Response = CreateMockStockDataOrgResponse("^GSPC", startDate, endDate);
            SetupMockHttpResponse(sp500Response);
            var service = new BackupApiService(_apiConfig, _httpClient, _loggerMock.Object);
            var sp500Result = await service.GetHistoricalDataAsync("^GSPC", startDate, endDate);
            
            // Test with Dow Jones
            var dowResponse = CreateMockStockDataOrgResponse("^DJI", startDate, endDate);
            SetupMockHttpResponse(dowResponse);
            var dowResult = await service.GetHistoricalDataAsync("^DJI", startDate, endDate);
            
            // Test with NASDAQ
            var nasdaqResponse = CreateMockStockDataOrgResponse("^IXIC", startDate, endDate);
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
        public async Task Should_HandleStockDataOrgApiResponse_When_UsingBackupProvider()
        {
            // Arrange
            var symbol = "^GSPC";
            var date = DateTime.Today;
            
            // Create a response with the specific StockData.org format
            var response = new StockDataOrgResponse
            {
                Meta = new StockDataOrgMeta
                {
                    Requested = 1,
                    Returned = 1,
                    Status = 200,
                    Message = "Success"
                },
                Data = new List<StockDataOrgQuote>
                {
                    new StockDataOrgQuote
                    {
                        Symbol = symbol,
                        Name = "S&P 500",
                        Exchange = "NYSE",
                        Open = 4800m,
                        High = 4850m,
                        Low = 4750m,
                        Close = 4820m,
                        Volume = 1000000,
                        Date = date.ToString("yyyy-MM-dd"),
                        PreviousClose = 4780m,
                        Change = 40m,
                        ChangePercent = 0.84m
                    }
                }
            };
            
            SetupMockHttpResponse(JsonSerializer.Serialize(response));
            
            var service = new BackupApiService(_apiConfig, _httpClient, _loggerMock.Object);
            
            // Act
            var result = await service.GetLatestDataAsync(symbol);
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal("S&P 500", result.IndexName);
            Assert.Equal(date.Date, result.Date.Date);
            Assert.Equal(4820m, result.CloseValue);
            Assert.Equal(4800m, result.OpenValue);
            Assert.Equal(4850m, result.HighValue);
            Assert.Equal(4750m, result.LowValue);
            Assert.Equal(1000000, result.Volume);
        }

        [Fact]
        public async Task Should_ReturnPartialData_When_DateRangePartiallyAvailable()
        {
            // Arrange
            var symbol = "^GSPC";
            var startDate = new DateTime(2025, 1, 20);
            var endDate = new DateTime(2025, 1, 25);
            
            // Create response with only 3 days of data even though 6 days were requested
            var partialResponse = new StockDataOrgResponse
            {
                Meta = new StockDataOrgMeta
                {
                    Requested = 6,
                    Returned = 3,
                    Status = 200,
                    Message = "Partial data available"
                },
                Data = new List<StockDataOrgQuote>
                {
                    CreateMockStockDataOrgQuote(symbol, endDate, 4820m, 4800m, 4850m, 4750m, 1000000),
                    CreateMockStockDataOrgQuote(symbol, endDate.AddDays(-1), 4800m, 4780m, 4830m, 4760m, 950000),
                    CreateMockStockDataOrgQuote(symbol, endDate.AddDays(-2), 4780m, 4750m, 4800m, 4700m, 900000)
                }
            };
            
            SetupMockHttpResponse(JsonSerializer.Serialize(partialResponse));
            
            var service = new BackupApiService(_apiConfig, _httpClient, _loggerMock.Object);
            
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
        public async Task Should_HandleApiAuthentication_When_UsingBackupProvider()
        {
            // Arrange
            var symbol = "^GSPC";
            var startDate = new DateTime(2025, 1, 20);
            var endDate = new DateTime(2025, 1, 22);
            
            var mockResponse = CreateMockStockDataOrgResponse(symbol, startDate, endDate);
            
            // Setup handler to verify API token is included in request
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => 
                        req.RequestUri.ToString().Contains("api_token=test_api_token")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(mockResponse)
                });
            
            var service = new BackupApiService(_apiConfig, _httpClient, _loggerMock.Object);
            
            // Act
            var result = await service.GetHistoricalDataAsync(symbol, startDate, endDate);
            
            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            
            // Verify the API token was included in the request
            _httpMessageHandlerMock
                .Protected()
                .Verify<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Times.Once(),
                    ItExpr.Is<HttpRequestMessage>(req => 
                        req.RequestUri.ToString().Contains("api_token=test_api_token")),
                    ItExpr.IsAny<CancellationToken>());
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
            
            var service = new BackupApiService(_apiConfig, _httpClient, _loggerMock.Object);
            
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
            
            var service = new BackupApiService(_apiConfig, _httpClient, _loggerMock.Object);
            
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
            
            var service = new BackupApiService(_apiConfig, _httpClient, _loggerMock.Object);
            
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

        [Fact]
        public async Task Should_ThrowException_When_ApiRateLimitReached()
        {
            // Arrange
            var symbol = "^GSPC";
            
            // Create a service with 0 remaining API calls
            var apiConfig = new ApiConfiguration
            {
                BackupApi = new StockDataOrgConfig
                {
                    ApiToken = "test_api_token",
                    BaseUrl = "https://api.stockdata.org/v1/data/quote",
                    DailyLimit = 0 // Set to 0 to simulate rate limit reached
                }
            };
            
            var service = new BackupApiService(apiConfig, _httpClient, _loggerMock.Object);
            
            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.GetHistoricalDataAsync(symbol, DateTime.Today.AddDays(-10), DateTime.Today));
            
            Assert.Contains("API call limit reached", exception.Message);
            
            // Verify warning was logged
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("API call limit reached")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Should_LogApiErrors_When_ApiResponseContainsErrorCodes()
        {
            // Arrange
            var symbol = "^GSPC";
            var errorResponse = new StockDataOrgResponse
            {
                Error = new StockDataOrgError
                {
                    Code = "invalid_symbol",
                    Message = "The symbol provided is not valid or not supported."
                }
            };
            
            SetupMockHttpResponse(JsonSerializer.Serialize(errorResponse));
            
            var service = new BackupApiService(_apiConfig, _httpClient, _loggerMock.Object);
            
            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.GetHistoricalDataAsync(symbol, DateTime.Today.AddDays(-10), DateTime.Today));
            
            // Verify error was logged
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("not valid")),
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

        private string CreateMockStockDataOrgResponse(string symbol, DateTime startDate, DateTime endDate)
        {
            var response = new StockDataOrgResponse
            {
                Meta = new StockDataOrgMeta
                {
                    Requested = 3, // Always request 3 data points
                    Returned = 3,  // Always return 3 data points
                    Status = 200,
                    Message = "Success"
                },
                Data = new List<StockDataOrgQuote>()
            };

            // Add only 3 data points regardless of the date range
            for (int i = 0; i < 3; i++)
            {
                var date = startDate.AddDays(i);
                if (date <= endDate) // Make sure we don't go beyond the end date
                {
                    var baseValue = GetBaseValueForSymbol(symbol);
                    
                    response.Data.Add(CreateMockStockDataOrgQuote(
                        symbol,
                        date,
                        baseValue + (i * 10) + 20, // close
                        baseValue + (i * 10),      // open
                        baseValue + (i * 10) + 50, // high
                        baseValue + (i * 10) - 50, // low
                        1000000 - (i * 50000)      // volume
                    ));
                }
            }

            return JsonSerializer.Serialize(response);
        }

        private StockDataOrgQuote CreateMockStockDataOrgQuote(string symbol, DateTime date, decimal close, decimal open, decimal high, decimal low, long volume)
        {
            return new StockDataOrgQuote
            {
                Symbol = symbol,
                Name = GetNameForSymbol(symbol),
                Exchange = GetExchangeForSymbol(symbol),
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume,
                Date = date.ToString("yyyy-MM-dd"),
                PreviousClose = close - 20,
                Change = 20,
                ChangePercent = 0.5m
            };
        }

        private string GetNameForSymbol(string symbol)
        {
            return symbol switch
            {
                "^GSPC" => "S&P 500",
                "^DJI" => "Dow Jones Industrial Average",
                "^IXIC" => "NASDAQ Composite",
                _ => "Unknown Index"
            };
        }

        private string GetExchangeForSymbol(string symbol)
        {
            return symbol switch
            {
                "^GSPC" => "NYSE",
                "^DJI" => "NYSE",
                "^IXIC" => "NASDAQ",
                _ => "Unknown"
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

    // Mock implementation of BackupApiService for testing
    public class BackupApiService : IStockApiService
    {
        private readonly ApiConfiguration _apiConfig;
        private readonly HttpClient _httpClient;
        private readonly ILogger<BackupApiService> _logger;
        private int _remainingApiCalls;

        public BackupApiService(ApiConfiguration apiConfig, HttpClient httpClient, ILogger<BackupApiService> logger)
        {
            _apiConfig = apiConfig;
            _httpClient = httpClient;
            _logger = logger;
            _remainingApiCalls = apiConfig.BackupApi.DailyLimit;
        }

        public string ServiceName => "StockData.org";

        public int ApiCallLimit => _apiConfig.BackupApi.DailyLimit;

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
                _remainingApiCalls--;

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

        public async Task<StockIndexData> GetLatestDataAsync(string indexSymbol)
        {
            try
            {
                if (_remainingApiCalls <= 0)
                {
                    _logger.LogWarning("API call limit reached for StockData.org.");
                    throw new InvalidOperationException("API call limit reached for StockData.org.");
                }

                var url = $"v1/data/quote?symbols={indexSymbol}&api_token={_apiConfig.BackupApi.ApiToken}";
                var response = await _httpClient.GetAsync(url);
                _remainingApiCalls--;

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

                    var stockData = ParseStockDataOrgResponse(responseContent, indexSymbol);
                    return stockData.FirstOrDefault() ?? 
                           throw new InvalidOperationException($"No data available for {indexSymbol}");
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

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                // Simple check to see if the API is responding
                var url = $"v1/data/quote?symbols=^GSPC&api_token={_apiConfig.BackupApi.ApiToken}";
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
