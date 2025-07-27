using PhotoLocator.Helpers;
using PhotoLocator.PictureFileFormats;
using PhotoLocator.Settings;
using System.Diagnostics;
using System.Windows.Media.Imaging;

namespace PhotoLocator.BitmapOperations
{
    [TestClass]
    public class TimeSliceOperationTest
    {
        [TestMethod]
        public async Task TimeSlice()
        {
            const string SourceVideoPath = @"";

            if (!File.Exists(VideoProcessingTest.FFmpegPath))
                Assert.Inconclusive("FFmpegPath not found");
            if (!File.Exists(SourceVideoPath))
                Assert.Inconclusive("SourceVideoPath not found");


            var settings = new ObservableSettings() { FFmpegPath = VideoProcessingTest.FFmpegPath };
            var videoTransforms = new VideoProcessing(settings);

            var timeSlice = new TimeSliceOperation();
            timeSlice.SelectionMap = new FloatBitmap();
            timeSlice.SelectionMap.Assign(GeneralFileFormatHandler.LoadFromStream(File.OpenRead(@"Resources\LeftToRight.png"), Rotation.Rotate0, int.MaxValue, true, default), 1);

            var readerArgs = $" -i \"{SourceVideoPath}\" -vf \"setpts=PTS/(4)\"";
            var readTask = videoTransforms.RunFFmpegWithStreamOutputImagesAsync(readerArgs, timeSlice.AddFrame, stdError => Debug.WriteLine("Out: " + stdError), default);

            await readTask;

            Debug.WriteLine("Number of frames: " + timeSlice.NumberOfFrames);

            var result = timeSlice.GenerateTimeSliceImage();

            GeneralFileFormatHandler.SaveToFile(result, @"z:\TimeSlice.png");
        }
    }
}
