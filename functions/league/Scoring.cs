namespace SideSpins.Api.Models
{
    public enum RackWinner
    {
        Home,
        Away,
    }

    public static class Scoring
    {
        public static void ValidatePoints(int points, string paramName)
        {
            if (points < 0)
            {
                throw new ArgumentException($"Points must be non-negative", paramName);
            }
        }

        public static void ValidateRackNumber(int rackNumber, string paramName)
        {
            if (rackNumber < 1)
            {
                throw new ArgumentException($"Rack number must be greater than 0", paramName);
            }
        }
    }
}
