using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace PasswordstateOperator.Tests
{
    public class FakeLogger<T> : ILogger<T>
    {
        public List<(LogLevel level, string message)> Messages { get; } = new();

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            Messages.Add((logLevel, state.ToString()));
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }
    }
}