using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using AdLibertataBot.Web.Data;
using AdLibertataBot.Web.Services.Core;
using AdLibertataBot.Web.Services.User;
using AdLibertataBot.Web.Services.Company;
using AdLibertataBot.Web.Services.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AdLibertataBot.Web.Handlers
{
    public class OnboardingHandler
    {
        private readonly ITelegramBotClient _bot;
        private readonly UserService _userService;
        private readonly UserStateService _stateService;
        private readonly CompanyService _companyService;
        private readonly CompanyAdminService _adminService;
        private readonly FagerstromTestService _fagerstromService;
        private readonly DiagnosticService _diagnosticService;
        private readonly ILogger<OnboardingHandler> _logger;

        public OnboardingHandler(
            ITelegramBotClient bot,
            UserService userService,
            UserStateService stateService,
            CompanyService companyService,
            CompanyAdminService adminService,
            FagerstromTestService fagerstromService,
            DiagnosticService diagnosticService,
            ILogger<OnboardingHandler> logger)
        {
            _bot = bot;
            _userService = userService;
            _stateService = stateService;
            _companyService = companyService;
            _adminService = adminService;
            _fagerstromService = fagerstromService;
            _diagnosticService = diagnosticService;
            _logger = logger;
        }

        public async Task HandleAsync(string chatId, string text, CancellationToken cancellationToken)
        {
            // Получаем текущее состояние
            var state = await _stateService.GetUserStateAsync(chatId);

            if (state == null)
            {
                // Нет состояния - начинаем с выбора роли
                await StartOnboarding(chatId, text, cancellationToken);
            }
            else
            {
                // Есть состояние - продолжаем онбординг
                await ContinueOnboarding(chatId, text, state, cancellationToken);
            }
        }

        private async Task StartOnboarding(string chatId, string text, CancellationToken cancellationToken)
        {
            // Показываем выбор роли
            var keyboard = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { new("👤 Пользователь"), new("👨‍💼 Администратор") }
            })
            {
                ResizeKeyboard = true
            };

            await _bot.SendTextMessageAsync(
                chatId,
                "👋 Добро пожаловать в Ad Libertata Work!\n\nВыберите вашу роль:",
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);

            // Создаем начальное состояние
            var state = new UserState
            {
                ChatId = chatId,
                Step = OnboardingStep.RoleSelection,
                LastUpdated = DateTime.UtcNow
            };
            
            await _stateService.SaveFullStateAsync(state);
        }

        private async Task ContinueOnboarding(string chatId, string text, UserState state, CancellationToken cancellationToken)
        {
            switch (state.Step)
            {
                case OnboardingStep.RoleSelection:
                    await HandleRoleSelection(chatId, text, state, cancellationToken);
                    break;
                    
                case OnboardingStep.CompanyCode:
                    await HandleCompanyCode(chatId, text, state, cancellationToken);
                    break;
                    
                case OnboardingStep.FagerstromTest:
                    await HandleFagerstromTest(chatId, text, state, cancellationToken);
                    break;
                    
                case OnboardingStep.SmokingExperience:
                    await HandleSmokingExperience(chatId, text, state, cancellationToken);
                    break;
                    
                case OnboardingStep.CigarettesPerDay:
                    await HandleCigarettesPerDay(chatId, text, state, cancellationToken);
                    break;
                    
                case OnboardingStep.GoalSelection:
                    await HandleGoalSelection(chatId, text, state, cancellationToken);
                    break;
            }
        }

        private async Task HandleRoleSelection(string chatId, string text, UserState state, CancellationToken cancellationToken)
{
    if (text.Contains("👤 Пользователь") || text.Contains("👤 Войти как пользователь"))
    {
        state.Role = UserRole.User;
        state.Step = OnboardingStep.CompanyCode;
        await _stateService.SaveFullStateAsync(state);
        
        // Проверяем, есть ли уже пользователь в БД
        var userExists = await _userService.UserExistsAsync(chatId);
        
        if (userExists)
        {
            // Пользователь уже есть - сразу показываем главное меню
            var user = await _userService.GetUserAsync(chatId);
            if (user != null)
            {
                await _stateService.DeleteUserStateAsync(chatId);
                
                await _bot.SendTextMessageAsync(
                    chatId,
                    "С возвращением! 👋",
                    replyMarkup: CommandHandler.GetMainKeyboard(),
                    cancellationToken: cancellationToken);
            }
            else
            {
                await _bot.SendTextMessageAsync(
                    chatId,
                    "👤 Вход как **Пользователь**\n\nВведите корпоративный код:",
                    replyMarkup: new ReplyKeyboardRemove(),
                    cancellationToken: cancellationToken);
            }
        }
        else
        {
            await _bot.SendTextMessageAsync(
                chatId,
                "👤 Вы выбрали роль **Пользователь**\n\nВведите корпоративный код:",
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: cancellationToken);
        }
    }
    else if (text.Contains("👨‍💼 Администратор") || text.Contains("👨‍💼 Войти как администратор"))
    {
        state.Role = UserRole.Admin;
        state.Step = OnboardingStep.CompanyCode;
        await _stateService.SaveFullStateAsync(state);
        
        await _bot.SendTextMessageAsync(
            chatId,
            "👨‍💼 Вход как **Администратор**\n\nВведите корпоративный код компании:",
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: cancellationToken);
    }
    else
    {
        await _bot.SendTextMessageAsync(
            chatId,
            "Выберите вариант, нажав на кнопку 👆",
            cancellationToken: cancellationToken);
    }
}

        private async Task HandleCompanyCode(string chatId, string text, UserState state, CancellationToken cancellationToken)
        {
            var company = await _companyService.GetCompanyByCodeAsync(text.ToUpper().Trim());
            
            if (company == null)
            {
                await _bot.SendTextMessageAsync(
                    chatId,
                    "❌ Компания не найдена. Попробуйте снова:",
                    cancellationToken: cancellationToken);
                return;
            }

            state.CompanyId = company.Id;
            state.CompanyCode = company.Code;

            if (state.Role == UserRole.Admin)
            {
                // АДМИН - запрашиваем пароль
                state.Step = OnboardingStep.GoalSelection;
                await _stateService.SaveFullStateAsync(state);
                
                await _bot.SendTextMessageAsync(
                    chatId,
                    $"✅ Компания: {company.Name}\n\nВведите пароль администратора:",
                    cancellationToken: cancellationToken);
            }
            else
            {
                // ПОЛЬЗОВАТЕЛЬ - начинаем тест
                state.Step = OnboardingStep.FagerstromTest;
                state.CurrentFagerstromQuestion = 1;
                await _stateService.SaveFullStateAsync(state);
                
                await _bot.SendTextMessageAsync(
                    chatId,
                    $"✅ Компания: {company.Name}\n\nТеперь пройдем короткий тест:",
                    cancellationToken: cancellationToken);
                    
                await Task.Delay(1000);
                
                await _bot.SendTextMessageAsync(
                    chatId,
                    _fagerstromService.GetQuestionText(1),
                    cancellationToken: cancellationToken);
            }
        }

        private async Task HandleFagerstromTest(string chatId, string text, UserState state, CancellationToken cancellationToken)
        {
            // Обработка теста Фагерстрема (оставь свою текущую логику)
            // ...
            
            // После завершения теста:
            state.Step = OnboardingStep.SmokingExperience;
            await _stateService.SaveFullStateAsync(state);
            
            await _bot.SendTextMessageAsync(
                chatId,
                "Сколько лет вы курите?\n1️⃣ - Меньше года\n2️⃣ - 1-3 года\n3️⃣ - 3-5 лет\n4️⃣ - 5-10 лет\n5️⃣ - Более 10 лет",
                cancellationToken: cancellationToken);
        }

        private async Task HandleSmokingExperience(string chatId, string text, UserState state, CancellationToken cancellationToken)
        {
            if (!int.TryParse(text.Trim(), out int exp) || exp < 1 || exp > 5)
            {
                await _bot.SendTextMessageAsync(
                    chatId,
                    "Введите номер от 1 до 5",
                    cancellationToken: cancellationToken);
                return;
            }

            state.SmokingExperience = (SmokingExperience)exp;
            state.Step = OnboardingStep.CigarettesPerDay;
            await _stateService.SaveFullStateAsync(state);
            
            await _bot.SendTextMessageAsync(
                chatId,
                "Сколько сигарет в день?\n1️⃣ - <5\n2️⃣ - 5-10\n3️⃣ - 10-15\n4️⃣ - 15-20\n5️⃣ - >20",
                cancellationToken: cancellationToken);
        }

        private async Task HandleCigarettesPerDay(string chatId, string text, UserState state, CancellationToken cancellationToken)
        {
            if (!int.TryParse(text.Trim(), out int cig) || cig < 1 || cig > 5)
            {
                await _bot.SendTextMessageAsync(
                    chatId,
                    "Введите номер от 1 до 5",
                    cancellationToken: cancellationToken);
                return;
            }

            state.CigarettesPerDay = (CigarettesPerDay)cig;
            state.Step = OnboardingStep.GoalSelection;
            await _stateService.SaveFullStateAsync(state);
            
            await _bot.SendTextMessageAsync(
                chatId,
                "🎯 Ваша цель?\n1️⃣ - Отслеживание\n2️⃣ - Снизить перекуры\n3️⃣ - Снизить стресс\n4️⃣ - Полный отказ",
                cancellationToken: cancellationToken);
        }

        private async Task HandleGoalSelection(string chatId, string text, UserState state, CancellationToken cancellationToken)
        {
            if (state.Role == UserRole.Admin)
            {
                // АДМИН - проверяем пароль
                var admin = await _adminService.GetAdminByPasswordAsync(text, state.CompanyId.Value);
                
                if (admin == null)
                {
                    await _bot.SendTextMessageAsync(
                        chatId,
                        "❌ Неверный пароль. Попробуйте снова:",
                        cancellationToken: cancellationToken);
                    return;
                }

                // Успешная авторизация админа
                await _stateService.DeleteUserStateAsync(chatId);
                
                var keyboard = new ReplyKeyboardMarkup(new[]
                {
                    new KeyboardButton[] { new("📊 Отчёт"), new("🏆 Топ") },
                    new KeyboardButton[] { new("👥 Статистика") },
                    new KeyboardButton[] { new("🚪 Выйти") }
                })
                {
                    ResizeKeyboard = true
                };

                await _bot.SendTextMessageAsync(
                    chatId,
                    $"👨‍💼 Добро пожаловать, {admin.FullName}!\nАдмин-панель:",
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
            }
            else
            {
                // ПОЛЬЗОВАТЕЛЬ - завершаем регистрацию
                if (!int.TryParse(text.Trim(), out int goal) || goal < 1 || goal > 4)
                {
                    await _bot.SendTextMessageAsync(
                        chatId,
                        "Введите номер от 1 до 4",
                        cancellationToken: cancellationToken);
                    return;
                }

                // Проверяем, есть ли пользователь
                var existingUser = await _userService.GetUserAsync(chatId);
                int userId;

                if (existingUser != null)
                {
                    userId = existingUser.Id;
                    await _userService.UpdateUserGoalAsync(userId, (UserGoal)goal);
                }
                else
                {
                    userId = await _userService.CreateUserAsync(chatId, state.CompanyId.Value, (UserGoal)goal);
                }

                // Сохраняем диагностику
                // ... (твоя текущая логика)

                // Завершаем онбординг
                await _stateService.DeleteUserStateAsync(chatId);

                await _bot.SendTextMessageAsync(
                    chatId,
                    "🎉 Регистрация завершена! Теперь вы можете использовать бота.",
                    replyMarkup: CommandHandler.GetMainKeyboard(),
                    cancellationToken: cancellationToken);
            }
        }
    }
}