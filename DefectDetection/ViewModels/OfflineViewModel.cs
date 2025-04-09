using CommunityToolkit.Mvvm.ComponentModel;
using DefectDetection.Helpers;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;

namespace DefectDetection.ViewModels;

public partial class OfflineViewModel : ObservableRecipient
{
    private ModelHelper _modelHelper = new();

    public OfflineViewModel()
    {
        initializeModel();
    }

    public async void initializeModel(float confRate = 0.30f, float IoURate = 0.50f)
    {
        await _modelHelper.modelCheckerAsync();
        inferenceHelper = new InferenceHelper
            (ModelHelper.modelOnnxPath, Commoms.labels, confThreshold: confRate, iouThreshold: IoURate);
    }

    [ObservableProperty]
    private string strInfoTitle = "提示";

    [ObservableProperty]
    private string strDetectInfo = "请先选择一张照片";

    [ObservableProperty]
    private double dConfRate = 0.30f;

    [ObservableProperty]
    private double dIouRate = 0.50f;

    [ObservableProperty]
    private string strClassName = "";

    [ObservableProperty]
    private float fConfidence = 0.0f;

    [ObservableProperty]
    private string strConfidence = "";

    public InferenceHelper inferenceHelper
    {
        get;
        set;
    }

    public SoftwareBitmap softwareBitmap;
}
