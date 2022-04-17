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

        public string OriginalFileName => _file.Name;

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

        static bool TagIs(string tag, string value, out int iColon)
        {
            iColon = -1;
            if (tag.StartsWith(value))
            {
                if (tag.Length == value.Length)
                    return true;
                if (tag[value.Length] == ':')
                {
                    iColon = value.Length;
                    return true;
                }
            }
            return false;
        }

        private static void AppendInt(StringBuilder result, int iColon, string tag, int value)
        {
            if (iColon < 0)
                result.Append(value);
            else
                result.Append(value.ToString("D" + tag[(iColon + 1)..]));
        }

        private void AppendMetadataRational(StringBuilder result, int iColon, string tag, string query1, string query2)
        {
            var metadata = GetMetadata();
            var value = Rational.Decode(metadata?.GetQuery(query1) ?? metadata?.GetQuery(query2));
            if (value != null)
            {
                if (iColon < 0)
                    result.Append(value.ToDouble());
                else
                    result.Append(value.ToDouble().ToString("F" + tag[(iColon + 1)..]));
            }
        }

        private void AppendMetadataInt(StringBuilder result, int iColon, string tag, string query1, string query2)
        {
            var metadata = GetMetadata();
            var value = metadata?.GetQuery(query1) ?? metadata?.GetQuery(query2);
            if (iColon < 0)
                result.Append(value);
            else
                result.Append(Convert.ToInt32(value).ToString("D" + tag[(iColon + 1)..]));
        }

        public string GetFileName(string mask)
        {
            var result = new StringBuilder();
            for (int i = 0; i < mask.Length; i++)
            {
                if (mask[i] == '|')
                {
                    int iColon;
                    int iEnd = mask.IndexOf('|', i + 1);
                    if (iEnd < 0)
                        throw new ArgumentException($"Tag at {i} not closed");
                    var tag = mask[(i + 1)..iEnd];
                    if (tag == "ext")
                        result.Append(Path.GetExtension(OriginalFileName));
                    else if (tag == "*")
                        result.Append(Path.GetFileNameWithoutExtension(OriginalFileName));
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
                    else if (TagIs(tag, "width", out iColon))
                    {
                        AppendInt(result, iColon, tag, GetFrame().PixelWidth);
                    }
                    else if (TagIs(tag, "height", out iColon))
                    {
                        AppendInt(result, iColon, tag, GetFrame().PixelHeight);
                    }
                    else if (TagIs(tag, "w/h", out iColon))
                    {
                        var frame = GetFrame();
                        var whr = (double)frame.PixelWidth / frame.PixelHeight;
                        result.Append(whr.ToString(iColon < 0 ? "F2" : "F" + tag[(iColon + 1)..]));
                    }
                    else if (TagIs(tag, "a", out iColon))
                    {
                        AppendMetadataRational(result, iColon, tag, ExifHandler.LensApertureQuery1, ExifHandler.LensApertureQuery2);
                    }
                    else if (TagIs(tag, "t", out iColon))
                    {
                        AppendMetadataRational(result, iColon, tag, ExifHandler.ExposureTimeQuery1, ExifHandler.ExposureTimeQuery2);
                    }
                    else if (TagIs(tag, "f", out iColon))
                    {
                        AppendMetadataRational(result, iColon, tag, ExifHandler.FocalLengthQuery1, ExifHandler.FocalLengthQuery2);
                    }
                    else if (TagIs(tag, "iso", out iColon))
                    {
                        AppendMetadataInt(result, iColon, tag, ExifHandler.IsoQuery1, ExifHandler.IsoQuery2);
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
