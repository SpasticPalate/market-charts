using System;
using System.Collections.Generic;
using System.Linq;
using MarketCharts.Client.Interfaces;
using MarketCharts.Client.Models;
using MarketCharts.Client.Models.Chart;

namespace MarketCharts.Client.Services.ChartProcessors
{
    /// <summary>
    /// Implementation of IChartDataProcessor for processing stock data for chart display
    /// </summary>
    public class ChartDataProcessor : IChartDataProcessor
    {
        /// <summary>
        /// Formats raw stock data for chart display
        /// </summary>
        public ChartData FormatDataForChart(Dictionary<string, List<StockIndexData>> data, string title, DateTime startDate, DateTime endDate)
        {
            if (data == null || !data.Any())
            {
                throw new ArgumentException("Data cannot be null or empty", nameof(data));
            }

            var chartData = new ChartData
            {
                Title = title,
                StartDate = startDate,
                EndDate = endDate
            };

            // Generate labels based on the date range
            var firstSeries = data.First().Value;
            var sortedDates = firstSeries
                .Where(d => d.Date >= startDate && d.Date <= endDate)
                .OrderBy(d => d.Date)
                .Select(d => d.Date)
                .ToList();

            if (sortedDates.Count == 0)
            {
                throw new ArgumentException("No data points found within the specified date range");
            }

            chartData.Labels = sortedDates.Select(d => d.ToString("MM/dd/yyyy")).ToList();

            // Create a series for each index
            foreach (var kvp in data)
            {
                var indexName = kvp.Key;
                var indexData = kvp.Value;

                // Filter data by date range and sort by date
                var filteredData = indexData
                    .Where(d => d.Date >= startDate && d.Date <= endDate)
                    .OrderBy(d => d.Date)
                    .ToList();

                if (filteredData.Count == 0)
                {
                    continue; // Skip if no data points in range
                }

                var series = new ChartDataSeries
                {
                    Name = indexName,
                    Data = filteredData.Select(d => d.CloseValue).ToList(),
                    Color = GetColorForIndex(indexName),
                    DataType = "Price"
                };

                chartData.Series.Add(series);
            }

            return chartData;
        }

        /// <summary>
        /// Calculates percentage changes from a base date
        /// </summary>
        public Dictionary<string, List<decimal>> CalculatePercentageChanges(Dictionary<string, List<StockIndexData>> data, DateTime baseDate)
        {
            if (data == null || !data.Any())
            {
                throw new ArgumentException("Data cannot be null or empty", nameof(data));
            }

            var result = new Dictionary<string, List<decimal>>();

            foreach (var kvp in data)
            {
                var indexName = kvp.Key;
                var indexData = kvp.Value.OrderBy(d => d.Date).ToList();

                // Find the base value
                var baseValue = indexData.FirstOrDefault(d => d.Date.Date == baseDate.Date)?.CloseValue;
                if (baseValue == null || baseValue == 0)
                {
                    // If exact date not found, find closest date before baseDate
                    var closestData = indexData
                        .Where(d => d.Date.Date <= baseDate.Date)
                        .OrderByDescending(d => d.Date)
                        .FirstOrDefault();

                    if (closestData == null)
                    {
                        continue; // Skip if no base value can be found
                    }

                    baseValue = closestData.CloseValue;
                }

                // Calculate percentage changes
                var percentageChanges = indexData
                    .Select(d => CalculatePercentageChange(d.CloseValue, baseValue.Value))
                    .ToList();

                result.Add(indexName, percentageChanges);
            }

            return result;
        }

        /// <summary>
        /// Generates appropriate labels for chart display
        /// </summary>
        public List<string> GenerateChartLabels(DateTime startDate, DateTime endDate, int dataPoints)
        {
            if (dataPoints <= 0)
            {
                throw new ArgumentException("Data points must be greater than zero", nameof(dataPoints));
            }

            if (startDate >= endDate)
            {
                throw new ArgumentException("Start date must be before end date");
            }

            var labels = new List<string>();
            var totalDays = (endDate - startDate).TotalDays;
            
            // If we have more data points than days, use daily labels
            if (dataPoints >= totalDays)
            {
                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    labels.Add(FormatDateLabel(date));
                }
            }
            else
            {
                // Otherwise, distribute labels evenly
                var interval = totalDays / (dataPoints - 1);
                for (int i = 0; i < dataPoints; i++)
                {
                    var date = startDate.AddDays(i * interval);
                    if (date > endDate)
                    {
                        date = endDate;
                    }
                    labels.Add(FormatDateLabel(date));
                }
            }

            return labels;
        }

        /// <summary>
        /// Applies technical indicators to the data
        /// </summary>
        public ChartData ApplyTechnicalIndicators(ChartData data, List<string> indicators)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (indicators == null || !indicators.Any())
            {
                return data; // No indicators to apply
            }

            // Initialize technical indicators list if null
            if (data.TechnicalIndicators == null)
            {
                data.TechnicalIndicators = new List<TechnicalIndicator>();
            }

            foreach (var series in data.Series)
            {
                foreach (var indicator in indicators)
                {
                    switch (indicator.ToUpper())
                    {
                        case "SMA":
                            var smaPeriods = new List<int> { 20, 50, 200 };
                            var smaIndicators = CalculateMovingAverages(series, smaPeriods);
                            data.TechnicalIndicators.AddRange(smaIndicators);
                            break;
                        case "RSI":
                            var rsiIndicator = CalculateRSI(series);
                            data.TechnicalIndicators.Add(rsiIndicator);
                            break;
                        case "VOLATILITY":
                            var volatilityIndicator = CalculateVolatility(series);
                            data.TechnicalIndicators.Add(volatilityIndicator);
                            break;
                    }
                }
            }

            return data;
        }

        /// <summary>
        /// Handles missing dates in the data
        /// </summary>
        public ChartData HandleMissingDates(ChartData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            // Create a complete set of dates between start and end
            var allDates = new List<DateTime>();
            for (var date = data.StartDate; date <= data.EndDate; date = date.AddDays(1))
            {
                // Skip weekends for stock market data
                if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                {
                    allDates.Add(date);
                }
            }

            // Create a new set of labels
            var newLabels = allDates.Select(d => FormatDateLabel(d)).ToList();

            // For each series, fill in missing data points
            var newSeries = new List<ChartDataSeries>();
            foreach (var series in data.Series)
            {
                var newData = new List<decimal>();
                var currentDataIndex = 0;

                foreach (var date in allDates)
                {
                    var dateStr = FormatDateLabel(date);
                    var labelIndex = data.Labels.IndexOf(dateStr);

                    if (labelIndex >= 0 && labelIndex < series.Data.Count)
                    {
                        // We have data for this date
                        newData.Add(series.Data[labelIndex]);
                        currentDataIndex = labelIndex + 1;
                    }
                    else
                    {
                        // Missing data - interpolate or use last known value
                        if (newData.Count > 0)
                        {
                            newData.Add(newData.Last()); // Use last known value
                        }
                        else if (currentDataIndex < series.Data.Count)
                        {
                            newData.Add(series.Data[currentDataIndex]); // Use next available value
                        }
                        else
                        {
                            newData.Add(0); // No data available
                        }
                    }
                }

                var newSeries1 = new ChartDataSeries
                {
                    Name = series.Name,
                    Data = newData,
                    Color = series.Color,
                    DataType = series.DataType,
                    IsComparison = series.IsComparison
                };

                newSeries.Add(newSeries1);
            }

            return new ChartData
            {
                Title = data.Title,
                Labels = newLabels,
                Series = newSeries,
                StartDate = data.StartDate,
                EndDate = data.EndDate,
                Annotations = data.Annotations,
                TechnicalIndicators = data.TechnicalIndicators
            };
        }

        /// <summary>
        /// Aligns multiple data series for comparison
        /// </summary>
        public List<ChartDataSeries> AlignDataSeries(List<ChartDataSeries> series)
        {
            if (series == null || !series.Any())
            {
                throw new ArgumentException("Series cannot be null or empty", nameof(series));
            }

            // Find the shortest series length
            var minLength = series.Min(s => s.Data.Count);

            // Truncate all series to the same length
            var alignedSeries = new List<ChartDataSeries>();
            foreach (var s in series)
            {
                var alignedData = s.Data.Take(minLength).ToList();
                
                alignedSeries.Add(new ChartDataSeries
                {
                    Name = s.Name,
                    Data = alignedData,
                    Color = s.Color,
                    DataType = s.DataType,
                    IsComparison = s.IsComparison
                });
            }

            return alignedSeries;
        }

        /// <summary>
        /// Generates comparison data with previous administration
        /// </summary>
        public ChartData GenerateComparisonData(ChartData currentData, Dictionary<string, List<StockIndexData>> previousData)
        {
            if (currentData == null)
            {
                throw new ArgumentNullException(nameof(currentData));
            }

            if (previousData == null || !previousData.Any())
            {
                return currentData; // No previous data to compare with
            }

            // Create a copy of the current data
            var result = new ChartData
            {
                Title = currentData.Title + " (with comparison)",
                Labels = new List<string>(currentData.Labels),
                Series = new List<ChartDataSeries>(currentData.Series),
                StartDate = currentData.StartDate,
                EndDate = currentData.EndDate,
                Annotations = currentData.Annotations != null ? new List<ChartAnnotation>(currentData.Annotations) : null,
                TechnicalIndicators = currentData.TechnicalIndicators != null ? new List<TechnicalIndicator>(currentData.TechnicalIndicators) : null
            };

            // Calculate the duration of the current data
            var currentDuration = (currentData.EndDate - currentData.StartDate).TotalDays;

            // For each index in the current data, add a comparison series from previous data
            foreach (var series in currentData.Series)
            {
                if (previousData.TryGetValue(series.Name, out var prevIndexData))
                {
                    // Sort previous data by date
                    var sortedPrevData = prevIndexData.OrderBy(d => d.Date).ToList();
                    
                    if (sortedPrevData.Count > 0)
                    {
                        // Use the same duration for previous data, starting from its first available date
                        var prevStartDate = sortedPrevData.First().Date;
                        var prevEndDate = prevStartDate.AddDays(currentDuration);
                        
                        // Filter previous data to the same duration
                        var filteredPrevData = sortedPrevData
                            .Where(d => d.Date >= prevStartDate && d.Date <= prevEndDate)
                            .ToList();
                        
                        if (filteredPrevData.Count > 0)
                        {
                            // Create a comparison series
                            var comparisonSeries = new ChartDataSeries
                            {
                                Name = series.Name + " (Previous)",
                                Data = filteredPrevData.Select(d => d.CloseValue).ToList(),
                                Color = GetComparisonColor(series.Color),
                                DataType = series.DataType,
                                IsComparison = true
                            };
                            
                            result.Series.Add(comparisonSeries);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Calculates moving averages for the data
        /// </summary>
        public List<TechnicalIndicator> CalculateMovingAverages(ChartDataSeries data, List<int> periods)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (periods == null || !periods.Any())
            {
                throw new ArgumentException("Periods cannot be null or empty", nameof(periods));
            }

            var indicators = new List<TechnicalIndicator>();

            foreach (var period in periods)
            {
                if (period <= 0)
                {
                    throw new ArgumentException($"Period must be greater than zero: {period}");
                }

                var maData = new List<decimal?>();
                
                // Calculate SMA for each point
                for (int i = 0; i < data.Data.Count; i++)
                {
                    if (i < period - 1)
                    {
                        // Not enough data points yet
                        maData.Add(null);
                    }
                    else
                    {
                        // Calculate average of last 'period' points
                        var sum = 0m;
                        for (int j = 0; j < period; j++)
                        {
                            sum += data.Data[i - j];
                        }
                        maData.Add(sum / period);
                    }
                }

                var indicator = new TechnicalIndicator
                {
                    Name = $"SMA{period}",
                    Parameters = new Dictionary<string, object> { { "Period", period } },
                    Data = maData,
                    Color = GetTechnicalIndicatorColor($"SMA{period}")
                };

                indicators.Add(indicator);
            }

            return indicators;
        }

        /// <summary>
        /// Identifies trends in the market data
        /// </summary>
        public List<string> IdentifyTrends(ChartDataSeries data)
        {
            if (data == null || data.Data.Count < 2)
            {
                throw new ArgumentException("Data must contain at least two points", nameof(data));
            }

            var trends = new List<string>();
            
            // Calculate overall trend
            var firstValue = data.Data.First();
            var lastValue = data.Data.Last();
            var overallChange = (lastValue - firstValue) / firstValue * 100;
            
            if (overallChange > 5)
            {
                trends.Add("Strong Uptrend");
            }
            else if (overallChange > 0)
            {
                trends.Add("Mild Uptrend");
            }
            else if (overallChange > -5)
            {
                trends.Add("Mild Downtrend");
            }
            else
            {
                trends.Add("Strong Downtrend");
            }
            
            // Check for recent reversal (last 10% of data points)
            var recentDataCount = Math.Max(data.Data.Count / 10, 2);
            var recentData = data.Data.Skip(data.Data.Count - recentDataCount).ToList();
            
            var recentFirst = recentData.First();
            var recentLast = recentData.Last();
            var recentChange = (recentLast - recentFirst) / recentFirst * 100;
            
            // If recent trend is opposite of overall trend
            if ((overallChange > 0 && recentChange < -2) || (overallChange < 0 && recentChange > 2))
            {
                trends.Add("Recent Reversal");
            }
            
            // Check for volatility
            var changes = new List<decimal>();
            for (int i = 1; i < data.Data.Count; i++)
            {
                var percentChange = Math.Abs((data.Data[i] - data.Data[i - 1]) / data.Data[i - 1] * 100);
                changes.Add(percentChange);
            }
            
            var avgChange = changes.Average();
            if (avgChange > 1.5m)
            {
                trends.Add("High Volatility");
            }
            else if (avgChange < 0.5m)
            {
                trends.Add("Low Volatility");
            }
            
            return trends;
        }

        /// <summary>
        /// Calculates Relative Strength Index (RSI)
        /// </summary>
        public TechnicalIndicator CalculateRSI(ChartDataSeries data, int period = 14)
        {
            if (data == null || data.Data.Count <= period)
            {
                throw new ArgumentException($"Data must contain more than {period} points", nameof(data));
            }

            var rsiData = new List<decimal?>();
            
            // Add null values for the first 'period' data points
            for (int i = 0; i < period; i++)
            {
                rsiData.Add(null);
            }
            
            // Calculate price changes
            var changes = new List<decimal>();
            for (int i = 1; i < data.Data.Count; i++)
            {
                changes.Add(data.Data[i] - data.Data[i - 1]);
            }
            
            // Calculate RSI for each point after the initial period
            for (int i = period; i < data.Data.Count; i++)
            {
                var periodChanges = changes.Skip(i - period).Take(period).ToList();
                var gains = periodChanges.Where(c => c > 0).Sum();
                var losses = Math.Abs(periodChanges.Where(c => c < 0).Sum());
                
                if (losses == 0)
                {
                    rsiData.Add(100); // No losses means RSI = 100
                }
                else
                {
                    var rs = gains / losses;
                    var rsi = 100 - (100 / (1 + rs));
                    rsiData.Add(rsi);
                }
            }
            
            return new TechnicalIndicator
            {
                Name = "RSI",
                Parameters = new Dictionary<string, object> { { "Period", period } },
                Data = rsiData,
                Color = GetTechnicalIndicatorColor("RSI")
            };
        }

        /// <summary>
        /// Calculates volatility for the data
        /// </summary>
        public TechnicalIndicator CalculateVolatility(ChartDataSeries data, int period = 20)
        {
            if (data == null || data.Data.Count <= period)
            {
                throw new ArgumentException($"Data must contain more than {period} points", nameof(data));
            }

            var volatilityData = new List<decimal?>();
            
            // Add null values for the first 'period' data points
            for (int i = 0; i < period; i++)
            {
                volatilityData.Add(null);
            }
            
            // Calculate daily returns
            var returns = new List<decimal>();
            for (int i = 1; i < data.Data.Count; i++)
            {
                returns.Add((data.Data[i] / data.Data[i - 1]) - 1);
            }
            
            // Calculate volatility (standard deviation of returns) for each window
            for (int i = period; i < data.Data.Count; i++)
            {
                var windowReturns = returns.Skip(i - period).Take(period).ToList();
                var mean = windowReturns.Average();
                var sumSquaredDiff = windowReturns.Sum(r => Math.Pow((double)(r - mean), 2));
                var stdDev = (decimal)Math.Sqrt(sumSquaredDiff / period);
                
                // Annualized volatility (assuming daily data)
                var annualizedVolatility = stdDev * (decimal)Math.Sqrt(252);
                volatilityData.Add(annualizedVolatility * 100); // Convert to percentage
            }
            
            return new TechnicalIndicator
            {
                Name = "Volatility",
                Parameters = new Dictionary<string, object> { { "Period", period } },
                Data = volatilityData,
                Color = GetTechnicalIndicatorColor("Volatility")
            };
        }

        /// <summary>
        /// Normalizes data for comparing different scales
        /// </summary>
        public List<ChartDataSeries> NormalizeData(List<ChartDataSeries> series)
        {
            if (series == null || !series.Any())
            {
                throw new ArgumentException("Series cannot be null or empty", nameof(series));
            }

            var normalizedSeries = new List<ChartDataSeries>();
            
            foreach (var s in series)
            {
                if (s.Data.Count == 0)
                {
                    // Skip empty series
                    continue;
                }
                
                // Use the first value as the base (100%)
                var baseValue = s.Data.First();
                if (baseValue == 0)
                {
                    // Find first non-zero value
                    for (int i = 1; i < s.Data.Count; i++)
                    {
                        if (s.Data[i] != 0)
                        {
                            baseValue = s.Data[i];
                            break;
                        }
                    }
                    
                    if (baseValue == 0)
                    {
                        // All values are zero, skip this series
                        continue;
                    }
                }
                
                // Calculate normalized values (percentage of base value)
                var normalizedData = s.Data.Select(v => (v / baseValue) * 100).ToList();
                
                normalizedSeries.Add(new ChartDataSeries
                {
                    Name = s.Name,
                    Data = normalizedData,
                    Color = s.Color,
                    DataType = "Normalized (%)",
                    IsComparison = s.IsComparison
                });
            }
            
            return normalizedSeries;
        }

        /// <summary>
        /// Optimizes the number of data points for display
        /// </summary>
        public ChartData OptimizeDataPoints(ChartData data, int maxPoints)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (maxPoints <= 0)
            {
                throw new ArgumentException("Max points must be greater than zero", nameof(maxPoints));
            }

            // If we already have fewer points than the maximum, return the original data
            if (data.Labels.Count <= maxPoints)
            {
                return data;
            }

            // Calculate the sampling interval
            var interval = (int)Math.Ceiling((double)data.Labels.Count / maxPoints);
            
            // Create optimized data
            var optimizedLabels = new List<string>();
            var optimizedSeries = new List<ChartDataSeries>();
            
            // Sample the labels
            for (int i = 0; i < data.Labels.Count; i += interval)
            {
                optimizedLabels.Add(data.Labels[i]);
            }
            
            // Sample each series
            foreach (var series in data.Series)
            {
                var optimizedData = new List<decimal>();
                
                for (int i = 0; i < series.Data.Count; i += interval)
                {
                    optimizedData.Add(series.Data[i]);
                }
                
                optimizedSeries.Add(new ChartDataSeries
                {
                    Name = series.Name,
                    Data = optimizedData,
                    Color = series.Color,
                    DataType = series.DataType,
                    IsComparison = series.IsComparison
                });
            }
            
            // Sample technical indicators if present
            var optimizedIndicators = new List<TechnicalIndicator>();
            if (data.TechnicalIndicators != null)
            {
                foreach (var indicator in data.TechnicalIndicators)
                {
                    var optimizedData = new List<decimal?>();
                    
                    for (int i = 0; i < indicator.Data.Count; i += interval)
                    {
                        optimizedData.Add(indicator.Data[i]);
                    }
                    
                    optimizedIndicators.Add(new TechnicalIndicator
                    {
                        Name = indicator.Name,
                        Parameters = new Dictionary<string, object>(indicator.Parameters),
                        Data = optimizedData,
                        Color = indicator.Color
                    });
                }
            }
            
            return new ChartData
            {
                Title = data.Title,
                Labels = optimizedLabels,
                Series = optimizedSeries,
                StartDate = data.StartDate,
                EndDate = data.EndDate,
                Annotations = data.Annotations, // Keep original annotations
                TechnicalIndicators = optimizedIndicators.Count > 0 ? optimizedIndicators : null
            };
        }

        /// <summary>
        /// Generates annotations for significant events
        /// </summary>
        public ChartData GenerateAnnotations(ChartData data, Dictionary<DateTime, string> events)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (events == null || !events.Any())
            {
                return data; // No events to annotate
            }

            // Create a copy of the data
            var result = new ChartData
            {
                Title = data.Title,
                Labels = new List<string>(data.Labels),
                Series = new List<ChartDataSeries>(data.Series),
                StartDate = data.StartDate,
                EndDate = data.EndDate,
                TechnicalIndicators = data.TechnicalIndicators != null ? new List<TechnicalIndicator>(data.TechnicalIndicators) : null
            };
            
            // Initialize annotations list
            result.Annotations = data.Annotations != null ? new List<ChartAnnotation>(data.Annotations) : new List<ChartAnnotation>();
            
            // Add annotations for each event within the date range
            foreach (var evt in events)
            {
                if (evt.Key >= data.StartDate && evt.Key <= data.EndDate)
                {
                    result.Annotations.Add(new ChartAnnotation
                    {
                        Date = evt.Key,
                        Text = evt.Value,
                        Type = "Event"
                    });
                }
            }
            
            return result;
        }

        #region Helper Methods

        private decimal CalculatePercentageChange(decimal currentValue, decimal baseValue)
        {
            if (baseValue == 0)
            {
                return 0; // Avoid division by zero
            }
            
            return ((currentValue - baseValue) / baseValue) * 100;
        }

        private string FormatDateLabel(DateTime date)
        {
            // For daily data
            return date.ToString("MM/dd/yyyy");
        }

        private string GetColorForIndex(string indexName)
        {
            // Assign consistent colors to common indices
            return indexName.ToLower() switch
            {
                "s&p 500" => "#1f77b4", // Blue
                "dow jones" => "#ff7f0e", // Orange
                "nasdaq" => "#2ca02c", // Green
                "russell 2000" => "#d62728", // Red
                "wilshire 5000" => "#9467bd", // Purple
                _ => "#8c564b" // Brown (default)
            };
        }

        private string GetComparisonColor(string originalColor)
        {
            // Create a lighter or dashed version of the original color
            // For simplicity, we'll just return a different color
            return originalColor switch
            {
                "#1f77b4" => "#aec7e8", // Light blue
                "#ff7f0e" => "#ffbb78", // Light orange
                "#2ca02c" => "#98df8a", // Light green
                "#d62728" => "#ff9896", // Light red
                "#9467bd" => "#c5b0d5", // Light purple
                _ => "#c49c94" // Light brown (default)
            };
        }

        private string GetTechnicalIndicatorColor(string indicatorName)
        {
            // Assign consistent colors to technical indicators
            return indicatorName switch
            {
                "SMA20" => "#7f7f7f", // Gray
                "SMA50" => "#bcbd22", // Olive
                "SMA200" => "#17becf", // Cyan
                "RSI" => "#e377c2", // Pink
                "Volatility" => "#ffbb78", // Light orange
                _ => "#7f7f7f" // Gray (default)
            };
        }

        #endregion
    }
}
