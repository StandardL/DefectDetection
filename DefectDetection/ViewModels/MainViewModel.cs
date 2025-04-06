using CommunityToolkit.Mvvm.ComponentModel;
using Windows.Media.Capture.Frames;
using Windows.Media.Capture;

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
}
