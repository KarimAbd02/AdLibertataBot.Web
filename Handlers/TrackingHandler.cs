using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;
using AdLibertataBot.Web.Data;
using AdLibertataBot.Web.Services.Core;
using AdLibertataBot.Web.Services.User;
using AdLibertataBot.Web.Services.Tracking;
using AdLibertataBot.Web.Services.Content;
using AdLibertataBot.Web.Services.Gamification;
using AdLibertataBot.Web.Services.Analytics;

namespace AdLibertataBot.Web.Handlers
{
    public class TrackingHandler
    {
        private readonly ITelegramBotClient _bot;
        private readonly MessageService _messageService;
        private readonly UserService _userService;
        private readonly UserStateService _stateService;
        private readonly CravingService _cravingService;
        private readonly SmokeService _smokeService;
        private readonly AlternativeService _alternativeService;
        private readonly MotivationService _motivationService;
        private readonly BreathingTechniqueService _breathingService;
        private readonly ChallengeService _challengeService;
        private readonly PointsService _pointsService;
        private readonly ProgressService _progressService;
        private readonly StatsService _statsService;
        private readonly ILogger<TrackingHandler> _logger;

        public TrackingHandler(
            ITelegramBotClient bot,
            MessageService messageService,
            UserService userService,
            UserStateService stateService,
            CravingService cravingService,
            SmokeService smokeService,
            AlternativeService alternativeService,
            MotivationService motivationService,
            BreathingTechniqueService breathingService,
            ChallengeService challengeService,
            PointsService pointsService,
            ProgressService progressService,
            StatsService statsService,
            ILogger<TrackingHandler> logger)
        {
            _bot = bot;
            _messageService = messageService;
            _userService = userService;
            _stateService = stateService;
            _cravingService = cravingService;
            _smokeService = smokeService;
            _alternativeService = alternativeService;
            _motivationService = motivationService;
            _breathingService = breathingService;
            _challengeService = challengeService;
            _pointsService = pointsService;
            _progressService = progressService;
            _statsService = statsService;
            _logger = logger;
        }

        public async Task HandleAsync(string chatId, string text, CancellationToken cancellationToken)
        {
            var user = await _userService.GetUserAsync(chatId);
            
            if (user == null)
            {
                await _bot.SendTextMessageAsync(
                    chatId,
                    "Для начала работы введите /start",
                    replyMarkup: CommandHandler.GetStartKeyboard(),
                    cancellationToken: cancellationToken);
                return;
            }

            try
            {
                switch (text)
                {
                    case "Тяга к перекуру 🚬":
                        await HandleCravingAsync(user, cancellationToken);
                        break;
                    
                    case "Отложить на 2 мин ⏰":
                        await HandlePostponeAsync(user, 2, cancellationToken);
                        break;
                    
                    case "Отложить на 5 мин ⏰":
                        await HandlePostponeAsync(user, 5, cancellationToken);
                        break;
                    
                    case "Зафиксировать перекур 🚬":
                        await HandleSmokeAsync(user, cancellationToken);
                        break;
                    
                    case "Альтернатива 🔄":
                    case "Выбрать альтернативу 🔄":
                        await HandleAlternativeMenuAsync(user, cancellationToken);
                        break;
                    
                    case "Дыхание 🫁":
                        await HandleBreathingAsync(user, cancellationToken);
                        break;
                    
                    case "Вода/чай 💧":
                        await HandleAlternativeAsync(user, AlternativeType.Water, cancellationToken);
                        break;
                    
                    case "Зарядка 💪":
                        await HandleAlternativeAsync(user, AlternativeType.Exercise, cancellationToken);
                        break;
                    
                    case "Прогулка 🚶":
                        await HandleAlternativeAsync(user, AlternativeType.Walk, cancellationToken);
                        break;
                    
                    case "5 чувств 👃":
                        await HandleAlternativeAsync(user, AlternativeType.FiveSenses, cancellationToken);
                        break;
                    
                    case "Расслабление 🧘":
                        await HandleAlternativeAsync(user, AlternativeType.Relaxation, cancellationToken);
                        break;
                    
                    case "Статистика 📊":
                        await HandleStatsAsync(user, cancellationToken);
                        break;
                    
                    case "Мой прогресс 🎯":
                        await HandleProgressAsync(user, cancellationToken);
                        break;
                    
                    case "Челлендж 🏆":
                        await HandleChallengeAsync(user, cancellationToken);
                        break;
                    
                    case "✅ Выполнил челлендж":
                        await HandleCompleteChallengeAsync(user, cancellationToken);
                        break;
                    
                    case "❌ Пропустить":
                        await HandleSkipChallengeAsync(user, cancellationToken);
                        break;
                    
                    case "Мотивация 💫":
                        await HandleMotivationAsync(user, cancellationToken);
                        break;
                    
                    case "Назад ↩️":
                        await _bot.SendTextMessageAsync(
                            chatId,
                            await _messageService.GetMainMenuTextAsync(),
                            replyMarkup: CommandHandler.GetMainKeyboard(),
                            cancellationToken: cancellationToken);
                        break;
                    
                    case "🚪 Выйти":
                        await HandleLogoutAsync(chatId, cancellationToken);
                        break;
                    
                    default:
                        await _bot.SendTextMessageAsync(
                            chatId,
                            await _messageService.GetUseButtonsAsync(),
                            replyMarkup: CommandHandler.GetMainKeyboard(),
                            cancellationToken: cancellationToken);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error handling tracking for {chatId}");
                await _bot.SendTextMessageAsync(
                    chatId,
                    await _messageService.GetErrorGenericAsync(),
                    replyMarkup: CommandHandler.GetMainKeyboard(),
                    cancellationToken: cancellationToken);
            }
        }

        private async Task HandleCravingAsync(AppUser user, CancellationToken cancellationToken)
        {
            await _cravingService.AddCravingEventAsync(user.Id);
            
            await _bot.SendTextMessageAsync(
                user.ChatId,
                await _messageService.GetCravingDetectedAsync(),
                replyMarkup: GetCravingKeyboard(),
                cancellationToken: cancellationToken);
        }

        private async Task HandlePostponeAsync(AppUser user, int minutes, CancellationToken cancellationToken)
        {
            await _cravingService.AddCravingEventAsync(user.Id, minutes);
            
            var points = minutes == 2 
                ? PointsService.POINTS_CRAVING_POSTPONED_2MIN 
                : PointsService.POINTS_CRAVING_POSTPONED_5MIN;
            
            await _pointsService.AwardPointsAsync(user.Id, points, $"postponed_{minutes}min");
            
            await _bot.SendTextMessageAsync(
                user.ChatId,
                $"⏰ Вы отложили перекур на {minutes} минут!\n\n💰 +{points} очков\n💪 Так держать! Используйте это время для альтернативы.",
                replyMarkup: CommandHandler.GetMainKeyboard(),
                cancellationToken: cancellationToken);

            // Напоминание через заданное время
            _ = Task.Delay(minutes * 60 * 1000).ContinueWith(async _ =>
            {
                try
                {
                    await _bot.SendTextMessageAsync(
                        user.ChatId,
                        "⏰ Время вышло! Как Ваше состояние сейчас?",
                        replyMarkup: GetCravingKeyboard(),
                        cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending reminder");
                }
            }, cancellationToken);
        }

        private async Task HandleSmokeAsync(AppUser user, CancellationToken cancellationToken)
        {
            await _smokeService.AddSmokeEvent(user.Id);
            await _userService.UpdateUserStatsAsync(user.Id, true, false);
            await _progressService.UpdateDailyStatsAsync(user.Id);
            
            var motivation = await _motivationService.GetRandomMotivationAsync();
            
            await _bot.SendTextMessageAsync(
                user.ChatId,
                $"🚬 Записано. {motivation}",
                replyMarkup: CommandHandler.GetMainKeyboard(),
                cancellationToken: cancellationToken);
        }

        private async Task HandleAlternativeMenuAsync(AppUser user, CancellationToken cancellationToken)
        {
            await _bot.SendTextMessageAsync(
                user.ChatId,
                "🌿 Выберите здоровую альтернативу:\n\n" +
                "🫁 Дыхание - дыхательные техники\n" +
                "💧 Вода/чай - выпить стакан воды\n" +
                "💪 Зарядка - мини разминка\n" +
                "🚶 Прогулка - 100 шагов\n" +
                "👃 5 чувств - техника заземления\n" +
                "🧘 Расслабление - расслабить плечи/шею",
                replyMarkup: GetAlternativesKeyboard(),
                cancellationToken: cancellationToken);
        }

        private async Task HandleBreathingAsync(AppUser user, CancellationToken cancellationToken)
        {
            var technique = await _breathingService.GetRandomTechniqueAsync();
            
            if (technique == null)
            {
                await _bot.SendTextMessageAsync(
                    user.ChatId,
                    "К сожалению, техники временно недоступны",
                    replyMarkup: CommandHandler.GetMainKeyboard(),
                    cancellationToken: cancellationToken);
                return;
            }

            await _bot.SendTextMessageAsync(
                user.ChatId,
                $"🫁 {technique.Name}\n\n" +
                $"📝 {technique.Description}\n\n" +
                $"Инструкция:\n{technique.Instructions}\n\n" +
                $"⏱ Длительность: {technique.DurationSeconds} секунд",
                replyMarkup: CommandHandler.GetMainKeyboard(),
                cancellationToken: cancellationToken);

            await _alternativeService.AddAlternativeEvent(user.Id, AlternativeType.Breathing, technique.PointsReward);
            await _pointsService.AwardPointsAsync(user.Id, technique.PointsReward, "breathing_technique");
            await _userService.UpdateUserStatsAsync(user.Id, false, true);
            await _progressService.UpdateDailyStatsAsync(user.Id);

            await Task.Delay(2000, cancellationToken);
            
            await _bot.SendTextMessageAsync(
                user.ChatId,
                $"✨ Отлично! +{technique.PointsReward} очков за дыхательную технику!",
                cancellationToken: cancellationToken);
        }

        private async Task HandleAlternativeAsync(AppUser user, AlternativeType type, CancellationToken cancellationToken)
        {
            var points = await _pointsService.CalculatePointsForAlternativeAsync(type);
            
            await _alternativeService.AddAlternativeEvent(user.Id, type, points);
            await _pointsService.AwardPointsAsync(user.Id, points, $"alternative_{type}");
            await _userService.UpdateUserStatsAsync(user.Id, false, true);
            await _progressService.UpdateDailyStatsAsync(user.Id);

            var (emoji, message) = type switch
            {
                AlternativeType.Water => ("💧", "Вода - отличный выбор! Организм благодарит вас."),
                AlternativeType.Exercise => ("💪", "Отлично! Небольшая разминка взбодрит тело."),
                AlternativeType.Walk => ("🚶", "Прогулка - прекрасная альтернатива!"),
                AlternativeType.FiveSenses => ("", """
                                                     💫 Сосредоточьтесь на настоящем моменте:

                                                     • 5 вещей, которые вы видите
                                                     • 4 звука, которые слышите
                                                     • 3 ощущения, которые чувствуете
                                                     • 2 запаха
                                                     • 1 вкус

                                                     🫁 Дышите спокойно и выполняйте упражнение не спеша.
                                                     """),
                AlternativeType.Relaxation => ("🧘", "Расслабление плеч и шеи снимет напряжение."),
                _ => ("🔄", "Отличная альтернатива перекуру!")
            };

            await _bot.SendTextMessageAsync(
                user.ChatId,
                $"{emoji} {message}\n\n💰 +{points} очков\n✨ Отличная замена перекуру!",
                replyMarkup: CommandHandler.GetMainKeyboard(),
                cancellationToken: cancellationToken);
        }

        private async Task HandleStatsAsync(AppUser user, CancellationToken cancellationToken)
        {
            var stats = await _statsService.GetStatsMessageAsync(user.ChatId);
            await _bot.SendTextMessageAsync(
                user.ChatId,
                stats,
                parseMode: ParseMode.Markdown,
                replyMarkup: CommandHandler.GetMainKeyboard(),
                cancellationToken: cancellationToken);
        }

        private async Task HandleProgressAsync(AppUser user, CancellationToken cancellationToken)
        {
            var progress = await _progressService.GetProgressReportAsync(user.Id);
            await _bot.SendTextMessageAsync(
                user.ChatId,
                progress,
                replyMarkup: CommandHandler.GetMainKeyboard(),
                cancellationToken: cancellationToken);
        }

        private async Task HandleChallengeAsync(AppUser user, CancellationToken cancellationToken)
        {
            var activeChallenge = await _challengeService.GetActiveChallengeAsync(user.Id);
            
            if (activeChallenge != null)
            {
                // Показываем активный челлендж с кнопкой выполнения
                var challenge = await _challengeService.GetChallengeByIdAsync(activeChallenge.ChallengeId);
                if (challenge != null)
                {
                    var timeLeft = DateTime.UtcNow - activeChallenge.AssignedAt;
                    var hoursLeft = challenge.Duration.TotalHours - timeLeft.TotalHours;
                    
                    var message = $"📋 **Активный челлендж**\n\n" +
                                 $"🏆 {challenge.Title}\n" +
                                 $"📝 {challenge.Description}\n\n" +
                                 $"⏱ Осталось: {Math.Max(0, (int)hoursLeft)}ч\n" +
                                 $"💰 Награда: {challenge.PointsReward} очков\n\n" +
                                 $"✅ Когда выполните - нажмите кнопку ниже!";

                    var keyboard = new ReplyKeyboardMarkup(new[]
                    {
                        new KeyboardButton[] { "✅ Выполнил челлендж", "❌ Пропустить" },
                        new KeyboardButton[] { "Назад ↩️" }
                    })
                    { ResizeKeyboard = true };

                    await _bot.SendTextMessageAsync(
                        user.ChatId,
                        message,
                        parseMode: ParseMode.Markdown,
                        replyMarkup: keyboard,
                        cancellationToken: cancellationToken);
                }
                return;
            }

            // Берем новый челлендж
            var newChallenge = await _challengeService.GetRandomChallengeAsync();
            
            if (newChallenge == null)
            {
                await _bot.SendTextMessageAsync(
                    user.ChatId,
                    "К сожалению, сейчас нет доступных челленджей. Попробуйте позже!",
                    replyMarkup: CommandHandler.GetMainKeyboard(),
                    cancellationToken: cancellationToken);
                return;
            }

            await _challengeService.AssignChallengeAsync(user.Id, newChallenge.Id);
            
            var assignMessage = $"🎯 **Новый челлендж!**\n\n" +
                               $"🏆 {newChallenge.Title}\n" +
                               $"📝 {newChallenge.Description}\n\n" +
                               $"⏱ На выполнение: {newChallenge.Duration.TotalHours}ч\n" +
                               $"💰 Награда: {newChallenge.PointsReward} очков\n\n" +
                               $"Когда выполните - нажмите 'Челлендж 🏆' снова и подтвердите выполнение!";

            await _bot.SendTextMessageAsync(
                user.ChatId,
                assignMessage,
                parseMode: ParseMode.Markdown,
                replyMarkup: CommandHandler.GetMainKeyboard(),
                cancellationToken: cancellationToken);
        }

        private async Task HandleCompleteChallengeAsync(AppUser user, CancellationToken cancellationToken)
        {
            var activeChallenge = await _challengeService.GetActiveChallengeAsync(user.Id);
    
            if (activeChallenge == null)
            {
                await _bot.SendTextMessageAsync(
                    user.ChatId,
                    "У Вас нет активного челленджа!",
                    replyMarkup: CommandHandler.GetMainKeyboard(),
                    cancellationToken: cancellationToken);
                return;
            }

            // Получаем информацию о челлендже, включая количество очков
            var challenge = await _challengeService.GetChallengeByIdAsync(activeChallenge.ChallengeId);
    
            if (challenge == null)
            {
                await _bot.SendTextMessageAsync(
                    user.ChatId,
                    "Ошибка: челлендж не найден",
                    replyMarkup: CommandHandler.GetMainKeyboard(),
                    cancellationToken: cancellationToken);
                return;
            }

            // Завершаем челлендж и получаем количество начисленных очков
            await _challengeService.CompleteChallengeAsync(user.Id, activeChallenge.Id);
    
            await _bot.SendTextMessageAsync(
                user.ChatId,
                $"🎉 Поздравляю с выполнением!\n💰 +{challenge.PointsReward} очков начислено!",
                replyMarkup: CommandHandler.GetMainKeyboard(),
                cancellationToken: cancellationToken);
        }

        private async Task HandleSkipChallengeAsync(AppUser user, CancellationToken cancellationToken)
        {
            var activeChallenge = await _challengeService.GetActiveChallengeAsync(user.Id);
            
            if (activeChallenge == null)
            {
                await _bot.SendTextMessageAsync(
                    user.ChatId,
                    "У Вас нет активного челленджа!",
                    replyMarkup: CommandHandler.GetMainKeyboard(),
                    cancellationToken: cancellationToken);
                return;
            }

            // Пропускаем челлендж
            await _challengeService.SkipChallengeAsync(user.Id, activeChallenge.Id);
            
            await _bot.SendTextMessageAsync(
                user.ChatId,
                "❌ Челлендж пропущен. Можно взять новый!",
                replyMarkup: CommandHandler.GetMainKeyboard(),
                cancellationToken: cancellationToken);
        }

        private async Task HandleMotivationAsync(AppUser user, CancellationToken cancellationToken)
        {
            var motivation = await _motivationService.GetRandomMotivationAsync();
            
            await _bot.SendTextMessageAsync(
                user.ChatId,
                "💫 " + motivation,
                replyMarkup: CommandHandler.GetMainKeyboard(),
                cancellationToken: cancellationToken);
        }

        private async Task HandleLogoutAsync(string chatId, CancellationToken cancellationToken)
        {
            // Полностью очищаем состояние
            await _stateService.DeleteUserStateAsync(chatId);
            
            await _bot.SendTextMessageAsync(
                chatId,
                "Вы вышли из системы.\n\nДля входа нажмите /start",
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: cancellationToken);
        }

        private static IReplyMarkup GetCravingKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "Отложить на 2 мин ⏰", "Отложить на 5 мин ⏰" },
                new KeyboardButton[] { "Выбрать альтернативу 🔄", "Зафиксировать перекур 🚬" },
                new KeyboardButton[] { "Назад ↩️" }
            })
            { 
                ResizeKeyboard = true 
            };
        }

        private static IReplyMarkup GetAlternativesKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "Дыхание 🫁", "Вода/чай 💧" },
                new KeyboardButton[] { "Зарядка 💪", "Прогулка 🚶" },
                new KeyboardButton[] { "5 чувств 👃", "Расслабление 🧘" },
                new KeyboardButton[] { "Назад ↩️" }
            })
            { 
                ResizeKeyboard = true 
            };
        }
    }
}