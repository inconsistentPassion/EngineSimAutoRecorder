using System.Windows.Controls;

namespace EngineSimRecorder.View.Pages;

public partial class LogPage : Page
{
    public static LogPage? Instance { get; private set; }

    public LogPage()
    {
        InitializeComponent();
        Instance = this;
    }

    public void AppendLog(string line)
    {
        txtLog.AppendText(line + "\n");
        txtLog.ScrollToEnd();
    }

    public void Clear() => txtLog.Text = "";
}
