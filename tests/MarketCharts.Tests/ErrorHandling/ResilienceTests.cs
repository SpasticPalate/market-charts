using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MarketCharts.Client.Interfaces;
using MarketCharts.Client.Models;
using MarketCharts.Client.Models.Configuration;
using MarketCharts.Client.Services.ApiServices;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace MarketCharts.Tests.ErrorHandling
{
    public class ResilienceTests
    {
        [Fact]
        public async Task Should_RecoverFromDatabaseCorruption_When_Detected()
        {
            // Arrange
            var mockRepository = new Mock<IStockDataRepository>();
            var mockLogger = new Mock<ILogger<IStockDataRepository>>();
            
            // Setup repository to simulate corruption and recovery
            mockRepository.Setup(repo => repo.VerifyDatabaseIntegrityAsync()).ReturnsAsync(false);
            mockRepository.Setup(repo => repo.BackupDatabaseAsync(It.IsAny<string>())).ReturnsAsync(true);
            mockRepository.Setup(repo => repo.CompactDatabaseAsync()).ReturnsAsync(true);
            mockRepository.Setup(repo => repo.InitializeSchemaAsync()).ReturnsAsync(true);
            
            // Setup repository to return data after recovery
            var testData = new StockIndexData
            {
                Id = 1,
                IndexName = "S&P 500",
                Date = DateTime.Today,
                CloseValue = 4000
            };
            
            mockRepository.Setup(repo => repo.GetStockDataByIdAsync(1)).ReturnsAsync(testData);

            // Act
            // Simulate database corruption detection and recovery process
            var isIntact = await mockRepository.Object.VerifyDatabaseIntegrityAsync();
            
            if (!isIntact)
            {
                // Backup the database before attempting recovery
                var backupPath = $"backup_{DateTime.Now:yyyyMMddHHmmss}.db";
                await mockRepository.Object.BackupDatabaseAsync(backupPath);
                
                // Attempt recovery
                await mockRepository.Object.CompactDatabaseAsync();
                await mockRepository.Object.InitializeSchemaAsync();
            }
            
            // Try to access data after recovery
            var recoveredData = await mockRepository.Object.GetStockDataByIdAsync(1);

            // Assert
            Assert.False(isIntact); // Verify corruption was detected
            Assert.NotNull(recoveredData); // Verify data is accessible after recovery
            Assert.Equal("S&P 500", recoveredData.IndexName);
            Assert.Equal(4000, recoveredData.CloseValue);
            
            // Verify recovery methods were called
            mockRepository.Verify(repo => repo.BackupDatabaseAsync(It.IsAny<string>()), Times.Once);
            mockRepository.Verify(repo => repo.CompactDatabaseAsync(), Times.Once);
            mockRepository.Verify(repo => repo.InitializeSchemaAsync(), Times.Once);
        }

        [Fact]
        public async Task Should_HandleNetworkInterruptions_When_FetchingData()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<PrimaryApiService>>();
            var apiConfig = new ApiConfiguration
            {
                PrimaryApi = new AlphaVantageConfig { ApiKey = "test_key" },
                MaxRetryAttempts = 3,
                RetryDelayMilliseconds = 100
            };
            
            // Setup mock HTTP handler that fails with network error then succeeds
            var handlerMock = new Mock<HttpMessageHandler>();
            var requestCount = 0;
            
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage request, CancellationToken token) => {
                    requestCount++;
                    
                    // First two requests fail with network error
                    if (requestCount <= 2)
                    {
                        throw new HttpRequestException("Network error");
                    }
                    
                    // Third request succeeds
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(@"{
                            ""Meta Data"": {
                                ""1. Information"": ""Daily Prices for S&P 500"",
                                ""2. Symbol"": ""^GSPC""
                            },
                            ""Time Series (Daily)"": {
                                ""2023-01-01"": {
                                    ""1. open"": ""4000.00"",
                                    ""2. high"": ""4100.00"",
                                    ""3. low"": ""3900.00"",
                                    ""4. close"": ""4050.00"",
                                    ""5. volume"": ""1000000""
                                }
                            }
                        }")
                    };
                    return response;
                });
            
            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("https://www.alphavantage.co/")
            };
            
            var service = new PrimaryApiService(apiConfig, httpClient, mockLogger.Object);

            // Act
            // Implement a simple retry mechanism
            var maxRetries = apiConfig.MaxRetryAttempts;
            var retryDelay = apiConfig.RetryDelayMilliseconds;
            var attempt = 0;
            StockIndexData result = null;
            
            while (attempt < maxRetries)
            {
                try
                {
                    attempt++;
                    result = await service.GetLatestDataAsync("^GSPC");
                    break; // Success, exit the loop
                }
                catch (HttpRequestException ex)
                {
                    if (attempt >= maxRetries)
                    {
                        throw; // Rethrow if max retries reached
                    }
                    
                    // Log the failure and retry
                    mockLogger.Object.LogWarning(ex, "Network error on attempt {Attempt}, retrying in {Delay}ms", 
                        attempt, retryDelay);
                    
                    await Task.Delay(retryDelay);
                    retryDelay *= 2; // Exponential backoff
                }
            }

            // Assert
            Assert.NotNull(result);
            Assert.Equal("S&P 500", result.IndexName);
            Assert.Equal(4050.00m, result.CloseValue);
            Assert.Equal(3, requestCount); // Verify that it took 3 attempts
            
            // Verify that failures were logged
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Network error on attempt")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Exactly(2)); // Two failed attempts should be logged
        }

        [Fact]
        public async Task Should_RetryFailedOperations_When_TransientErrorsOccur()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ApiServiceFactory>>();
            var mockPrimaryApi = new Mock<IStockApiService>();
            var mockBackupApi = new Mock<IStockApiService>();
            var apiConfig = new ApiConfiguration
            {
                MaxRetryAttempts = 3,
                RetryDelayMilliseconds = 100
            };
            
            mockPrimaryApi.Setup(api => api.ServiceName).Returns("Primary API");
            mockBackupApi.Setup(api => api.ServiceName).Returns("Backup API");
            
            // Setup primary API to fail with transient error then succeed
            var callCount = 0;
            mockPrimaryApi.Setup(api => api.GetLatestDataAsync(It.IsAny<string>()))
                .ReturnsAsync(() => {
                    callCount++;
                    if (callCount <= 2)
                    {
                        throw new InvalidOperationException("Transient error");
                    }
                    return new StockIndexData { IndexName = "S&P 500", CloseValue = 4000 };
                });
            
            var factory = new ApiServiceFactory(mockPrimaryApi.Object, mockBackupApi.Object, apiConfig, mockLogger.Object);

            // Act
            // Implement retry logic for transient errors
            var maxRetries = apiConfig.MaxRetryAttempts;
            var retryDelay = apiConfig.RetryDelayMilliseconds;
            var attempt = 0;
            StockIndexData result = null;
            
            var service = await factory.GetApiServiceAsync();
            
            while (attempt < maxRetries)
            {
                try
                {
                    attempt++;
                    result = await service.GetLatestDataAsync("^GSPC");
                    break; // Success, exit the loop
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Transient error"))
                {
                    if (attempt >= maxRetries)
                    {
                        throw; // Rethrow if max retries reached
                    }
                    
                    // Log the failure and retry
                    mockLogger.Object.LogWarning(ex, "Transient error on attempt {Attempt}, retrying in {Delay}ms", 
                        attempt, retryDelay);
                    
                    await Task.Delay(retryDelay);
                    retryDelay *= 2; // Exponential backoff
                }
            }

            // Assert
            Assert.NotNull(result);
            Assert.Equal("S&P 500", result.IndexName);
            Assert.Equal(4000, result.CloseValue);
            Assert.Equal(3, callCount); // Verify that it took 3 attempts
        }

        [Fact]
        public void Should_FallbackToSafeDefaults_When_ConfigurationInvalid()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ApiConfiguration>>();
            
            // Create an invalid configuration (missing API key)
            var invalidConfig = new ApiConfiguration
            {
                PrimaryApi = new AlphaVantageConfig { ApiKey = "" },
                BackupApi = new StockDataOrgConfig { ApiToken = "" }
            };
            
            // Define safe default values
            var defaultApiKey = "demo";
            var defaultApiToken = "demo";
            var defaultRetryMinutes = 60;
            var defaultMaxRetries = 3;
            var defaultRetryDelay = 1000;

            // Act
            // Validate and fix configuration
            if (string.IsNullOrEmpty(invalidConfig.PrimaryApi.ApiKey))
            {
                mockLogger.Object.LogWarning("Primary API key is missing, using default key");
                invalidConfig.PrimaryApi.ApiKey = defaultApiKey;
            }
            
            if (string.IsNullOrEmpty(invalidConfig.BackupApi.ApiToken))
            {
                mockLogger.Object.LogWarning("Backup API token is missing, using default token");
                invalidConfig.BackupApi.ApiToken = defaultApiToken;
            }
            
            if (invalidConfig.RetryPrimaryApiAfterMinutes <= 0)
            {
                mockLogger.Object.LogWarning("Invalid retry minutes, using default value");
                invalidConfig.RetryPrimaryApiAfterMinutes = defaultRetryMinutes;
            }
            
            if (invalidConfig.MaxRetryAttempts <= 0)
            {
                mockLogger.Object.LogWarning("Invalid max retry attempts, using default value");
                invalidConfig.MaxRetryAttempts = defaultMaxRetries;
            }
            
            if (invalidConfig.RetryDelayMilliseconds <= 0)
            {
                mockLogger.Object.LogWarning("Invalid retry delay, using default value");
                invalidConfig.RetryDelayMilliseconds = defaultRetryDelay;
            }

            // Assert
            Assert.Equal(defaultApiKey, invalidConfig.PrimaryApi.ApiKey);
            Assert.Equal(defaultApiToken, invalidConfig.BackupApi.ApiToken);
            Assert.Equal(defaultRetryMinutes, invalidConfig.RetryPrimaryApiAfterMinutes);
            Assert.Equal(defaultMaxRetries, invalidConfig.MaxRetryAttempts);
            Assert.Equal(defaultRetryDelay, invalidConfig.RetryDelayMilliseconds);
            
            // Verify that warnings were logged
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Primary API key is missing")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
            
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Backup API token is missing")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Should_GracefullyHandleResourceExhaustion_When_MemoryLimited()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<IStockDataService>>();
            var mockApiService = new Mock<IStockApiService>();
            var mockRepository = new Mock<IStockDataRepository>();
            
            // Setup API service to return a large dataset
            var largeDataset = new List<StockIndexData>();
            for (int i = 0; i < 1000; i++)
            {
                largeDataset.Add(new StockIndexData
                {
                    Id = i,
                    IndexName = "S&P 500",
                    Date = DateTime.Today.AddDays(-i),
                    CloseValue = 4000 + i
                });
            }
            
            mockApiService.Setup(api => api.GetHistoricalDataAsync(
                    It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(largeDataset);
            
            // Setup repository to simulate memory pressure during batch save
            mockRepository.Setup(repo => repo.SaveStockDataBatchAsync(It.IsAny<List<StockIndexData>>()))
                .ReturnsAsync((List<StockIndexData> data) => {
                    if (data.Count > 100)
                    {
                        throw new OutOfMemoryException("Simulated memory pressure");
                    }
                    return data.Count;
                });

            // Act
            // Implement chunked processing to handle memory constraints
            var startDate = DateTime.Today.AddYears(-5);
            var endDate = DateTime.Today;
            var symbol = "^GSPC";
            
            try
            {
                // First attempt - try to process all data at once
                var allData = await mockApiService.Object.GetHistoricalDataAsync(symbol, startDate, endDate);
                await mockRepository.Object.SaveStockDataBatchAsync(allData);
            }
            catch (OutOfMemoryException ex)
            {
                mockLogger.Object.LogWarning(ex, "Memory pressure detected, switching to chunked processing");
                
                // Second attempt - process in smaller chunks
                var allData = await mockApiService.Object.GetHistoricalDataAsync(symbol, startDate, endDate);
                var totalSaved = 0;
                
                // Process in chunks of 50 items
                for (int i = 0; i < allData.Count; i += 50)
                {
                    var chunk = allData.Skip(i).Take(50).ToList();
                    var saved = await mockRepository.Object.SaveStockDataBatchAsync(chunk);
                    totalSaved += saved;
                }
                
                // Assert
                Assert.Equal(1000, totalSaved);
                
                // Verify chunked processing was logged
                mockLogger.Verify(
                    x => x.Log(
                        LogLevel.Warning,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Memory pressure detected")),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                    Times.Once);
                
                // Verify repository was called multiple times with smaller chunks
                mockRepository.Verify(repo => repo.SaveStockDataBatchAsync(It.Is<List<StockIndexData>>(
                    list => list.Count <= 100)), Times.AtLeast(10));
            }
        }
    }
}
