using System;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace wvv
{
    /**
     * 動画ファイルのトランスコード
     */
    public class WvvTranscoder
    {
        public enum Format
        {
            AVI,
            HEVC,
            MP4,
            WMV
        }

        /**
         * ソース動画ファイルのサイズにあわせて、EncodingProfileに、いい感じのサイズを設定する(HD720用）
         */
        public static MediaEncodingProfile SetFeelGoodSizeToEncodingProfileForHD720(MediaClip clip, MediaEncodingProfile profile)
        {
            // 動画のサイズ情報取得
            var prop = clip.GetVideoEncodingProperties();
            double width = prop.Width, height = prop.Height;
            double numerator = prop.PixelAspectRatio.Numerator, denominator = prop.PixelAspectRatio.Denominator;

            // PixelAspectRatio の考慮
            if ((numerator != 1 || denominator != 1) && numerator != 0 && denominator != 0)
            {
                width = width * numerator / denominator;
            }

            // - 短いほうの辺の長さが、720以下となり、かつ、
            // - 長いほうの辺の長さが、1280以下となるように、縮小。（拡大はしない）
            double r;
            if (width > height)
            {
                // 横長
                r = Math.Min(1280.0 / width, 720.0 / height);
            }
            else
            {
                // 縦長
                r = Math.Min(720.0 / width, 1280.0 / height);
            }
            if (r > 1)
            {
                r = 1;  // 拡大はしない
            }
            profile.Video.Width = (uint)Math.Round(width * r);
            profile.Video.Height = (uint)Math.Round(height * r);
            return profile;
        }

        /**
         * HD720用に、EncodingProfileのサイズをいい感じにする。
         */
        public async Task MakeFeelGoodProfileForHD720(StorageFile inputFile)
        {
            var clip = await MediaClip.CreateFromFileAsync(inputFile);
            SetFeelGoodSizeToEncodingProfileForHD720(clip, mProfile);
        }


        public object ClientData { get; set; }

        private MediaEncodingProfile mProfile;

        public Size VideoSize
        {
            get => new Size(mProfile.Video.Width, mProfile.Video.Height);
        }

        public Format OutputFormat { get; private set; }

        public WvvTranscoder(Format outFormat= Format.MP4, VideoEncodingQuality quality= VideoEncodingQuality.HD720p)
        {
            OutputFormat = outFormat;
            switch(outFormat)
            {
                case Format.AVI:
                    mProfile = MediaEncodingProfile.CreateAvi(quality);
                    break;
                case Format.HEVC:
                    mProfile = MediaEncodingProfile.CreateHevc(quality);
                    break;
                case Format.WMV:
                    mProfile = MediaEncodingProfile.CreateWmv(quality);
                    break;
                case Format.MP4:
                default:
                    mProfile = MediaEncodingProfile.CreateMp4(quality);
                    break;
            }
        }

        public TranscodeFailureReason Error { get; private set; }
        private IAsyncInfo mTask;

        public async Task<bool> Transcode(StorageFile inputFile, StorageFile outputFile, IWvvProgress<WvvTranscoder> progress=null)
        {
            var transcoder = new MediaTranscoder();
            var op = await transcoder.PrepareFileTranscodeAsync(inputFile, outputFile, mProfile);
            if (op.CanTranscode)
            {
                try
                {
                    var task = op.TranscodeAsync();
                    mTask = task;
                    if (null != progress)
                    {
                        task.Progress += (s, percent) =>
                        {
                            progress(this, percent);
                        };
                    }
                    await task;
                    return true;
                }
                catch
                {
                    return false;
                }
                finally
                {
                    mTask = null;
                }
            }
            else
            {
                Error = op.FailureReason;
                return false;
            }
        }

        public void Cancel()
        {
            mTask?.Cancel();
        }
    }
}
