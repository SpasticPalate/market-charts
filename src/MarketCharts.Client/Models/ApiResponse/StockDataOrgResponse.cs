using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MarketCharts.Client.Models.ApiResponse
{
    /// <summary>
    /// Represents the response structure from StockData.org API
    /// </summary>
    public class StockDataOrgResponse
    {
        [JsonPropertyName("meta")]
        public StockDataOrgMeta? Meta { get; set; }

        [JsonPropertyName("data")]
        public List<StockDataOrgQuote>? Data { get; set; }

        [JsonPropertyName("error")]
        public StockDataOrgError? Error { get; set; }
    }

    public class StockDataOrgMeta
    {
        [JsonPropertyName("requested")]
        public int Requested { get; set; }

        [JsonPropertyName("returned")]
        public int Returned { get; set; }

        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    public class StockDataOrgQuote
    {
        [JsonPropertyName("symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("exchange")]
        public string? Exchange { get; set; }

        [JsonPropertyName("open")]
        public decimal? Open { get; set; }

        [JsonPropertyName("high")]
        public decimal? High { get; set; }

        [JsonPropertyName("low")]
        public decimal? Low { get; set; }

        [JsonPropertyName("close")]
        public decimal? Close { get; set; }

        [JsonPropertyName("volume")]
        public long? Volume { get; set; }

        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("previous_close")]
        public decimal? PreviousClose { get; set; }

        [JsonPropertyName("change")]
        public decimal? Change { get; set; }

        [JsonPropertyName("change_percent")]
        public decimal? ChangePercent { get; set; }
    }

    public class StockDataOrgError
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
