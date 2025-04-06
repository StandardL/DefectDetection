using DefectDetection.ViewModels;

using Microsoft.UI.Xaml.Controls;
using Windows.Media.Capture.Frames;
using Windows.Media.Capture;
using Microsoft.UI.Xaml;

namespace DefectDetection.Views;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel
    {
        get;
    }

    public MainPage()
    {
        ViewModel = App.GetService<MainViewModel>();
        InitializeComponent();

        StartCaptureElement();

        this.Unloaded += MainPage_Unloaded;
    }

    private async void StartCaptureElement()
    {
        var groups = await MediaFrameSourceGroup.FindAllAsync();
        if (groups.Count == 0)
        {
            ViewModel.strDetectInfo = "No camera devices found.";
            return;
        }
        ViewModel.mediaFrameSourceGroup = groups.First();

        ViewModel.strDetectInfo = "Viewing: " + ViewModel.mediaFrameSourceGroup.DisplayName;
        ViewModel.mediaCapture = new MediaCapture();
        var mediaCaptureInitializationSettings = new MediaCaptureInitializationSettings()
        {
            SourceGroup = ViewModel.mediaFrameSourceGroup,
            SharingMode = MediaCaptureSharingMode.SharedReadOnly,
            StreamingCaptureMode = StreamingCaptureMode.Video,
            MemoryPreference = MediaCaptureMemoryPreference.Cpu
        };
        await ViewModel.mediaCapture.InitializeAsync(mediaCaptureInitializationSettings);

        // Set the MediaPlayerElement's Source property to the MediaSource for the mediaCapture.
        var frameSource = ViewModel.mediaCapture.FrameSources[ViewModel.mediaFrameSourceGroup.SourceInfos[0].Id];
        MainPageCaptureElement.Source = Windows.Media.Core.MediaSource.CreateFromMediaFrameSource(frameSource);
    }

    private void MainPage_Unloaded(object sender, RoutedEventArgs e)
    {
        // Needs to run as task to unblock UI thread
        if (ViewModel.mediaCapture != null)
        {
            new Task(ViewModel.mediaCapture.Dispose).Start();
        }
    }

}
