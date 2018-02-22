using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage;
using Windows.Web.Http;

namespace wvv
{
    /**
     * キャッシュマネージャ
     * 
     * Strategy
     * - URLとファイル名が１対１に対応するファイル名（URLのハッシュ）を使用（URL-->ファイル名への１方向のみ）
     * - キャッシュの古い・新しいは、タイムスタンプ(ModifiedDate)で比較
     * - ファイルサイズはチェックせず、ファイル数が一定以下になるよう動作
     * - キャッシュフォルダには、キャッシュファイル以外の情報は持たない（このフォルダに置かれたファイルはすべてキャッシュとみなして、時期が来れば削除される・・・ゴミが残らない）
     * Risk
     * - WvvCacheインスタンスとキャッシュファイルとのライフサイクルが違うので、削除・作成・取得がレースコンディションになる可能性がある。
     * 　--> この問題発生時は、最悪でも、ファイルが取得できず、動画が再生できない（グルグルのまま）、という状況になる程度のはず（-->もう一度表示すれば大丈夫なはず）。
     * - 上記レースコンディションを回避するため、少し大きめにLockして動作させている。デッドロックしないことを祈る。
     *   --> 可能性としては、IWvvCache.GetFile()の完了コールバック内で、WvvCacheManagerのAPIを呼び出すようなケースが考えれるが、それをやらない限り（そのような実装はたぶんやらないだろうから）、たぶん大丈夫。
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
        public static async Task InitializeAsync()
        {
            if (null == sInstance)
            {
                sInstance = new WvvCacheManager();
                await sInstance.InitAsync();
            }
        }
        /**
         * 初期化＋インスタンス取得
         */
        public static async Task<WvvCacheManager> GetInstanceAsync()
        {
            await InitializeAsync();
            return sInstance;
        }

        #endregion

        #region Privates

        /**
         * キャッシュファイルの最大数
         */
#if DEBUG
        private const int MAX_CACHE_COUNT = 5;
#else
        private const int MAX_CACHE_COUNT = 20;
#endif
        private WvvTempFolder mFolder;
        private Dictionary<string, WvvCache> mCacheList;
        private WvvMutex mManagerLock;
        private bool mSweeping = false;

        /**
         * コンストラクタ（シングルトンなので非公開）
         */
        private WvvCacheManager()
        {
        }

        /**
         * 初期化
         * async呼び出しが必要なので、コンストラクタとは別に初期化メソッドを用意する
         */
        private async Task InitAsync()
        {
            if (null == mFolder)
            {
                mFolder = await WvvTempFolder.Create("video-cache");
                mCacheList = new Dictionary<string, WvvCache>();
                mManagerLock = new WvvMutex();
            }
        }

        /**
         * キャッシュファイルのソートに使う構造体
         */
        struct TimedFile
        {
            public StorageFile File;
            public DateTimeOffset Date;
            public TimedFile(StorageFile file, DateTimeOffset date)
            {
                File = file;
                Date = date;
            }
        }

        /**
         * キャッシュファイル数が制限を超えていたら、古いファイルから削除する
         */
        private async void Sweep()
        {
            // 再入防止
            if (mSweeping)
            {
                return;
            }
            mSweeping = true;
            try
            {
                // キャッシュファイル列挙
                var folder = mFolder.Folder;
                var list = await folder.GetFilesAsync();
                if (list.Count < MAX_CACHE_COUNT)
                {
                    return;
                }

                // 更新日時(BasicProperty.ItemData）順（昇順）にソート
                var files = new List<TimedFile>(list.Count);
                foreach (var f in list)
                {
                    var bp = await f.GetBasicPropertiesAsync();
                    files.Add(new TimedFile(f, bp.ItemDate));
                }
                files.Sort((x, y) =>
                {
                    return (x.Date < y.Date) ? 1 : (x.Date > y.Date) ? -1 : 0;
                });

                //foreach(var f in files)
                //{
                //    await DumpFileTimeStamp(f.File);
                //}

                // 古いファイルから削除
                for (int i = MAX_CACHE_COUNT; i < files.Count; i++)
                {
                    using (mManagerLock.Lock())
                    {
                        WvvCache cache;
                        if (mCacheList.TryGetValue(files[i].File.Name, out cache))
                        {
                            if (cache.RefCount > 0)
                            {
                                continue;   // 使っているので削除不可 (breakでもよいと思うけど）
                            }
                            mCacheList.Remove(files[i].File.Name);
                        }
                        await files[i].File.DeleteAsync();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
            finally
            {
                mSweeping = false;
            }
        }

        /**
         * キャッシュをフォルダ毎削除して再作成
         * Swipe用
         */
        public async Task ClearAllAsync()
        {
            try
            {
                var folder = mFolder.Folder;
                mFolder = null;
                await folder.DeleteAsync();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
            finally
            {
                await InitAsync();
            }
        }

        /**
         * ファイルのタイムスタンプをデバッグ出力する
         */
        public static async Task DumpFileTimeStampAsync(StorageFile file)
        {
            string keyAccessed = "System.DateAccessed";
            string keyCreated = "System.DateCreated";
            string keyModified = "System.DateModified";
            var names = new List<string> { keyCreated, keyAccessed, keyModified };

            Debug.WriteLine("----------");
            Debug.WriteLine(file.Name);
            var dic = await file.Properties.RetrievePropertiesAsync(names);
            foreach (var k in dic.Keys)
            {
                var date = dic[k];
                Debug.WriteLine("  " + k + ": " + date);
            }
            var bp = await file.GetBasicPropertiesAsync();
            Debug.WriteLine("  BP:Modified:" + bp.DateModified);
            Debug.WriteLine("  BP:ItemDate: " + bp.ItemDate);
            Debug.WriteLine("  Created: " + file.DateCreated);
        }

        /**
         * ファイルのタイムスタンプを現在時間に更新する
         * - BasicProperty.DateModified
         * - BasicProperty.ItemDate
         * - Properties[System.DateModified]
         * が変更される。
         * 以下のプロパティは変化しない。
         * - StorageFile.DataCreated
         * - Properties[System.DateCreated]
         * - Properties[System.DateAccessed]
         */
        public static async Task TouchFileAsync(StorageFile file)
        {
            // タイムスタンプを更新するための秘技
            // - file.Properties.SavePropertiesAsync()で、ModifiedDate や AccessedDateが変えられない。(ArgumentExceptionがスローされる）
            // - file.OpenAsync(ReadWrite)/Dispose しても、ModifiedDateもAccessedDateも変わらない。
            // - OpenAsyncした後、１バイト読み込んで１バイト書き込む、のような操作をすることも考えたが、気落ち悪い。
            // で、この技・・・０行の文字列を追加する。
            await FileIO.AppendLinesAsync(file, new string[0]);
        }

        /**
         * 文字列からハッシュ値を計算する
         */
        private string getHash(string src, string algorithm)
        {
            var prov = HashAlgorithmProvider.OpenAlgorithm(algorithm);
            var maker = prov.CreateHash();
            maker.Append(CryptographicBuffer.ConvertStringToBinary(src, BinaryStringEncoding.Utf8));
            return CryptographicBuffer.EncodeToHexString(maker.GetValueAndReset());
        }

        /**
         * URLからキー（キャッシュファイルのファイル名）を取得
         * URLのハッシュ値をキャッシュファイル名にして管理している。
         */
        private string keyFromUri(Uri uri)
        {
            var md5 = getHash(uri.ToString(), "MD5");
            return getHash(uri.ToString() + md5, "SHA1");
        }

        private WvvTempFolder CacheFolder { get { return mFolder; } }

        #endregion

        #region Public API's

        /**
         * URLのキャッシュ(IWvvCache)を取得
         */
        public async Task<IWvvCache> GetCacheAsync(Uri uri)
        {
            WvvCache cache;
            using (mManagerLock.Lock())
            {
                string key = keyFromUri(uri);
                if (!mCacheList.TryGetValue(key, out cache))
                {
                    try
                    {
                        var file = await mFolder.Folder.GetFileAsync(key);
                        cache = new WvvCache(key, uri, file);
                        mCacheList[key] = cache;
                        Debug.WriteLine(string.Format("WvvCacheManager.GetCache(): Use cold cache: {0}", uri.ToString()));
                    }
                    catch (FileNotFoundException)
                    {
                        // target is not found in cache
                        cache = new WvvCache(key, uri, null);
                        mCacheList[key] = cache;
                        Debug.WriteLine(string.Format("WvvCacheManager.GetCache(): Use new cache: {0}", uri.ToString()));
                    }
                    catch (Exception e2)
                    {
                        Debug.WriteLine(e2);
                        return null;
                    }
                }
                else
                {
                    Debug.WriteLine(string.Format("WvvCacheManager.GetCache(): Use hot cache: {0}", uri.ToString()));
                }
            }
            cache.AddRef();

            Sweep();
            return cache;
        }

        #endregion

        #region WvvCache Internal Class

        /**
         * キャッシュファイルの内部表現
         */
        private class WvvCache : IWvvCache
        {
            /**
             * コンストラクタ
             */
            public WvvCache(string key, Uri targetUri, StorageFile existsFile)
            {
                mLock = new Object();

                mRefCount = 0;
                mKey = key;
                mUri = targetUri;

                if (null != existsFile)
                {
                    mFile = existsFile;
                }
                else
                {
                    Download();
                }
            }


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
                        if(null == mDownloadTask)
                        {
                            Download();
                        }
                        Debug.WriteLine("WvvCacheManager: GetFile() ... Downloading");
                        Downloaded += callback;
                        return;
                    }
                }
                TouchFileAsync(mFile).ContinueWith((t) =>
                {
                    Debug.WriteLine("WvvCacheManager: GetFile() ... Cache File Available");
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
             * キャッシュを無効化する
             */
            public void Invalidate()
            {
                lock(mLock)
                {
                    mInvalidFile = mFile;
                    mFile = null;
                }
            }

            /**
             * キャッシュを解放する（CacheManagerによって削除可能な状態にする）
             */
            public void Release()
            {
                lock (mLock)
                {
                    mRefCount--;
                    if(mRefCount==0 && mFile==null && mDownloadTask==null && mInvalidFile!=null)
                    {
                        try
                        {
                            var _ = mInvalidFile.DeleteAsync();
                            mInvalidFile = null;
                        }
                        catch(Exception e)
                        {
                            Debug.WriteLine(e);
                        }
                    }
                }
            }

            #endregion

            #region Privates

            private string mKey;
            private Uri mUri;
            private StorageFile mFile;
            private StorageFile mInvalidFile;
            private int mRefCount;
            private object mLock;
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
                    mInvalidFile = null;
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
             * ファイルのダウンロードを開始
             */
            private void Download()
            {
                lock (mLock)
                {
                    if(null!=mDownloadTask)
                    {
                        return;
                    }

                    mFile = null;
                    var client = new HttpClient();
                    mDownloadTask = client.GetAsync(mUri, HttpCompletionOption.ResponseContentRead);
                    mDownloadTask.Completed += async (info, status) =>
                    {
                        lock (mLock)
                        {
                            mDownloadTask = null;
                        }
                        try
                        {
                            switch (status)
                            {
                                case AsyncStatus.Completed:
                                    using (var response = info.GetResults())
                                    using (var input = (await response.Content.ReadAsInputStreamAsync()).AsStreamForRead())
                                    using (var output = await WindowsRuntimeStorageExtensions.OpenStreamForWriteAsync(WvvCacheManager.Instance.CacheFolder.Folder, mKey, CreationCollisionOption.ReplaceExisting))
                                    {
                                        await input.CopyToAsync(output);
                                        output.Flush();
                                    }
                                    onDownloadCompleted(await WvvCacheManager.Instance.CacheFolder.Folder.GetFileAsync(mKey));
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
                            client.Dispose();
                        }
                    };
                }
            }
            #endregion

            #region Reference Counter

            /**
             * 参照カウンタをインクリメント
             */
            public void AddRef()
            {
                lock (mLock)
                {
                    mRefCount++;
                    if (mRefCount <= 0)
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
