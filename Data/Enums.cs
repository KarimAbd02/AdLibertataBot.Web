namespace AdLibertataBot.Web.Data
{
    public enum UserGoal
    {
        Tracking = 1,
        ReduceSmoking = 2,
        ReduceStress = 3,
        QuitSmoking = 4
    }

    public enum EventType
    {
        Craving = 1,
        Smoke = 2,
        Alternative = 3,
        Success = 4
    }

    public enum AlternativeType
    {
        Breathing = 1,
        Water = 2,
        Exercise = 3,
        Walk = 4,
        FiveSenses = 5,
        Relaxation = 6
    }

    public enum ChallengeStatus
    {
        Active = 1,
        Completed = 2,
        Skipped = 3
    }

    public enum OnboardingStep
    {
        RoleSelection = 0, 
        CompanyCode = 1,
        FagerstromTest = 2,
        SmokingExperience = 3,
        CigarettesPerDay = 4,
        GoalSelection = 5
    }
    
    public enum UserRole
    {
        User = 0,
        Admin = 1
    }
    public enum SmokingExperience
    {
        LessThanYear = 1,
        OneToThreeYears = 2,
        ThreeToFiveYears = 3,
        FiveToTenYears = 4,
        MoreThanTenYears = 5
    }

    public enum CigarettesPerDay
    {
        LessThanFive = 1,
        FiveToTen = 2,
        TenToFifteen = 3,
        FifteenToTwenty = 4,
        MoreThanTwenty = 5
    }

    public enum FagerstromQuestion
    {
        TimeToFirstCigarette = 1,
        DifficultToRefrain = 2,
        WhichCigarette = 3,
        CigarettesPerDay = 4,
        MoreInMorning = 5,
        SmokeWhenIll = 6
    }

    public enum DependencyLevel
    {
        VeryWeak = 0,
        Weak = 1,
        Medium = 2,
        High = 3,
        VeryHigh = 4
    }
}