using System;

namespace MarketCharts.Client.Models.Configuration
{
    /// <summary>
    /// Configuration for the database
    /// </summary>
    public class DatabaseConfiguration
    {
        /// <summary>
        /// Connection string for the database
        /// </summary>
        public string ConnectionString { get; set; } = "Filename=marketdata.db";

        /// <summary>
        /// Maximum size of the database in megabytes before compaction
        /// </summary>
        public int MaxSizeMB { get; set; } = 100;

        /// <summary>
        /// Interval in days for database backup
        /// </summary>
        public int BackupIntervalDays { get; set; } = 7;

        /// <summary>
        /// Maximum number of concurrent connections
        /// </summary>
        public int MaxConnections { get; set; } = 10;

        /// <summary>
        /// Command timeout in seconds
        /// </summary>
        public int CommandTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Whether to enable query logging
        /// </summary>
        public bool EnableQueryLogging { get; set; } = false;

        /// <summary>
        /// Whether to verify data integrity on import
        /// </summary>
        public bool VerifyDataIntegrity { get; set; } = true;

        /// <summary>
        /// Whether to create the database if it doesn't exist
        /// </summary>
        public bool CreateIfNotExists { get; set; } = true;
    }
}
