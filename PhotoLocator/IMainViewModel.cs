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

        void AddOrUpdateItem(string fullPath, bool isDirectory, bool selectItem);

        string? ProgressBarText { get; set; }

        Task RunProcessWithProgressBarAsync(Func<Action<double>, CancellationToken, Task> body, string text, PictureItemViewModel? focusItem = null);

        IDisposable PauseFileSystemWatcher();
    }
}