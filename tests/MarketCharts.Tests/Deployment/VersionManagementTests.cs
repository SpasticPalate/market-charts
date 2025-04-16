using Xunit;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MarketCharts.Client.Interfaces;
using MarketCharts.Client.Models.Configuration;
using MarketCharts.Client.Services.Deployment;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace MarketCharts.Tests.Deployment
{
    public class VersionManagementTests
    {
        private readonly Mock<IJSRuntime> _mockJsRuntime;
        private readonly Mock<ILogger<VersionManagementService>> _mockLogger;
        private readonly AppConfiguration _appConfiguration;

        public VersionManagementTests()
        {
            _mockJsRuntime = new Mock<IJSRuntime>();
            _mockLogger = new Mock<ILogger<VersionManagementService>>();
            _appConfiguration = new AppConfiguration
            {
                Version = "1.0.0",
                ChartConfig = new ChartConfiguration
                {
                    UseDarkMode = true,
                    EnableAnimations = true
                },
                EnableComparison = true,
                EnableTechnicalIndicators = true
            };
        }

        [Fact]
        public async Task Should_MaintainCompatibility_When_UpgradingDependencies()
        {
            // Arrange
            var service = new VersionManagementService(_mockJsRuntime.Object, _mockLogger.Object, _appConfiguration);
            await service.InitializeAsync();
            
            // Act
            bool isCompatible = service.IsCompatibleWithDependency("blazor", "7.0.0");
            bool isIncompatible = service.IsCompatibleWithDependency("blazor", "6.0.0");
            
            // Assert
            Assert.True(isCompatible);
            Assert.False(isIncompatible);
        }

        [Fact]
        public async Task Should_ExecuteMigrations_When_SchemaChanges()
        {
            // Arrange
            _mockJsRuntime.Setup(js => js.InvokeAsync<string>(It.Is<string>(s => s == "eval"), It.Is<object[]>(args => args.Length > 0 && args[0].ToString().Contains("lastMigrationVersion"))))
                .ReturnsAsync("0.9.0");
            
            // We can't mock extension methods directly, so we'll skip this setup
            // and modify our service to handle the case when this method is not mocked
            
            var service = new VersionManagementService(_mockJsRuntime.Object, _mockLogger.Object, _appConfiguration);
            await service.InitializeAsync();
            
            // Act
            bool migrationsNeeded = await service.AreMigrationsNeededAsync();
            bool migrationsExecuted = await service.ExecuteMigrationsAsync();
            
            // Assert
            Assert.True(migrationsNeeded);
            Assert.True(migrationsExecuted);
        }

        [Fact]
        public async Task Should_PreserveUserSettings_When_ApplicationUpdated()
        {
            // Arrange
            _mockJsRuntime.Setup(js => js.InvokeAsync<bool>(It.Is<string>(s => s == "eval"), It.Is<object[]>(args => args.Length > 0 && args[0].ToString().Contains("userSettings"))))
                .ReturnsAsync(false);
            
            // We can't mock extension methods directly, so we'll skip this setup
            // and modify our service to handle the case when this method is not mocked
            
            var service = new VersionManagementService(_mockJsRuntime.Object, _mockLogger.Object, _appConfiguration);
            await service.InitializeAsync();
            
            // Act
            var settings = await service.GetUserSettingsAsync();
            bool settingsPreserved = await service.PreserveUserSettingsAsync();
            bool settingsRestored = await service.RestoreUserSettingsAsync(settings);
            
            // Assert
            Assert.NotEmpty(settings);
            Assert.True(settingsPreserved);
            Assert.True(settingsRestored);
        }

        [Fact]
        public async Task Should_ProvideVersionInfo_When_ApplicationLoads()
        {
            // Arrange
            var service = new VersionManagementService(_mockJsRuntime.Object, _mockLogger.Object, _appConfiguration);
            await service.InitializeAsync();
            
            // Act
            string version = service.GetCurrentVersion();
            var dependencies = service.GetDependencies();
            
            // Assert
            Assert.Equal("1.0.0", version);
            Assert.NotEmpty(dependencies);
        }

        [Fact]
        public async Task Should_SupportRollback_When_DeploymentFails()
        {
            // Arrange
            _mockJsRuntime.Setup(js => js.InvokeAsync<bool>(It.Is<string>(s => s == "eval"), It.IsAny<object[]>()))
                .ReturnsAsync(false);
            
            // We can't mock extension methods directly, so we'll skip this setup
            // and modify our service to handle the case when this method is not mocked
            
            var service = new VersionManagementService(_mockJsRuntime.Object, _mockLogger.Object, _appConfiguration);
            await service.InitializeAsync();
            
            // Act
            var availableVersions = await service.GetAvailableRollbackVersionsAsync();
            bool isRollbackInProgress = service.IsRollbackInProgress();
            bool rollbackSuccessful = await service.RollbackAsync("0.9.0");
            
            // Assert
            Assert.NotEmpty(availableVersions);
            Assert.False(isRollbackInProgress);
            Assert.True(rollbackSuccessful);
            Assert.Equal("0.9.0", _appConfiguration.Version);
        }
    }
}
