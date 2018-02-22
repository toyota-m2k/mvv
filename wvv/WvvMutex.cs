using System;
using System.Diagnostics;
using System.Threading;

namespace wvv
{
    /**
     * Mutex のWaitOne/ReleaseMutexを自動化するクラス
     * 使い方
     * WvvMutex mutex = new WvvMutex();
     * ...
     * using(mutex.Lock()) {
     *   // ロック中の処理
     * }
     * 
     * // usingブロックに入るときにLockされ、抜けるときに自動的にUnlockされる。
     */
    public class WvvMutex : IDisposable
    {
        private Mutex mMutex;

        /**
         * コンストラクタ
         */
        public WvvMutex()
        {
            mMutex = new Mutex();
        }

        /**
         * 破棄
         */
        public void Dispose()
        {
            mMutex.Dispose();
        }
        /**
         * ロックする
         */
        public Locker Lock()
        {
            return new Locker(mMutex);
        }
        
        /**
         * UnlockしたLockerを再利用して、もう一度ロックする
         */
        public Locker Lock(Locker reused)
        {
            reused.Lock(mMutex);
            return reused;
        }

        /**
         * IDisposable.Dispose()によってUnlockできるLockerクラス
         * 何回かに分けて Lock/Unlockする場合は、このインスタンスを使いまわすこともできるが、
         * 通常は、usingブロックを使うことを想定しており、その場合は、このインスタンスの存在を意識する必要はない。
         */
        public class Locker : IDisposable
        {
            private Mutex mMutex;

            public Locker()
            {
                mMutex = null;
            }

            public Locker(Mutex mutex)
            {
                Lock(mutex);
            }

            public void Lock(Mutex mutex)
            {
                Debug.Assert(mMutex == null);
                mMutex = mutex;
                mMutex.WaitOne();
            }

            public void Unlock()
            {
                if (null != mMutex)
                {
                    mMutex.ReleaseMutex();
                    mMutex = null;
                }
            }
            public void Dispose()
            {
                Unlock();
            }
        }
    }
}
