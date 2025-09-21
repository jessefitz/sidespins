using SideSpins.Api.Models;

namespace SideSpins.Api.Services
{
    public interface IScoreRecomputeService
    {
        Task<PlayerMatch> RecomputePlayerMatchAsync(
            PlayerMatch playerMatch,
            IEnumerable<Game> games
        );
        Task<TeamMatch> RecomputeTeamMatchAsync(
            TeamMatch teamMatch,
            IEnumerable<PlayerMatch> playerMatches
        );
    }
}
