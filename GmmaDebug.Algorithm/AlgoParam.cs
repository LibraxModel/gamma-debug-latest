using System;
using System.Collections.Generic;
using System.Text;

namespace GammaDebug.Algorithm
{
    public class GammaConfigParam
    {
        // 255灰阶的Lv基础亮度
        public double LvBase { get; set; }

        // 255灰阶的亮度下限
        public double LvHigh { get; set; }

        // 255灰阶的亮度上限
        public double LvLow { get; set; }

        // Gamma模式
        public GammaMode_enum Mode_Enum { get; set; } = GammaMode_enum.全Gamma;

        // 当前可调节的RGB最大值
        public int MaxRGB { get; set; } = 1023;

        // Pgamma模式下±1来回迭代最大次数
        public int PGammaRoundTimesMax = 5;

    }

    public class AlgoParam
    {
        // 灰阶
        public int Gray { get; set; }

        // Gamma基准值
        public double GammaBasic { get; set; } = 2.2;

        // Gamma下限
        public double GammaLow { get; set; }

        // Gamma上限
        public double GammaHigh { get; set; }

        // 色坐标X下限
        public double XLow { get; set; }

        // 色坐标X上限
        public double XHigh { get; set; }

        // 色坐标Y下限
        public double YLow { get; set; }

        // 色坐标Y上限
        public double YHigh { get; set; }

        // 色坐标X最大迭代步进
        public int StepX { get; set; } = 4;

        // 色坐标Y最大迭代步进
        public int StepY { get; set; } = 4;

        // 是否启用本地亮度
        public bool IsUseLocalLvRange { get; set; } = false;

        // 启用本地亮度时，亮度下限
        public double LocalLvLow { get; set; } = 0.0;

        // 启用本地亮度时，亮度上限
        public double LocalLvHigh { get; set; } = 1000.0;

        // 是否跳过XY调整
        public bool SkipXY { get; set; }

        // 计算亮度是否接近中间值的比例范围
        public double Percent { get; set; } = 1.0;

        // 低于以下灰阶，RGB寄存器值在算法基础上增加不同比例的值
        public int DeltaMaxGray { get; set; } = 255;

        // R寄存器增加比例
        public double DeltaR { get; set; } = 0;

        // G寄存器增加比例
        public double DeltaG { get; set; } = 0;

        // B寄存器增加比例
        public double DeltaB { get; set; } = 0;

    }

    public enum GammaMode_enum
    {
        全Gamma,
        PGamma,
    }
}
