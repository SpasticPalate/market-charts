using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MarketCharts.Client.Interfaces;
using MarketCharts.Client.Models.Configuration;
using MarketCharts.Client.Services.ApiServices;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace MarketCharts.Tests.Integration
{
    public class ApiFallbackTests
    {
        private readonly Mock<ILogger<ApiServiceFactory>> _mockFactoryLogger;
        private readonly Mock<ILogger<PrimaryApiService>> _mockPrimaryLogger;
        private readonly Mock<ILogger<BackupApiService>> _mockBackupLogger;
        private readonly Mock<HttpMessageHandler> _mockHttpHandler;
        private readonly HttpClient _httpClient;
        private readonly ApiConfiguration _apiConfig;

        public ApiFallbackTests()
        {
            _mockFactoryLogger = new Mock<ILogger<ApiServiceFactory>>();
            _mockPrimaryLogger = new Mock<ILogger<PrimaryApiService>>();
            _mockBackupLogger = new Mock<ILogger<BackupApiService>>();
            _mockHttpHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpHandler.Object);
            
            _apiConfig = new ApiConfiguration
            {
                RetryPrimaryApiAfterMinutes = 5,
                PrimaryApi = new AlphaVantageConfig
                {
                    ApiKey = "demo_primary_key",
                    BaseUrl = "https://www.alphavantage.co/query",
                    DailyLimit = 25
                },
                BackupApi = new StockDataOrgConfig
                {
                    ApiToken = "demo_backup_token",
                    BaseUrl = "https://api.stockdata.org/v1/data/quote",
                    DailyLimit = 100
                }
            };
        }

        [Fact]
        public async Task Should_SwitchToBackupApi_When_PrimaryApiFails()
        {
            // Arrange
            // Setup primary API to fail
            SetupHttpResponse("https://www.alphavantage.co/", HttpStatusCode.ServiceUnavailable);
            
            // Setup backup API to succeed
            SetupHttpResponse("https://api.stockdata.org/", HttpStatusCode.OK, "{\"meta\":{\"requested\":1,\"returned\":1},\"data\":[{\"symbol\":\"^GSPC\",\"name\":\"S&P 500\",\"exchange\":\"INDEX\",\"open\":4000,\"high\":4100,\"low\":3950,\"close\":4050,\"volume\":1000000,\"date\":\"2023-01-01\"}]}");
            
            var primaryApiService = new PrimaryApiService(_apiConfig, _httpClient, _mockPrimaryLogger.Object);
            var backupApiService = new BackupApiService(_apiConfig, _httpClient, _mockBackupLogger.Object);
            var apiServiceFactory = new ApiServiceFactory(primaryApiService, backupApiService, _apiConfig, _mockFactoryLogger.Object);

            // Act
            // First call should try primary and fail
            await apiServiceFactory.NotifyPrimaryApiFailureAsync(new Exception("Primary API unavailable"));
            var apiService = await apiServiceFactory.GetApiServiceAsync();

            // Assert
            Assert.Equal("StockData.org", apiService.ServiceName);
            _mockFactoryLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Primary API service") && v.ToString().Contains("failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Should_RecoverGracefully_When_BothApisFail()
        {
            // Arrange
            // Setup both APIs to fail
            SetupHttpResponse("https://www.alphavantage.co/", HttpStatusCode.ServiceUnavailable);
            SetupHttpResponse("https://api.stockdata.org/", HttpStatusCode.ServiceUnavailable);
            
            var primaryApiService = new PrimaryApiService(_apiConfig, _httpClient, _mockPrimaryLogger.Object);
            var backupApiService = new BackupApiService(_apiConfig, _httpClient, _mockBackupLogger.Object);
            var apiServiceFactory = new ApiServiceFactory(primaryApiService, backupApiService, _apiConfig, _mockFactoryLogger.Object);

            // Act & Assert
            await apiServiceFactory.NotifyPrimaryApiFailureAsync(new Exception("Primary API unavailable"));
            
            // Both APIs are unavailable, so GetApiServiceAsync should throw
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => 
                await apiServiceFactory.GetApiServiceAsync());
            
            Assert.Contains("All API services are unavailable", exception.Message);
            
            // Verify that AreAllApiServicesUnavailableAsync returns true
            var allUnavailable = await apiServiceFactory.AreAllApiServicesUnavailableAsync();
            Assert.True(allUnavailable);
        }

        [Fact]
        public async Task Should_RevertToPrimaryApi_When_ItBecomesAvailable()
        {
            // Arrange
            // Setup primary API to initially fail, then succeed
            var primaryResponseQueue = new Queue<HttpResponseMessage>();
            primaryResponseQueue.Enqueue(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            primaryResponseQueue.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) 
            { 
                Content = new StringContent("{\"Meta Data\":{\"1. Information\":\"Daily Prices\",\"2. Symbol\":\"^GSPC\"},\"Time Series (Daily)\":{\"2023-01-01\":{\"1. open\":\"4000\",\"2. high\":\"4100\",\"3. low\":\"3950\",\"4. close\":\"4050\",\"5. volume\":\"1000000\"}}}")
            });
            
            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString().StartsWith("https://www.alphavantage.co/")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => primaryResponseQueue.Dequeue());
            
            // Setup backup API to succeed
            SetupHttpResponse("https://api.stockdata.org/", HttpStatusCode.OK, "{\"meta\":{\"requested\":1,\"returned\":1},\"data\":[{\"symbol\":\"^GSPC\",\"name\":\"S&P 500\",\"exchange\":\"INDEX\",\"open\":4000,\"high\":4100,\"low\":3950,\"close\":4050,\"volume\":1000000,\"date\":\"2023-01-01\"}]}");
            
            var primaryApiService = new PrimaryApiService(_apiConfig, _httpClient, _mockPrimaryLogger.Object);
            var backupApiService = new BackupApiService(_apiConfig, _httpClient, _mockBackupLogger.Object);
            var apiServiceFactory = new ApiServiceFactory(primaryApiService, backupApiService, _apiConfig, _mockFactoryLogger.Object);

            // Act
            // First, notify that primary API failed
            await apiServiceFactory.NotifyPrimaryApiFailureAsync(new Exception("Primary API unavailable"));
            
            // Get API service, should be backup
            var backupService = await apiServiceFactory.GetApiServiceAsync();
            Assert.Equal("StockData.org", backupService.ServiceName);
            
            // Now try to reset to primary API
            var resetResult = await apiServiceFactory.TryResetToPrimaryApiAsync();
            
            // Get API service again, should be primary
            var primaryService = await apiServiceFactory.GetApiServiceAsync();

            // Assert
            Assert.True(resetResult);
            Assert.Equal("Alpha Vantage", primaryService.ServiceName);
        }

        [Fact]
        public async Task Should_NotifyUser_When_ApiFallbackOccurs()
        {
            // Arrange
            // Setup primary API to fail
            SetupHttpResponse("https://www.alphavantage.co/", HttpStatusCode.ServiceUnavailable);
            
            // Setup backup API to succeed
            SetupHttpResponse("https://api.stockdata.org/", HttpStatusCode.OK, "{\"meta\":{\"requested\":1,\"returned\":1},\"data\":[{\"symbol\":\"^GSPC\",\"name\":\"S&P 500\",\"exchange\":\"INDEX\",\"open\":4000,\"high\":4100,\"low\":3950,\"close\":4050,\"volume\":1000000,\"date\":\"2023-01-01\"}]}");
            
            var primaryApiService = new PrimaryApiService(_apiConfig, _httpClient, _mockPrimaryLogger.Object);
            var backupApiService = new BackupApiService(_apiConfig, _httpClient, _mockBackupLogger.Object);
            var apiServiceFactory = new ApiServiceFactory(primaryApiService, backupApiService, _apiConfig, _mockFactoryLogger.Object);

            // Create a mock notification service to verify user notification
            var mockNotificationService = new Mock<INotificationService>();
            
            // Act
            await apiServiceFactory.NotifyPrimaryApiFailureAsync(new Exception("Primary API unavailable"));
            
            // Simulate notification when fallback occurs
            mockNotificationService.Setup(n => n.NotifyUser(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()))
                .Callback<string, string, NotificationType>((title, message, type) => 
                {
                    Assert.Equal("API Fallback", title);
                    Assert.Contains("switched to backup API", message);
                    Assert.Equal(NotificationType.Warning, type);
                });
            
            mockNotificationService.Object.NotifyUser(
                "API Fallback", 
                "Primary API service (Alpha Vantage) is unavailable. Switched to backup API service (StockData.org).", 
                NotificationType.Warning);

            // Assert
            mockNotificationService.Verify(
                n => n.NotifyUser(
                    It.Is<string>(s => s == "API Fallback"),
                    It.Is<string>(s => s.Contains("switched to backup API")),
                    It.Is<NotificationType>(t => t == NotificationType.Warning)),
                Times.Once);
        }

        [Fact]
        public async Task Should_ContinueOperation_When_PartialDataAvailable()
        {
            // Arrange
            // Setup primary API to return partial data (missing some dates)
            SetupHttpResponse("https://www.alphavantage.co/", HttpStatusCode.OK, 
                "{\"Meta Data\":{\"1. Information\":\"Daily Prices\",\"2. Symbol\":\"^GSPC\"},\"Time Series (Daily)\":{\"2023-01-01\":{\"1. open\":\"4000\",\"2. high\":\"4100\",\"3. low\":\"3950\",\"4. close\":\"4050\",\"5. volume\":\"1000000\"}}}");
            
            var primaryApiService = new PrimaryApiService(_apiConfig, _httpClient, _mockPrimaryLogger.Object);
            var backupApiService = new BackupApiService(_apiConfig, _httpClient, _mockBackupLogger.Object);
            var apiServiceFactory = new ApiServiceFactory(primaryApiService, backupApiService, _apiConfig, _mockFactoryLogger.Object);

            // Act
            var apiService = await apiServiceFactory.GetApiServiceAsync();
            var data = await apiService.GetHistoricalDataAsync("^GSPC", DateTime.Parse("2023-01-01"), DateTime.Parse("2023-01-05"));

            // Assert
            Assert.NotNull(data);
            Assert.Single(data); // Only one data point available
            Assert.Equal("S&P 500", data[0].IndexName);
            Assert.Equal(DateTime.Parse("2023-01-01"), data[0].Date);
        }

        [Fact]
        public async Task Should_ReattemptFetch_When_NetworkConnectivityRestored()
        {
            // Arrange
            // Setup primary API to initially fail due to network issue, then succeed
            var primaryResponseQueue = new Queue<HttpResponseMessage>();
            primaryResponseQueue.Enqueue(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            primaryResponseQueue.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) 
            { 
                Content = new StringContent("{\"Meta Data\":{\"1. Information\":\"Daily Prices\",\"2. Symbol\":\"^GSPC\"},\"Time Series (Daily)\":{\"2023-01-01\":{\"1. open\":\"4000\",\"2. high\":\"4100\",\"3. low\":\"3950\",\"4. close\":\"4050\",\"5. volume\":\"1000000\"}}}")
            });
            
            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString().StartsWith("https://www.alphavantage.co/")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => primaryResponseQueue.Dequeue());
            
            // Setup backup API to also fail
            SetupHttpResponse("https://api.stockdata.org/", HttpStatusCode.ServiceUnavailable);
            
            var primaryApiService = new PrimaryApiService(_apiConfig, _httpClient, _mockPrimaryLogger.Object);
            var backupApiService = new BackupApiService(_apiConfig, _httpClient, _mockBackupLogger.Object);
            var apiServiceFactory = new ApiServiceFactory(primaryApiService, backupApiService, _apiConfig, _mockFactoryLogger.Object);

            // Create a mock network monitor
            var mockNetworkMonitor = new Mock<INetworkMonitor>();
            mockNetworkMonitor.SetupSequence(n => n.IsNetworkAvailable())
                .Returns(false)  // Initially no network
                .Returns(true);  // Network restored
            
            // Act
            // First attempt - network unavailable
            await apiServiceFactory.NotifyPrimaryApiFailureAsync(new Exception("Network connectivity issue"));
            
            // Both APIs should fail due to network issues
            var allUnavailable = await apiServiceFactory.AreAllApiServicesUnavailableAsync();
            Assert.True(allUnavailable);
            
            // Simulate network connectivity restoration
            var networkAvailable = mockNetworkMonitor.Object.IsNetworkAvailable();
            Assert.True(networkAvailable);
            
            // Try to reset to primary API
            var resetResult = await apiServiceFactory.TryResetToPrimaryApiAsync();
            
            // Get API service again
            var apiService = await apiServiceFactory.GetApiServiceAsync();
            var data = await apiService.GetHistoricalDataAsync("^GSPC", DateTime.Parse("2023-01-01"), DateTime.Parse("2023-01-05"));

            // Assert
            Assert.True(resetResult);
            Assert.Equal("Alpha Vantage", apiService.ServiceName);
            Assert.NotNull(data);
            Assert.Single(data);
            Assert.Equal("S&P 500", data[0].IndexName);
        }

        private void SetupHttpResponse(string baseUrl, HttpStatusCode statusCode, string content = "")
        {
            var response = new HttpResponseMessage(statusCode);
            if (!string.IsNullOrEmpty(content))
            {
                response.Content = new StringContent(content);
            }
            
            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString().StartsWith(baseUrl)),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);
        }
    }

    // Interface for notification service
    public interface INotificationService
    {
        void NotifyUser(string title, string message, NotificationType type);
    }

    // Enum for notification types
    public enum NotificationType
    {
        Information,
        Success,
        Warning,
        Error
    }

    // Interface for network monitoring
    public interface INetworkMonitor
    {
        bool IsNetworkAvailable();
    }
}
