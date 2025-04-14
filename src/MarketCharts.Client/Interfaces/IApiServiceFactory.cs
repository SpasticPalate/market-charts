using System;
using System.Threading.Tasks;

namespace MarketCharts.Client.Interfaces
{
    /// <summary>
    /// Factory interface for creating and managing API services
    /// </summary>
    public interface IApiServiceFactory
    {
        /// <summary>
        /// Gets the current API service to use
        /// </summary>
        /// <returns>The current API service</returns>
        Task<IStockApiService> GetApiServiceAsync();

        /// <summary>
        /// Notifies the factory that the primary API has failed
        /// </summary>
        /// <param name="exception">The exception that caused the failure</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task NotifyPrimaryApiFailureAsync(Exception exception);

        /// <summary>
        /// Attempts to reset to the primary API service
        /// </summary>
        /// <returns>True if the reset was successful, false otherwise</returns>
        Task<bool> TryResetToPrimaryApiAsync();

        /// <summary>
        /// Gets the primary API service
        /// </summary>
        /// <returns>The primary API service</returns>
        IStockApiService GetPrimaryApiService();

        /// <summary>
        /// Gets the backup API service
        /// </summary>
        /// <returns>The backup API service</returns>
        IStockApiService GetBackupApiService();

        /// <summary>
        /// Checks if all API services are unavailable
        /// </summary>
        /// <returns>True if all API services are unavailable, false otherwise</returns>
        Task<bool> AreAllApiServicesUnavailableAsync();

        /// <summary>
        /// Gets the time when the primary API service will be retried
        /// </summary>
        /// <returns>The time when the primary API service will be retried, or null if not applicable</returns>
        DateTime? GetPrimaryApiRetryTime();
    }
}
