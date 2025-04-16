using System;
using System.Threading.Tasks;
using MarketCharts.Client.Services.Security;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MarketCharts.Tests.Security
{
    public class ApiKeyManagementTests
    {
        private readonly Mock<ILogger<ApiKeyManager>> _mockLogger;
        private readonly string _testEncryptionKey;
        private readonly string _testStoragePath;
        private readonly ApiKeyManager _apiKeyManager;

        public ApiKeyManagementTests()
        {
            _mockLogger = new Mock<ILogger<ApiKeyManager>>();
            _testEncryptionKey = "TestEncryptionKey123!";
            _testStoragePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"apikeys_test_{Guid.NewGuid()}.dat");
            _apiKeyManager = new ApiKeyManager(_mockLogger.Object, _testEncryptionKey, _testStoragePath);
        }

        [Fact]
        public async Task Should_SecurelyStoreApiKeys_When_ConfiguringServices()
        {
            // Arrange
            var serviceName = "AlphaVantage";
            var apiKey = "DEMO_API_KEY_12345";
            var permissions = new[] { "read", "historical_data" };

            // Act
            var result = await _apiKeyManager.StoreApiKeyAsync(serviceName, apiKey, permissions);
            var retrievedKey = _apiKeyManager.GetApiKey(serviceName);

            // Assert
            Assert.True(result);
            Assert.Equal(apiKey, retrievedKey);
            
            // Verify the key is stored in an encrypted format
            if (System.IO.File.Exists(_testStoragePath))
            {
                var fileContent = await System.IO.File.ReadAllTextAsync(_testStoragePath);
                Assert.DoesNotContain(apiKey, fileContent);
            }
        }

        [Fact]
        public async Task Should_NotExposeApiKeysInLogs_When_ErrorsOccur()
        {
            // Arrange
            var serviceName = "AlphaVantage";
            var apiKey = "DEMO_API_KEY_12345";
            var permissions = new[] { "read", "historical_data" };

            // Act
            await _apiKeyManager.StoreApiKeyAsync(serviceName, apiKey, permissions);
            
            // Simulate an error by using an invalid service name
            var retrievedKey = _apiKeyManager.GetApiKey("NonExistentService");

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => !v.ToString().Contains(apiKey)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task Should_RotateApiKeys_When_SecurityPolicyDictates()
        {
            // Arrange
            var serviceName = "AlphaVantage";
            var originalApiKey = "ORIGINAL_API_KEY_12345";
            var newApiKey = "NEW_API_KEY_67890";
            var permissions = new[] { "read", "historical_data" };

            await _apiKeyManager.StoreApiKeyAsync(serviceName, originalApiKey, permissions);
            var originalRetrievedKey = _apiKeyManager.GetApiKey(serviceName);

            // Act
            var rotateResult = await _apiKeyManager.RotateApiKeyAsync(serviceName, newApiKey);
            var newRetrievedKey = _apiKeyManager.GetApiKey(serviceName);

            // Assert
            Assert.True(rotateResult);
            Assert.Equal(originalApiKey, originalRetrievedKey);
            Assert.Equal(newApiKey, newRetrievedKey);
            Assert.NotEqual(originalRetrievedKey, newRetrievedKey);
        }

        [Fact]
        public async Task Should_ValidateApiKeyPermissions_When_MakingRequests()
        {
            // Arrange
            var serviceName = "AlphaVantage";
            var apiKey = "DEMO_API_KEY_12345";
            var permissions = new[] { "read", "historical_data" };

            await _apiKeyManager.StoreApiKeyAsync(serviceName, apiKey, permissions);

            // Act
            var hasAllPermissions = _apiKeyManager.ValidateApiKeyPermissions(serviceName, new[] { "read", "historical_data" });
            var hasSomePermissions = _apiKeyManager.ValidateApiKeyPermissions(serviceName, new[] { "read" });
            var hasNoPermissions = _apiKeyManager.ValidateApiKeyPermissions(serviceName, new[] { "write", "delete" });

            // Assert
            Assert.True(hasAllPermissions);
            Assert.True(hasSomePermissions);
            Assert.False(hasNoPermissions);
        }

        [Fact]
        public async Task Should_RevokeCompromisedKeys_When_SecurityBreachDetected()
        {
            // Arrange
            var serviceName = "AlphaVantage";
            var apiKey = "COMPROMISED_API_KEY_12345";
            var permissions = new[] { "read", "historical_data" };

            await _apiKeyManager.StoreApiKeyAsync(serviceName, apiKey, permissions);
            var keyBeforeRevocation = _apiKeyManager.GetApiKey(serviceName);

            // Act
            var revokeResult = await _apiKeyManager.RevokeApiKeyAsync(serviceName);
            var keyAfterRevocation = _apiKeyManager.GetApiKey(serviceName);

            // Assert
            Assert.True(revokeResult);
            Assert.Equal(apiKey, keyBeforeRevocation);
            Assert.Null(keyAfterRevocation);
        }
    }
}
