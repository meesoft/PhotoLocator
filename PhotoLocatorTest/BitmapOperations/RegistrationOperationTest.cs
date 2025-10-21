namespace PhotoLocator.BitmapOperations;

[TestClass]
public class RegistrationOperationTest
{
    [TestMethod]
    public void Apply_ShouldRegisterToFirst()
    {
        const int Size = 256;
        const int dx = 2;
        const int dy = 3;

        var random = new Random(0);
        var features = new List<(int x, int y)>();
        for (int i = 0; i < 20; i++)
            features.Add((random.Next(10, Size - 10), random.Next(10, Size - 10)));

        var refFrame = new byte[Size * Size];
        foreach (var feature in features)
            refFrame[feature.y * Size + feature.x] = 255;

        var frame1 = new byte[Size * Size];
        foreach (var feature in features)
            frame1[(feature.y + dy) * Size + feature.x + dx] = 255;

        var op = new RegistrationOperation(refFrame, Size, Size, 1, RegistrationOperation.Reference.First, RegistrationOperation.Borders.Black);
        op.Apply(frame1);

        foreach (var feature in features)
            Assert.AreEqual(255, frame1[feature.y * Size + feature.x]);
    }

    [TestMethod]
    public void Apply_ShouldRegisterToPrevious_ReuseFeatures()
    {
        const int Size = 512;
        const int dx = 2;
        const int dy = 3;

        var random = new Random(0);
        var features = new List<(int x, int y)>();
        for (int i = 0; i < 100; i++)
            features.Add((random.Next(10, Size - 10), random.Next(10, Size - 10)));

        var refFrame = new byte[Size * Size];
        foreach (var feature in features)
            refFrame[feature.y * Size + feature.x] = 255;

        var frame1 = new byte[Size * Size];
        foreach (var feature in features)
            frame1[(feature.y + dy) * Size + feature.x + dx] = 255;

        var op = new RegistrationOperation(refFrame, Size, Size, 1, RegistrationOperation.Reference.Previous, RegistrationOperation.Borders.Black);
        op.Apply(frame1);
        op.Apply(frame1);

        foreach (var feature in features)
            Assert.AreEqual(255, frame1[feature.y * Size + feature.x]);
    }

    [TestMethod]
    public void Apply_ShouldRegisterToPrevious_FindNewFeatures()
    {
        const int Size = 256;
        const int dx = 2;
        const int dy = 3;

        var random = new Random(0);
        var features = new List<(int x, int y)>();
        for (int i = 0; i < 20; i++)
            features.Add((random.Next(10, Size - 10), random.Next(10, Size - 10)));

        var refFrame = new byte[Size * Size];
        foreach (var feature in features)
            refFrame[feature.y * Size + feature.x] = 255;

        var frame1 = new byte[Size * Size];
        foreach (var feature in features)
            frame1[(feature.y + dy) * Size + feature.x + dx] = 255;

        var op = new RegistrationOperation(refFrame, Size, Size, 1, RegistrationOperation.Reference.Previous, RegistrationOperation.Borders.Black);
        op.Apply(frame1);
        op.Apply(frame1);

        foreach (var feature in features)
            Assert.AreEqual(255, frame1[feature.y * Size + feature.x]);
    }
}
