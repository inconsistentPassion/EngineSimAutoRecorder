using System.Collections.Generic;
using System.Windows.Controls;

namespace EngineSimRecorder.View.Pages;

public partial class LogPage : Page
{
    public static LogPage? Instance { get; private set; }

    // Buffer for messages logged before LogPage is created
    private static readonly List<string> _buffer = new();
    private static bool _bufferCleared;

    public LogPage()
    {
        InitializeComponent();
        Instance = this;

        // Flush any buffered messages
        if (_bufferCleared)
        {
            _bufferCleared = false;
        }
        else
        {
            foreach (var line in _buffer)
                txtLog.AppendText(line + "\n");
            if (_buffer.Count > 0)
                txtLog.ScrollToEnd();
        }
    }

    public void AppendLog(string line)
    {
        if (txtLog != null)
        {
            txtLog.AppendText(line + "\n");
            txtLog.ScrollToEnd();
        }
        // Always buffer too, so messages survive page recreation
        _buffer.Add(line);
    }

    /// <summary>
    /// Add a message to the buffer (call when LogPage.Instance is null).
    /// Messages will appear when the page is first navigated to.
    /// </summary>
    public static void AppendBuffered(string line)
    {
        _buffer.Add(line);
    }

    public static void ClearBuffer()
    {
        _buffer.Clear();
        _bufferCleared = true;
    }

    public void Clear()
    {
        txtLog.Text = "";
        _buffer.Clear();
        _bufferCleared = true;
    }
}
