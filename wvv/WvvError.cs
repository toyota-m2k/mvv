using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wvv
{
    public class WvvError : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged i/f

        public event PropertyChangedEventHandler PropertyChanged;
        private void notify()
        {
            if (null != PropertyChanged)
            {
                PropertyChanged(this, new PropertyChangedEventArgs("HasError"));
                PropertyChanged(this, new PropertyChangedEventArgs("Message"));
            }
        }

        #endregion

        public bool HasError
        {
            get
            {
                return (mMessage != null || mException != null);
            }
        }


        public string Message
        {
            get
            {
                if (null != mMessage)
                {
                    return mMessage;
                }
                else if (null != mException)
                {
                    return mException.Message;
                }
                else
                {
                    return null;
                }
            }
        }
        
        private Exception mException = null;
        private string mMessage = null;

        public WvvError()
        {
        }

        public void Reset()
        {
            mException = null;
            mMessage = null;
            notify();
        }

        public void SetError(Exception e)
        {
            if (null == mException)
            {
                mException = e;
                notify();
            }
        }

        public void SetError(string message)
        {
            if (null == mMessage)
            {
                mMessage = message;
                notify();
            }
        }

        public void CopyFrom(WvvError e)
        {
            if(!HasError)
            {
                mException = e.mException;
                mMessage = e.mMessage;
                notify();
            }
        }
    }
}
