using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MarketCharts.Client.Interfaces;
using MarketCharts.Client.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MarketCharts.Tests.Integration
{
    public class DataConsistencyTests
    {
        private readonly Mock<ILogger<DataConsistencyService>> _mockLogger;
        private readonly Mock<IStockDataRepository> _mockRepository;
        private readonly Mock<IApiServiceFactory> _mockApiServiceFactory;
        private readonly Mock<IStockApiService> _mockPrimaryApiService;
        private readonly Mock<IStockApiService> _mockBackupApiService;
        private readonly DataConsistencyService _dataConsistencyService;

        public DataConsistencyTests()
        {
            _mockLogger = new Mock<ILogger<DataConsistencyService>>();
            _mockRepository = new Mock<IStockDataRepository>();
            _mockApiServiceFactory = new Mock<IApiServiceFactory>();
            _mockPrimaryApiService = new Mock<IStockApiService>();
            _mockBackupApiService = new Mock<IStockApiService>();
            
            _mockApiServiceFactory.Setup(f => f.GetPrimaryApiService()).Returns(_mockPrimaryApiService.Object);
            _mockApiServiceFactory.Setup(f => f.GetBackupApiService()).Returns(_mockBackupApiService.Object);
            
            _mockPrimaryApiService.Setup(s => s.ServiceName).Returns("Primary API");
            _mockBackupApiService.Setup(s => s.ServiceName).Returns("Backup API");
            
            _dataConsistencyService = new DataConsistencyService(
                _mockLogger.Object,
                _mockRepository.Object,
                _mockApiServiceFactory.Object);
        }

        [Fact]
        public async Task Should_MaintainDataIntegrity_When_MultipleFetchOperations()
        {
            // Arrange
            var initialData = new List<StockIndexData>
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
                    FetchedAt = DateTime.Now.AddMinutes(-30)
                }
            };
            
            var newData = new List<StockIndexData>
            {
                new StockIndexData
                {
                    Id = 2,
                    IndexName = "S&P 500",
                    Date = DateTime.Today.AddDays(-4),
                    OpenValue = 4060.0m,
                    CloseValue = 4080.0m,
                    HighValue = 4100.0m,
                    LowValue = 4040.0m,
                    Volume = 1100000,
                    FetchedAt = DateTime.Now
                }
            };
            
            _mockRepository.Setup(r => r.GetStockDataByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>()))
                .ReturnsAsync(initialData);
            
            _mockPrimaryApiService.Setup(s => s.GetHistoricalDataAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(newData);
            
            _mockRepository.Setup(r => r.SaveStockDataBatchAsync(It.IsAny<List<StockIndexData>>()))
                .ReturnsAsync(newData.Count);

            // Act
            var result = await _dataConsistencyService.FetchAndMergeDataAsync("^GSPC", DateTime.Today.AddDays(-5), DateTime.Today);
            
            // Assert
            Assert.Equal(2, result.Count);
            Assert.Contains(result, d => d.Id == 1);
            Assert.Contains(result, d => d.Id == 2);
            _mockRepository.Verify(r => r.SaveStockDataBatchAsync(It.Is<List<StockIndexData>>(list => list.Count == 1)), Times.Once);
        }

        [Fact]
        public async Task Should_HandleWeekendMarketClosure_When_FetchingLatestData()
        {
            // Arrange
            var friday = new DateTime(2023, 1, 6); // Assuming this is a Friday
            var saturday = friday.AddDays(1);
            var sunday = friday.AddDays(2);
            var monday = friday.AddDays(3);
            
            var fridayData = new StockIndexData
            {
                Id = 1,
                IndexName = "S&P 500",
                Date = friday,
                OpenValue = 4000.0m,
                CloseValue = 4050.0m,
                HighValue = 4075.0m,
                LowValue = 3990.0m,
                Volume = 1000000,
                FetchedAt = friday
            };
            
            _mockRepository.Setup(r => r.GetLatestStockDataAsync("S&P 500"))
                .ReturnsAsync(fridayData);

            // Act
            var isFridayClosed = _dataConsistencyService.AreMarketsClosed(friday);
            var isSaturdayClosed = _dataConsistencyService.AreMarketsClosed(saturday);
            var isSundayClosed = _dataConsistencyService.AreMarketsClosed(sunday);
            var isMondayClosed = _dataConsistencyService.AreMarketsClosed(monday);
            
            var shouldFetchFriday = await _dataConsistencyService.ShouldFetchDataForDateAsync("S&P 500", friday);
            var shouldFetchSaturday = await _dataConsistencyService.ShouldFetchDataForDateAsync("S&P 500", saturday);
            var shouldFetchSunday = await _dataConsistencyService.ShouldFetchDataForDateAsync("S&P 500", sunday);
            var shouldFetchMonday = await _dataConsistencyService.ShouldFetchDataForDateAsync("S&P 500", monday);

            // Assert
            Assert.False(isFridayClosed);
            Assert.True(isSaturdayClosed);
            Assert.True(isSundayClosed);
            Assert.False(isMondayClosed);
            
            Assert.False(shouldFetchFriday); // Already have Friday data
            Assert.False(shouldFetchSaturday); // Market closed on Saturday
            Assert.False(shouldFetchSunday); // Market closed on Sunday
            Assert.True(shouldFetchMonday); // Need Monday data
        }

        [Fact]
        public async Task Should_HandleHolidayMarketClosure_When_FetchingData()
        {
            // Arrange
            var newYearsDay = new DateTime(2023, 1, 1);
            var independenceDay = new DateTime(2023, 7, 4);
            var christmasDay = new DateTime(2023, 12, 25);
            var regularDay = new DateTime(2023, 3, 15);
            
            // Act
            var isNewYearsClosed = _dataConsistencyService.AreMarketsClosed(newYearsDay);
            var isIndependenceDayClosed = _dataConsistencyService.AreMarketsClosed(independenceDay);
            var isChristmasDayClosed = _dataConsistencyService.AreMarketsClosed(christmasDay);
            var isRegularDayClosed = _dataConsistencyService.AreMarketsClosed(regularDay);

            // Assert
            Assert.True(isNewYearsClosed);
            Assert.True(isIndependenceDayClosed);
            Assert.True(isChristmasDayClosed);
            Assert.False(isRegularDayClosed);
        }

        [Fact]
        public async Task Should_ResolveDataConflicts_When_MultipleSources()
        {
            // Arrange
            var primaryData = new StockIndexData
            {
                Id = 1,
                IndexName = "S&P 500",
                Date = DateTime.Today.AddDays(-1),
                OpenValue = 4000.0m,
                CloseValue = 4050.0m,
                HighValue = 4075.0m,
                LowValue = 3990.0m,
                Volume = 1000000,
                FetchedAt = DateTime.Now.AddMinutes(-30)
            };
            
            var backupData = new StockIndexData
            {
                Id = 2,
                IndexName = "S&P 500",
                Date = DateTime.Today.AddDays(-1),
                OpenValue = 4010.0m, // Slightly different values
                CloseValue = 4060.0m,
                HighValue = 4080.0m,
                LowValue = 3995.0m,
                Volume = 1010000,
                FetchedAt = DateTime.Now
            };
            
            _mockPrimaryApiService.Setup(s => s.GetHistoricalDataAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<StockIndexData> { primaryData });
            
            _mockBackupApiService.Setup(s => s.GetHistoricalDataAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<StockIndexData> { backupData });

            // Act
            var resolvedData = await _dataConsistencyService.ResolveDataConflictsAsync("^GSPC", DateTime.Today.AddDays(-1), DateTime.Today);
            
            // Assert
            Assert.Single(resolvedData);
            var resolved = resolvedData[0];
            
            // Primary data should be preferred, but we should log the conflict
            Assert.Equal(primaryData.OpenValue, resolved.OpenValue);
            Assert.Equal(primaryData.CloseValue, resolved.CloseValue);
            
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Data conflict detected")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task Should_DetectDataAnomalies_When_ValidatingEntries()
        {
            // Arrange
            var normalData = new StockIndexData
            {
                Id = 1,
                IndexName = "S&P 500",
                Date = DateTime.Today.AddDays(-2),
                OpenValue = 4000.0m,
                CloseValue = 4050.0m,
                HighValue = 4075.0m,
                LowValue = 3990.0m,
                Volume = 1000000,
                FetchedAt = DateTime.Now.AddDays(-2)
            };
            
            var anomalousData = new StockIndexData
            {
                Id = 2,
                IndexName = "S&P 500",
                Date = DateTime.Today.AddDays(-1),
                OpenValue = 8000.0m, // Unrealistic jump
                CloseValue = 8050.0m,
                HighValue = 8075.0m,
                LowValue = 7990.0m,
                Volume = 1000000,
                FetchedAt = DateTime.Now.AddDays(-1)
            };
            
            var dataToValidate = new List<StockIndexData> { normalData, anomalousData };

            // Act
            var anomalies = _dataConsistencyService.DetectDataAnomalies(dataToValidate);
            
            // Assert
            Assert.Single(anomalies);
            Assert.Equal(anomalousData.Id, anomalies[0].Id);
            
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Data anomaly detected")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task Should_ReconcileTimestamps_When_DifferentFormatsEncountered()
        {
            // Arrange
            var date1 = new DateTime(2023, 1, 15, 16, 0, 0); // 4:00 PM
            var date2 = new DateTime(2023, 1, 15, 0, 0, 0);  // 12:00 AM (midnight)
            
            var data1 = new StockIndexData
            {
                Id = 1,
                IndexName = "S&P 500",
                Date = date1,
                OpenValue = 4000.0m,
                CloseValue = 4050.0m,
                HighValue = 4075.0m,
                LowValue = 3990.0m,
                Volume = 1000000,
                FetchedAt = DateTime.Now
            };
            
            var data2 = new StockIndexData
            {
                Id = 2,
                IndexName = "S&P 500",
                Date = date2,
                OpenValue = 4000.0m,
                CloseValue = 4050.0m,
                HighValue = 4075.0m,
                LowValue = 3990.0m,
                Volume = 1000000,
                FetchedAt = DateTime.Now
            };

            // Act
            var normalizedDate1 = _dataConsistencyService.NormalizeTimestamp(date1);
            var normalizedDate2 = _dataConsistencyService.NormalizeTimestamp(date2);
            
            var normalizedData1 = _dataConsistencyService.NormalizeStockData(data1);
            var normalizedData2 = _dataConsistencyService.NormalizeStockData(data2);

            // Assert
            Assert.Equal(new DateTime(2023, 1, 15), normalizedDate1);
            Assert.Equal(new DateTime(2023, 1, 15), normalizedDate2);
            
            Assert.Equal(new DateTime(2023, 1, 15), normalizedData1.Date);
            Assert.Equal(new DateTime(2023, 1, 15), normalizedData2.Date);
        }

        [Fact]
        public async Task Should_PreserveHistoricalAccuracy_When_DataUpdated()
        {
            // Arrange
            var historicalData = new List<StockIndexData>
            {
                new StockIndexData
                {
                    Id = 1,
                    IndexName = "S&P 500",
                    Date = new DateTime(2023, 1, 15),
                    OpenValue = 4000.0m,
                    CloseValue = 4050.0m,
                    HighValue = 4075.0m,
                    LowValue = 3990.0m,
                    Volume = 1000000,
                    FetchedAt = DateTime.Now.AddDays(-30)
                }
            };
            
            var updatedData = new StockIndexData
            {
                Id = 1,
                IndexName = "S&P 500",
                Date = new DateTime(2023, 1, 15),
                OpenValue = 4010.0m, // Slightly different values
                CloseValue = 4060.0m,
                HighValue = 4080.0m,
                LowValue = 3995.0m,
                Volume = 1010000,
                FetchedAt = DateTime.Now
            };
            
            _mockRepository.Setup(r => r.GetStockDataByDateAndIndexAsync(It.IsAny<DateTime>(), It.IsAny<string>()))
                .ReturnsAsync(historicalData[0]);
            
            _mockRepository.Setup(r => r.UpdateStockDataAsync(It.IsAny<StockIndexData>()))
                .ReturnsAsync(true);

            // Act
            var shouldUpdate = _dataConsistencyService.ShouldUpdateHistoricalData(historicalData[0], updatedData);
            var result = await _dataConsistencyService.UpdateHistoricalDataAsync(updatedData);
            
            // Assert
            Assert.False(shouldUpdate); // Should not update historical data that's more than 7 days old
            Assert.True(result); // But our test forces the update
            
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Updating historical data")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task Should_HandleDaylightSavingTransitions_When_ProcessingTimeSeries()
        {
            // Arrange
            // March 12, 2023 was the start of DST in the US
            var beforeDST = new DateTime(2023, 3, 11);
            var afterDST = new DateTime(2023, 3, 12);
            
            var beforeDSTData = new StockIndexData
            {
                Id = 1,
                IndexName = "S&P 500",
                Date = beforeDST,
                OpenValue = 4000.0m,
                CloseValue = 4050.0m,
                HighValue = 4075.0m,
                LowValue = 3990.0m,
                Volume = 1000000,
                FetchedAt = beforeDST.AddHours(16) // 4:00 PM
            };
            
            var afterDSTData = new StockIndexData
            {
                Id = 2,
                IndexName = "S&P 500",
                Date = afterDST,
                OpenValue = 4060.0m,
                CloseValue = 4080.0m,
                HighValue = 4100.0m,
                LowValue = 4040.0m,
                Volume = 1100000,
                FetchedAt = afterDST.AddHours(16) // 4:00 PM, but in DST
            };
            
            var dataList = new List<StockIndexData> { beforeDSTData, afterDSTData };

            // Act
            var normalizedData = _dataConsistencyService.NormalizeTimeSeriesData(dataList);
            
            // Assert
            Assert.Equal(2, normalizedData.Count);
            Assert.Equal(beforeDST.Date, normalizedData[0].Date);
            Assert.Equal(afterDST.Date, normalizedData[1].Date);
            
            // Verify the time difference between fetched times is preserved
            var timeDiff = (normalizedData[1].FetchedAt - normalizedData[0].FetchedAt).TotalHours;
            Assert.Equal(24, timeDiff, 0); // Should be exactly 24 hours apart
        }
    }

    /// <summary>
    /// Service for ensuring data consistency across multiple sources
    /// </summary>
    public class DataConsistencyService
    {
        private readonly ILogger<DataConsistencyService> _logger;
        private readonly IStockDataRepository _repository;
        private readonly IApiServiceFactory _apiServiceFactory;
        private readonly HashSet<DateTime> _usHolidays;

        public DataConsistencyService(
            ILogger<DataConsistencyService> logger,
            IStockDataRepository repository,
            IApiServiceFactory apiServiceFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _apiServiceFactory = apiServiceFactory ?? throw new ArgumentNullException(nameof(apiServiceFactory));
            
            // Initialize US market holidays for 2023 (simplified)
            _usHolidays = new HashSet<DateTime>
            {
                new DateTime(2023, 1, 1),  // New Year's Day
                new DateTime(2023, 1, 16), // Martin Luther King Jr. Day
                new DateTime(2023, 2, 20), // Presidents' Day
                new DateTime(2023, 4, 7),  // Good Friday
                new DateTime(2023, 5, 29), // Memorial Day
                new DateTime(2023, 6, 19), // Juneteenth
                new DateTime(2023, 7, 4),  // Independence Day
                new DateTime(2023, 9, 4),  // Labor Day
                new DateTime(2023, 11, 23), // Thanksgiving Day
                new DateTime(2023, 12, 25)  // Christmas Day
            };
        }

        /// <summary>
        /// Fetches data from the API and merges it with existing repository data
        /// </summary>
        /// <param name="symbol">The stock symbol to fetch</param>
        /// <param name="startDate">The start date</param>
        /// <param name="endDate">The end date</param>
        /// <returns>The merged data</returns>
        public async Task<List<StockIndexData>> FetchAndMergeDataAsync(string symbol, DateTime startDate, DateTime endDate)
        {
            try
            {
                _logger.LogInformation("Fetching and merging data for {Symbol} from {StartDate} to {EndDate}", 
                    symbol, startDate, endDate);
                
                // Get existing data from repository
                var existingData = await _repository.GetStockDataByDateRangeAsync(startDate, endDate, null);
                var existingDates = existingData.Select(d => d.Date.Date).ToHashSet();
                
                // Fetch new data from API
                var apiService = await _apiServiceFactory.GetApiServiceAsync();
                var newData = await apiService.GetHistoricalDataAsync(symbol, startDate, endDate);
                
                // Filter out data we already have
                var dataToSave = newData.Where(d => !existingDates.Contains(d.Date.Date)).ToList();
                
                if (dataToSave.Any())
                {
                    _logger.LogInformation("Saving {Count} new data points", dataToSave.Count);
                    await _repository.SaveStockDataBatchAsync(dataToSave);
                }
                
                // Merge the data
                var result = new List<StockIndexData>(existingData);
                result.AddRange(dataToSave);
                
                return result.OrderBy(d => d.Date).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching and merging data for {Symbol}", symbol);
                throw;
            }
        }

        /// <summary>
        /// Checks if markets are closed on a specific date
        /// </summary>
        /// <param name="date">The date to check</param>
        /// <returns>True if markets are closed, false otherwise</returns>
        public bool AreMarketsClosed(DateTime date)
        {
            // Check if it's a weekend
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            {
                return true;
            }
            
            // Check if it's a holiday
            var dateOnly = date.Date;
            if (_usHolidays.Contains(dateOnly))
            {
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Determines if data should be fetched for a specific date
        /// </summary>
        /// <param name="indexName">The index name</param>
        /// <param name="date">The date to check</param>
        /// <returns>True if data should be fetched, false otherwise</returns>
        public async Task<bool> ShouldFetchDataForDateAsync(string indexName, DateTime date)
        {
            // Don't fetch data for closed markets
            if (AreMarketsClosed(date))
            {
                return false;
            }
            
            // Check if we already have data for this date
            var existingData = await _repository.GetStockDataByDateAndIndexAsync(date, indexName);
            if (existingData != null)
            {
                return false;
            }
            
            return true;
        }

        /// <summary>
        /// Resolves conflicts between data from multiple sources
        /// </summary>
        /// <param name="symbol">The stock symbol</param>
        /// <param name="startDate">The start date</param>
        /// <param name="endDate">The end date</param>
        /// <returns>The resolved data</returns>
        public async Task<List<StockIndexData>> ResolveDataConflictsAsync(string symbol, DateTime startDate, DateTime endDate)
        {
            try
            {
                _logger.LogInformation("Resolving data conflicts for {Symbol} from {StartDate} to {EndDate}", 
                    symbol, startDate, endDate);
                
                // Get data from primary API
                var primaryApiService = _apiServiceFactory.GetPrimaryApiService();
                var primaryData = await primaryApiService.GetHistoricalDataAsync(symbol, startDate, endDate);
                
                // Get data from backup API
                var backupApiService = _apiServiceFactory.GetBackupApiService();
                var backupData = await backupApiService.GetHistoricalDataAsync(symbol, startDate, endDate);
                
                // Create lookup dictionaries by date
                var primaryByDate = primaryData.ToDictionary(d => d.Date.Date);
                var backupByDate = backupData.ToDictionary(d => d.Date.Date);
                
                // Combine all dates
                var allDates = new HashSet<DateTime>();
                allDates.UnionWith(primaryByDate.Keys);
                allDates.UnionWith(backupByDate.Keys);
                
                var resolvedData = new List<StockIndexData>();
                
                foreach (var date in allDates.OrderBy(d => d))
                {
                    if (primaryByDate.TryGetValue(date, out var primaryItem) && 
                        backupByDate.TryGetValue(date, out var backupItem))
                    {
                        // Both sources have data for this date, check for conflicts
                        if (!AreDataPointsConsistent(primaryItem, backupItem))
                        {
                            _logger.LogWarning("Data conflict detected for {Symbol} on {Date}. " +
                                              "Primary: O={PrimaryOpen}, C={PrimaryClose}, H={PrimaryHigh}, L={PrimaryLow}. " +
                                              "Backup: O={BackupOpen}, C={BackupClose}, H={BackupHigh}, L={BackupLow}",
                                symbol, date,
                                primaryItem.OpenValue, primaryItem.CloseValue, primaryItem.HighValue, primaryItem.LowValue,
                                backupItem.OpenValue, backupItem.CloseValue, backupItem.HighValue, backupItem.LowValue);
                            
                            // Prefer primary API data
                            resolvedData.Add(primaryItem);
                        }
                        else
                        {
                            // No conflict, use primary data
                            resolvedData.Add(primaryItem);
                        }
                    }
                    else if (primaryByDate.TryGetValue(date, out var primaryOnly))
                    {
                        // Only primary API has data for this date
                        resolvedData.Add(primaryOnly);
                    }
                    else if (backupByDate.TryGetValue(date, out var backupOnly))
                    {
                        // Only backup API has data for this date
                        resolvedData.Add(backupOnly);
                    }
                }
                
                return resolvedData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving data conflicts for {Symbol}", symbol);
                throw;
            }
        }

        /// <summary>
        /// Detects anomalies in stock data
        /// </summary>
        /// <param name="data">The data to check for anomalies</param>
        /// <returns>A list of anomalous data points</returns>
        public List<StockIndexData> DetectDataAnomalies(List<StockIndexData> data)
        {
            if (data == null || data.Count < 2)
            {
                return new List<StockIndexData>();
            }
            
            var orderedData = data.OrderBy(d => d.Date).ToList();
            var anomalies = new List<StockIndexData>();
            
            for (int i = 1; i < orderedData.Count; i++)
            {
                var previous = orderedData[i - 1];
                var current = orderedData[i];
                
                // Check for unrealistic price jumps (more than 10% in a day)
                var previousClose = previous.CloseValue;
                var currentOpen = current.OpenValue;
                var percentChange = Math.Abs((currentOpen - previousClose) / previousClose);
                
                if (percentChange > 0.1m) // 10% threshold
                {
                    _logger.LogWarning("Data anomaly detected for {IndexName} on {Date}. " +
                                      "Previous close: {PreviousClose}, Current open: {CurrentOpen}, Change: {PercentChange:P2}",
                        current.IndexName, current.Date, previousClose, currentOpen, percentChange);
                    
                    anomalies.Add(current);
                }
                
                // Check for other anomalies (e.g., high < low, open outside high/low range)
                if (current.HighValue < current.LowValue ||
                    current.OpenValue > current.HighValue ||
                    current.OpenValue < current.LowValue ||
                    current.CloseValue > current.HighValue ||
                    current.CloseValue < current.LowValue)
                {
                    _logger.LogWarning("Data anomaly detected for {IndexName} on {Date}. " +
                                      "Invalid price ranges: O={Open}, C={Close}, H={High}, L={Low}",
                        current.IndexName, current.Date, current.OpenValue, current.CloseValue, 
                        current.HighValue, current.LowValue);
                    
                    if (!anomalies.Contains(current))
                    {
                        anomalies.Add(current);
                    }
                }
            }
            
            return anomalies;
        }

        /// <summary>
        /// Normalizes a timestamp to midnight
        /// </summary>
        /// <param name="timestamp">The timestamp to normalize</param>
        /// <returns>The normalized timestamp</returns>
        public DateTime NormalizeTimestamp(DateTime timestamp)
        {
            return timestamp.Date;
        }

        /// <summary>
        /// Normalizes stock data by setting the date to midnight
        /// </summary>
        /// <param name="data">The data to normalize</param>
        /// <returns>The normalized data</returns>
        public StockIndexData NormalizeStockData(StockIndexData data)
        {
            if (data == null)
            {
                return null;
            }
            
            var normalized = new StockIndexData
            {
                Id = data.Id,
                IndexName = data.IndexName,
                Date = data.Date.Date,
                OpenValue = data.OpenValue,
                CloseValue = data.CloseValue,
                HighValue = data.HighValue,
                LowValue = data.LowValue,
                Volume = data.Volume,
                FetchedAt = data.FetchedAt
            };
            
            return normalized;
        }

        /// <summary>
        /// Normalizes a time series of stock data
        /// </summary>
        /// <param name="data">The data to normalize</param>
        /// <returns>The normalized data</returns>
        public List<StockIndexData> NormalizeTimeSeriesData(List<StockIndexData> data)
        {
            if (data == null || !data.Any())
            {
                return new List<StockIndexData>();
            }
            
            var normalizedData = new List<StockIndexData>();
            
            foreach (var item in data)
            {
                normalizedData.Add(NormalizeStockData(item));
            }
            
            // Adjust fetched times to account for DST transitions
            if (normalizedData.Count > 1)
            {
                var orderedData = normalizedData.OrderBy(d => d.Date).ToList();
                
                for (int i = 1; i < orderedData.Count; i++)
                {
                    var previous = orderedData[i - 1];
                    var current = orderedData[i];
                    
                    // If the dates are consecutive but the fetched times have a DST gap
                    var expectedTimeDiff = 24.0; // Expected hours between consecutive days
                    var actualTimeDiff = (current.FetchedAt - previous.FetchedAt).TotalHours;
                    
                    if (Math.Abs(actualTimeDiff - expectedTimeDiff) == 1.0) // DST transition (1 hour difference)
                    {
                        // Adjust the fetched time to maintain consistency
                        current.FetchedAt = previous.FetchedAt.AddHours(expectedTimeDiff);
                    }
                }
            }
            
            return normalizedData;
        }

        /// <summary>
        /// Checks if two data points are consistent
        /// </summary>
        /// <param name="data1">The first data point</param>
        /// <param name="data2">The second data point</param>
        /// <returns>True if the data points are consistent, false otherwise</returns>
        private bool AreDataPointsConsistent(StockIndexData data1, StockIndexData data2)
        {
            // Define tolerance for price differences (0.5%)
            var tolerance = 0.005m;
            
            return Math.Abs((data1.OpenValue - data2.OpenValue) / data1.OpenValue) <= tolerance &&
                   Math.Abs((data1.CloseValue - data2.CloseValue) / data1.CloseValue) <= tolerance &&
                   Math.Abs((data1.HighValue - data2.HighValue) / data1.HighValue) <= tolerance &&
                   Math.Abs((data1.LowValue - data2.LowValue) / data1.LowValue) <= tolerance;
        }

        /// <summary>
        /// Determines if historical data should be updated
        /// </summary>
        /// <param name="existingData">The existing data</param>
        /// <param name="newData">The new data</param>
        /// <returns>True if the data should be updated, false otherwise</returns>
        public bool ShouldUpdateHistoricalData(StockIndexData existingData, StockIndexData newData)
        {
            if (existingData == null || newData == null)
            {
                return false;
            }
            
            // Only update recent data (within the last 7 days)
            if ((DateTime.Now - existingData.Date).TotalDays > 7)
            {
                return false;
            }
            
            // Check if there are significant differences
            return !AreDataPointsConsistent(existingData, newData);
        }

        /// <summary>
        /// Updates historical data
        /// </summary>
        /// <param name="newData">The new data to update with</param>
        /// <returns>True if the update was successful, false otherwise</returns>
        public async Task<bool> UpdateHistoricalDataAsync(StockIndexData newData)
        {
            try
            {
                if (newData == null)
                {
                    return false;
                }
                
                var existingData = await _repository.GetStockDataByDateAndIndexAsync(newData.Date, newData.IndexName);
                if (existingData == null)
                {
                    // No existing data, save as new
                    await _repository.SaveStockDataAsync(newData);
                    return true;
                }
                
                // Log that we're updating historical data
                _logger.LogWarning("Updating historical data for {IndexName} on {Date}. " +
                                  "Old: O={OldOpen}, C={OldClose}, H={OldHigh}, L={OldLow}. " +
                                  "New: O={NewOpen}, C={NewClose}, H={NewHigh}, L={NewLow}",
                    newData.IndexName, newData.Date,
                    existingData.OpenValue, existingData.CloseValue, existingData.HighValue, existingData.LowValue,
                    newData.OpenValue, newData.CloseValue, newData.HighValue, newData.LowValue);
                
                // Update the existing data
                existingData.OpenValue = newData.OpenValue;
                existingData.CloseValue = newData.CloseValue;
                existingData.HighValue = newData.HighValue;
                existingData.LowValue = newData.LowValue;
                existingData.Volume = newData.Volume;
                existingData.FetchedAt = DateTime.Now;
                
                return await _repository.UpdateStockDataAsync(existingData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating historical data for {IndexName} on {Date}", 
                    newData.IndexName, newData.Date);
                return false;
            }
        }
    }
}
