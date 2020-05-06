namespace Foralla.Scheduler
{
    /// <summary>
    ///     An internal helper interface to handle system jobs.
    /// </summary>
    internal interface ISystemJob
    {
        object Job { get; }
    }
}
