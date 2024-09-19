using System;
using Microsoft.Extensions.Logging;

namespace ProjectOrigin.Electricity.IntegrationTests;

public class CustomActionLogger : ILogger
{
    private readonly Action<string> _outputAction;
    private readonly string _categoryName;

    public CustomActionLogger(string categoryName, Action<string> outputAction)
    {
        _categoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));
        _outputAction = outputAction ?? throw new ArgumentNullException(nameof(outputAction));
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        string message = formatter(state, exception);
        if (!string.IsNullOrEmpty(message))
        {
            _outputAction($"{_categoryName}-{logLevel}: {message}");
        }
    }
}
