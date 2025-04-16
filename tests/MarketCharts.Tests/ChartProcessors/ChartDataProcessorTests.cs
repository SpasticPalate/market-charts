using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using MarketCharts.Client.Models;
using MarketCharts.Client.Models.Chart;
using MarketCharts.Client.Services.ChartProcessors;

namespace MarketCharts.Tests.ChartProcessors
{
    public class ChartDataProcessorTests
    {
        private readonly ChartDataProcessor _processor;
        private readonly Dictionary<string, List<StockIndexData>> _testData;
        private readonly DateTime _startDate;
        private readonly DateTime _endDate;

        public ChartDataProcessorTests()
        {
            _processor = new ChartDataProcessor();
            _startDate = new DateTime(2022, 1, 1);
            _endDate = new DateTime(2022, 1, 10);
            
            // Create test data
            _testData = new Dictionary<string, List<StockIndexData>>
            {
                {
                    "S&P 500", new List<StockIndexData>
                    {
                        CreateStockData("S&P 500", _startDate, 4000m),
                        CreateStockData("S&P 500", _startDate.AddDays(1), 4050m),
                        CreateStockData("S&P 500", _startDate.AddDays(2), 4100m),
                        CreateStockData("S&P 500", _startDate.AddDays(3), 4080m),
                        CreateStockData("S&P 500", _startDate.AddDays(4), 4120m),
                        CreateStockData("S&P 500", _startDate.AddDays(5), 4150m),
                        CreateStockData("S&P 500", _startDate.AddDays(6), 4200m),
                        CreateStockData("S&P 500", _startDate.AddDays(7), 4180m),
                        CreateStockData("S&P 500", _startDate.AddDays(8), 4220m),
                        CreateStockData("S&P 500", _startDate.AddDays(9), 4250m)
                    }
                },
                {
                    "Dow Jones", new List<StockIndexData>
                    {
                        CreateStockData("Dow Jones", _startDate, 32000m),
                        CreateStockData("Dow Jones", _startDate.AddDays(1), 32200m),
                        CreateStockData("Dow Jones", _startDate.AddDays(2), 32400m),
                        CreateStockData("Dow Jones", _startDate.AddDays(3), 32300m),
                        CreateStockData("Dow Jones", _startDate.AddDays(4), 32500m),
                        CreateStockData("Dow Jones", _startDate.AddDays(5), 32700m),
                        CreateStockData("Dow Jones", _startDate.AddDays(6), 32900m),
                        CreateStockData("Dow Jones", _startDate.AddDays(7), 32800m),
                        CreateStockData("Dow Jones", _startDate.AddDays(8), 33000m),
                        CreateStockData("Dow Jones", _startDate.AddDays(9), 33200m)
                    }
                }
            };
        }

        [Fact]
        public void Should_FormatDataForCharts_When_RawDataProvided()
        {
            // Arrange
            var title = "Test Chart";

            // Act
            var result = _processor.FormatDataForChart(_testData, title, _startDate, _endDate);

            // Assert
            Assert.Equal(title, result.Title);
            Assert.Equal(_startDate, result.StartDate);
            Assert.Equal(_endDate, result.EndDate);
            Assert.Equal(10, result.Labels.Count);
            Assert.Equal(2, result.Series.Count);
            
            var sp500Series = result.Series.FirstOrDefault(s => s.Name == "S&P 500");
            var dowSeries = result.Series.FirstOrDefault(s => s.Name == "Dow Jones");
            
            Assert.NotNull(sp500Series);
            Assert.NotNull(dowSeries);
            Assert.Equal(10, sp500Series.Data.Count);
            Assert.Equal(10, dowSeries.Data.Count);
            Assert.Equal(4000m, sp500Series.Data[0]);
            Assert.Equal(32000m, dowSeries.Data[0]);
        }

        [Fact]
        public void Should_CalculatePercentageChanges_When_Required()
        {
            // Arrange
            var baseDate = _startDate;

            // Act
            var result = _processor.CalculatePercentageChanges(_testData, baseDate);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.True(result.ContainsKey("S&P 500"));
            Assert.True(result.ContainsKey("Dow Jones"));
            
            var sp500Changes = result["S&P 500"];
            var dowChanges = result["Dow Jones"];
            
            Assert.Equal(10, sp500Changes.Count);
            Assert.Equal(10, dowChanges.Count);
            
            // First value should be 0% (base value)
            Assert.Equal(0m, sp500Changes[0]);
            Assert.Equal(0m, dowChanges[0]);
            
            // Check a few other values
            Assert.Equal(1.25m, sp500Changes[1]); // (4050 - 4000) / 4000 * 100 = 1.25%
            Assert.Equal(6.25m, sp500Changes[9]); // (4250 - 4000) / 4000 * 100 = 6.25%
            
            Assert.Equal(0.625m, dowChanges[1]); // (32200 - 32000) / 32000 * 100 = 0.625%
            Assert.Equal(3.75m, dowChanges[9]); // (33200 - 32000) / 32000 * 100 = 3.75%
        }

        [Fact]
        public void Should_GenerateAppropriateLabels_When_ChartRendered()
        {
            // Arrange
            var startDate = new DateTime(2022, 1, 1);
            var endDate = new DateTime(2022, 1, 10);
            var dataPoints = 5; // We want 5 labels distributed evenly

            // Act
            var result = _processor.GenerateChartLabels(startDate, endDate, dataPoints);

            // Assert
            Assert.Equal(5, result.Count);
            Assert.Equal("01/01/2022", result[0]);
            Assert.Equal("01/03/2022", result[1]); // Approximately every 2.25 days
            Assert.Equal("01/05/2022", result[2]);
            Assert.Equal("01/07/2022", result[3]);
            Assert.Equal("01/10/2022", result[4]); // Last date should be the end date
        }

        [Fact]
        public void Should_ApplyTechnicalIndicators_When_DataProcessed()
        {
            // Arrange
            var chartData = _processor.FormatDataForChart(_testData, "Test Chart", _startDate, _endDate);
            var indicators = new List<string> { "SMA" };

            // Act
            var result = _processor.ApplyTechnicalIndicators(chartData, indicators);

            // Assert
            Assert.NotNull(result.TechnicalIndicators);
            Assert.True(result.TechnicalIndicators.Count > 0);
            
            // Check for SMA indicators
            var sma20 = result.TechnicalIndicators.FirstOrDefault(i => i.Name == "SMA20");
            Assert.NotNull(sma20);
            Assert.Equal("SMA20", sma20.Name);
            Assert.Equal(10, sma20.Data.Count);
            
            // First few values should be null (not enough data points)
            Assert.Null(sma20.Data[0]);
            Assert.Null(sma20.Data[1]);
            
            // Check that parameters are set correctly
            Assert.True(sma20.Parameters.ContainsKey("Period"));
            Assert.Equal(20, sma20.Parameters["Period"]);
        }

        [Fact]
        public void Should_HandleMissingDates_When_DataHasGaps()
        {
            // Arrange
            var gappyData = new Dictionary<string, List<StockIndexData>>
            {
                {
                    "S&P 500", new List<StockIndexData>
                    {
                        CreateStockData("S&P 500", _startDate, 4000m),
                        // Missing day 1
                        CreateStockData("S&P 500", _startDate.AddDays(2), 4100m),
                        CreateStockData("S&P 500", _startDate.AddDays(3), 4080m),
                        // Missing day 4
                        CreateStockData("S&P 500", _startDate.AddDays(5), 4150m)
                    }
                }
            };
            
            var chartData = _processor.FormatDataForChart(gappyData, "Gappy Chart", _startDate, _startDate.AddDays(5));

            // Act
            var result = _processor.HandleMissingDates(chartData);

            // Assert
            Assert.Equal(4, chartData.Labels.Count); // Original has 4 data points
            Assert.Equal(6, result.Labels.Count); // Should now have 6 data points (no weekends)
            
            var series = result.Series.First();
            Assert.Equal(6, series.Data.Count);
            
            // Check that missing values are filled
            Assert.Equal(4000m, series.Data[0]); // Original value
            Assert.Equal(4000m, series.Data[1]); // Filled with previous value
            Assert.Equal(4100m, series.Data[2]); // Original value
            Assert.Equal(4080m, series.Data[3]); // Original value
            Assert.Equal(4080m, series.Data[4]); // Filled with previous value
            Assert.Equal(4150m, series.Data[5]); // Original value
        }

        [Fact]
        public void Should_AlignMultipleDataSeries_When_ComparingIndices()
        {
            // Arrange
            var series1 = new ChartDataSeries
            {
                Name = "Series 1",
                Data = new List<decimal> { 100m, 110m, 120m, 130m, 140m }
            };
            
            var series2 = new ChartDataSeries
            {
                Name = "Series 2",
                Data = new List<decimal> { 200m, 210m, 220m }
            };
            
            var series3 = new ChartDataSeries
            {
                Name = "Series 3",
                Data = new List<decimal> { 300m, 310m, 320m, 330m }
            };
            
            var seriesList = new List<ChartDataSeries> { series1, series2, series3 };

            // Act
            var result = _processor.AlignDataSeries(seriesList);

            // Assert
            Assert.Equal(3, result.Count);
            
            // All series should now have the same length (shortest series length)
            Assert.Equal(3, result[0].Data.Count);
            Assert.Equal(3, result[1].Data.Count);
            Assert.Equal(3, result[2].Data.Count);
            
            // Check that data is preserved correctly
            Assert.Equal(100m, result[0].Data[0]);
            Assert.Equal(110m, result[0].Data[1]);
            Assert.Equal(120m, result[0].Data[2]);
            
            Assert.Equal(200m, result[1].Data[0]);
            Assert.Equal(210m, result[1].Data[1]);
            Assert.Equal(220m, result[1].Data[2]);
            
            Assert.Equal(300m, result[2].Data[0]);
            Assert.Equal(310m, result[2].Data[1]);
            Assert.Equal(320m, result[2].Data[2]);
        }

        [Fact]
        public void Should_GenerateComparisonData_When_PreviousAdministrationSelected()
        {
            // Arrange
            var currentData = _processor.FormatDataForChart(_testData, "Current Admin", _startDate, _endDate);
            
            var previousData = new Dictionary<string, List<StockIndexData>>
            {
                {
                    "S&P 500", new List<StockIndexData>
                    {
                        CreateStockData("S&P 500", _startDate.AddYears(-4), 2500m),
                        CreateStockData("S&P 500", _startDate.AddYears(-4).AddDays(5), 2600m),
                        CreateStockData("S&P 500", _startDate.AddYears(-4).AddDays(9), 2700m)
                    }
                },
                {
                    "Dow Jones", new List<StockIndexData>
                    {
                        CreateStockData("Dow Jones", _startDate.AddYears(-4), 25000m),
                        CreateStockData("Dow Jones", _startDate.AddYears(-4).AddDays(5), 25500m),
                        CreateStockData("Dow Jones", _startDate.AddYears(-4).AddDays(9), 26000m)
                    }
                }
            };

            // Act
            var result = _processor.GenerateComparisonData(currentData, previousData);

            // Assert
            Assert.Equal("Current Admin (with comparison)", result.Title);
            Assert.Equal(4, result.Series.Count); // 2 original + 2 comparison series
            
            var comparisonSeries = result.Series.Where(s => s.IsComparison).ToList();
            Assert.Equal(2, comparisonSeries.Count);
            
            var sp500Comparison = comparisonSeries.FirstOrDefault(s => s.Name == "S&P 500 (Previous)");
            var dowComparison = comparisonSeries.FirstOrDefault(s => s.Name == "Dow Jones (Previous)");
            
            Assert.NotNull(sp500Comparison);
            Assert.NotNull(dowComparison);
            
            // Check that comparison data is included
            Assert.Contains(2500m, sp500Comparison.Data);
            Assert.Contains(25000m, dowComparison.Data);
        }

        [Fact]
        public void Should_CalculateMovingAverages_When_TechnicalIndicatorsEnabled()
        {
            // Arrange
            var series = new ChartDataSeries
            {
                Name = "Test Series",
                Data = new List<decimal> { 100m, 110m, 120m, 130m, 140m, 150m, 140m, 130m, 120m, 110m }
            };
            
            var periods = new List<int> { 3, 5 };

            // Act
            var result = _processor.CalculateMovingAverages(series, periods);

            // Assert
            Assert.Equal(2, result.Count);
            
            var sma3 = result.FirstOrDefault(i => i.Name == "SMA3");
            var sma5 = result.FirstOrDefault(i => i.Name == "SMA5");
            
            Assert.NotNull(sma3);
            Assert.NotNull(sma5);
            
            // Check SMA3 values
            Assert.Equal(10, sma3.Data.Count);
            Assert.Null(sma3.Data[0]); // Not enough data yet
            Assert.Null(sma3.Data[1]); // Not enough data yet
            Assert.Equal(110m, sma3.Data[2]); // (100 + 110 + 120) / 3 = 110
            Assert.Equal(120m, sma3.Data[3]); // (110 + 120 + 130) / 3 = 120
            
            // Check SMA5 values
            Assert.Equal(10, sma5.Data.Count);
            Assert.Null(sma5.Data[0]); // Not enough data yet
            Assert.Null(sma5.Data[1]); // Not enough data yet
            Assert.Null(sma5.Data[2]); // Not enough data yet
            Assert.Null(sma5.Data[3]); // Not enough data yet
            Assert.Equal(120m, sma5.Data[4]); // (100 + 110 + 120 + 130 + 140) / 5 = 120
        }

        [Fact]
        public void Should_IdentifyTrends_When_AnalyzingMarketData()
        {
            // Arrange
            var uptrend = new ChartDataSeries
            {
                Name = "Uptrend",
                Data = new List<decimal> { 100m, 110m, 120m, 130m, 140m, 150m }
            };
            
            var downtrend = new ChartDataSeries
            {
                Name = "Downtrend",
                Data = new List<decimal> { 150m, 140m, 130m, 120m, 110m, 100m }
            };
            
            var volatileSeries = new ChartDataSeries
            {
                Name = "Volatile",
                Data = new List<decimal> { 100m, 120m, 90m, 130m, 80m, 140m }
            };

            // Act
            var uptrendResult = _processor.IdentifyTrends(uptrend);
            var downtrendResult = _processor.IdentifyTrends(downtrend);
            var volatileResult = _processor.IdentifyTrends(volatileSeries);

            // Assert
            Assert.Contains("Strong Uptrend", uptrendResult);
            Assert.Contains("Strong Downtrend", downtrendResult);
            Assert.Contains("High Volatility", volatileResult);
        }

        [Fact]
        public void Should_GenerateRelativeStrengthIndex_When_TechnicalIndicatorsEnabled()
        {
            // Arrange
            var series = new ChartDataSeries
            {
                Name = "Test Series",
                Data = Enumerable.Range(1, 30).Select(i => (decimal)(100 + i * 2 + (i % 3 == 0 ? -5 : 0))).ToList()
            };

            // Act
            var result = _processor.CalculateRSI(series, 14);

            // Assert
            Assert.Equal("RSI", result.Name);
            Assert.Equal(30, result.Data.Count);
            
            // First 14 values should be null
            for (int i = 0; i < 14; i++)
            {
                Assert.Null(result.Data[i]);
            }
            
            // Remaining values should be between 0 and 100
            for (int i = 14; i < 30; i++)
            {
                Assert.NotNull(result.Data[i]);
                Assert.True(result.Data[i].Value >= 0);
                Assert.True(result.Data[i].Value <= 100);
            }
        }

        [Fact]
        public void Should_CalculateVolatility_When_TechnicalIndicatorsEnabled()
        {
            // Arrange
            var lowVolatility = new ChartDataSeries
            {
                Name = "Low Volatility",
                Data = Enumerable.Range(1, 30).Select(i => 100m + i * 0.1m).ToList()
            };
            
            var highVolatility = new ChartDataSeries
            {
                Name = "High Volatility",
                Data = Enumerable.Range(1, 30).Select(i => 100m + (i % 2 == 0 ? 5m : -5m)).ToList()
            };

            // Act
            var lowVolResult = _processor.CalculateVolatility(lowVolatility, 10);
            var highVolResult = _processor.CalculateVolatility(highVolatility, 10);

            // Assert
            Assert.Equal("Volatility", lowVolResult.Name);
            Assert.Equal("Volatility", highVolResult.Name);
            
            Assert.Equal(30, lowVolResult.Data.Count);
            Assert.Equal(30, highVolResult.Data.Count);
            
            // First 10 values should be null
            for (int i = 0; i < 10; i++)
            {
                Assert.Null(lowVolResult.Data[i]);
                Assert.Null(highVolResult.Data[i]);
            }
            
            // Check that high volatility values are higher than low volatility values
            for (int i = 10; i < 30; i++)
            {
                Assert.NotNull(lowVolResult.Data[i]);
                Assert.NotNull(highVolResult.Data[i]);
                Assert.True(highVolResult.Data[i].Value > lowVolResult.Data[i].Value);
            }
        }

        [Fact]
        public void Should_NormalizeData_When_ComparingDifferentScales()
        {
            // Arrange
            var series1 = new ChartDataSeries
            {
                Name = "Large Scale",
                Data = new List<decimal> { 10000m, 10500m, 11000m, 10800m, 11200m }
            };
            
            var series2 = new ChartDataSeries
            {
                Name = "Small Scale",
                Data = new List<decimal> { 100m, 105m, 110m, 108m, 112m }
            };
            
            var seriesList = new List<ChartDataSeries> { series1, series2 };

            // Act
            var result = _processor.NormalizeData(seriesList);

            // Assert
            Assert.Equal(2, result.Count);
            
            // Both series should start at 100
            Assert.Equal(100m, result[0].Data[0]);
            Assert.Equal(100m, result[1].Data[0]);
            
            // Both series should have the same relative changes
            Assert.Equal(105m, result[0].Data[1]); // (10500 / 10000) * 100 = 105
            Assert.Equal(105m, result[1].Data[1]); // (105 / 100) * 100 = 105
            
            Assert.Equal(110m, result[0].Data[2]); // (11000 / 10000) * 100 = 110
            Assert.Equal(110m, result[1].Data[2]); // (110 / 100) * 100 = 110
            
            // Check that data type is updated
            Assert.Equal("Normalized (%)", result[0].DataType);
            Assert.Equal("Normalized (%)", result[1].DataType);
        }

        [Fact]
        public void Should_HandleTimeZoneConversions_When_ProcessingData()
        {
            // This test is a placeholder since the actual implementation would depend on
            // how time zones are handled in the application. For now, we'll just verify
            // that dates are preserved correctly in the chart data.
            
            // Arrange
            var title = "Test Chart";

            // Act
            var result = _processor.FormatDataForChart(_testData, title, _startDate, _endDate);

            // Assert
            Assert.Equal(_startDate, result.StartDate);
            Assert.Equal(_endDate, result.EndDate);
            
            // Verify that the first label corresponds to the start date
            Assert.Equal(_startDate.ToString("MM/dd/yyyy"), result.Labels[0]);
            
            // Verify that the last label corresponds to the end date
            Assert.Equal(_endDate.ToString("MM/dd/yyyy"), result.Labels[result.Labels.Count - 1]);
        }

        [Fact]
        public void Should_OptimizeDataPoints_When_LargeDatasetDisplayed()
        {
            // Arrange
            var largeData = new Dictionary<string, List<StockIndexData>>
            {
                {
                    "S&P 500", Enumerable.Range(0, 100).Select(i => 
                        CreateStockData("S&P 500", _startDate.AddDays(i), 4000m + i * 10m)
                    ).ToList()
                }
            };
            
            var chartData = _processor.FormatDataForChart(largeData, "Large Dataset", _startDate, _startDate.AddDays(99));
            var maxPoints = 20;

            // Act
            var result = _processor.OptimizeDataPoints(chartData, maxPoints);

            // Assert
            Assert.Equal(100, chartData.Labels.Count); // Original has 100 data points
            Assert.True(result.Labels.Count <= maxPoints); // Should have at most maxPoints
            
            // Check that data is sampled correctly
            var series = result.Series.First();
            Assert.Equal(result.Labels.Count, series.Data.Count);
            
            // First and last points should be preserved
            Assert.Equal(4000m, series.Data[0]);
            Assert.Equal(4990m, series.Data[series.Data.Count - 1]);
        }

        [Fact]
        public void Should_GenerateAnnotations_When_SignificantEventsDetected()
        {
            // Arrange
            var chartData = _processor.FormatDataForChart(_testData, "Test Chart", _startDate, _endDate);
            
            var events = new Dictionary<DateTime, string>
            {
                { _startDate.AddDays(2), "Important Announcement" },
                { _startDate.AddDays(5), "Policy Change" },
                { _startDate.AddDays(8), "Market Event" }
            };

            // Act
            var result = _processor.GenerateAnnotations(chartData, events);

            // Assert
            Assert.NotNull(result.Annotations);
            Assert.Equal(3, result.Annotations.Count);
            
            // Check that annotations are created correctly
            var annotation1 = result.Annotations.FirstOrDefault(a => a.Date == _startDate.AddDays(2));
            var annotation2 = result.Annotations.FirstOrDefault(a => a.Date == _startDate.AddDays(5));
            var annotation3 = result.Annotations.FirstOrDefault(a => a.Date == _startDate.AddDays(8));
            
            Assert.NotNull(annotation1);
            Assert.NotNull(annotation2);
            Assert.NotNull(annotation3);
            
            Assert.Equal("Important Announcement", annotation1.Text);
            Assert.Equal("Policy Change", annotation2.Text);
            Assert.Equal("Market Event", annotation3.Text);
            
            Assert.Equal("Event", annotation1.Type);
        }

        private StockIndexData CreateStockData(string indexName, DateTime date, decimal closeValue)
        {
            return new StockIndexData
            {
                Id = 0,
                IndexName = indexName,
                Date = date,
                CloseValue = closeValue,
                OpenValue = closeValue * 0.99m,
                HighValue = closeValue * 1.01m,
                LowValue = closeValue * 0.98m,
                Volume = 1000000,
                FetchedAt = DateTime.Now
            };
        }
    }
}
