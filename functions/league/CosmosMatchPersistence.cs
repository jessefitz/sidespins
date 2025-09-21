using SideSpins.Api.Models;

namespace SideSpins.Api.Services
{
    public class CosmosMatchPersistence : IMatchPersistence
    {
        private readonly LeagueService _leagueService;

        public CosmosMatchPersistence(LeagueService leagueService)
        {
            _leagueService = leagueService;
        }

        // TeamMatch operations
        public async Task<TeamMatch> CreateTeamMatchAsync(TeamMatch teamMatch)
        {
            return await _leagueService.CreateMatchAsync(teamMatch);
        }

        public async Task<TeamMatch?> GetTeamMatchByIdAsync(string id, string divisionId)
        {
            return await _leagueService.GetMatchByIdAsync(id, divisionId);
        }

        public async Task<TeamMatch?> UpdateTeamMatchAsync(TeamMatch teamMatch)
        {
            return await _leagueService.UpdateMatchAsync(
                teamMatch.Id,
                teamMatch.DivisionId,
                teamMatch
            );
        }

        // PlayerMatch operations
        public async Task<PlayerMatch> CreatePlayerMatchAsync(PlayerMatch playerMatch)
        {
            return await _leagueService.CreatePlayerMatchAsync(playerMatch);
        }

        public async Task<PlayerMatch?> GetPlayerMatchByIdAsync(string id, string divisionId)
        {
            return await _leagueService.GetPlayerMatchByIdAsync(id, divisionId);
        }

        public async Task<IEnumerable<PlayerMatch>> GetPlayerMatchesByTeamMatchIdAsync(
            string teamMatchId,
            string divisionId
        )
        {
            return await _leagueService.GetPlayerMatchesByTeamMatchIdAsync(teamMatchId, divisionId);
        }

        public async Task<PlayerMatch?> UpdatePlayerMatchAsync(PlayerMatch playerMatch)
        {
            return await _leagueService.UpdatePlayerMatchAsync(playerMatch);
        }

        // Game operations
        public async Task<Game> CreateGameAsync(Game game)
        {
            return await _leagueService.CreateGameAsync(game);
        }

        public async Task<IEnumerable<Game>> GetGamesByPlayerMatchIdAsync(
            string playerMatchId,
            string divisionId
        )
        {
            return await _leagueService.GetGamesByPlayerMatchIdAsync(playerMatchId, divisionId);
        }

        // Batch operations
        public async Task<(PlayerMatch playerMatch, List<Game> games)> AddPlayerMatchBatchAsync(
            PlayerMatch playerMatch,
            List<Game> games
        )
        {
            return await _leagueService.AddPlayerMatchBatchAsync(playerMatch, games);
        }

        public async Task<Game> AddGameBatchAsync(Game game, PlayerMatch playerMatch)
        {
            return await _leagueService.AddGameBatchAsync(game, playerMatch);
        }
    }
}
