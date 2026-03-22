using System;
using System.Collections.Generic;
using System.IO;
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
        public static List<string>? TryExtractFiles(System.Windows.IDataObject data, string targetDirectory, Func<string,bool> overwriteCheck, Action<double>? progressCallback)
        {
            // Standard file drop
            if (data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] dropped && dropped.Length > 0)
            {
                var saved = new List<string>();
                for (var i = 0; i < dropped.Length; i++)
                {
                    var targetPath = Path.Combine(targetDirectory, Path.GetFileName(dropped[i]));
                    if (dropped[i] == targetPath || File.Exists(targetPath) && !overwriteCheck(targetPath))
                        continue;
                    File.Copy(dropped[i], targetPath, true);
                    saved.Add(targetPath);
                    progressCallback?.Invoke((i + 1) / (double)dropped.Length);
                }
                return saved;
            }

            // Read FILEGROUPDESCRIPTOR bytes
            var fgObj = data.GetData("FileGroupDescriptorW");
            if (fgObj is null) 
                return null;
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

            var fileInfos = new List<(string Name, uint SizeLow, uint SizeHigh, DateTime LastWriteTime)>();
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                var ptr = handle.AddrOfPinnedObject();
                int count = Marshal.ReadInt32(ptr);
                var descSize = Marshal.SizeOf<FILEDESCRIPTOR>();
                if (count * descSize + 4 > bytes.Length)
                    return null;
                for (int i = 0; i < count; i++)
                {
                    var itemPtr = IntPtr.Add(ptr, 4 + i * descSize);
                    var fd = Marshal.PtrToStructure<FILEDESCRIPTOR>(itemPtr);
                    fileInfos.Add((fd.cFileName, fd.nFileSizeLow, fd.nFileSizeHigh, 
                        DateTime.FromFileTime((((long)fd.ftLastWriteTime.dwHighDateTime) << 32) + fd.ftLastWriteTime.dwLowDateTime)));
                }
            }
            finally
            {
                handle.Free();
            }
            if (fileInfos.Count == 0) 
                return null;

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
                    for (int i = 0; i < fileInfos.Count; i++)
                    {
                        var fmt = new FORMATETC
                        {
                            cfFormat = (short)fileContentsId,
                            ptd = IntPtr.Zero,
                            dwAspect = DVASPECT.DVASPECT_CONTENT,
                            lindex = i,
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
                            var fileInfo = fileInfos[i];
                            var targetPath = Path.Combine(targetDirectory, fileInfo.Name);
                            if (File.Exists(targetPath) && !overwriteCheck(targetPath))
                                continue;
                            if (fileInfo.SizeHigh > 0)
                                throw new UserMessageException("Huge file import not supported");

                            // IStream
                            if ((medium.tymed & TYMED.TYMED_ISTREAM) != 0 && medium.unionmember != IntPtr.Zero)
                            {
                                using var memStream = new MemoryStream();
                                var comStream = (IStream)Marshal.GetObjectForIUnknown(medium.unionmember);
                                try
                                {
                                    CopyIStreamToStream(comStream, memStream);
                                }
                                finally
                                {
                                    Marshal.ReleaseComObject(comStream);
                                }
                                memStream.Position = 0;
                                using (var outFs = File.Create(targetPath))
                                    memStream.CopyTo(outFs);
                                File.SetLastWriteTime(targetPath, fileInfo.LastWriteTime);
                                saved.Add(targetPath);
                            }
                            else if ((medium.tymed & TYMED.TYMED_HGLOBAL) != 0 && medium.unionmember != IntPtr.Zero)
                            {
                                // HGLOBAL: lock and copy
                                var hglobal = medium.unionmember;
                                var ptrData = GlobalLock(hglobal);
                                try
                                {
                                    var buffer = new byte[fileInfo.SizeLow];
                                    Marshal.Copy(ptrData, buffer, 0, buffer.Length);
                                    File.WriteAllBytes(targetPath, buffer);
                                    File.SetLastWriteTime(targetPath, fileInfo.LastWriteTime);
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
                        progressCallback?.Invoke((i + 1) / (double)fileInfos.Count);
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

        static void CopyIStreamToStream(IStream sourceStream, Stream outStream)
        {
            const int Chunk = 64 * 1024;
            var buffer = new byte[Chunk];
            var pcbRead = Marshal.AllocCoTaskMem(sizeof(int));
            try
            {
                while (true)
                {
                    sourceStream.Read(buffer, buffer.Length, pcbRead);
                    int read = Marshal.ReadInt32(pcbRead);
                    if (read == 0) 
                        break;
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

        [DllImport("ole32.dll"), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        static extern void ReleaseStgMedium(ref STGMEDIUM pmedium);
    }
}