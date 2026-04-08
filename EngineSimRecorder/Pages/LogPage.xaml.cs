using System.Windows.Controls;

namespace EngineSimRecorder.Pages
{
    public partial class LogPage : Page
    {
        public LogPage()
        {
            InitializeComponent();
            PageContext.Log = this;
        }

        public void AppendLog(string line)
        {
            txtLog.AppendText(line + "\n");
            txtLog.ScrollToEnd();
        }

        public void Clear() => txtLog.Text = "";
    }
}
