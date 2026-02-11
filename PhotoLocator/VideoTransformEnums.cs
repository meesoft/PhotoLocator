namespace PhotoLocator;

public enum OutputMode
{
    Video,
    ImageSequence,
    Average,
    Max,
    TimeSliceImage,
}

public enum CombineFramesMode
{
    None,
    RollingAverage,
    FadingAverage,
    FadingMax,
    TimeSlice,
    TimeSliceInterpolated,
}

public enum RegistrationMode
{
    Off, ToFirst, ToPrevious
}
