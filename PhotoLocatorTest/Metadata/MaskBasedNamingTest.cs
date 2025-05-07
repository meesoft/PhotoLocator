using System;

namespace PhotoLocator.Metadata
{
    [TestClass]
    public class MaskBasedNamingTest
    {
        static MaskBasedNaming _renamer = null!;

        class FileInfo(string fullPath, DateTimeOffset? timeStamp) : IFileInformation
        {
            public string Name => Path.GetFileName(FullPath);

            public string FullPath { get; } = fullPath;

            public DateTimeOffset? TimeStamp { get; } = timeStamp;
        }

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            var file = new FileInfo(@"TestData\2022-06-17_19.03.02.jpg", new DateTimeOffset(2022, 6, 17, 19, 03, 02, TimeSpan.FromHours(-4)));
            _renamer = new MaskBasedNaming(file, 1);
        }

        [TestMethod]
        public void GetFileNameWithMaskSourceName()
        {
            Assert.AreEqual("2022-06-17_19.03.02.jpg",
                _renamer.GetFileName("|*||ext|"));
        }

        [TestMethod]
        public void GetFileNameWithMaskSourceSkip2()
        {
            Assert.AreEqual("22-06-17_19.03.02.jpg",
                _renamer.GetFileName("|*:2||ext|"));
        }

        [TestMethod]
        public void GetFileNameWithMaskSourceSkipBefore()
        {
            Assert.AreEqual("19.jpg",
                _renamer.GetFileName("|_??||ext|"));
        }

        [TestMethod]
        public void GetFileNameWithMaskCounter()
        {
            Assert.AreEqual("0001.jpg",
                _renamer.GetFileName("|#:4||ext|"));
        }

        [TestMethod]
        public void GetFileNameWithMaskDT()
        {
            Assert.AreEqual("2022-06-17 19.03.02.jpg",
                _renamer.GetFileName("|DT||ext|"));
        }

        [TestMethod]
        public void GetFileNameWithMaskDTplus1()
        {
            Assert.AreEqual("2022-06-17 20.03.02.jpg",
                _renamer.GetFileName("|DT+1||ext|"));
        }

        [TestMethod]
        public void GetFileNameWithMaskExposureSettings()
        {
            Assert.AreEqual("3-0-4-100-101",
                _renamer.GetFileName("|a:0|-|t:0|-|f:0|-|iso|-|alt:0|"));
        }

        [TestMethod]
        public void GetFileNameWithMaskDimensions()
        {
            Assert.AreEqual("0341-0191-2",
                _renamer.GetFileName("|width:4|-|height:4|-|w/h:0|"));
        }
    }
}
