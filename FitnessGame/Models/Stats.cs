namespace FitnessGame.Models;

public class Stats
{
    public int Day { get; set; } = 1;

    public double Energy { get; set; } = 80;        // 0..100
    public double Mood { get; set; } = 60;          // 0..100
    public double Health { get; set; } = 75;       // 0..100

    public double Weight { get; set; } = 62;        // kg
    public double BodyFat { get; set; } = 24;       // percent
    public double MuscleMass { get; set; } = 22;    // kg

    public double Strength { get; set; } = 30;     // 0..100
    public double Endurance { get; set; } = 30;    // 0..100
    public double Flexibility { get; set; } = 30;  // 0..100

    // Per-body-part muscle tone (0..100)
    public double ChestTone { get; set; } = 25;
    public double BackTone { get; set; } = 25;
    public double ArmsTone { get; set; } = 25;
    public double AbsTone { get; set; } = 25;
    public double LegsTone { get; set; } = 25;
    public double GluteTone { get; set; } = 30;

    public int Followers { get; set; } = 1200;     // Instagram followers

    public double Clamp(double v) => Math.Clamp(v, 0, 100);

    public void Normalize()
    {
        Energy = Math.Clamp(Energy, 0, 100);
        Mood = Math.Clamp(Mood, 0, 100);
        Health = Math.Clamp(Health, 0, 100);
        BodyFat = Math.Clamp(BodyFat, 6, 50);
        MuscleMass = Math.Clamp(MuscleMass, 10, 50);
        Weight = Math.Clamp(Weight, 40, 120);
        Strength = Math.Clamp(Strength, 0, 100);
        Endurance = Math.Clamp(Endurance, 0, 100);
        Flexibility = Math.Clamp(Flexibility, 0, 100);
        ChestTone = Clamp(ChestTone);
        BackTone = Clamp(BackTone);
        ArmsTone = Clamp(ArmsTone);
        AbsTone = Clamp(AbsTone);
        LegsTone = Clamp(LegsTone);
        GluteTone = Clamp(GluteTone);
    }

    public Stats Clone() => (Stats)MemberwiseClone();
}
