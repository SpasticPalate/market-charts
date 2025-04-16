using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MarketCharts.Client.Interfaces
{
    /// <summary>
    /// Interface for version management service
    /// </summary>
    public interface IVersionManagementService
    {
        /// <summary>
        /// Initializes the version management service
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        Task InitializeAsync();

        /// <summary>
        /// Gets the current application version
        /// </summary>
        /// <returns>The current version</returns>
        string GetCurrentVersion();

        /// <summary>
        /// Checks if the current version is compatible with the specified dependency version
        /// </summary>
        /// <param name="dependencyName">The name of the dependency</param>
        /// <param name="dependencyVersion">The version of the dependency</param>
        /// <returns>True if compatible, false otherwise</returns>
        bool IsCompatibleWithDependency(string dependencyName, string dependencyVersion);

        /// <summary>
        /// Gets a list of all dependencies and their versions
        /// </summary>
        /// <returns>A dictionary of dependency names and versions</returns>
        Dictionary<string, string> GetDependencies();

        /// <summary>
        /// Executes database schema migrations if needed
        /// </summary>
        /// <returns>True if migrations were executed successfully, false otherwise</returns>
        Task<bool> ExecuteMigrationsAsync();

        /// <summary>
        /// Checks if database schema migrations are needed
        /// </summary>
        /// <returns>True if migrations are needed, false otherwise</returns>
        Task<bool> AreMigrationsNeededAsync();

        /// <summary>
        /// Preserves user settings during an application update
        /// </summary>
        /// <returns>True if settings were preserved, false otherwise</returns>
        Task<bool> PreserveUserSettingsAsync();

        /// <summary>
        /// Gets user settings that need to be preserved during updates
        /// </summary>
        /// <returns>A dictionary of user settings</returns>
        Task<Dictionary<string, object>> GetUserSettingsAsync();

        /// <summary>
        /// Restores user settings after an application update
        /// </summary>
        /// <param name="settings">The settings to restore</param>
        /// <returns>True if settings were restored, false otherwise</returns>
        Task<bool> RestoreUserSettingsAsync(Dictionary<string, object> settings);

        /// <summary>
        /// Rolls back to a previous version if deployment fails
        /// </summary>
        /// <param name="targetVersion">The version to roll back to</param>
        /// <returns>True if rollback was successful, false otherwise</returns>
        Task<bool> RollbackAsync(string targetVersion);

        /// <summary>
        /// Gets a list of available versions to roll back to
        /// </summary>
        /// <returns>A list of available versions</returns>
        Task<List<string>> GetAvailableRollbackVersionsAsync();

        /// <summary>
        /// Checks if a rollback is in progress
        /// </summary>
        /// <returns>True if a rollback is in progress, false otherwise</returns>
        bool IsRollbackInProgress();
    }
}
