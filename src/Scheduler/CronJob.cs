using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Cronos;

namespace Foralla.Scheduler
{
    /// <summary>
    ///     Base class that supports cron scheduling expressions.
    /// </summary>
    public abstract class CronJob : IJob
    {
        private DateTimeOffset? _nextScheduledTime;

        /// <summary>
        ///     Gets the latest start time. No end time exists if null.
        /// </summary>
        public virtual DateTimeOffset? DontStartAfter { get; } = null;

        /// <summary>
        ///     Gets the earliest start time for this job. No start time exists if null.
        /// </summary>
        public virtual DateTimeOffset? DontStartBefore { get; } = null;

        /// <summary>
        ///     Gets the cron expression to use.
        /// </summary>
        public abstract string Expression { get; }

        /// <summary>
        ///     Gets the job name.
        /// </summary>
        /// <remarks>
        ///     This must be unique for the host
        /// </remarks>
        public abstract string Name { get; }

        /// <summary>
        ///     Gets the next scheduled execution time.
        /// </summary>
        /// <remarks>The next scheduled time is in local time for the host.</remarks>
        public DateTimeOffset? NextScheduledTime
        {
            get
            {
                if ((_nextScheduledTime == null || _nextScheduledTime.Value < InternalNow()) &&
                    !string.IsNullOrWhiteSpace(Expression))
                {
                    _nextScheduledTime = CronExpression.Parse(Expression, UseSeconds ? CronFormat.IncludeSeconds : CronFormat.Standard)
                                                       .GetNextOccurrence(InternalNow(), TimeZoneInfo.Local, true);

                    if (_nextScheduledTime < (DontStartBefore ?? InternalNow()) ||
                        _nextScheduledTime >= (DontStartAfter ?? DateTimeOffset.MaxValue))
                    {
                        _nextScheduledTime = null;
                    }
                }

                return _nextScheduledTime;
            }
        }

        /// <summary>
        ///     Gets if the cron expression uses seconds.
        /// </summary>
        public virtual bool UseSeconds { get; } = true;

        /// <summary>
        ///     Internal helper that can be overridden by unit tests to have a fixed DateTimeOffset for testing.
        /// </summary>
        [ExcludeFromCodeCoverage]
        internal Func<DateTimeOffset> InternalNow { get; set; } = () => DateTimeOffset.Now;

        public abstract Task ExecuteAsync(CancellationToken stoppingToken);
    }
}
