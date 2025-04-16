using Xunit;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using MarketCharts.Client.Interfaces;
using MarketCharts.Client.Services.Compatibility;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace MarketCharts.Tests.CrossBrowser
{
    public class BrowserCompatibilityTests
    {
        private readonly Mock<IJSRuntime> _mockJsRuntime;
        private readonly Mock<ILogger<BrowserCompatibilityService>> _mockLogger;

        public BrowserCompatibilityTests()
        {
            _mockJsRuntime = new Mock<IJSRuntime>();
            _mockLogger = new Mock<ILogger<BrowserCompatibilityService>>();
        }

        [Fact]
        public async Task Should_RenderConsistently_When_RunningOnChrome()
        {
            // Arrange
            _mockJsRuntime.Setup(js => js.InvokeAsync<string>(It.Is<string>(s => s == "eval"), It.IsAny<object[]>()))
                .ReturnsAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            
            _mockJsRuntime.Setup(js => js.InvokeAsync<bool>(It.Is<string>(s => s == "eval"), It.IsAny<object[]>()))
                .ReturnsAsync(true);
            
            var service = new BrowserCompatibilityService(_mockJsRuntime.Object, _mockLogger.Object);
            await service.InitializeAsync();
            
            // Act
            string browserName = service.GetBrowserName();
            bool isSupported = service.IsBrowserSupported();
            bool optimizationsApplied = service.ApplyBrowserOptimizations();
            
            // Assert
            Assert.Equal("Chrome", browserName);
            Assert.True(isSupported);
            Assert.True(optimizationsApplied);
        }

        [Fact]
        public async Task Should_RenderConsistently_When_RunningOnFirefox()
        {
            // Arrange
            _mockJsRuntime.Setup(js => js.InvokeAsync<string>(It.Is<string>(s => s == "eval"), It.IsAny<object[]>()))
                .ReturnsAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:89.0) Gecko/20100101 Firefox/89.0");
            
            _mockJsRuntime.Setup(js => js.InvokeAsync<bool>(It.Is<string>(s => s == "eval"), It.IsAny<object[]>()))
                .ReturnsAsync(true);
            
            var service = new BrowserCompatibilityService(_mockJsRuntime.Object, _mockLogger.Object);
            await service.InitializeAsync();
            
            // Act
            string browserName = service.GetBrowserName();
            bool isSupported = service.IsBrowserSupported();
            bool optimizationsApplied = service.ApplyBrowserOptimizations();
            
            // Assert
            Assert.Equal("Firefox", browserName);
            Assert.True(isSupported);
            Assert.True(optimizationsApplied);
        }

        [Fact]
        public async Task Should_RenderConsistently_When_RunningOnSafari()
        {
            // Arrange
            _mockJsRuntime.Setup(js => js.InvokeAsync<string>(It.Is<string>(s => s == "eval"), It.IsAny<object[]>()))
                .ReturnsAsync("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.1.1 Safari/605.1.15");
            
            _mockJsRuntime.Setup(js => js.InvokeAsync<bool>(It.Is<string>(s => s == "eval"), It.IsAny<object[]>()))
                .ReturnsAsync(true);
            
            var service = new BrowserCompatibilityService(_mockJsRuntime.Object, _mockLogger.Object);
            await service.InitializeAsync();
            
            // Act
            string browserName = service.GetBrowserName();
            bool isSupported = service.IsBrowserSupported();
            bool optimizationsApplied = service.ApplyBrowserOptimizations();
            
            // Assert
            Assert.Equal("Safari", browserName);
            Assert.True(isSupported);
            Assert.True(optimizationsApplied);
        }

        [Fact]
        public async Task Should_RenderConsistently_When_RunningOnEdge()
        {
            // Arrange
            _mockJsRuntime.Setup(js => js.InvokeAsync<string>(It.Is<string>(s => s == "eval"), It.IsAny<object[]>()))
                .ReturnsAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36 Edg/91.0.864.59");
            
            _mockJsRuntime.Setup(js => js.InvokeAsync<bool>(It.Is<string>(s => s == "eval"), It.IsAny<object[]>()))
                .ReturnsAsync(true);
            
            var service = new BrowserCompatibilityService(_mockJsRuntime.Object, _mockLogger.Object);
            await service.InitializeAsync();
            
            // Act
            string browserName = service.GetBrowserName();
            bool isSupported = service.IsBrowserSupported();
            bool optimizationsApplied = service.ApplyBrowserOptimizations();
            
            // Assert
            Assert.Equal("Edge", browserName);
            Assert.True(isSupported);
            Assert.True(optimizationsApplied);
        }

        [Fact]
        public async Task Should_HandleWebAssemblySupport_When_CheckingBrowserCompatibility()
        {
            // Arrange
            _mockJsRuntime.Setup(js => js.InvokeAsync<string>(It.Is<string>(s => s == "eval"), It.IsAny<object[]>()))
                .ReturnsAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            
            _mockJsRuntime.Setup(js => js.InvokeAsync<bool>(It.Is<string>(s => s == "eval"), It.Is<object[]>(args => args.Length > 0 && args[0].ToString().Contains("WebAssembly"))))
                .ReturnsAsync(true);
            
            var service = new BrowserCompatibilityService(_mockJsRuntime.Object, _mockLogger.Object);
            await service.InitializeAsync();
            
            // Act
            bool isWebAssemblySupported = service.IsWebAssemblySupported();
            
            // Assert
            Assert.True(isWebAssemblySupported);
        }

        [Fact]
        public async Task Should_AdaptToRenderingDifferences_When_CrossBrowser()
        {
            // Arrange
            _mockJsRuntime.Setup(js => js.InvokeAsync<string>(It.Is<string>(s => s == "eval"), It.IsAny<object[]>()))
                .ReturnsAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            
            _mockJsRuntime.Setup(js => js.InvokeAsync<bool>(It.Is<string>(s => s == "eval"), It.IsAny<object[]>()))
                .ReturnsAsync(true);
            
            var service = new BrowserCompatibilityService(_mockJsRuntime.Object, _mockLogger.Object);
            await service.InitializeAsync();
            
            // Act
            bool adaptationsApplied = service.AdaptToRenderingDifferences();
            
            // Assert
            Assert.True(adaptationsApplied);
        }

        [Fact]
        public async Task Should_OptimizeForMobileWebKit_When_RunningOnIOSDevices()
        {
            // Arrange
            _mockJsRuntime.Setup(js => js.InvokeAsync<string>(It.Is<string>(s => s == "eval"), It.IsAny<object[]>()))
                .ReturnsAsync("Mozilla/5.0 (iPhone; CPU iPhone OS 14_6 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.0 Mobile/15E148 Safari/604.1");
            
            _mockJsRuntime.Setup(js => js.InvokeAsync<bool>(It.Is<string>(s => s == "eval"), It.Is<object[]>(args => args.Length > 0 && (args[0].ToString().Contains("iPhone") || args[0].ToString().Contains("iPad") || args[0].ToString().Contains("iPod")))))
                .ReturnsAsync(true);
            
            var service = new BrowserCompatibilityService(_mockJsRuntime.Object, _mockLogger.Object);
            await service.InitializeAsync();
            
            // Act
            bool isMobile = service.IsMobileBrowser();
            string browserName = service.GetBrowserName();
            bool optimizationsApplied = service.OptimizeForMobileWebKit();
            
            // Assert
            Assert.True(isMobile);
            Assert.Equal("Safari", browserName);
            Assert.True(optimizationsApplied);
        }
    }
}
