namespace FitnessGame.Models;

public enum ActivityKind
{
    Rest,
    Laze,
    Hike,
    Yoga,
    Cardio,
    TrainChest,
    TrainBack,
    TrainArms,
    TrainAbs,
    TrainLegs,
    TrainGlutes
}

public class Activity
{
    public ActivityKind Kind { get; init; }
    public string Name { get; init; } = "";
    public string Emoji { get; init; } = "";
    public string Description { get; init; } = "";
    public int EnergyCost { get; init; }
    public int CaloriesBurned { get; init; }

    public static IReadOnlyList<Activity> All { get; } = new[]
    {
        new Activity { Kind = ActivityKind.Rest,        Name = "Rest day",        Emoji = "🛌️",
            Description = "Sleep in, recover energy. Slight detraining.",
            EnergyCost = -40, CaloriesBurned = 1400 },
        new Activity { Kind = ActivityKind.Laze,        Name = "Laze on couch",   Emoji = "📺",
            Description = "Netflix and chill. Mood up, fitness down.",
            EnergyCost = -10, CaloriesBurned = 1500 },
        new Activity { Kind = ActivityKind.Hike,        Name = "Mountain hike",   Emoji = "🥾",
            Description = "Long outdoor hike. Endurance + mood.",
            EnergyCost = 30, CaloriesBurned = 2400 },
        new Activity { Kind = ActivityKind.Yoga,        Name = "Yoga & stretch",  Emoji = "🧘‍♀️",
            Description = "Flexibility, mood, light tone everywhere.",
            EnergyCost = 10, CaloriesBurned = 1800 },
        new Activity { Kind = ActivityKind.Cardio,      Name = "HIIT cardio",     Emoji = "🏃‍♀️",
            Description = "Intense cardio. Fat-burn + endurance.",
            EnergyCost = 35, CaloriesBurned = 2700 },
        new Activity { Kind = ActivityKind.TrainChest,  Name = "Train chest",     Emoji = "🤎",
            Description = "Bench, push-ups, dumbbell flyes.",
            EnergyCost = 30, CaloriesBurned = 2200 },
        new Activity { Kind = ActivityKind.TrainBack,   Name = "Train back",      Emoji = "🪵",
            Description = "Rows, pull-ups, lat work.",
            EnergyCost = 30, CaloriesBurned = 2200 },
        new Activity { Kind = ActivityKind.TrainArms,   Name = "Train arms",      Emoji = "💪",
            Description = "Curls, triceps extensions, shoulder press.",
            EnergyCost = 25, CaloriesBurned = 2000 },
        new Activity { Kind = ActivityKind.TrainAbs,    Name = "Train abs",       Emoji = "🔥",
            Description = "Crunches, planks, hanging leg raises.",
            EnergyCost = 20, CaloriesBurned = 2000 },
        new Activity { Kind = ActivityKind.TrainLegs,   Name = "Train legs",      Emoji = "🦵",
            Description = "Squats, lunges, leg press.",
            EnergyCost = 35, CaloriesBurned = 2500 },
        new Activity { Kind = ActivityKind.TrainGlutes, Name = "Glute workout",   Emoji = "🍑",
            Description = "Hip thrusts, kickbacks, RDLs.",
            EnergyCost = 28, CaloriesBurned = 2200 }
    };
}
