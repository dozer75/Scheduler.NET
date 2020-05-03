﻿using System.Collections.Generic;
using System.Threading.Tasks;

namespace Foralla.Scheduler
{
    /// <summary>
    ///     Interface for managing jobs to schedule.
    /// </summary>
    public interface ISchedulerManager
    {
        /// <summary>
        ///     Gets a <see cref="IEnumerable{T}" /> of <see cref="IJob" /> for all dynamic jobs.
        /// </summary>
        IEnumerable<IJob> Jobs { get; }

        /// <summary>
        ///     Get a <see cref="IEnumerable{T}" /> of <see cref="IJob" /> for all system jobs.
        /// </summary>
        IEnumerable<IJob> SystemJobs { get; }

        /// <summary>
        ///     Adds the <paramref name="job" /> to the scheduler.
        /// </summary>
        /// <param name="job">The job to add.</param>
        /// <returns>If the job already exists it returns false, otherwise true.</returns>
        /// <remarks>
        ///     The <see cref="IJob.Name" /> is used to check if the job already exist, so the
        ///     <see cref="IJob.Name" /> must be unique.
        /// </remarks>
        bool AddJob(IJob job);

        /// <summary>
        ///     Removes the job with the specified <paramref name="name" />.
        /// </summary>
        /// <param name="name">The name of the job to remove.</param>
        /// <returns>Returns false if the job is a system job or doesn't exist.</returns>
        Task<bool> RemoveJobAsync(string name);
    }
}
