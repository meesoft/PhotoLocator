﻿using PhotoLocator.Settings;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PhotoLocator
{
    public interface IMainViewModel
    {
        ISettings Settings { get; }

        PictureItemViewModel? SelectedItem { get; set; }

        IEnumerable<PictureItemViewModel> GetSelectedItems(bool filesOnly);

        Task SelectFileAsync(string fileName);

        void AddItem(string fullPath, bool isDirectory);

        string? ProgressBarText { get; set; }

        Task RunProcessWithProgressBarAsync(Func<Action<double>, CancellationToken, Task> body, string text, PictureItemViewModel? focusItem = null);
    }
}