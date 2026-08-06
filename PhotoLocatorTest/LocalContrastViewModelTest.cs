namespace PhotoLocator;

[TestClass]
public class LocalContrastViewModelTest
{
    [TestMethod]
    public void CopyAndPasteAdjustments_ShouldCopyAndRestoreAdjustments()
    {
        var vm1 = new LocalContrastViewModel();
        vm1.ToneMapping = 1.1;
        vm1.MaxStretch = 70;
        vm1.ToneAdjustments[0].AdjustHue = 0.1f;
        vm1.ToneAdjustments[1].HueUniformity = 0.1f;
        vm1.CopyAdjustmentsCommand.Execute(null);

        var vm2 = new LocalContrastViewModel();
        vm2.PasteAdjustmentsCommand.Execute(null);

        Assert.AreEqual(vm1.ToneMapping, vm2.ToneMapping);
        Assert.AreEqual(vm1.MaxStretch, vm2.MaxStretch);
        Assert.AreEqual(vm1.ToneAdjustments[0].AdjustHue, vm2.ToneAdjustments[0].AdjustHue);
        Assert.AreEqual(vm1.ToneAdjustments[1].HueUniformity, vm2.ToneAdjustments[1].HueUniformity);
    }
}
