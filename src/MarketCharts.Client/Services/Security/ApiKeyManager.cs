using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MarketCharts.Client.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace MarketCharts.Client.Services.Security
{
    /// <summary>
    /// Manages API keys securely for the application
    /// </summary>
    public class ApiKeyManager
    {
        private readonly ILogger<ApiKeyManager> _logger;
        private readonly string _encryptionKey;
        private readonly string _keyStoragePath;
        private readonly Dictionary<string, ApiKeyInfo> _apiKeys = new Dictionary<string, ApiKeyInfo>();
        private readonly object _lockObject = new object();

        /// <summary>
        /// Initializes a new instance of the ApiKeyManager class
        /// </summary>
        /// <param name="logger">The logger</param>
        /// <param name="encryptionKey">The encryption key for securing API keys</param>
        /// <param name="keyStoragePath">The path to store encrypted API keys</param>
        public ApiKeyManager(ILogger<ApiKeyManager> logger, string encryptionKey, string keyStoragePath)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            if (string.IsNullOrEmpty(encryptionKey))
                throw new ArgumentException("Encryption key cannot be null or empty", nameof(encryptionKey));
            
            _encryptionKey = encryptionKey;
            _keyStoragePath = keyStoragePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "apikeys.dat");
        }

        /// <summary>
        /// Stores an API key securely
        /// </summary>
        /// <param name="serviceName">The name of the service the key is for</param>
        /// <param name="apiKey">The API key to store</param>
        /// <param name="permissions">The permissions associated with this key</param>
        /// <returns>True if the key was stored successfully, false otherwise</returns>
        public async Task<bool> StoreApiKeyAsync(string serviceName, string apiKey, string[] permissions)
        {
            try
            {
                if (string.IsNullOrEmpty(serviceName) || string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogWarning("Attempted to store null or empty API key for service {ServiceName}", serviceName);
                    return false;
                }

                var keyInfo = new ApiKeyInfo
                {
                    ServiceName = serviceName,
                    EncryptedKey = EncryptString(apiKey),
                    Permissions = permissions,
                    CreatedAt = DateTime.UtcNow,
                    LastRotatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddDays(90) // Default 90-day expiration
                };

                lock (_lockObject)
                {
                    _apiKeys[serviceName] = keyInfo;
                }

                await SaveKeysToStorageAsync();
                _logger.LogInformation("API key for service {ServiceName} stored securely", serviceName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing API key for service {ServiceName}", serviceName);
                return false;
            }
        }

        /// <summary>
        /// Retrieves an API key
        /// </summary>
        /// <param name="serviceName">The name of the service to get the key for</param>
        /// <returns>The API key, or null if not found</returns>
        public string? GetApiKey(string serviceName)
        {
            try
            {
                lock (_lockObject)
                {
                    if (_apiKeys.TryGetValue(serviceName, out var keyInfo))
                    {
                        if (keyInfo.ExpiresAt < DateTime.UtcNow)
                        {
                            _logger.LogWarning("API key for service {ServiceName} has expired", serviceName);
                            return null;
                        }

                        return DecryptString(keyInfo.EncryptedKey);
                    }
                }

                _logger.LogWarning("API key for service {ServiceName} not found", serviceName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving API key for service {ServiceName}", serviceName);
                return null;
            }
        }

        /// <summary>
        /// Rotates an API key with a new value
        /// </summary>
        /// <param name="serviceName">The name of the service to rotate the key for</param>
        /// <param name="newApiKey">The new API key</param>
        /// <returns>True if the key was rotated successfully, false otherwise</returns>
        public async Task<bool> RotateApiKeyAsync(string serviceName, string newApiKey)
        {
            try
            {
                lock (_lockObject)
                {
                    if (!_apiKeys.TryGetValue(serviceName, out var keyInfo))
                    {
                        _logger.LogWarning("Cannot rotate API key for service {ServiceName} - key not found", serviceName);
                        return false;
                    }

                    var permissions = keyInfo.Permissions;
                    keyInfo.EncryptedKey = EncryptString(newApiKey);
                    keyInfo.LastRotatedAt = DateTime.UtcNow;
                    keyInfo.ExpiresAt = DateTime.UtcNow.AddDays(90); // Reset expiration
                    _apiKeys[serviceName] = keyInfo;
                }

                await SaveKeysToStorageAsync();
                _logger.LogInformation("API key for service {ServiceName} rotated successfully", serviceName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rotating API key for service {ServiceName}", serviceName);
                return false;
            }
        }

        /// <summary>
        /// Revokes an API key
        /// </summary>
        /// <param name="serviceName">The name of the service to revoke the key for</param>
        /// <returns>True if the key was revoked successfully, false otherwise</returns>
        public async Task<bool> RevokeApiKeyAsync(string serviceName)
        {
            try
            {
                bool removed;
                lock (_lockObject)
                {
                    removed = _apiKeys.Remove(serviceName);
                }

                if (removed)
                {
                    await SaveKeysToStorageAsync();
                    _logger.LogInformation("API key for service {ServiceName} revoked successfully", serviceName);
                    return true;
                }

                _logger.LogWarning("Cannot revoke API key for service {ServiceName} - key not found", serviceName);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking API key for service {ServiceName}", serviceName);
                return false;
            }
        }

        /// <summary>
        /// Validates if an API key has the required permissions
        /// </summary>
        /// <param name="serviceName">The name of the service to check</param>
        /// <param name="requiredPermissions">The permissions to check for</param>
        /// <returns>True if the key has all required permissions, false otherwise</returns>
        public bool ValidateApiKeyPermissions(string serviceName, string[] requiredPermissions)
        {
            try
            {
                lock (_lockObject)
                {
                    if (!_apiKeys.TryGetValue(serviceName, out var keyInfo))
                    {
                        _logger.LogWarning("Cannot validate permissions for service {ServiceName} - key not found", serviceName);
                        return false;
                    }

                    if (keyInfo.ExpiresAt < DateTime.UtcNow)
                    {
                        _logger.LogWarning("API key for service {ServiceName} has expired", serviceName);
                        return false;
                    }

                    foreach (var permission in requiredPermissions)
                    {
                        if (!Array.Exists(keyInfo.Permissions, p => p.Equals(permission, StringComparison.OrdinalIgnoreCase)))
                        {
                            _logger.LogWarning("API key for service {ServiceName} lacks required permission: {Permission}", 
                                serviceName, permission);
                            return false;
                        }
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating permissions for service {ServiceName}", serviceName);
                return false;
            }
        }

        /// <summary>
        /// Loads API keys from secure storage
        /// </summary>
        /// <returns>True if keys were loaded successfully, false otherwise</returns>
        public async Task<bool> LoadKeysFromStorageAsync()
        {
            try
            {
                if (!File.Exists(_keyStoragePath))
                {
                    _logger.LogInformation("No API key storage file found at {Path}", _keyStoragePath);
                    return false;
                }

                var encryptedData = await File.ReadAllTextAsync(_keyStoragePath);
                var decryptedJson = DecryptString(encryptedData);
                var loadedKeys = JsonSerializer.Deserialize<Dictionary<string, ApiKeyInfo>>(decryptedJson);

                if (loadedKeys != null)
                {
                    lock (_lockObject)
                    {
                        _apiKeys.Clear();
                        foreach (var key in loadedKeys)
                        {
                            _apiKeys[key.Key] = key.Value;
                        }
                    }
                    _logger.LogInformation("Loaded {Count} API keys from storage", loadedKeys.Count);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading API keys from storage");
                return false;
            }
        }

        /// <summary>
        /// Saves API keys to secure storage
        /// </summary>
        /// <returns>True if keys were saved successfully, false otherwise</returns>
        private async Task<bool> SaveKeysToStorageAsync()
        {
            try
            {
                Dictionary<string, ApiKeyInfo> keysToSave;
                lock (_lockObject)
                {
                    keysToSave = new Dictionary<string, ApiKeyInfo>(_apiKeys);
                }

                var json = JsonSerializer.Serialize(keysToSave);
                var encryptedData = EncryptString(json);
                
                // Ensure directory exists
                var directory = Path.GetDirectoryName(_keyStoragePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                await File.WriteAllTextAsync(_keyStoragePath, encryptedData);
                _logger.LogInformation("Saved {Count} API keys to storage", keysToSave.Count);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving API keys to storage");
                return false;
            }
        }

        /// <summary>
        /// Encrypts a string using AES encryption
        /// </summary>
        /// <param name="plainText">The text to encrypt</param>
        /// <returns>The encrypted string</returns>
        private string EncryptString(string plainText)
        {
            using var aes = Aes.Create();
            var key = SHA256.HashData(Encoding.UTF8.GetBytes(_encryptionKey));
            aes.Key = key;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            
            // Write the IV to the beginning of the stream
            ms.Write(aes.IV, 0, aes.IV.Length);
            
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
            }

            return Convert.ToBase64String(ms.ToArray());
        }

        /// <summary>
        /// Decrypts a string using AES encryption
        /// </summary>
        /// <param name="cipherText">The text to decrypt</param>
        /// <returns>The decrypted string</returns>
        private string DecryptString(string cipherText)
        {
            var cipherBytes = Convert.FromBase64String(cipherText);
            using var aes = Aes.Create();
            var key = SHA256.HashData(Encoding.UTF8.GetBytes(_encryptionKey));
            aes.Key = key;
            
            // Get the IV from the beginning of the cipher bytes
            var iv = new byte[aes.IV.Length];
            Array.Copy(cipherBytes, 0, iv, 0, iv.Length);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream(cipherBytes, iv.Length, cipherBytes.Length - iv.Length);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            
            return sr.ReadToEnd();
        }
    }

    /// <summary>
    /// Information about an API key
    /// </summary>
    public class ApiKeyInfo
    {
        /// <summary>
        /// The name of the service the key is for
        /// </summary>
        public string ServiceName { get; set; } = string.Empty;
        
        /// <summary>
        /// The encrypted API key
        /// </summary>
        public string EncryptedKey { get; set; } = string.Empty;
        
        /// <summary>
        /// The permissions associated with this key
        /// </summary>
        public string[] Permissions { get; set; } = Array.Empty<string>();
        
        /// <summary>
        /// When the key was created
        /// </summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// When the key was last rotated
        /// </summary>
        public DateTime LastRotatedAt { get; set; }
        
        /// <summary>
        /// When the key expires
        /// </summary>
        public DateTime ExpiresAt { get; set; }
    }
}
