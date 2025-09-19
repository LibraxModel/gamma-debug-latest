using Logger;
using System;
using System.Collections.Generic;
using System.Text;

namespace GammaDebug.Algorithm
{
    public static class GammaServices
    {
        const double MAX_GAMMA_ERR = 4;
        const double MIN_GAMMA_ERR = 1;
        public static double Lv255 { get; private set; }
        public static double Lv0 { get; private set; }

        public static double LvN { get; private set; }

        public static void SetLv255(double lv255)
        {
            Lv255 = lv255;
        }

        public static void SetLv0(double lv0)
        {
            Lv0 = lv0;
        }

        public static void SetLvN(double lvN)
        {
            LvN = lvN;
        }

        public static double GetLv(double gamma, double gray)
        {
            if (Lv255 <= Lv0 || Lv255 == 0)
            {
                throw new Exception("0-255灰阶lv未设置");
            }

            double range = Lv255 - Lv0;
            if (gamma > MAX_GAMMA_ERR || gamma < MIN_GAMMA_ERR)
            {
                throw new Exception("Gamma值设置错误");
            }
            else
            {
                //提供的公式
                double lv = (double)Math.Pow(gray / 255.0, gamma) * range + Lv0;
                return lv;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gray"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static (double, double) GetLvRange(AlgoParam param)
        {
            //TODO:这个方法需要提供重载，区分使用ErrL,ErrH计算的和使用ErrNit计算
            throw new NotImplementedException();
        }

    }
}
