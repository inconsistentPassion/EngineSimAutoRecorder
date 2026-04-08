using System.Windows;
using System.Windows.Controls;

namespace EngineSimRecorder.Pages
{
    public partial class RecorderPage : Page
    {
        public RecorderPage()
        {
            InitializeComponent();
            PageContext.Recorder = this;
            Loaded += (s, e) => PageContext.RaiseRecorderLoaded();
        }
    }
}
