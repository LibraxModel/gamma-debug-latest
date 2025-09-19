using GammaDebug.Algorithm;
using Logger;
using System;
using System.Reflection;

namespace GammaDebug.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Gamma调试算法测试程序 ===");
            Console.WriteLine("保持原有输入输出接口不变，人工模拟上位机输入");
            Console.WriteLine();

            try
            {
                RunManualTest();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 测试过程中发生错误: {ex.Message}");
                Console.WriteLine($"详细错误: {ex}");
            }

            Console.WriteLine();
            Console.WriteLine("测试完成，按任意键退出...");
            Console.ReadKey();
        }

        static void RunManualTest()
        {
            // 1. 初始化 GammaConfigParam
            GammaConfigParam config = new GammaConfigParam
            {
                Mode_Enum = GammaMode_enum.全Gamma,
                MaxRGB = 1023,
                PGammaRoundTimesMax = 500,
                LvBase = 3000,  // 255灰阶基准亮度
                LvHigh = 3060,  // 255灰阶亮度上限
                LvLow = 2940    // 255灰阶亮度下限
            };

            // 2. 初始化 AlgoParam
            AlgoParam param = new AlgoParam
            {
                Gray = 71, // 测试灰阶
                StepX = 4,
                StepY = 4,
                Percent = 0.1,
                // 设置目标范围和容差
                XLow = 0.3, XHigh = 0.316,  // x坐标范围
                YLow = 0.32, YHigh = 0.332,  // y坐标范围
                GammaLow = 2.18, GammaHigh = 2.22,  // Gamma范围
                IsUseLocalLvRange = false,
                
            };

            // 3. 初始化 GammaOpen
            GammaOpen gammaOpen = new GammaOpen();
            GrayInfoCollection grayInfos = new GrayInfoCollection();
            
            // 添加初始灰阶信息
            GrayInfo initialGrayInfo = new GrayInfo(param.Gray, 460,375,383);
            grayInfos.Add(initialGrayInfo);
            
            gammaOpen.Init(grayInfos, config);

            // 直接通过算法计算并初始化 GammaServices 的静态属性
            double lv0 = 0;
            double lv255 = config.LvBase;
            // lvN 不是必须设置的，如果算法需要可以设置为中间值，否则可以省略
            GammaServices.SetLv0(lv0);
            GammaServices.SetLv255(lv255);
            // 如果后续算法需要用到 LvN，可以取消注释下面一行
            // GammaServices.SetLvN((lv0 + lv255) / 2);

            // 4. 开始灰阶调试
            gammaOpen.StartGrayNew(param);

            Console.WriteLine($"\n--- 开始 {param.Gray} 灰阶手动调试 ---");
            Console.WriteLine("目标范围:");
            Console.WriteLine($"  x: [{param.XLow:F4}, {param.XHigh:F4}]");
            Console.WriteLine($"  y: [{param.YLow:F4}, {param.YHigh:F4}]");
            if (param.IsUseLocalLvRange)
            {
                Console.WriteLine($"  Lv: [{param.LocalLvLow:F4}, {param.LocalLvHigh:F4}]");
            }
            else
            {
                // 使用GammaServices.GetLv计算基于gamma的上下界范围
                double lvLow = GammaServices.GetLv(param.GammaLow, param.Gray);
                double lvHigh = GammaServices.GetLv(param.GammaHigh, param.Gray);
                Console.WriteLine($"  Lv: [{lvLow:F4}, {lvHigh:F4}] (基于Gamma=[{param.GammaLow:F1}, {param.GammaHigh:F1}], 灰阶={param.Gray})");
            }
            Console.WriteLine();
            Console.WriteLine("请根据上位机测量结果输入 xyLv 值。");

            IterFdRst result = null;
            int iterationCount = 0;

            while (true)
            {
                iterationCount++;
                Console.WriteLine($"\n--- 第 {iterationCount} 次迭代 ---");

                double lv, x, y;

                // 获取用户输入
                Console.Write("请输入当前测量 Lv 值: ");
                while (!double.TryParse(Console.ReadLine(), out lv))
                {
                    Console.Write("无效输入，请重新输入 Lv 值: ");
                }

                Console.Write("请输入当前测量 x 值: ");
                while (!double.TryParse(Console.ReadLine(), out x))
                {
                    Console.Write("无效输入，请重新输入 x 值: ");
                }

                Console.Write("请输入当前测量 y 值: ");
                while (!double.TryParse(Console.ReadLine(), out y))
                {
                    Console.Write("无效输入，请重新输入 y 值: ");
                }

                Console.WriteLine($"📊 输入测量值: Lv={lv:F4}, x={x:F4}, y={y:F4}");

                // 调用算法获取下一个RGB
                result = gammaOpen.GetNextRGB(lv, x, y);

                Console.WriteLine($"🔄 算法返回状态: {result.RstType}");
                Console.WriteLine($"🎯 推荐新RGB: R={result.GrayInfo.R}, G={result.GrayInfo.G}, B={result.GrayInfo.B}");

                // 检查是否完成
                if (result.RstType == IterRstType_enum.Finished)
                {
                    Console.WriteLine("✅ 算法已完成！目标已达成。");
                    break;
                }
                else if (result.RstType == IterRstType_enum.Error_Iter_OverTimes)
                {
                    Console.WriteLine("⚠️ 达到最大迭代次数，算法结束。");
                    break;
                }
                else if (result.RstType.ToString().StartsWith("Error"))
                {
                    Console.WriteLine($"❌ 算法发生错误: {result.RstType}");
                    break;
                }

                // 显示当前RGB变化
                //if (iterationCount > 1)
                //{
                //    Console.WriteLine($"📈 RGB变化: R={result.GrayInfo.R - 500}, G={result.GrayInfo.G - 500}, B={result.GrayInfo.B - 500}");
                //}
            }

            Console.WriteLine($"\n📋 调试总结:");
            Console.WriteLine($"  总迭代次数: {iterationCount}");
            Console.WriteLine($"  最终状态: {result.RstType}");
            Console.WriteLine($"  最终RGB: R={result.GrayInfo.R}, G={result.GrayInfo.G}, B={result.GrayInfo.B}");
        }

        // 使用反射设置 GammaServices 的静态属性
        static void SetStaticLvValues(double lv0, double lv255, double lvN)
        {
            try
            {
                Type gammaServicesType = typeof(GammaServices);

                MethodInfo setLv0Method = gammaServicesType.GetMethod("SetLv0", BindingFlags.Static | BindingFlags.Public);
                setLv0Method?.Invoke(null, new object[] { lv0 });

                MethodInfo setLv255Method = gammaServicesType.GetMethod("SetLv255", BindingFlags.Static | BindingFlags.Public);
                setLv255Method?.Invoke(null, new object[] { lv255 });

                MethodInfo setLvNMethod = gammaServicesType.GetMethod("SetLvN", BindingFlags.Static | BindingFlags.Public);
                setLvNMethod?.Invoke(null, new object[] { lvN });

                Console.WriteLine($"✅ GammaServices 静态Lv值设置成功: Lv0={lv0}, Lv255={lv255}, LvN={lvN}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 设置 GammaServices 静态Lv值失败: {ex.Message}");
            }
        }
    }
}

