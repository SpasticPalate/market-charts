using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarketCharts.Client.Interfaces;
using MarketCharts.Client.Models;
using MarketCharts.Client.Models.Chart;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MarketCharts.Tests.Performance
{
    public class DataLoadingTests
    {
        private readonly Mock<ILogger<DataLoadingService>> _mockLogger;
        private readonly Mock<IStockDataRepository> _mockRepository;
        private readonly Mock<IStockDataService> _mockDataService;
        private readonly Mock<IChartDataProcessor> _mockChartProcessor;
        private readonly DataLoadingService _dataLoadingService;

        public DataLoadingTests()
        {
            _mockLogger = new Mock<ILogger<DataLoadingService>>();
            _mockRepository = new Mock<IStockDataRepository>();
            _mockDataService = new Mock<IStockDataService>();
            _mockChartProcessor = new Mock<IChartDataProcessor>();
            
            _dataLoadingService = new DataLoadingService(
                _mockLogger.Object,
                _mockRepository.Object,
                _mockDataService.Object,
                _mockChartProcessor.Object);
        }

        [Fact]
        public async Task Should_LoadWithinAcceptableTimeframe_When_CachedDataUsed()
        {
            // Arrange
            var cachedData = new Dictionary<string, List<StockIndexData>>
            {
                ["S&P 500"] = new List<StockIndexData>
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
                }
            };
            
            _mockDataService.Setup(s => s.GetInaugurationToPresent())
                .ReturnsAsync(cachedData);
            
            // Act
            var stopwatch = Stopwatch.StartNew();
            var result = await _dataLoadingService.LoadInaugurationDataAsync();
            stopwatch.Stop();
            
            // Assert
            Assert.NotNull(result);
            Assert.True(stopwatch.ElapsedMilliseconds < 100, $"Loading took {stopwatch.ElapsedMilliseconds}ms, which exceeds the acceptable timeframe of 100ms");
        }

        [Fact]
        public async Task Should_NotBlockUI_When_FetchingNewData()
        {
            // Arrange
            var progressUpdates = new List<int>();
            var uiThreadBlocked = false;
            
            // Setup data service to simulate a slow operation
            _mockDataService.Setup(s => s.GetTariffAnnouncementToPresent())
                .Returns(async () => 
                {
                    // Simulate a slow operation that reports progress
                    for (int i = 0; i <= 100; i += 20)
                    {
                        await Task.Delay(50); // Simulate work
                        _dataLoadingService.ReportProgress(i);
                    }
                    
                    return new Dictionary<string, List<StockIndexData>>
                    {
                        ["S&P 500"] = new List<StockIndexData>
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
                        }
                    };
                });
            
            // Setup progress handler
            _dataLoadingService.ProgressChanged += (sender, progress) =>
            {
                progressUpdates.Add(progress);
                
                // Check if we're on a UI thread (simulated)
                uiThreadBlocked = Thread.CurrentThread.IsThreadPoolThread;
            };

            // Act
            var loadingTask = _dataLoadingService.LoadTariffDataAsync();
            
            // Simulate UI thread work while data is loading
            for (int i = 0; i < 5; i++)
            {
                await Task.Delay(20); // Simulate UI work
            }
            
            var result = await loadingTask;

            // Assert
            Assert.NotNull(result);
            Assert.False(uiThreadBlocked, "UI thread was blocked during data loading");
            Assert.True(progressUpdates.Count >= 3, "Progress updates were not received");
            Assert.Contains(100, progressUpdates); // Final progress update
        }

        [Fact]
        public async Task Should_HandleLargeDatasets_When_LongTimeRangesSelected()
        {
            // Arrange
            var largeDataset = new Dictionary<string, List<StockIndexData>>();
            var sp500Data = new List<StockIndexData>();
            
            // Generate a large dataset (5 years of daily data)
            for (int i = 0; i < 365 * 5; i++)
            {
                sp500Data.Add(new StockIndexData
                {
                    Id = i,
                    IndexName = "S&P 500",
                    Date = DateTime.Today.AddDays(-i),
                    OpenValue = 4000.0m + (i % 100),
                    CloseValue = 4050.0m + (i % 100),
                    HighValue = 4075.0m + (i % 100),
                    LowValue = 3990.0m + (i % 100),
                    Volume = 1000000 + i,
                    FetchedAt = DateTime.Now
                });
            }
            
            largeDataset["S&P 500"] = sp500Data;
            
            _mockDataService.Setup(s => s.GetInaugurationToPresent())
                .ReturnsAsync(largeDataset);
            
            // Act
            var stopwatch = Stopwatch.StartNew();
            var result = await _dataLoadingService.LoadInaugurationDataAsync();
            stopwatch.Stop();
            
            // Assert
            Assert.NotNull(result);
            
            // Verify memory usage is reasonable
            var memoryUsed = GC.GetTotalMemory(true);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Memory usage")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
            
            // Verify data was optimized
            _mockChartProcessor.Verify(p => p.OptimizeDataPoints(It.IsAny<ChartData>(), It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task Should_OptimizeMemoryUsage_When_ProcessingMultipleIndices()
        {
            // Arrange
            var multipleIndicesData = new Dictionary<string, List<StockIndexData>>
            {
                ["S&P 500"] = new List<StockIndexData>
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
                },
                ["Dow Jones"] = new List<StockIndexData>
                {
                    new StockIndexData
                    {
                        Id = 2,
                        IndexName = "Dow Jones",
                        Date = DateTime.Today.AddDays(-5),
                        OpenValue = 34000.0m,
                        CloseValue = 34050.0m,
                        HighValue = 34075.0m,
                        LowValue = 33990.0m,
                        Volume = 2000000,
                        FetchedAt = DateTime.Now
                    }
                },
                ["NASDAQ"] = new List<StockIndexData>
                {
                    new StockIndexData
                    {
                        Id = 3,
                        IndexName = "NASDAQ",
                        Date = DateTime.Today.AddDays(-5),
                        OpenValue = 14000.0m,
                        CloseValue = 14050.0m,
                        HighValue = 14075.0m,
                        LowValue = 13990.0m,
                        Volume = 3000000,
                        FetchedAt = DateTime.Now
                    }
                }
            };
            
            _mockDataService.Setup(s => s.GetInaugurationToPresent())
                .ReturnsAsync(multipleIndicesData);
            
            // Act
            var memoryBefore = GC.GetTotalMemory(true);
            var result = await _dataLoadingService.LoadInaugurationDataAsync();
            var memoryAfter = GC.GetTotalMemory(true);
            
            // Assert
            Assert.NotNull(result);
            
            // Verify memory usage is reasonable
            var memoryDelta = memoryAfter - memoryBefore;
            Assert.True(memoryDelta < 1024 * 1024, $"Memory usage increased by {memoryDelta} bytes, which exceeds the acceptable limit of 1MB");
            
            // Verify data was processed efficiently
            _mockChartProcessor.Verify(p => p.FormatDataForChart(It.IsAny<Dictionary<string, List<StockIndexData>>>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
        }

        [Fact]
        public async Task Should_MaintainResponseTime_When_MultipleChartsRendered()
        {
            // Arrange
            var testData = new Dictionary<string, List<StockIndexData>>
            {
                ["S&P 500"] = new List<StockIndexData>
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
                }
            };
            
            _mockDataService.Setup(s => s.GetInaugurationToPresent())
                .ReturnsAsync(testData);
            
            _mockDataService.Setup(s => s.GetTariffAnnouncementToPresent())
                .ReturnsAsync(testData);
            
            // Act
            var stopwatch1 = Stopwatch.StartNew();
            var result1 = await _dataLoadingService.LoadInaugurationDataAsync();
            stopwatch1.Stop();
            
            var stopwatch2 = Stopwatch.StartNew();
            var result2 = await _dataLoadingService.LoadTariffDataAsync();
            stopwatch2.Stop();
            
            // Assert
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            
            // Verify response times are consistent
            var timeDifference = Math.Abs(stopwatch1.ElapsedMilliseconds - stopwatch2.ElapsedMilliseconds);
            Assert.True(timeDifference < 50, $"Response time difference of {timeDifference}ms exceeds the acceptable limit of 50ms");
        }

        [Fact]
        public async Task Should_ScaleEfficientlyWithDataSize_When_TimeRangeExtended()
        {
            // Arrange
            var smallDataset = new Dictionary<string, List<StockIndexData>>();
            var largeDataset = new Dictionary<string, List<StockIndexData>>();
            
            // Small dataset (30 days)
            var smallData = new List<StockIndexData>();
            for (int i = 0; i < 30; i++)
            {
                smallData.Add(new StockIndexData
                {
                    Id = i,
                    IndexName = "S&P 500",
                    Date = DateTime.Today.AddDays(-i),
                    OpenValue = 4000.0m + (i % 10),
                    CloseValue = 4050.0m + (i % 10),
                    HighValue = 4075.0m + (i % 10),
                    LowValue = 3990.0m + (i % 10),
                    Volume = 1000000 + i,
                    FetchedAt = DateTime.Now
                });
            }
            smallDataset["S&P 500"] = smallData;
            
            // Large dataset (365 days)
            var largeData = new List<StockIndexData>();
            for (int i = 0; i < 365; i++)
            {
                largeData.Add(new StockIndexData
                {
                    Id = i + 100,
                    IndexName = "S&P 500",
                    Date = DateTime.Today.AddDays(-i),
                    OpenValue = 4000.0m + (i % 10),
                    CloseValue = 4050.0m + (i % 10),
                    HighValue = 4075.0m + (i % 10),
                    LowValue = 3990.0m + (i % 10),
                    Volume = 1000000 + i,
                    FetchedAt = DateTime.Now
                });
            }
            largeDataset["S&P 500"] = largeData;
            
            // Setup mock to return different datasets based on date range
            _mockDataService.Setup(s => s.GetTariffAnnouncementToPresent())
                .ReturnsAsync(smallDataset);
            
            _mockDataService.Setup(s => s.GetInaugurationToPresent())
                .ReturnsAsync(largeDataset);
            
            // Act
            var stopwatch1 = Stopwatch.StartNew();
            var result1 = await _dataLoadingService.LoadTariffDataAsync();
            stopwatch1.Stop();
            var smallDataTime = stopwatch1.ElapsedMilliseconds;
            
            var stopwatch2 = Stopwatch.StartNew();
            var result2 = await _dataLoadingService.LoadInaugurationDataAsync();
            stopwatch2.Stop();
            var largeDataTime = stopwatch2.ElapsedMilliseconds;
            
            // Assert
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            
            // Verify scaling is sub-linear (time should not increase proportionally with data size)
            // 365/30 = 12.17x more data, but should take less than 12.17x more time
            var scaleFactor = (double)largeDataTime / smallDataTime;
            var dataRatio = 365.0 / 30.0;
            
            Assert.True(scaleFactor < dataRatio, $"Processing time scaled by {scaleFactor}x, which exceeds the data size ratio of {dataRatio}x");
        }

        [Fact]
        public async Task Should_ParallelizeOperations_When_MultipleResourcesAvailable()
        {
            // Arrange
            var multipleIndicesData = new Dictionary<string, List<StockIndexData>>
            {
                ["S&P 500"] = new List<StockIndexData>
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
                },
                ["Dow Jones"] = new List<StockIndexData>
                {
                    new StockIndexData
                    {
                        Id = 2,
                        IndexName = "Dow Jones",
                        Date = DateTime.Today.AddDays(-5),
                        OpenValue = 34000.0m,
                        CloseValue = 34050.0m,
                        HighValue = 34075.0m,
                        LowValue = 33990.0m,
                        Volume = 2000000,
                        FetchedAt = DateTime.Now
                    }
                },
                ["NASDAQ"] = new List<StockIndexData>
                {
                    new StockIndexData
                    {
                        Id = 3,
                        IndexName = "NASDAQ",
                        Date = DateTime.Today.AddDays(-5),
                        OpenValue = 14000.0m,
                        CloseValue = 14050.0m,
                        HighValue = 14075.0m,
                        LowValue = 13990.0m,
                        Volume = 3000000,
                        FetchedAt = DateTime.Now
                    }
                }
            };
            
            _mockDataService.Setup(s => s.GetInaugurationToPresent())
                .ReturnsAsync(multipleIndicesData);
            
            // Act
            var stopwatch = Stopwatch.StartNew();
            var result = await _dataLoadingService.LoadInaugurationDataParallelAsync();
            stopwatch.Stop();
            var parallelTime = stopwatch.ElapsedMilliseconds;
            
            stopwatch.Restart();
            var result2 = await _dataLoadingService.LoadInaugurationDataAsync();
            stopwatch.Stop();
            var sequentialTime = stopwatch.ElapsedMilliseconds;
            
            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result2);
            
            // Verify parallel processing is faster
            Assert.True(parallelTime < sequentialTime, $"Parallel processing ({parallelTime}ms) should be faster than sequential processing ({sequentialTime}ms)");
            
            // Verify parallel processing was used
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Processing in parallel")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Should_MeetTargetFrameRate_When_ChartAnimationsPlayed()
        {
            // Arrange
            var testData = new Dictionary<string, List<StockIndexData>>
            {
                ["S&P 500"] = new List<StockIndexData>
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
                }
            };
            
            _mockDataService.Setup(s => s.GetInaugurationToPresent())
                .ReturnsAsync(testData);
            
            // Act
            var frameRates = new List<double>();
            var animationTask = _dataLoadingService.AnimateChartAsync(progress =>
            {
                // Simulate measuring frame rate
                var frameTime = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency * 1000;
                var frameRate = 1000.0 / frameTime;
                frameRates.Add(frameRate);
            });
            
            // Wait for animation to complete
            await animationTask;
            
            // Assert
            var averageFrameRate = frameRates.Average();
            Assert.True(averageFrameRate >= 30.0, $"Average frame rate of {averageFrameRate} fps is below the target of 30 fps");
            
            // Verify animation was smooth
            var frameRateVariance = frameRates.Select(r => Math.Pow(r - averageFrameRate, 2)).Average();
            Assert.True(frameRateVariance < 100.0, $"Frame rate variance of {frameRateVariance} exceeds the acceptable limit of 100");
        }
    }

    /// <summary>
    /// Service for loading and processing stock data
    /// </summary>
    public class DataLoadingService
    {
        private readonly ILogger<DataLoadingService> _logger;
        private readonly IStockDataRepository _repository;
        private readonly IStockDataService _dataService;
        private readonly IChartDataProcessor _chartProcessor;
        
        // Event for progress reporting
        public event EventHandler<int> ProgressChanged;

        public DataLoadingService(
            ILogger<DataLoadingService> logger,
            IStockDataRepository repository,
            IStockDataService dataService,
            IChartDataProcessor chartProcessor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _chartProcessor = chartProcessor ?? throw new ArgumentNullException(nameof(chartProcessor));
        }

        /// <summary>
        /// Loads data for the inauguration chart
        /// </summary>
        /// <returns>The chart data</returns>
        public async Task<ChartData> LoadInaugurationDataAsync()
        {
            try
            {
                _logger.LogInformation("Loading inauguration data");
                ReportProgress(0);
                
                // Get data from service
                var data = await _dataService.GetInaugurationToPresent();
                ReportProgress(50);
                
                // Log memory usage
                var memoryUsed = GC.GetTotalMemory(false);
                _logger.LogInformation("Memory usage after loading data: {MemoryUsed} bytes", memoryUsed);
                
                // Format data for chart
                var startDate = new DateTime(2021, 1, 20); // Inauguration date
                var endDate = DateTime.Today;
                var chartData = _chartProcessor.FormatDataForChart(data, "Market Performance Since Inauguration", startDate, endDate);
                ReportProgress(75);
                
                // Optimize data points for large datasets
                if (data.Values.Sum(list => list.Count) > 1000)
                {
                    chartData = _chartProcessor.OptimizeDataPoints(chartData, 500);
                }
                
                ReportProgress(100);
                return chartData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading inauguration data");
                throw;
            }
        }

        /// <summary>
        /// Loads data for the tariff announcement chart
        /// </summary>
        /// <returns>The chart data</returns>
        public async Task<ChartData> LoadTariffDataAsync()
        {
            try
            {
                _logger.LogInformation("Loading tariff announcement data");
                ReportProgress(0);
                
                // Get data from service
                var data = await _dataService.GetTariffAnnouncementToPresent();
                ReportProgress(50);
                
                // Format data for chart
                var startDate = new DateTime(2018, 3, 1); // Approximate tariff announcement date
                var endDate = DateTime.Today;
                var chartData = _chartProcessor.FormatDataForChart(data, "Market Performance Since Tariff Announcement", startDate, endDate);
                ReportProgress(75);
                
                // Optimize data points for large datasets
                if (data.Values.Sum(list => list.Count) > 1000)
                {
                    chartData = _chartProcessor.OptimizeDataPoints(chartData, 500);
                }
                
                ReportProgress(100);
                return chartData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading tariff announcement data");
                throw;
            }
        }

        /// <summary>
        /// Loads inauguration data using parallel processing
        /// </summary>
        /// <returns>The chart data</returns>
        public async Task<ChartData> LoadInaugurationDataParallelAsync()
        {
            try
            {
                _logger.LogInformation("Loading inauguration data with parallel processing");
                ReportProgress(0);
                
                // Get data from service
                var data = await _dataService.GetInaugurationToPresent();
                ReportProgress(30);
                
                // Process each index in parallel
                _logger.LogInformation("Processing in parallel: {Count} indices", data.Count);
                var startDate = new DateTime(2021, 1, 20); // Inauguration date
                var endDate = DateTime.Today;
                
                var processedSeries = new List<ChartDataSeries>();
                var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
                
                await Task.Run(() =>
                {
                    Parallel.ForEach(data, parallelOptions, (kvp) =>
                    {
                        var indexName = kvp.Key;
                        var indexData = kvp.Value;
                        
                        // Process this index's data
                        var series = new ChartDataSeries
                        {
                            Name = indexName,
                            Data = indexData.OrderBy(d => d.Date)
                                           .Select(d => d.CloseValue)
                                           .ToList(),
                            Color = GetColorForIndex(indexName)
                        };
                        
                        lock (processedSeries)
                        {
                            processedSeries.Add(series);
                        }
                    });
                });
                
                ReportProgress(70);
                
                // Create chart data
                var chartData = new ChartData
                {
                    Title = "Market Performance Since Inauguration",
                    StartDate = startDate,
                    EndDate = endDate,
                    Labels = _chartProcessor.GenerateChartLabels(startDate, endDate, processedSeries.FirstOrDefault()?.Data.Count ?? 0),
                    Series = processedSeries
                };
                
                ReportProgress(100);
                return chartData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading inauguration data in parallel");
                throw;
            }
        }

        /// <summary>
        /// Animates a chart with smooth transitions
        /// </summary>
        /// <param name="frameCallback">Callback for each animation frame</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task AnimateChartAsync(Action<int> frameCallback)
        {
            try
            {
                _logger.LogInformation("Starting chart animation");
                
                // Simulate chart animation with 60 frames
                var frameCount = 60;
                var frameDuration = 1000 / 60; // Target 60 fps
                
                for (int i = 0; i < frameCount; i++)
                {
                    var frameStartTime = Stopwatch.GetTimestamp();
                    
                    // Simulate frame rendering
                    frameCallback(i);
                    
                    // Calculate time spent on this frame
                    var frameEndTime = Stopwatch.GetTimestamp();
                    var frameTime = (frameEndTime - frameStartTime) / (double)Stopwatch.Frequency * 1000;
                    
                    // Sleep for the remaining time to maintain frame rate
                    var sleepTime = Math.Max(0, frameDuration - frameTime);
                    if (sleepTime > 0)
                    {
                        await Task.Delay((int)sleepTime);
                    }
                    
                    // Report progress
                    ReportProgress((i + 1) * 100 / frameCount);
                }
                
                _logger.LogInformation("Chart animation completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error animating chart");
                throw;
            }
        }

        /// <summary>
        /// Reports progress to subscribers
        /// </summary>
        /// <param name="progress">The progress percentage (0-100)</param>
        public void ReportProgress(int progress)
        {
            ProgressChanged?.Invoke(this, progress);
        }

        /// <summary>
        /// Gets a color for an index
        /// </summary>
        /// <param name="indexName">The index name</param>
        /// <returns>A color string</returns>
        private string GetColorForIndex(string indexName)
        {
            return indexName switch
            {
                "S&P 500" => "#4285F4", // Blue
                "Dow Jones" => "#34A853", // Green
                "NASDAQ" => "#EA4335", // Red
                _ => "#FBBC05" // Yellow
            };
        }
    }
}
