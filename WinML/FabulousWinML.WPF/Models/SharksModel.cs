using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Storage;
using Windows.AI.MachineLearning.Preview;

// 1323b843-ad67-402f-9331-3a197a6fc6da_61c18ea8-4b6d-453b-9028-3a48ccd8a4e9

namespace FabulousWinML.WPF.Models
{
    public sealed class SharksModelInput
    {
        public VideoFrame data { get; set; }
    }

    public sealed class SharksModelOutput
    {
        public IList<string> classLabel { get; set; }
        public IDictionary<string, float> loss { get; set; }
        public SharksModelOutput()
        {
            this.classLabel = new List<string>();
            this.loss = new Dictionary<string, float>()
            {
                { "shark", float.NaN },
            };
        }
    }

    public sealed class SharksModel
    {
        private LearningModelPreview learningModel;
        public static async Task<SharksModel> CreateSharksModel(StorageFile file)
        {
            LearningModelPreview learningModel = await LearningModelPreview.LoadModelFromStorageFileAsync(file);
            SharksModel model = new SharksModel();
            learningModel.InferencingOptions.PreferredDeviceKind = LearningModelDeviceKindPreview.LearningDeviceGpu;
            learningModel.InferencingOptions.ReclaimMemoryAfterEvaluation = true;

            model.learningModel = learningModel;
            return model;
        }

        public async Task<SharksModelOutput> EvaluateAsync(SharksModelInput input)
        {
            SharksModelOutput output = new SharksModelOutput();
            LearningModelBindingPreview binding = new LearningModelBindingPreview(learningModel);
            binding.Bind("data", input.data);
            binding.Bind("classLabel", output.classLabel);
            binding.Bind("loss", output.loss);
            LearningModelEvaluationResultPreview evalResult = await learningModel.EvaluateAsync(binding, string.Empty);
            return output;
        }
    }
}
