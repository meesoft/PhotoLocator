using MapControl;
using Moq;
using PhotoLocator.Gps;
using PhotoLocator.Settings;

namespace PhotoLocator
{
    [TestClass]
    public class AutoTagViewModelTest
    {
        [TestMethod]
        public async Task AutoTag_ShouldSetGeotag()
        {
            var file = new PictureItemViewModel(@"TestData\2022-06-17_19.03.02.jpg", false, (s, e) => { }, null);
            file.IsSelected = true;
            await file.LoadThumbnailAndMetadataAsync(CancellationToken.None);

            var trace = new GpsTrace();
            var location = new Location(1, 2);
            trace.Locations.Add(location);
            trace.TimeStamps.Add(file.TimeStamp!.Value.AddMinutes(1));
            var settings = Mock.Of<IRegistrySettings>();
            var vm = new AutoTagViewModel([file], [file], [trace], () => { }, settings);
            vm.MaxTimestampDifference = 2;
            vm.AutoTag(vm.GpsTraces);

            Assert.AreEqual(location.Latitude, file.GeoTag!.Latitude);
            Assert.AreEqual(location.Longitude, file.GeoTag.Longitude);
        }
    }
}
