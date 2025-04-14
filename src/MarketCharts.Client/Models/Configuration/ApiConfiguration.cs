using System;

namespace MarketCharts.Client.Models.Configuration
{
    /// <summary>
    /// Configuration for API services
    /// </summary>
    public class ApiConfiguration
    {
        /// <summary>
        /// Configuration for the primary API (Alpha Vantage)
        /// </summary>
        public AlphaVantageConfig PrimaryApi { get; set; } = new AlphaVantageConfig();

        /// <summary>
        /// Configuration for the backup API (StockData.org)
        /// </summary>
        public StockDataOrgConfig BackupApi { get; set; } = new StockDataOrgConfig();

        /// <summary>
        /// Time in minutes before retrying the primary API after a failure
        /// </summary>
        public int RetryPrimaryApiAfterMinutes { get; set; } = 60;

        /// <summary>
        /// Maximum number of retry attempts for API calls
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Delay in milliseconds between retry attempts
        /// </summary>
        public int RetryDelayMilliseconds { get; set; } = 1000;
    }

    /// <summary>
    /// Configuration for Alpha Vantage API
    /// </summary>
    public class AlphaVantageConfig
    {
        /// <summary>
        /// Base URL for the Alpha Vantage API
        /// </summary>
        public string BaseUrl { get; set; } = "https://www.alphavantage.co/query";

        /// <summary>
        /// API key for Alpha Vantage
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Maximum number of API calls allowed per day
        /// </summary>
        public int DailyLimit { get; set; } = 25;

        /// <summary>
        /// Symbol for S&P 500 index
        /// </summary>
        public string SP500Symbol { get; set; } = "^GSPC";

        /// <summary>
        /// Symbol for Dow Jones index
        /// </summary>
        public string DowJonesSymbol { get; set; } = "^DJI";

        /// <summary>
        /// Symbol for NASDAQ index
        /// </summary>
        public string NasdaqSymbol { get; set; } = "^IXIC";
    }

    /// <summary>
    /// Configuration for StockData.org API
    /// </summary>
    public class StockDataOrgConfig
    {
        /// <summary>
        /// Base URL for the StockData.org API
        /// </summary>
        public string BaseUrl { get; set; } = "https://api.stockdata.org/v1/data/quote";

        /// <summary>
        /// API token for StockData.org
        /// </summary>
        public string ApiToken { get; set; } = string.Empty;

        /// <summary>
        /// Maximum number of API calls allowed per day
        /// </summary>
        public int DailyLimit { get; set; } = 100;

        /// <summary>
        /// Symbol for S&P 500 index
        /// </summary>
        public string SP500Symbol { get; set; } = "^GSPC";

        /// <summary>
        /// Symbol for Dow Jones index
        /// </summary>
        public string DowJonesSymbol { get; set; } = "^DJI";

        /// <summary>
        /// Symbol for NASDAQ index
        /// </summary>
        public string NasdaqSymbol { get; set; } = "^IXIC";
    }
}
