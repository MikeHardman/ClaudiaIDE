using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using ClaudiaIDE.Settings;
using Microsoft.VisualStudio.Text.Editor;
using System.Threading;
using System.Collections;
using System.Net;
using Newtonsoft.Json;

namespace ClaudiaIDE
{
    public class OnlineSlideShowImageProvider : IImageProvider
    {
        private readonly Timer _timer;
        private Setting _setting;
        private OnlineImageFiles _imageFiles;
        private IEnumerator<string> _imageFilesPath;

        public OnlineSlideShowImageProvider(Setting setting)        
        {
            _setting = setting;

            _imageFiles = GetImageUrlsFromAlbum();
            _imageFilesPath = _imageFiles.GetEnumerator();
            ChangeImage(null);

            _timer = new Timer(new TimerCallback(ChangeImage));
            _timer.Change((int)_setting.UpdateImageInterval.TotalMilliseconds, (int)_setting.UpdateImageInterval.TotalMilliseconds);
        }

        private OnlineImageFiles GetImageUrlsFromAlbum()
        {
            return new OnlineImageFiles { Extensions = _setting.Extensions, ImageAlbumUrl = _setting.BackgroundImageAlbumUrl };
        }
        //private ImageFiles GetImagesFromDirectory()
        //{
        //    return new ImageFiles { Extensions = _setting.Extensions, ImageDirectoryPath = _setting.BackgroundImagesDirectoryAbsolutePath };
        //}

        public BitmapImage GetBitmap(IWpfTextView provider)
        {
            var current = _imageFilesPath.Current;
            if (string.IsNullOrEmpty(current)) return null;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(current, UriKind.Absolute);
            bitmap.EndInit();
            //bitmap.Freeze();
            return bitmap;
        }

        public void ReloadSettings()
        {
            if (_setting.ImageBackgroundType == ImageBackgroundType.Single)
            {
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            else if(_setting.ImageAlbumProvider != ImageAlbumProvider.Local)
            {
                _imageFiles = GetImageUrlsFromAlbum();
                _imageFilesPath = _imageFiles.GetEnumerator();
                ChangeImage(null);
                _timer.Change(0, (int)_setting.UpdateImageInterval.TotalMilliseconds);
            }
        }

        public event EventHandler NewImageAvaliable;

        private void ChangeImage(object args)
        {
            if (_imageFilesPath.MoveNext())
            {
                NewImageAvaliable?.Invoke(this, EventArgs.Empty);
            }
        }

        public ImageBackgroundType ProviderType
        {
            get
            {
                return ImageBackgroundType.OnlineSlideshow;
            }
        }
    }

    public class OnlineImageFiles : IEnumerable<string>
    {
        public string Extensions { get; set; }
        public string ImageAlbumUrl { get; set; }

        private List<string> ImageFilePaths;

        public IEnumerator<string> GetEnumerator()
        {
            if (string.IsNullOrEmpty(Extensions) || string.IsNullOrEmpty(ImageAlbumUrl)) yield return "";

            var parser = new ImgurAlbumParser(ImageAlbumUrl);

            var extensions = Extensions.Split(new[] { ",", " " }, StringSplitOptions.RemoveEmptyEntries);

            ImageFilePaths = parser.Images.OrderBy(x => Guid.NewGuid()).ToList();
            
            //ImageFilePaths = Directory.GetFiles(new DirectoryInfo(ImageDirectoryPath).FullName)
            //    .Where(x => extensions.Contains(Path.GetExtension(x)))
            //    .OrderBy(x => Guid.NewGuid())
            //    .ToList();

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

    public class ImgurAlbumParser
    {
        string _albumId;

        public ImgurAlbumParser(string url)
        {
            //first lets make sure this is an imgur album url... so we're looking for /a/{ID} at the end.
            var uri = new Uri(url);
            var path = uri.PathAndQuery;
            var parts = path.TrimStart('/').Split('/');
            if(parts[0] != "a")
            {
                throw new InvalidOperationException("INVALID IMGUR ALBUM URL");
            }
            _albumId = parts[1];
        }

        public List<string> Images { get
            {
                var images = new List<string>();
                try
                {
                    var response = MakeImgurWebRequest(_albumId);

                    var result = JsonConvert.DeserializeObject<ImgurRequestWrapper<ImgurAlbum>>(response);
                    images = result.data.images.Select(i => i.link).ToList();
                }
                catch
                {

                }
                return images;
            }
        }


        public string MakeImgurWebRequest(string AlbumId)
        {
            HttpWebRequest request = WebRequest.Create("https://api.imgur.com/3/album/" + AlbumId) as HttpWebRequest;
            request.Headers.Add("Authorization: Client-ID 09446a94d1182cc");
            using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
            {
                StreamReader reader = new StreamReader(response.GetResponseStream());
                return reader.ReadToEnd();
            }
        }
    }

    public class ImgurRequestWrapper<T>
    {
        public T data;
    }

    public class ImgurAlbum
    {
        public string id { get; set; }
        public List<ImgurImage> images { get; set; }
    }

    public class ImgurImage
    {
        public string link { get; set; }
    }
}