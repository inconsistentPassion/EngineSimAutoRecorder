using System.Windows.Controls;
using EngineSimRecorder.ViewModel.Pages;

namespace EngineSimRecorder.View.Pages;

public partial class RecorderPage : Page
{
    public RecorderPage(RecorderPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Initialize(this);
    }
}
