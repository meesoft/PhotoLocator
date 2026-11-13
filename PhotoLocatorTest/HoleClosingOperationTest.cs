using Microsoft.VisualStudio.TestTools.UnitTesting;
using PhotoLocator.BitmapOperations;
using System;

namespace PhotoLocator
{
    [TestClass]
    public class HoleClosingOperationTest
    {
        [TestMethod]
        public void CloseHolesIteratively_FillsSingleHole()
        {
            // Arrange
            int width = 3;
            int height = 1;
            int planes = 1;
            var image = new FloatBitmap(width, height, planes);
            var mask = new FloatBitmap(width, height, planes);
            // set all pixel values to 1 (non-hole)
            for (int x = 0; x < width; x++)
                image[x, 0] = 1f;
            // create a single hole at middle pixel
            image[1, 0] = 0f;
            mask[1, 0] = 1f; // hole indicator
            // non-hole pixels in mask are 0
            float threshold = 0.5f;
            // Act
            HoleClosingOperation.CloseHolesIteratively(image, mask, threshold);
            // Assert
            // The center pixel should be replaced by the average of its neighbors (both 1)
            Assert.AreEqual(1f, image[1, 0], 1e-6f, "Center pixel should be filled with average of neighbors");
        }

        [TestMethod]
        public void CloseHolesIteratively_FillsAdjacentHoles()
        {
            // Arrange
            int width = 5;
            int height = 1;
            int planes = 1;
            var image = new FloatBitmap(width, height, planes);
            var mask = new FloatBitmap(width, height, planes);
            // set all pixel values to 1 (non-hole)
            for (int x = 0; x < width; x++)
                image[x, 0] = 1f;
            // create holes at positions 1 and 3
            image[1, 0] = 0f;
            image[3, 0] = 0f;
            mask[1, 0] = 1f;
            mask[3, 0] = 1f;
            float threshold = 0.5f;
            // Act
            HoleClosingOperation.CloseHolesIteratively(image, mask, threshold);
            // Assert
            Assert.AreEqual(1f, image[1, 0], 1e-6f, "Hole at position 1 should be filled");
            Assert.AreEqual(1f, image[3, 0], 1e-6f, "Hole at position 3 should be filled");
        }
    }
}