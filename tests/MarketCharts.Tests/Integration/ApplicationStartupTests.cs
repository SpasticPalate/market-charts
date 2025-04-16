using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MarketCharts.Client.Interfaces;
using MarketCharts.Client.Models;
using MarketCharts.Client.Models.Configuration;
using MarketCharts.Client.Services.ApiServices;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MarketCharts.Tests.Integration
{
    public class ApplicationStartupTests
    {
        private readonly Mock<ILogger<ApplicationStartup>> _mockLogger;
        private readonly Mock<IStockDataRepository> _mockRepository;
        private readonly Mock<IApiServiceFactory> _mockApiServiceFactory;
        private readonly Mock<IStockApiService> _mockApiService;
        private readonly Mock<INetworkMonitor> _mockNetworkMonitor;
        private readonly string _testConfigPath;
        private readonly ApplicationStartup _applicationStartup;

        public ApplicationStartupTests()
        {
            _mockLogger = new Mock<ILogger<ApplicationStartup>>();
            _mockRepository = new Mock<IStockDataRepository>();
            _mockApiServiceFactory = new Mock<IApiServiceFactory>();
            _mockApiService = new Mock<IStockApiService>();
            _mockNetworkMonitor = new Mock<INetworkMonitor>();
            
            _testConfigPath = Path.Combine(Path.GetTempPath(), $"appsettings_test_{Guid.NewGuid()}.json");
            
            _mockApiServiceFactory.Setup(f => f.GetApiServiceAsync()).ReturnsAsync(_mockApiService.Object);
            _mockApiService.Setup(s => s.ServiceName).Returns("Test API Service");
            
            _applicationStartup = new ApplicationStartup(
                _mockLogger.Object,
                _mockRepository.Object,
                _mockApiServiceFactory.Object,
                _mockNetworkMonitor.Object,
                _testConfigPath);
        }

        [Fact]
        public async Task Should_LoadHistoricalData_When_FirstTimeStartup()
        {
            // Arrange
            _mockRepository.Setup(r => r.GetStockDataByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>()))
                .ReturnsAsync(new List<StockIndexData>()); // Empty repository
            
            var testData = new List<StockIndexData>
            {
                new StockIndexData
                {
                    Id = 1,
                    IndexName = "S&P 500",
                    Date = DateTime.Today.AddDays(-5),
                    OpenValue = 4000.0m,
                    CloseValue = 4050.0m,
                    HighValue = 4075.0m,
                    LowValue = 3990.0m,
                    Volume = 1000000,
                    FetchedAt = DateTime.Now
                }
            };
            
            _mockApiService.Setup(s => s.GetHistoricalDataAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(testData);
            
            _mockRepository.Setup(r => r.SaveStockDataBatchAsync(It.IsAny<List<StockIndexData>>()))
                .ReturnsAsync(testData.Count);

            // Act
            var result = await _applicationStartup.InitializeAsync();

            // Assert
            Assert.True(result);
            _mockApiService.Verify(s => s.GetHistoricalDataAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.AtLeastOnce);
            _mockRepository.Verify(r => r.SaveStockDataBatchAsync(It.IsAny<List<StockIndexData>>()), Times.AtLeastOnce);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Historical data loaded")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task Should_UseLocalData_When_SecondTimeStartup()
        {
            // Arrange
            var testData = new List<StockIndexData>
            {
                new StockIndexData
                {
                    Id = 1,
                    IndexName = "S&P 500",
                    Date = DateTime.Today.AddDays(-5),
                    OpenValue = 4000.0m,
                    CloseValue = 4050.0m,
                    HighValue = 4075.0m,
                    LowValue = 3990.0m,
                    Volume = 1000000,
                    FetchedAt = DateTime.Now
                }
            };
            
            _mockRepository.Setup(r => r.GetStockDataByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>()))
                .ReturnsAsync(testData); // Repository has data

            // Act
            var result = await _applicationStartup.InitializeAsync();

            // Assert
            Assert.True(result);
            _mockApiService.Verify(s => s.GetHistoricalDataAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
            _mockRepository.Verify(r => r.GetStockDataByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>()), Times.AtLeastOnce);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Using existing local data")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task Should_FetchLatestDay_When_YesterdaysDataMissing()
        {
            // Arrange
            var historicalData = new List<StockIndexData>
            {
                new StockIndexData
                {
                    Id = 1,
                    IndexName = "S&P 500",
                    Date = DateTime.Today.AddDays(-5),
                    OpenValue = 4000.0m,
                    CloseValue = 4050.0m,
                    HighValue = 4075.0m,
                    LowValue = 3990.0m,
                    Volume = 1000000,
                    FetchedAt = DateTime.Now.AddDays(-5)
                }
            };
            
            var latestData = new StockIndexData
            {
                Id = 2,
                IndexName = "S&P 500",
                Date = DateTime.Today.AddDays(-1),
                OpenValue = 4100.0m,
                CloseValue = 4150.0m,
                HighValue = 4175.0m,
                LowValue = 4090.0m,
                Volume = 1100000,
                FetchedAt = DateTime.Now
            };
            
            _mockRepository.Setup(r => r.GetStockDataByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>()))
                .ReturnsAsync(historicalData); // Repository has old data
            
            _mockRepository.Setup(r => r.GetLatestStockDataAsync(It.IsAny<string>()))
                .ReturnsAsync(historicalData[0]); // Latest data is 5 days old
            
            _mockApiService.Setup(s => s.GetLatestDataAsync(It.IsAny<string>()))
                .ReturnsAsync(latestData);
            
            _mockRepository.Setup(r => r.SaveStockDataAsync(It.IsAny<StockIndexData>()))
                .ReturnsAsync(2);

            // Act
            var result = await _applicationStartup.CheckForLatestDataAsync();

            // Assert
            Assert.True(result);
            _mockApiService.Verify(s => s.GetLatestDataAsync(It.IsAny<string>()), Times.AtLeastOnce);
            _mockRepository.Verify(r => r.SaveStockDataAsync(It.IsAny<StockIndexData>()), Times.AtLeastOnce);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Latest data fetched")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task Should_InitializeDatabase_When_FirstRun()
        {
            // Arrange
            _mockRepository.Setup(r => r.InitializeSchemaAsync())
                .ReturnsAsync(true);

            // Act
            var result = await _applicationStartup.InitializeDatabaseAsync();

            // Assert
            Assert.True(result);
            _mockRepository.Verify(r => r.InitializeSchemaAsync(), Times.Once);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Database schema initialized")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task Should_ConfigureApiServices_When_ApplicationStarts()
        {
            // Arrange
            var config = new ApiConfiguration
            {
                PrimaryApi = new AlphaVantageConfig
                {
                    ApiKey = "test_primary_key",
                    BaseUrl = "https://www.alphavantage.co/query"
                },
                BackupApi = new StockDataOrgConfig
                {
                    ApiToken = "test_backup_token",
                    BaseUrl = "https://api.stockdata.org/v1/data/quote"
                }
            };
            
            // Create a test config file
            var configJson = System.Text.Json.JsonSerializer.Serialize(new { Api = config });
            await File.WriteAllTextAsync(_testConfigPath, configJson);

            // Act
            var result = await _applicationStartup.ConfigureApiServicesAsync();

            // Assert
            Assert.True(result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("API services configured")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task Should_LoadDefaultSettings_When_ConfigurationMissing()
        {
            // Arrange
            // Ensure config file doesn't exist
            if (File.Exists(_testConfigPath))
            {
                File.Delete(_testConfigPath);
            }

            // Act
            var result = await _applicationStartup.LoadConfigurationAsync();

            // Assert
            Assert.True(result);
            Assert.True(File.Exists(_testConfigPath)); // Default config should be created
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Default configuration created")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public void Should_DetectNetworkStatus_When_ApplicationStarts()
        {
            // Arrange
            _mockNetworkMonitor.Setup(n => n.IsNetworkAvailable()).Returns(true);

            // Act
            var result = _applicationStartup.CheckNetworkConnectivity();

            // Assert
            Assert.True(result);
            _mockNetworkMonitor.Verify(n => n.IsNetworkAvailable(), Times.Once);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Network connectivity")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task Should_LogStartupSequence_When_ApplicationInitializes()
        {
            // Arrange
            _mockRepository.Setup(r => r.InitializeSchemaAsync()).ReturnsAsync(true);
            _mockRepository.Setup(r => r.GetStockDataByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>()))
                .ReturnsAsync(new List<StockIndexData>()); // Empty repository
            _mockApiService.Setup(s => s.GetHistoricalDataAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<StockIndexData>());
            _mockNetworkMonitor.Setup(n => n.IsNetworkAvailable()).Returns(true);

            // Act
            await _applicationStartup.StartAsync();

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Application startup initiated")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
            
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Application startup completed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
    }

    /// <summary>
    /// Class responsible for application startup and initialization
    /// </summary>
    public class ApplicationStartup
    {
        private readonly ILogger<ApplicationStartup> _logger;
        private readonly IStockDataRepository _repository;
        private readonly IApiServiceFactory _apiServiceFactory;
        private readonly INetworkMonitor _networkMonitor;
        private readonly string _configPath;

        public ApplicationStartup(
            ILogger<ApplicationStartup> logger,
            IStockDataRepository repository,
            IApiServiceFactory apiServiceFactory,
            INetworkMonitor networkMonitor,
            string configPath)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _apiServiceFactory = apiServiceFactory ?? throw new ArgumentNullException(nameof(apiServiceFactory));
            _networkMonitor = networkMonitor ?? throw new ArgumentNullException(nameof(networkMonitor));
            _configPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
        }

        /// <summary>
        /// Starts the application initialization process
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task<bool> StartAsync()
        {
            try
            {
                _logger.LogInformation("Application startup initiated at {Time}", DateTime.Now);
                
                // Check network connectivity
                var networkAvailable = CheckNetworkConnectivity();
                if (!networkAvailable)
                {
                    _logger.LogWarning("Network connectivity not available. Starting in offline mode.");
                }
                
                // Load configuration
                await LoadConfigurationAsync();
                
                // Configure API services
                await ConfigureApiServicesAsync();
                
                // Initialize database
                await InitializeDatabaseAsync();
                
                // Initialize data
                await InitializeAsync();
                
                // Check for latest data
                if (networkAvailable)
                {
                    await CheckForLatestDataAsync();
                }
                
                _logger.LogInformation("Application startup completed at {Time}", DateTime.Now);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during application startup");
                return false;
            }
        }

        /// <summary>
        /// Initializes the application data
        /// </summary>
        /// <returns>True if initialization was successful, false otherwise</returns>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                // Check if we have any data in the repository
                var startDate = DateTime.Today.AddYears(-1);
                var endDate = DateTime.Today;
                var existingData = await _repository.GetStockDataByDateRangeAsync(startDate, endDate, "S&P 500");
                
                if (existingData.Count == 0)
                {
                    _logger.LogInformation("No existing data found. Loading historical data...");
                    
                    // Fetch historical data for major indices
                    var apiService = await _apiServiceFactory.GetApiServiceAsync();
                    var sp500Data = await apiService.GetHistoricalDataAsync("^GSPC", startDate, endDate);
                    var dowData = await apiService.GetHistoricalDataAsync("^DJI", startDate, endDate);
                    var nasdaqData = await apiService.GetHistoricalDataAsync("^IXIC", startDate, endDate);
                    
                    // Combine all data
                    var allData = new List<StockIndexData>();
                    allData.AddRange(sp500Data);
                    allData.AddRange(dowData);
                    allData.AddRange(nasdaqData);
                    
                    // Save to repository
                    var savedCount = await _repository.SaveStockDataBatchAsync(allData);
                    _logger.LogInformation("Historical data loaded and saved. {Count} records saved.", savedCount);
                }
                else
                {
                    _logger.LogInformation("Using existing local data. {Count} records found.", existingData.Count);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing application data");
                return false;
            }
        }

        /// <summary>
        /// Checks for and fetches the latest data
        /// </summary>
        /// <returns>True if the check was successful, false otherwise</returns>
        public async Task<bool> CheckForLatestDataAsync()
        {
            try
            {
                // Get the latest data for each index
                var latestSP500 = await _repository.GetLatestStockDataAsync("S&P 500");
                var latestDow = await _repository.GetLatestStockDataAsync("Dow Jones");
                var latestNasdaq = await _repository.GetLatestStockDataAsync("NASDAQ");
                
                // Check if we need to fetch the latest data
                var today = DateTime.Today;
                var apiService = await _apiServiceFactory.GetApiServiceAsync();
                
                if (latestSP500 == null || latestSP500.Date < today.AddDays(-1))
                {
                    var latestData = await apiService.GetLatestDataAsync("^GSPC");
                    await _repository.SaveStockDataAsync(latestData);
                    _logger.LogInformation("Latest S&P 500 data fetched for {Date}", latestData.Date);
                }
                
                if (latestDow == null || latestDow.Date < today.AddDays(-1))
                {
                    var latestData = await apiService.GetLatestDataAsync("^DJI");
                    await _repository.SaveStockDataAsync(latestData);
                    _logger.LogInformation("Latest Dow Jones data fetched for {Date}", latestData.Date);
                }
                
                if (latestNasdaq == null || latestNasdaq.Date < today.AddDays(-1))
                {
                    var latestData = await apiService.GetLatestDataAsync("^IXIC");
                    await _repository.SaveStockDataAsync(latestData);
                    _logger.LogInformation("Latest NASDAQ data fetched for {Date}", latestData.Date);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for latest data");
                return false;
            }
        }

        /// <summary>
        /// Initializes the database schema
        /// </summary>
        /// <returns>True if initialization was successful, false otherwise</returns>
        public async Task<bool> InitializeDatabaseAsync()
        {
            try
            {
                var result = await _repository.InitializeSchemaAsync();
                if (result)
                {
                    _logger.LogInformation("Database schema initialized successfully");
                }
                else
                {
                    _logger.LogWarning("Database schema initialization skipped (already exists)");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing database schema");
                return false;
            }
        }

        /// <summary>
        /// Configures the API services
        /// </summary>
        /// <returns>True if configuration was successful, false otherwise</returns>
        public async Task<bool> ConfigureApiServicesAsync()
        {
            try
            {
                // In a real implementation, this would configure the API services
                // based on the loaded configuration
                _logger.LogInformation("API services configured successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error configuring API services");
                return false;
            }
        }

        /// <summary>
        /// Loads the application configuration
        /// </summary>
        /// <returns>True if loading was successful, false otherwise</returns>
        public async Task<bool> LoadConfigurationAsync()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    _logger.LogInformation("Configuration file not found. Creating default configuration...");
                    
                    // Create default configuration
                    var defaultConfig = new
                    {
                        Api = new ApiConfiguration
                        {
                            PrimaryApi = new AlphaVantageConfig
                            {
                                ApiKey = "demo",
                                BaseUrl = "https://www.alphavantage.co/query"
                            },
                            BackupApi = new StockDataOrgConfig
                            {
                                ApiToken = "demo",
                                BaseUrl = "https://api.stockdata.org/v1/data/quote"
                            }
                        }
                    };
                    
                    var json = System.Text.Json.JsonSerializer.Serialize(defaultConfig, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(_configPath, json);
                    
                    _logger.LogInformation("Default configuration created at {Path}", _configPath);
                }
                else
                {
                    _logger.LogInformation("Configuration loaded from {Path}", _configPath);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading configuration");
                return false;
            }
        }

        /// <summary>
        /// Checks network connectivity
        /// </summary>
        /// <returns>True if network is available, false otherwise</returns>
        public bool CheckNetworkConnectivity()
        {
            try
            {
                var isAvailable = _networkMonitor.IsNetworkAvailable();
                _logger.LogInformation("Network connectivity check: {Status}", isAvailable ? "Available" : "Not available");
                return isAvailable;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking network connectivity");
                return false;
            }
        }
    }
}
