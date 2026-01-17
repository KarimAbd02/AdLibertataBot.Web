using AdLibertataBot.Web.Data;

namespace AdLibertataBot.Web.Services.User
{
    public class OnboardingService
    {
        private readonly ILogger<OnboardingService> _logger;

        public OnboardingService(ILogger<OnboardingService> logger)
        {
            _logger = logger;
        }

        public string GetOnboardingStepDescription(OnboardingStep step)
        {
            return step switch
            {
                OnboardingStep.CompanyCode => "Ввод корпоративного кода",
                OnboardingStep.FagerstromTest => "Тест Фагерстрема",
                OnboardingStep.SmokingExperience => "Опыт курения",
                OnboardingStep.CigarettesPerDay => "Сигарет в день",
                OnboardingStep.GoalSelection => "Выбор цели",
                _ => "Неизвестный шаг"
            };
        }

        public int GetOnboardingProgress(OnboardingStep step)
        {
            return step switch
            {
                OnboardingStep.CompanyCode => 20,
                OnboardingStep.FagerstromTest => 40,
                OnboardingStep.SmokingExperience => 60,
                OnboardingStep.CigarettesPerDay => 80,
                OnboardingStep.GoalSelection => 100,
                _ => 0
            };
        }

        public bool IsOnboardingComplete(OnboardingStep step)
        {
            return step == OnboardingStep.GoalSelection;
        }
    }
}