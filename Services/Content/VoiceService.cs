using Npgsql;
using AdLibertataBot.Web.Data;
using AdLibertataBot.Web.Services.Core;

namespace AdLibertataBot.Web.Services.Content
{
    public class VoiceService
    {
        private readonly DatabaseService _db;

        public VoiceService(DatabaseService db)
        {
            _db = db;
        }

        public async Task<string> GetRandomMotivationAsync()
        {
            await using var conn = await _db.CreateConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT message FROM motivational_messages WHERE is_active = true ORDER BY RANDOM() LIMIT 1", 
                conn);
            
            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString() ?? "Вы делаете отличные успехи! Продолжайте в том же духе! 💪";
        }
    }
}