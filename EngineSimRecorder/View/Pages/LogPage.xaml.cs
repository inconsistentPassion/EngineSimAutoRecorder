using System.Windows.Controls;

namespace EngineSimRecorder.View.Pages;

public partial class LogPage : Page
{
    public LogPage() => InitializeComponent();

    public void AppendLog(string line)
    {
        txtLog.AppendText(line + "\n");
        txtLog.ScrollToEnd();
    }

    public void Clear() => txtLog.Text = "";
}
