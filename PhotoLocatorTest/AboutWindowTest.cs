namespace PhotoLocator
{
    [TestClass]
    public class AboutWindowTest
    {
        [TestMethod]
        public void LicenseText_ShouldReadAllLicenses()
        {
            Assert.IsNotNull(AboutWindow.LicenseText);
        }
    }
}
