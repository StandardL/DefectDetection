// InferenceHelper.cs
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace DefectDetection.Helpers;

public class InferenceResult
{
    public IReadOnlyList<Detection> Detections
    {
        get; set;
    }
    public SoftwareBitmap ProcessedBitmap
    {
        get; set;
    }
}

public class Detection
{
    public float[] BBox
    {
        get; set;
    }  // [x_min, y_min, x_max, y_max] (xyxy坐标)
    public float Confidence
    {
        get; set;
    }
    public int ClassId
    {
        get; set;
    }
    public string Label
    {
        get; set;
    }
}

public class InferenceHelper
{
    private readonly InferenceSession _session;
    private readonly string[] _labels;
    private readonly int _inputSize;
    private readonly float _confThreshold;
    private readonly float _iouThreshold;
    private System.Drawing.Size _originalSize;

    public InferenceHelper(
        string modelPath,
        string[] labels,
        int inputSize = 640,
        float confThreshold = 0.3f,
        float iouThreshold = 0.5f)
    {
        _session = new InferenceSession(modelPath);
        _labels = labels;
        _inputSize = inputSize;
        _confThreshold = confThreshold;
        _iouThreshold = iouThreshold;
    }

    public async Task<InferenceResult> ProcessImageAsync(SoftwareBitmap inputBitmap)
    {
        _originalSize = new System.Drawing.Size(inputBitmap.PixelWidth, inputBitmap.PixelHeight);

        // 预处理
        var (processedBitmap, inputTensor, ratio) = await PreprocessImageAsync(inputBitmap);

        // 创建输入
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("images", inputTensor)
        };

        // 推理
        using var results = _session.Run(inputs);

        // 后处理
        var outputTensor = results.FirstOrDefault()?.AsTensor<float>();
        var processedOutput = PostProcess((DenseTensor<float>)outputTensor, _inputSize, p6: false);
        var detections = ParseOutput(processedOutput, ratio);
        var filteredDetections = ApplyNMS(detections);

        return new InferenceResult
        {
            Detections = filteredDetections,
            ProcessedBitmap = processedBitmap
        };
    }

    /// <summary>
    /// 预处理输入的图像数据
    /// </summary>
    /// <param name="bitmap"></param>
    /// <returns>(处理后的bitmap, Tensor, ratio)</returns>
    private async Task<(SoftwareBitmap, Tensor<float>, float)> PreprocessImageAsync(SoftwareBitmap bitmap)
    {

        // 原始尺寸
        int originalWidth = bitmap.PixelWidth;
        int originalHeight = bitmap.PixelHeight;

        // 获取bitmap的shape数据
        var r = Math.Min((float)_inputSize / originalWidth, (float)_inputSize / originalHeight);
        int scaledWidth = (int)(originalWidth * r);
        int scaledHeight = (int)(originalHeight * r);

        /*var scaledBitmap = await ScaleImageAsync(bitmap, (uint)scaledWidth, (uint)scaledHeight);

        // 填充画布
        var paddedBitmap = new SoftwareBitmap(
        BitmapPixelFormat.Bgra8,
        (int)_inputSize,
        (int)_inputSize,
        BitmapAlphaMode.Premultiplied
        );

        // 使用Canvas进行绘制
        using (var canvasBuffer = new CanvasRenderTarget(
            CanvasDevice.GetSharedDevice(),
            _inputSize,
            _inputSize,
            96 // 默认DPI
        ))
        using (var drawingSession = canvasBuffer.CreateDrawingSession())
        {
            // 填充背景色 (B=114, G=114, R=114, A=255)
            drawingSession.Clear(Windows.UI.Color.FromArgb(255, 114, 114, 114));

            // 将缩放后的图像绘制到左上角
            using (var scaledCanvas = CanvasBitmap.CreateFromSoftwareBitmap(
                CanvasDevice.GetSharedDevice(),
                scaledBitmap))
            {
                drawingSession.DrawImage(scaledCanvas, new Rect(0, 0, scaledWidth, scaledHeight));
            }

            // 将结果复制回SoftwareBitmap
            paddedBitmap = SoftwareBitmap.CreateCopyFromBuffer(
                canvasBuffer.GetPixelBytes().AsBuffer(),
                BitmapPixelFormat.Bgra8,
                (int)_inputSize,
                (int)_inputSize,
                BitmapAlphaMode.Premultiplied
            );
        }*/

        // 计算填充
        float padTop = (_inputSize - scaledHeight) / 2f;
        float padLeft = (_inputSize - scaledWidth) / 2f;

        // 转换为BGRA8格式
        if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8)
        {
            bitmap = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8);
        }

        // 创建调整大小后的Bitmap
        var processedBitmap = new SoftwareBitmap(
            BitmapPixelFormat.Bgra8,
            _inputSize,
            _inputSize,
            BitmapAlphaMode.Premultiplied);

        // 使用BitmapTransformer进行缩放
        using var inputStream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, inputStream);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();

        var decoder = await BitmapDecoder.CreateAsync(inputStream);
        var transform = new BitmapTransform
        {
            ScaledWidth = (uint)(originalWidth * r),
            ScaledHeight = (uint)(originalHeight * r),
            InterpolationMode = BitmapInterpolationMode.Linear
        };

        var pixelData = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            transform,
            ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.DoNotColorManage);

        var pixels = pixelData.DetachPixelData();
        processedBitmap.CopyFromBuffer(pixels.AsBuffer());

        // 转换为Tensor
        var tensor = new DenseTensor<float>([1, 3, _inputSize, _inputSize]);  // NCHW

        /*var buffer = new Windows.Storage.Streams.Buffer(
        (uint)(_inputSize * _inputSize * 4) // BGRA8每个像素占4字节
    );
        paddedBitmap.CopyToBuffer(buffer);
        var pixels = buffer.ToArray(); // 获取byte[]*/

        // 转换为RGB
        Parallel.For(0, _inputSize, y =>
        {
            Parallel.For(0, _inputSize, x =>
            {
                var idx = (y * _inputSize + x) * 4;
                tensor[0, 0, y, x] = pixels[idx + 2];   // R
                tensor[0, 1, y, x] = pixels[idx + 1];   // G
                tensor[0, 2, y, x] = pixels[idx];       // B
            });
        });

        return (processedBitmap, tensor, r);
    }

    private async Task<SoftwareBitmap> ScaleImageAsync(SoftwareBitmap source, uint targetWidth, uint targetHeight)
    {
        using var stream = new InMemoryRandomAccessStream();

        // 编码源图像
        var encoder = await BitmapEncoder.CreateAsync(
            BitmapEncoder.JpegEncoderId,
            stream);
        encoder.SetSoftwareBitmap(source);
        await encoder.FlushAsync();

        // 应用缩放变换
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var transform = new BitmapTransform
        {
            ScaledWidth = targetWidth,
            ScaledHeight = targetHeight,
            InterpolationMode = BitmapInterpolationMode.Linear
        };

        // 获取缩放后的像素数据
        var pixelData = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            transform,
            ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.DoNotColorManage
        );

        // 创建缩放后的SoftwareBitmap
        return SoftwareBitmap.CreateCopyFromBuffer(
            pixelData.DetachPixelData().AsBuffer(),
            BitmapPixelFormat.Bgra8,
            (int)targetWidth,
            (int)targetHeight,
            BitmapAlphaMode.Premultiplied
        );
    }

    /// <summary>
    /// 从Tensor中提取Bbox，obj_score，cls_prop，
    /// </summary>
    /// <param name="output"></param>
    /// <returns>检测到的目标结果</returns>
    private List<Detection> ParseOutput(Tensor<float> output, float ratio)
    {
        var detections = new List<Detection>();
        if (output == null) return detections;

        // 输出形状为 [batch_size, num_detections, 11]
        for (int i = 0; i < output.Dimensions[1]; i++)
        {
            // 前4个元素为bbox坐标（x_center, y_center, w, h）
            // 第5个元素为obj_score
            // 后续为cls_prop
            var objScore = output[0, i, 4];
            if (objScore < _confThreshold) continue;

            var classProbs = new float[_labels.Length];
            for (int c = 0; c < _labels.Length; c++)
            {
                classProbs[c] = output[0, i, 5 + c];
            }

            var maxProb = classProbs.Max();
            var classId = classProbs.ToList().IndexOf(maxProb);
            var confidence = objScore * maxProb;

            if (confidence > _confThreshold)
            {
                detections.Add(new Detection
                {
                    BBox =
                    [
                        output[0, i, 0],  // x_center
                        output[0, i, 1],  // y_center
                        output[0, i, 2],  // width
                        output[0, i, 3]   // height
                    ],
                    Confidence = confidence,
                    ClassId = classId,
                    Label = _labels[classId]
                });
            }
        }
        return detections;
    }

    /// <summary>
    /// 输出图像后处理，将坐标转换为原始图像坐标
    /// </summary>
    /// <param name="outputs">结果向量</param>
    /// <param name="inputShape">输入图像尺寸</param>
    /// <param name="p6"></param>
    /// <returns>转换后的结果向量</returns>
    private Tensor<float> PostProcess(DenseTensor<float> outputs, int inputShape = 640, bool p6 = false)
    {
        int[] strides = p6 ? [8, 16, 32, 64] : [8, 16, 32];

        var hsizes = strides.Select(stride => inputShape / stride).ToList();
        var wsizes = strides.Select(stride => inputShape / stride).ToList();

        List<double[,]> grids = [];
        List<double[]> expanded_strides = [];

        for (int i = 0; i < strides.Length; i++)
        {
            int hsize = hsizes[i];
            int wsize = wsizes[i];
            int stride = strides[i];

            // 生成坐标网格
            List<double[]> points = new List<double[]>();
            for (int y = 0; y < hsize; y++)
            {
                for (int x = 0; x < wsize; x++)
                {
                    points.Add([x, y]);
                }
            }
            double[,] grid = new double[points.Count, 2];
            for (int j = 0; j < points.Count; j++)
            {
                grid[j, 0] = points[j][0];
                grid[j, 1] = points[j][1];
            }
            grids.Add(grid);

            double[] stridesArray = new double[points.Count];
            Array.Fill(stridesArray, stride);
            expanded_strides.Add(stridesArray);
        }

        double[,] combinedGrids = CombineGrids(grids);
        double[] combinedStrides = expanded_strides.SelectMany(s => s).ToArray();

        var dimansions = outputs.Dimensions.ToArray();
        int batchSize = dimansions[0];
        int numPoints = dimansions[1];

        // Process outputs
        Parallel.For(0, batchSize, b =>
        {
            Parallel.For(0, numPoints, i =>
            {
                int baseIndex = (int)(b * numPoints * 4 + i * 4);
                // Update coordinates
                outputs[b, i, 0] = (float)((outputs[b, i, 0] + combinedGrids[i, 0]) * combinedStrides[i]);
                outputs[b, i, 1] = (float)((outputs[b, i, 1] + combinedGrids[i, 1]) * combinedStrides[i]);

                // Update dimensions
                outputs[b, i, 2] = (float)(Math.Exp(outputs[b, i, 2]) * combinedStrides[i]);
                outputs[b, i, 3] = (float)(Math.Exp(outputs[b, i, 3]) * combinedStrides[i]);
            });
        });

        return outputs;
    }

    private static double[,] CombineGrids(List<double[,]> grids)
    {
        int totalPoints = grids.Sum(g => g.GetLength(0));
        double[,] combined = new double[totalPoints, 2];
        int index = 0;

        foreach (double[,] grid in grids)
        {
            int points = grid.GetLength(0);
            for (int i = 0; i < points; i++)
            {
                combined[index, 0] = grid[i, 0];
                combined[index, 1] = grid[i, 1];
                index++;
            }
        }

        return combined;
    }

    private List<Detection> ApplyNMS(IEnumerable<Detection> detections)
    {
        var ordered = detections.OrderByDescending(d => d.Confidence).ToList();
        var keep = new List<Detection>();

        while (ordered.Count > 0)
        {
            var current = ordered[0];
            keep.Add(current);
            ordered.RemoveAt(0);

            for (int i = ordered.Count - 1; i >= 0; i--)
            {
                if (CalculateIoU(current, ordered[i]) > _iouThreshold)
                {
                    ordered.RemoveAt(i);
                }
            }
        }
        return keep;
    }

    private static float CalculateIoU(Detection a, Detection b)
    {
        // 实现IoU计算
        var boxA = a.BBox;
        var boxB = b.BBox;

        float x1 = Math.Max(boxA[0] - boxA[2] / 2, boxB[0] - boxB[2] / 2);
        float y1 = Math.Max(boxA[1] - boxA[3] / 2, boxB[1] - boxB[3] / 2);
        float x2 = Math.Min(boxA[0] + boxA[2] / 2, boxB[0] + boxB[2] / 2);
        float y2 = Math.Min(boxA[1] + boxA[3] / 2, boxB[1] + boxB[3] / 2);

        float interArea = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        float areaA = boxA[2] * boxA[3];
        float areaB = boxB[2] * boxB[3];

        return interArea / (areaA + areaB - interArea);
    }

    public async Task<SoftwareBitmap> GetAnnotatedImageAsync(SoftwareBitmap originBitmap, IEnumerable<Detection> detections)
    {
        return await ImageProcessor.DrawDetectionsAsync(originBitmap, detections, 3);
    }
}