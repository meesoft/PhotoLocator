using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace PhotoLocator.Helpers
{
    public class OrderedCollection : ObservableCollection<PictureItemViewModel>, IComparer<PictureItemViewModel>
    {
        /// <summary> Index of item or binary complement of next item </summary>
        internal int BinarySearch(PictureItemViewModel item)
        {
            int min = 0;
            int max = Items.Count - 1;
            while (min <= max)
            {
                int mid = (min + max) / 2;
                var compare = Compare(item, Items[mid]);
                if (compare == 0)
                    return mid;
                if (compare < 0)
                    max = mid - 1;
                else
                    min = mid + 1;
            }
            return ~min;
        }

        public void Sort()
        {
            var list = Items.ToList();
            list.Sort(this);
            Clear();
            foreach (var item in list)
                Add(item);
        }

        public ItemSortOrder SortOrder
        { 
            get;
            set
            {
                if (value != field)
                {
                    field = value;
                    Sort();
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(SortOrder)));
                }
            }
        }

        public string? FilterText 
        { 
            get;
            set
            {
                if (string.IsNullOrEmpty(value))
                    value = null;
                if (value != field)
                {
                    field = value;
                    Sort();
                }
            }
        }

        /// <summary> Return new item or existing item if one with the same name and path already exists </summary>
        public PictureItemViewModel InsertOrdered(PictureItemViewModel item)
        {
            if (SortOrder != ItemSortOrder.Name)
            {
                var existing = Items.FirstOrDefault(i => string.Equals(i.FullPath, item.FullPath, StringComparison.CurrentCultureIgnoreCase));
                if (existing is not null)
                    return existing;
            }
            var index = BinarySearch(item);
            if (index >= 0)
                return Items[index];
            Insert(~index, item);
            return item;
        }

        public int Compare(PictureItemViewModel? x, PictureItemViewModel? y)
        {
            if (x is null || y is null || ReferenceEquals(x, y))
                return 0;
            if (x.IsDirectory && !y.IsDirectory)
                return -1;
            if (y.IsDirectory && !x.IsDirectory)
                return 1;
            if (FilterText is not null)
            {
                var xContainsFilter = x.Name.Contains(FilterText, StringComparison.CurrentCultureIgnoreCase);
                var yContainsFilter = y.Name.Contains(FilterText, StringComparison.CurrentCultureIgnoreCase);
                if (xContainsFilter && !yContainsFilter)
                    return -1;
                if (yContainsFilter && !xContainsFilter)
                    return 1;
            }
            if (SortOrder == ItemSortOrder.FileSize)
            {
                if (x.IsFile && y.IsFile)
                    try
                    {
                        var compareFileSize = new FileInfo(x.FullPath).Length.CompareTo(new FileInfo(y.FullPath).Length);
                        if (compareFileSize != 0)
                            return compareFileSize;
                    }
                    catch { } // Ignore exceptions when accessing file
            }
            else if (SortOrder == ItemSortOrder.ImageTimestamp)
            {
                if (x.TimeStamp.HasValue && !y.TimeStamp.HasValue)
                    return -1;
                if (y.TimeStamp.HasValue && !x.TimeStamp.HasValue)
                    return 1;
                var compareTimeStamp = Nullable.Compare(x.TimeStamp, y.TimeStamp);
                if (compareTimeStamp != 0)
                    return compareTimeStamp;
            }
            else if (SortOrder == ItemSortOrder.FileModifiedTimestamp)
            {
                try
                {
                    var compareFileModified = File.GetLastWriteTime(x.FullPath).CompareTo(File.GetLastWriteTime(y.FullPath));
                    if (compareFileModified != 0)
                        return compareFileModified;
                } catch { } // Ignore exceptions when accessing file
            }
            var compareName = string.Compare(x.Name, y.Name, StringComparison.CurrentCultureIgnoreCase);
            if (compareName != 0)
                return compareName;
            return string.Compare(x.FullPath, y.FullPath, StringComparison.CurrentCultureIgnoreCase);
        }
    }

    public enum ItemSortOrder
    {
        Name = 0,
        FileSize = 1,
        ImageTimestamp = 2,
        FileModifiedTimestamp = 3,
    }
}
