using App.Models;
using Microsoft.ML;
using Microsoft.ML.Data;

public class SmartModeML
{
    private readonly MLContext _mlContext;
    private ITransformer _model;
    private PredictionEngine<SystemUsageData, SystemUsagePrediction> _predictionEngine;

    public SmartModeML()
    {
        _mlContext = new MLContext();
    }

    public void Train(string dataPath)
    {
        var data = _mlContext.Data.LoadFromTextFile<SystemUsageData>(
            path: dataPath,
            hasHeader: true,
            separatorChar: ',');

        var split = _mlContext.Data.TrainTestSplit(data, testFraction: 0.2);

        var pipeline = _mlContext.Transforms.Conversion.MapValueToKey("Label")
            .Append(_mlContext.Transforms.Concatenate("Features", "CpuUsage", "GpuUsage", "RamUsage"))
            .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
            .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
            .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

        _model = pipeline.Fit(split.TrainSet);

        var predictions = _model.Transform(split.TestSet);
        var metrics = _mlContext.MulticlassClassification.Evaluate(predictions);
        Console.WriteLine($"✅ Accuracy: {metrics.MicroAccuracy:P1}");

        _predictionEngine = _mlContext.Model.CreatePredictionEngine<SystemUsageData, SystemUsagePrediction>(_model);
    }

    public string Predict(SystemUsageData input)
    {
        var prediction = _predictionEngine.Predict(input);
        return prediction.PredictedLabel;
    }
}
