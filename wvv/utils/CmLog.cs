using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wvv.utils
{
    static class CmLog
    {
        // @todo WinRT では System.Diagnostics.Trace などが存在しない
        //  TraceSwitch もないので出力制御は自前でなんとかするしかなさそう

        // ↓ suzaku 追加

        public enum Level
        {
            DEBUG,
            INFO,
            WARN,
            ERROR,
        }

        public delegate void TraceEvent(Level level, string message);

        public static event TraceEvent error_trace;
        public static event TraceEvent warn_trace;
        public static event TraceEvent info_trace;
        public static event TraceEvent debug_trace;

        // ↑ suzaku 追加
        /// <summary>
        /// 例外が発生したことをログに書き出します。
        /// <param name="ex">発生した例外</param>
        /// </summary>
        public static void logException(Exception ex)
        {
            logException(ex, "");
        }

        /// <summary>
        /// 例外が発生したことをログに書き出します。
        /// <param name="ex">発生した例外</param>
        /// <param name="optMessage">メッセージ</param>
        /// </summary>
        public static void logException(Exception ex, string optMessage)
        {
            string logMessage = optMessage + "\nException detected:\n" + ex.ToString();
            // @todo javascript では sourceURL, line を表示していたが、C# では何がいいか
            //if (ex.sourceURL != null || ex.line != null) {
            //    logMessage += " (" + ex.sourceURL + ":" + ex.line + ")"; 
            //}
            Debug.WriteLine("[error] " + logMessage);

            // var _ = ErrorInfo.Initialize().WriteErrorAsync(ex, optMessage);
        }

        public static void logException(Exception ex, string format, params object[] args)
        {
            logException(ex, String.Format(format, args));
        }

        /// <summary>
        /// 種別「エラー」のログ出力を行います。
        /// </summary>
        /// <param name="message">メッセージ</param>
        public static void error(string message)
        {
            //#if ENABLE_LOGGING
            Debug.WriteLine("[error] " + message);
            if (error_trace != null)
                error_trace(Level.ERROR, message);
            //#endif
        }
        /**
         * 書式指定版（もっと早く作っておけばよかった。。。）
         */
        public static void error(string format, params object[] args)
        {
            error(string.Format(format, args));
        }

        public static void error(Exception e, string message)
        {
            logException(e, message);
        }

        public static void error(Exception e, string format, params object[] args)
        {
            logException(e, format, args);
        }

        /// <summary>
        /// 種別「警告」のログ出力を行います。
        /// </summary>
        /// <param name="message">メッセージ</param>
        public static void warn(string message)
        {
            //#if ENABLE_LOGGING
            Debug.WriteLine("[warning] " + message);
            if (warn_trace != null)
                warn_trace(Level.WARN, message);
            //#endif
        }
        /**
         * 書式指定版（もっと早く作っておけばよかった。。。）
         */
        public static void warn(string format, params object[] args)
        {
            warn(string.Format(format, args));
        }
        /// <summary>
        /// 種別「情報」のログ出力を行います。
        /// </summary>
        /// <param name="message">メッセージ</param>
        public static void info(string message)
        {
            //#if ENABLE_LOGGING
            Debug.WriteLine("[info] " + message);
            if (info_trace != null)
                info_trace(Level.INFO, message);
            //#endif
        }
        /**
         * 書式指定版（もっと早く作っておけばよかった。。。）
         */
        public static void info(string format, params object[] args)
        {
            info(string.Format(format, args));
        }

        /// <summary>
        /// 種別「デバッグ」のログ出力を行います。
        /// </summary>
        /// <param name="message">メッセージ</param>
        [Conditional("DEBUG")]
        public static void debug(string message)
        {
            //#if ENABLE_LOGGING
            Debug.WriteLine("[debug] " + message);
            if (debug_trace != null)
                debug_trace(Level.DEBUG, message);
            //#endif
        }

        /**
         * 書式指定版（もっと早く作っておけばよかった。。。）
         */
        [Conditional("DEBUG")]
        public static void debug(string format, params object[] args)
        {
            debug(string.Format(format, args));
        }

        /**
         * 現在のフォーカスビューを出力
         */
        //[Conditional("DEBUG")]
        //public static void dumpFocusView()
        //{
        //    var focused = FocusManager.GetFocusedElement();
        //    if (null == focused)
        //    {
        //        debug("Focued on: no elements.");
        //        return;
        //    }

        //    string name = (focused as FrameworkElement)?.Name ?? "";
        //    debug("Focused on: {0} ({1})", focused.ToString(), name);
        //    if (focused is DependencyObject)
        //    {
        //        dumpViewTree((DependencyObject)focused);
        //    }
        //}

        /**
         * 指定されたビューの階層を出力
         */
        //[Conditional("DEBUG")]
        //public static void dumpViewTree(DependencyObject view)
        //{
        //    if (null == view)
        //    {
        //        return;
        //    }
        //    internalDumpViewTree(view);
        //}

        // 戻り型がvoidでないので、[Conditional]にできないからprivateにして隠した。
        //private static int internalDumpViewTree(DependencyObject view)
        //{
        //    int space = 0;
        //    var parent = VisualTreeHelper.GetParent(view);
        //    if (null != parent)
        //    {
        //        space = internalDumpViewTree(parent);
        //    }
        //    var sb = new StringBuilder();
        //    for (int i = 0; i < space; i++)
        //    {
        //        sb.Append(" ");
        //    }
        //    sb.Append("- ");
        //    sb.Append(view.ToString());
        //    if (view is FrameworkElement)
        //    {
        //        string name = ((FrameworkElement)view).Name;
        //        if (null != name)
        //        {
        //            sb.AppendFormat(" ({0})", name);
        //        }
        //    }
        //    debug(sb.ToString());
        //    return space + 2;
        //}

        public static T GetTarget<T>(this WeakReference<T> wr)
            where T : class
        {
            T o;
            return (wr.TryGetTarget(out o)) ? o : null;
        }

    }
}
