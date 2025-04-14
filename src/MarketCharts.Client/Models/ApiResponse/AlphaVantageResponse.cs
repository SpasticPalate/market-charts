using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MarketCharts.Client.Models.ApiResponse
{
    /// <summary>
    /// Represents the response structure from Alpha Vantage API
    /// </summary>
    public class AlphaVantageResponse
    {
        [JsonPropertyName("Meta Data")]
        public AlphaVantageMetadata? Metadata { get; set; }

        [JsonPropertyName("Time Series (Daily)")]
        public Dictionary<string, AlphaVantageDailyData>? TimeSeriesDaily { get; set; }

        /// <summary>
        /// Error message if the API returns an error
        /// </summary>
        [JsonPropertyName("Error Message")]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Information message from the API
        /// </summary>
        [JsonPropertyName("Information")]
        public string? Information { get; set; }

        /// <summary>
        /// Note message from the API (often used for rate limiting information)
        /// </summary>
        [JsonPropertyName("Note")]
        public string? Note { get; set; }
    }

    public class AlphaVantageMetadata
    {
        [JsonPropertyName("1. Information")]
        public string? Information { get; set; }

        [JsonPropertyName("2. Symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("3. Last Refreshed")]
        public string? LastRefreshed { get; set; }

        [JsonPropertyName("4. Output Size")]
        public string? OutputSize { get; set; }

        [JsonPropertyName("5. Time Zone")]
        public string? TimeZone { get; set; }
    }

    public class AlphaVantageDailyData
    {
        [JsonPropertyName("1. open")]
        public string? Open { get; set; }

        [JsonPropertyName("2. high")]
        public string? High { get; set; }

        [JsonPropertyName("3. low")]
        public string? Low { get; set; }

        [JsonPropertyName("4. close")]
        public string? Close { get; set; }

        [JsonPropertyName("5. volume")]
        public string? Volume { get; set; }
    }
}
