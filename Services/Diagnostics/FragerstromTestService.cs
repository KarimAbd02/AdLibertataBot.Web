using Npgsql;
using AdLibertataBot.Web.Data;
using AdLibertataBot.Web.Services.Core;

namespace AdLibertataBot.Web.Services.Diagnostics
{
    public class FagerstromTestService
    {
        private readonly DatabaseService _db;

        public FagerstromTestService(DatabaseService db)
        {
            _db = db;
        }

        public string GetQuestionText(int questionNumber)
        {
            return questionNumber switch
            {
                1 => "📋 Тест Фагерстрема (Вопрос 1/6)\n\n" +
                     "Как скоро после пробуждения Вы выкуриваете первую сигарету?\n\n" +
                     "1️⃣ - Первые 5 минут\n" +
                     "2️⃣ - 6-30 минут\n" +
                     "3️⃣ - 30-60 минут\n" +
                     "4️⃣ - Через 1 час или позже",
                
                2 => "📋 Тест Фагерстрема (Вопрос 2/6)\n\n" +
                     "Сложно ли для Вас воздержаться от курения в местах, где курение запрещено?\n\n" +
                     "1️⃣ - Да\n" +
                     "2️⃣ - Нет",
                
                3 => "📋 Тест Фагерстрема (Вопрос 3/6)\n\n" +
                     "От какой сигареты Вы не можете легко отказаться?\n\n" +
                     "1️⃣ - Первая утром\n" +
                     "2️⃣ - Все остальные",
                
                4 => "📋 Тест Фагерстрема (Вопрос 4/6)\n\n" +
                     "Сколько сигарет Вы выкуриваете в день?\n\n" +
                     "1️⃣ - 10 и меньше\n" +
                     "2️⃣ - 11-20\n" +
                     "3️⃣ - 21-30\n" +
                     "4️⃣ - 31 и более",
                
                5 => "📋 Тест Фагерстрема (Вопрос 5/6)\n\n" +
                     "Вы курите более часто в первые часы утром, после того как проснетесь?\n\n" +
                     "1️⃣ - Да\n" +
                     "2️⃣ - Нет",
                
                6 => "📋 Тест Фагерстрема (Вопрос 6/6)\n\n" +
                     "Курите ли Вы, если сильно больны и вынуждены находиться в кровати целый день?\n\n" +
                     "1️⃣ - Да\n" +
                     "2️⃣ - Нет",
                
                _ => "Неизвестный вопрос"
            };
        }

        public int CalculateQuestionScore(int questionNumber, int answerOption)
        {
            return (questionNumber, answerOption) switch
            {
                (1, 1) => 3,  // Первые 5 мин
                (1, 2) => 2,  // 6-30 мин
                (1, 3) => 1,  // 30-60 мин
                (1, 4) => 0,  // Через 1 час
                
                (2, 1) => 1,  // Да, сложно
                (2, 2) => 0,  // Нет
                
                (3, 1) => 1,  // Первая утром
                (3, 2) => 0,  // Остальные
                
                (4, 1) => 0,  // 10 и меньше
                (4, 2) => 1,  // 11-20
                (4, 3) => 2,  // 21-30
                (4, 4) => 3,  // 31 и более
                
                (5, 1) => 1,  // Да, чаще утром
                (5, 2) => 0,  // Нет
                
                (6, 1) => 1,  // Да, курю когда болею
                (6, 2) => 0,  // Нет
                
                _ => 0
            };
        }

        public (DependencyLevel level, string description) EvaluateScore(int totalScore)
        {
            return totalScore switch
            {
                <= 2 => (DependencyLevel.VeryWeak, "Очень слабая зависимость"),
                <= 4 => (DependencyLevel.Weak, "Слабая зависимость"),
                5 => (DependencyLevel.Medium, "Средняя зависимость"),
                <= 7 => (DependencyLevel.High, "Высокая зависимость"),
                _ => (DependencyLevel.VeryHigh, "Очень высокая зависимость")
            };
        }

        public async Task SaveTestResultAsync(FagerstromTestResult result)
        {
            await using var conn = await _db.CreateConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO fagerstrom_tests 
                (user_id, total_score, dependency_level, answers, completed_at) 
                VALUES (@user_id, @score, @level, @answers::jsonb, @completed_at)",
                conn);
            
            cmd.Parameters.AddWithValue("user_id", result.UserId);
            cmd.Parameters.AddWithValue("score", result.TotalScore);
            cmd.Parameters.AddWithValue("level", (int)result.DependencyLevel);
            cmd.Parameters.AddWithValue("answers", System.Text.Json.JsonSerializer.Serialize(result.Answers));
            cmd.Parameters.AddWithValue("completed_at", result.CompletedAt);
            
            await cmd.ExecuteNonQueryAsync();
        }

        public string GetRecommendationByLevel(DependencyLevel level)
        {
            return level switch
            {
                DependencyLevel.VeryWeak => 
                    "У вас очень слабая зависимость! Вам будет легче контролировать перекуры и заменять их альтернативами.",
                
                DependencyLevel.Weak => 
                    "У вас слабая зависимость. Фокус на осознанности и альтернативах поможет вам легко достичь целей.",
                
                DependencyLevel.Medium => 
                    "Средняя зависимость. Вам помогут регулярные техники дыхания и постепенное увеличение интервалов.",
                
                DependencyLevel.High => 
                    "Высокая зависимость. Рекомендуем начать с малого: откладывать перекур на 2-5 минут и использовать альтернативы.",
                
                DependencyLevel.VeryHigh => 
                    "Очень высокая зависимость. Будьте терпеливы к себе. Каждый маленький шаг важен. Мы поддержим вас на этом пути.",
                
                _ => "Начните свой путь к осознанности и здоровью!"
            };
        }
    }
}