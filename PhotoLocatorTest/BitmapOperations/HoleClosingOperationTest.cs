using System.Diagnostics;

namespace PhotoLocator.BitmapOperations;

[TestClass]
public class HoleClosingOperationTest
{
    [TestMethod]
    public void CloseHolesIteratively_FillsSingleHole()
    {
        int width = 3;
        int height = 1;
        var image = new FloatBitmap(width, height, 1);
        var mask = new FloatBitmap(width, height, 1);
        // set all pixel values to 1 (non-hole)
        for (int x = 0; x < width; x++)
            image[x, 0] = 1f;
        // create a single hole at middle pixel
        image[1, 0] = 0f; // non-hole pixels in mask are 0
        mask[1, 0] = 1f; // hole indicator
        
        HoleClosingOperation.CloseHolesIteratively(image, mask, 0.5f);
        
        // The center pixel should be replaced by the average of its neighbors (both 1)
        Assert.AreEqual(1f, image[1, 0], 1e-6f, "Center pixel should be filled with average of neighbors");
    }

    [TestMethod]
    public void CloseHolesIteratively_FillsAdjacentHoles()
    {
        int width = 5;
        int height = 1;
        var image = new FloatBitmap(width, height, 1);
        var mask = new FloatBitmap(width, height, 1);
        // set all pixel values to 1 (non-hole)
        for (int x = 0; x < width; x++)
            image[x, 0] = 1f;
        // create holes at positions 1 and 3
        image[1, 0] = 0f;
        image[3, 0] = 0f;
        mask[1, 0] = 1f;
        mask[3, 0] = 1f;
        
        HoleClosingOperation.CloseHolesIteratively(image, mask, 0.5f);
        
        Assert.AreEqual(1f, image[1, 0], 1e-6f, "Hole at position 1 should be filled");
        Assert.AreEqual(1f, image[3, 0], 1e-6f, "Hole at position 3 should be filled");
    }

    [TestMethod]
    public void CloseHolesIteratively_FillsBiggerHole()
    {
        var image = new FloatBitmap(
            [ 1, 1, 1, 1 ],
            [ 0.5f, 0, 0, 0 ],
            [ 0.5f, 0, 0, 0 ],
            [ 0.5f, 0, 0, 0 ],
            [ 1, 1, 1, 1 ]);
        var mask = new FloatBitmap(
            [ 0, 0, 0, 0 ],
            [ 0, 1, 1, 1 ],
            [ 0, 1, 1, 1 ],
            [ 0, 1, 1, 1 ],
            [ 0, 0, 0, 0 ]);
        
        HoleClosingOperation.CloseHolesIteratively(image, mask, 0.5f);

        Debug.WriteLine(image);
        Assert.AreEqual(0.8f, image[1, 1], 1e-6f, "Hole at position 1 should be filled");
        Assert.AreEqual(1f, image[3, 3], 1e-6f, "Hole at position 3 should be filled");
    }
}