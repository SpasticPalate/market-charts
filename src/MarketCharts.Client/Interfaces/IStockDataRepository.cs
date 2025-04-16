using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MarketCharts.Client.Models;

namespace MarketCharts.Client.Interfaces
{
    /// <summary>
    /// Interface for stock data repository operations
    /// </summary>
    public interface IStockDataRepository : IDisposable
    {
        /// <summary>
        /// Saves a stock index data point to the repository
        /// </summary>
        /// <param name="data">The stock index data to save</param>
        /// <returns>The ID of the saved data</returns>
        Task<int> SaveStockDataAsync(StockIndexData data);

        /// <summary>
        /// Saves multiple stock index data points to the repository
        /// </summary>
        /// <param name="dataList">The list of stock index data to save</param>
        /// <returns>The number of records saved</returns>
        Task<int> SaveStockDataBatchAsync(List<StockIndexData> dataList);

        /// <summary>
        /// Gets a stock index data point by ID
        /// </summary>
        /// <param name="id">The ID of the data to retrieve</param>
        /// <returns>The stock index data, or null if not found</returns>
        Task<StockIndexData?> GetStockDataByIdAsync(int id);

        /// <summary>
        /// Gets stock index data for a specific date and index
        /// </summary>
        /// <param name="date">The date of the data</param>
        /// <param name="indexName">The name of the index</param>
        /// <returns>The stock index data, or null if not found</returns>
        Task<StockIndexData?> GetStockDataByDateAndIndexAsync(DateTime date, string indexName);

        /// <summary>
        /// Gets stock index data for a date range and index
        /// </summary>
        /// <param name="startDate">The start date of the range</param>
        /// <param name="endDate">The end date of the range</param>
        /// <param name="indexName">The name of the index (optional)</param>
        /// <returns>A list of stock index data points</returns>
        Task<List<StockIndexData>> GetStockDataByDateRangeAsync(DateTime startDate, DateTime endDate, string? indexName = null);

        /// <summary>
        /// Updates an existing stock index data point
        /// </summary>
        /// <param name="data">The stock index data to update</param>
        /// <returns>True if the update was successful, false otherwise</returns>
        Task<bool> UpdateStockDataAsync(StockIndexData data);

        /// <summary>
        /// Gets the latest stock index data for a specific index
        /// </summary>
        /// <param name="indexName">The name of the index</param>
        /// <returns>The latest stock index data, or null if not found</returns>
        Task<StockIndexData?> GetLatestStockDataAsync(string indexName);

        /// <summary>
        /// Checks if the database needs to be compacted
        /// </summary>
        /// <returns>True if compaction is needed, false otherwise</returns>
        Task<bool> NeedsCompactionAsync();

        /// <summary>
        /// Compacts the database
        /// </summary>
        /// <returns>True if compaction was successful, false otherwise</returns>
        Task<bool> CompactDatabaseAsync();

        /// <summary>
        /// Creates a backup of the database
        /// </summary>
        /// <param name="backupPath">The path to save the backup to</param>
        /// <returns>True if the backup was successful, false otherwise</returns>
        Task<bool> BackupDatabaseAsync(string backupPath);

        /// <summary>
        /// Verifies the integrity of the database
        /// </summary>
        /// <returns>True if the database is intact, false otherwise</returns>
        Task<bool> VerifyDatabaseIntegrityAsync();

        /// <summary>
        /// Initializes the database schema if it doesn't exist
        /// </summary>
        /// <returns>True if initialization was successful, false otherwise</returns>
        Task<bool> InitializeSchemaAsync();

        /// <summary>
        /// Deletes a stock index data point from the repository
        /// </summary>
        /// <param name="id">The ID of the data to delete</param>
        /// <returns>True if the deletion was successful, false otherwise</returns>
        Task<bool> DeleteStockDataAsync(int id);
    }
}
