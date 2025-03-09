using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        internal void Sort()
        {
            var list = Items.ToList();
            list.Sort(this);
            Clear();
            foreach (var item in list)
                Add(item);
        }

        public string? FilterText 
        { 
            get => _filterText;
            set
            {
                if (string.IsNullOrEmpty(value))
                    value = null;
                if (value != _filterText)
                {
                    _filterText = value;
                    Sort();
                }
            }
        }
        private string? _filterText;

        /// <summary> Return new item or existing item if one with the same name already exists </summary>
        public PictureItemViewModel InsertOrdered(PictureItemViewModel item)
        {
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
            var compareName = string.Compare(x.Name, y.Name, StringComparison.CurrentCultureIgnoreCase);
            if (compareName != 0)
                return compareName;
            return string.Compare(x.FullPath, y.FullPath, StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
