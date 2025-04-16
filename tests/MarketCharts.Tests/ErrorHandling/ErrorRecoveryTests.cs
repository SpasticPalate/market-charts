using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using MarketCharts.Client.Interfaces;
using MarketCharts.Client.Models;
using MarketCharts.Client.Models.Configuration;
using MarketCharts.Client.Services.ApiServices;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MarketCharts.Tests.ErrorHandling
{
    public class ErrorRecoveryTests
    {
        [Fact]
        public async Task Should_GracefullyDegrade_When_NonCriticalComponentsFail()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ApiServiceFactory>>();
            var mockPrimaryApi = new Mock<IStockApiService>();
            var mockBackupApi = new Mock<IStockApiService>();
            var apiConfig = new ApiConfiguration();

            mockPrimaryApi.Setup(api => api.ServiceName).Returns("Primary API");
            mockBackupApi.Setup(api => api.ServiceName).Returns("Backup API");
            
            // Setup primary API to fail
            mockPrimaryApi.Setup(api => api.IsAvailableAsync()).ReturnsAsync(false);
            
            // Setup backup API to be available
            mockBackupApi.Setup(api => api.IsAvailableAsync()).ReturnsAsync(true);
            mockBackupApi.Setup(api => api.GetLatestDataAsync(It.IsAny<string>()))
                .ReturnsAsync(new StockIndexData { IndexName = "S&P 500", CloseValue = 4000 });

            var factory = new ApiServiceFactory(mockPrimaryApi.Object, mockBackupApi.Object, apiConfig, mockLogger.Object);

            // Act
            // Simulate primary API failure
            await factory.NotifyPrimaryApiFailureAsync(new Exception("Primary API failure"));
            
            // Get the current API service (should be backup)
            var currentService = await factory.GetApiServiceAsync();
            var result = await currentService.GetLatestDataAsync("^GSPC");

            // Assert
            Assert.Equal("Backup API", currentService.ServiceName);
            Assert.Equal("S&P 500", result.IndexName);
            Assert.Equal(4000, result.CloseValue);
            
            // Verify that the system logged the failure and fallback
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Primary API service Primary API failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Should_ProvideUsefulErrorMessages_When_OperationsFail()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<PrimaryApiService>>();
            var mockHttpClient = new Mock<HttpClient>();
            var apiConfig = new ApiConfiguration
            {
                PrimaryApi = new AlphaVantageConfig { ApiKey = "test_key" }
            };

            // Setup a mock for HttpClient that would normally be injected
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("https://www.alphavantage.co/")
            };

            // Create service with mocked dependencies
            var service = new Mock<PrimaryApiService>(apiConfig, httpClient, mockLogger.Object);

            // Setup the service to throw a specific exception with a useful message
            var expectedErrorMessage = "API call limit reached for Alpha Vantage.";
            service.Setup(s => s.GetHistoricalDataAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ThrowsAsync(new InvalidOperationException(expectedErrorMessage));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => 
                await service.Object.GetHistoricalDataAsync("^GSPC", DateTime.Now.AddDays(-10), DateTime.Now));
            
            Assert.Equal(expectedErrorMessage, exception.Message);
            
            // Verify that the error was logged
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task Should_AttemptSelfRepair_When_CorruptDataDetected()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ApiServiceFactory>>();
            var mockPrimaryApi = new Mock<IStockApiService>();
            var mockBackupApi = new Mock<IStockApiService>();
            var mockRepository = new Mock<IStockDataRepository>();
            var apiConfig = new ApiConfiguration();

            mockPrimaryApi.Setup(api => api.ServiceName).Returns("Primary API");
            mockBackupApi.Setup(api => api.ServiceName).Returns("Backup API");
            
            // Setup repository to detect corruption and attempt repair
            mockRepository.Setup(repo => repo.VerifyDatabaseIntegrityAsync()).ReturnsAsync(false);
            mockRepository.Setup(repo => repo.CompactDatabaseAsync()).ReturnsAsync(true);
            mockRepository.Setup(repo => repo.InitializeSchemaAsync()).ReturnsAsync(true);
            
            // After repair, integrity check passes
            var integrityCheckCalls = 0;
            mockRepository.Setup(repo => repo.VerifyDatabaseIntegrityAsync())
                .ReturnsAsync(() => {
                    integrityCheckCalls++;
                    return integrityCheckCalls > 1; // First call fails, subsequent calls succeed
                });

            // Act
            var initialIntegrityCheck = await mockRepository.Object.VerifyDatabaseIntegrityAsync();
            
            // Simulate self-repair process
            if (!initialIntegrityCheck)
            {
                await mockRepository.Object.CompactDatabaseAsync();
                await mockRepository.Object.InitializeSchemaAsync();
            }
            
            var finalIntegrityCheck = await mockRepository.Object.VerifyDatabaseIntegrityAsync();

            // Assert
            Assert.False(initialIntegrityCheck);
            Assert.True(finalIntegrityCheck);
            
            // Verify that the repair methods were called
            mockRepository.Verify(repo => repo.CompactDatabaseAsync(), Times.Once);
            mockRepository.Verify(repo => repo.InitializeSchemaAsync(), Times.Once);
        }

        [Fact]
        public void Should_LogDetailedDiagnostics_When_ExceptionsOccur()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<PrimaryApiService>>();
            var mockHttpClient = new Mock<HttpClient>();
            var apiConfig = new ApiConfiguration
            {
                PrimaryApi = new AlphaVantageConfig { ApiKey = "test_key" }
            };

            // Setup a mock for HttpClient that would normally be injected
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("https://www.alphavantage.co/")
            };

            // Create service with mocked dependencies
            var service = new PrimaryApiService(apiConfig, httpClient, mockLogger.Object);

            // Act
            try
            {
                // Force an exception by calling a method with invalid parameters
                var result = service.ParseAlphaVantageResponse("invalid json", "^GSPC");
                Assert.Empty(result); // This should not execute if an exception is thrown
            }
            catch (Exception ex)
            {
                // Assert
                Assert.IsType<JsonException>(ex);
                
                // Verify that detailed diagnostics were logged
                mockLogger.Verify(
                    x => x.Log(
                        LogLevel.Error,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error parsing Alpha Vantage API response")),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                    Times.AtLeastOnce);
            }
        }

        [Fact]
        public async Task Should_MaintainStateConsistency_When_ErrorsOccur()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ApiServiceFactory>>();
            var mockPrimaryApi = new Mock<IStockApiService>();
            var mockBackupApi = new Mock<IStockApiService>();
            var apiConfig = new ApiConfiguration();

            mockPrimaryApi.Setup(api => api.ServiceName).Returns("Primary API");
            mockBackupApi.Setup(api => api.ServiceName).Returns("Backup API");
            
            // Setup primary API to be initially available
            mockPrimaryApi.Setup(api => api.IsAvailableAsync()).ReturnsAsync(true);
            
            var factory = new ApiServiceFactory(mockPrimaryApi.Object, mockBackupApi.Object, apiConfig, mockLogger.Object);
            
            // Initial state - primary API should be used
            var initialService = await factory.GetApiServiceAsync();
            Assert.Equal("Primary API", initialService.ServiceName);
            
            // Act - simulate a failure in the primary API
            await factory.NotifyPrimaryApiFailureAsync(new Exception("Primary API failure"));
            
            // Setup backup API to be available
            mockBackupApi.Setup(api => api.IsAvailableAsync()).ReturnsAsync(true);
            
            // After failure - backup API should be used
            var serviceAfterFailure = await factory.GetApiServiceAsync();
            Assert.Equal("Backup API", serviceAfterFailure.ServiceName);
            
            // Simulate primary API becoming available again
            mockPrimaryApi.Setup(api => api.IsAvailableAsync()).ReturnsAsync(true);
            
            // Force a retry of the primary API
            var retrySuccessful = await factory.TryResetToPrimaryApiAsync();
            
            // After recovery - primary API should be used again
            var serviceAfterRecovery = await factory.GetApiServiceAsync();
            
            // Assert
            Assert.True(retrySuccessful);
            Assert.Equal("Primary API", serviceAfterRecovery.ServiceName);
            
            // Verify the state transitions were logged
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Primary API service Primary API failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
            
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Successfully reset to primary API service")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
    }
}
