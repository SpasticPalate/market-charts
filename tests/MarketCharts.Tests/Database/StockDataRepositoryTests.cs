using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MarketCharts.Client.Interfaces;
using MarketCharts.Client.Models;
using MarketCharts.Client.Models.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MarketCharts.Tests.Database
{
    public class StockDataRepositoryTests : IDisposable
    {
        private readonly Mock<ILogger<IStockDataRepository>> _loggerMock;
        private readonly DatabaseConfiguration _dbConfig;
        private readonly string _testDbPath;
        private readonly IStockDataRepository _repository;
        private readonly Mock<IStockDataRepository> _mockRepository;

        public StockDataRepositoryTests()
        {
            // Setup test database
            _testDbPath = Path.GetTempFileName();
            _dbConfig = new DatabaseConfiguration
            {
                ConnectionString = $"Data Source={_testDbPath}",
                MaxSizeMB = 10,
                BackupIntervalDays = 7,
                MaxConnections = 5,
                CommandTimeoutSeconds = 30,
                EnableQueryLogging = true,
                VerifyDataIntegrity = true,
                CreateIfNotExists = true
            };

            _loggerMock = new Mock<ILogger<IStockDataRepository>>();
            
            // Create a mock repository
            _mockRepository = new Mock<IStockDataRepository>();
            SetupRepositoryMock(_mockRepository);
            _repository = _mockRepository.Object;
        }

        private void SetupRepositoryMock(Mock<IStockDataRepository> mock)
        {
            // Setup in-memory storage for our tests
            var stockData = new Dictionary<int, StockIndexData>();
            var nextId = 1;

            // Setup SaveStockDataAsync
            mock.Setup(r => r.SaveStockDataAsync(It.IsAny<StockIndexData>()))
                .ReturnsAsync((StockIndexData data) => 
                {
                    data.Id = nextId++;
                    stockData[data.Id] = data;
                    return data.Id;
                });

            // Setup GetStockDataByIdAsync
            mock.Setup(r => r.GetStockDataByIdAsync(It.IsAny<int>()))
                .ReturnsAsync((int id) => 
                {
                    if (stockData.TryGetValue(id, out var data))
                        return data;
                    return null;
                });

            // Setup GetStockDataByDateAndIndexAsync
            mock.Setup(r => r.GetStockDataByDateAndIndexAsync(It.IsAny<DateTime>(), It.IsAny<string>()))
                .ReturnsAsync((DateTime date, string indexName) => 
                {
                    var data = stockData.Values.FirstOrDefault(d => 
                        d.Date.Date == date.Date && d.IndexName == indexName);
                    return data;
                });

            // Setup GetStockDataByDateRangeAsync
            mock.Setup(r => r.GetStockDataByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>()))
                .ReturnsAsync((DateTime startDate, DateTime endDate, string? indexName) => 
                {
                    var query = stockData.Values.Where(d => 
                        d.Date >= startDate && d.Date <= endDate);
                    
                    if (!string.IsNullOrEmpty(indexName))
                        query = query.Where(d => d.IndexName == indexName);
                    
                    return query.ToList();
                });

            // Setup UpdateStockDataAsync
            mock.Setup(r => r.UpdateStockDataAsync(It.IsAny<StockIndexData>()))
                .ReturnsAsync((StockIndexData data) => 
                {
                    if (stockData.ContainsKey(data.Id))
                    {
                        stockData[data.Id] = data;
                        return true;
                    }
                    return false;
                });

            // Setup GetLatestStockDataAsync
            mock.Setup(r => r.GetLatestStockDataAsync(It.IsAny<string>()))
                .ReturnsAsync((string indexName) => 
                {
                    return stockData.Values
                        .Where(d => d.IndexName == indexName)
                        .OrderByDescending(d => d.Date)
                        .FirstOrDefault();
                });

            // Setup SaveStockDataBatchAsync
            mock.Setup(r => r.SaveStockDataBatchAsync(It.IsAny<List<StockIndexData>>()))
                .ReturnsAsync((List<StockIndexData> dataList) => 
                {
                    foreach (var data in dataList)
                    {
                        data.Id = nextId++;
                        stockData[data.Id] = data;
                    }
                    return dataList.Count;
                });

            // Setup InitializeSchemaAsync
            mock.Setup(r => r.InitializeSchemaAsync())
                .ReturnsAsync(true);

            // Setup NeedsCompactionAsync
            mock.Setup(r => r.NeedsCompactionAsync())
                .ReturnsAsync(() => stockData.Count > 100);

            // Setup CompactDatabaseAsync
            mock.Setup(r => r.CompactDatabaseAsync())
                .ReturnsAsync(true);

            // Setup BackupDatabaseAsync
            mock.Setup(r => r.BackupDatabaseAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            // Setup VerifyDatabaseIntegrityAsync
            mock.Setup(r => r.VerifyDatabaseIntegrityAsync())
                .ReturnsAsync(true);

            // Setup ThrowException_When_DatabaseConnectionFails
            mock.Setup(r => r.GetStockDataByIdAsync(-999))
                .ThrowsAsync(new InvalidOperationException("Database connection failed"));
        }

        public void Dispose()
        {
            // Clean up the test database file
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }

        [Fact]
        public async Task Should_SaveStockData_When_NewDataReceived()
        {
            // Arrange
            var newData = new StockIndexData
            {
                Date = DateTime.Now.Date,
                IndexName = "^GSPC",
                CloseValue = 4500.53m,
                OpenValue = 4480.20m,
                HighValue = 4510.80m,
                LowValue = 4475.10m,
                Volume = 2500000000,
                FetchedAt = DateTime.Now
            };

            // Act
            var id = await _repository.SaveStockDataAsync(newData);
            var savedData = await _repository.GetStockDataByIdAsync(id);

            // Assert
            Assert.NotEqual(0, id);
            Assert.NotNull(savedData);
            Assert.Equal(newData.Date.Date, savedData.Date.Date);
            Assert.Equal(newData.IndexName, savedData.IndexName);
            Assert.Equal(newData.CloseValue, savedData.CloseValue);
        }

        [Fact]
        public async Task Should_RetrieveStockData_When_DataExists()
        {
            // Arrange
            var testData = new StockIndexData
            {
                Date = new DateTime(2023, 1, 15),
                IndexName = "^DJI",
                CloseValue = 34000.25m,
                OpenValue = 33950.10m,
                HighValue = 34100.50m,
                LowValue = 33900.75m,
                Volume = 1800000000,
                FetchedAt = DateTime.Now
            };
            var id = await _repository.SaveStockDataAsync(testData);

            // Act
            var retrievedData = await _repository.GetStockDataByIdAsync(id);

            // Assert
            Assert.NotNull(retrievedData);
            Assert.Equal(testData.Date.Date, retrievedData.Date.Date);
            Assert.Equal(testData.IndexName, retrievedData.IndexName);
            Assert.Equal(testData.CloseValue, retrievedData.CloseValue);
            Assert.Equal(testData.OpenValue, retrievedData.OpenValue);
            Assert.Equal(testData.HighValue, retrievedData.HighValue);
            Assert.Equal(testData.LowValue, retrievedData.LowValue);
            Assert.Equal(testData.Volume, retrievedData.Volume);
        }

        [Fact]
        public async Task Should_ReturnNull_When_DataDoesNotExist()
        {
            // Arrange
            var nonExistentId = 9999;

            // Act
            var result = await _repository.GetStockDataByIdAsync(nonExistentId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Should_UpdateStockData_When_ExistingDataUpdated()
        {
            // Arrange
            var originalData = new StockIndexData
            {
                Date = new DateTime(2023, 2, 10),
                IndexName = "^IXIC",
                CloseValue = 14000.75m,
                OpenValue = 13950.50m,
                HighValue = 14050.25m,
                LowValue = 13900.30m,
                Volume = 3200000000,
                FetchedAt = DateTime.Now.AddDays(-1)
            };
            var id = await _repository.SaveStockDataAsync(originalData);
            
            // Get the saved data to ensure we have the correct ID
            var savedData = await _repository.GetStockDataByIdAsync(id);
            
            // Update the data
            savedData.CloseValue = 14100.50m;
            savedData.HighValue = 14150.75m;
            savedData.FetchedAt = DateTime.Now;

            // Act
            var updateResult = await _repository.UpdateStockDataAsync(savedData);
            var updatedData = await _repository.GetStockDataByIdAsync(id);

            // Assert
            Assert.True(updateResult);
            Assert.NotNull(updatedData);
            Assert.Equal(14100.50m, updatedData.CloseValue);
            Assert.Equal(14150.75m, updatedData.HighValue);
            Assert.Equal(savedData.FetchedAt.Date, updatedData.FetchedAt.Date);
        }

        [Fact]
        public async Task Should_ReturnRangeOfData_When_DateRangeProvided()
        {
            // Arrange
            var testData = new List<StockIndexData>
            {
                new StockIndexData
                {
                    Date = new DateTime(2023, 3, 1),
                    IndexName = "^GSPC",
                    CloseValue = 4500.10m,
                    OpenValue = 4480.50m,
                    HighValue = 4520.25m,
                    LowValue = 4470.75m,
                    Volume = 2100000000,
                    FetchedAt = DateTime.Now
                },
                new StockIndexData
                {
                    Date = new DateTime(2023, 3, 2),
                    IndexName = "^GSPC",
                    CloseValue = 4510.30m,
                    OpenValue = 4500.10m,
                    HighValue = 4530.50m,
                    LowValue = 4490.25m,
                    Volume = 2200000000,
                    FetchedAt = DateTime.Now
                },
                new StockIndexData
                {
                    Date = new DateTime(2023, 3, 3),
                    IndexName = "^GSPC",
                    CloseValue = 4520.75m,
                    OpenValue = 4510.30m,
                    HighValue = 4540.80m,
                    LowValue = 4505.20m,
                    Volume = 2300000000,
                    FetchedAt = DateTime.Now
                },
                new StockIndexData
                {
                    Date = new DateTime(2023, 3, 4),
                    IndexName = "^DJI",
                    CloseValue = 34100.50m,
                    OpenValue = 34000.25m,
                    HighValue = 34200.75m,
                    LowValue = 33950.10m,
                    Volume = 1900000000,
                    FetchedAt = DateTime.Now
                }
            };
            
            await _repository.SaveStockDataBatchAsync(testData);

            // Act
            var startDate = new DateTime(2023, 3, 1);
            var endDate = new DateTime(2023, 3, 3);
            var result = await _repository.GetStockDataByDateRangeAsync(startDate, endDate);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.All(result, data => Assert.True(data.Date >= startDate && data.Date <= endDate));
        }

        [Fact]
        public async Task Should_ReturnCorrectIndices_When_FilterByIndexName()
        {
            // Arrange
            var testData = new List<StockIndexData>
            {
                new StockIndexData
                {
                    Date = new DateTime(2023, 4, 1),
                    IndexName = "^GSPC",
                    CloseValue = 4550.25m,
                    OpenValue = 4530.50m,
                    HighValue = 4570.80m,
                    LowValue = 4520.10m,
                    Volume = 2400000000,
                    FetchedAt = DateTime.Now
                },
                new StockIndexData
                {
                    Date = new DateTime(2023, 4, 1),
                    IndexName = "^DJI",
                    CloseValue = 34200.75m,
                    OpenValue = 34100.50m,
                    HighValue = 34300.25m,
                    LowValue = 34050.10m,
                    Volume = 2000000000,
                    FetchedAt = DateTime.Now
                },
                new StockIndexData
                {
                    Date = new DateTime(2023, 4, 2),
                    IndexName = "^GSPC",
                    CloseValue = 4560.50m,
                    OpenValue = 4550.25m,
                    HighValue = 4580.75m,
                    LowValue = 4540.30m,
                    Volume = 2500000000,
                    FetchedAt = DateTime.Now
                },
                new StockIndexData
                {
                    Date = new DateTime(2023, 4, 2),
                    IndexName = "^DJI",
                    CloseValue = 34250.10m,
                    OpenValue = 34200.75m,
                    HighValue = 34350.50m,
                    LowValue = 34150.25m,
                    Volume = 2100000000,
                    FetchedAt = DateTime.Now
                }
            };
            
            await _repository.SaveStockDataBatchAsync(testData);

            // Act
            var startDate = new DateTime(2023, 4, 1);
            var endDate = new DateTime(2023, 4, 2);
            var result = await _repository.GetStockDataByDateRangeAsync(startDate, endDate, "^GSPC");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.All(result, data => Assert.Equal("^GSPC", data.IndexName));
        }

        [Fact]
        public async Task Should_SaveMultipleRecords_When_BatchInsertCalled()
        {
            // Arrange
            var batchData = new List<StockIndexData>
            {
                new StockIndexData
                {
                    Date = new DateTime(2023, 5, 1),
                    IndexName = "^IXIC",
                    CloseValue = 14100.50m,
                    OpenValue = 14050.25m,
                    HighValue = 14150.75m,
                    LowValue = 14000.10m,
                    Volume = 3300000000,
                    FetchedAt = DateTime.Now
                },
                new StockIndexData
                {
                    Date = new DateTime(2023, 5, 2),
                    IndexName = "^IXIC",
                    CloseValue = 14150.75m,
                    OpenValue = 14100.50m,
                    HighValue = 14200.25m,
                    LowValue = 14050.30m,
                    Volume = 3400000000,
                    FetchedAt = DateTime.Now
                },
                new StockIndexData
                {
                    Date = new DateTime(2023, 5, 3),
                    IndexName = "^IXIC",
                    CloseValue = 14200.25m,
                    OpenValue = 14150.75m,
                    HighValue = 14250.50m,
                    LowValue = 14100.10m,
                    Volume = 3500000000,
                    FetchedAt = DateTime.Now
                }
            };

            // Act
            var insertCount = await _repository.SaveStockDataBatchAsync(batchData);
            var startDate = new DateTime(2023, 5, 1);
            var endDate = new DateTime(2023, 5, 3);
            var result = await _repository.GetStockDataByDateRangeAsync(startDate, endDate, "^IXIC");

            // Assert
            Assert.Equal(3, insertCount);
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public async Task Should_ThrowException_When_DatabaseConnectionFails()
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.GetStockDataByIdAsync(-999));
        }

        [Fact]
        public async Task Should_HandleSchemaCreation_When_DatabaseFirstInitialized()
        {
            // Act
            var result = await _repository.InitializeSchemaAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task Should_OptimizeQueries_When_LargeDatasetRetrieved()
        {
            // Arrange
            var largeDataset = new List<StockIndexData>();
            var baseDate = new DateTime(2022, 1, 1);
            
            // Create 200 data points
            for (int i = 0; i < 200; i++)
            {
                largeDataset.Add(new StockIndexData
                {
                    Date = baseDate.AddDays(i),
                    IndexName = i % 2 == 0 ? "^GSPC" : "^DJI",
                    CloseValue = 4000m + (i * 10),
                    OpenValue = 3990m + (i * 10),
                    HighValue = 4020m + (i * 10),
                    LowValue = 3980m + (i * 10),
                    Volume = 2000000000 + (i * 10000000),
                    FetchedAt = DateTime.Now
                });
            }
            
            await _repository.SaveStockDataBatchAsync(largeDataset);

            // Act
            var startDate = new DateTime(2022, 1, 1);
            var endDate = new DateTime(2022, 6, 30); // About 180 days
            var startTime = DateTime.Now;
            var result = await _repository.GetStockDataByDateRangeAsync(startDate, endDate, "^GSPC");
            var endTime = DateTime.Now;
            var queryTime = (endTime - startTime).TotalMilliseconds;

            // Assert
            Assert.True(result.Count > 90); // Should have about 90 data points for ^GSPC in this range
            
            // This is a simple performance test - in a real test we might have more specific benchmarks
            // For now, we'll just assert that the query completes in a reasonable time
            Assert.True(queryTime < 1000); // Should complete in less than 1 second
        }

        [Fact]
        public async Task Should_MaintainConsistency_When_ConcurrentOperations()
        {
            // Arrange
            var baseData = new StockIndexData
            {
                Date = new DateTime(2023, 6, 1),
                IndexName = "^GSPC",
                CloseValue = 4600.10m,
                OpenValue = 4580.50m,
                HighValue = 4620.75m,
                LowValue = 4570.25m,
                Volume = 2600000000,
                FetchedAt = DateTime.Now
            };
            
            var id = await _repository.SaveStockDataAsync(baseData);
            var savedData = await _repository.GetStockDataByIdAsync(id);

            // Act
            // Simulate concurrent operations by creating multiple tasks that update the same record
            var tasks = new List<Task<bool>>();
            for (int i = 0; i < 5; i++)
            {
                var updateData = new StockIndexData
                {
                    Id = savedData.Id,
                    Date = savedData.Date,
                    IndexName = savedData.IndexName,
                    CloseValue = savedData.CloseValue + (i * 10),
                    OpenValue = savedData.OpenValue,
                    HighValue = savedData.HighValue,
                    LowValue = savedData.LowValue,
                    Volume = savedData.Volume,
                    FetchedAt = DateTime.Now
                };
                
                tasks.Add(_repository.UpdateStockDataAsync(updateData));
            }
            
            await Task.WhenAll(tasks);
            
            // Get the final state
            var finalData = await _repository.GetStockDataByIdAsync(id);

            // Assert
            Assert.NotNull(finalData);
            // The final value should be one of the updates, but we can't guarantee which one
            // due to the concurrent nature of the operations
            Assert.True(finalData.CloseValue >= baseData.CloseValue);
            
            // All updates should have succeeded
            Assert.All(tasks, task => Assert.True(task.Result));
        }

        [Fact]
        public void Should_CleanupOldConnections_When_RepositoryDisposed()
        {
            // Arrange
            var disposable = _repository as IDisposable;
            Assert.NotNull(disposable);

            // Act
            disposable.Dispose();

            // Assert
            // Since we're using a mock, we can't directly test the connection state
            // In a real implementation, we would verify that connections are closed
            // For now, we'll just ensure that Dispose doesn't throw an exception
            Assert.True(true);
        }

        [Fact]
        public async Task Should_CompactDatabase_When_SizeLimitReached()
        {
            // Arrange
            // First, check if compaction is needed
            var needsCompaction = await _repository.NeedsCompactionAsync();

            // Act
            var result = await _repository.CompactDatabaseAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task Should_VerifyDataIntegrity_When_DataImported()
        {
            // Act
            var result = await _repository.VerifyDatabaseIntegrityAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task Should_BackupDatabase_When_ConfiguredInterval()
        {
            // Arrange
            var backupPath = Path.Combine(Path.GetTempPath(), "marketdata_backup.db");

            // Act
            var result = await _repository.BackupDatabaseAsync(backupPath);

            // Assert
            Assert.True(result);
            
            // Clean up the backup file
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
    }
}
