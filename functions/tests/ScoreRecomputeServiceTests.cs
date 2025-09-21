using SideSpins.Api.Models;
using SideSpins.Api.Services;

namespace SideSpins.Api.Tests
{
    // Unit test stub for ScoreRecomputeService happy paths
    public class ScoreRecomputeServiceTests
    {
        public void RecomputePlayerMatch_PointsOnly()
        {
            // TODO: Implement test for points-only recomputation
            // Arrange
            var featureFlags = new FeatureFlags(); // Default values
            var service = new ScoreRecomputeService(featureFlags);

            var playerMatch = new PlayerMatch
            {
                Id = "pm_test",
                DivisionId = "DIV123",
                TeamMatchId = "tm_test",
                HomePlayerId = "P1",
                AwayPlayerId = "P2",
                Order = 1,
            };

            var games = new List<Game>
            {
                new Game
                {
                    Id = "g1",
                    RackNumber = 1,
                    PointsHome = 2,
                    PointsAway = 1,
                    Winner = "home",
                },
                new Game
                {
                    Id = "g2",
                    RackNumber = 2,
                    PointsHome = 1,
                    PointsAway = 2,
                    Winner = "away",
                },
                new Game
                {
                    Id = "g3",
                    RackNumber = 3,
                    PointsHome = 3,
                    PointsAway = 0,
                    Winner = "home",
                },
            };

            // Act
            var result = service.RecomputePlayerMatchAsync(playerMatch, games).Result;

            // Assert
            if (result.PointsHome != 6)
                throw new Exception("Expected pointsHome = 6");
            if (result.PointsAway != 3)
                throw new Exception("Expected pointsAway = 3");
            if (result.TotalRacks != 3)
                throw new Exception("Expected totalRacks = 3");
            if (result.GamesWonHome != 2)
                throw new Exception("Expected gamesWonHome = 2");
            if (result.GamesWonAway != 1)
                throw new Exception("Expected gamesWonAway = 1");
        }

        public void RecomputeTeamMatch_PointsPriority()
        {
            // TODO: Implement test for team match recomputation with points priority
            var featureFlags = new FeatureFlags();
            var service = new ScoreRecomputeService(featureFlags);

            var teamMatch = new TeamMatch
            {
                Id = "tm_test",
                DivisionId = "DIV123",
                HomeTeamId = "TEAM_A",
                AwayTeamId = "TEAM_B",
            };

            var playerMatches = new List<PlayerMatch>
            {
                new PlayerMatch
                {
                    PointsHome = 6,
                    PointsAway = 3,
                    GamesWonHome = 2,
                    GamesWonAway = 1,
                },
                new PlayerMatch
                {
                    PointsHome = 4,
                    PointsAway = 5,
                    GamesWonHome = 1,
                    GamesWonAway = 2,
                },
            };

            // Act
            var result = service.RecomputeTeamMatchAsync(teamMatch, playerMatches).Result;

            // Assert: Should use points, not games won
            if (result.TeamScoreHome != 10)
                throw new Exception("Expected teamScoreHome = 10");
            if (result.TeamScoreAway != 8)
                throw new Exception("Expected teamScoreAway = 8");
        }

        public void RecomputeTeamMatch_GamesWonFallback()
        {
            // TODO: Implement test for fallback to gamesWon when no points
            var featureFlags = new FeatureFlags();
            var service = new ScoreRecomputeService(featureFlags);

            var teamMatch = new TeamMatch
            {
                Id = "tm_test",
                DivisionId = "DIV123",
                HomeTeamId = "TEAM_A",
                AwayTeamId = "TEAM_B",
            };

            // PlayerMatches with no points (all zero) should fall back to gamesWon
            var playerMatches = new List<PlayerMatch>
            {
                new PlayerMatch
                {
                    PointsHome = 0,
                    PointsAway = 0,
                    GamesWonHome = 3,
                    GamesWonAway = 2,
                },
                new PlayerMatch
                {
                    PointsHome = 0,
                    PointsAway = 0,
                    GamesWonHome = 2,
                    GamesWonAway = 3,
                },
            };

            // Act
            var result = service.RecomputeTeamMatchAsync(teamMatch, playerMatches).Result;

            // Assert: Should use gamesWon as fallback
            if (result.TeamScoreHome != 5)
                throw new Exception("Expected teamScoreHome = 5 (gamesWon fallback)");
            if (result.TeamScoreAway != 5)
                throw new Exception("Expected teamScoreAway = 5 (gamesWon fallback)");
        }
    }
}
