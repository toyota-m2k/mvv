using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;

namespace wvv
{
    /**
     * テンポラリフォルダークラス
     */
    public class WvvTempFolder : IDisposable
    {
        /**
         * テンポラリフォルダ
         */
        public StorageFolder Folder { get; private set; }

        /**
         * LocalCacheFolderの下にテンポラリフォルダを作成する。
         * 
         * Dispose()で、dirName以下を削除する。
         */
        public static Task<WvvTempFolder> Create(string dirName)
        {
            return Create(ApplicationData.Current.LocalCacheFolder, dirName);
        }

        public static StorageFolder TempFolder { get => ApplicationData.Current.LocalCacheFolder; }

        /**
         * baseFolderの下にテンポラリフォルダを作成する。
         * 
         * Dispose()で、dirName以下を削除する。
         */
        public static async Task<WvvTempFolder> Create(StorageFolder baseFolder, string dirName)
        {
            if (null == dirName || dirName.Length == 0)
            {
                throw new ArgumentException("invalid directory name.");
            }

            var folder = await baseFolder.CreateFolderAsync(dirName, CreationCollisionOption.OpenIfExists);
            return new WvvTempFolder(folder);
        }

        private WvvTempFolder(StorageFolder folder)
        {
            Folder = folder;
        }


        public async void Dispose()
        {
            if(null!=Folder)
            {
                var f = Folder;
                Folder = null;
                try
                {
                    await f.DeleteAsync();
                }
                catch(Exception e)
                {
                    Debug.WriteLine(e);
                }
            }
        }

        public async Task<WvvTempFile> CreateTempFile(string prefix, string suffix="")
        {
            return await WvvTempFile.Create(Folder, prefix, suffix);
        }

        public StorageFolder Detach()
        {
            var r = Folder;
            Folder = null;
            return r;
        }
    }

    public class WvvTempFile : IDisposable
    {
        public StorageFile File { get; private set; }

        public static async Task<WvvTempFile> Create(StorageFolder folder, string name, string ext = "")
        {
            if(name==null || name=="")
            {
                name = "w";
            }
            if(ext==null)
            {
                ext = "";
            }
            var file = await folder.CreateFileAsync(name + ext, CreationCollisionOption.GenerateUniqueName);
            return new WvvTempFile(file);
        }

        public WvvTempFile(StorageFile file)
        {
            File = file;
        }

        public async void Dispose()
        {
            if(null!=File)
            {
                var f = File;
                File = null;
                try
                {
                    await f.DeleteAsync();
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
            }
        }

        public StorageFile Detach()
        {
            var r = File;
            File = null;
            return r;
        }
    }
}
