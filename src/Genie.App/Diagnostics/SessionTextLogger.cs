using System.Collections.Specialized;
using System.IO;
using Genie.App.ViewModels;

namespace Genie.App.Diagnostics;

/// <summary>
/// Automatic rendered-text session log — the Genie 4 <c>AutoLog</c> feature.
/// Writes every line shown in the game window (game text, command echoes,
/// script / system lines) to a plain-text file under <c>{AppData}/Genie5/Logs/</c>,
/// gated by <c>GenieConfig.AutoLog</c> and started/stopped on connect/disconnect.
///
/// <para>Genie 4 parity: the filename is <c>{Character}{Game}_{yyyy-MM-dd}.log</c>
/// and opened in <b>append</b> mode — one file per character/game/day, so
/// multiple sessions on the same day concatenate. AutoFlush so a crash leaves
/// the captured prefix on disk.</para>
///
/// <para>Sibling of <see cref="SessionRecorder"/> (which captures the raw XML
/// stream on a manual toggle); this one captures the <i>rendered</i> text
/// automatically. Same local-only policy stance — recording the user's own
/// client stream locally is fine; external transmission is gated elsewhere.</para>
/// </summary>
public sealed class SessionTextLogger : IDisposable
{
    private readonly string _logsDir;
    private StreamWriter?   _writer;
    private GameTextViewModel? _source;
    private NotifyCollectionChangedEventHandler? _handler;
    private string? _currentFile;

    public SessionTextLogger(string logsDir)
    {
        _logsDir = logsDir;
        Directory.CreateDirectory(_logsDir);
    }

    /// <summary>True while a log file is open and subscribed.</summary>
    public bool    IsLogging   => _writer is not null;
    /// <summary>Absolute path of the open log file, or null when stopped.</summary>
    public string? CurrentFile => _currentFile;

    /// <summary>
    /// Begin logging the rendered lines from <paramref name="source"/> to the
    /// per-character/day file. Idempotent — a prior log is closed first.
    /// </summary>
    public void Start(GameTextViewModel source, string characterName, string gameName)
    {
        Stop();

        var safeChar = Sanitize(characterName, "unknown");
        var safeGame = Sanitize(gameName, string.Empty);
        _currentFile = Path.Combine(
            _logsDir, $"{safeChar}{safeGame}_{DateTime.Now:yyyy-MM-dd}.log");

        _writer = new StreamWriter(_currentFile, append: true) { AutoFlush = true };
        _source = source;
        _handler = (_, e) =>
        {
            if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems is null) return;
            foreach (TextLine line in e.NewItems)
            {
                try { _writer?.WriteLine(line.Text); }
                catch (Exception ex) { ErrorLog.Log("SessionTextLogger.Write", ex); }
            }
        };
        source.Lines.CollectionChanged += _handler;
    }

    /// <summary>Stop logging and close the file. Safe to call when not logging.</summary>
    public void Stop()
    {
        if (_source is not null && _handler is not null)
            _source.Lines.CollectionChanged -= _handler;
        _handler = null;
        _source  = null;

        try { _writer?.Dispose(); }
        catch (Exception ex) { ErrorLog.Log("SessionTextLogger.CloseFile", ex); }
        _writer = null;
        _currentFile = null;
    }

    public void Dispose() => Stop();

    private static string Sanitize(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        foreach (var c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return value;
    }
}
