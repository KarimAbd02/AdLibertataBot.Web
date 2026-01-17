using Npgsql;
using AdLibertataBot.Web.Data;
using AdLibertataBot.Web.Services.Core;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AdLibertataBot.Web.Services.User
{
    public class UserStateService
    {
        private readonly DatabaseService _db;
        private readonly ILogger<UserStateService> _logger;

        public UserStateService(DatabaseService db, ILogger<UserStateService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task SaveUserStateAsync(string chatId, OnboardingStep step, int? companyId = null, string? companyCode = null)
        {
            try
            {
                var state = new UserState
                {
                    ChatId = chatId,
                    Step = step,
                    CompanyId = companyId,
                    CompanyCode = companyCode,
                    LastUpdated = DateTime.UtcNow,
                    FagerstromAnswers = new Dictionary<int, int>(),
                    CurrentFagerstromQuestion = 1
                };

                await SaveFullStateAsync(state);
                _logger.LogInformation($"Saved state for {chatId}: Step={step}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving user state for {chatId}");
                throw;
            }
        }

        public async Task SaveFullStateAsync(UserState state)
        {
            NpgsqlConnection? connection = null;
            try
            {
                connection = await _db.CreateConnectionAsync();
                
                // Сериализуем ответы в JSON
                string? answersJson = null;
                if (state.FagerstromAnswers != null && state.FagerstromAnswers.Count > 0)
                {
                    answersJson = JsonSerializer.Serialize(state.FagerstromAnswers);
                }

                await using var cmd = new NpgsqlCommand(@"
                    INSERT INTO user_states 
                    (chat_id, step, role, company_id, company_code, fagerstrom_answers, current_question, 
                     smoking_experience, cigarettes_per_day, last_updated) 
                    VALUES (@chat_id, @step, @role, @company_id, @company_code, @answers::jsonb, @current_q,
                            @smoking_exp, @cigarettes, @last_updated)
                    ON CONFLICT (chat_id) 
                    DO UPDATE SET 
                        step = EXCLUDED.step, 
                        role = EXCLUDED.role,
                        company_id = EXCLUDED.company_id, 
                        company_code = EXCLUDED.company_code,
                        fagerstrom_answers = EXCLUDED.fagerstrom_answers,
                        current_question = EXCLUDED.current_question,
                        smoking_experience = EXCLUDED.smoking_experience,
                        cigarettes_per_day = EXCLUDED.cigarettes_per_day,
                        last_updated = EXCLUDED.last_updated",
                    connection);
                
                cmd.Parameters.AddWithValue("chat_id", state.ChatId);
                cmd.Parameters.AddWithValue("step", (int)state.Step);
                cmd.Parameters.AddWithValue("role", state.Role.HasValue ? (int)state.Role.Value : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("company_id", state.CompanyId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("company_code", state.CompanyCode ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("answers", answersJson ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("current_q", state.CurrentFagerstromQuestion);
                cmd.Parameters.AddWithValue("smoking_exp", state.SmokingExperience.HasValue ? (int)state.SmokingExperience.Value : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("cigarettes", state.CigarettesPerDay.HasValue ? (int)state.CigarettesPerDay.Value : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("last_updated", DateTime.UtcNow);
                
                await cmd.ExecuteNonQueryAsync();
                _logger.LogDebug($"Saved full state to DB for {state.ChatId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to save user state to DB for {state.ChatId}");
                throw;
            }
            finally
            {
                if (connection != null)
                {
                    await connection.CloseAsync();
                    await connection.DisposeAsync();
                }
            }
        }

        public async Task<UserState?> GetUserStateAsync(string chatId)
        {
            NpgsqlConnection? connection = null;
            try
            {
                connection = await _db.CreateConnectionAsync();
                
                await using var cmd = new NpgsqlCommand(
                    "SELECT chat_id, step, role, company_id, company_code, fagerstrom_answers, " +
                    "current_question, smoking_experience, cigarettes_per_day, last_updated " +
                    "FROM user_states WHERE chat_id = @chat_id", 
                    connection);
                
                cmd.Parameters.AddWithValue("chat_id", chatId);
                
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var state = new UserState
                    {
                        ChatId = reader.GetString(0),
                        Step = (OnboardingStep)reader.GetInt32(1),
                        LastUpdated = reader.GetDateTime(9),
                        CurrentFagerstromQuestion = reader.IsDBNull(6) ? 1 : reader.GetInt32(6)
                    };

                    // Загружаем роль
                    if (!reader.IsDBNull(2))
                    {
                        state.Role = (UserRole)reader.GetInt32(2);
                    }

                    // Загружаем company_id и company_code
                    state.CompanyId = reader.IsDBNull(3) ? null : reader.GetInt32(3);
                    state.CompanyCode = reader.IsDBNull(4) ? null : reader.GetString(4);

                    // Десериализуем ответы если они есть
                    if (!reader.IsDBNull(5))
                    {
                        var answersJson = reader.GetString(5);
                        if (!string.IsNullOrEmpty(answersJson))
                        {
                            try
                            {
                                state.FagerstromAnswers = JsonSerializer.Deserialize<Dictionary<int, int>>(answersJson)
                                    ?? new Dictionary<int, int>();
                            }
                            catch (JsonException)
                            {
                                state.FagerstromAnswers = new Dictionary<int, int>();
                            }
                        }
                    }
                    else
                    {
                        state.FagerstromAnswers = new Dictionary<int, int>();
                    }

                    // Загружаем smoking experience
                    if (!reader.IsDBNull(7))
                    {
                        state.SmokingExperience = (SmokingExperience)reader.GetInt32(7);
                    }

                    // Загружаем cigarettes per day
                    if (!reader.IsDBNull(8))
                    {
                        state.CigarettesPerDay = (CigarettesPerDay)reader.GetInt32(8);
                    }
                    
                    _logger.LogDebug($"Loaded state from DB for {chatId}: Step={state.Step}, Role={state.Role}, CurrentQ={state.CurrentFagerstromQuestion}");
                    return state;
                }
                
                _logger.LogDebug($"No state found in DB for {chatId}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading user state from DB for {chatId}");
                return null;
            }
            finally
            {
                if (connection != null)
                {
                    await connection.CloseAsync();
                    await connection.DisposeAsync();
                }
            }
        }

        public async Task DeleteUserStateAsync(string chatId)
        {
            NpgsqlConnection? connection = null;
            try
            {
                connection = await _db.CreateConnectionAsync();
                
                await using var cmd = new NpgsqlCommand(
                    "DELETE FROM user_states WHERE chat_id = @chat_id", 
                    connection);
                
                cmd.Parameters.AddWithValue("chat_id", chatId);
                var rowsDeleted = await cmd.ExecuteNonQueryAsync();
                _logger.LogInformation($"Deleted state for {chatId}, rows affected: {rowsDeleted}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting user state for {chatId}");
                throw;
            }
            finally
            {
                if (connection != null)
                {
                    await connection.CloseAsync();
                    await connection.DisposeAsync();
                }
            }
        }

        public async Task<bool> HasUserStateAsync(string chatId)
        {
            NpgsqlConnection? connection = null;
            try
            {
                connection = await _db.CreateConnectionAsync();
                
                await using var cmd = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM user_states WHERE chat_id = @chat_id", 
                    connection);
                
                cmd.Parameters.AddWithValue("chat_id", chatId);
                var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking user state for {chatId}");
                return false;
            }
            finally
            {
                if (connection != null)
                {
                    await connection.CloseAsync();
                    await connection.DisposeAsync();
                }
            }
        }
    }
}