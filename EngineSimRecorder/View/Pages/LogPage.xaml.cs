using System.Collections.Generic;
using System.Windows.Controls;

namespace EngineSimRecorder.View.Pages;

public partial class LogPage : Page
{
    public static LogPage? Instance { get; private set; }

    // Buffer for messages logged before LogPage is created
    private static readonly List<string> _buffer = new();
    private static readonly object _bufferLock = new();
    private static bool _bufferCleared;

    public LogPage()
    {
        InitializeComponent();
        Instance = this;

        // Flush any buffered messages
        lock (_bufferLock)
        {
            if (!_bufferCleared)
            {
                foreach (var line in _buffer)
                    txtLog.AppendText(line + "\n");
                if (_buffer.Count > 0)
                    txtLog.ScrollToEnd();
            }
        }
    }

    public void AppendLog(string line)
    {
        txtLog.AppendText(line + "\n");
        txtLog.ScrollToEnd();
    }

    /// <summary>
    /// Add a message to the buffer (call when LogPage.Instance is null).
    /// Messages will appear when the page is first navigated to.
    /// </summary>
    public static void AppendBuffered(string line)
    {
        lock (_bufferLock) { _buffer.Add(line); }
    }

    public void Clear()
    {
        txtLog.Text = "";
        lock (_bufferLock) { _buffer.Clear(); _bufferCleared = true; }
    }

    public static void ClearBuffer()
    {
        lock (_bufferLock) { _buffer.Clear(); _bufferCleared = true; }
    }
}
