using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.FSharp.Core;
using FabulousWinML.Services;
using FabulousWinML.UWP.Services;
using FabulousWinML.UWP.Models;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Storage;
using Windows.Storage.Pickers;
using Xamarin.Forms;

[assembly: Dependency(typeof(RecognitionService))]
namespace FabulousWinML.UWP.Services
{
    public class RecognitionService : IRecognitionService
    {
        private SharksModel _model;

        public async Task<FSharpOption<byte[]>> OpenImage()
        {
            var fileOpenPicker = new FileOpenPicker();
            fileOpenPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            fileOpenPicker.FileTypeFilter.Add(".bmp");
            fileOpenPicker.FileTypeFilter.Add(".jpg");
            fileOpenPicker.FileTypeFilter.Add(".png");
            fileOpenPicker.ViewMode = PickerViewMode.Thumbnail;
            var selectedStorageFile = await fileOpenPicker.PickSingleFileAsync();

            if (selectedStorageFile != null)
            {
                return FSharpOption<byte[]>.Some(ReadFully(selectedStorageFile.OpenStreamForReadAsync().Result));
            }
            return FSharpOption<byte[]>.None;
        }

        public async Task<IDictionary<string, double>> OfflineClassifierRecognize(byte[] stream)
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
                var modelFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Models/SharksModel.onnx"));
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
