using Npgsql;
using Microsoft.Extensions.Logging;
using AdLibertataBot.Web.Services.Core;

namespace AdLibertataBot.Web.Services.Company
{
    public class CompanyService
    {
        private readonly DatabaseService _db;
        private readonly ILogger<CompanyService> _logger;

        public CompanyService(DatabaseService db, ILogger<CompanyService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<AdLibertataBot.Web.Data.Company?> GetCompanyByCodeAsync(string code)
        {
            try
            {
                await using var conn = await _db.CreateConnectionAsync();
                await using var cmd = new NpgsqlCommand(
                    "SELECT id, code, name, contact_email, max_users, is_active, created_at " +
                    "FROM companies WHERE code = @code AND is_active = true", 
                    conn);
                
                cmd.Parameters.AddWithValue("code", code.ToUpper());
                
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new AdLibertataBot.Web.Data.Company(
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetInt32(4),
                        reader.GetBoolean(5),
                        reader.GetDateTime(6)
                    );
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting company by code: {code}");
                return null;
            }
        }

        public async Task<bool> CanAddUserToCompanyAsync(int companyId)
        {
            try
            {
                await using var conn = await _db.CreateConnectionAsync();
                
                // Получаем компанию
                await using var companyCmd = new NpgsqlCommand(
                    "SELECT max_users FROM companies WHERE id = @id", 
                    conn);
                companyCmd.Parameters.AddWithValue("id", companyId);
                
                var maxUsers = await companyCmd.ExecuteScalarAsync();
                if (maxUsers == null) return false;
                
                // Считаем пользователей
                await using var countCmd = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM users WHERE company_id = @company_id AND is_active = true", 
                    conn);
                countCmd.Parameters.AddWithValue("company_id", companyId);
                
                var userCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                return userCount < Convert.ToInt32(maxUsers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking company capacity for {companyId}");
                return false;
            }
        }

        public async Task<AdLibertataBot.Web.Data.Company?> GetCompanyById(int companyId)
        {
            await using var conn = await _db.CreateConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT id, code, name, contact_email, max_users, is_active, created_at " +
                "FROM companies WHERE id = @id", 
                conn);
            
            cmd.Parameters.AddWithValue("id", companyId);
            
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new AdLibertataBot.Web.Data.Company(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetInt32(4),
                    reader.GetBoolean(5),
                    reader.GetDateTime(6)
                );
            }
            return null;
        }
    }
}