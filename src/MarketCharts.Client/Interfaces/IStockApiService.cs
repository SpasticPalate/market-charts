using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MarketCharts.Client.Models;

namespace MarketCharts.Client.Interfaces
{
    /// <summary>
    /// Interface for stock market API services
    /// </summary>
    public interface IStockApiService
    {
        /// <summary>
        /// Gets historical stock index data for a specified date range
        /// </summary>
        /// <param name="indexSymbol">The symbol for the stock index</param>
        /// <param name="startDate">The start date for the data range</param>
        /// <param name="endDate">The end date for the data range</param>
        /// <returns>A list of stock index data points</returns>
        Task<List<StockIndexData>> GetHistoricalDataAsync(string indexSymbol, DateTime startDate, DateTime endDate);

        /// <summary>
        /// Gets the latest stock index data for a specified symbol
        /// </summary>
        /// <param name="indexSymbol">The symbol for the stock index</param>
        /// <returns>The latest stock index data point</returns>
        Task<StockIndexData> GetLatestDataAsync(string indexSymbol);

        /// <summary>
        /// Checks if the API is available
        /// </summary>
        /// <returns>True if the API is available, false otherwise</returns>
        Task<bool> IsAvailableAsync();

        /// <summary>
        /// Gets the name of the API service
        /// </summary>
        string ServiceName { get; }

        /// <summary>
        /// Gets the remaining API calls for the current period
        /// </summary>
        /// <returns>The number of remaining API calls</returns>
        Task<int> GetRemainingApiCallsAsync();

        /// <summary>
        /// Gets the API call limit for the current period
        /// </summary>
        int ApiCallLimit { get; }
    }
}
