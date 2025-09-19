using Logger;
using System;
using System.Collections.Generic;
using System.Text;

namespace GammaDebug.Algorithm
{
    /// <summary>
    /// 单独一个绑点设置信息，包含绑点的基础信息
    /// </summary>
    internal class GammaBundle
    {
        // 亮度范围
        public (double, double) LvRange { get; private set; }

        // 色坐标X范围
        public (double, double) XRange { get; private set; }

        // 色坐标Y范围
        public (double, double) YRange { get; private set; }

        // 灰阶RGB值
        public GrayInfo GrayInfo { get; private set; }

        // 灰阶
        public int Gray => _param.Gray;

        // 灰阶Gamma基准值计算的亮度基准值
        public double Dest { get; private set; }

        // 是否需要初始化
        public bool Initialized { get; private set; }

        // 灰阶传入的算法参数
        public AlgoParam _param;

        // 灰阶传入的配置参数
        private GammaConfigParam _config;

        /// <summary>
        /// 初始化绑点信息，设置Lv，LvRgn, XYRng, RGBRng等
        /// </summary>
        /// <param name="param"></param>
        internal void Init(AlgoParam param, GammaConfigParam _configParam, GrayInfo grayInfo)
        {
            _param = param;
            _config = _configParam;
            GrayInfo = grayInfo;
            SetLv();
            SetLvRange();
            if (_config.Mode_Enum == GammaMode_enum.全Gamma)
            {
                SetXYRange();
                SetRGBRange();
            }
            Initialized = true;
            Log.Trace($"初始化{Gray}灰阶完成，RGB初始值：{grayInfo.R},{grayInfo.G},{grayInfo.B}");
        }

        internal void Stop()
        {
            Initialized = false;
        }
        /// <summary>
        /// 设置当前绑点的合理范围
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        bool SetLvRange()
        {
            if (this._param.IsUseLocalLvRange)
            {
                this.LvRange = (this._param.LocalLvLow, this._param.LocalLvHigh);
                Log.Trace(string.Format("设置{0}灰阶LvRng：{1:f3}-{2:f3}", (object)this.Gray, (object)this.LvRange.Item1, (object)this.LvRange.Item2));
            }
            else
            {
                if (_param.Gray == 255)
                {
                    LvRange = (_config.LvLow, _config.LvHigh);
                }
                else if (_param.Gray == 0)
                {
                    // 0灰阶亮度范围默认为【0，1】
                    LvRange = (0, 1);
                }
                else
                {
                    if (_param.GammaLow >= _param.GammaHigh)
                    {
                        throw new Exception("LvRange计算参数设置错误");
                    }
                    double DestL = GammaServices.GetLv(_param.GammaHigh, _param.Gray);
                    double DestH = GammaServices.GetLv(_param.GammaLow, _param.Gray);
                    //DestH = Math.Min(DestH, GammaServices.LvN);
                    this.LvRange = (DestL, DestH);
                    Log.Trace(string.Format("设置{0}GammaBiasH：{1:f3}-GammaBiasL:{2:f3}-DestL:{3:f3}-DestH:{4:f3}", (object)this.Gray, (object)_param.GammaHigh, (object)_param.GammaLow, (object)DestL, (object)DestH));
                }
                Log.Trace(string.Format("设置{0}灰阶LvRng：{1:f3}-{2:f3}", (object)this.Gray, (object)this.LvRange.Item1, (object)this.LvRange.Item2));
            }
            
            return true;
        }
        bool SetXYRange()
        {
            //TODO:rng计算
            XRange = (_param.XLow, _param.XHigh);
            YRange = (_param.YLow, _param.YHigh);
            Log.Trace($"设置XRng：{_param.XLow}-{_param.XHigh}");
            Log.Trace($"设置YRng：{_param.YLow}-{_param.YHigh}");
            return true;
        }

        bool SetRGBRange()
        {
            //TODO:为沟通交互方式
            return true;

        }

        bool SetLv()
        {
            if (Gray == 255)
            {
                Dest = (_config.LvLow + _config.LvHigh) / 2;
            }
            else if (Gray == 0)
            {
                Dest = 0;
            }
            else
            {
                Dest = GammaServices.GetLv(_param.GammaBasic, _param.Gray);
                Log.Trace($"{Gray}灰阶Gamma基准为{_param.GammaBasic}");
            }
            Log.Trace($"调节{Gray}灰阶，设置亮度为{Dest:f3}");
            return true;
        }

    }
}
