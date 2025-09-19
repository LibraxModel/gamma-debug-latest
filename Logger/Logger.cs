using NLog;
using System;
using System.Collections.Generic;

namespace Logger
{
    public static class Log
    {

        private static NLog.Logger _default;
        static Log()
        {
            _default = LogManager.GetCurrentClassLogger();
        }

        /// <summary>
        /// 自定义日志保存路径，这里设置的是根目录
        /// <para>需要在软件启动时设置好</para>
        /// </summary>
        /// <param name="path"></param>
        public static void SetDirectory(string path)
        {
            GlobalDiagnosticsContext.Set("logDirectory", path);
            //string s = GlobalDiagnosticsContext.Get("logDirectory");
            //var s1 = LogManager.Configuration.Variables["filePath"];
        }

        /// <summary>
        /// 记录普通日志
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="showInMain"></param>
        public static void Trace(string msg)
        {
            string dt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffff");
            _default.Trace("{customDate}{customMsg}", dt, msg);
        }

        /// <summary>
        /// 记录警报日志
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="showInMain"></param>
        public static void Warning(string msg)
        {
            string dt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffff");
            _default.Warn("{customDate}{customMsg}", dt, msg);
        }

        /// <summary>
        /// 记录异常,默认不显示
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="showInMain"></param>
        public static void Error(Exception ex)
        {
            try
            {
                string dts = DateTime.Now.ToString("HH:mm:ss.fff");
                _default.Error(ex);

            }
            finally
            {
            }
        }

        /// <summary>
        /// 纪录异常，可以自己输入信息
        /// </summary>
        /// <param name="errorMsg"></param>
        /// <param name="showInMain"></param>
        public static void Error(string errorMsg)
        {
            string dts = DateTime.Now.ToString("HH:mm:ss.fff");
            _default.Error(errorMsg);
        }

        /// <summary>
        /// 记录普通日志
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="showInMain"></param>
        public static void Fatal(Exception ex)
        {
            string dt = DateTime.Now.ToString("HH:mm:ss-fff");
            _default.Fatal(ex);
        }
    }
}
