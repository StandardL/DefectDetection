using DefectDetection.Services;
using Windows.Storage;

namespace DefectDetection.Helpers;
public class ModelHelper
{
    private readonly int modelVersion = 1; // 模型版本号
    public static readonly string modelName = "quantEtdNet.onnx";
    public static readonly string modelDataName = "quantEtdNet.onnx.data";
    public static readonly string modelPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "DefectDetection", "models");
    public static readonly string modelOnnxPath = Path.Combine(modelPath, modelName);

    public async Task modelCheckerAsync()
    {
        if (!Directory.Exists(modelPath))
        {
            Directory.CreateDirectory(modelPath);
        }

        // 模型文件不存在或版本过低时，从Assets中复制模型文件到本地
        var modelVersionStorageService = new ModelVersionStorageService();
        var _modelVersion = modelVersionStorageService.GetVersion();
        if (!Directory.Exists(Path.Combine(modelPath, modelName)) || _modelVersion < modelVersion)  // 或版本过低
        {
            var modelUri = new Uri("ms-appx:///Assets/merged_quantized_ckpt_int8_best_ap50.onnx");
            var modelFile = await StorageFile.GetFileFromApplicationUriAsync(modelUri);
            await modelFile.CopyAsync(StorageFolder.GetFolderFromPathAsync(modelPath).AsTask().Result, modelName, NameCollisionOption.ReplaceExisting);

            modelVersionStorageService.SetVersion(modelVersion);
        }
    }
}
