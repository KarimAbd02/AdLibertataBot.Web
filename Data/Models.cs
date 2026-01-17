namespace AdLibertataBot.Web.Data
{
    public record Company(
        int Id,
        string Code,
        string Name,
        string ContactEmail,
        int MaxUsers,
        bool IsActive,
        DateTime CreatedAt);

    public record AdminUser(
        int Id,
        int CompanyId,
        string Username,
        string Email,
        string FullName,
        string AdminPassword,
        bool IsActive,
        DateTime CreatedAt);

    public record AppUser(
        int Id,
        string ChatId,
        int CompanyId,
        UserGoal Goal,
        DateTime JoinedAt,
        int Level,
        int Points,
        int TotalSmokes,
        int TotalAlternatives,
        int CurrentStreak,
        int BestStreak,
        bool IsActive,
        DateTime LastActivity);

    public record Challenge(
        int Id,
        string Title,
        string Description,
        int PointsReward,
        TimeSpan Duration,
        int DifficultyLevel,
        bool IsActive);

    public record UserChallenge(
        int Id,
        int UserId,
        int ChallengeId,
        ChallengeStatus Status,
        DateTime AssignedAt,
        DateTime? CompletedAt,
        int PointsEarned);

    public record CompanySettings(
        int Id,
        int CompanyId,
        string WellnessCoachName,
        string WellnessCoachContact,
        string CustomWelcomeMessage,
        string[] CustomMotivations,
        string ReportingFrequency);

    public class UserState
    {
        public string ChatId { get; set; } = string.Empty;
        public OnboardingStep Step { get; set; }
        public UserRole? Role { get; set; }
        public int? CompanyId { get; set; }
        public string? CompanyCode { get; set; }
        public DateTime LastUpdated { get; set; }
        
        public SmokingExperience? SmokingExperience { get; set; }
        public CigarettesPerDay? CigarettesPerDay { get; set; }
        
        public Dictionary<int, int> FagerstromAnswers { get; set; } = new();
        public int CurrentFagerstromQuestion { get; set; } = 1;

        public UserState() { }
    }

    public class UserDiagnostic
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public SmokingExperience SmokingExperience { get; set; }
        public CigarettesPerDay CigarettesPerDay { get; set; }
        public UserGoal GoalType { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public record FagerstromTestResult(
        int UserId,
        int TotalScore,
        DependencyLevel DependencyLevel,
        Dictionary<int, int> Answers,
        DateTime CompletedAt);

    public record BreathingTechnique(
        int Id,
        string Name,
        string Description,
        string Instructions,
        int DurationSeconds,
        int PointsReward,
        bool IsActive);

    public record Fact(
        int Id,
        string Title,
        string Content,
        string Category,
        int ReadingTimeMinutes,
        bool IsActive);

    public record VoiceMessage(
        int Id,
        string TriggerType,
        string FileId,
        string Text,
        bool IsActive);

    public record Achievement(
        int Id,
        string Name,
        string Description,
        string Icon,
        string Category,
        int PointsRequired,
        bool IsUnlocked);

    public record CompanyReport(
        string CompanyCode,
        int ActiveUsers,
        double AvgDailySmokes,
        double AvgDailyAlternatives,
        double AvgCravingInterval,
        int TotalChallengesCompleted,
        double WellnessScore,
        double AvgDailyCravings,
        double ImprovementPercentage);
}