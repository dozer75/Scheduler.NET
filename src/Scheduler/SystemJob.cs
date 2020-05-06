namespace Foralla.Scheduler
{
    /// <summary>
    ///     An internal helper class to handle system jobs.
    /// </summary>
    /// <typeparam name="TJob">The type of system job.</typeparam>
    internal class SystemJob<TJob> : ISystemJob
        where TJob : IJob
    {
        /// <summary>
        ///     The system job.
        /// </summary>
        /// <remarks>
        ///     Since it returns a <see cref="object" /> (due to generic limitation) it must be converted to an
        ///     <see cref="IJob" />.
        /// </remarks>
        public object Job { get; }

        public SystemJob(TJob job)
        {
            Job = job;
        }
    }
}
