using System.Diagnostics;
using DefectDetection.Helpers;
using DefectDetection.ViewModels;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Globalization.NumberFormatting;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace DefectDetection.Views;

public sealed partial class OfflinePage : Page
{
    public OfflineViewModel ViewModel
    {
        get;
    }

    public OfflinePage()
    {
        ViewModel = App.GetService<OfflineViewModel>();
        InitializeComponent();

        ConfigureNumberBoxFormat();
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

        OfflinePageConfNumberBox.NumberFormatter = formatter;
        OfflinePageIoUNumberBox.NumberFormatter = formatter;
    }

    private async void OfflinePagePickPhotoButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var senderButton = sender as Button;
        senderButton!.IsEnabled = false;

        ViewModel.StrDetectInfo = string.Empty;
        OfflinePageInfoBar.Severity = InfoBarSeverity.Informational;

        var openPicker = new Windows.Storage.Pickers.FileOpenPicker();

        var window = App.MainWindow;
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);

        openPicker.ViewMode = PickerViewMode.Thumbnail;
        openPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        openPicker.FileTypeFilter.Add(".jpg");
        openPicker.FileTypeFilter.Add(".jpeg");

        var file = await openPicker.PickSingleFileAsync();
        if (file != null)
        {
            OfflinePageInfoBar.Severity = InfoBarSeverity.Success;
            ViewModel.StrDetectInfo = $"选择的图像：{file.Name}{Environment.NewLine}图像已加载完成";
            using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read))
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                ViewModel.softwareBitmap = await decoder.GetSoftwareBitmapAsync();
            }
                
            OfflinePageImageOri.Source = new BitmapImage(new Uri(file.Path));
        }
        else
        {
            OfflinePageInfoBar.Severity = InfoBarSeverity.Error;
            ViewModel.StrDetectInfo = "操作取消";
        }

        senderButton.IsEnabled = true;
    }

    private async void OfflinePageDetectButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        OfflinePageDetectProgressRing.IsActive = true;
        // 更新参数
        ViewModel.inferenceHelper = new(
            ModelHelper.modelOnnxPath,
            Commoms.labels,
            640,
            (float)ViewModel.DConfRate,
            (float)ViewModel.DIouRate
        );

        // 计时器
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        var result = await ViewModel.inferenceHelper.ProcessImageAsync(ViewModel.softwareBitmap);

        stopWatch.Stop();

        DispatcherQueue.TryEnqueue(() =>
        {
            DisplayResult(result);
        });

        // 获取总运行时间（精确到小数秒）
        var totalSeconds = stopWatch.Elapsed.TotalMilliseconds;
        OfflinePageDetectTime.Text = $"{totalSeconds}ms";
        OfflinePageDetectProgressRing.IsActive = false;
    }

    private async void DisplayResult(InferenceResult result)
    {
        // 显示处理后的图像
        var annotatedBitmap = await ViewModel.inferenceHelper.GetAnnotatedImageAsync(
        result.ProcessedBitmap,
        result.Detections);
        var imageSource = new SoftwareBitmapSource();
        await imageSource.SetBitmapAsync(annotatedBitmap);
        OfflinePageImageOri.Source = imageSource;
        List<String> lstResultMinPoints = [];
        List<String> lstResultMaxPoints = [];
        List<String> lstResultConf = [];
        List<String> lstResultLable = [];

        // 显示检测结果
        foreach (var det in result.Detections)
        {
            lstResultLable.Add(Commoms.dicEng2Chi[det.Label]);
            lstResultConf.Add(det.Confidence.ToString());
            var Xmin = det.BBox[0] - det.BBox[2] / 2;
            var Ymin = det.BBox[1] - det.BBox[3] / 2;
            var Xmax = det.BBox[0] + det.BBox[2] / 2;
            var Ymax = det.BBox[1] + det.BBox[3] / 2;
            var pointMin = $"({Xmin},{Ymin})";
            var pointMax = $"({Xmax},{Ymax})";
            lstResultMinPoints.Add(pointMin);
            lstResultMaxPoints.Add(pointMax);
        }

        ViewModel.StrClassName = String.Join(", ", lstResultLable);
        ViewModel.StrConfidence = String.Join(", ", lstResultConf);

        var minPoints = String.Join(", ", lstResultMinPoints);
        var maxPoints = String.Join(", ", lstResultMaxPoints);

        OfflinePageMinText.Text = $"min坐标: {minPoints}";
        OfflinePageMaxText.Text = $"min坐标: {maxPoints}";
    }
}
