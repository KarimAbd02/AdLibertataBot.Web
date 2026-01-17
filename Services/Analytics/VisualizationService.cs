using AdLibertataBot.Web.Data;

namespace AdLibertataBot.Web.Services.Analytics
{
    public class VisualizationService
    {
        public string GenerateProgressBar(int current, int total, int length = 10)
        {
            if (total == 0) return $"[{new string('░', length)}] 0%";
            
            var percentage = (int)Math.Round((current / (double)total) * 100);
            var filled = (int)Math.Round(length * (percentage / 100.0));
            var empty = length - filled;
            return $"[{new string('█', filled)}{new string('░', empty)}] {percentage}%";
        }

        public string GenerateDailyComparison(int today, int yesterday)
        {
            if (yesterday == 0) return "📊 Нет данных за вчера";
            
            var difference = yesterday - today;
            var percentage = (int)Math.Round((difference / (double)yesterday) * 100);
            
            if (difference > 0)
                return $"📉 На {difference} меньше чем вчера ({percentage}% улучшение)";
            else if (difference < 0)
                return $"📈 На {Math.Abs(difference)} больше чем вчера";
            else
                return "➡️ Столько же сколько вчера";
        }

        public string GenerateWeeklyStats(int currentWeek, int previousWeek)
        {
            if (previousWeek == 0) return "📊 Первая неделя использования";
            
            var difference = previousWeek - currentWeek;
            var percentage = (int)Math.Round((difference / (double)previousWeek) * 100);
            
            if (difference > 0)
                return $"📉 На {difference} меньше чем на прошлой неделе ({percentage}% улучшение)";
            else if (difference < 0)
                return $"📈 На {Math.Abs(difference)} больше чем на прошлой неделе";
            else
                return "➡️ Столько же сколько на прошлой неделе";
        }
    }
}