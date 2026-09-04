namespace TeamCityRemoteMcpServer.Tests;

/// <summary>
/// Captures every log line written through the process-global Serilog.Log during this capture's
/// lifetime. This covers both logging pipelines in play under Auth/: the static Log.X(...) calls
/// in Program.cs, and every ILogger&lt;T&gt; call elsewhere, because Program.cs's
/// builder.Services.AddSerilog() bridges Microsoft.Extensions.Logging into the same static
/// Log.Logger. Swapping Log.Logger is process-global, which is a second, independent reason (recorded
/// in AssemblyInfo.cs alongside the crypto-provider one) that this assembly must keep
/// DisableTestParallelization. Runs at Verbose deliberately: the strongest form of a redaction
/// assertion is "even with every level enabled, none of these strings appears."
/// </summary>
public sealed class LogCapture : IDisposable
{
    private readonly List<string> _lines = [];
    private readonly object _gate = new();

    public LogCapture()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new CapturingSink(this))
            .CreateLogger();
    }

    public IReadOnlyList<string> Lines
    {
        get { lock (_gate) { return _lines.ToArray(); } }
    }

    /// <summary>All captured lines joined for substring assertions — a redacted value that leaks
    /// as part of a larger rendered line (not as a standalone list element) still needs to be
    /// findable.</summary>
    public string Text
    {
        get { lock (_gate) { return string.Join('\n', _lines); } }
    }

    public void Dispose()
    {
        Log.CloseAndFlush();
        Log.Logger = Serilog.Core.Logger.None;
    }

    private void AddLine(string? line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        lock (_gate)
        {
            _lines.Add(line);
        }
    }

    private sealed class CapturingSink : Serilog.Core.ILogEventSink
    {
        private readonly LogCapture _capture;

        public CapturingSink(LogCapture capture) => _capture = capture;

        public void Emit(Serilog.Events.LogEvent logEvent)
        {
            // Exclude ASP.NET Core's own framework diagnostics (Kestrel/hosting/routing request
            // logging, which echoes the full request path+query at Information/Debug and would
            // otherwise make every public, non-secret query parameter — code_challenge, state,
            // scope — look like a "leak" regardless of anything TeamCityRemoteMcpServer's own code
            // does). Lines with no SourceContext at all (Program.cs's raw static Log.X calls) still
            // pass through; anything under our own namespace still passes through.
            if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContext) &&
                !sourceContext.ToString().Contains("TeamCityRemoteMcpServer", StringComparison.Ordinal))
            {
                return;
            }

            _capture.AddLine(logEvent.RenderMessage());

            // Render every structured property individually — a Log.Debug("{@Entry}", entry)
            // destructuring leak never appears in the rendered message alone.
            foreach (var property in logEvent.Properties.Values)
            {
                _capture.AddLine(property.ToString());
            }

            // Deliberately captured, not skipped: if production code ever attaches an exception
            // object directly to a log call (instead of only its type name, per the never-log-
            // Message rule), this is what lets the redaction assertions actually catch it rather
            // than passing vacuously.
            if (logEvent.Exception is not null)
            {
                _capture.AddLine(logEvent.Exception.GetType().Name);
                _capture.AddLine(logEvent.Exception.Message);
            }
        }
    }
}
