using System;
using System.Threading.Tasks;
using MarketCharts.Client.Models;
using MarketCharts.Client.Services.Security;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MarketCharts.Tests.Security
{
    public class DataProtectionTests
    {
        private readonly Mock<ILogger<DataProtectionService>> _mockLogger;
        private readonly string _testEncryptionKey;
        private readonly string _testStoragePath;
        private readonly DataProtectionService _dataProtectionService;

        public DataProtectionTests()
        {
            _mockLogger = new Mock<ILogger<DataProtectionService>>();
            _testEncryptionKey = "TestEncryptionKey123!";
            _testStoragePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"securedata_test_{Guid.NewGuid()}");
            _dataProtectionService = new DataProtectionService(_mockLogger.Object, _testEncryptionKey, _testStoragePath);
        }

        [Fact]
        public void Should_SanitizeInputData_When_ProcessingApiResponses()
        {
            // Arrange
            var maliciousInput = "<script>alert('XSS');</script>Stock data for S&P 500";
            var sqlInjectionInput = "S&P 500'; DROP TABLE StockData; --";
            var htmlTagsInput = "<div>Stock data</div> for <b>S&P 500</b>";

            // Act
            var sanitizedScript = _dataProtectionService.SanitizeInput(maliciousInput);
            var sanitizedSqlInjection = _dataProtectionService.SanitizeInput(sqlInjectionInput);
            var sanitizedHtmlTags = _dataProtectionService.SanitizeInput(htmlTagsInput);

            // Assert
            Assert.DoesNotContain("<script>", sanitizedScript);
            Assert.DoesNotContain("alert", sanitizedScript);
            Assert.Contains("Stock data for S&P 500", sanitizedScript);
            
            Assert.Contains("S&P 500", sanitizedSqlInjection);
            Assert.DoesNotContain("DROP TABLE", sanitizedSqlInjection);
            
            Assert.DoesNotContain("<div>", sanitizedHtmlTags);
            Assert.DoesNotContain("<b>", sanitizedHtmlTags);
            Assert.Contains("Stock data for S&P 500", sanitizedHtmlTags);
        }

        [Fact]
        public void Should_ValidateDataIntegrity_When_LoadingFromStorage()
        {
            // Arrange
            var validStockData = new StockIndexData
            {
                Id = 1,
                IndexName = "S&P 500",
                Date = DateTime.Today,
                OpenValue = 4000.0m,
                CloseValue = 4050.0m,
                HighValue = 4075.0m,
                LowValue = 3990.0m,
                Volume = 1000000,
                FetchedAt = DateTime.Now
            };

            var invalidStockData = new StockIndexData
            {
                Id = 2,
                IndexName = "S&P 500",
                Date = DateTime.Today,
                OpenValue = -4000.0m, // Invalid negative value
                CloseValue = 0m,      // Invalid zero value
                HighValue = 3000.0m,  // Invalid (lower than low value)
                LowValue = 5000.0m,   // Invalid (higher than high value)
                Volume = -1000,       // Invalid negative volume
                FetchedAt = DateTime.Now
            };

            // Define validation rules
            bool ValidateStockData(StockIndexData data)
            {
                return !string.IsNullOrEmpty(data.IndexName) &&
                       data.OpenValue > 0 &&
                       data.CloseValue > 0 &&
                       data.HighValue >= data.OpenValue &&
                       data.HighValue >= data.CloseValue &&
                       data.LowValue <= data.OpenValue &&
                       data.LowValue <= data.CloseValue &&
                       data.Volume >= 0;
            }

            // Act
            var validResult = _dataProtectionService.ValidateDataIntegrity(validStockData, ValidateStockData);
            var invalidResult = _dataProtectionService.ValidateDataIntegrity(invalidStockData, ValidateStockData);
            var nullResult = _dataProtectionService.ValidateDataIntegrity<StockIndexData>(null, ValidateStockData);

            // Assert
            Assert.True(validResult);
            Assert.False(invalidResult);
            Assert.False(nullResult);
        }

        [Fact]
        public async Task Should_SecureLocalStorage_When_SensitiveDataStored()
        {
            // Arrange
            var key = "api_credentials";
            var sensitiveData = new
            {
                Username = "apiuser",
                Password = "P@ssw0rd123!",
                ApiKey = "SECRET_API_KEY_12345"
            };

            // Act
            var storeResult = await _dataProtectionService.SecureStoreAsync(key, sensitiveData);
            var retrievedData = await _dataProtectionService.SecureRetrieveAsync<dynamic>(key);

            // Assert
            Assert.True(storeResult);
            Assert.NotNull(retrievedData);
            
            // Check if data is stored in encrypted form
            if (System.IO.Directory.Exists(_testStoragePath))
            {
                var filePath = System.IO.Path.Combine(_testStoragePath, $"{key}.dat");
                if (System.IO.File.Exists(filePath))
                {
                    var fileContent = await System.IO.File.ReadAllTextAsync(filePath);
                    Assert.DoesNotContain("apiuser", fileContent);
                    Assert.DoesNotContain("P@ssw0rd123!", fileContent);
                    Assert.DoesNotContain("SECRET_API_KEY_12345", fileContent);
                }
            }
        }

        [Fact]
        public void Should_ImplementProperEncryption_When_StoringCredentials()
        {
            // Arrange
            var plainCredentials = "username:password123";

            // Act
            var encryptedCredentials = _dataProtectionService.EncryptCredentials(plainCredentials);
            var decryptedCredentials = _dataProtectionService.DecryptCredentials(encryptedCredentials);

            // Assert
            Assert.NotEqual(plainCredentials, encryptedCredentials);
            Assert.Equal(plainCredentials, decryptedCredentials);
            Assert.DoesNotContain(plainCredentials, encryptedCredentials);
        }

        [Fact]
        public void Should_PreventUnauthorizedAccess_When_ApplicationInactive()
        {
            // Arrange
            var sessionId = _dataProtectionService.CreateSession();
            Assert.True(_dataProtectionService.ValidateSession(sessionId));

            // Act - simulate inactivity by manipulating the private field via reflection
            var fieldInfo = typeof(DataProtectionService).GetField("_lastActivityTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            fieldInfo.SetValue(_dataProtectionService, DateTime.UtcNow.AddMinutes(-31)); // Set last activity to 31 minutes ago (timeout is 30 minutes)

            var isSessionValid = _dataProtectionService.ValidateSession(sessionId);
            var shouldLock = _dataProtectionService.ShouldLockApplication();

            // Assert
            Assert.False(isSessionValid);
            Assert.True(shouldLock);

            // Act - create a new session and record activity
            var newSessionId = _dataProtectionService.CreateSession();
            _dataProtectionService.RecordActivity();

            // Assert
            Assert.True(_dataProtectionService.ValidateSession(newSessionId));
            Assert.False(_dataProtectionService.ShouldLockApplication());
        }
    }
}
