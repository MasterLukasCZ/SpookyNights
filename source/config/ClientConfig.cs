namespace SpookyNights
{
    public class ClientConfig
    {
        public string Version { get; set; } = "1.7.2"; // Updated Version
        public bool EnableJackOLanternParticles { get; set; } = true;
        public bool EnableBossWarningSound { get; set; } = true;
        public double BossWarningMaxRange { get; set; } = 35.0;
        public double BossWarningMinRange { get; set; } = 10.0;

        public ClientConfig() { }
    }
}