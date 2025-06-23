# PhotoLocator

![Icon](./PhotoLocator/Resources/PhotoLocator.png)

## Photo and video geotagging and location browsing
PhotoLocator can import GPS traces from various formats and then apply the GPS positions to picture and video files by 
synchronizing with the file metadata timestamps. GPS tags can also be imported from other already geotagged photos.
The locations can either be set automatically based on timestamps or manually by copy/pasting from other files or selecting 
on the map. PhotoLocator can perform a lossless update of the EXIF data in the target files directly or in copies of the files.

If you need to save geotags to other than JPEG files you need to download [ExifTool](https://exiftool.org/) and setup the path 
to it in the Settings dialog.

PhotoLocator supports GPX, KML, GeoJSON and Google timeline export files. GPX files can be exported by many location and 
sports tracking apps. 

![Screenshot](./Screenshot.jpg)

## JPEG, raw and video file preview
PhotoLocator can also preview JPEG, raw and video files and you can delete unwanted files. Note that to be able to preview 
some raw image formats you need to install the Raw Image Extension from the Microsoft Store.

![Screenshot](./SplitViewScreenshot.jpg)

## Mask based automatic renaming
Selected files can be automatically renamed based on timestamp, dimensions and other metadata tags.

## EXIF timestamp adjustment
Image timestamps can be adjusted if the camera time setting was wrong. 
This can be useful to correct e.g. missing daylight saving or time zone adjustment.

## Slideshow with location map
There is also a full screen slideshow feature with a small location map display. The slideshow can also play video files.
You can drag/drop in files and folders to do a slideshow across multiple folders. 

Note that it is possible to use Chrome's full screen cast feature to display the slideshow on a TV with Chromecast.

![Screenshot](./SlideshowScreenshot.jpg)

## Basic photo and video editing
* Lossless JPEG crop and rotation.

* Local contrast, brightness and color correction for photos and video clips.

* Uses jpegli for better compression of processed files.

* Combine or crop video clips.

* Combine image files to video for e.g. timelapse.

* Extract frames from video to image files.

* Stabilize shaky video.

* Rotate, adjust brightness, saturation and contrast and denoise video files

* Generate average and max frames for video for e.g. timelapse or long exposure simulation.

## Troubleshooting
* If PhotoLocator cannot start, you need to install the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0/runtime).

* You can make ExifTool ignore minor errors by renaming it to exiftool(-m).exe and updating the path under Settings accordingly.

## Why PhotoLocator?
I have been using GeoSetter for many years but it has not been maintained for a long time and then it completely stopped working 
for me. Since I was unable to find a suitable replacement I decided to make a new geotagging application and make it open source.

## Version history
https://github.com/meesoft/PhotoLocator/releases

## Source code
PhotoLocator is written in C# targeting .net8.0 for Windows.

The source code is available at https://github.com/meesoft/PhotoLocator

Released binaries will be made available at http://meesoft.com/PhotoLocator and the [Microsoft Store](https://apps.microsoft.com/store/detail/9P22GWVGDWN9?cid=DevShareMCLPCS)

Note that the source code for JpegTransform will not build. It is only included to document the command line interface.

## License
[Ms-PL](LICENSE)
