namespace SideSpins.Api.Services
{
    public interface ITimeProvider
    {
        DateTime UtcNow { get; }
    }
}
