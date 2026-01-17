using Npgsql;
using AdLibertataBot.Web.Data;
using AdLibertataBot.Web.Services.Core;

namespace AdLibertataBot.Web.Services.Diagnostics
{
    public class DiagnosticService
    {
        private readonly DatabaseService _db;

        public DiagnosticService(DatabaseService db)
        {
            _db = db;
        }

        public async Task SaveUserDiagnosticAsync(UserDiagnostic diagnostic)
        {
            await using var conn = await _db.CreateConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO user_diagnostics (user_id, smoking_experience, cigarettes_per_day, goal_type, created_at) 
                  VALUES (@user_id, @smoking_exp, @cigarettes, @goal_type, @created_at)",
                conn);
            
            cmd.Parameters.AddWithValue("user_id", diagnostic.UserId);
            cmd.Parameters.AddWithValue("smoking_exp", (int)diagnostic.SmokingExperience);
            cmd.Parameters.AddWithValue("cigarettes", (int)diagnostic.CigarettesPerDay);
            cmd.Parameters.AddWithValue("goal_type", (int)diagnostic.GoalType);
            cmd.Parameters.AddWithValue("created_at", diagnostic.CreatedAt);
            
            await cmd.ExecuteNonQueryAsync();
        }

        public string GetSmokingExperienceText(SmokingExperience experience)
        {
            return experience switch
            {
                SmokingExperience.LessThanYear => "Меньше года",
                SmokingExperience.OneToThreeYears => "1-3 года",
                SmokingExperience.ThreeToFiveYears => "3-5 лет",
                SmokingExperience.FiveToTenYears => "5-10 лет",
                SmokingExperience.MoreThanTenYears => "Более 10 лет",
                _ => "Не указано"
            };
        }

        public string GetCigarettesPerDayText(CigarettesPerDay cigarettes)
        {
            return cigarettes switch
            {
                CigarettesPerDay.LessThanFive => "Меньше 5 сигарет",
                CigarettesPerDay.FiveToTen => "5-10 сигарет",
                CigarettesPerDay.TenToFifteen => "10-15 сигарет",
                CigarettesPerDay.FifteenToTwenty => "15-20 сигарет",
                CigarettesPerDay.MoreThanTwenty => "Более 20 сигарет",
                _ => "Не указано"
            };
        }
    }
}