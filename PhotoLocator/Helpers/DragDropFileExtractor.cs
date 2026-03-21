using OpenCvSharp.Dnn;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows;

namespace PhotoLocator.Helpers
{
    static class DragDropFileExtractor
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct FILEDESCRIPTOR
        {
            public uint dwFlags;
            public Guid clsid;
            public System.Drawing.Size sizel;
            public System.Drawing.Point pointl;
            public uint dwFileAttributes;
            public FILETIME ftCreationTime;
            public FILETIME ftLastAccessTime;
            public FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
        }

        /// <summary>
        /// Try to extract files from a drag-drop data object. Supports FileDrop and virtual file formats used by cameras (FileGroupDescriptor / FileContents).
        /// Returns saved file paths when any files were extracted.
        /// </summary>
        public static List<string>? TryExtractFiles(System.Windows.IDataObject data, string targetDirectory, Func<string,bool> overwriteCheck)
        {
            // Standard file drop
            if (data.GetDataPresent(DataFormats.FileDrop))
            {
                var saved = new List<string>();
                var droppedObj = data.GetData(DataFormats.FileDrop);
                if (droppedObj is string[] dropped && dropped.Length > 0)
                {
                    foreach (var sourceFileName in dropped)
                    {
                        var targetPath = Path.Combine(targetDirectory, Path.GetFileName(sourceFileName));
                        if (File.Exists(targetPath) && !overwriteCheck(targetPath))
                            continue;
                        File.Copy(sourceFileName, targetPath, true);
                        saved.Add(targetPath);
                    }
                    return saved;
                }
            }

            // Look for FileGroupDescriptorW or FileGroupDescriptor
            var formats = data.GetFormats(true);
            var fgFormat = formats.Contains("FileGroupDescriptorW") ? "FileGroupDescriptorW" : formats.Contains("FileGroupDescriptor") ? "FileGroupDescriptor" : null;
            if (fgFormat is null)
                return null;

            // Read FILEGROUPDESCRIPTOR bytes
            var fgObj = data.GetData(fgFormat);
            if (fgObj is null) return null;
            byte[] bytes;
            if (fgObj is MemoryStream fgStream)
                bytes = fgStream.ToArray();
            else if (fgObj is byte[] b)
                bytes = b;
            else if (fgObj is UnmanagedMemoryStream ums)
            {
                bytes = new byte[ums.Length];
                ums.Read(bytes, 0, bytes.Length);
            }
            else
                return null;

            var fileNames = new List<string>();
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                var ptr = handle.AddrOfPinnedObject();
                int count = Marshal.ReadInt32(ptr);
                var descSize = Marshal.SizeOf<FILEDESCRIPTOR>();
                for (int i = 0; i < count; i++)
                {
                    var itemPtr = IntPtr.Add(ptr, 4 + i * descSize);
                    var fd = Marshal.PtrToStructure<FILEDESCRIPTOR>(itemPtr);
                    fileNames.Add(fd.cFileName);
                }
            }
            finally
            {
                handle.Free();
            }

            if (fileNames.Count == 0) return null;

            // Obtain COM IDataObject
            var unk = Marshal.GetIUnknownForObject(data);
            try
            {
                var iid = typeof(System.Runtime.InteropServices.ComTypes.IDataObject).GUID;
                Marshal.QueryInterface(unk, in iid, out var pDataObj);
                if (pDataObj == IntPtr.Zero) return null;
                try
                {
                    var comData = (System.Runtime.InteropServices.ComTypes.IDataObject)Marshal.GetObjectForIUnknown(pDataObj);
                    // Clipboard format id for FileContents
                    var fileContentsId = System.Windows.Forms.DataFormats.GetFormat("FileContents").Id;

                    var saved = new List<string>();
                    for (int index = 0; index < fileNames.Count; index++)
                    {
                        var fmt = new FORMATETC
                        {
                            cfFormat = (short)fileContentsId,
                            ptd = IntPtr.Zero,
                            dwAspect = DVASPECT.DVASPECT_CONTENT,
                            lindex = index,
                            tymed = TYMED.TYMED_ISTREAM | TYMED.TYMED_HGLOBAL
                        };

                        var medium = new STGMEDIUM();
                        try
                        {
                            comData.GetData(ref fmt, out medium);
                        }
                        catch
                        {
                            continue;
                        }

                        try
                        {
                            var fileName = fileNames[index];
                            var targetPath = Path.Combine(targetDirectory, fileName);
                            if (File.Exists(targetPath) && !overwriteCheck(targetPath))
                                continue;

                            // IStream
                            if (((int)medium.tymed & (int)TYMED.TYMED_ISTREAM) != 0 && medium.unionmember != IntPtr.Zero)
                            {
                                var comStream = (IStream)Marshal.GetObjectForIUnknown(medium.unionmember);
                                using var outFs = File.Create(targetPath);
                                CopyIStreamToStream(comStream, outFs);
                                saved.Add(targetPath);
                            }
                            else if (((int)medium.tymed & (int)TYMED.TYMED_HGLOBAL) != 0 && medium.unionmember != IntPtr.Zero)
                            {
                                // HGLOBAL: lock and copy
                                var hglobal = medium.unionmember;
                                var ptrData = GlobalLock(hglobal);
                                try
                                {
                                    // We don't have size here; attempt to write until null or best-effort using FILEDESCRIPTOR values
                                    // As a fallback, create file from bytes until GlobalSize
                                    var globalSize = GlobalSize(hglobal);
                                    var buffer = new byte[globalSize];
                                    Marshal.Copy(ptrData, buffer, 0, buffer.Length);
                                    File.WriteAllBytes(targetPath, buffer);
                                    saved.Add(targetPath);
                                }
                                finally
                                {
                                    GlobalUnlock(hglobal);
                                }
                            }
                        }
                        finally
                        {
                            ReleaseStgMedium(ref medium);
                        }
                    }

                    return saved.Count > 0 ? saved : null;
                }
                finally
                {
                    Marshal.Release(pDataObj);
                }
            }
            finally
            {
                Marshal.Release(unk);
            }
        }

        static void CopyIStreamToStream(IStream comStream, Stream outStream)
        {
            const int chunk = 64 * 1024;
            var buffer = new byte[chunk];
            var pcbRead = Marshal.AllocCoTaskMem(sizeof(int));
            try
            {
                while (true)
                {
                    comStream.Read(buffer, buffer.Length, pcbRead);
                    int read = Marshal.ReadInt32(pcbRead);
                    if (read == 0) break;
                    outStream.Write(buffer, 0, read);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(pcbRead);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        static extern int GlobalSize(IntPtr hMem);

        [DllImport("ole32.dll"), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        static extern void ReleaseStgMedium(ref STGMEDIUM pmedium);
    }
}