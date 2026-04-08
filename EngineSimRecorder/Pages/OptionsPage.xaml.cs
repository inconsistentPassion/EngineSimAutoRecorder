using System.Windows;
using System.Windows.Controls;

namespace EngineSimRecorder.Pages
{
    public partial class OptionsPage : Page
    {
        public OptionsPage()
        {
            InitializeComponent();
            PageContext.Options = this;
            Loaded += (s, e) => PageContext.RaiseOptionsLoaded();
        }
    }
}
