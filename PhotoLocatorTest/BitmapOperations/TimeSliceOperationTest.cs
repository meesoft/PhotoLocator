using PhotoLocator.Helpers;
using PhotoLocator.PictureFileFormats;
using PhotoLocator.Settings;
using System.Diagnostics;

namespace PhotoLocator.BitmapOperations
{
    [TestClass]
    public class TimeSliceOperationTest
    {
        [TestMethod]
        public async Task GenerateTimeSliceImage_ShouldGenerateFromImages()
        {
            var images = new string[] {
            };

            if (!File.Exists(VideoProcessingTest.FFmpegPath))
                Assert.Inconclusive("FFmpegPath not found");
            if (images.Any(f => !File.Exists(f)))
                Assert.Inconclusive("Input images not found");

            var settings = new ObservableSettings() { FFmpegPath = VideoProcessingTest.FFmpegPath };
            var videoTransforms = new VideoProcessing(settings);

            var timeSlice = new TimeSliceOperation();
            timeSlice.SelectionMapExpression = TimeSliceSelectionMaps.TopRightToBottomLeft;

            const string InputListFileName = "input.txt";
            await File.WriteAllLinesAsync(InputListFileName, images.Select(f => $"file '{f}'")).ConfigureAwait(false);
            var readerArgs = $"-f concat -safe 0 -i \"{InputListFileName}\"";
            await videoTransforms.RunFFmpegWithStreamOutputImagesAsync(readerArgs, timeSlice.AddFrame, stdError => Debug.WriteLine("Out: " + stdError), default);

            Debug.WriteLine("Number of frames: " + timeSlice.UsedFrames);
            Debug.WriteLine("Skipped frames: " + timeSlice.SkippedFrames);

            var result = timeSlice.GenerateTimeSliceImageInterpolated();

            GeneralFileFormatHandler.SaveToFile(result, @"TimeSlice.png");
        }
    }
}
