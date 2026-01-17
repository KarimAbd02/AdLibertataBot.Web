using Npgsql;
using Microsoft.Extensions.Logging;
using AdLibertataBot.Web;
using System.Collections.Concurrent;

namespace AdLibertataBot.Web.Services.Core
{
    public class DatabaseService
    {
        private readonly string _connectionString;
        private readonly ILogger<DatabaseService> _logger;
        private readonly ConcurrentQueue<NpgsqlConnection> _connectionPool = new();
        private readonly SemaphoreSlim _poolSemaphore = new(5, 5);
        private bool _disposed = false;

        public DatabaseService(BotConfig config, ILogger<DatabaseService> logger)
        {
            var baseConnection = config.SupabaseConnection 
                                ?? throw new ArgumentNullException(nameof(config.SupabaseConnection));
            
            // Добавляем оптимизации для Supabase
            _connectionString = baseConnection + 
                ";Pooling=true;" +
                "Minimum Pool Size=1;" +
                "Maximum Pool Size=20;" +
                "Connection Idle Lifetime=60;" +
                "Connection Pruning Interval=5;" +
                "Timeout=60;" +
                "Command Timeout=60;" +
                "Keepalive=60;" +
                "Tcp Keepalive=true;" +
                "No Reset On Close=true;";
            
            _logger = logger;
            _logger.LogInformation("Database service initialized for Supabase");
        }

        public async Task<NpgsqlConnection> CreateConnectionAsync()
        {
            try
            {
                _logger.LogDebug("Creating connection to Supabase");
                
                var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                
                // Быстрая проверка подключения
                await using var testCmd = new NpgsqlCommand("SELECT 1", connection);
                await testCmd.ExecuteScalarAsync();
                
                _logger.LogDebug("✅ Database connection created successfully");
                return connection;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to create database connection");
                
                // Дополнительная диагностика (без пароля)
                var safeConnectionString = _connectionString.Replace(
                    "Password=ad_libertata", 
                    "Password=***"
                );
                _logger.LogWarning($"Connection string: {safeConnectionString}");
                
                throw;
            }
        }

        // ⭐ ДОБАВЛЕН ЭТОТ МЕТОД ⭐
        public async Task<T> ExecuteWithConnectionAsync<T>(Func<NpgsqlConnection, Task<T>> operation)
        {
            var connection = await CreateConnectionAsync();
            try
            {
                return await operation(connection);
            }
            finally
            {
                await connection.CloseAsync();
                await connection.DisposeAsync();
            }
        }

        // ⭐ ДОБАВЛЕН ЭТОТ МЕТОД ⭐
        public async Task ExecuteWithConnectionAsync(Func<NpgsqlConnection, Task> operation)
        {
            var connection = await CreateConnectionAsync();
            try
            {
                await operation(connection);
            }
            finally
            {
                await connection.CloseAsync();
                await connection.DisposeAsync();
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                await using var conn = await CreateConnectionAsync();
                await using var cmd = new NpgsqlCommand("SELECT version()", conn);
                var version = await cmd.ExecuteScalarAsync();
                _logger.LogInformation($"✅ PostgreSQL version: {version}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Connection test failed");
                return false;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                while (_connectionPool.TryDequeue(out var connection))
                {
                    connection?.Dispose();
                }
                _poolSemaphore.Dispose();
                _logger.LogInformation("Database service disposed");
            }
        }
    }
}