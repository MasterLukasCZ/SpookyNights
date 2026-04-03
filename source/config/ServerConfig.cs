using System.Collections.Generic;

namespace SpookyNights
{
    public class BossSpawningConfig
    {
        public bool Enabled { get; set; } = true;
        public List<string> AllowedMoonPhases { get; set; } = new List<string>();
    }

    public class ServerConfig
    {
        public string? Version { get; set; } = "1.7.2";

        // Global Toggle for Candy System
        public bool EnableCandyLoot { get; set; } = true;

        public List<int> AllowedCandyMonths { get; set; } = new List<int>();

        // Restrict candy to Full Moon nights only?
        public bool CandyOnlyOnFullMoon { get; set; } = false;

        public Dictionary<string, float> SpawnMultipliers { get; set; } = new Dictionary<string, float>();
        public bool UseTimeBasedSpawning { get; set; } = true;
        public bool SpawnOnlyAtNight { get; set; } = true;

        public string NightTimeMode { get; set; } = "Auto";
        public float NightStartHour { get; set; } = 20f;
        public float NightEndHour { get; set; } = 6f;
        public int LightLevelThreshold { get; set; } = 14;

        public List<int> AllowedSpawnMonths { get; set; } = new List<int>();
        public bool SpawnOnlyOnLastDayOfMonth { get; set; } = false;

        // Removed SpawnOnlyOnLastDayOfWeek

        public bool SpawnOnlyOnFullMoon { get; set; } = false;
        public float FullMoonSpawnMultiplier { get; set; } = 2.0f;
        public Dictionary<string, BossSpawningConfig> Bosses { get; set; } = new Dictionary<string, BossSpawningConfig>();

        public bool EnableDebugLogging { get; set; } = false;

        public ServerConfig()
        {
        }
    }
}