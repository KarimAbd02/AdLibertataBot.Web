using Npgsql;
using AdLibertataBot.Web.Data;
using AdLibertataBot.Web.Services.Core;

namespace AdLibertataBot.Web.Services.Content
{
    public class FactService
    {
        private readonly DatabaseService _db;
        private readonly ILogger<FactService> _logger;

        public FactService(DatabaseService db, ILogger<FactService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<Fact?> GetRandomUnviewedFactAsync(int userId)
        {
            await using var conn = await _db.CreateConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT f.id, f.title, f.content, f.category, f.reading_time_minutes, f.is_active
                FROM facts f
                WHERE f.is_active = true
                AND f.id NOT IN (
                    SELECT fact_id FROM user_fact_views WHERE user_id = @user_id
                )
                ORDER BY RANDOM()
                LIMIT 1", 
                conn);
            
            cmd.Parameters.AddWithValue("user_id", userId);
            
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Fact(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetInt32(4),
                    reader.GetBoolean(5)
                );
            }
            
            // Если все просмотрены - сбрасываем историю
            return await GetRandomFactAsync();
        }

        public async Task<Fact?> GetRandomFactAsync()
        {
            await using var conn = await _db.CreateConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT id, title, content, category, reading_time_minutes, is_active
                FROM facts
                WHERE is_active = true
                ORDER BY RANDOM()
                LIMIT 1", 
                conn);
            
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Fact(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetInt32(4),
                    reader.GetBoolean(5)
                );
            }
            return null;
        }

        public async Task MarkFactAsViewedAsync(int userId, int factId)
        {
            await using var conn = await _db.CreateConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO user_fact_views (user_id, fact_id, viewed_at)
                VALUES (@user_id, @fact_id, NOW())
                ON CONFLICT (user_id, fact_id) DO NOTHING", 
                conn);
            
            cmd.Parameters.AddWithValue("user_id", userId);
            cmd.Parameters.AddWithValue("fact_id", factId);
            
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task InitializeFactsAsync()
        {
            // Проверяем, есть ли уже факты
            await using var conn = await _db.CreateConnectionAsync();
            await using var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM facts", conn);
            var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
            
            if (count > 0)
            {
                _logger.LogInformation($"Facts already initialized: {count}");
                return;
            }

            _logger.LogInformation("Initializing facts...");
            // Здесь можно добавить начальные факты
        }

        public async Task<bool> ShouldShowFactAsync(int userId)
        {
            // Показываем факт раз в 2 дня
            await using var conn = await _db.CreateConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT viewed_at 
                FROM user_fact_views 
                WHERE user_id = @user_id 
                ORDER BY viewed_at DESC 
                LIMIT 1", 
                conn);
            
            cmd.Parameters.AddWithValue("user_id", userId);
            
            var result = await cmd.ExecuteScalarAsync();
            if (result == null) return true;
            
            var lastViewed = (DateTime)result;
            return (DateTime.UtcNow - lastViewed).TotalDays >= 2;
        }
    }
}