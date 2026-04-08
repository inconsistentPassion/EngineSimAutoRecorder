using Wpf.Ui.Controls;

namespace EngineSimRecorder.Pages
{
    public partial class LogPage : UiPage
    {
        public LogPage() => InitializeComponent();

        public void AppendLog(string line)
        {
            txtLog.AppendText(line + "\n");
            txtLog.ScrollToEnd();
        }

        public void Clear() => txtLog.Text = "";
    }
}
