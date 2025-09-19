using System;
using System.Collections.Generic;
using System.Text;

namespace GammaDebug.Algorithm
{
    /// <summary>
    /// 各灰阶信息，灰阶值，rgb
    /// </summary>
    public class GrayInfo
    {
        /// <summary>
        /// 灰阶信息
        /// </summary>
        public int Gray { get; set; } = 255;
        /// <summary>
        /// R通道信息
        /// </summary>
        public int R { get; set; }
        /// <summary>
        /// G通道信息
        /// </summary>
        public int G { get; set; }
        /// <summary>
        /// B通道信息
        /// </summary>
        public int B { get; set; }

        public GrayInfo(int gray, int r, int g, int b)
        {
            Gray = gray;
            R = r;
            G = g;
            B = b;
        }
    }

    public class GrayInfoCollection
    {
        private List<GrayInfo> _grayInfos;

        public GrayInfoCollection()
        {
            _grayInfos = new List<GrayInfo>();
        }

        public int Count => _grayInfos.Count;

        public GrayInfoCollection(List<GrayInfo> grayInfos)
        {
            _grayInfos = new List<GrayInfo>(grayInfos);
        }

        public GrayInfo GetDataByGray(int gray)
        {
            return _grayInfos.Find(gi => gi.Gray == gray);
        }

        public List<GrayInfo> GetAll()
        {
            return _grayInfos;
        }

        public void Add(GrayInfo grayInfo)
        {
            if (_grayInfos.Find(gi => gi.Gray == grayInfo.Gray) != null)
            {
                throw new Exception("重复赋值");
            }

            _grayInfos.Add(grayInfo);
        }

        public void Clear()
        {
            _grayInfos.Clear();
        }
    }
}
