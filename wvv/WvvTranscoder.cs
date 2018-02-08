using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace wvv
{
    public class WvvTranscoder
    {
        public enum Format
        {
            AVI,
            HEVC,
            MP4,
            WMV
        }

        public object ClientData { get; set; }

        private MediaEncodingProfile mProfile;

        public Format OutputFormat { get; private set; }

        public WvvTranscoder(Format outFormat= Format.MP4, VideoEncodingQuality quality= VideoEncodingQuality.Auto)
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
            }
            else
            {
                Error = op.FailureReason;
                return false;
            }
        }
    }
}
