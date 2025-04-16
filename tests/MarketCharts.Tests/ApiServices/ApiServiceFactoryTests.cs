using System;
using System.Threading.Tasks;
using MarketCharts.Client.Interfaces;
using MarketCharts.Client.Models.Configuration;
using MarketCharts.Client.Services.ApiServices;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MarketCharts.Tests.ApiServices
{
    public class ApiServiceFactoryTests
    {
        private readonly Mock<IStockApiService> _primaryApiServiceMock;
        private readonly Mock<IStockApiService> _backupApiServiceMock;
        private readonly ApiConfiguration _apiConfig;
        private readonly Mock<ILogger<ApiServiceFactory>> _loggerMock;

        public ApiServiceFactoryTests()
        {
            // Setup primary API service mock
            _primaryApiServiceMock = new Mock<IStockApiService>();
            _primaryApiServiceMock.Setup(s => s.ServiceName).Returns("Alpha Vantage");
            
            // Setup backup API service mock
            _backupApiServiceMock = new Mock<IStockApiService>();
            _backupApiServiceMock.Setup(s => s.ServiceName).Returns("StockData.org");
            
            // Setup API configuration
            _apiConfig = new ApiConfiguration
            {
                RetryPrimaryApiAfterMinutes = 60,
                MaxRetryAttempts = 3,
                RetryDelayMilliseconds = 1000
            };
            
            // Setup logger mock
            _loggerMock = new Mock<ILogger<ApiServiceFactory>>();
        }

        [Fact]
        public async Task Should_ReturnPrimaryApiService_When_First_Call()
        {
            // Arrange
            _primaryApiServiceMock.Setup(s => s.IsAvailableAsync()).ReturnsAsync(true);
            var factory = new ApiServiceFactory(
                _primaryApiServiceMock.Object,
                _backupApiServiceMock.Object,
                _apiConfig,
                _loggerMock.Object);

            // Act
            var result = await factory.GetApiServiceAsync();

            // Assert
            Assert.Same(_primaryApiServiceMock.Object, result);
        }

        [Fact]
        public async Task Should_ReturnBackupApiService_When_PrimaryFails()
        {
            // Arrange
            _primaryApiServiceMock.Setup(s => s.IsAvailableAsync()).ReturnsAsync(false);
            _backupApiServiceMock.Setup(s => s.IsAvailableAsync()).ReturnsAsync(true);
            
            var factory = new ApiServiceFactory(
                _primaryApiServiceMock.Object,
                _backupApiServiceMock.Object,
                _apiConfig,
                _loggerMock.Object);

            // Notify that primary API has failed
            await factory.NotifyPrimaryApiFailureAsync(new Exception("Primary API failed"));

            // Act
            var result = await factory.GetApiServiceAsync();

            // Assert
            Assert.Same(_backupApiServiceMock.Object, result);
        }

        [Fact]
        public async Task Should_ThrowException_When_AllApisFail()
        {
            // Arrange
            _primaryApiServiceMock.Setup(s => s.IsAvailableAsync()).ReturnsAsync(false);
            _backupApiServiceMock.Setup(s => s.IsAvailableAsync()).ReturnsAsync(false);
            
            var factory = new ApiServiceFactory(
                _primaryApiServiceMock.Object,
                _backupApiServiceMock.Object,
                _apiConfig,
                _loggerMock.Object);

            // Notify that primary API has failed
            await factory.NotifyPrimaryApiFailureAsync(new Exception("Primary API failed"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => factory.GetApiServiceAsync());
            
            Assert.Contains("All API services are unavailable", exception.Message);
        }

        [Fact]
        public async Task Should_LogFailover_When_SwitchingFromPrimaryToBackup()
        {
            // Arrange
            _primaryApiServiceMock.Setup(s => s.IsAvailableAsync()).ReturnsAsync(false);
            _backupApiServiceMock.Setup(s => s.IsAvailableAsync()).ReturnsAsync(true);
            
            var factory = new ApiServiceFactory(
                _primaryApiServiceMock.Object,
                _backupApiServiceMock.Object,
                _apiConfig,
                _loggerMock.Object);

            // Act
            await factory.NotifyPrimaryApiFailureAsync(new Exception("Primary API failed"));

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Primary API service Alpha Vantage failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Should_RetryPrimaryApi_When_SpecifiedTimeElapsed()
        {
            // Arrange
            _primaryApiServiceMock.Setup(s => s.IsAvailableAsync()).ReturnsAsync(true);
            
            var factory = new ApiServiceFactory(
                _primaryApiServiceMock.Object,
                _backupApiServiceMock.Object,
                _apiConfig,
                _loggerMock.Object);

            // Notify that primary API has failed
            await factory.NotifyPrimaryApiFailureAsync(new Exception("Primary API failed"));
            
            // Verify retry time is set
            var retryTime = factory.GetPrimaryApiRetryTime();
            Assert.NotNull(retryTime);
            Assert.True(retryTime > DateTime.Now);
            
            // Act
            var resetSuccessful = await factory.TryResetToPrimaryApiAsync();
            
            // Assert
            Assert.True(resetSuccessful);
            
            // Verify that the primary API is now being used
            var result = await factory.GetApiServiceAsync();
            Assert.Same(_primaryApiServiceMock.Object, result);
        }

        [Fact]
        public async Task Should_ConfigurePrimaryApiFromEnvironment_When_ApplicationStarts()
        {
            // Arrange
            var apiConfig = new ApiConfiguration
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
            
            // Create a real primary API service with the configuration
            var httpClient = new System.Net.Http.HttpClient
            {
                BaseAddress = new Uri("https://www.alphavantage.co/")
            };
            var logger = new Mock<ILogger<PrimaryApiService>>().Object;
            var primaryApiService = new PrimaryApiService(apiConfig, httpClient, logger);
            
            // Act
            var factory = new ApiServiceFactory(
                primaryApiService,
                _backupApiServiceMock.Object,
                apiConfig,
                _loggerMock.Object);
            
            // Assert
            Assert.Equal("Alpha Vantage", factory.GetPrimaryApiService().ServiceName);
            Assert.Equal(25, factory.GetPrimaryApiService().ApiCallLimit);
        }

        [Fact]
        public async Task Should_ConfigureBackupApiFromEnvironment_When_ApplicationStarts()
        {
            // Arrange
            var apiConfig = new ApiConfiguration
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
            
            // Create a real backup API service with the configuration
            var httpClient = new System.Net.Http.HttpClient
            {
                BaseAddress = new Uri("https://api.stockdata.org/")
            };
            var logger = new Mock<ILogger<BackupApiService>>().Object;
            var backupApiService = new BackupApiService(apiConfig, httpClient, logger);
            
            // Act
            var factory = new ApiServiceFactory(
                _primaryApiServiceMock.Object,
                backupApiService,
                apiConfig,
                _loggerMock.Object);
            
            // Assert
            Assert.Equal("StockData.org", factory.GetBackupApiService().ServiceName);
            Assert.Equal(100, factory.GetBackupApiService().ApiCallLimit);
        }
    }
}
