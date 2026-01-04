using PhotoLocator.Helpers;
using PhotoLocator.PictureFileFormats;
using PhotoLocator.Settings;
using System.Diagnostics;

namespace PhotoLocator.BitmapOperations
{
    [TestClass]
    public class TimeSliceOperationTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task GenerateTimeSliceImage_ShouldGenerateFromImages()
        {
            var images = new string[] {
                @"TestData\2022-06-17_19.03.02.jpg",
                @"TestData\2022-06-17_19.03.02.jpg",
            };

            if (!File.Exists(VideoProcessingTest.FFmpegPath))
                Assert.Inconclusive("FFmpegPath not found");
            if (images.Length == 0 || images.Any(f => !File.Exists(f)))
                Assert.Inconclusive("Input images not found");

            var settings = new ObservableSettings() { FFmpegPath = VideoProcessingTest.FFmpegPath };
            var videoTransforms = new VideoProcessing(settings);

            var timeSlice = new TimeSliceOperation { SelectionMapExpression = TimeSliceSelectionMaps.TopRightToBottomLeft };

            const string InputListFileName = "input.txt";
            await File.WriteAllLinesAsync(InputListFileName, images.Select(f => $"file '{f}'"), TestContext.CancellationToken).ConfigureAwait(false);
            var readerArgs = $"-f concat -safe 0 -i \"{InputListFileName}\"";
            await videoTransforms.RunFFmpegWithStreamOutputImagesAsync(readerArgs, timeSlice.AddFrame, stdError => Debug.WriteLine("Out: " + stdError), TestContext.CancellationToken);

            var result = timeSlice.GenerateTimeSliceImage();

            Assert.AreEqual(images.Length, timeSlice.UsedFrames);
            Assert.AreEqual(0, timeSlice.SkippedFrames);
#if DEBUG
            GeneralFileFormatHandler.SaveToFile(result, @"TimeSlice.png");
#endif
        }

        [TestMethod]
        public async Task GenerateTimeSliceImage_ShouldGenerateFromVideo()
        {
            if (!File.Exists(VideoProcessingTest.FFmpegPath))
                Assert.Inconclusive("FFmpegPath not found");

            var settings = new ObservableSettings() { FFmpegPath = VideoProcessingTest.FFmpegPath };
            var videoTransforms = new VideoProcessing(settings);

            var timeSlice = new TimeSliceOperation();
            timeSlice.SelectionMapExpression = TimeSliceSelectionMaps.LeftToRight;
            //timeSlice.SelectionMap.Assign(GeneralFileFormatHandler.LoadFromStream(File.OpenRead(@"DownRight.png"), Rotation.Rotate0, int.MaxValue, true, default), 1);

            var readerArgs = $" -i \"{VideoProcessingTest.SourceVideoPath}\" -vf \"setpts=PTS/(4)\"";
            var readTask = videoTransforms.RunFFmpegWithStreamOutputImagesAsync(readerArgs, timeSlice.AddFrame, stdError => Debug.WriteLine("Decode: " + stdError), TestContext.CancellationToken);
            await readTask;

            var sw = Stopwatch.StartNew();
            var result = timeSlice.GenerateTimeSliceImage();
            Debug.WriteLine(sw.Elapsed);
#if DEBUG
            GeneralFileFormatHandler.SaveToFile(result, @"TimeSlice.png");
#endif
            Assert.AreEqual(5, timeSlice.UsedFrames);
            Assert.AreEqual(0, timeSlice.SkippedFrames);
        }

        [TestMethod]
        public async Task GenerateTimeSliceVideo_ShouldGenerateFromVideo()
        {
            const string TargetPath = @"TimeSlice.mp4";

            if (!File.Exists(VideoProcessingTest.FFmpegPath))
                Assert.Inconclusive("FFmpegPath not found");

            var settings = new ObservableSettings() { FFmpegPath = VideoProcessingTest.FFmpegPath };
            var videoTransforms = new VideoProcessing(settings);

            var timeSlice = new TimeSliceOperation { SelectionMapExpression = TimeSliceSelectionMaps.Clock };

            var readerArgs = $" -i \"{VideoProcessingTest.SourceVideoPath}\" -vf \"setpts=PTS/(4)\"";
            await videoTransforms.RunFFmpegWithStreamOutputImagesAsync(readerArgs, timeSlice.AddFrame, stdError => Debug.WriteLine("Decode: " + stdError), TestContext.CancellationToken);

            var sw = Stopwatch.StartNew();
            var frames = timeSlice.GenerateTimeSliceVideo();
            var writerArgs = $"-pix_fmt yuv420p -y {TargetPath}";
            await videoTransforms.RunFFmpegWithStreamInputImagesAsync(25, writerArgs, frames, stdError => Debug.WriteLine("Encode: " + stdError), TestContext.CancellationToken);
            Debug.WriteLine(sw.Elapsed);

            Debug.Assert(File.Exists(TargetPath), "Output file not found");
            Assert.AreEqual(5, timeSlice.UsedFrames);
            Assert.AreEqual(0, timeSlice.SkippedFrames);
        }
    }
}
