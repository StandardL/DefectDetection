using CommunityToolkit.Mvvm.ComponentModel;
using Windows.Media.Capture.Frames;
using Windows.Media.Capture;
using System.Collections.ObjectModel;

namespace DefectDetection.ViewModels;

public partial class MainViewModel : ObservableRecipient
{
    public MainViewModel()
    {
        strDetectInfo = string.Empty;
        mediaCapture = new MediaCapture();
    }

    public string strDetectInfo
    {
        get;
        set;
    }

    public MediaFrameSourceGroup mediaFrameSourceGroup
    {
        get;
        set;
    }
    public MediaCapture mediaCapture
    {
        get;
        set;
    }

    public ObservableCollection<MediaFrameSourceGroup> sourceGroups
    {
        get;
        set;
    } = new();

    [ObservableProperty]
    private double dConfRate = 0.30f;

    [ObservableProperty]
    private double dIouRate = 0.50f;
}