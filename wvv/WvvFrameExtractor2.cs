using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace wvv
{
    public class WvvFrameExtractor2
    {
        /**
         * Extractの途中経過を返すデリゲート
         * コールバック形式のExtractの場合は、すべてのフレームの抽出が終わった後に、frameIndex=-1, frameImage=nullで打ち止めコールが返る。
         * Async形式の場合は、有効なフレームに関してのみコールバックする。
         */
        public delegate void OnBlankThumbnailHandler(WvvFrameExtractor2 sender, ImageSource frameImage);
        public delegate void OnThumbnailExtractedHandler(WvvFrameExtractor2 sender, int frameIndex, ImageSource frameImage);

        public static Task<bool> ExtractAsync(int frameHeight, int frameCount, StorageFile source, OnThumbnailExtractedHandler extracted, OnBlankThumbnailHandler blank=null)
        {
            var ex = new WvvFrameExtractor2(frameHeight, frameCount);
            return ex.ExtractAsync(source, extracted, blank);
        }

        public static Task<bool> ExtractAsync(int frameHeight, int frameCount, MediaClip clip, OnThumbnailExtractedHandler extracted, OnBlankThumbnailHandler blank=null)
        {
            var ex = new WvvFrameExtractor2(frameHeight, frameCount);
            return ex.ExtractAsync(clip, extracted, blank);
        }

        public int ThumbnailHeight
        {
            get; set;
        }
        public int FrameCount
        {
            get; set;
        }

        private int mDoing = 0;

        public Exception Error { get; private set; }

        /**
         * コンストラクタ
         * 
         * @param thumbnailHeight サムネイル画像の高さ
         * @param frameCount フレーム数
         */
        public WvvFrameExtractor2(int thumbnailHeight, int frameCount)
        {
            FrameCount = frameCount;
            ThumbnailHeight = thumbnailHeight;
        }

        public static async Task<BitmapSource> CreateBlankBitmap(int width, int height, Color? color=null)
        {
            byte r = 0xA0, g = 0xA0, b = 0xA0, a = 0xFF;
            if(null!=color)
            {
                var c = color.Value;
                r = c.R;
                g = c.G;
                b = c.B;
                a = c.A;
            }
            var source = new WriteableBitmap(width, height);
            var len = source.PixelBuffer.Length;
            var buff = new byte[len];
            for (int i = 0; i < len; i += 4)
            {
                buff[i] = b;
                buff[i + 1] = g;
                buff[i + 2] = r;
                buff[i + 3] = a;
            }
            var stream = source.PixelBuffer.AsStream();
            await stream.WriteAsync(buff, 0, buff.Length);
            return source;
        }

        /**
         * フレームの抽出処理を開始
         * 
         * @param   source      ソースファイル
         * @param   extracted   取得したフレーム画像をコールバックするハンドラ
         */
        public async Task<bool> ExtractAsync(StorageFile source, OnThumbnailExtractedHandler extracted, OnBlankThumbnailHandler blank)
        {
            var clip = await MediaClip.CreateFromFileAsync(source);
            return await ExtractAsync(clip, extracted, blank);
        }

        public void Cancel()
        {
            mDoing++;
        }


        /**
         * フレームの抽出処理を開始
         * 
         * @param   clip        ソースを保持したMediaClip
         * @param   extracted   取得したフレーム画像をコールバックするハンドラ
         */
        public async Task<bool> ExtractAsync(MediaClip clip, OnThumbnailExtractedHandler extracted, OnBlankThumbnailHandler blank)
        {
            int doing = ++mDoing;
            Error = null;

            // Debug.WriteLine("Logical-DPI = {0}", DisplayInformation.GetForCurrentView().LogicalDpi);
            var composer = new MediaComposition();
            composer.Clips.Add(clip);

            try
            {
                var totalRange = clip.OriginalDuration.TotalMilliseconds;
                var span = totalRange / FrameCount;
                var offset = span / 2;
                for (int n = 0; n < FrameCount; n++)
                {
                    var imageStream = await composer.GetThumbnailAsync(TimeSpan.FromMilliseconds(offset + span * n), 0, ThumbnailHeight, VideoFramePrecision.NearestFrame);
                    if(doing!=mDoing)
                    {
                        // cancelling
                        return false;
                    }
                    var bmp = new BitmapImage();
                    bmp.SetSource(imageStream);

                    if(null!=blank && n==0)
                    {
                        var source = await CreateBlankBitmap(bmp.PixelWidth, bmp.PixelHeight);
#if false
                        var bb = new SoftwareBitmap(BitmapPixelFormat.Bgra8, bmp.PixelWidth, bmp.PixelHeight, BitmapAlphaMode.Ignore);
                        var source = new SoftwareBitmapSource();
                        await source.SetBitmapAsync(bb);
#endif
                        blank(this, source);
                    }

                    extracted(this, n, bmp);
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                Error = e;
                return false;
            }
            finally
            {
                composer.Clips.Clear();
            }
        }

        public async Task<BitmapImage> ExtractSingleFrameAsync(StorageFile source, TimeSpan position)
        {
            var clip = await MediaClip.CreateFromFileAsync(source);
            return await ExtractSingleFrameAsync(clip, position);
        }

        public async Task<BitmapImage> ExtractSingleFrameAsync(MediaClip clip, TimeSpan position)
        {
            Error = null;

            var composer = new MediaComposition();
            composer.Clips.Add(clip);

            try
            {
                var imageStream = await composer.GetThumbnailAsync(position, 0, ThumbnailHeight, VideoFramePrecision.NearestFrame);
                var bmp = new BitmapImage();
                bmp.SetSource(imageStream);
                composer.Clips.Clear();
                return bmp;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                Error = e;
                return null;
            }
            finally
            {
                composer.Clips.Clear();
            }
        }
    }
}