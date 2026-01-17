using Npgsql;
using AdLibertataBot.Web.Data;
using Microsoft.Extensions.Logging;
using AdLibertataBot.Web.Services.Core;

namespace AdLibertataBot.Web.Services.Company
{
    public class CompanyReportService
    {
        private readonly DatabaseService _db;
        private readonly ILogger<CompanyReportService> _logger;

        public CompanyReportService(DatabaseService db, ILogger<CompanyReportService> logger)
        {
            _db = db;
            _logger = logger;
        }

        // Добавляем этот метод
        public async Task<int> GetCompanyUserCountAsync(int companyId)
        {
            try
            {
                await using var conn = await _db.CreateConnectionAsync();
                await using var cmd = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM users WHERE company_id = @company_id AND is_active = true", 
                    conn);
                
                cmd.Parameters.AddWithValue("company_id", companyId);
                
                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting company user count");
                return 0;
            }
        }

        // Остальные методы остаются как были...
        public async Task<string> GenerateCompanyReportAsync(int companyId)
        {
            try
            {
                await using var conn = await _db.CreateConnectionAsync();
                await using var cmd = new NpgsqlCommand(@"
                    SELECT 
                        c.name as company_name,
                        COUNT(DISTINCT u.id) as active_users,
                        COALESCE(AVG(daily_smokes), 0) as avg_daily_smokes,
                        COALESCE(AVG(daily_alternatives), 0) as avg_daily_alternatives
                    FROM companies c
                    LEFT JOIN users u ON u.company_id = c.id AND u.is_active = true
                    LEFT JOIN (
                        SELECT user_id, COUNT(*) as daily_smokes
                        FROM smoke_events 
                        WHERE event_time >= CURRENT_DATE - INTERVAL '7 days'
                        GROUP BY user_id
                    ) se ON u.id = se.user_id
                    LEFT JOIN (
                        SELECT user_id, COUNT(*) as daily_alternatives
                        FROM alternative_events 
                        WHERE event_time >= CURRENT_DATE - INTERVAL '7 days'
                        GROUP BY user_id
                    ) ae ON u.id = ae.user_id
                    WHERE c.id = @company_id
                    GROUP BY c.id, c.name", 
                    conn);
                
                cmd.Parameters.AddWithValue("company_id", companyId);
                
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var companyName = reader.GetString(0);
                    var activeUsers = reader.GetInt32(1);
                    var avgDailySmokes = reader.GetDouble(2);
                    var avgDailyAlternatives = reader.GetDouble(3);

                    var wellnessScore = (avgDailySmokes + avgDailyAlternatives) > 0 
                        ? Math.Round(avgDailyAlternatives * 100.0 / (avgDailySmokes + avgDailyAlternatives), 1)
                        : 0;

                    return $"🏢 Отчёт компании: {companyName}\n\n" +
                           $"👥 Активных сотрудников: {activeUsers}\n" +
                           $"📊 Wellness-индекс: {wellnessScore}%\n\n" +
                           $"📈 Средние показатели за 7 дней:\n" +
                           $"🚬 Перекуров в день: {avgDailySmokes:F1}\n" +
                           $"🌿 Альтернатив в день: {avgDailyAlternatives:F1}\n\n" +
                           $"📅 Отчёт сгенерирован: {DateTime.Now:dd.MM.yyyy HH:mm}";
                }
                
                return "❌ Не удалось сгенерировать отчёт для компании";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating company report");
                return "❌ Ошибка при генерации отчёта";
            }
        }

        public async Task<string> GetTopUsersAsync(int companyId, int topCount = 5)
        {
            try
            {
                await using var conn = await _db.CreateConnectionAsync();
                await using var cmd = new NpgsqlCommand(@"
                    SELECT 
                        u.level,
                        u.points,
                        u.total_smokes,
                        u.total_alternatives,
                        u.current_streak
                    FROM users u
                    WHERE u.company_id = @company_id AND u.is_active = true
                    ORDER BY u.points DESC
                    LIMIT @limit", 
                    conn);
                
                cmd.Parameters.AddWithValue("company_id", companyId);
                cmd.Parameters.AddWithValue("limit", topCount);
                
                var result = "🏆 Топ сотрудников по очкам:\n\n";
                var rank = 1;
                
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var level = reader.GetInt32(0);
                    var points = reader.GetInt32(1);
                    var totalSmokes = reader.GetInt32(2);
                    var totalAlternatives = reader.GetInt32(3);
                    var currentStreak = reader.GetInt32(4);
                    
                    result += $"{rank}. ⭐ {points} очков | 🎯 Ур. {level}\n";
                    result += $"   🚬 {totalSmokes} | 🌿 {totalAlternatives} | 🔥 {currentStreak} дн.\n\n";
                    rank++;
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top users");
                return "❌ Ошибка при получении топа пользователей";
            }
        }
    }
}