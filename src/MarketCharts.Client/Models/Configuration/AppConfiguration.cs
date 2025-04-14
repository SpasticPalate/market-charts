using System;

namespace MarketCharts.Client.Models.Configuration
{
    /// <summary>
    /// Main application configuration
    /// </summary>
    public class AppConfiguration
    {
        /// <summary>
        /// API configuration
        /// </summary>
        public ApiConfiguration ApiConfig { get; set; } = new ApiConfiguration();

        /// <summary>
        /// Database configuration
        /// </summary>
        public DatabaseConfiguration DatabaseConfig { get; set; } = new DatabaseConfiguration();

        /// <summary>
        /// Chart configuration
        /// </summary>
        public ChartConfiguration ChartConfig { get; set; } = new ChartConfiguration();

        /// <summary>
        /// Date of Trump's second term inauguration (January 20, 2025)
        /// </summary>
        public DateTime InaugurationDate { get; set; } = new DateTime(2025, 1, 20);

        /// <summary>
        /// Date of tariff announcement (early April 2025)
        /// </summary>
        public DateTime TariffAnnouncementDate { get; set; } = new DateTime(2025, 4, 1);

        /// <summary>
        /// Whether to enable daily updates
        /// </summary>
        public bool EnableDailyUpdates { get; set; } = true;

        /// <summary>
        /// Time of day to perform daily updates (in 24-hour format)
        /// </summary>
        public TimeSpan DailyUpdateTime { get; set; } = new TimeSpan(18, 0, 0); // 6:00 PM

        /// <summary>
        /// Whether to enable comparison with previous administrations
        /// </summary>
        public bool EnableComparison { get; set; } = true;

        /// <summary>
        /// Whether to enable technical indicators
        /// </summary>
        public bool EnableTechnicalIndicators { get; set; } = true;

        /// <summary>
        /// Application version
        /// </summary>
        public string Version { get; set; } = "1.0.0";
    }

    /// <summary>
    /// Chart configuration
    /// </summary>
    public class ChartConfiguration
    {
        /// <summary>
        /// Color for S&P 500 index
        /// </summary>
        public string SP500Color { get; set; } = "#10B981"; // Emerald

        /// <summary>
        /// Color for Dow Jones index
        /// </summary>
        public string DowJonesColor { get; set; } = "#3B82F6"; // Blue

        /// <summary>
        /// Color for NASDAQ index
        /// </summary>
        public string NasdaqColor { get; set; } = "#8B5CF6"; // Purple

        /// <summary>
        /// Color for UI accents
        /// </summary>
        public string AccentColor { get; set; } = "#F59E0B"; // Amber

        /// <summary>
        /// Whether to use dark mode
        /// </summary>
        public bool UseDarkMode { get; set; } = false;

        /// <summary>
        /// Whether to show annotations for significant events
        /// </summary>
        public bool ShowAnnotations { get; set; } = true;

        /// <summary>
        /// Whether to optimize data points for large datasets
        /// </summary>
        public bool OptimizeDataPoints { get; set; } = true;

        /// <summary>
        /// Maximum number of data points to display before optimization
        /// </summary>
        public int MaxDataPointsBeforeOptimization { get; set; } = 100;

        /// <summary>
        /// Whether to enable chart animations
        /// </summary>
        public bool EnableAnimations { get; set; } = true;

        /// <summary>
        /// Whether to enable chart export
        /// </summary>
        public bool EnableExport { get; set; } = true;
    }
}
