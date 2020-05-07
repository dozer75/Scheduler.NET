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

        /// <summary>
        ///     Validates the <paramref name="expression" /> is a valid cron expression.
        /// </summary>
        /// <param name="expression">The expression to validate.</param>
        /// <returns>True if it is, otherwise false.</returns>
        protected internal bool TryValidateExpression(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return false;
            }

            try
            {
                ValidateExpression(expression);

                return true;
            }
            catch (CronFormatException)
            {
                return false;
            }
        }

        /// <summary>
        ///     Validates the <paramref name="expression" /> is a valid cron expression.
        /// </summary>
        /// <param name="expression">The expression to validate.</param>
        /// <exception cref="CronFormatException">Thrown if the <paramref name="expression" /> isn't a valid cron expression.</exception>
        protected internal void ValidateExpression(string expression)
        {
            CronExpression.Parse(expression, UseSeconds ? CronFormat.IncludeSeconds : CronFormat.Standard);
        }
    }
}
