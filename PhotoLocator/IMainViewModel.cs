using PhotoLocator.Settings;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhotoLocator
{
    public interface IMainViewModel
    {
        public ISettings Settings { get; }

        PictureItemViewModel? SelectedPicture { get; set; }

        IEnumerable<PictureItemViewModel> GetSelectedItems();

        public string? ProgressBarText { get; set; }

        Task RunProcessWithProgressBarAsync(Func<Action<double>, Task> body, string text);
    }
}