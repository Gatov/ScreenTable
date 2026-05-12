namespace FitnessGame.Models;

public enum FoodTier
{
    Junk,
    Average,
    Healthy,
    Strict
}

public class Food
{
    public string Name { get; init; } = "";
    public string Emoji { get; init; } = "";
    public FoodTier Tier { get; init; }
    public int Calories { get; init; }
    public int Protein { get; init; }   // grams
    public int Carbs { get; init; }     // grams
    public int Fat { get; init; }       // grams
    public string Description { get; init; } = "";

    public static IReadOnlyList<Food> All { get; } = new[]
    {
        // --- Junk ---
        new Food { Name = "Double burger + fries", Emoji = "🍔", Tier = FoodTier.Junk,
            Calories = 1450, Protein = 45, Carbs = 130, Fat = 78,
            Description = "Greasy and delicious. Devastating to abs." },
        new Food { Name = "Pepperoni pizza",       Emoji = "🍕", Tier = FoodTier.Junk,
            Calories = 1600, Protein = 55, Carbs = 180, Fat = 65,
            Description = "Half a large pie, no regrets... yet." },
        new Food { Name = "Donuts & soda",         Emoji = "🍩", Tier = FoodTier.Junk,
            Calories = 1300, Protein = 12, Carbs = 200, Fat = 50,
            Description = "Pure sugar bomb. Energy spike, then crash." },
        new Food { Name = "Ice cream tub",         Emoji = "🍨", Tier = FoodTier.Junk,
            Calories = 1100, Protein = 18, Carbs = 130, Fat = 55,
            Description = "Mood booster. Body fat enabler." },

        // --- Average ---
        new Food { Name = "Pasta carbonara",       Emoji = "🍝", Tier = FoodTier.Average,
            Calories = 950, Protein = 35, Carbs = 110, Fat = 38,
            Description = "Hearty Italian comfort food." },
        new Food { Name = "Club sandwich",         Emoji = "🥪", Tier = FoodTier.Average,
            Calories = 850, Protein = 45, Carbs = 75, Fat = 30,
            Description = "Decent macros, reasonable choice." },
        new Food { Name = "Sushi platter",         Emoji = "🍣", Tier = FoodTier.Average,
            Calories = 800, Protein = 38, Carbs = 110, Fat = 18,
            Description = "Tasty, but the rice adds up." },
        new Food { Name = "Veggie burrito",        Emoji = "🌯", Tier = FoodTier.Average,
            Calories = 900, Protein = 28, Carbs = 120, Fat = 28,
            Description = "Filling, moderate macros." },

        // --- Healthy ---
        new Food { Name = "Grilled chicken bowl",  Emoji = "🍚", Tier = FoodTier.Healthy,
            Calories = 650, Protein = 55, Carbs = 65, Fat = 18,
            Description = "Classic gains meal. High protein." },
        new Food { Name = "Salmon & quinoa",       Emoji = "🐟", Tier = FoodTier.Healthy,
            Calories = 700, Protein = 50, Carbs = 55, Fat = 28,
            Description = "Omega-3s, complete protein, complex carbs." },
        new Food { Name = "Greek salad + feta",    Emoji = "🥗", Tier = FoodTier.Healthy,
            Calories = 550, Protein = 22, Carbs = 35, Fat = 32,
            Description = "Light, fresh, micronutrient rich." },
        new Food { Name = "Protein smoothie",      Emoji = "🥤", Tier = FoodTier.Healthy,
            Calories = 500, Protein = 45, Carbs = 50, Fat = 10,
            Description = "Berries, banana, whey, almond milk." },

        // --- Strict ---
        new Food { Name = "Tuna & egg whites",     Emoji = "🥚", Tier = FoodTier.Strict,
            Calories = 380, Protein = 55, Carbs = 8,  Fat = 14,
            Description = "Bodybuilder cut. Bland but lean." },
        new Food { Name = "Steamed cod + broccoli",Emoji = "🥦", Tier = FoodTier.Strict,
            Calories = 350, Protein = 50, Carbs = 18, Fat = 6,
            Description = "Stage-prep tier. Suffering, but shredded." },
        new Food { Name = "Cottage cheese bowl",   Emoji = "🍶", Tier = FoodTier.Strict,
            Calories = 320, Protein = 40, Carbs = 20, Fat = 6,
            Description = "Slow-digesting casein, very lean." },
        new Food { Name = "Skip a meal",           Emoji = "💧", Tier = FoodTier.Strict,
            Calories = 0,   Protein = 0,  Carbs = 0,  Fat = 0,
            Description = "Intermittent fast. Risky but cutting." }
    };
}
