using System;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace PhotoLocator.Metadata
{
    sealed class MaskBasedNaming : IDisposable
    {
        readonly PictureItemViewModel _file;
        FileStream? _fileStream;
        BitmapFrame? _frame;
        BitmapMetadata? _metadata;

        public MaskBasedNaming(PictureItemViewModel file)
        {
            _file = file;
        }

        public void Dispose()
        {
            _metadata = null;
            _frame = null;
            _fileStream?.Dispose();
            _fileStream = null;
        }

        DateTime GetTimestamp()
        {
            if (_file.TimeStamp.HasValue)
                return _file.TimeStamp.Value;
            return File.GetCreationTimeUtc(_file.FullPath);
        }

        BitmapFrame GetFrame()
        {
            if (_frame is null)
            {
                _fileStream = File.OpenRead(_file.FullPath);
                var decoder = BitmapDecoder.Create(_fileStream, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnDemand);
                _frame = decoder.Frames[0];
            }
            return _frame;
        }

        BitmapMetadata? GetMetadata()
        {
            if (_metadata is null)
                _metadata = GetFrame().Metadata as BitmapMetadata;
            return _metadata;
        }

        public string GetFileName(string mask)
        {
            var result = new StringBuilder();
            for (int i = 0; i < mask.Length; i++)
            {
                if (mask[i] == '|')
                {
                    int iEnd = mask.IndexOf('|', i + 1);
                    if (iEnd < 0)
                        throw new ArgumentException($"Tag at {i} not closed");
                    var tag = mask[(i + 1)..iEnd];
                    if (tag == "ext")
                        result.Append(Path.GetExtension(_file.Name));
                    else if (tag == "*")
                        result.Append(Path.GetFileNameWithoutExtension(_file.Name));
                    else if (tag == "D")
                        result.Append(GetTimestamp().ToString("yyyy-MM-dd"));
                    else if (tag == "T")
                        result.Append(GetTimestamp().ToString("HH.mm.ss"));
                    else if (tag == "DT")
                        result.Append(GetTimestamp().ToString("yyyy-MM-dd HH.mm.ss"));
                    else if (tag.EndsWith('?'))
                    {
                        var iFirstWildcard = tag.IndexOf('?');
                        var nChars = tag.Length - iFirstWildcard;
                        var prefix = tag[0..iFirstWildcard];
                        var iPrefix = _file.Name.IndexOf(prefix);
                        if (iPrefix < 0)
                            throw new ArgumentException($"Search string '{prefix}' not found in name '{_file.Name}'");
                        result.Append(_file.Name.AsSpan(iPrefix + prefix.Length, nChars));
                    }
                    else if (tag.StartsWith("width"))
                    {
                        int iDigits = tag.IndexOf(':');
                        if (iDigits < 0)
                            result.Append(GetFrame().PixelWidth);
                        else
                            result.Append(GetFrame().PixelWidth.ToString("D" + tag[(iDigits + 1)..]));
                    }
                    else if (tag.StartsWith("height"))
                    {
                        int iDigits = tag.IndexOf(':');
                        if (iDigits < 0)
                            result.Append(GetFrame().PixelHeight);
                        else
                            result.Append(GetFrame().PixelHeight.ToString("D" + tag[(iDigits + 1)..]));
                    }
                    else if (tag.StartsWith("w/h"))
                    {
                        var frame = GetFrame();
                        var whr = (double)frame.PixelWidth / frame.PixelHeight;
                        int iDigits = tag.IndexOf(':');
                        if (iDigits < 0)
                            result.Append(whr.ToString("F2"));
                        else
                            result.Append(whr.ToString("F" + tag[(iDigits + 1)..]));
                    }
                    else if (tag.StartsWith("a"))
                    {
                        var metadata = GetMetadata();
                        var aperture = Rational.Decode(metadata?.GetQuery(ExifHandler.LensApertureQuery1) ?? metadata?.GetQuery(ExifHandler.LensApertureQuery2));
                        if (aperture != null)
                        {
                            int iDigits = tag.IndexOf(':');
                            if (iDigits < 0)
                                result.Append(aperture.ToDouble());
                            else
                                result.Append(aperture.ToDouble().ToString("F" + tag[(iDigits + 1)..]));
                        }
                    }
                    else if (tag.StartsWith("t"))
                    {
                        var metadata = GetMetadata();
                        var exposureTime = Rational.Decode(metadata?.GetQuery(ExifHandler.ExposureTimeQuery1) ?? metadata?.GetQuery(ExifHandler.ExposureTimeQuery2));
                        if (exposureTime != null)
                        {
                            int iDigits = tag.IndexOf(':');
                            if (iDigits < 0)
                                result.Append(exposureTime.ToDouble());
                            else
                                result.Append(exposureTime.ToDouble().ToString("F" + tag[(iDigits + 1)..]));
                        }
                    }
                    else if (tag.StartsWith("f"))
                    {
                        var metadata = GetMetadata();
                        var focalLength = Rational.Decode(metadata?.GetQuery(ExifHandler.FocalLengthQuery1) ?? metadata?.GetQuery(ExifHandler.FocalLengthQuery2));
                        if (focalLength != null)
                        {
                            int iDigits = tag.IndexOf(':');
                            if (iDigits < 0)
                                result.Append(focalLength.ToDouble());
                            else
                                result.Append(focalLength.ToDouble().ToString("F" + tag[(iDigits + 1)..]));
                        }
                    }
                    else if (tag.StartsWith("iso"))
                    {
                        var metadata = GetMetadata();
                        var iso = metadata?.GetQuery(ExifHandler.IsoQuery1) ?? metadata?.GetQuery(ExifHandler.IsoQuery2);
                        int iDigits = tag.IndexOf(':');
                        if (iDigits < 0)
                            result.Append(iso);
                        else
                            result.Append(Convert.ToInt32(iso).ToString("D" + tag[(iDigits + 1)..]));
                    }
                    else
                        throw new ArgumentException($"Unsupported tag |{tag}|");
                    i = iEnd;
                }
                else
                    result.Append(mask[i]);
            }
            return result.ToString();
        }
    }
}
