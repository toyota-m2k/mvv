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

        public static Task<MediaClip> ExtractAsync(int frameHeight, int frameCount, StorageFile source, OnThumbnailExtractedHandler extracted)
        {
            var ex = new WvvFrameExtractor2(frameHeight, frameCount);
            return ex.ExtractAsync(source, extracted);
        }

        private int mFrameCount;
        private int mThumbnailHeight;

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
         * @param   player      Sourceを設定してMediaOpenedの状態になっているMediaPlayerオブジェクト
         * @param   ownerView   Dispatcherを供給するビュー
         * @param   extracted   取得したフレーム画像をコールバックするハンドラ
         */
        public async Task<MediaClip> ExtractAsync(StorageFile source, OnThumbnailExtractedHandler extracted)
        {
            // Debug.WriteLine("Logical-DPI = {0}", DisplayInformation.GetForCurrentView().LogicalDpi);
            var composer = new MediaComposition();
            var clip = await MediaClip.CreateFromFileAsync(source);
            composer.Clips.Add(clip);

            var totalRange = clip.OriginalDuration.TotalMilliseconds;
            var span = totalRange / mFrameCount;
            var offset = span / 2;
            for(int n=0; n<mFrameCount; n++ )
            {
                var imageStream = await composer.GetThumbnailAsync(TimeSpan.FromMilliseconds(offset + span * n), 0, mThumbnailHeight, VideoFramePrecision.NearestFrame);
                var bmp = new BitmapImage();
                bmp.SetSource(imageStream);
                extracted(this, n, bmp);
            }
            composer.Clips.Clear();
            return clip;
        }
    }
}