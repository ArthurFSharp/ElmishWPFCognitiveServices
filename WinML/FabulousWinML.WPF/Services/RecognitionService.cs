using Microsoft.FSharp.Core;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FabulousWinML.Services;
using FabulousWinML.WPF.Models;
using FabulousWinML.WPF.Services;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Storage;
using Xamarin.Forms;

[assembly: Dependency(typeof(RecognitionService))]
namespace FabulousWinML.WPF.Services
{
    public class RecognitionService : IRecognitionService
    {
        private SharksModel _model;

        public async Task<FSharpOption<byte[]>> OpenImage()
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = "Image files (*.jpg, *.jpeg, *.jpe, *.jfif, *.png) | *.jpg; *.jpeg; *.jpe; *.jfif; *.png";
            if (dialog.ShowDialog() == true)
            {
                var raStream = dialog.OpenFile();
                return FSharpOption<byte[]>.Some(ReadFully(raStream));
            }
            return FSharpOption<byte[]>.None;
        }

        public async Task<IDictionary<string, double>> Recognize(byte[] stream)
        {
            SoftwareBitmap softwareBitmap;

            Stream ms = new MemoryStream(stream);

            var decoder = await BitmapDecoder.CreateAsync(ms.AsRandomAccessStream());

            if (_model == null)
            {
                // Load the model
                await Task.Run(async () => await LoadModelAsync());
            }

            // Get the SoftwareBitmap representation of the file in BGRA8 format
            softwareBitmap = await decoder.GetSoftwareBitmapAsync();
            softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            var frameImage = VideoFrame.CreateWithSoftwareBitmap(softwareBitmap);

            if (frameImage != null)
            {
                try
                {
                    var inputData = new SharksModelInput();
                    inputData.data = frameImage;
                    var results = await _model.EvaluateAsync(inputData);
                    var loss = results.loss.ToList().OrderBy(x => -(x.Value));
                    var labels = results.classLabel;

                    return new Dictionary<string, double>(loss.ToDictionary(pair => pair.Key, pair => (double)pair.Value));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"error: {ex.Message}");
                }
            }

            return new Dictionary<string, double>();
        }

        private async Task LoadModelAsync()
        {
            try
            {
                var modelFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///FabulousWinML.WPF/Models/SharksModel.onnx"));
                _model = await SharksModel.CreateSharksModel(modelFile);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"error: {ex.Message}");
                _model = null;
            }
        }

        public static byte[] ReadFully(Stream input)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }
}
