using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MarketCharts.Client.Services.Security
{
    /// <summary>
    /// Service for data protection and security operations
    /// </summary>
    public class DataProtectionService
    {
        private readonly ILogger<DataProtectionService> _logger;
        private readonly string _encryptionKey;
        private readonly string _secureStoragePath;
        private readonly HashSet<string> _activeSessionIds = new HashSet<string>();
        private readonly object _lockObject = new object();
        private DateTime _lastActivityTime = DateTime.UtcNow;
        private readonly TimeSpan _inactivityTimeout = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Initializes a new instance of the DataProtectionService class
        /// </summary>
        /// <param name="logger">The logger</param>
        /// <param name="encryptionKey">The encryption key for securing data</param>
        /// <param name="secureStoragePath">The path to store encrypted data</param>
        public DataProtectionService(ILogger<DataProtectionService> logger, string encryptionKey, string secureStoragePath)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            if (string.IsNullOrEmpty(encryptionKey))
                throw new ArgumentException("Encryption key cannot be null or empty", nameof(encryptionKey));
            
            _encryptionKey = encryptionKey;
            _secureStoragePath = secureStoragePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "securedata");
            
            // Ensure the secure storage directory exists
            if (!Directory.Exists(_secureStoragePath))
            {
                Directory.CreateDirectory(_secureStoragePath);
            }
        }

        /// <summary>
        /// Sanitizes input data to prevent injection attacks
        /// </summary>
        /// <param name="input">The input to sanitize</param>
        /// <returns>The sanitized input</returns>
        public string SanitizeInput(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Remove potentially dangerous script tags
            var sanitized = Regex.Replace(input, @"<script[^>]*>.*?</script>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            // Remove other potentially dangerous HTML tags
            sanitized = Regex.Replace(sanitized, @"<[^>]*>", "", RegexOptions.IgnoreCase);
            
            // Remove SQL injection patterns
            sanitized = Regex.Replace(sanitized, @"(\b)(on\S+)(\s*)=|javascript:|(<\s*)\s*script|(<\s*)\s*iframe|(<\s*)\s*frame|document\.cookie|document\.write|window\.location|eval\(", 
                "", RegexOptions.IgnoreCase);
            
            return sanitized;
        }

        /// <summary>
        /// Validates the integrity of data loaded from storage
        /// </summary>
        /// <typeparam name="T">The type of data to validate</typeparam>
        /// <param name="data">The data to validate</param>
        /// <param name="validator">A function to validate the data</param>
        /// <returns>True if the data is valid, false otherwise</returns>
        public bool ValidateDataIntegrity<T>(T data, Func<T, bool> validator)
        {
            if (data == null)
            {
                _logger.LogWarning("Data validation failed: data is null");
                return false;
            }

            try
            {
                return validator(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating data integrity");
                return false;
            }
        }

        /// <summary>
        /// Securely stores data in local storage
        /// </summary>
        /// <typeparam name="T">The type of data to store</typeparam>
        /// <param name="key">The key to store the data under</param>
        /// <param name="data">The data to store</param>
        /// <returns>True if the data was stored successfully, false otherwise</returns>
        public async Task<bool> SecureStoreAsync<T>(string key, T data)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                {
                    _logger.LogWarning("Cannot store data with null or empty key");
                    return false;
                }

                var json = JsonSerializer.Serialize(data);
                var encryptedData = EncryptString(json);
                var filePath = Path.Combine(_secureStoragePath, $"{SanitizeFileName(key)}.dat");
                
                await File.WriteAllTextAsync(filePath, encryptedData);
                _logger.LogInformation("Data stored securely with key {Key}", key);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing data securely with key {Key}", key);
                return false;
            }
        }

        /// <summary>
        /// Retrieves securely stored data from local storage
        /// </summary>
        /// <typeparam name="T">The type of data to retrieve</typeparam>
        /// <param name="key">The key the data is stored under</param>
        /// <returns>The retrieved data, or default if not found</returns>
        public async Task<T?> SecureRetrieveAsync<T>(string key)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                {
                    _logger.LogWarning("Cannot retrieve data with null or empty key");
                    return default;
                }

                var filePath = Path.Combine(_secureStoragePath, $"{SanitizeFileName(key)}.dat");
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("No data found for key {Key}", key);
                    return default;
                }

                var encryptedData = await File.ReadAllTextAsync(filePath);
                var json = DecryptString(encryptedData);
                var data = JsonSerializer.Deserialize<T>(json);
                
                _logger.LogInformation("Data retrieved securely with key {Key}", key);
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving data securely with key {Key}", key);
                return default;
            }
        }

        /// <summary>
        /// Encrypts credentials for secure storage
        /// </summary>
        /// <param name="credentials">The credentials to encrypt</param>
        /// <returns>The encrypted credentials</returns>
        public string EncryptCredentials(string credentials)
        {
            if (string.IsNullOrEmpty(credentials))
                return string.Empty;

            return EncryptString(credentials);
        }

        /// <summary>
        /// Decrypts credentials from secure storage
        /// </summary>
        /// <param name="encryptedCredentials">The encrypted credentials</param>
        /// <returns>The decrypted credentials</returns>
        public string DecryptCredentials(string encryptedCredentials)
        {
            if (string.IsNullOrEmpty(encryptedCredentials))
                return string.Empty;

            return DecryptString(encryptedCredentials);
        }

        /// <summary>
        /// Creates a new session for the application
        /// </summary>
        /// <returns>The session ID</returns>
        public string CreateSession()
        {
            var sessionId = Guid.NewGuid().ToString();
            lock (_lockObject)
            {
                _activeSessionIds.Add(sessionId);
                _lastActivityTime = DateTime.UtcNow;
            }
            _logger.LogInformation("Created new session with ID {SessionId}", sessionId);
            return sessionId;
        }

        /// <summary>
        /// Validates a session ID
        /// </summary>
        /// <param name="sessionId">The session ID to validate</param>
        /// <returns>True if the session is valid, false otherwise</returns>
        public bool ValidateSession(string sessionId)
        {
            lock (_lockObject)
            {
                if (_activeSessionIds.Contains(sessionId))
                {
                    // Check for inactivity timeout
                    if (DateTime.UtcNow - _lastActivityTime > _inactivityTimeout)
                    {
                        _logger.LogInformation("Session {SessionId} expired due to inactivity", sessionId);
                        _activeSessionIds.Remove(sessionId);
                        return false;
                    }
                    
                    // Update last activity time
                    _lastActivityTime = DateTime.UtcNow;
                    return true;
                }
                
                _logger.LogWarning("Invalid session ID {SessionId}", sessionId);
                return false;
            }
        }

        /// <summary>
        /// Ends a session
        /// </summary>
        /// <param name="sessionId">The session ID to end</param>
        public void EndSession(string sessionId)
        {
            lock (_lockObject)
            {
                if (_activeSessionIds.Remove(sessionId))
                {
                    _logger.LogInformation("Ended session with ID {SessionId}", sessionId);
                }
                else
                {
                    _logger.LogWarning("Attempted to end non-existent session with ID {SessionId}", sessionId);
                }
            }
        }

        /// <summary>
        /// Checks if the application is inactive and should be locked
        /// </summary>
        /// <returns>True if the application should be locked, false otherwise</returns>
        public bool ShouldLockApplication()
        {
            lock (_lockObject)
            {
                return DateTime.UtcNow - _lastActivityTime > _inactivityTimeout;
            }
        }

        /// <summary>
        /// Records user activity to prevent inactivity timeout
        /// </summary>
        public void RecordActivity()
        {
            lock (_lockObject)
            {
                _lastActivityTime = DateTime.UtcNow;
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

        /// <summary>
        /// Sanitizes a filename to prevent path traversal attacks
        /// </summary>
        /// <param name="filename">The filename to sanitize</param>
        /// <returns>The sanitized filename</returns>
        private string SanitizeFileName(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return "unnamed";

            // Remove invalid characters
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(filename.Where(c => !invalidChars.Contains(c)).ToArray());
            
            // Prevent path traversal
            sanitized = sanitized.Replace(".", "_");
            
            return sanitized;
        }
    }
}
