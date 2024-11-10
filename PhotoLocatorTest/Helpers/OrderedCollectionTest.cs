namespace PhotoLocator.Helpers
{
    [TestClass]
    public class OrderedCollectionTest
    {
        [TestMethod]
        public void BinarySearch_ShouldReturnCorrectIndex()
        {
            var collection = new OrderedCollection();
            Assert.AreEqual(0, ~collection.BinarySearch(new PictureItemViewModel("0", false, (s, o) => { }, null)));

            collection.InsertOrdered(new PictureItemViewModel("0", false, (s, o) => { }, null));
            collection.InsertOrdered(new PictureItemViewModel("1", false, (s, o) => { }, null));
            collection.InsertOrdered(new PictureItemViewModel("3", false, (s, o) => { }, null));
            collection.InsertOrdered(new PictureItemViewModel("4", false, (s, o) => { }, null));

            Assert.AreEqual(1, collection.BinarySearch(new PictureItemViewModel("1", false, (s, o) => { }, null)));
            Assert.AreEqual(3, collection.BinarySearch(new PictureItemViewModel("4", false, (s, o) => { }, null)));
            Assert.AreEqual(2, ~collection.BinarySearch(new PictureItemViewModel("2", false, (s, o) => { }, null)));
            Assert.AreEqual(4, ~collection.BinarySearch(new PictureItemViewModel("5", false, (s, o) => { }, null)));
        }
    }
}
