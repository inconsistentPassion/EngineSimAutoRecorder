using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace EngineSimRecorder.View.Pages;

public partial class LogPage : Page
{
    public static LogPage? Instance { get; private set; }

    // Buffer for messages logged before LogPage is created
    private static readonly List<string> _buffer = new();
    private static readonly object _bufferLock = new();
    private static bool _bufferCleared;

    private readonly ISnackbarService _snackbarService;

    public LogPage()
    {
        InitializeComponent();
        Instance = this;
        _snackbarService = App.GetService<ISnackbarService>();

        // Wire up buttons
        btnCopyLog.Click += BtnCopyLog_Click;
        btnClearLog.Click += (s, e) => Clear();

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

    private void BtnCopyLog_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(txtLog.Text))
        {
            _snackbarService.Show("Nothing to Copy", "Log is empty.", ControlAppearance.Info, new SymbolIcon(SymbolRegular.Info24), TimeSpan.FromSeconds(3));
            return;
        }

        try
        {
            Clipboard.SetText(txtLog.Text);
            _snackbarService.Show("Copied", "Log copied to clipboard!", ControlAppearance.Success, new SymbolIcon(SymbolRegular.Checkmark24), TimeSpan.FromSeconds(3));
        }
        catch (Exception ex)
        {
            _snackbarService.Show("Error", $"Failed to copy: {ex.Message}", ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
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
        Dispatcher.BeginInvoke(() =>
        {
            txtLog.Text = "";
        });
        lock (_bufferLock) { _buffer.Clear(); _bufferCleared = true; }
    }

    public static void ClearBuffer()
    {
        lock (_bufferLock) { _buffer.Clear(); _bufferCleared = true; }
    }
}
