using MapControl;

namespace PhotoLocator.Gps
{
    [TestClass]
    public class AutoTaggerTest
    {
        [TestMethod]
        public async Task AutoTag_ShouldSetGeotag_WithMaxTimestampDifference()
        {
            var file = new PictureItemViewModel(@"TestData\2022-06-17_19.03.02.jpg", false, (s, e) => { }, null);
            await file.LoadThumbnailAndMetadataAsync(default);

            var trace = new GpsTrace();
            var location = new Location(1, 2);
            trace.Locations.Add(location);
            trace.TimeStamps.Add(file.TimeStamp!.Value.AddMinutes(-1).UtcDateTime);
            var autoTagger = new AutoTagger([], [trace], default, 1);
            autoTagger.AutoTag([file]);

            Assert.AreEqual(location.Latitude, file.Location!.Latitude);
            Assert.AreEqual(location.Longitude, file.Location.Longitude);
        }
    }
}
