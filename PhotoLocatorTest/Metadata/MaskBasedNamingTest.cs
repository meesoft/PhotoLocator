using System.Windows.Media.Imaging;

namespace PhotoLocator.Metadata
{
    [TestClass]
    public class MaskBasedNamingTest
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        static MaskBasedNaming _renamer;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            var file = new PictureItemViewModel(@"TestData\2022-06-17_19.03.02.jpg", false);
            _renamer = new MaskBasedNaming(file, 1);
        }

        [TestMethod]
        public void GetFileNameWithMask1()
        {
            Assert.AreEqual("2022-08-28 21.41.37.jpg",
                _renamer.GetFileName("|DT||ext|"));
        }

        [TestMethod]
        public void GetFileNameWithMask2()
        {
            Assert.AreEqual("2022-06-17_19.03.02.jpg",
                _renamer.GetFileName("|*||ext|"));
        }

        [TestMethod]
        public void GetFileNameWithMask3()
        {
            Assert.AreEqual("3-0-4-100-101",
                _renamer.GetFileName("|a:0|-|t:0|-|f:0|-|iso|-|alt:0|"));
        }

        [TestMethod]
        public void GetFileNameWithMask4()
        {
            Assert.AreEqual("0341-0191-2",
                _renamer.GetFileName("|width:4|-|height:4|-|w/h:0|"));
        }
    }
}
