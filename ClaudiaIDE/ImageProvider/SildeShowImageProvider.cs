using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using ClaudiaIDE.Settings;
using Microsoft.VisualStudio.Text.Editor;
using System.Threading;
using System.Collections;

namespace ClaudiaIDE
{
    public class SlideShowImageProvider : IImageProvider
    {
        private readonly Timer _timer;
        private Setting _setting;
        private ImageFiles _imageFiles;
        private IEnumerator<string> _imageFilesPath;

        public SlideShowImageProvider(Setting setting)
        {
            _setting = setting;

            _imageFiles = GetImagesFromDirectory();
            _imageFilesPath = _imageFiles.GetEnumerator();
            ChangeImage(null);

            _timer = new Timer(new TimerCallback(ChangeImage));
            _timer.Change((int)_setting.UpdateImageInterval.TotalMilliseconds, (int)_setting.UpdateImageInterval.TotalMilliseconds);
        }

        public event EventHandler NewImageAvaliable;

        private ImageFiles GetImagesFromDirectory()
        {
            return new ImageFiles{ Extensions = _setting.Extensions, ImageDirectoryPath = _setting.BackgroundImagesDirectoryAbsolutePath };
        }

        public BitmapImage GetBitmap(IWpfTextView provider)
        {
            var current = _imageFilesPath.Current;
            if (string.IsNullOrEmpty(current)) return null;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(current, UriKind.RelativeOrAbsolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        public void ReloadSettings()
        {
            if( _setting.ImageBackgroundType == ImageBackgroundType.Single)
            {
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            else if (_setting.ImageAlbumProvider == ImageAlbumProvider.Local)
            {
                _imageFiles = GetImagesFromDirectory();
                _imageFilesPath = _imageFiles.GetEnumerator();
                ChangeImage(null);
                _timer.Change(0, (int)_setting.UpdateImageInterval.TotalMilliseconds);
            }
        }

        private void ChangeImage(object args)
        {
            if(_imageFilesPath.MoveNext())
            {
                NewImageAvaliable?.Invoke(this, EventArgs.Empty);
            }
        }

        public ImageBackgroundType ProviderType
        {
            get
            {
                return ImageBackgroundType.Slideshow;
            }
        }
    }

    public class ImageFiles : IEnumerable<string>
    {
        public string Extensions { get; set; }
        public string ImageDirectoryPath { get; set; }

        private List<string> ImageFilePaths;

        public IEnumerator<string> GetEnumerator()
        {
            if (string.IsNullOrEmpty(Extensions) || string.IsNullOrEmpty(ImageDirectoryPath)) yield return "";

            var extensions = Extensions.Split(new[] { ",", " " }, StringSplitOptions.RemoveEmptyEntries);
            ImageFilePaths = Directory.GetFiles(new DirectoryInfo(ImageDirectoryPath).FullName)
                .Where(x => extensions.Contains(Path.GetExtension(x)))
                .OrderBy(x => Guid.NewGuid())
                .ToList();

            if (!ImageFilePaths.Any())
            {
                yield return "";
            }
            else
            {
                while (true)
                {
                    foreach (var path in ImageFilePaths)
                    {
                        yield return path;
                    }
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }


}