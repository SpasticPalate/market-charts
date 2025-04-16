using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using MarketCharts.Client.Interfaces;
using MarketCharts.Client.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MarketCharts.Tests.Performance
{
    public class CachingTests
    {
        private readonly Mock<ILogger<CachingService>> _mockLogger;
        private readonly Mock<IStockDataRepository> _mockRepository;
        private readonly Mock<IApiServiceFactory> _mockApiServiceFactory;
        private readonly Mock<IStockApiService> _mockApiService;
        private readonly CachingService _cachingService;

        public CachingTests()
        {
            _mockLogger = new Mock<ILogger<CachingService>>();
            _mockRepository = new Mock<IStockDataRepository>();
            _mockApiServiceFactory = new Mock<IApiServiceFactory>();
            _mockApiService = new Mock<IStockApiService>();
            
            _mockApiServiceFactory.Setup(f => f.GetApiServiceAsync()).ReturnsAsync(_mockApiService.Object);
            
            _cachingService = new CachingService(
                _mockLogger.Object,
                _mockRepository.Object,
                _mockApiServiceFactory.Object);
        }

        [Fact]
        public async Task Should_ImproveLoadTime_When_DataCached()
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
            
            // Setup repository to simulate a cache miss followed by a cache hit
            _mockRepository.SetupSequence(r => r.GetStockDataByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>()))
                .ReturnsAsync(new List<StockIndexData>()) // First call - cache miss
                .ReturnsAsync(testData); // Second call - cache hit
            
            // Setup API service to return data (for cache miss)
            _mockApiService.Setup(s => s.GetHistoricalDataAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(testData);
            
            // Act
            // First call - cache miss, should fetch from API
            var stopwatch1 = Stopwatch.StartNew();
            var result1 = await _cachingService.GetDataAsync("^GSPC", DateTime.Today.AddDays(-5), DateTime.Today);
            stopwatch1.Stop();
            var cacheMissTime = stopwatch1.ElapsedMilliseconds;
            
            // Second call - cache hit, should be faster
            var stopwatch2 = Stopwatch.StartNew();
            var result2 = await _cachingService.GetDataAsync("^GSPC", DateTime.Today.AddDays(-5), DateTime.Today);
            stopwatch2.Stop();
            var cacheHitTime = stopwatch2.ElapsedMilliseconds;

            // Assert
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            Assert.True(cacheHitTime < cacheMissTime, $"Cache hit time ({cacheHitTime}ms) should be less than cache miss time ({cacheMissTime}ms)");
            
            // Verify API was called only once (for cache miss)
            _mockApiService.Verify(s => s.GetHistoricalDataAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
            
            // Verify repository was called twice (once for cache miss check, once for cache hit)
            _mockRepository.Verify(r => r.GetStockDataByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>()), Times.Exactly(2));
        }

        [Fact]
        public async Task Should_MinimizeApiCalls_When_DataAlreadyStored()
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
            
            // Setup repository to always return data (cache hit)
            _mockRepository.Setup(r => r.GetStockDataByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>()))
                .ReturnsAsync(testData);
            
            // Act
            // Make multiple calls
            await _cachingService.GetDataAsync("^GSPC", DateTime.Today.AddDays(-5), DateTime.Today);
            await _cachingService.GetDataAsync("^GSPC", DateTime.Today.AddDays(-5), DateTime.Today);
            await _cachingService.GetDataAsync("^GSPC", DateTime.Today.AddDays(-5), DateTime.Today);

            // Assert
            // Verify API was never called
            _mockApiService.Verify(s => s.GetHistoricalDataAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
            
            // Verify repository was called for each request
            _mockRepository.Verify(r => r.GetStockDataByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>()), Times.Exactly(3));
            
            // Verify cache hit rate
            Assert.Equal(100, _cachingService.GetCacheHitRate());
        }

        [Fact]
        public async Task Should_InvalidateCache_When_DataStale()
        {
            // Arrange
            var oldData = new List<StockIndexData>
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
                    FetchedAt = DateTime.Now.AddHours(-25) // Fetched more than 24 hours ago
                }
            };
            
            var newData = new List<StockIndexData>
            {
                new StockIndexData
                {
                    Id = 1,
                    IndexName = "S&P 500",
                    Date = DateTime.Today.AddDays(-5),
                    OpenValue = 4010.0m, // Updated values
                    CloseValue = 4060.0m,
                    HighValue = 4080.0m,
                    LowValue = 3995.0m,
                    Volume = 1010000,
                    FetchedAt = DateTime.Now
                }
            };
            
            // Setup repository to return old data
            _mockRepository.Setup(r => r.GetStockDataByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>()))
                .ReturnsAsync(oldData);
            
            // Setup API service to return new data
            _mockApiService.Setup(s => s.GetHistoricalDataAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(newData);
            
            // Setup repository to accept updates
            _mockRepository.Setup(r => r.UpdateStockDataAsync(It.IsAny<StockIndexData>()))
                .ReturnsAsync(true);

            // Act
            var result = await _cachingService.GetDataAsync("^GSPC", DateTime.Today.AddDays(-5), DateTime.Today, true);

            // Assert
            // Verify API was called to refresh stale data
            _mockApiService.Verify(s => s.GetHistoricalDataAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
            
            // Verify repository was updated
            _mockRepository.Verify(r => r.UpdateStockDataAsync(It.IsAny<StockIndexData>()), Times.Once);
            
            // Verify we got the new data
            Assert.Equal(newData[0].OpenValue, result[0].OpenValue);
            Assert.Equal(newData[0].CloseValue, result[0].CloseValue);
        }

        [Fact]
        public void Should_OptimizeCacheSize_When_StorageLimited()
        {
            // Arrange
            var largeDataSet = new List<StockIndexData>();
            for (int i = 0; i < 1000; i++)
            {
                largeDataSet.Add(new StockIndexData
                {
                    Id = i,
                    IndexName = "S&P 500",
                    Date = DateTime.Today.AddDays(-i),
                    OpenValue = 4000.0m + i,
                    CloseValue = 4050.0m + i,
                    HighValue = 4075.0m + i,
                    LowValue = 3990.0m + i,
                    Volume = 1000000 + i,
                    FetchedAt = DateTime.Now
                });
            }

            // Act
            var optimizedSize = _cachingService.OptimizeCacheSize(largeDataSet, 100);

            // Assert
            Assert.Equal(100, optimizedSize.Count);
            
            // Verify we kept the most recent data
            var oldestDate = optimizedSize.Min(d => d.Date);
            var newestDate = optimizedSize.Max(d => d.Date);
            Assert.Equal(DateTime.Today.AddDays(-99), oldestDate);
            Assert.Equal(DateTime.Today, newestDate);
        }

        [Fact]
        public void Should_PrioritizeCriticalData_When_CacheEvictionNeeded()
        {
            // Arrange
            var mixedData = new List<StockIndexData>
            {
                // Critical data (S&P 500)
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
                    FetchedAt = DateTime.Now.AddHours(-2) // Older fetch time
                },
                // Less critical data (Custom Index)
                new StockIndexData
                {
                    Id = 2,
                    IndexName = "Custom Index",
                    Date = DateTime.Today.AddDays(-4),
                    OpenValue = 1000.0m,
                    CloseValue = 1050.0m,
                    HighValue = 1075.0m,
                    LowValue = 990.0m,
                    Volume = 500000,
                    FetchedAt = DateTime.Now.AddHours(-1) // Newer fetch time
                }
            };

            // Act
            var prioritizedData = _cachingService.PrioritizeCacheData(mixedData);
            var evictionOrder = _cachingService.GetEvictionOrder(mixedData);

            // Assert
            // Verify S&P 500 has higher priority despite older fetch time
            Assert.Equal("S&P 500", prioritizedData[0].IndexName);
            
            // Verify eviction order has Custom Index first (to be evicted first)
            Assert.Equal("Custom Index", evictionOrder[0].IndexName);
        }

        [Fact]
        public async Task Should_PreloadFrequentlyAccessedData_When_ApplicationIdle()
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
            
            // Setup API service to return data
            _mockApiService.Setup(s => s.GetHistoricalDataAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(testData);
            
            // Setup repository to save data
            _mockRepository.Setup(r => r.SaveStockDataBatchAsync(It.IsAny<List<StockIndexData>>()))
                .ReturnsAsync(testData.Count);

            // Act
            await _cachingService.PreloadFrequentlyAccessedDataAsync();

            // Assert
            // Verify API was called for each major index
            _mockApiService.Verify(s => s.GetHistoricalDataAsync("^GSPC", It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
            _mockApiService.Verify(s => s.GetHistoricalDataAsync("^DJI", It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
            _mockApiService.Verify(s => s.GetHistoricalDataAsync("^IXIC", It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
            
            // Verify data was saved to repository
            _mockRepository.Verify(r => r.SaveStockDataBatchAsync(It.IsAny<List<StockIndexData>>()), Times.Exactly(3));
        }

        [Fact]
        public void Should_MeasureCacheHitRate_When_ApplicationRunning()
        {
            // Arrange
            _cachingService.ResetCacheStatistics();

            // Act
            // Simulate some cache hits and misses
            _cachingService.RecordCacheHit();
            _cachingService.RecordCacheHit();
            _cachingService.RecordCacheHit();
            _cachingService.RecordCacheMiss();
            _cachingService.RecordCacheMiss();
            
            var hitRate = _cachingService.GetCacheHitRate();
            var stats = _cachingService.GetCacheStatistics();

            // Assert
            Assert.Equal(60, hitRate); // 3 hits out of 5 requests = 60%
            Assert.Equal(3, stats.Hits);
            Assert.Equal(2, stats.Misses);
            Assert.Equal(5, stats.TotalRequests);
        }

        [Fact]
        public async Task Should_ImplementPurgePolicy_When_CacheExpiryReached()
        {
            // Arrange
            var oldData = new List<StockIndexData>
            {
                new StockIndexData
                {
                    Id = 1,
                    IndexName = "S&P 500",
                    Date = DateTime.Today.AddDays(-31), // More than 30 days old
                    OpenValue = 4000.0m,
                    CloseValue = 4050.0m,
                    HighValue = 4075.0m,
                    LowValue = 3990.0m,
                    Volume = 1000000,
                    FetchedAt = DateTime.Now.AddDays(-31)
                }
            };
            
            // Setup repository to return old data
            _mockRepository.Setup(r => r.GetStockDataByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>()))
                .ReturnsAsync(oldData);
            
            // Setup repository to delete data
            var deletedIds = new List<int>();
            _mockRepository.Setup(r => r.DeleteStockDataAsync(It.IsAny<int>()))
                .Callback<int>(id => deletedIds.Add(id))
                .ReturnsAsync(true);

            // Act
            var purgedCount = await _cachingService.PurgeCacheAsync(30); // Purge data older than 30 days

            // Assert
            Assert.Equal(1, purgedCount);
            Assert.Contains(1, deletedIds); // ID 1 should be deleted
            
            // Verify repository delete was called
            _mockRepository.Verify(r => r.DeleteStockDataAsync(1), Times.Once);
        }
    }

    /// <summary>
    /// Service for caching stock data
    /// </summary>
    public class CachingService
    {
        private readonly ILogger<CachingService> _logger;
        private readonly IStockDataRepository _repository;
        private readonly IApiServiceFactory _apiServiceFactory;
        private readonly TimeSpan _defaultCacheExpiry = TimeSpan.FromHours(24);
        private readonly HashSet<string> _criticalIndices = new HashSet<string> { "S&P 500", "Dow Jones", "NASDAQ" };
        
        // Cache statistics
        private int _cacheHits = 0;
        private int _cacheMisses = 0;

        public CachingService(
            ILogger<CachingService> logger,
            IStockDataRepository repository,
            IApiServiceFactory apiServiceFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _apiServiceFactory = apiServiceFactory ?? throw new ArgumentNullException(nameof(apiServiceFactory));
        }

        /// <summary>
        /// Gets data from cache or API
        /// </summary>
        /// <param name="symbol">The stock symbol</param>
        /// <param name="startDate">The start date</param>
        /// <param name="endDate">The end date</param>
        /// <param name="forceRefresh">Whether to force a refresh from the API</param>
        /// <returns>The stock data</returns>
        public async Task<List<StockIndexData>> GetDataAsync(string symbol, DateTime startDate, DateTime endDate, bool forceRefresh = false)
        {
            try
            {
                // Try to get data from cache (repository)
                var cachedData = await _repository.GetStockDataByDateRangeAsync(startDate, endDate, GetIndexNameFromSymbol(symbol));
                
                if (cachedData.Any() && !forceRefresh)
                {
                    // Check if data is stale
                    var oldestFetchTime = cachedData.Min(d => d.FetchedAt);
                    if (DateTime.Now - oldestFetchTime <= _defaultCacheExpiry)
                    {
                        // Cache hit - data is fresh
                        _logger.LogInformation("Cache hit for {Symbol} from {StartDate} to {EndDate}", symbol, startDate, endDate);
                        RecordCacheHit();
                        return cachedData;
                    }
                }
                
                // Cache miss or forced refresh
                RecordCacheMiss();
                _logger.LogInformation("Cache miss or refresh for {Symbol} from {StartDate} to {EndDate}", symbol, startDate, endDate);
                
                // Get data from API
                var apiService = await _apiServiceFactory.GetApiServiceAsync();
                var apiData = await apiService.GetHistoricalDataAsync(symbol, startDate, endDate);
                
                if (cachedData.Any())
                {
                    // Update existing data
                    foreach (var newDataPoint in apiData)
                    {
                        var existingDataPoint = cachedData.FirstOrDefault(d => d.Date.Date == newDataPoint.Date.Date);
                        if (existingDataPoint != null)
                        {
                            // Update existing data point
                            existingDataPoint.OpenValue = newDataPoint.OpenValue;
                            existingDataPoint.CloseValue = newDataPoint.CloseValue;
                            existingDataPoint.HighValue = newDataPoint.HighValue;
                            existingDataPoint.LowValue = newDataPoint.LowValue;
                            existingDataPoint.Volume = newDataPoint.Volume;
                            existingDataPoint.FetchedAt = DateTime.Now;
                            
                            await _repository.UpdateStockDataAsync(existingDataPoint);
                        }
                        else
                        {
                            // New data point
                            await _repository.SaveStockDataAsync(newDataPoint);
                            cachedData.Add(newDataPoint);
                        }
                    }
                }
                else
                {
                    // No existing data, save all new data
                    if (apiData.Any())
                    {
                        await _repository.SaveStockDataBatchAsync(apiData);
                    }
                    
                    return apiData;
                }
                
                return cachedData.OrderBy(d => d.Date).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting data for {Symbol} from {StartDate} to {EndDate}", symbol, startDate, endDate);
                
                // If we have cached data, return it even if it's stale
                var cachedData = await _repository.GetStockDataByDateRangeAsync(startDate, endDate, GetIndexNameFromSymbol(symbol));
                if (cachedData.Any())
                {
                    _logger.LogWarning("Returning stale cached data due to error");
                    return cachedData;
                }
                
                throw;
            }
        }

        /// <summary>
        /// Preloads frequently accessed data
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task PreloadFrequentlyAccessedDataAsync()
        {
            try
            {
                _logger.LogInformation("Preloading frequently accessed data");
                
                // Define the date range (last 30 days)
                var endDate = DateTime.Today;
                var startDate = endDate.AddDays(-30);
                
                // Get data for major indices
                var apiService = await _apiServiceFactory.GetApiServiceAsync();
                
                // S&P 500
                var sp500Data = await apiService.GetHistoricalDataAsync("^GSPC", startDate, endDate);
                if (sp500Data.Any())
                {
                    await _repository.SaveStockDataBatchAsync(sp500Data);
                }
                
                // Dow Jones
                var dowData = await apiService.GetHistoricalDataAsync("^DJI", startDate, endDate);
                if (dowData.Any())
                {
                    await _repository.SaveStockDataBatchAsync(dowData);
                }
                
                // NASDAQ
                var nasdaqData = await apiService.GetHistoricalDataAsync("^IXIC", startDate, endDate);
                if (nasdaqData.Any())
                {
                    await _repository.SaveStockDataBatchAsync(nasdaqData);
                }
                
                _logger.LogInformation("Preloading complete");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preloading frequently accessed data");
            }
        }

        /// <summary>
        /// Purges old data from the cache
        /// </summary>
        /// <param name="maxAgeDays">The maximum age of data to keep (in days)</param>
        /// <returns>The number of records purged</returns>
        public async Task<int> PurgeCacheAsync(int maxAgeDays)
        {
            try
            {
                _logger.LogInformation("Purging cache data older than {MaxAgeDays} days", maxAgeDays);
                
                var cutoffDate = DateTime.Today.AddDays(-maxAgeDays);
                var oldData = await _repository.GetStockDataByDateRangeAsync(DateTime.MinValue, cutoffDate, null);
                
                int purgedCount = 0;
                foreach (var dataPoint in oldData)
                {
                    var success = await _repository.DeleteStockDataAsync(dataPoint.Id);
                    if (success)
                    {
                        purgedCount++;
                    }
                }
                
                _logger.LogInformation("Purged {PurgedCount} records from cache", purgedCount);
                return purgedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error purging cache");
                return 0;
            }
        }

        /// <summary>
        /// Optimizes the cache size by reducing the number of data points
        /// </summary>
        /// <param name="data">The data to optimize</param>
        /// <param name="maxSize">The maximum number of data points to keep</param>
        /// <returns>The optimized data</returns>
        public List<StockIndexData> OptimizeCacheSize(List<StockIndexData> data, int maxSize)
        {
            if (data == null || data.Count <= maxSize)
            {
                return data;
            }
            
            // Sort by date (newest first)
            var sortedData = data.OrderByDescending(d => d.Date).ToList();
            
            // Keep only the most recent data points
            return sortedData.Take(maxSize).ToList();
        }

        /// <summary>
        /// Prioritizes cache data based on importance
        /// </summary>
        /// <param name="data">The data to prioritize</param>
        /// <returns>The prioritized data</returns>
        public List<StockIndexData> PrioritizeCacheData(List<StockIndexData> data)
        {
            if (data == null || !data.Any())
            {
                return data;
            }
            
            // Sort by priority (critical indices first, then by date)
            return data
                .OrderByDescending(d => _criticalIndices.Contains(d.IndexName))
                .ThenByDescending(d => d.Date)
                .ToList();
        }

        /// <summary>
        /// Gets the order in which data should be evicted from the cache
        /// </summary>
        /// <param name="data">The data to evaluate</param>
        /// <returns>The data in eviction order (first to be evicted first)</returns>
        public List<StockIndexData> GetEvictionOrder(List<StockIndexData> data)
        {
            if (data == null || !data.Any())
            {
                return data;
            }
            
            // Sort by priority (non-critical indices first, then by oldest date)
            return data
                .OrderBy(d => _criticalIndices.Contains(d.IndexName))
                .ThenBy(d => d.Date)
                .ToList();
        }

        /// <summary>
        /// Records a cache hit
        /// </summary>
        public void RecordCacheHit()
        {
            _cacheHits++;
        }

        /// <summary>
        /// Records a cache miss
        /// </summary>
        public void RecordCacheMiss()
        {
            _cacheMisses++;
        }

        /// <summary>
        /// Gets the cache hit rate (percentage)
        /// </summary>
        /// <returns>The cache hit rate</returns>
        public int GetCacheHitRate()
        {
            var totalRequests = _cacheHits + _cacheMisses;
            if (totalRequests == 0)
            {
                return 0;
            }
            
            return (int)((_cacheHits / (double)totalRequests) * 100);
        }

        /// <summary>
        /// Gets the cache statistics
        /// </summary>
        /// <returns>The cache statistics</returns>
        public CacheStatistics GetCacheStatistics()
        {
            return new CacheStatistics
            {
                Hits = _cacheHits,
                Misses = _cacheMisses,
                TotalRequests = _cacheHits + _cacheMisses,
                HitRate = GetCacheHitRate()
            };
        }

        /// <summary>
        /// Resets the cache statistics
        /// </summary>
        public void ResetCacheStatistics()
        {
            _cacheHits = 0;
            _cacheMisses = 0;
        }

        /// <summary>
        /// Gets the index name from a symbol
        /// </summary>
        /// <param name="symbol">The symbol</param>
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

    /// <summary>
    /// Cache statistics
    /// </summary>
    public class CacheStatistics
    {
        /// <summary>
        /// The number of cache hits
        /// </summary>
        public int Hits { get; set; }
        
        /// <summary>
        /// The number of cache misses
        /// </summary>
        public int Misses { get; set; }
        
        /// <summary>
        /// The total number of requests
        /// </summary>
        public int TotalRequests { get; set; }
        
        /// <summary>
        /// The cache hit rate (percentage)
        /// </summary>
        public int HitRate { get; set; }
    }
}
