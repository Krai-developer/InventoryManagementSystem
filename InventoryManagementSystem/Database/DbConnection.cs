using System;
using System.IO;
using Microsoft.Data.SqlClient;

namespace InventoryManagementSystem.Database
{
    /// <summary>
    /// Manages SQL Server connection strings and provides SqlConnection instances for ADO.NET operations.
    /// Automatically detects available SQL Server instances (LocalDB, SQLEXPRESS, or localhost)
    /// and stores the database file locally if needed.
    /// </summary>
    public static class DbConnection
    {
        public const string DatabaseName = "InventoryManagementDB";

        private static string? _resolvedServer;
        private static string? _connectionString;

        /// <summary>
        /// Candidate SQL Server instances to probe on startup.
        /// </summary>
        private static readonly string[] ServerCandidates = new[]
        {
            @"(localdb)\MSSQLLocalDB",
            @".\SQLEXPRESS",
            @"localhost\SQLEXPRESS",
            @"localhost",
            @"(local)"
        };

        /// <summary>
        /// Gets the active SQL Server instance name.
        /// </summary>
        public static string ActiveServer => _resolvedServer ?? ServerCandidates[0];

        /// <summary>
        /// Returns the master database connection string used by DatabaseInitializer
        /// to verify or create the InventoryManagementDB database.
        /// </summary>
        public static string GetMasterConnectionString()
        {
            EnsureServerResolved();
            return $"Server={_resolvedServer};Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5;";
        }

        /// <summary>
        /// Returns the application connection string pointing to InventoryManagementDB.
        /// </summary>
        public static string GetConnectionString()
        {
            EnsureServerResolved();
            if (!string.IsNullOrEmpty(_connectionString))
            {
                return _connectionString;
            }

            _connectionString = $"Server={_resolvedServer};Database={DatabaseName};Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10;";
            return _connectionString;
        }

        /// <summary>
        /// Creates and returns a new SqlConnection for InventoryManagementDB.
        /// Caller is responsible for wrapping in a 'using' block and calling Open().
        /// </summary>
        public static SqlConnection GetConnection()
        {
            return new SqlConnection(GetConnectionString());
        }

        /// <summary>
        /// Allows overriding the SQL Server instance name (e.g., if user specifies a custom server).
        /// </summary>
        public static void SetCustomServer(string serverName)
        {
            _resolvedServer = serverName.Trim();
            _connectionString = null;
        }

        private static void EnsureServerResolved()
        {
            if (!string.IsNullOrEmpty(_resolvedServer))
                return;

            foreach (string candidate in ServerCandidates)
            {
                try
                {
                    string testConnStr = $"Server={candidate};Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=3;";
                    using (SqlConnection conn = new SqlConnection(testConnStr))
                    {
                        conn.Open();
                        _resolvedServer = candidate;
                        return;
                    }
                }
                catch
                {
                    // Try next candidate
                }
            }

            // Default fallback
            _resolvedServer = ServerCandidates[0];
        }
    }
}
