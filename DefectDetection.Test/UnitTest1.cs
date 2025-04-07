using DefectDetection.Helpers;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace DefectDetection.Test;

public class UnitTest1
{
    [Fact]
    public async void InferenceTest()
    {
        var labels = new List<string> { "crazing", "inclusion", "pitted_surface", "rolled-in_scale", "scratches", "patches" };

        InferenceHelper inferenceHelper = new(
            "D:\\Coder\\C# Source Files\\DefectDetection\\DefectDetection\\Assets\\ONNX Models\\quantized_ckpt_int8_best_ap50.onnx",
            labels.ToArray(),
                confThreshold: 0.3f
            );

        var imgPath = "D:\\华师\\毕设\\Codes\\ETDNet\\datasets\\Neu_Det_5Flod_coco\\flod1\\train2017\\crazing_1.jpg";

        var file = await StorageFile.GetFileFromPathAsync(imgPath);

        var result = new InferenceResult();

        using (var stream = await file.OpenAsync(FileAccessMode.Read))
        {
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var bitmap = await decoder.GetSoftwareBitmapAsync();

            result = await inferenceHelper.ProcessImageAsync(bitmap);
        }
        Assert.NotEmpty(result.Detections);

        // 输出检测结果
        var resStr = $"置信度：{result.Detections[0].Confidence}\n类别：{result.Detections[0].Label}" +
            $"\n位置{result.Detections[0].BBox}";
        Console.WriteLine(resStr);
    }

}
