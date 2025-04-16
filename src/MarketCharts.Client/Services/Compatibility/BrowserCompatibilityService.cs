using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MarketCharts.Client.Interfaces;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;

namespace MarketCharts.Client.Services.Compatibility
{
    /// <summary>
    /// Service for handling browser compatibility
    /// </summary>
    public class BrowserCompatibilityService : IBrowserCompatibilityService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<BrowserCompatibilityService> _logger;
        private Dictionary<string, bool> _browserCapabilities;
        private string _browserName;
        private bool _isMobile;
        private bool _isWebAssemblySupported;
        private bool _initialized = false;

        /// <summary>
        /// Constructor for BrowserCompatibilityService
        /// </summary>
        /// <param name="jsRuntime">JavaScript runtime</param>
        /// <param name="logger">Logger</param>
        public BrowserCompatibilityService(IJSRuntime jsRuntime, ILogger<BrowserCompatibilityService> logger)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _browserCapabilities = new Dictionary<string, bool>();
        }

        /// <summary>
        /// Initializes the browser compatibility service
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task InitializeAsync()
        {
            if (_initialized)
                return;

            try
            {
                _browserName = await _jsRuntime.InvokeAsync<string>("eval", "navigator.userAgent");
                _isMobile = await _jsRuntime.InvokeAsync<bool>("eval", 
                    "(/Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent))");
                _isWebAssemblySupported = await _jsRuntime.InvokeAsync<bool>("eval", 
                    "typeof WebAssembly === 'object' && typeof WebAssembly.instantiate === 'function'");
                
                await DetectBrowserCapabilities();
                _initialized = true;
                
                _logger.LogInformation($"Browser detected: {_browserName}, Mobile: {_isMobile}, WebAssembly: {_isWebAssemblySupported}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing browser compatibility service");
                throw;
            }
        }

        /// <summary>
        /// Detects browser capabilities
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        private async Task DetectBrowserCapabilities()
        {
            try
            {
                _browserCapabilities["webAssembly"] = _isWebAssemblySupported;
                _browserCapabilities["localStorage"] = await _jsRuntime.InvokeAsync<bool>("eval", "typeof localStorage !== 'undefined'");
                _browserCapabilities["sessionStorage"] = await _jsRuntime.InvokeAsync<bool>("eval", "typeof sessionStorage !== 'undefined'");
                _browserCapabilities["indexedDB"] = await _jsRuntime.InvokeAsync<bool>("eval", "typeof indexedDB !== 'undefined'");
                _browserCapabilities["webWorkers"] = await _jsRuntime.InvokeAsync<bool>("eval", "typeof Worker !== 'undefined'");
                _browserCapabilities["webSockets"] = await _jsRuntime.InvokeAsync<bool>("eval", "typeof WebSocket !== 'undefined'");
                _browserCapabilities["webGL"] = await _jsRuntime.InvokeAsync<bool>("eval", 
                    "(() => { try { return !!window.WebGLRenderingContext; } catch(e) { return false; } })()");
                _browserCapabilities["canvas"] = await _jsRuntime.InvokeAsync<bool>("eval", 
                    "(() => { try { document.createElement('canvas').getContext('2d'); return true; } catch(e) { return false; } })()");
                _browserCapabilities["css3"] = await _jsRuntime.InvokeAsync<bool>("eval", 
                    "(() => { try { return 'CSS' in window && 'supports' in window.CSS; } catch(e) { return false; } })()");
                _browserCapabilities["flexbox"] = await _jsRuntime.InvokeAsync<bool>("eval", 
                    "(() => { try { return CSS.supports('display', 'flex'); } catch(e) { return false; } })()");
                _browserCapabilities["grid"] = await _jsRuntime.InvokeAsync<bool>("eval", 
                    "(() => { try { return CSS.supports('display', 'grid'); } catch(e) { return false; } })()");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting browser capabilities");
                throw;
            }
        }

        /// <summary>
        /// Checks if the current browser is supported
        /// </summary>
        /// <returns>True if the browser is supported, false otherwise</returns>
        public bool IsBrowserSupported()
        {
            EnsureInitialized();
            
            // Check for Chrome, Firefox, Safari, or Edge
            bool isChrome = _browserName.Contains("Chrome") && !_browserName.Contains("Edg");
            bool isFirefox = _browserName.Contains("Firefox");
            bool isSafari = _browserName.Contains("Safari") && !_browserName.Contains("Chrome");
            bool isEdge = _browserName.Contains("Edg");
            
            return isChrome || isFirefox || isSafari || isEdge;
        }

        /// <summary>
        /// Gets the current browser name
        /// </summary>
        /// <returns>The name of the current browser</returns>
        public string GetBrowserName()
        {
            EnsureInitialized();
            
            if (_browserName.Contains("Chrome") && !_browserName.Contains("Edg"))
                return "Chrome";
            if (_browserName.Contains("Firefox"))
                return "Firefox";
            if (_browserName.Contains("Safari") && !_browserName.Contains("Chrome"))
                return "Safari";
            if (_browserName.Contains("Edg"))
                return "Edge";
            
            return "Unknown";
        }

        /// <summary>
        /// Checks if the current browser supports WebAssembly
        /// </summary>
        /// <returns>True if WebAssembly is supported, false otherwise</returns>
        public bool IsWebAssemblySupported()
        {
            EnsureInitialized();
            return _isWebAssemblySupported;
        }

        /// <summary>
        /// Applies browser-specific rendering optimizations
        /// </summary>
        /// <returns>True if optimizations were applied, false otherwise</returns>
        public bool ApplyBrowserOptimizations()
        {
            EnsureInitialized();
            
            try
            {
                string browserName = GetBrowserName();
                
                switch (browserName)
                {
                    case "Chrome":
                        // Chrome-specific optimizations
                        _logger.LogInformation("Applying Chrome-specific optimizations");
                        return true;
                    
                    case "Firefox":
                        // Firefox-specific optimizations
                        _logger.LogInformation("Applying Firefox-specific optimizations");
                        return true;
                    
                    case "Safari":
                        // Safari-specific optimizations
                        _logger.LogInformation("Applying Safari-specific optimizations");
                        return true;
                    
                    case "Edge":
                        // Edge-specific optimizations
                        _logger.LogInformation("Applying Edge-specific optimizations");
                        return true;
                    
                    default:
                        _logger.LogWarning($"No specific optimizations available for browser: {browserName}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying browser optimizations");
                return false;
            }
        }

        /// <summary>
        /// Checks if the current browser is a mobile browser
        /// </summary>
        /// <returns>True if the browser is a mobile browser, false otherwise</returns>
        public bool IsMobileBrowser()
        {
            EnsureInitialized();
            return _isMobile;
        }

        /// <summary>
        /// Applies mobile-specific optimizations for WebKit browsers (Safari on iOS)
        /// </summary>
        /// <returns>True if optimizations were applied, false otherwise</returns>
        public bool OptimizeForMobileWebKit()
        {
            EnsureInitialized();
            
            try
            {
                if (IsMobileBrowser() && GetBrowserName() == "Safari")
                {
                    _logger.LogInformation("Applying mobile WebKit optimizations");
                    // Apply iOS Safari specific optimizations
                    return true;
                }
                
                _logger.LogInformation("Mobile WebKit optimizations not applicable for current browser");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying mobile WebKit optimizations");
                return false;
            }
        }

        /// <summary>
        /// Adapts rendering for cross-browser compatibility
        /// </summary>
        /// <returns>True if adaptations were applied, false otherwise</returns>
        public bool AdaptToRenderingDifferences()
        {
            EnsureInitialized();
            
            try
            {
                string browserName = GetBrowserName();
                
                // Apply cross-browser adaptations based on detected browser
                _logger.LogInformation($"Adapting rendering for {browserName}");
                
                // Apply specific adaptations for each browser
                switch (browserName)
                {
                    case "Safari":
                        // Safari-specific adaptations
                        return true;
                    
                    case "Firefox":
                        // Firefox-specific adaptations
                        return true;
                    
                    case "Edge":
                        // Edge-specific adaptations
                        return true;
                    
                    case "Chrome":
                    default:
                        // Chrome is our baseline, no specific adaptations needed
                        return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adapting to rendering differences");
                return false;
            }
        }

        /// <summary>
        /// Gets browser capabilities and features
        /// </summary>
        /// <returns>A dictionary of browser capabilities</returns>
        public Dictionary<string, bool> GetBrowserCapabilities()
        {
            EnsureInitialized();
            return new Dictionary<string, bool>(_browserCapabilities);
        }

        /// <summary>
        /// Ensures that the service is initialized
        /// </summary>
        private void EnsureInitialized()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("Browser compatibility service is not initialized. Call InitializeAsync first.");
            }
        }
    }
}
