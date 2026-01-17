using Npgsql;
using AdLibertataBot.Web.Data;
using AdLibertataBot.Web.Services.Core;

namespace AdLibertataBot.Web.Services.Company
{
    public class CompanyAdminService
    {
        private readonly DatabaseService _db;
        private readonly ILogger<CompanyAdminService> _logger;

        public CompanyAdminService(DatabaseService db, ILogger<CompanyAdminService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<AdminUser?> GetAdminByPasswordAsync(string password, int companyId)
        {
            try
            {
                await using var conn = await _db.CreateConnectionAsync();
                await using var cmd = new NpgsqlCommand(
                    "SELECT id, company_id, username, email, full_name, admin_password, is_active, created_at " +
                    "FROM admin_users WHERE admin_password = @password AND company_id = @company_id AND is_active = true", 
                    conn);
                
                cmd.Parameters.AddWithValue("password", password);
                cmd.Parameters.AddWithValue("company_id", companyId);
                
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new AdminUser(
                        reader.GetInt32(0),
                        reader.GetInt32(1),
                        reader.GetString(2),
                        reader.IsDBNull(3) ? "" : reader.GetString(3),
                        reader.GetString(4),
                        reader.GetString(5),
                        reader.GetBoolean(6),
                        reader.GetDateTime(7)
                    );
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting admin by password");
                return null;
            }
        }
    }
}