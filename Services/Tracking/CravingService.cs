using Npgsql;
using AdLibertataBot.Web.Data;
using AdLibertataBot.Web.Services.Core;

namespace AdLibertataBot.Web.Services.Tracking
{
    public class CravingService
    {
        private readonly DatabaseService _db;

        public CravingService(DatabaseService db)
        {
            _db = db;
        }

        public async Task AddCravingEventAsync(int userId, int postponedMinutes = 0)
        {
            await using var conn = await _db.CreateConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "INSERT INTO craving_events (user_id, event_time, postponed_minutes) VALUES (@user_id, @event_time, @postponed)", 
                conn);
            
            cmd.Parameters.AddWithValue("user_id", userId);
            cmd.Parameters.AddWithValue("event_time", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("postponed", postponedMinutes);
            
            await cmd.ExecuteNonQueryAsync();
        }
    }
}