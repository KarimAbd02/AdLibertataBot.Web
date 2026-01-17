using Npgsql;
using AdLibertataBot.Web.Data;
using AdLibertataBot.Web.Services.Core;

namespace AdLibertataBot.Web.Services.Content
{
    public class BreathingTechniqueService
    {
        private readonly DatabaseService _db;
        private readonly ILogger<BreathingTechniqueService> _logger;

        public BreathingTechniqueService(DatabaseService db, ILogger<BreathingTechniqueService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<BreathingTechnique?> GetRandomTechniqueAsync()
        {
            try
            {
                await using var conn = await _db.CreateConnectionAsync();
                await using var cmd = new NpgsqlCommand(@"
                    SELECT id, name, description, instructions, duration_seconds, points_reward, is_active
                    FROM breathing_techniques 
                    WHERE is_active = true 
                    ORDER BY RANDOM() 
                    LIMIT 1", 
                    conn);
                
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new BreathingTechnique(
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetInt32(4),
                        reader.GetInt32(5),
                        reader.GetBoolean(6)
                    );
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting random breathing technique");
                return GetFallbackTechnique();
            }
        }

        private BreathingTechnique GetFallbackTechnique()
        {
            return new BreathingTechnique(
                1,
                "Дыхание 4-7-8",
                "Успокаивающая техника для снятия стресса",
                "1. Вдох через нос на 4 счета\n2. Задержка на 7 счетов\n3. Выдох через рот на 8 счетов\n4. Повторить 3-4 раза",
                60,
                15,
                true
            );
        }

        public async Task InitializeTechniquesAsync()
        {
            try
            {
                // Проверяем, есть ли уже техники
                await using var conn = await _db.CreateConnectionAsync();
                await using var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM breathing_techniques", conn);
                var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                
                if (count > 0)
                {
                    _logger.LogInformation($"Breathing techniques already exist: {count}");
                    return;
                }

                _logger.LogInformation("Initializing breathing techniques...");
                
                // Добавляем только одну базовую технику вместо множества
                var technique = new[]
                {
                    ("Дыхание 4-7-8", "Успокаивающая техника от доктора Вейля", 
                     "1. Вдох через нос на 4 счета\n2. Задержка на 7 счетов\n3. Выдох через рот на 8 счетов\n4. Повторить 3-4 раза", 60, 15)
                };

                foreach (var (name, desc, instructions, duration, points) in technique)
                {
                    await using var cmd = new NpgsqlCommand(@"
                        INSERT INTO breathing_techniques 
                        (name, description, instructions, duration_seconds, points_reward, is_active) 
                        VALUES (@name, @desc, @instructions, @duration, @points, true)
                        ON CONFLICT (name) DO NOTHING", 
                        conn);
                    
                    cmd.Parameters.AddWithValue("name", name);
                    cmd.Parameters.AddWithValue("desc", desc);
                    cmd.Parameters.AddWithValue("instructions", instructions);
                    cmd.Parameters.AddWithValue("duration", duration);
                    cmd.Parameters.AddWithValue("points", points);
                    
                    await cmd.ExecuteNonQueryAsync();
                }
                
                _logger.LogInformation("Breathing techniques initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize breathing techniques, using fallback");
            }
        }
    }
}