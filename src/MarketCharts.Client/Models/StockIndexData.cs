using System;

namespace MarketCharts.Client.Models
{
    /// <summary>
    /// Represents stock market index data for a specific date
    /// </summary>
    public class StockIndexData
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string IndexName { get; set; } = string.Empty;
        public decimal CloseValue { get; set; }
        public decimal OpenValue { get; set; }
        public decimal HighValue { get; set; }
        public decimal LowValue { get; set; }
        public long Volume { get; set; }
        public DateTime FetchedAt { get; set; }
    }
}
