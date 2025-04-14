using System;
using System.Collections.Generic;

namespace MarketCharts.Client.Models.Chart
{
    /// <summary>
    /// Represents data formatted for chart display
    /// </summary>
    public class ChartData
    {
        /// <summary>
        /// Title of the chart
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Labels for the X-axis (typically dates)
        /// </summary>
        public List<string> Labels { get; set; } = new List<string>();

        /// <summary>
        /// Data series to be displayed on the chart
        /// </summary>
        public List<ChartDataSeries> Series { get; set; } = new List<ChartDataSeries>();

        /// <summary>
        /// Start date of the data range
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// End date of the data range
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Optional annotations for significant events
        /// </summary>
        public List<ChartAnnotation>? Annotations { get; set; }

        /// <summary>
        /// Technical indicators applied to the chart
        /// </summary>
        public List<TechnicalIndicator>? TechnicalIndicators { get; set; }
    }

    /// <summary>
    /// Represents a single data series on a chart
    /// </summary>
    public class ChartDataSeries
    {
        /// <summary>
        /// Name of the data series (e.g., "S&P 500")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Data points for the series
        /// </summary>
        public List<decimal> Data { get; set; } = new List<decimal>();

        /// <summary>
        /// Color to use for this series
        /// </summary>
        public string Color { get; set; } = string.Empty;

        /// <summary>
        /// Whether this is a comparison data series
        /// </summary>
        public bool IsComparison { get; set; } = false;

        /// <summary>
        /// Type of data (e.g., "Price", "Percentage Change")
        /// </summary>
        public string DataType { get; set; } = "Price";
    }

    /// <summary>
    /// Represents an annotation on a chart
    /// </summary>
    public class ChartAnnotation
    {
        /// <summary>
        /// Date position for the annotation
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Text of the annotation
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Type of the annotation (e.g., "Event", "Policy Change")
        /// </summary>
        public string Type { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a technical indicator applied to chart data
    /// </summary>
    public class TechnicalIndicator
    {
        /// <summary>
        /// Name of the indicator (e.g., "SMA", "RSI")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Parameters for the indicator (e.g., period)
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Data points for the indicator
        /// </summary>
        public List<decimal?> Data { get; set; } = new List<decimal?>();

        /// <summary>
        /// Color to use for this indicator
        /// </summary>
        public string Color { get; set; } = string.Empty;
    }
}
