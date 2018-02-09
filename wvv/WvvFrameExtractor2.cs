using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Storage;
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
        public delegate void OnThumbnailExtractedHandler(WvvFrameExtractor2 sender, int frameIndex, ImageSource frameImage);

        public static Task<bool> ExtractAsync(int frameHeight, int frameCount, StorageFile source, OnThumbnailExtractedHandler extracted)
        {
            var ex = new WvvFrameExtractor2(frameHeight, frameCount);
            return ex.ExtractAsync(source, extracted);
        }

        public static Task<bool> ExtractAsync(int frameHeight, int frameCount, MediaClip clip, OnThumbnailExtractedHandler extracted)
        {
            var ex = new WvvFrameExtractor2(frameHeight, frameCount);
            return ex.ExtractAsync(clip, extracted);
        }

        private int mFrameCount;
        private int mThumbnailHeight;

        public Exception Error { get; private set; }

        /**
         * コンストラクタ
         * 
         * @param thumbnailHeight サムネイル画像の高さ
         * @param frameCount フレーム数
         */
        public WvvFrameExtractor2(int thumbnailHeight, int frameCount)
        {
            mFrameCount = frameCount;
            mThumbnailHeight = thumbnailHeight;
        }

        /**
         * フレームの抽出処理を開始
         * 
         * @param   source      ソースファイル
         * @param   extracted   取得したフレーム画像をコールバックするハンドラ
         */
        public async Task<bool> ExtractAsync(StorageFile source, OnThumbnailExtractedHandler extracted)
        {
            var clip = await MediaClip.CreateFromFileAsync(source);
            return await ExtractAsync(clip, extracted);
        }

        /**
         * フレームの抽出処理を開始
         * 
         * @param   clip        ソースを保持したMediaClip
         * @param   extracted   取得したフレーム画像をコールバックするハンドラ
         */
        public async Task<bool> ExtractAsync(MediaClip clip, OnThumbnailExtractedHandler extracted)
        {
            Error = null;

            // Debug.WriteLine("Logical-DPI = {0}", DisplayInformation.GetForCurrentView().LogicalDpi);
            var composer = new MediaComposition();
            composer.Clips.Add(clip);

            try
            {
                var totalRange = clip.OriginalDuration.TotalMilliseconds;
                var span = totalRange / mFrameCount;
                var offset = span / 2;
                for (int n = 0; n < mFrameCount; n++)
                {
                    var imageStream = await composer.GetThumbnailAsync(TimeSpan.FromMilliseconds(offset + span * n), 0, mThumbnailHeight, VideoFramePrecision.NearestFrame);
                    var bmp = new BitmapImage();
                    bmp.SetSource(imageStream);
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
                var imageStream = await composer.GetThumbnailAsync(position, 0, mThumbnailHeight, VideoFramePrecision.NearestFrame);
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