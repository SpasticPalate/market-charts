using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MarketCharts.Client.Models;
using MarketCharts.Client.Models.Chart;

namespace MarketCharts.Client.Interfaces
{
    /// <summary>
    /// Interface for stock data service operations
    /// </summary>
    public interface IStockDataService
    {
        /// <summary>
        /// Gets historical stock index data from inauguration date to present
        /// </summary>
        /// <returns>A list of stock index data points for all indices</returns>
        Task<Dictionary<string, List<StockIndexData>>> GetInaugurationToPresent();

        /// <summary>
        /// Gets historical stock index data from tariff announcement date to present
        /// </summary>
        /// <returns>A list of stock index data points for all indices</returns>
        Task<Dictionary<string, List<StockIndexData>>> GetTariffAnnouncementToPresent();

        /// <summary>
        /// Gets comparison data from previous administration
        /// </summary>
        /// <returns>A list of stock index data points for comparison</returns>
        Task<Dictionary<string, List<StockIndexData>>> GetPreviousAdministrationData();

        /// <summary>
        /// Gets the latest stock index data for all indices
        /// </summary>
        /// <returns>A dictionary of index names to their latest data</returns>
        Task<Dictionary<string, StockIndexData>> GetLatestDataForAllIndices();

        /// <summary>
        /// Checks if data needs to be updated and fetches it if necessary
        /// </summary>
        /// <returns>True if data was updated, false otherwise</returns>
        Task<bool> CheckAndUpdateDataAsync();

        /// <summary>
        /// Initializes the service and loads initial data if needed
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        Task InitializeAsync();

        /// <summary>
        /// Schedules daily updates of stock data
        /// </summary>
        /// <param name="updateTime">The time of day to perform updates</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task ScheduleDailyUpdatesAsync(TimeSpan updateTime);

        /// <summary>
        /// Gets the time of the last data update
        /// </summary>
        /// <returns>The time of the last update, or null if no updates have occurred</returns>
        DateTime? GetLastUpdateTime();

        /// <summary>
        /// Fills any gaps in the data by interpolating or using previous values
        /// </summary>
        /// <param name="data">The data to fill gaps in</param>
        /// <returns>The data with gaps filled</returns>
        Task<List<StockIndexData>> FillDataGapsAsync(List<StockIndexData> data);

        /// <summary>
        /// Verifies the consistency of data from multiple sources
        /// </summary>
        /// <param name="data">The data to verify</param>
        /// <returns>True if the data is consistent, false otherwise</returns>
        Task<bool> VerifyDataConsistencyAsync(List<StockIndexData> data);

        /// <summary>
        /// Handles weekend and holiday market closures
        /// </summary>
        /// <param name="date">The date to check</param>
        /// <returns>True if markets are closed on the specified date, false otherwise</returns>
        bool AreMarketsClosed(DateTime date);
    }
}
