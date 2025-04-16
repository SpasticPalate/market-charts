using Xunit;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using MarketCharts.Client.Interfaces;
using MarketCharts.Client.Services.Deployment;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace MarketCharts.Tests.Deployment
{
    public class VercelDeploymentTests
    {
        private readonly Mock<IJSRuntime> _mockJsRuntime;
        private readonly Mock<ILogger<VercelDeploymentService>> _mockLogger;

        public VercelDeploymentTests()
        {
            _mockJsRuntime = new Mock<IJSRuntime>();
            _mockLogger = new Mock<ILogger<VercelDeploymentService>>();
        }

        [Fact]
        public async Task Should_CompileToStaticAssets_When_BuildTriggered()
        {
            // Arrange
            _mockJsRuntime.Setup(js => js.InvokeAsync<bool>(It.Is<string>(s => s == "eval"), It.IsAny<object[]>()))
                .ReturnsAsync(true);
            
            _mockJsRuntime.Setup(js => js.InvokeAsync<string>(It.Is<string>(s => s == "eval"), It.IsAny<object[]>()))
                .ReturnsAsync("vercel-app.com");
            
            var service = new VercelDeploymentService(_mockJsRuntime.Object, _mockLogger.Object);
            await service.InitializeAsync();
            
            // Act
            bool result = service.CompileToStaticAssets();
            
            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task Should_ConfigureProperRouting_When_DeployedToVercel()
        {
            // Arrange
            _mockJsRuntime.Setup(js => js.InvokeAsync<bool>(It.Is<string>(s => s == "eval"), It.IsAny<object[]>()))
                .ReturnsAsync(true);
            
            _mockJsRuntime.Setup(js => js.InvokeAsync<string>(It.Is<string>(s => s == "eval"), It.IsAny<object[]>()))
                .ReturnsAsync("vercel-app.com");
            
            var service = new VercelDeploymentService(_mockJsRuntime.Object, _mockLogger.Object);
            await service.InitializeAsync();
            
            // Act
            bool result = service.ConfigureRouting();
            
            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task Should_LoadEnvironmentVariables_When_DeployedToProduction()
        {
            // Arrange
            _mockJsRuntime.Setup(js => js.InvokeAsync<bool>(It.Is<string>(s => s == "eval"), It.IsAny<object[]>()))
                .ReturnsAsync(true);
            
            _mockJsRuntime.Setup(js => js.InvokeAsync<string>(It.Is<string>(s => s == "eval"), It.IsAny<object[]>()))
                .ReturnsAsync("production-vercel-app.com");
            
            var service = new VercelDeploymentService(_mockJsRuntime.Object, _mockLogger.Object);
            await service.InitializeAsync();
            
            // Act
            var environmentVariables = service.LoadEnvironmentVariables();
            bool isProduction = service.IsProduction();
            
            // Assert
            Assert.NotEmpty(environmentVariables);
            Assert.True(isProduction);
            Assert.Contains("VERCEL", environmentVariables.Keys);
            Assert.Contains("VERCEL_ENV", environmentVariables.Keys);
        }

        [Fact]
        public async Task Should_OptimizeAssetSize_When_Bundled()
        {
            // Arrange
            _mockJsRuntime.Setup(js => js.InvokeAsync<bool>(It.Is<string>(s => s == "eval"), It.IsAny<object[]>()))
                .ReturnsAsync(true);
            
            _mockJsRuntime.Setup(js => js.InvokeAsync<string>(It.Is<string>(s => s == "eval"), It.IsAny<object[]>()))
                .ReturnsAsync("vercel-app.com");
            
            var service = new VercelDeploymentService(_mockJsRuntime.Object, _mockLogger.Object);
            await service.InitializeAsync();
            
            // Act
            bool result = service.OptimizeAssetSize();
            bool areAssetsOptimized = service.AreAssetsOptimized();
            
            // Assert
            Assert.True(result);
            Assert.True(areAssetsOptimized);
        }

        [Fact]
        public async Task Should_SupportCDNDistribution_When_StaticAssetsLoaded()
        {
            // Arrange
            _mockJsRuntime.Setup(js => js.InvokeAsync<bool>(It.Is<string>(s => s == "eval"), It.IsAny<object[]>()))
                .ReturnsAsync(true);
            
            _mockJsRuntime.Setup(js => js.InvokeAsync<string>(It.Is<string>(s => s == "eval"), It.IsAny<object[]>()))
                .ReturnsAsync("https://market-charts.vercel.app");
            
            var service = new VercelDeploymentService(_mockJsRuntime.Object, _mockLogger.Object);
            await service.InitializeAsync();
            
            // Act
            bool isUsingCdn = service.IsUsingCdn();
            string cdnUrl = service.GetCdnUrl();
            
            // Assert
            Assert.True(isUsingCdn);
            Assert.NotEmpty(cdnUrl);
        }
    }
}
