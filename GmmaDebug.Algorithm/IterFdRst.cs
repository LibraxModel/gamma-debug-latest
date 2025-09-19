using System;
using System.Collections.Generic;
using System.Text;

namespace GammaDebug.Algorithm
{
    public class IterFdRst
    {
        /// <summary>
        /// 是否结束当前灰阶
        /// </summary>
        public IterRstType_enum RstType { get; set; }

        public IterFdRst(IterRstType_enum rstType, GrayInfo grayInfo)
        {
            RstType = rstType;
            GrayInfo = grayInfo;
        }

        /// <summary>
        /// 传递的RGB信息
        /// </summary>
        public GrayInfo GrayInfo { get; set; }

    }

    public enum IterRstType_enum
    {
        Finished,
        Finished_P,
        Continue_Lv,
        Continue_XY,
        Error_Iter_OverTimes = 100,
        Error0,
        Error1,
        Error2,
        Error3,
        Error4,
    }
}
