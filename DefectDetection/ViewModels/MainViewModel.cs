using CommunityToolkit.Mvvm.ComponentModel;
using Windows.Media.Capture.Frames;
using Windows.Media.Capture;
using System.Collections.ObjectModel;
using DefectDetection.Helpers;

namespace DefectDetection.ViewModels;

public partial class MainViewModel : ObservableRecipient
{
    private ModelHelper _modelHelper = new();
    public MainViewModel()
    {
        strDetectInfo = string.Empty;
        mediaCapture = new MediaCapture();
        initializeModel();
    }

    public async void initializeModel(float confRate = 0.30f, float IoURate = 0.50f)
    {
        await _modelHelper.modelCheckerAsync();
        inferenceHelper = new InferenceHelper
            (ModelHelper.modelOnnxPath, Commoms.labels, confThreshold: confRate, iouThreshold:IoURate);
    }

    [ObservableProperty]
    private string strDetectInfo;

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

    public MediaFrameReader frameReader
    {
        get;
        set;
    }

    public ObservableCollection<MediaFrameSourceGroup> sourceGroups
    {
        get;
        set;
    } = new();

    public InferenceHelper inferenceHelper
    {
        get;
        set;
    }

    [ObservableProperty]
    private double dConfRate = 0.30f;

    [ObservableProperty]
    private double dIouRate = 0.50f;

    [ObservableProperty]
    private bool isDetecting = false;

    [ObservableProperty]
    private bool isEditable = true;

    [ObservableProperty]
    private float xmin = 0.0f;
    [ObservableProperty]
    private float ymin = 0.0f;
    [ObservableProperty]
    private float xmax = 0.0f;
    [ObservableProperty]
    private float ymax = 0.0f;
    [ObservableProperty]
    private string strClassName = "";
    [ObservableProperty]
    private float fConfidence = 0.0f;
}