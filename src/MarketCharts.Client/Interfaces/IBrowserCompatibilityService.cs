using System.Threading.Tasks;

namespace MarketCharts.Client.Interfaces
{
    /// <summary>
    /// Interface for browser compatibility service
    /// </summary>
    public interface IBrowserCompatibilityService
    {
        /// <summary>
        /// Initializes the browser compatibility service
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        Task InitializeAsync();

        /// <summary>
        /// Checks if the current browser is supported
        /// </summary>
        /// <returns>True if the browser is supported, false otherwise</returns>
        bool IsBrowserSupported();

        /// <summary>
        /// Gets the current browser name
        /// </summary>
        /// <returns>The name of the current browser</returns>
        string GetBrowserName();

        /// <summary>
        /// Checks if the current browser supports WebAssembly
        /// </summary>
        /// <returns>True if WebAssembly is supported, false otherwise</returns>
        bool IsWebAssemblySupported();

        /// <summary>
        /// Applies browser-specific rendering optimizations
        /// </summary>
        /// <returns>True if optimizations were applied, false otherwise</returns>
        bool ApplyBrowserOptimizations();

        /// <summary>
        /// Checks if the current browser is a mobile browser
        /// </summary>
        /// <returns>True if the browser is a mobile browser, false otherwise</returns>
        bool IsMobileBrowser();

        /// <summary>
        /// Applies mobile-specific optimizations for WebKit browsers (Safari on iOS)
        /// </summary>
        /// <returns>True if optimizations were applied, false otherwise</returns>
        bool OptimizeForMobileWebKit();

        /// <summary>
        /// Adapts rendering for cross-browser compatibility
        /// </summary>
        /// <returns>True if adaptations were applied, false otherwise</returns>
        bool AdaptToRenderingDifferences();

        /// <summary>
        /// Gets browser capabilities and features
        /// </summary>
        /// <returns>A dictionary of browser capabilities</returns>
        System.Collections.Generic.Dictionary<string, bool> GetBrowserCapabilities();
    }
}
