﻿using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using System.Collections.Concurrent;
using Yolov8Net.Extentions;

namespace Yolov8Net;

public class YoloXPredictor : PredictorBase, IPredictor
{
    /// <summary>
    /// Create a YoloX Predictor.
    /// </summary>
    /// <param name="modelPath">Path to the ONNX format model to load.</param>
    /// <param name="labels">Labels associated with model. If not provided, standard COCO labels are used.</param>
    /// <param name="useCuda">Use GPU/CUDA.  NOTE: Requires CUDA drivers AND CUDNN be installed.</param>
    /// <returns>IPredictor</returns>
    public static IPredictor Create(string modelPath, string[]? labels = null, bool useCuda = false)
    {
        return new YoloXPredictor(modelPath, labels, useCuda);
    }

    private YoloXPredictor(string modelPath, string[]? labels = null, bool useCuda = false)
        : base(modelPath, labels, useCuda) { }

    protected List<Prediction> ParseOutput(DenseTensor<float> output, Image image)
    {
        var result = new ConcurrentBag<Prediction>();

        var (w, h) = (image.Width, image.Height); // image w and h
        var (xGain, yGain) = (ModelInputWidth / (float)w, ModelInputHeight / (float)h); // x, y gains
        var (xPad, yPad) = ((ModelInputWidth - w * xGain) / 2, (ModelInputHeight - h * yGain) / 2); // left, right pads

        //for each batch
        Parallel.For(0, output.Dimensions[0], i =>
        {
            //divide total length by the elements per prediction
            Parallel.For(0, (int)(output.Length / output.Dimensions[2]), j =>
            {

                float xMin = ((output[i, j, 0] - output[i, j, 2] / 2) - xPad) / xGain; // unpad bbox tlx to original
                float yMin = ((output[i, j, 1] - output[i, j, 3] / 2) - yPad) / yGain; // unpad bbox tly to original
                float xMax = ((output[i, j, 0] + output[i, j, 2] / 2) - xPad) / xGain; // unpad bbox brx to original
                float yMax = ((output[i, j, 1] + output[i, j, 3] / 2) - yPad) / yGain; // unpad bbox bry to original

                xMin = Utils.Clamp(xMin, 0, w - 0); // clip bbox tlx to boundaries
                yMin = Utils.Clamp(yMin, 0, h - 0); // clip bbox tly to boundaries
                xMax = Utils.Clamp(xMax, 0, w - 1); // clip bbox brx to boundaries
                yMax = Utils.Clamp(yMax, 0, h - 1); // clip bbox bry to boundaries

                Parallel.For(0, ModelOutputDimensions - 4, l =>
                {
                    var pred = output[i, j, 4 + l];

                    //skip low confidence values
                    if (pred < Confidence) return;

                    result.Add(new Prediction()
                    {
                        Label = Labels[l],
                        Score = pred,
                        Rectangle = new RectangleF(xMin, yMin, xMax - xMin, yMax - yMin)
                    });
                });
            });
        });

        return result.ToList();
    }

    public override Prediction[] Predict(Image image)
    {
        return Suppress(
            ParseOutput(
                Inference(image)[0], image)
            );
    }
}
