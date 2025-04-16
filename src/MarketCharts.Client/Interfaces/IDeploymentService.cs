using System.Threading.Tasks;
using System.Collections.Generic;

namespace MarketCharts.Client.Interfaces
{
    /// <summary>
    /// Interface for deployment service
    /// </summary>
    public interface IDeploymentService
    {
        /// <summary>
        /// Initializes the deployment service
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        Task InitializeAsync();

        /// <summary>
        /// Checks if the application is running in a production environment
        /// </summary>
        /// <returns>True if in production, false otherwise</returns>
        bool IsProduction();

        /// <summary>
        /// Gets the current deployment environment (e.g., "development", "staging", "production")
        /// </summary>
        /// <returns>The name of the current environment</returns>
        string GetEnvironment();

        /// <summary>
        /// Gets the deployment platform (e.g., "Vercel", "Netlify", "Azure")
        /// </summary>
        /// <returns>The name of the deployment platform</returns>
        string GetDeploymentPlatform();

        /// <summary>
        /// Loads environment variables from the deployment platform
        /// </summary>
        /// <returns>A dictionary of environment variables</returns>
        Dictionary<string, string> LoadEnvironmentVariables();

        /// <summary>
        /// Configures routing for the deployment platform
        /// </summary>
        /// <returns>True if routing was configured successfully, false otherwise</returns>
        bool ConfigureRouting();

        /// <summary>
        /// Checks if static assets are properly bundled and optimized
        /// </summary>
        /// <returns>True if assets are optimized, false otherwise</returns>
        bool AreAssetsOptimized();

        /// <summary>
        /// Gets the CDN URL for static assets
        /// </summary>
        /// <returns>The CDN URL</returns>
        string GetCdnUrl();

        /// <summary>
        /// Checks if the application is using a CDN for static assets
        /// </summary>
        /// <returns>True if using a CDN, false otherwise</returns>
        bool IsUsingCdn();
    }
}
