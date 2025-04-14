using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MarketCharts.Client.Models;
using MarketCharts.Client.Models.Chart;

namespace MarketCharts.Client.Interfaces
{
    /// <summary>
    /// Interface for processing stock data for chart display
    /// </summary>
    public interface IChartDataProcessor
    {
        /// <summary>
        /// Formats raw stock data for chart display
        /// </summary>
        /// <param name="data">Dictionary of index names to their stock data</param>
        /// <param name="title">Title for the chart</param>
        /// <param name="startDate">Start date for the chart data</param>
        /// <param name="endDate">End date for the chart data</param>
        /// <returns>Formatted chart data</returns>
        ChartData FormatDataForChart(Dictionary<string, List<StockIndexData>> data, string title, DateTime startDate, DateTime endDate);

        /// <summary>
        /// Calculates percentage changes from a base date
        /// </summary>
        /// <param name="data">Dictionary of index names to their stock data</param>
        /// <param name="baseDate">The base date to calculate changes from</param>
        /// <returns>Dictionary of index names to their percentage change data</returns>
        Dictionary<string, List<decimal>> CalculatePercentageChanges(Dictionary<string, List<StockIndexData>> data, DateTime baseDate);

        /// <summary>
        /// Generates appropriate labels for chart display
        /// </summary>
        /// <param name="startDate">Start date for the labels</param>
        /// <param name="endDate">End date for the labels</param>
        /// <param name="dataPoints">Number of data points</param>
        /// <returns>List of formatted date labels</returns>
        List<string> GenerateChartLabels(DateTime startDate, DateTime endDate, int dataPoints);

        /// <summary>
        /// Applies technical indicators to the data
        /// </summary>
        /// <param name="data">The stock data to process</param>
        /// <param name="indicators">List of indicators to apply</param>
        /// <returns>Chart data with technical indicators applied</returns>
        ChartData ApplyTechnicalIndicators(ChartData data, List<string> indicators);

        /// <summary>
        /// Handles missing dates in the data
        /// </summary>
        /// <param name="data">The data with potential gaps</param>
        /// <returns>Data with missing dates handled</returns>
        ChartData HandleMissingDates(ChartData data);

        /// <summary>
        /// Aligns multiple data series for comparison
        /// </summary>
        /// <param name="series">List of data series to align</param>
        /// <returns>Aligned data series</returns>
        List<ChartDataSeries> AlignDataSeries(List<ChartDataSeries> series);

        /// <summary>
        /// Generates comparison data with previous administration
        /// </summary>
        /// <param name="currentData">Current administration data</param>
        /// <param name="previousData">Previous administration data</param>
        /// <returns>Chart data with comparison series added</returns>
        ChartData GenerateComparisonData(ChartData currentData, Dictionary<string, List<StockIndexData>> previousData);

        /// <summary>
        /// Calculates moving averages for the data
        /// </summary>
        /// <param name="data">The data to calculate moving averages for</param>
        /// <param name="periods">List of periods to calculate</param>
        /// <returns>List of moving average technical indicators</returns>
        List<TechnicalIndicator> CalculateMovingAverages(ChartDataSeries data, List<int> periods);

        /// <summary>
        /// Identifies trends in the market data
        /// </summary>
        /// <param name="data">The data to analyze</param>
        /// <returns>List of identified trends</returns>
        List<string> IdentifyTrends(ChartDataSeries data);

        /// <summary>
        /// Calculates Relative Strength Index (RSI)
        /// </summary>
        /// <param name="data">The data to calculate RSI for</param>
        /// <param name="period">The period to use for calculation</param>
        /// <returns>RSI technical indicator</returns>
        TechnicalIndicator CalculateRSI(ChartDataSeries data, int period = 14);

        /// <summary>
        /// Calculates volatility for the data
        /// </summary>
        /// <param name="data">The data to calculate volatility for</param>
        /// <param name="period">The period to use for calculation</param>
        /// <returns>Volatility technical indicator</returns>
        TechnicalIndicator CalculateVolatility(ChartDataSeries data, int period = 20);

        /// <summary>
        /// Normalizes data for comparing different scales
        /// </summary>
        /// <param name="series">List of data series to normalize</param>
        /// <returns>Normalized data series</returns>
        List<ChartDataSeries> NormalizeData(List<ChartDataSeries> series);

        /// <summary>
        /// Optimizes the number of data points for display
        /// </summary>
        /// <param name="data">The data to optimize</param>
        /// <param name="maxPoints">Maximum number of points to display</param>
        /// <returns>Optimized chart data</returns>
        ChartData OptimizeDataPoints(ChartData data, int maxPoints);

        /// <summary>
        /// Generates annotations for significant events
        /// </summary>
        /// <param name="data">The chart data</param>
        /// <param name="events">Dictionary of dates to event descriptions</param>
        /// <returns>Chart data with annotations added</returns>
        ChartData GenerateAnnotations(ChartData data, Dictionary<DateTime, string> events);
    }
}
