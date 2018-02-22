using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage;
using Windows.Web.Http;

namespace wvv
{
    #region Global Definitions

    /**
     * @param sender    IWvvCacheオブジェクト
     * @param file      キャッシュファイル (エラーが発生していれば、null: エラー情報は、sender.Errorで取得）
     */
    public delegate void WvvDownloadedHandler(IWvvCache sender, StorageFile file);

    /**
     * キャッシュマネージャが管理するキャッシュクラスのi/f定義
     */
    public interface IWvvCache
    {
        /**
         * キャッシュファイルを取得する。
         * @param callback  結果を返すコールバック
         */
        void GetFile(WvvDownloadedHandler callback);
        /**
         * キャッシュファイルを取得する。（非同期版）
         * 
         * @return キャッシュファイル (エラーが発生していれば、null: エラー情報は、Errorプロパティで取得）
         */
        Task<StorageFile> GetFileAsync();

        /**
         * エラー情報
         */
        Exception Error { get; }

        /**
         * キャッシュを解放する（CacheManagerによって削除可能な状態にする）
         */
        void Release();
    }

    #endregion

    /**
     * キャッシュマネージャ
     */
    public class WvvCacheManager
    {
        #region Singleton

        /**
         * シングルトンインスタンス（初期化後呼び出すこと）
         */
        public static WvvCacheManager Instance { get { return sInstance; } }
        private static WvvCacheManager sInstance = null;

        /**
         * 初期化
         */
        public static async Task Init()
        {
            if (null == sInstance)
            {
                sInstance = new WvvCacheManager();
                await sInstance.init();
            }
        }
        /**
         * 初期化＋インスタンス取得
         */
        public static async Task<WvvCacheManager> GetInstance()
        {
            await Init();
            return sInstance;
        }

        #endregion

        #region Privates

        private WvvTempFolder mFolder;
        private Dictionary<string, WvvCache> mCacheList;

        private WvvCacheManager()
        {
        }

        private async Task init()
        {
            if(null==mFolder)
            {
                mFolder = await WvvTempFolder.Create("video-cache");
                mCacheList = new Dictionary<string, WvvCache>();
            }

            var folder = mFolder.Folder;
            var list = await folder.GetFilesAsync();

            string keyAccessed = "System.DateAccessed";
            string keyCreated = "System.DateCreated";
            string keyModified = "System.DateModified";
            var names = new List<string> { keyCreated, keyAccessed, keyModified };
            foreach(var c in list)
            {
                if (c.IsOfType(StorageItemTypes.File))
                {
                    Debug.WriteLine("----------");
                    Debug.WriteLine(c.Name);
                    var dic = await c.Properties.RetrievePropertiesAsync(names);
                    //Debug.WriteLine("  accessed = " + dic["System.DataAccessed"]);
                    foreach(var k in dic.Keys)
                    {
                        var date = dic[k];
                        Debug.WriteLine("  " + k + ": " + date);
                    }
                    var bp = await c.GetBasicPropertiesAsync();
                    Debug.WriteLine("  BP:Modified:" + bp.DateModified);
                    Debug.WriteLine("  BP:ItemDate: " + bp.ItemDate);
                    Debug.WriteLine("  Created: " + c.DateCreated);
                }
            }
        }

        public static async Task TouchFile(StorageFile file)
        {
            await FileIO.AppendLinesAsync(file, new List<string>());

            //var s = await file.OpenAsync(FileAccessMode.ReadWrite,StorageOpenOptions.AllowReadersAndWriters);
            //await 
            //var buff = new byte[1].AsBuffer();
            //s.Seek(0);
            //await s.ReadAsync(buff, 1, Windows.Storage.Streams.InputStreamOptions.None);
            //s.Seek(0);
            //await s.WriteAsync(buff);
            //await s.FlushAsync();
            //s.Dispose();

            //var props = new KeyValuePair<string, object>[]
            //{
            //    new KeyValuePair<string, object>("System.DataAccessed", DateTimeOffset.Now)
            //};
            // await file.Properties.SavePropertiesAsync(props);
            var folder = Instance.CacheFolder.Folder;
            var list = await folder.GetFilesAsync();

            string keyAccessed = "System.DateAccessed";
            string keyCreated = "System.DateCreated";
            string keyModified = "System.DateModified";
            var names = new List<string> { keyCreated, keyAccessed, keyModified };
            foreach (var c in list)
            {
                if (c.IsOfType(StorageItemTypes.File))
                {
                    var bp = await c.GetBasicPropertiesAsync();

                    Debug.WriteLine("----------");
                    Debug.WriteLine(c.Name);
                    var dic = await bp.RetrievePropertiesAsync(names);
                    //Debug.WriteLine("  accessed = " + dic["System.DataAccessed"]);
                    foreach (var k in dic.Keys)
                    {
                        var date = dic[k];
                        Debug.WriteLine("  " + k + ": " + date);
                    }
                    Debug.WriteLine("  BP:Modified:" + bp.DateModified);
                    Debug.WriteLine("  BP:ItemDate: " + bp.ItemDate);
                    Debug.WriteLine("  Created: " + c.DateCreated);
                }
            }
        }

        private string getHash(string src, string algorithm)
        {
            var prov = HashAlgorithmProvider.OpenAlgorithm(algorithm);
            var maker = prov.CreateHash();
            maker.Append(CryptographicBuffer.ConvertStringToBinary(src, BinaryStringEncoding.Utf8));
            return CryptographicBuffer.EncodeToHexString(maker.GetValueAndReset());
        }

        private string keyFromUri(Uri uri)
        {
            var md5 = getHash(uri.ToString(), "MD5");
            return getHash(uri.ToString()+md5, "SHA1");
        }

        private WvvTempFolder CacheFolder { get { return mFolder; } }

        #endregion

        #region Public API's

        /**
         * URLのキャッシュ(IWvvCache)を取得
         */
        public async Task<IWvvCache> GetCache(Uri uri)
        {
            WvvCache cache;
            string key = keyFromUri(uri);
            if(!mCacheList.TryGetValue(key, out cache))
            {
                try
                {
                    var file = await mFolder.Folder.GetFileAsync(key);
                    cache = new WvvCache(key, uri, file);
                    mCacheList[key] = cache;
                    Debug.WriteLine(string.Format("Use cold cache: {0}", uri.ToString()));
                }
                catch (FileNotFoundException)
                {
                    // target is not found in cache
                    cache = new WvvCache(key, uri, null);
                    mCacheList[key] = cache;
                    Debug.WriteLine(string.Format("Use new cache: {0}", uri.ToString()));
                }
                catch (Exception e2)
                {
                    Debug.WriteLine(e2);
                    return null;
                }
            }
            else
            {
                Debug.WriteLine(string.Format("Use hot cache: {0}", uri.ToString()));
            }

            cache.AddRef();
            return cache;
        }

        #endregion

        #region WvvCache Internal Class
        
        private class WvvCache : IWvvCache
        {
            #region IWvvCache i/f

            /**
             * エラー情報
             */
            public Exception Error { get; private set; }

            /**
             * DLをキャンセルする（たぶん使わない）
             */
            public void Cancel()
            {
                lock (mLock)
                {
                    if (null != mDownloadTask)
                    {
                        mDownloadTask.Cancel();
                    }
                }
            }

            /**
             * キャッシュファイルを取得する。
             * @param callback  結果を返すコールバック
             */
            public void GetFile(WvvDownloadedHandler callback)
            {
                lock (mLock)
                {
                    if (null == mFile)
                    {
                        Downloaded += callback;
                        return;
                    }
                }
                TouchFile(mFile).ContinueWith((t) =>
                {
                    callback(this, mFile);
                });
            }

            /**
             * キャッシュファイルを取得する。（非同期版）
             * @return キャッシュファイル (エラーが発生していれば、null: エラー情報は、Errorプロパティで取得）
             */
            public Task<StorageFile> GetFileAsync()
            {
                var task = new TaskCompletionSource<StorageFile>();
                GetFile((sender, file) =>
                {
                    task.TrySetResult(file);
                });
                return task.Task;
            }

            /**
             * キャッシュを解放する（CacheManagerによって削除可能な状態にする）
             */
            public void Release()
            {
                lock (mLock)
                {
                    mRefCount--;
                }
            }

            #endregion

            #region Privates

            private string mKey;
            private Uri mUri;
            private StorageFile mFile;
            private int mRefCount;
            private Object mLock;
            private IAsyncOperationWithProgress<HttpResponseMessage, HttpProgress> mDownloadTask;
            private event WvvDownloadedHandler Downloaded;

            /**
             * ダウンロードが成功したときの処理
             */
            private void onDownloadCompleted(StorageFile file)
            {
                Error = null;
                lock (mLock)
                {
                    mFile = file;
                    try
                    {
                        Downloaded?.Invoke(this, file);
                        Downloaded = null;
                    }
                    catch (Exception e)
                    {
                        // コールバック中のエラーは無視する
                        Debug.WriteLine(e);
                    }
                }
            }

            /**
             * ダウンロードが失敗したときの処理
             */
            private void onDownloadError(Exception error)
            {
                Error = error;
                lock (mLock)
                {
                    mFile = null;
                    try
                    {
                        Downloaded?.Invoke(this, null);
                        Downloaded = null;
                    }
                    catch (Exception e)
                    {
                        // コールバック中のエラーは無視する
                        Debug.WriteLine(e);
                    }
                }
            }

            #endregion

            #region CacheManager Internals

            /**
             * コンストラクタ
             */
            public WvvCache(string key, Uri targetUri, StorageFile existsFile)
            {
                mLock = new Object();

                mRefCount = 0;
                mKey = key;
                mUri = targetUri;

                if(null!= existsFile)
                {
                    mFile = existsFile;
                }
                else
                {
                    var client = new HttpClient();
                    mDownloadTask = client.GetAsync(mUri, HttpCompletionOption.ResponseContentRead);
                    mDownloadTask.Completed += async (info, status) =>
                    {
                        try
                        {
                            switch (status)
                            {
                                case AsyncStatus.Completed:
                                    var file = (await WvvCacheManager.Instance.CacheFolder.CreateTempFile(mKey)).File;
                                    using (var response = info.GetResults())
                                    using (var input = (await response.Content.ReadAsInputStreamAsync()).AsStreamForRead())
                                    using (var output = await file.OpenStreamForWriteAsync())
                                    {
                                        await input.CopyToAsync(output);
                                        output.Flush();
                                    }
                                    onDownloadCompleted(file);
                                    break;
                                case AsyncStatus.Error:
                                    onDownloadError(info.ErrorCode);
                                    break;
                                case AsyncStatus.Canceled:
                                default:
                                    onDownloadError(new OperationCanceledException("Download operation cancelled."));
                                    break;
                            }
                        }
                        catch (Exception e)
                        {
                            onDownloadError(e);
                        }
                        finally
                        {
                            lock (mLock)
                            {
                                mDownloadTask = null;
                            }
                            client.Dispose();
                        }
                    };
                }
            }

            /**
             * 参照カウンタをインクリメント
             */
            public void AddRef()
            {
                lock(mLock)
                {
                    mRefCount++;
                    if(mRefCount<=0)
                    {
                        mRefCount = 1;
                    }
                }
            }

            /**
             * 参照カウンタを取得
             */
            public int RefCount
            {
                get
                {
                    lock (mLock)
                    {
                        return mRefCount;
                    }
                }
            }
            #endregion
        }
        #endregion
    }




}
