// ReSharper disable AccessToModifiedClosure

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Cronos;
using Xunit;

namespace Foralla.Scheduler.Test
{
    public class CronJobTest
    {
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(2, false)]
        [InlineData(3, true)]
        [InlineData(4, true)]
        [InlineData(5, false)]
        [InlineData(6, false)]
        [InlineData(7, false)]
        public void TestGetNextScheduledTimeBetweenRestrictions(int second, bool isValid)
        {
            var now = DateTimeOffset.Now;
            now = now.AddMilliseconds(-now.Millisecond);

            var cron = new CronJobHelper(now.AddSeconds(3), now.AddSeconds(5))
                       {
                           InternalNow = () => now
                       };

            now = now.AddSeconds(second);

            if (isValid)
            {
                Assert.NotNull(cron.NextScheduledTime);
            }
            else
            {
                Assert.Null(cron.NextScheduledTime);
            }
        }

        [ExcludeFromCodeCoverage]
        private class CronJobHelper : CronJob
        {
            private readonly bool _emptyExpression;
            public override DateTimeOffset? DontStartAfter { get; }

            public override DateTimeOffset? DontStartBefore { get; }
            public override string Expression => _emptyExpression ? null : "*/1 * * * * *";
            public override string Name => nameof(CronJobHelper);

            public CronJobHelper(DateTimeOffset? dontStartBefore = null, DateTimeOffset? dontStartAfter = null, bool emptyExpression = false)
            {
                _emptyExpression = emptyExpression;
                DontStartBefore = dontStartBefore;
                DontStartAfter = dontStartAfter;
            }

            public override Task ExecuteAsync(CancellationToken stoppingToken)
            {
                return Task.CompletedTask;
            }
        }

        [Theory]
        [InlineData("*/30 * * * *", false)]
        [InlineData("*/30 * * * * *", true)]
        public void TestIssue2TryValidateExpression(string expression, bool result)
        {
            Assert.Equal(result, new CronJobHelper().TryValidateExpression(expression));
        }

        [Fact]
        public void TestGetNextScheduledTimeMultiple()
        {
            var now = DateTimeOffset.Now;

            var cron = new CronJobHelper
                       {
                           InternalNow = () => now
                       };

            var nextScheduledTime1 = cron.NextScheduledTime;

            Assert.NotNull(nextScheduledTime1);
            Assert.Equal(now.Second + 1, nextScheduledTime1.Value.Second);

            now = nextScheduledTime1.Value.AddMilliseconds(1);
            var nextScheduledTime2 = cron.NextScheduledTime;

            Assert.NotNull(nextScheduledTime2);
            Assert.Equal(nextScheduledTime1.Value.Second + 1, nextScheduledTime2.Value.Second);
        }

        [Fact]
        public void TestGetNextScheduledTimeNotAfterRestrictionReturnsNull()
        {
            var cron = new CronJobHelper(dontStartAfter: DateTimeOffset.Now.AddSeconds(-10));

            Assert.Null(cron.NextScheduledTime);
        }

        [Fact]
        public void TestGetNextScheduledTimeNotBeforeRestrictionReturnsNull()
        {
            var cron = new CronJobHelper(DateTimeOffset.Now.AddSeconds(10));

            Assert.Null(cron.NextScheduledTime);
        }

        [Fact]
        public void TestGetNextScheduledTimeReturnsNullOnEmptyExpression()
        {
            var cron = new CronJobHelper(emptyExpression: true);

            Assert.Null(cron.NextScheduledTime);
        }

        [Fact]
        public void TestGetNextScheduledTimeSingle()
        {
            var now = DateTimeOffset.Now;

            var cron = new CronJobHelper
                       {
                           InternalNow = () => now
                       };

            var nextScheduledTime = cron.NextScheduledTime;

            Assert.NotNull(nextScheduledTime);
            Assert.Equal(now.Second + 1, nextScheduledTime.Value.Second);
        }

        [Fact]
        public void TestIssue2ValidateExpressionSucceedsOnValidExpression()
        {
            new CronJobHelper().ValidateExpression("*/30 * * * * *");
        }

        [Fact]
        public void TestIssue2ValidateExpressionThrowsOnInvalidExpression()
        {
            Assert.Throws<CronFormatException>(() => new CronJobHelper().ValidateExpression("*/30 * * * *"));
        }
    }
}
