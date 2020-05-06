using System;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Foralla.Scheduler.IntegrationTest
{
    internal static class LoggerExtensions
    {
        /// <summary>
        ///     Verifies that the <paramref name="loggerMock" /> is called the number of times specified by
        ///     <paramref name="times" /> with the specified <paramref name="logLevel" /> and <paramref name="value" />.
        /// </summary>
        /// <typeparam name="TCategoryName">The <see cref="ILogger" />'s category type.</typeparam>
        /// <param name="loggerMock">The <see cref="Mock{T}" /> to configure</param>
        /// <param name="logLevel">The <see cref="LogLevel" /> to verify.</param>
        /// <param name="value">The value to verify.</param>
        /// <param name="times">The number of times a method is expected to be called.</param>
        public static void Verify<TCategoryName>(this Mock<ILogger<TCategoryName>> loggerMock, LogLevel logLevel, string value, Times times)
        {
            if (loggerMock == null)
            {
                throw new ArgumentNullException(nameof(loggerMock));
            }

            loggerMock.Verify(logger => logger.Log(logLevel, 0, It.Is<It.IsAnyType>((actual, t) => CompareLogStringValue(actual, value)),
                                                   It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
                              times);
        }

        /// <summary>
        ///     Verifies that the <paramref name="loggerMock" /> is called the number of times specified by
        ///     <paramref name="times" /> with the specified <paramref name="logLevel" /> and <paramref name="value" />.
        /// </summary>
        /// <typeparam name="TCategoryName">The <see cref="ILogger" />'s category type.</typeparam>
        /// <param name="loggerMock">The <see cref="Mock{T}" /> to configure</param>
        /// <param name="logLevel">The <see cref="LogLevel" /> to verify.</param>
        /// <param name="value">The value to verify.</param>
        /// <param name="times">The number of times a method is expected to be called.</param>
        public static void Verify<TCategoryName>(this Mock<ILogger<TCategoryName>> loggerMock, LogLevel logLevel, string value, Func<Times> times)
        {
            if (loggerMock == null)
            {
                throw new ArgumentNullException(nameof(loggerMock));
            }

            loggerMock.Verify(logger => logger.Log(logLevel, 0, It.Is<It.IsAnyType>((actual, t) => CompareLogStringValue(actual, value)),
                                                   It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
                              times);
        }

        /// <summary>
        ///     Verifies that the <paramref name="loggerMock" /> is called the number of times specified by
        ///     <paramref name="times" /> with the specified <paramref name="logLevel" /> and <paramref name="value" />.
        /// </summary>
        /// <typeparam name="TCategoryName">The <see cref="ILogger" />'s category type.</typeparam>
        /// <param name="loggerMock">The <see cref="Mock{T}" /> to configure</param>
        /// <param name="logLevel">The <see cref="LogLevel" /> to verify.</param>
        /// <param name="value">The value to verify.</param>
        /// <param name="exception">The exception to verify.</param>
        /// <param name="times">The number of times a method is expected to be called.</param>
        public static void Verify<TCategoryName>(this Mock<ILogger<TCategoryName>> loggerMock, LogLevel logLevel, string value, Exception exception, Func<Times> times)
        {
            if (loggerMock == null)
            {
                throw new ArgumentNullException(nameof(loggerMock));
            }

            loggerMock.Verify(logger => logger.Log(logLevel, 0, It.Is<It.IsAnyType>((actual, t) => CompareLogStringValue(actual, value)),
                                                   It.Is<Exception>((val, type) => CompareExceptions(val as Exception, exception)),
                                                   (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), times);
        }

        /// <summary>
        ///     Verifies that the <paramref name="loggerMock" /> is called the number of times specified by
        ///     <paramref name="times" /> with the specified <paramref name="logLevel" /> and <paramref name="value" />.
        /// </summary>
        /// <typeparam name="TCategoryName">The <see cref="ILogger" />'s category type.</typeparam>
        /// <param name="loggerMock">The <see cref="Mock{T}" /> to configure</param>
        /// <param name="logLevel">The <see cref="LogLevel" /> to verify.</param>
        /// <param name="value">The value to verify.</param>
        /// <param name="exception">The exception to verify.</param>
        /// <param name="times">The number of times a method is expected to be called.</param>
        public static void Verify<TCategoryName>(this Mock<ILogger<TCategoryName>> loggerMock, LogLevel logLevel, string value, Exception exception, Times times)
        {
            if (loggerMock == null)
            {
                throw new ArgumentNullException(nameof(loggerMock));
            }

            loggerMock.Verify(logger => logger.Log(logLevel, 0, It.Is<It.IsAnyType>((actual, t) => CompareLogStringValue(actual, value)),
                                                   It.Is<Exception>((val, type) => CompareExceptions(val as Exception, exception)),
                                                   (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), times);
        }

        private static bool CompareExceptions(Exception actual, Exception expected)
        {
            switch (actual)
            {
                case null when expected == null:
                    return true;
                case null:
                    return false;
            }

            return actual.GetType() == expected.GetType() && CompareLogStringValue(actual.Message, expected.Message);
        }

        private static bool CompareLogStringValue(object actual, string expected)
        {
            switch (actual)
            {
                case null when expected == null:
                    return true;
                case null:
                    return false;
            }

            if (expected == null)
            {
                return false;
            }

            var actualVal = actual.ToString();

            return actualVal.Equals(expected, StringComparison.InvariantCulture) || Regex.IsMatch(actualVal, expected);
        }
    }
}
