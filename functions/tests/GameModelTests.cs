using SideSpins.Api.Models;

namespace SideSpins.Api.Tests
{
    // Unit test stub for Game model validation
    public class GameModelTests
    {
        public void ValidateNonNegativePoints()
        {
            // TODO: Implement validation tests using Scoring utility
            // Test cases:
            // 1. Valid points (both >= 0) should not throw
            // 2. Negative pointsHome should throw ArgumentException
            // 3. Negative pointsAway should throw ArgumentException

            // Arrange & Act & Assert
            try
            {
                Scoring.ValidatePoints(5, nameof(Game.PointsHome));
                Scoring.ValidatePoints(0, nameof(Game.PointsAway));
                // Should not throw
            }
            catch
            {
                throw new Exception("Valid points should not throw exception");
            }

            // Test negative points
            try
            {
                Scoring.ValidatePoints(-1, nameof(Game.PointsHome));
                throw new Exception("Negative points should throw ArgumentException");
            }
            catch (ArgumentException)
            {
                // Expected
            }
        }

        public void ValidateRackNumber()
        {
            // TODO: Implement rack number validation
            try
            {
                Scoring.ValidateRackNumber(1, nameof(Game.RackNumber));
                Scoring.ValidateRackNumber(9, nameof(Game.RackNumber));
                // Should not throw
            }
            catch
            {
                throw new Exception("Valid rack numbers should not throw exception");
            }

            // Test invalid rack number
            try
            {
                Scoring.ValidateRackNumber(0, nameof(Game.RackNumber));
                throw new Exception("Zero rack number should throw ArgumentException");
            }
            catch (ArgumentException)
            {
                // Expected
            }
        }
    }
}
