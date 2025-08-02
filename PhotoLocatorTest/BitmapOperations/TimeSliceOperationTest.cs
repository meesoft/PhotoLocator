using PhotoLocator.Helpers;
using PhotoLocator.PictureFileFormats;
using PhotoLocator.Settings;
using System.Diagnostics;

namespace PhotoLocator.BitmapOperations
{
    [TestClass]
    public class TimeSliceOperationTest
    {
        const string SourceVideoPath = @"";

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

            var timeSlice = new TimeSliceOperation { SelectionMapExpression = TimeSliceSelectionMaps.TopRightToBottomLeft };

            const string InputListFileName = "input.txt";
            await File.WriteAllLinesAsync(InputListFileName, images.Select(f => $"file '{f}'")).ConfigureAwait(false);
            var readerArgs = $"-f concat -safe 0 -i \"{InputListFileName}\"";
            await videoTransforms.RunFFmpegWithStreamOutputImagesAsync(readerArgs, timeSlice.AddFrame, stdError => Debug.WriteLine("Out: " + stdError), default);

            Debug.WriteLine("Used frames: " + timeSlice.UsedFrames);
            Debug.WriteLine("Skipped frames: " + timeSlice.SkippedFrames);

            var result = timeSlice.GenerateTimeSliceImage();

            GeneralFileFormatHandler.SaveToFile(result, @"TimeSlice.png");
        }

        [TestMethod]
        public async Task GenerateTimeSliceImage_ShouldGenerateFromVideo()
        {
            if (!File.Exists(VideoProcessingTest.FFmpegPath))
                Assert.Inconclusive("FFmpegPath not found");
            if (!File.Exists(SourceVideoPath))
                Assert.Inconclusive("SourceVideoPath not found");

            var settings = new ObservableSettings() { FFmpegPath = VideoProcessingTest.FFmpegPath };
            var videoTransforms = new VideoProcessing(settings);

            var timeSlice = new TimeSliceOperation();
            timeSlice.SelectionMapExpression = TimeSliceSelectionMaps.LeftToRight;
            //timeSlice.SelectionMap.Assign(GeneralFileFormatHandler.LoadFromStream(File.OpenRead(@"DownRight.png"), Rotation.Rotate0, int.MaxValue, true, default), 1);

            var readerArgs = $" -i \"{SourceVideoPath}\" -vf \"setpts=PTS/(4)\"";
            var readTask = videoTransforms.RunFFmpegWithStreamOutputImagesAsync(readerArgs, timeSlice.AddFrame, stdError => Debug.WriteLine("Decode: " + stdError), default);

            await readTask;

            Debug.WriteLine("Used frames: " + timeSlice.UsedFrames);
            Debug.WriteLine("Skipped frames: " + timeSlice.SkippedFrames);

            var sw = Stopwatch.StartNew();
            var result = timeSlice.GenerateTimeSliceImage();
            Debug.WriteLine(sw.Elapsed);

            GeneralFileFormatHandler.SaveToFile(result, @"TimeSlice.png");
        }        

        [TestMethod]
        public async Task GenerateTimeSliceVideo_ShouldGenerateFromVideo()
        {
            const string TargetPath = @"TimeSlice.mp4";

            if (!File.Exists(VideoProcessingTest.FFmpegPath))
                Assert.Inconclusive("FFmpegPath not found");
            if (!File.Exists(SourceVideoPath))
                Assert.Inconclusive("SourceVideoPath not found");

            var settings = new ObservableSettings() { FFmpegPath = VideoProcessingTest.FFmpegPath };
            var videoTransforms = new VideoProcessing(settings);

            var timeSlice = new TimeSliceOperation { SelectionMapExpression = TimeSliceSelectionMaps.Clock };

            var readerArgs = $" -i \"{SourceVideoPath}\" -vf \"setpts=PTS/(4)\"";
            await videoTransforms.RunFFmpegWithStreamOutputImagesAsync(readerArgs, timeSlice.AddFrame, stdError => Debug.WriteLine("Decode: " + stdError), default);

            Debug.WriteLine("Used frames: " + timeSlice.UsedFrames);
            Debug.WriteLine("Skipped frames: " + timeSlice.SkippedFrames);

            var sw = Stopwatch.StartNew();
            var frames = timeSlice.GenerateTimeSliceVideo();
            var writerArgs = $"-pix_fmt yuv420p -y {TargetPath}";
            await videoTransforms.RunFFmpegWithStreamInputImagesAsync(25, writerArgs, frames, stdError => Debug.WriteLine("Encode: " + stdError), default);
            Debug.WriteLine(sw.Elapsed);

            Debug.Assert(File.Exists(TargetPath), "Output file not found");
        }
    }
}
