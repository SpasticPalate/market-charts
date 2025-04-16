using System;
using System.Threading.Tasks;
using MarketCharts.Client.Interfaces;
using MarketCharts.Client.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace MarketCharts.Client.Services.ApiServices
{
    /// <summary>
    /// Factory for creating and managing API services with failover capabilities
    /// </summary>
    public class ApiServiceFactory : IApiServiceFactory
    {
        private readonly IStockApiService _primaryApiService;
        private readonly IStockApiService _backupApiService;
        private readonly ApiConfiguration _apiConfig;
        private readonly ILogger<ApiServiceFactory> _logger;
        
        private bool _isPrimaryApiAvailable = true;
        private DateTime? _primaryApiRetryTime = null;

        /// <summary>
        /// Initializes a new instance of the ApiServiceFactory class
        /// </summary>
        /// <param name="primaryApiService">The primary API service</param>
        /// <param name="backupApiService">The backup API service</param>
        /// <param name="apiConfig">The API configuration</param>
        /// <param name="logger">The logger</param>
        public ApiServiceFactory(
            IStockApiService primaryApiService,
            IStockApiService backupApiService,
            ApiConfiguration apiConfig,
            ILogger<ApiServiceFactory> logger)
        {
            _primaryApiService = primaryApiService ?? throw new ArgumentNullException(nameof(primaryApiService));
            _backupApiService = backupApiService ?? throw new ArgumentNullException(nameof(backupApiService));
            _apiConfig = apiConfig ?? throw new ArgumentNullException(nameof(apiConfig));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<IStockApiService> GetApiServiceAsync()
        {
            // If primary API is available, use it
            if (_isPrimaryApiAvailable)
            {
                return _primaryApiService;
            }

            // Check if it's time to retry the primary API
            if (_primaryApiRetryTime.HasValue && DateTime.Now >= _primaryApiRetryTime.Value)
            {
                var resetSuccessful = await TryResetToPrimaryApiAsync();
                if (resetSuccessful)
                {
                    _logger.LogInformation("Successfully reset to primary API service: {ServiceName}", _primaryApiService.ServiceName);
                    return _primaryApiService;
                }
            }

            // Check if backup API is available
            if (await _backupApiService.IsAvailableAsync())
            {
                return _backupApiService;
            }

            // If we get here, both APIs are unavailable
            _logger.LogError("All API services are unavailable");
            throw new InvalidOperationException("All API services are unavailable");
        }

        /// <inheritdoc/>
        public Task NotifyPrimaryApiFailureAsync(Exception exception)
        {
            _isPrimaryApiAvailable = false;
            _primaryApiRetryTime = DateTime.Now.AddMinutes(_apiConfig.RetryPrimaryApiAfterMinutes);
            
            _logger.LogWarning(exception, 
                "Primary API service {ServiceName} failed. Will retry at {RetryTime}. Switching to backup API service {BackupServiceName}",
                _primaryApiService.ServiceName,
                _primaryApiRetryTime,
                _backupApiService.ServiceName);
            
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task<bool> TryResetToPrimaryApiAsync()
        {
            var isAvailable = await _primaryApiService.IsAvailableAsync();
            if (isAvailable)
            {
                _isPrimaryApiAvailable = true;
                _primaryApiRetryTime = null;
                return true;
            }
            
            // If the primary API is still not available, update the retry time
            _primaryApiRetryTime = DateTime.Now.AddMinutes(_apiConfig.RetryPrimaryApiAfterMinutes);
            _logger.LogWarning(
                "Primary API service {ServiceName} is still unavailable. Will retry at {RetryTime}",
                _primaryApiService.ServiceName,
                _primaryApiRetryTime);
            
            return false;
        }

        /// <inheritdoc/>
        public IStockApiService GetPrimaryApiService()
        {
            return _primaryApiService;
        }

        /// <inheritdoc/>
        public IStockApiService GetBackupApiService()
        {
            return _backupApiService;
        }

        /// <inheritdoc/>
        public async Task<bool> AreAllApiServicesUnavailableAsync()
        {
            var isPrimaryAvailable = await _primaryApiService.IsAvailableAsync();
            var isBackupAvailable = await _backupApiService.IsAvailableAsync();
            
            return !isPrimaryAvailable && !isBackupAvailable;
        }

        /// <inheritdoc/>
        public DateTime? GetPrimaryApiRetryTime()
        {
            return _primaryApiRetryTime;
        }
    }
}
