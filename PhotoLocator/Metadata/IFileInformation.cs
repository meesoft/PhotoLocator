using System;

namespace PhotoLocator.Metadata
{
    interface IFileInformation
    {
        string Name { get; }

        string FullPath { get; }

        public DateTime? TimeStamp { get; }
    }
}
