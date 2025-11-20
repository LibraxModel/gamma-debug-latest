using Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;


namespace GammaDebug.Algorithm
{
    internal class GammaCore
    {
        private GammaBundle _bundle;
        private GammaBundle _bundlePre;

        private GammaIter _iter;
        private GrayInfoCollection _grayInfos;
        private GammaConfigParam _configParam;

        /// <summary>
        /// 每一个产品都进行新建
        /// </summary>
        /// <param name="grays"></param>
        /// <param name="configParam"></param>
        internal GammaCore(GrayInfoCollection grays, GammaConfigParam configParam)
        {
            Log.SetDirectory(AppDomain.CurrentDomain.BaseDirectory);
            _bundle = new GammaBundle();
            _bundlePre = new GammaBundle();
            _grayInfos = new GrayInfoCollection(grays.GetAll());
            _configParam = configParam;
        }

        internal bool StartGamma(AlgoParam param)
        {
            Log.Trace($"*--------开始调试{param.Gray}灰阶--------*");
            _bundle.Init(param, _configParam, _grayInfos.GetDataByGray(param.Gray));
            _iter = new GammaIter(param, _configParam);
            return true;
        }

        internal bool StopGamma(AlgoParam param)
        {
            if (param.Gray == _bundle.Gray)
            {
                Log.Trace($"*--------上位机通知退出{param.Gray}灰阶的调试--------*");
                _bundle.Stop();
                return true;
            }
            else
            {
                Log.Error($"--------上位机通知退出时灰阶信息{param.Gray}不匹配--------*");
                return false;
            }
        }

        internal IterFdRst GetNextRGB(double lv, double x, double y)
        {
            if (!_bundle.Initialized)
            {
                throw new InvalidOperationException("当前绑点已经退出调试流程，请重新初始化");
            }
            
            // 保存当前RGB值（用于本次测量的RGB），因为DoIterate会修改bundle.GrayInfo
            int currentR = _bundle.GrayInfo.R;
            int currentG = _bundle.GrayInfo.G;
            int currentB = _bundle.GrayInfo.B;
            
            var r = _iter.DoIterate(_bundle, lv, x, y);
            if (r.RstType == IterRstType_enum.Finished)
            {
                if (_bundle.Gray == 255)
                {
                    GammaServices.SetLv255(lv);
                    GammaServices.SetLvN(lv);
                    Log.Trace($"已设置255灰阶Lv:{lv}");
                }
                else if (_bundle.Gray == 0)
                {
                    GammaServices.SetLv0(lv);
                    Log.Trace($"已设置0灰阶:{lv}");
                }
                else
                {
                    GammaServices.SetLvN(lv);
                    Log.Trace($"已设置上一灰阶亮度:{lv}");
                }
                Log.Trace($"*--------{_bundle.Gray}灰阶调试完成，迭代{_iter.GetIterCount()}次--------*");
            }
            
            // 获取目标xyLv值
            double[] targetXylv = _iter.GetTarget();
            string targetStr;
            if (targetXylv != null && targetXylv.Length == 3)
            {
                targetStr = $"[{targetXylv[0]:F3},{targetXylv[1]:F3},{targetXylv[2]:F1}]";
            }
            else
            {
                targetStr = "[未设置]";
            }
            
            // 使用保存的当前RGB值（用于本次测量的RGB），而不是r.GrayInfo（下一轮的RGB）
            Log.Trace($"本轮RGB[{currentR},{currentG},{currentB}]，当前测量xyLv[{x:F3},{y:F3},{lv:F1}]，目标xyLv{targetStr}\n");

            return r;
            //_iterCount++;
            //if (_iterCount <= 3)
            //{
            //    return new IterFdRst(IterRstType_enum.Continue_Lv, new GrayInfo(_bundle.Gray, 255 - _iterCount, 255 - _iterCount, 255 - _iterCount));
            //}
            //if (_iterCount <= 3)
            //{
            //    return new IterFdRst(IterRstType_enum.Continue_XY, new GrayInfo(_bundle.Gray, 255 - 2 * _iterCount, 255 - 2 * _iterCount, 255 - 2 * _iterCount));

            //}
            //return new IterFdRst(IterRstType_enum.Finished, new GrayInfo(_bundle.Gray, 99, 99, 99));
        }
    }
}
