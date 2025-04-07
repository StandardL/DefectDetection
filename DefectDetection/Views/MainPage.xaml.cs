using DefectDetection.ViewModels;

using Microsoft.UI.Xaml.Controls;
using Windows.Media.Capture.Frames;
using Windows.Media.Capture;
using Microsoft.UI.Xaml;
using System.Collections.ObjectModel;
using Windows.Globalization.NumberFormatting;

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

        ConfigureNumberBoxFormat();
        StartCaptureElement();

        this.Unloaded += MainPage_Unloaded;
    }

    private async void StartCaptureElement()
    {
        var groups = await MediaFrameSourceGroup.FindAllAsync();
        ViewModel.sourceGroups = new(groups);
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

    private void ConfigureNumberBoxFormat()
    {
        var formatter = new DecimalFormatter
        {
            FractionDigits = 2,
            IntegerDigits = 1,    // 整数部分最少显示1位（避免显示为 .50）
            IsGrouped = false
        };
        var rounder = new IncrementNumberRounder
        {
            Increment = 0.01,
            RoundingAlgorithm = RoundingAlgorithm.RoundHalfUp  // 四舍五入算法
        };
        formatter.NumberRounder = rounder;

        MainPageConfNumberBox.NumberFormatter = formatter;
        MainPageIoUNumberBox.NumberFormatter = formatter;
    }

}
