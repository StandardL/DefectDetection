using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using DefectDetection.Helpers;
using DefectDetection.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.Globalization.NumberFormatting;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;

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
            MainPageInfoBar.Severity = InfoBarSeverity.Error;
            ViewModel.StrDetectInfo = "没有识别到有相机连接到此设备上";
            return;
        }
        ViewModel.mediaFrameSourceGroup = groups.First();
        MainPageCamComboBox.SelectedIndex = 0;

        ViewModel.StrDetectInfo = "Viewing: " + ViewModel.mediaFrameSourceGroup.DisplayName;
        ViewModel.mediaCapture = new MediaCapture();
        var mediaCaptureInitializationSettings = new MediaCaptureInitializationSettings()
        {
            SourceGroup = ViewModel.mediaFrameSourceGroup,
            SharingMode = MediaCaptureSharingMode.SharedReadOnly,
            StreamingCaptureMode = StreamingCaptureMode.Video,
            MemoryPreference = MediaCaptureMemoryPreference.Cpu
        };
        await ViewModel.mediaCapture.InitializeAsync(mediaCaptureInitializationSettings);

        var frameSource = ViewModel.mediaCapture.FrameSources[ViewModel.mediaFrameSourceGroup.SourceInfos[0].Id];
        MainPageCaptureElement.Source = Windows.Media.Core.MediaSource.CreateFromMediaFrameSource(frameSource);

        // 创建FrameReader但不立即启动
        if (frameSource != null)
        {
            ViewModel.frameReader = await ViewModel.mediaCapture.CreateFrameReaderAsync(frameSource);
            ViewModel.frameReader.FrameArrived += FrameReader_FrameArrived;
        }

    }

    private void MainPage_Unloaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel.mediaCapture != null)
        {
            new Task(ViewModel.mediaCapture.Dispose).Start();
        }
    }

    /// <summary>
    /// 调整 NumberBox 的格式
    /// </summary>
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

    private async void FrameReader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        if (ViewModel.IsDetecting == false) return;

        var frame = sender.TryAcquireLatestFrame();
        if (frame?.VideoMediaFrame == null) return;

        using var input = frame.VideoMediaFrame.SoftwareBitmap;
        var croppedInput = await CropVideoFrameToSquareAsync(input);
        var result = await ViewModel.inferenceHelper.ProcessImageAsync(croppedInput);

        DispatcherQueue.TryEnqueue(() =>
        {
            DisplayResult(result);
        });
    }

    private async void DisplayResult(InferenceResult result)
    {
        // 显示处理后的图像
        var annotatedBitmap = await ViewModel.inferenceHelper.GetAnnotatedImageAsync(
        result.ProcessedBitmap,
        result.Detections);
        var imageSource = new SoftwareBitmapSource();
        await imageSource.SetBitmapAsync(annotatedBitmap);
        MainPageDetectResultImage.Source = imageSource;

        // 显示检测结果
        foreach (var det in result.Detections)
        {
            ViewModel.StrClassName = Commoms.dicEng2Chi[det.Label];
            ViewModel.FConfidence = det.Confidence;
            ViewModel.Xmin = det.BBox[0] - det.BBox[2] / 2;
            ViewModel.Ymin = det.BBox[1] - det.BBox[3] / 2;
            ViewModel.Xmax = det.BBox[0] + det.BBox[2] / 2;
            ViewModel.Ymax = det.BBox[1] + det.BBox[3] / 2;
            MainPageMinText.Text = $"min坐标: ({ViewModel.Xmin},{ViewModel.Ymin})";
            MainPageMaxText.Text = $"max坐标: ({ViewModel.Xmax},{ViewModel.Ymax})";
        }
    }

    private async void MainPageCamComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var id = MainPageCamComboBox.SelectedIndex;
        ViewModel.mediaFrameSourceGroup = ViewModel.sourceGroups[id];
        ViewModel.StrDetectInfo = "Viewing: " + ViewModel.mediaFrameSourceGroup.DisplayName;

        ViewModel.mediaCapture = new();
        var mediaCaptureInitializationSettings = new MediaCaptureInitializationSettings()
        {
            SourceGroup = ViewModel.mediaFrameSourceGroup,
            SharingMode = MediaCaptureSharingMode.SharedReadOnly,
            StreamingCaptureMode = StreamingCaptureMode.Video,
            MemoryPreference = MediaCaptureMemoryPreference.Cpu
        };
        await ViewModel.mediaCapture.InitializeAsync(mediaCaptureInitializationSettings);

        var frameSource = ViewModel.mediaCapture.FrameSources[ViewModel.mediaFrameSourceGroup.SourceInfos[0].Id];
        MainPageCaptureElement.Source = Windows.Media.Core.MediaSource.CreateFromMediaFrameSource(frameSource);
        if (frameSource != null)
        {
            ViewModel.frameReader = await ViewModel.mediaCapture.CreateFrameReaderAsync(frameSource);
            ViewModel.frameReader.FrameArrived += FrameReader_FrameArrived;
        }

    }

    private async void MainPageRefreshButton_Click(object sender, RoutedEventArgs e)
    {
        var groups = await MediaFrameSourceGroup.FindAllAsync();
        ViewModel.sourceGroups = new(groups);
        if (groups.Count == 0)
        {
            MainPageInfoBar.Severity = InfoBarSeverity.Error;
            ViewModel.StrDetectInfo = "没有识别到有相机连接到此设备上";
            return;
        }
    }

    private async void MainPageDetectButton_Checked(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.inferenceHelper = new(
                ModelHelper.modelOnnxPath,
                Commoms.labels,
                confThreshold: (float)ViewModel.DConfRate,
                iouThreshold: (float)ViewModel.DIouRate
                );
            var status = await ViewModel.frameReader.StartAsync();
            if (status == MediaFrameReaderStartStatus.Success)
            {
                ViewModel.initializeModel((float)ViewModel.DConfRate, (float)ViewModel.DIouRate);
                ViewModel.IsDetecting = true;
                ViewModel.IsEditable = !ViewModel.IsDetecting;
                MainPageDetectResultImage.Visibility = Visibility.Visible;
                MainPageInfoBar.Severity = InfoBarSeverity.Success;
                ViewModel.StrDetectInfo = $"正在检测...";
            }
        }
        catch (Exception ex)
        {
            MainPageInfoBar.Severity = InfoBarSeverity.Error;
            ViewModel.StrDetectInfo = $"启动帧捕获失败: {ex.Message}";
            MainPageDetectButton.IsChecked = false;
            MainPageDetectResultImage.Visibility = Visibility.Collapsed;
        }
    }

    private async void MainPageDetectButton_Unchecked(object sender, RoutedEventArgs e)
    {
        
        if (ViewModel.frameReader == null) 
            return;

        try
        {
            await ViewModel.frameReader.StopAsync();
            ViewModel.IsDetecting = false;
            ViewModel.IsEditable = !ViewModel.IsDetecting;
            MainPageInfoBar.Severity = InfoBarSeverity.Informational;
            ViewModel.StrDetectInfo = string.Empty;
        }
        catch (Exception ex)
        {
            MainPageInfoBar.Severity = InfoBarSeverity.Error;
            ViewModel.StrDetectInfo = $"停止帧捕获失败: {ex.Message}";
        }
    }

    public async Task<SoftwareBitmap> CropVideoFrameToSquareAsync(SoftwareBitmap inputBitmap)
    {
        SoftwareBitmap bitmap = inputBitmap;
        if (inputBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
            inputBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
        {
            bitmap = SoftwareBitmap.Convert(inputBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }

        int originalWidth = bitmap.PixelWidth;
        int originalHeight = bitmap.PixelHeight;

        // 计算正方形裁剪区域
        int squareSize = Math.Min(originalWidth, originalHeight);
        uint offsetX = (uint)((originalWidth - squareSize) / 2);
        uint offsetY = (uint)((originalHeight - squareSize) / 2);

        // 借助 BitmapTransform 实现裁剪
        using (var memoryStream = new InMemoryRandomAccessStream())
        {
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, memoryStream);
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync();

            // 重置流的起始位置供解码使用
            memoryStream.Seek(0);

            // 使用 BitmapDecoder 解码流中的图像
            var decoder = await BitmapDecoder.CreateAsync(memoryStream);

            // 配置裁剪所需的 BitmapTransform
            var transform = new BitmapTransform()
            {
                Bounds = new BitmapBounds()
                {
                    X = offsetX,
                    Y = offsetY,
                    Width = (uint)squareSize,
                    Height = (uint)squareSize
                }
            };

            // 获取裁剪区域内的像素数据
            PixelDataProvider pixelData = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage);

            byte[] pixels = pixelData.DetachPixelData();

            // 创建裁剪后的 SoftwareBitmap，并将裁剪后的数据拷贝进去
            var croppedBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, squareSize, squareSize, BitmapAlphaMode.Premultiplied);
            croppedBitmap.CopyFromBuffer(pixels.AsBuffer());

            return croppedBitmap;
        }
    }
}
