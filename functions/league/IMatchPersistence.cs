using SideSpins.Api.Models;

namespace SideSpins.Api.Services
{
    public interface IMatchPersistence
    {
        // TeamMatch operations
        Task<TeamMatch> CreateTeamMatchAsync(TeamMatch teamMatch);
        Task<TeamMatch?> GetTeamMatchByIdAsync(string id, string divisionId);
        Task<TeamMatch?> UpdateTeamMatchAsync(TeamMatch teamMatch);

        // PlayerMatch operations
        Task<PlayerMatch> CreatePlayerMatchAsync(PlayerMatch playerMatch);
        Task<PlayerMatch?> GetPlayerMatchByIdAsync(string id, string divisionId);
        Task<IEnumerable<PlayerMatch>> GetPlayerMatchesByTeamMatchIdAsync(
            string teamMatchId,
            string divisionId
        );
        Task<PlayerMatch?> UpdatePlayerMatchAsync(PlayerMatch playerMatch);

        // Game operations
        Task<Game> CreateGameAsync(Game game);
        Task<IEnumerable<Game>> GetGamesByPlayerMatchIdAsync(
            string playerMatchId,
            string divisionId
        );

        // Batch operations
        Task<(PlayerMatch playerMatch, List<Game> games)> AddPlayerMatchBatchAsync(
            PlayerMatch playerMatch,
            List<Game> games
        );
        Task<Game> AddGameBatchAsync(Game game, PlayerMatch playerMatch);
    }
}
