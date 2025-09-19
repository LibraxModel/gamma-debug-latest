using Logger;
using System;
using System.Collections.Generic;
using System.Text;

namespace GammaDebug.Algorithm
{
    /// <summary>
    /// 对外接口，负责和Core的和新交互和上位机的交互
    /// </summary>
    public class GammaOpen
    {
        private GammaCore _core;
        public bool Init(GrayInfoCollection grays, GammaConfigParam config)
        {
            Log.Trace($"当前版本{GetVision()}，开始初始化");
            _core = new GammaCore(grays, config);
            Log.Trace($"初始化完成");

            return true;
        }

        public bool StartGray255(AlgoParam param)
        {
            return _core.StartGamma(param);
        }

        public bool StartGray0(AlgoParam param)
        {
            return _core.StartGamma(param);
        }

        public bool StartGrayNew(AlgoParam param)
        {
            return _core.StartGamma(param);
        }

        public bool StopGray(AlgoParam param)
        {
            return _core.StopGamma(param);
        }

        public IterFdRst GetNextRGB(double lv, double x, double y)
        {
            return _core.GetNextRGB(lv, x, y);
        }

        public string GetVision()
        {
            //1.2 优化日志
            //1.2 优化Lv计算的逻辑 减少有幅度调整时的次数

            //修复range计算判断的bug

            //修复小灰阶算法在判定lv和wb的时候<0.1这个参数不合理的问题，存在>0.1但是已经在范围内的问题

            //修复xy迭代补偿会被分割小于1的问题 

            //修复低灰阶直接掉过的问题

            //添加pgamma流程

            //添加pgamma模式下±1反复跳转

            //优化全Gamma算法，记录上次调整的内容，来判断是否需要重新计算部分值

            //优化全Gamma算法，迭代逻辑优化 06301322

            //优化全Gamma算法，Lv的比例计算当从xy切换到lv时使用新的计算逻辑；xy的补偿做了动态优化，去除出现混合调整时xy降为1然后变化比较弱的问题

            //优化全Gamma算法，开放高低灰阶分界线给上位机，开放停止调试流程，xy步长动态优化功能取消

            //优化全Gamma算法，修复停止调试功能的bug 修改色坐标调整时xy步长调整逻辑

            //优化全Gamma算法，添加了XY调整时如果XY的range设置不统一可能会出现选中一个XY但dir为0的死循环的问题。

            //优化全Gamma算法，在XY接近并切换到LV时会充值XY的步长
            //                 优化调整LV时rgb的关系，现在为rg改变为原本的一半，b不变
            return "V2.7.9";
        }

    }
}
