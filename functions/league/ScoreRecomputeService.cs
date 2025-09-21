using SideSpins.Api.Models;

namespace SideSpins.Api.Services
{
    public class ScoreRecomputeService : IScoreRecomputeService
    {
        private readonly FeatureFlags _featureFlags;

        public ScoreRecomputeService(FeatureFlags featureFlags)
        {
            _featureFlags = featureFlags;
        }

        public Task<PlayerMatch> RecomputePlayerMatchAsync(
            PlayerMatch playerMatch,
            IEnumerable<Game> games
        )
        {
            // Recompute cumulative scores from games
            var gamesList = games.ToList();

            playerMatch.PointsHome = gamesList.Sum(g => g.PointsHome);
            playerMatch.PointsAway = gamesList.Sum(g => g.PointsAway);
            playerMatch.TotalRacks = gamesList.Count;

            // Legacy gamesWon computation (for backward compatibility)
            playerMatch.GamesWonHome = gamesList.Count(g => g.Winner == "home");
            playerMatch.GamesWonAway = gamesList.Count(g => g.Winner == "away");

            playerMatch.UpdatedUtc = DateTime.UtcNow;

            return Task.FromResult(playerMatch);
        }

        public Task<TeamMatch> RecomputeTeamMatchAsync(
            TeamMatch teamMatch,
            IEnumerable<PlayerMatch> playerMatches
        )
        {
            var playerMatchesList = playerMatches.ToList();

            // Priority: Use points if any PlayerMatch has points > 0
            var hasPoints = playerMatchesList.Any(pm => pm.PointsHome > 0 || pm.PointsAway > 0);

            if (hasPoints)
            {
                // Points-based scoring (primary)
                teamMatch.TeamScoreHome = playerMatchesList.Sum(pm => pm.PointsHome);
                teamMatch.TeamScoreAway = playerMatchesList.Sum(pm => pm.PointsAway);
            }
            else if (!_featureFlags.DisableGamesWonFallback)
            {
                // Fallback to gamesWon aggregation (legacy 8-ball style)
                teamMatch.TeamScoreHome = playerMatchesList.Sum(pm => pm.GamesWonHome);
                teamMatch.TeamScoreAway = playerMatchesList.Sum(pm => pm.GamesWonAway);
            }
            else
            {
                // No fallback allowed, keep at zero
                teamMatch.TeamScoreHome = 0;
                teamMatch.TeamScoreAway = 0;
            }

            return Task.FromResult(teamMatch);
        }
    }
}
