using FitnessGame.Models;

namespace FitnessGame.Services;

public class TurnResult
{
    public Activity Activity { get; init; } = default!;
    public IReadOnlyList<Food> Foods { get; init; } = Array.Empty<Food>();
    public int CaloriesIn { get; init; }
    public int CaloriesOut { get; init; }
    public int Delta => CaloriesIn - CaloriesOut;
    public Stats Before { get; init; } = default!;
    public Stats After { get; init; } = default!;
    public List<string> Log { get; } = new();
}

public class GameEngine
{
    public Stats Stats { get; private set; } = new();
    public TurnResult? LastTurn { get; private set; }
    public List<TurnResult> History { get; } = new();

    public event Action? Changed;

    public void Reset()
    {
        Stats = new Stats();
        LastTurn = null;
        History.Clear();
        Changed?.Invoke();
    }

    public TurnResult AdvanceDay(Activity activity, IReadOnlyList<Food> foods)
    {
        if (foods is null || foods.Count == 0)
            throw new ArgumentException("At least one meal is required.", nameof(foods));

        var before = Stats.Clone();
        var log = new List<string>();

        var totalCalories = foods.Sum(f => f.Calories);
        var totalProtein  = foods.Sum(f => f.Protein);

        // Aggregate food acts as the day's intake for training/protein calculations.
        var aggregate = new Food
        {
            Name = string.Join(" + ", foods.Select(f => f.Name)),
            Calories = totalCalories,
            Protein  = totalProtein,
            Carbs    = foods.Sum(f => f.Carbs),
            Fat      = foods.Sum(f => f.Fat),
        };

        // --- Energy ---
        // Negative EnergyCost = recovery, positive = drain.
        Stats.Energy -= activity.EnergyCost;

        // --- Calorie balance => fat/muscle drift ---
        // ~7700 kcal per kg of fat. We split a portion into fat change and
        // a smaller portion into muscle change when training and protein is adequate.
        var delta = totalCalories - activity.CaloriesBurned;
        var fatKg = delta / 7700.0;

        Stats.BodyFat += fatKg * 1.2;                 // crude, but scales nicely
        Stats.Weight  += fatKg;

        if (delta < -200) log.Add($"Calorie deficit of {-delta} kcal — fat trending down.");
        else if (delta > 200) log.Add($"Calorie surplus of {delta} kcal — fat trending up.");
        else log.Add("Maintenance calories.");

        // --- Apply activity-specific effects ---
        ApplyActivity(activity, aggregate, log);

        // --- Food quality side-effects ---
        foreach (var f in foods)
            ApplyFood(f, activity, log);

        // Low-protein muscle dip applies once for the whole day, not per meal.
        if (totalProtein < 25 && activity.Kind.ToString().StartsWith("Train"))
        {
            Stats.MuscleMass -= 0.15;
            log.Add("Low protein on a training day — muscle dip.");
        }

        // --- Mood drift towards 50, energy drift ---
        if (Stats.Energy < 25) { Stats.Mood -= 3; log.Add("Low energy is tanking your mood."); }
        if (Stats.Health < 40) { Stats.Mood -= 2; }

        // --- Slow detraining if no real training today ---
        if (activity.Kind is ActivityKind.Rest or ActivityKind.Laze)
        {
            Stats.Strength    -= 0.6;
            Stats.Endurance   -= 0.6;
            Stats.ChestTone   -= 0.4;
            Stats.BackTone    -= 0.4;
            Stats.ArmsTone    -= 0.4;
            Stats.AbsTone     -= 0.4;
            Stats.LegsTone    -= 0.4;
            Stats.GluteTone   -= 0.4;
            Stats.MuscleMass  -= 0.1;
        }

        // --- Instagram followers: gain when you look better, lose if you slip ---
        var leanness = (100 - Stats.BodyFat) * 0.6;
        var tone = (Stats.ChestTone + Stats.BackTone + Stats.ArmsTone +
                    Stats.AbsTone + Stats.LegsTone + Stats.GluteTone) / 6.0;
        var aesthetic = leanness * 0.6 + tone * 0.4;       // 0..~100
        var followerDelta = (int)Math.Round((aesthetic - 55) * 12 + (Stats.Mood - 50) * 2);
        Stats.Followers = Math.Max(0, Stats.Followers + followerDelta);
        if (followerDelta > 0) log.Add($"+{followerDelta} followers liked today's post.");
        else if (followerDelta < 0) log.Add($"{followerDelta} followers unfollowed.");

        Stats.Day += 1;
        Stats.Normalize();

        var result = new TurnResult
        {
            Activity = activity,
            Foods = foods.ToList(),
            CaloriesIn = totalCalories,
            CaloriesOut = activity.CaloriesBurned,
            Before = before,
            After = Stats.Clone()
        };
        result.Log.AddRange(log);

        LastTurn = result;
        History.Add(result);
        Changed?.Invoke();
        return result;
    }

    private void ApplyActivity(Activity a, Food food, List<string> log)
    {
        // Sufficient protein boosts muscle gain from training.
        var proteinBoost = Math.Clamp(food.Protein / 40.0, 0.4, 1.4);

        switch (a.Kind)
        {
            case ActivityKind.Rest:
                Stats.Health += 4;
                Stats.Mood += 2;
                log.Add("You feel rested.");
                break;
            case ActivityKind.Laze:
                Stats.Mood += 4;
                Stats.Health -= 1;
                log.Add("Comfy, but you stiffened up a bit.");
                break;
            case ActivityKind.Hike:
                Stats.Endurance += 2.5;
                Stats.LegsTone  += 1.2;
                Stats.GluteTone += 1.0;
                Stats.Mood      += 5;
                Stats.Health    += 2;
                log.Add("The hike cleared your head.");
                break;
            case ActivityKind.Yoga:
                Stats.Flexibility += 3.5;
                Stats.AbsTone     += 0.6;
                Stats.GluteTone   += 0.6;
                Stats.Mood        += 4;
                Stats.Health      += 1;
                log.Add("Calm, centered, more flexible.");
                break;
            case ActivityKind.Cardio:
                Stats.Endurance += 3.5;
                Stats.LegsTone  += 0.8;
                Stats.AbsTone   += 0.6;
                Stats.Health    += 1;
                log.Add("HIIT torched the calories.");
                break;
            case ActivityKind.TrainChest:
                Stats.Strength   += 2.0;
                Stats.ChestTone  += 4.0 * proteinBoost;
                Stats.ArmsTone   += 1.0 * proteinBoost;
                Stats.MuscleMass += 0.20 * proteinBoost;
                log.Add($"Chest pumped (protein factor x{proteinBoost:0.0}).");
                break;
            case ActivityKind.TrainBack:
                Stats.Strength   += 2.0;
                Stats.BackTone   += 4.0 * proteinBoost;
                Stats.ArmsTone   += 1.0 * proteinBoost;
                Stats.MuscleMass += 0.20 * proteinBoost;
                log.Add($"Back day — lats firing (protein factor x{proteinBoost:0.0}).");
                break;
            case ActivityKind.TrainArms:
                Stats.Strength   += 1.5;
                Stats.ArmsTone   += 4.5 * proteinBoost;
                Stats.MuscleMass += 0.15 * proteinBoost;
                log.Add($"Arms feel like jelly. In a good way.");
                break;
            case ActivityKind.TrainAbs:
                Stats.AbsTone    += 5.0 * proteinBoost;
                Stats.Strength   += 0.8;
                Stats.MuscleMass += 0.08 * proteinBoost;
                log.Add("Core lit up.");
                break;
            case ActivityKind.TrainLegs:
                Stats.Strength   += 2.5;
                Stats.LegsTone   += 4.5 * proteinBoost;
                Stats.GluteTone  += 2.0 * proteinBoost;
                Stats.MuscleMass += 0.30 * proteinBoost;
                Stats.Endurance  += 1.0;
                log.Add("Leg day. You may regret the stairs tomorrow.");
                break;
            case ActivityKind.TrainGlutes:
                Stats.GluteTone  += 5.0 * proteinBoost;
                Stats.LegsTone   += 1.5 * proteinBoost;
                Stats.MuscleMass += 0.18 * proteinBoost;
                log.Add("Peach mode engaged.");
                break;
        }
    }

    private void ApplyFood(Food food, Activity activity, List<string> log)
    {
        switch (food.Tier)
        {
            case FoodTier.Junk:
                Stats.Mood   += 4;
                Stats.Health -= 4;
                Stats.Energy += 5;     // sugar rush
                log.Add($"{food.Emoji} {food.Name}: delicious shame.");
                break;
            case FoodTier.Average:
                Stats.Mood   += 1;
                Stats.Energy += 3;
                break;
            case FoodTier.Healthy:
                Stats.Mood   += 0;
                Stats.Health += 3;
                Stats.Energy += 4;
                log.Add($"{food.Emoji} {food.Name}: clean fuel.");
                break;
            case FoodTier.Strict:
                Stats.Mood   -= 3;
                Stats.Health += 2;
                Stats.Energy += food.Calories == 0 ? -4 : -2;
                log.Add($"{food.Emoji} {food.Name}: discipline hurts.");
                break;
        }
    }
}
