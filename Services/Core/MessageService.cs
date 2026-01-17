using Npgsql;
using System.Collections.Concurrent;
using AdLibertataBot.Web.Services.Core;

namespace AdLibertataBot.Web.Services.Core
{
    public class MessageService
    {
        private readonly DatabaseService _db;
        private readonly ILogger<MessageService> _logger;
        private readonly ConcurrentDictionary<string, string> _cache = new();
        private bool _cacheInitialized = false;

        public MessageService(DatabaseService db, ILogger<MessageService> logger)
        {
            _db = db;
            _logger = logger;
            
            // Инициализируем кэш с fallback сообщениями
            _cache["onboarding_welcome"] = "👋 Добро пожаловать в Ad Libertata Work!\n\n📝 Введите ваш корпоративный код:";
            _cache["error_generic"] = "❌ Произошла ошибка. Пожалуйста, попробуйте позже.";
            _cache["error_use_buttons"] = "Пожалуйста, используйте кнопки меню 👇";
            _cache["main_menu"] = "Выберите действие:";
            _cache["craving_detected"] = "🧘 Я чувствую, что вы хотите курить. Давайте вместе справимся с этим!";
            _cache["alternative_menu"] = "🌿 Выберите здоровую альтернативу:";
            _cache["smoke_recorded"] = "🚬 Записано. {motivation}";
            _cache["stats_header"] = "📊 Ваша статистика";
            _cache["progress_header"] = "🎯 Ваш прогресс";
            _cache["logout_success"] = "✅ Вы вышли из системы.";
            _cache["onboarding_company_not_found"] = "❌ Компания не найдена. Проверьте код.";
            _cache["onboarding_company_found"] = "✅ Добро пожаловать в {company_name}!";
            _cache["fagerstrom_result"] = "📋 Результат теста Фагерстрема\n\nВаш балл: {score}\nУровень: {level}";
            _cache["onboarding_complete"] = "🎉 Регистрация завершена! {goal}";
            
            _logger.LogInformation("MessageService initialized with fallback messages");
        }

        public async Task InitializeCacheAsync()
        {
            if (_cacheInitialized) return;

            try
            {
                _logger.LogInformation("Initializing message cache from database...");
                await using var conn = await _db.CreateConnectionAsync();
                await using var cmd = new NpgsqlCommand(
                    "SELECT template_key, template_text FROM text_templates WHERE is_active = true", 
                    conn);
                
                var count = 0;
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var key = reader.GetString(0);
                    var text = reader.GetString(1);
                    _cache[key] = text;
                    count++;
                }
                
                _cacheInitialized = true;
                _logger.LogInformation($"Cache initialized: {count} templates loaded");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load templates from database, using fallback");
            }
        }

        public async Task<string> GetTextAsync(string templateKey, Dictionary<string, string>? variables = null)
        {
            try
            {
                if (!_cacheInitialized && _cache.Count <= 20)
                {
                    _ = InitializeCacheAsync().ConfigureAwait(false);
                }

                if (_cache.TryGetValue(templateKey, out var template))
                {
                    return ReplaceVariables(template, variables);
                }

                _logger.LogWarning($"Template not found: {templateKey}");
                return $"[Template: {templateKey}]";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting template '{templateKey}'");
                return "❌ Ошибка загрузки сообщения";
            }
        }

        private string ReplaceVariables(string template, Dictionary<string, string>? variables)
        {
            if (variables == null || variables.Count == 0)
                return template;

            var result = template;
            foreach (var (key, value) in variables)
            {
                result = result.Replace($"{{{key}}}", value);
            }
            return result;
        }

        // Вспомогательные методы
        public async Task<string> GetWelcomeMessageAsync() 
            => await GetTextAsync("onboarding_welcome");

        public async Task<string> GetMainMenuTextAsync() 
            => await GetTextAsync("main_menu");

        public async Task<string> GetCravingDetectedAsync() 
            => await GetTextAsync("craving_detected");

        public async Task<string> GetSmokeRecordedAsync(string motivation) 
            => await GetTextAsync("smoke_recorded", new() { ["motivation"] = motivation });

        public async Task<string> GetAlternativeMenuAsync() 
            => await GetTextAsync("alternative_menu");

        public async Task<string> GetStatsHeaderAsync() 
            => await GetTextAsync("stats_header");

        public async Task<string> GetProgressHeaderAsync() 
            => await GetTextAsync("progress_header");

        public async Task<string> GetErrorGenericAsync() 
            => await GetTextAsync("error_generic");

        public async Task<string> GetUseButtonsAsync() 
            => await GetTextAsync("error_use_buttons");

        public async Task<string> GetLogoutSuccessAsync() 
            => await GetTextAsync("logout_success");
    }
}