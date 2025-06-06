﻿using MapControl;
using Moq;
using PhotoLocator.Gps;
using PhotoLocator.Settings;

namespace PhotoLocator
{
    [TestClass]
    public class AutoTagViewModelTest
    {
        [TestMethod]
        public async Task AutoTag_ShouldSetGeotag_WithMaxTimestampDifference()
        {
            var file = new PictureItemViewModel(@"TestData\2022-06-17_19.03.02.jpg", false, (s, e) => { }, null);
            await file.LoadThumbnailAndMetadataAsync(default);

            var trace = new GpsTrace();
            var location = new Location(1, 2);
            trace.Locations.Add(location);
            trace.TimeStamps.Add(file.TimeStamp!.Value.AddMinutes(1).UtcDateTime);
            var settings = Mock.Of<IRegistrySettings>();
            var vm = new AutoTagViewModel([file], [file], [], () => { }, settings);
            vm.MaxTimestampDifference = 1;
            vm.AutoTag([trace]);

            Assert.AreEqual(location.Latitude, file.GeoTag!.Latitude);
            Assert.AreEqual(location.Longitude, file.GeoTag.Longitude);
        }

        [TestMethod]
        public async Task AutoTag_ShouldSetGeotag_WithTimestampOffset()
        {
            var file = new PictureItemViewModel(@"TestData\2022-06-17_19.03.02.jpg", false, (s, e) => { }, null);
            await file.LoadThumbnailAndMetadataAsync(default);

            var trace = new GpsTrace();
            var location = new Location(1, 2);
            trace.Locations.Add(location);
            trace.TimeStamps.Add(file.TimeStamp!.Value.AddHours(1).UtcDateTime);
            var settings = Mock.Of<IRegistrySettings>();
            var vm = new AutoTagViewModel([file], [file], [], () => { }, settings);
            vm.MaxTimestampDifference = 0;
            vm.TimestampOffset = "1:00";
            vm.AutoTag([trace]);

            Assert.AreEqual(location.Latitude, file.GeoTag!.Latitude);
            Assert.AreEqual(location.Longitude, file.GeoTag.Longitude);
        }

        [TestMethod]
        public async Task AutoTag_ShouldSetGeotag_WithNegativeTimestampOffset()
        {
            var file = new PictureItemViewModel(@"TestData\2022-06-17_19.03.02.jpg", false, (s, e) => { }, null);
            await file.LoadThumbnailAndMetadataAsync(default);

            var trace = new GpsTrace();
            var location = new Location(1, 2);
            trace.Locations.Add(location);
            trace.TimeStamps.Add(file.TimeStamp!.Value.AddMinutes(-1).UtcDateTime);
            var settings = Mock.Of<IRegistrySettings>();
            var vm = new AutoTagViewModel([file], [file], [], () => { }, settings);
            vm.MaxTimestampDifference = 0;
            vm.TimestampOffset = "-0:01";
            vm.AutoTag([trace]);

            Assert.AreEqual(location.Latitude, file.GeoTag!.Latitude);
            Assert.AreEqual(location.Longitude, file.GeoTag.Longitude);
        }
    }
}
