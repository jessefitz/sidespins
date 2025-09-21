namespace SideSpins.Api.Services
{
    public class FeatureFlags
    {
        public bool AllowSecretMutations { get; }
        public bool DisableApiSecretMutations { get; }
        public bool DisableGamesWonFallback { get; }
        public bool MatchManagementEnabled { get; }

        public FeatureFlags()
        {
            AllowSecretMutations = GetBoolEnvironmentVariable("ALLOW_SECRET_MUTATIONS", true);
            DisableApiSecretMutations = GetBoolEnvironmentVariable(
                "DISABLE_API_SECRET_MUTATIONS",
                false
            );
            DisableGamesWonFallback = GetBoolEnvironmentVariable(
                "DISABLE_GAMESWON_FALLBACK",
                false
            );
            MatchManagementEnabled = GetBoolEnvironmentVariable("MATCH_MANAGEMENT_ENABLED", true);
        }

        private static bool GetBoolEnvironmentVariable(string name, bool defaultValue)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            return bool.TryParse(value, out var result) ? result : defaultValue;
        }
    }
}
