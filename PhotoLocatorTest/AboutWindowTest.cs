namespace PhotoLocator
{
    [TestClass]
    public class AboutWindowTest
    {
        [TestMethod]
        public void LicenseText_ShouldReadAllLicenses()
        {
            var licenseText = AboutWindow.LicenseText;
            Assert.IsNotNull(licenseText);
            StringAssert.Contains(licenseText, "jpegli");
        }
    }
}
