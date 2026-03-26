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

        const int FD_WRITESTIME = 0x20;
        const int FD_FILESIZE = 0x40;

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
            var fgObj = data.GetData("FileGroupDescriptorW"); // Consider checking for the ANSI version "FileGroupDescriptor" if the Unicode one is not available
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
            if (bytes.Length < 4) // At least the count of items
                return null;

            var fileInfos = new List<(string Name, long Size, DateTime LastWriteTime)>();
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                var ptr = handle.AddrOfPinnedObject();
                var count = Marshal.ReadInt32(ptr);
                var descSize = Marshal.SizeOf<FILEDESCRIPTOR>();
                if ((long)count * descSize + 4 > bytes.Length)
                    return null;
                for (int i = 0; i < count; i++)
                {
                    var itemPtr = IntPtr.Add(ptr, 4 + i * descSize);
                    var fd = Marshal.PtrToStructure<FILEDESCRIPTOR>(itemPtr);
                    var fileName = Path.GetFileName(fd.cFileName);
                    if (string.IsNullOrWhiteSpace(fileName))
                        continue;
                    var timeStamp = (fd.dwFlags & FD_WRITESTIME) == 0 ? DateTime.Now :
                        DateTime.FromFileTime((((long)(uint)fd.ftLastWriteTime.dwHighDateTime) << 32) | (uint)fd.ftLastWriteTime.dwLowDateTime);
                    var fileSize = (fd.dwFlags & FD_FILESIZE) == 0 ? -1 : (((long)fd.nFileSizeHigh) << 32) | fd.nFileSizeLow;
                    fileInfos.Add((fileName, fileSize, timeStamp));
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
                        catch (Exception ex)
                        {
                            ExceptionHandler.LogException(ex);
                            continue;
                        }
                        try
                        {
                            var fileInfo = fileInfos[i];
                            var targetPath = Path.Combine(targetDirectory, fileInfo.Name);
                            if (File.Exists(targetPath) && !overwriteCheck(targetPath))
                                continue;

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
                                using (var targetFile = File.Create(targetPath))
                                    memStream.CopyTo(targetFile);
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
                                    if (ptrData == nint.Zero)
                                        throw new IOException("Failed to read file data");
                                    var dataSize = GlobalSize(hglobal);
                                    if (fileInfo.Size >= 0)
                                    {
                                        if (dataSize < fileInfo.Size)
                                            throw new IOException("File size mismatch");
                                        dataSize = fileInfo.Size;
                                    }
                                    var buffer = new byte[dataSize];
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

        [DllImport("kernel32.dll", SetLastError = true), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        static extern long GlobalSize(IntPtr hMem);

        [DllImport("ole32.dll"), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        static extern void ReleaseStgMedium(ref STGMEDIUM pmedium);
    }
}