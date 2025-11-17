using Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GammaDebug.Algorithm
{
    /// <summary>
    /// 高斯-牛顿法RGB优化迭代器
    /// 基于Python版高斯牛顿法算法重写，保持原有接口不变
    /// </summary>
    internal class GammaIter
    {
        // ========== 原有接口保持不变的字段 ==========
        private int _gray = 0;
        int _iterTimes = 0;
        GammaMode_enum _mode;
        const int MAX_IterTimes = 500;
        AlgoParam _param;
        GammaConfigParam _config;
        DebugType _lastDebugType;
        GrayInfo _lastLvGrayInfo = null;

        // ========== 高斯牛顿法新增字段 ==========
        private double[] _target; // 目标xyLv值 [x, y, Lv]
        private double[] _normalizationFactors; // 归一化因子
        private double[] _weights; // 权重矩阵
        private double _learningRate = 1.0; // 学习率
        private double _maxStepSize; // 最大步长（在InitializeGaussNewtonParameters中根据MaxRGB调整）
        private double _firstStepMaxSize; // 第一步雅可比的最大步长（在InitializeGaussNewtonParameters中根据MaxRGB调整）
        private double _minJacobianDelta; // 最小扰动量（在InitializeGaussNewtonParameters中根据MaxRGB调整）
        private double _maxJacobianDelta; // 最大扰动量（在InitializeGaussNewtonParameters中根据MaxRGB调整）
        private double _deltaAdaptiveFactor = 0.5; // 自适应因子
        private bool _normalizeErrors = true; // 是否使用偏差率归一化
        private double _lowLvThreshold = 0; // 低亮度阈值，降低以避免过度触发
        private double[] _lowLvStep; // 低亮度固定步长（在InitializeGaussNewtonParameters中根据MaxRGB调整）
        
        // 缓存和收敛检测
        private Dictionary<string, (double[], double)> _experimentCache; // 实验缓存
        private bool _earlyConverged = false;
        private GrayInfo _convergedRgb = null;
        private double[] _convergedXylv = null;
        private double[] _convergedError = null;
        
        // 动态范围收缩逻辑
        private double[] _originalTarget; // 原始目标值
        private double[] _originalTolerances; // 原始容差（对称）
        private double[] _originalLowerTolerances; // 原始下容差（非对称）
        private double[] _originalUpperTolerances; // 原始上容差（非对称）
        private double[] _shrunkTarget; // 收缩后的目标值
        private double[] _shrunkLowerTolerances; // 收缩后的下容差（非对称）
        private double[] _shrunkUpperTolerances; // 收缩后的上容差（非对称）
        private double[] _lowerTolerances; // 当前下容差（非对称）
        private double[] _upperTolerances; // 当前上容差（非对称）
        private bool _hasReachedOriginalRange = false; // 是否已达到原始范围
        private GrayInfo _originalRangeRgb = null; // 达到原始范围时的RGB
        private double[] _originalRangeXylv = null; // 达到原始范围时的xyLv
        private int _iterationsSinceOriginalRange = 0; // 达到原始范围后的迭代次数
        private const int MAX_ITERATIONS_AFTER_ORIGINAL = 10; // 达到原始范围后的最大迭代次数
        
        // 历史记录
        private List<double[]> _historyRgb = new List<double[]>();
        private List<double[]> _historyXylv = new List<double[]>();
        private List<double[]> _historyError = new List<double[]>();
        private List<double> _historyObjectiveValue = new List<double>();
        private List<double> _historyStepSize = new List<double>();
        private List<double> _historyConditionNumber = new List<double>();
        
        // 雅可比计算状态管理
        private bool _isComputingJacobian = false; // 是否正在计算雅可比矩阵
        private int _jacobianPerturbationIndex = 0; // 当前扰动索引 (0=R, 1=G, 2=B)
        private double[] _jacobianBaseRgb; // 雅可比计算的基础RGB
        private double[] _jacobianBaseXylv; // 雅可比计算的基础xyLv
        private double[,] _jacobianMatrix; // 计算中的雅可比矩阵
        private double _jacobianDelta; // 当前使用的扰动大小
        private double _actualPerturbationDelta; // 实际的扰动量（包括方向）
        private bool _jacobianMatrixInitialized = false; // 雅可比矩阵是否已初始化
        private bool _isFirstJacobianComputation = true; // 是否为第一次雅可比矩阵计算

        internal GammaIter(AlgoParam param, GammaConfigParam config)
        {
            // ========== 保持原有初始化逻辑 ==========
            _gray = param.Gray;
            _lastDebugType = DebugType.Init;
            _mode = config.Mode_Enum;
            _param = param;
            _config = config;

            // ========== 高斯牛顿法初始化 ==========
            InitializeGaussNewtonParameters();
        }

        /// <summary>
        /// 初始化高斯牛顿法参数
        /// </summary>
        private void InitializeGaussNewtonParameters()
        {
            // 根据MaxRGB计算比例因子
            double scaleFactor = (_config.MaxRGB + 1.0) / 1024.0;
            
            // 根据比例因子调整参数
            _maxStepSize = 40.0 * scaleFactor;
            _firstStepMaxSize = 1000.0 * scaleFactor / 2;
            _minJacobianDelta = 1.0 ;
            _maxJacobianDelta = 40.0 * scaleFactor ;
            _lowLvStep = new double[] { 20.0 * scaleFactor, 20.0 * scaleFactor, 20.0 * scaleFactor };
            
            // 初始化缓存
            _experimentCache = new Dictionary<string, (double[], double)>();
            
            // 不设置默认目标值和容差，等待从GammaBundle中获取
            _target = null; // 未初始化状态
            _normalizationFactors = null; // 未初始化状态
            
            // 初始化权重（等权重）
            _weights = new double[3] { 1.0, 1.0, 1.0 };
            
            // 清空历史记录
            ClearHistory();
            
            Log.Trace($"高斯牛顿法参数初始化完成，MaxRGB={_config.MaxRGB}，比例因子={scaleFactor:F4}");
        }


        /// <summary>
        /// 设置目标值和容差 - 供外部调用（非对称容差）
        /// </summary>
        internal void SetTargetAndTolerances(double[] target, double[] lowerTolerances, double[] upperTolerances, GammaBundle bundle = null)
        {
            if (target == null || lowerTolerances == null || upperTolerances == null)
                throw new ArgumentNullException("目标值和容差不能为null");
            if (target.Length != 3 || lowerTolerances.Length != 3 || upperTolerances.Length != 3)
                throw new ArgumentException("目标值和容差数组长度必须为3");
            
            // 保存原始目标值和容差
            _originalTarget = (double[])target.Clone();
            _originalLowerTolerances = (double[])lowerTolerances.Clone();
            _originalUpperTolerances = (double[])upperTolerances.Clone();
            
            // 计算收缩后的目标值和容差（所有容差都缩小到3/4）
            _shrunkTarget = (double[])target.Clone();
            _shrunkLowerTolerances = new double[3];
            _shrunkUpperTolerances = new double[3];
            for (int i = 0; i < 3; i++)
            {
                _shrunkLowerTolerances[i] = lowerTolerances[i] * 0.75; // 缩小到3/4
                _shrunkUpperTolerances[i] = upperTolerances[i] * 0.75; // 缩小到3/4
            }
            
            // 初始使用收缩后的目标值和容差
            _target = (double[])_shrunkTarget.Clone();
            _lowerTolerances = (double[])_shrunkLowerTolerances.Clone();
            _upperTolerances = (double[])_shrunkUpperTolerances.Clone();
            
            // 重置动态范围收缩状态
            _hasReachedOriginalRange = false;
            _originalRangeRgb = null;
            _originalRangeXylv = null;
            _iterationsSinceOriginalRange = 0;
            
            // 初始化并计算归一化因子
            _normalizationFactors = new double[3];
            for (int i = 0; i < 3; i++)
            {
                _normalizationFactors[i] = Math.Abs(_target[i]) < 1e-6 ? 1.0 : Math.Abs(_target[i]);
            }   
            
            // 输出原始目标范围
            if (bundle != null)
            {
                // 直接从bundle中获取原始目标范围
                Log.Trace($"  原始目标范围: X[{bundle.XRange.Item1:F3},{bundle.XRange.Item2:F3}] Y[{bundle.YRange.Item1:F3},{bundle.YRange.Item2:F3}] Lv[{bundle.LvRange.Item1:F1},{bundle.LvRange.Item2:F1}]");
            }
            else
            {
                // 从非对称容差推导范围
                double[] originalRangeMin = { _originalTarget[0] - _originalLowerTolerances[0], _originalTarget[1] - _originalLowerTolerances[1], _originalTarget[2] - _originalLowerTolerances[2] };
                double[] originalRangeMax = { _originalTarget[0] + _originalUpperTolerances[0], _originalTarget[1] + _originalUpperTolerances[1], _originalTarget[2] + _originalUpperTolerances[2] };
                Log.Trace($"  原始目标范围: X[{originalRangeMin[0]:F3},{originalRangeMax[0]:F3}] Y[{originalRangeMin[1]:F3},{originalRangeMax[1]:F3}] Lv[{originalRangeMin[2]:F1},{originalRangeMax[2]:F1}]");
            }
            
            // 计算并输出缩放后的目标范围
            double[] shrunkRangeMin = { _target[0] - _lowerTolerances[0], _target[1] - _lowerTolerances[1], _target[2] - _lowerTolerances[2] };
            double[] shrunkRangeMax = { _target[0] + _upperTolerances[0], _target[1] + _upperTolerances[1], _target[2] + _upperTolerances[2] };
            Log.Trace($"  缩放目标范围: X[{shrunkRangeMin[0]:F3},{shrunkRangeMax[0]:F3}] Y[{shrunkRangeMin[1]:F3},{shrunkRangeMax[1]:F3}] Lv[{shrunkRangeMin[2]:F1},{shrunkRangeMax[2]:F1}]");
        }

        /// <summary>
        /// 获取当前目标值
        /// </summary>
        internal double[] GetTarget()
        {
            if (_target == null)
                return null;
            return (double[])_target.Clone();
        }

        /// <summary>
        /// 获取当前容差（返回对称容差，取上下容差的平均值）
        /// </summary>
        internal double[] GetTolerances()
        {
            if (_lowerTolerances == null || _upperTolerances == null)
                return null;
            
            double[] symmetricTolerances = new double[3];
            for (int i = 0; i < 3; i++)
            {
                symmetricTolerances[i] = (_lowerTolerances[i] + _upperTolerances[i]) / 2.0;
            }
            return symmetricTolerances;
        }

        /// <summary>
        /// 获取当前非对称容差
        /// </summary>
        internal (double[], double[]) GetAsymmetricTolerances()
        {
            if (_lowerTolerances == null || _upperTolerances == null)
                return (null, null);
            
            return ((double[])_lowerTolerances.Clone(), (double[])_upperTolerances.Clone());
        }

        /// <summary>
        /// 获取当前目标范围
        /// </summary>
        internal (double[], double[]) GetTargetRange()
        {
            if (_target == null || _lowerTolerances == null || _upperTolerances == null)
                return (null, null);
            
            double[] minRange = new double[3];
            double[] maxRange = new double[3];
            for (int i = 0; i < 3; i++)
            {
                minRange[i] = _target[i] - _lowerTolerances[i];
                maxRange[i] = _target[i] + _upperTolerances[i];
            }
            return (minRange, maxRange);
        }

        /// <summary>
        /// 获取原始目标范围
        /// </summary>
        internal (double[], double[]) GetOriginalTargetRange()
        {
            if (_originalTarget == null || _originalLowerTolerances == null || _originalUpperTolerances == null)
                return (null, null);
            
            double[] minRange = new double[3];
            double[] maxRange = new double[3];
            for (int i = 0; i < 3; i++)
            {
                minRange[i] = _originalTarget[i] - _originalLowerTolerances[i];
                maxRange[i] = _originalTarget[i] + _originalUpperTolerances[i];
            }
            return (minRange, maxRange);
        }

        /// <summary>
        /// 从GammaBundle直接设置目标值和容差 
        /// </summary>
        internal void SetTargetFromBundle(GammaBundle bundle)
        {
            // 计算目标值
            double targetX = (bundle.XRange.Item1 + bundle.XRange.Item2) / 2;
            double targetY = (bundle.YRange.Item1 + bundle.YRange.Item2) / 2;
            // 使用bundle.Dest作为目标Lv，这是通过GammaServices.GetLv(_param.GammaBasic, _param.Gray)计算出的确定值
            double targetLv = bundle.Dest;
            
            // 计算非对称容差，确保容差推导的范围完全等于bundle的范围
            double lowerToleranceX = targetX - bundle.XRange.Item1;
            double upperToleranceX = bundle.XRange.Item2 - targetX;
            double lowerToleranceY = targetY - bundle.YRange.Item1;
            double upperToleranceY = bundle.YRange.Item2 - targetY;
            double lowerToleranceLv = targetLv - bundle.LvRange.Item1;
            double upperToleranceLv = bundle.LvRange.Item2 - targetLv;
            
            // 确保容差不为零，但不要改变容差推导的范围
            // 只有当计算出的容差为0或负数时，才设置最小容差
            if (lowerToleranceX <= 0) lowerToleranceX = 0.001;
            if (upperToleranceX <= 0) upperToleranceX = 0.001;
            if (lowerToleranceY <= 0) lowerToleranceY = 0.001;
            if (upperToleranceY <= 0) upperToleranceY = 0.001;
            if (lowerToleranceLv <= 0) lowerToleranceLv = 0.1;
            if (upperToleranceLv <= 0) upperToleranceLv = 0.1;
            
            double[] target = { targetX, targetY, targetLv };
            double[] lowerTolerances = { lowerToleranceX, lowerToleranceY, lowerToleranceLv };
            double[] upperTolerances = { upperToleranceX, upperToleranceY, upperToleranceLv };
            
            SetTargetAndTolerances(target, lowerTolerances, upperTolerances, bundle);
            
            Log.Trace($"设置目标值: X={targetX:F3}, Y={targetY:F3}, Lv={targetLv:F3}");
            Log.Trace($"非对称容差设置: X下={lowerToleranceX:F3}, X上={upperToleranceX:F3}, Y下={lowerToleranceY:F3}, Y上={upperToleranceY:F3}, Lv下={lowerToleranceLv:F3}, Lv上={upperToleranceLv:F3}");
            Log.Trace($"Lv容差验证: 目标Lv={targetLv:F3}, 范围=[{bundle.LvRange.Item1:F3},{bundle.LvRange.Item2:F3}], 下容差={lowerToleranceLv:F3}, 上容差={upperToleranceLv:F3}");
        }

        /// <summary>
        /// 清空历史记录
        /// </summary>
        private void ClearHistory()
        {
            _historyRgb.Clear();
            _historyXylv.Clear();
            _historyError.Clear();
            _historyObjectiveValue.Clear();
            _historyStepSize.Clear();
            _historyConditionNumber.Clear();
            _experimentCache.Clear();
        }

        internal IterFdRst DoIterate(GammaBundle bundle, double lv, double x, double y)
        {
            // ========== 保持原有的边界条件检查 ==========
            if (bundle.Gray == 0 || (bundle.Gray == 255 && _mode == GammaMode_enum.PGamma))
            {
                return new IterFdRst(IterRstType_enum.Finished, bundle.GrayInfo);
            }

            // 检查迭代次数
            if (!CanIter())
            {
                Log.Trace($"达到最大迭代次数 {MAX_IterTimes}，退出");
                return new IterFdRst(IterRstType_enum.Error_Iter_OverTimes, bundle.GrayInfo);
            }

            Log.Trace($"+++{bundle.Gray}灰阶开始第{_iterTimes}次迭代");
            
            // 记录当前测量值
            double[] currentXylv = { x, y, lv };
            double[] currentRgb = { bundle.GrayInfo.R, bundle.GrayInfo.G, bundle.GrayInfo.B };

            // 判断当前状态并处理
            // 更可靠的状态判断：既要有雅可比计算标志，又要有初始化的矩阵
            if (_isComputingJacobian && _jacobianMatrixInitialized)
            {
                // 正在计算雅可比矩阵，处理扰动测量结果
                // 注意：雅可比计算过程中的测量结果也应该记录历史
                _historyRgb.Add((double[])currentRgb.Clone());
                _historyXylv.Add((double[])currentXylv.Clone());
                
                return ProcessJacobianPerturbation(bundle, currentRgb, currentXylv);
                        }
                        else
                        {
                // 正常迭代，记录历史数据并决定下一步
                _historyRgb.Add((double[])currentRgb.Clone());
                _historyXylv.Add((double[])currentXylv.Clone());

                // 检查是否需要使用高斯牛顿法
                if (lv > _lowLvThreshold)
                {
                    Log.Trace($"使用高斯牛顿法进行优化 (当前Lv={lv:F1},x={x:F3},y={y:F3})");
                    return StartGaussNewtonOptimization(bundle, currentRgb, currentXylv);
                    }
                    else
                    {
                    Log.Trace($"使用固定步长法进行优化 (当前Lv={lv:F1},x={x:F3},y={y:F3})");
                    IterFdRst result = ApplyFixedStep(currentRgb, _lowLvStep, currentXylv, bundle);
                    
                    // 更新bundle中的RGB值，确保下一次迭代使用新的RGB值
                    bundle.GrayInfo.R = result.GrayInfo.R;
                    bundle.GrayInfo.G = result.GrayInfo.G;
                    bundle.GrayInfo.B = result.GrayInfo.B;
                    
                    return result;
                }
            }
        }

        /// <summary>
        /// 开始高斯牛顿优化
        /// </summary>
        private IterFdRst StartGaussNewtonOptimization(GammaBundle bundle, double[] currentRgb, double[] currentXylv)
        {
            // 只在第一次或目标值未设置时才设置目标值和容差
            if (!IsTargetAndTolerancesSet())
            {
                SetTargetFromBundle(bundle);
            }
            
            Log.Trace($"第{_iterTimes}次迭代: 当前RGB[{currentRgb[0]:F0},{currentRgb[1]:F0},{currentRgb[2]:F0}] 当前xylv测量值[{currentXylv[0]:F3},{currentXylv[1]:F3},{currentXylv[2]:F3}]");
            
            // 计算误差
            double[] rawError = ComputeError(currentXylv);
            Log.Trace($"  原始误差: [x={rawError[0]:F5}, y={rawError[1]:F5}, Lv={rawError[2]:F3}]");
            
            // 如果首次达到原始范围，记录当前点（在收敛检查之前）
            if (_hasReachedOriginalRange && _originalRangeRgb == null)
            {
                _originalRangeRgb = new GrayInfo(bundle.GrayInfo.Gray, bundle.GrayInfo.R, bundle.GrayInfo.G, bundle.GrayInfo.B);
                _originalRangeXylv = (double[])currentXylv.Clone();
                Log.Trace("🎯 记录达到原始范围时的点作为备选结果");
            }
            
            // 检查收敛（CheckConvergence会处理原始范围检查）
            var (converged, firstTimeReachOriginal) = CheckConvergence(rawError);
            if (converged)
            {
                Log.Trace($" 已收敛！误差满足容差要求");
                
                // 如果是因为达到原始范围后10次迭代仍未在收缩范围内收敛，返回原始范围内的点
                if (_hasReachedOriginalRange && _iterationsSinceOriginalRange > MAX_ITERATIONS_AFTER_ORIGINAL && _originalRangeRgb != null)
                {
                    Log.Trace("🔄 返回达到原始范围时的点");
                    return new IterFdRst(IterRstType_enum.Finished, _originalRangeRgb);
                }
                
                return new IterFdRst(IterRstType_enum.Finished, bundle.GrayInfo);
            }
            
            // 开始计算雅可比矩阵
            return StartJacobianComputation(bundle, currentRgb, currentXylv, rawError);
        }

        /// <summary>
        /// 开始雅可比矩阵计算
        /// </summary>
        private IterFdRst StartJacobianComputation(GammaBundle bundle, double[] currentRgb, double[] currentXylv, double[] rawError)
        {
            // 计算动态扰动大小
            double errorMagnitude = Math.Sqrt(rawError[0] * rawError[0] + rawError[1] * rawError[1] + rawError[2] * rawError[2]);
            double adaptiveDelta = Math.Max(_minJacobianDelta, 
                Math.Min(_deltaAdaptiveFactor * errorMagnitude, _maxJacobianDelta));
            adaptiveDelta = Math.Max(1, Math.Floor(adaptiveDelta));
            
            Log.Trace($"开始雅可比计算: delta={adaptiveDelta:F0} 第一次雅可比迭代={_isFirstJacobianComputation}");
            Console.WriteLine($" 开始雅可比矩阵计算，动态delta: {adaptiveDelta:F3} (第一次雅可比: {_isFirstJacobianComputation})");
            
            // 初始化雅可比计算状态
            _isComputingJacobian = true;
            _jacobianPerturbationIndex = 0;
            _jacobianBaseRgb = (double[])currentRgb.Clone();
            _jacobianBaseXylv = (double[])currentXylv.Clone();
            _jacobianDelta = adaptiveDelta;
            _jacobianMatrix = new double[3, 3];
            _jacobianMatrixInitialized = true; // 标记矩阵已初始化
            
            // 开始第一个扰动（R分量）
            return PerformJacobianPerturbation(bundle, 0);
        }

        /// <summary>
        /// 执行雅可比矩阵扰动
        /// </summary>
        private IterFdRst PerformJacobianPerturbation(GammaBundle bundle, int componentIndex)
        {
            double[] perturbedRgb = (double[])_jacobianBaseRgb.Clone();
            
            // 智能选择扰动方向：根据目标方向决定扰动方向
            double perturbationDirection = DeterminePerturbationDirection(bundle, componentIndex);
            double actualDelta = perturbationDirection * _jacobianDelta;
            perturbedRgb[componentIndex] += actualDelta;
            
            // 保存实际的扰动量（包括方向）
            _actualPerturbationDelta = actualDelta;
            
            // 确保RGB值在有效范围内
            for (int i = 0; i < 3; i++)
            {
                perturbedRgb[i] = Math.Max(0, Math.Min(_config.MaxRGB, perturbedRgb[i]));
            }
            
            // 更新bundle的RGB值
            bundle.GrayInfo.R = (int)Math.Round(perturbedRgb[0]);
            bundle.GrayInfo.G = (int)Math.Round(perturbedRgb[1]);
            bundle.GrayInfo.B = (int)Math.Round(perturbedRgb[2]);
            
            string componentName = componentIndex == 0 ? "R" : (componentIndex == 1 ? "G" : "B");
            string direction = perturbationDirection > 0 ? "+" : "-";
            Console.WriteLine($" 雅可比扰动 {componentName}: [{perturbedRgb[0]}, {perturbedRgb[1]}, {perturbedRgb[2]}] (delta={direction}{_jacobianDelta})");
            
            return new IterFdRst(IterRstType_enum.Continue_Lv, bundle.GrayInfo);
        }
        
        /// <summary>
        /// 根据目标方向确定扰动方向
        /// </summary>
        private double DeterminePerturbationDirection(GammaBundle bundle, int componentIndex)
        {
            // 获取目标Lv值 - 使用与SetTargetFromBundle一致的计算方式
            double targetLv;
            if (_param.IsUseLocalLvRange)
            {
                targetLv = (_param.LocalLvLow + _param.LocalLvHigh) / 2.0;
            }
            else
            {
                targetLv = bundle.Dest; // 使用预计算的目标亮度值
            }
            
            // 获取当前Lv值
            double currentLv = _jacobianBaseXylv[2]; // Lv是第三个分量
            
            // 根据Lv比较决定扰动方向
            // Lv比目标Lv大，扰动方向为负（减少RGB）
            // Lv比目标Lv小，扰动方向为正（增加RGB）
            if (currentLv > targetLv)
            {
                return -1.0; // 负方向，减少RGB值
            }
            else
            {
                return 1.0;  // 正方向，增加RGB值
            }
        }
        
        /// <summary>
        /// 获取目标xyLv值
        /// </summary>
        private double[] GetTargetXylv(GammaBundle bundle)
        {
            // 从AlgoParam获取目标值
            return new double[] 
            {
                (_param.XLow + _param.XHigh) / 2.0,  // 目标X
                (_param.YLow + _param.YHigh) / 2.0,  // 目标Y
                _param.IsUseLocalLvRange ? 
                    (_param.LocalLvLow + _param.LocalLvHigh) / 2.0 :
                    bundle.Dest  // 目标Lv - 使用预计算的目标亮度值
            };
        }

        /// <summary>
        /// 处理雅可比矩阵扰动测量结果
        /// </summary>
        private IterFdRst ProcessJacobianPerturbation(GammaBundle bundle, double[] currentRgb, double[] currentXylv)
        {
            int componentIndex = _jacobianPerturbationIndex;
            string componentName = componentIndex == 0 ? "R" : (componentIndex == 1 ? "G" : "B");
            
            Console.WriteLine($" 雅可比扰动 {componentName} 测量结果: [{currentXylv[0]:f3}, {currentXylv[1]:f3}, {currentXylv[2]:f3}]");
            
            // 重要：在雅可比计算过程中也要检查是否已经收敛
            double[] rawError = ComputeError(currentXylv);
            var (converged, firstTimeReachOriginal) = CheckConvergence(rawError);
            if (converged)
            {
                Console.WriteLine($" 雅可比计算过程中已收敛！误差满足容差要求");
                _isComputingJacobian = false;
                _jacobianMatrixInitialized = false;
                return new IterFdRst(IterRstType_enum.Finished, bundle.GrayInfo);
            }
            
            // 检查扰动后是否有变化
            Console.WriteLine($" 扰动检测：基准值=[{_jacobianBaseXylv[0]:f3}, {_jacobianBaseXylv[1]:f3}, {_jacobianBaseXylv[2]:f3}]");
            Console.WriteLine($" 扰动检测：当前值=[{currentXylv[0]:f3}, {currentXylv[1]:f3}, {currentXylv[2]:f3}]");
            
            bool hasZeroChange = false;
            for (int i = 0; i < 3; i++)
            {
                double diff = Math.Abs(currentXylv[i] - _jacobianBaseXylv[i]);
                Console.WriteLine($" 扰动检测：{GetXylvName(i)} 差值={diff:f6}");
                // 如果有任何一个分量没有变化，就需要增大扰动
                if (diff < 1e-6)
                {
                    hasZeroChange = true;
                    Console.WriteLine($" 扰动检测：{GetXylvName(i)} 分量无变化！");
                }
            }
            
            if (hasZeroChange)
            {
                Console.WriteLine($" 扰动检测：{componentName} 扰动后有分量测量值无变化，尝试更大扰动");
                // 增大扰动并重新尝试当前分量
                _jacobianDelta = Math.Min(_jacobianDelta * 4, _maxJacobianDelta);
                Console.WriteLine($"   增大扰动到: {_jacobianDelta}");
                // 注意：这里不增加 _jacobianPerturbationIndex，保持当前分量
                return PerformJacobianPerturbation(bundle, componentIndex);
            }
            
            // 计算偏导数 - 使用实际的扰动量（包括方向）
            for (int i = 0; i < 3; i++) // xyLv维度
            {
                double derivative = (currentXylv[i] - _jacobianBaseXylv[i]) / _actualPerturbationDelta;
                _jacobianMatrix[i, componentIndex] = derivative;
                Console.WriteLine($"  ∂{GetXylvName(i)}/∂{componentName} = {derivative:F6} (实际扰动={_actualPerturbationDelta:F3})");
            }
            
            // 移动到下一个分量
            _jacobianPerturbationIndex++;
            
            if (_jacobianPerturbationIndex < 3)
            {
                // 继续下一个分量的扰动
                return PerformJacobianPerturbation(bundle, _jacobianPerturbationIndex);
            }
            else
            {
                // 雅可比矩阵计算完成，进行高斯牛顿步
                _isComputingJacobian = false;
                _jacobianMatrixInitialized = false; // 重置矩阵初始化标志
                return CompleteGaussNewtonStep(bundle, currentRgb, currentXylv);
            }
        }

        /// <summary>
        /// 完成高斯牛顿步计算
        /// </summary>
        private IterFdRst CompleteGaussNewtonStep(GammaBundle bundle, double[] currentRgb, double[] currentXylv)
        {
            // 简洁记录雅可比矩阵
            Log.Trace($"雅可比矩阵: [{_jacobianMatrix[0, 0]:F4},{_jacobianMatrix[0, 1]:F4},{_jacobianMatrix[0, 2]:F4}] [{_jacobianMatrix[1, 0]:F4},{_jacobianMatrix[1, 1]:F4},{_jacobianMatrix[1, 2]:F4}] [{_jacobianMatrix[2, 0]:F4},{_jacobianMatrix[2, 1]:F4},{_jacobianMatrix[2, 2]:F4}]");
            
            Console.WriteLine($" 雅可比矩阵计算完成");
            Console.WriteLine($"雅可比矩阵:");
            for (int i = 0; i < 3; i++)
            {
                Console.WriteLine($"  [{_jacobianMatrix[i, 0]:F6}, {_jacobianMatrix[i, 1]:F6}, {_jacobianMatrix[i, 2]:F6}]");
            }
            
            // 计算误差 - 使用扰动前的基准值
            double[] rawError = ComputeError(_jacobianBaseXylv);
            
            // 计算加权雅可比矩阵和加权误差
            double[,] weightedJacobian = ComputeWeightedJacobian(_jacobianMatrix);
            double[] weightedError = ComputeWeightedError(rawError);
            
            // 判断是否为第一次雅可比矩阵计算，应用不同的步长限制
            double currentMaxStepSize = _isFirstJacobianComputation ? _firstStepMaxSize : _maxStepSize;
            
            // 执行高斯牛顿步，传入步长限制参数
            double[] deltaRgb = GaussNewtonStep(weightedJacobian, weightedError, currentMaxStepSize);
            
            // 计算新RGB - 基于扰动前的基准RGB值
            double[] newRgb = new double[3];
            for (int i = 0; i < 3; i++)
            {
                newRgb[i] = _jacobianBaseRgb[i] + deltaRgb[i];
                newRgb[i] = Math.Max(0, Math.Min(_config.MaxRGB, newRgb[i]));
            }
            
            // 更新bundle
            bundle.GrayInfo.R = (int)Math.Round(newRgb[0]);
            bundle.GrayInfo.G = (int)Math.Round(newRgb[1]);
            bundle.GrayInfo.B = (int)Math.Round(newRgb[2]);
            
            Console.WriteLine($" 高斯牛顿步完成: [{newRgb[0]:F1}, {newRgb[1]:F1}, {newRgb[2]:F1}] (基于基准RGB: [{_jacobianBaseRgb[0]:F1}, {_jacobianBaseRgb[1]:F1}, {_jacobianBaseRgb[2]:F1}])");
            
            // 简洁记录RGB变化
            double[] rgbChange = { newRgb[0] - _jacobianBaseRgb[0], newRgb[1] - _jacobianBaseRgb[1], newRgb[2] - _jacobianBaseRgb[2] };
            Log.Trace($"RGB变化: [{_jacobianBaseRgb[0]:F0},{_jacobianBaseRgb[1]:F0},{_jacobianBaseRgb[2]:F0}] -> [{newRgb[0]:F0},{newRgb[1]:F0},{newRgb[2]:F0}] (ΔR={rgbChange[0]}, ΔG={rgbChange[1]}, ΔB={rgbChange[2]})");
            
            // 第一次雅可比计算完成后，将标志设置为false
            if (_isFirstJacobianComputation)
            {
                _isFirstJacobianComputation = false;
                Console.WriteLine($" 第一次雅可比计算完成，后续雅可比计算将使用标准步长限制: {_maxStepSize}");
            }
            
            return new IterFdRst(IterRstType_enum.Continue_Lv, bundle.GrayInfo);
        }

        /// <summary>
        /// 获取xyLv分量名称
        /// </summary>
        private string GetXylvName(int index)
        {
            return index == 0 ? "X" : (index == 1 ? "Y" : "Lv");
        }

        /// <summary>
        /// 高斯牛顿法迭代主逻辑（保留原方法名以兼容）
        /// 统一委派到主状态机 DoIterate，避免重复实现。
        /// </summary>
        private IterFdRst DoGaussNewtonIteration(GammaBundle bundle, double lv, double x, double y)
        {
            return DoIterate(bundle, lv, x, y);
        }


        /// <summary>
        /// 计算原始误差
        /// </summary>
        private double[] ComputeError(double[] currentXylv)
        {
            if (_target == null)
                throw new InvalidOperationException("目标值未设置，请先调用SetTargetAndTolerances或SetTargetFromBundle");
            
            double[] error = new double[3];
            for (int i = 0; i < 3; i++)
            {
                error[i] = _target[i] - currentXylv[i];
            }
            
            // 添加误差计算调试输出
            Console.WriteLine($" 误差计算调试:");
            Console.WriteLine($"   目标值: [X={_target[0]:F4}, Y={_target[1]:F4}, Lv={_target[2]:F4}]");
            Console.WriteLine($"   当前值: [X={currentXylv[0]:F4}, Y={currentXylv[1]:F4}, Lv={currentXylv[2]:F4}]");
            Console.WriteLine($"   误差值: [X={error[0]:F4}, Y={error[1]:F4}, Lv={error[2]:F4}]");
            
            return error;
        }

        /// <summary>
        /// 检查是否收敛
        /// </summary>
        /// <returns>(是否收敛, 是否首次达到原始范围)</returns>
        private (bool converged, bool firstTimeReachOriginal) CheckConvergence(double[] rawError)
        {
            if (_lowerTolerances == null || _upperTolerances == null)
                throw new InvalidOperationException("容差未设置，请先调用SetTargetAndTolerances或SetTargetFromBundle");
            
            // 检查是否在收缩范围内收敛（非对称容差）
            bool inShrunkRange = true;
            for (int i = 0; i < 3; i++)
            {
                if (rawError[i] < -_lowerTolerances[i] || rawError[i] > _upperTolerances[i])
                {
                    inShrunkRange = false;
                    break;
                }
            }
            
            // 检查是否在原始范围内（非对称容差）
            bool inOriginalRange = true;
            for (int i = 0; i < 3; i++)
            {
                if (rawError[i] < -_originalLowerTolerances[i] || rawError[i] > _originalUpperTolerances[i])
                {
                    inOriginalRange = false;
                    break;
                }
            }
            
            // 检查是否首次达到原始范围
            bool firstTimeReachOriginal = false;
            if (inOriginalRange && !_hasReachedOriginalRange)
            {
                _hasReachedOriginalRange = true;
                _iterationsSinceOriginalRange = 0;
                firstTimeReachOriginal = true;
                Log.Trace("🎯 首次达到原始范围！");
            }
            
            // 如果已经达到过原始范围，增加总迭代计数器（不管当前是否在原始范围内）
            if (_hasReachedOriginalRange)
            {
                _iterationsSinceOriginalRange++;
            }
            
            // 如果在收缩范围内收敛，直接返回成功
            if (inShrunkRange)
            {
                Log.Trace("✅ 在收缩范围内收敛！");
                return (true, firstTimeReachOriginal);
            }
            
            // 如果达到原始范围后超过10次迭代仍未在收缩范围内收敛，返回原始范围内的点
            if (_hasReachedOriginalRange && _iterationsSinceOriginalRange > MAX_ITERATIONS_AFTER_ORIGINAL)
            {
                Log.Trace($"⚠️ 达到原始范围后{_iterationsSinceOriginalRange}次迭代仍未在收缩范围内收敛，返回原始范围内的点");
                return (true, firstTimeReachOriginal);
            }
            
            return (false, firstTimeReachOriginal);
        }

        /// <summary>
        /// 记录历史数据
        /// </summary>
        private void RecordHistory(double[] rgb, double[] xylv, double[] error)
        {
            _historyRgb.Add((double[])rgb.Clone());
            _historyXylv.Add((double[])xylv.Clone());
            _historyError.Add((double[])error.Clone());
            
            // 计算目标函数值
            double objectiveValue = ComputeObjectiveFunction(xylv);
            _historyObjectiveValue.Add(objectiveValue);
        }

        /// <summary>
        /// 计算目标函数值
        /// </summary>
        private double ComputeObjectiveFunction(double[] xylv)
        {
            double[] error = ComputeError(xylv);
            double[] weightedError = ComputeWeightedError(error);
            
            double objective = 0;
            for (int i = 0; i < 3; i++)
            {
                objective += weightedError[i] * weightedError[i];
            }
            return 0.5 * objective;
        }

        /// <summary>
        /// 计算加权误差
        /// </summary>
        private double[] ComputeWeightedError(double[] rawError)
        {
            if (_normalizationFactors == null)
                throw new InvalidOperationException("归一化因子未设置，请先调用SetTargetAndTolerances或SetTargetFromBundle");
            
            double[] weightedError = new double[3];
            for (int i = 0; i < 3; i++)
            {
                double normalizedError = _normalizeErrors ? rawError[i] / _normalizationFactors[i] : rawError[i];
                weightedError[i] = _weights[i] * normalizedError;
            }
            return weightedError;
        }

        internal int GetIterCount()
        {
            return _iterTimes;
        }

        /// <summary>
        /// 获取算法历史记录
        /// </summary>
        internal (List<double[]>, List<double[]>, List<double[]>, List<double>) GetHistory()
        {
            return (_historyRgb, _historyXylv, _historyError, _historyObjectiveValue);
        }

        /// <summary>
        /// 获取当前算法状态
        /// </summary>
        internal (double[], double[], bool) GetAlgorithmStatus()
        {
            return (GetTarget(), GetTolerances(), _earlyConverged);
        }

        /// <summary>
        /// 检查目标值和容差是否已设置
        /// </summary>
        internal bool IsTargetAndTolerancesSet()
        {
            return _target != null && _lowerTolerances != null && _upperTolerances != null && _normalizationFactors != null;
        }

        /// <summary>
        /// 重置算法状态（用于新的调试任务）
        /// </summary>
        internal void ResetAlgorithm()
        {
            _iterTimes = 0;
            _earlyConverged = false;
            _convergedRgb = null;
            _convergedXylv = null;
            _convergedError = null;
            
            // 重置动态范围收缩状态
            _hasReachedOriginalRange = false;
            _originalRangeRgb = null;
            _originalRangeXylv = null;
            _iterationsSinceOriginalRange = 0;
            
            // 重置雅可比计算状态
            _isFirstJacobianComputation = true;
            
            ClearHistory();
            Log.Trace("算法状态已重置");
        }


        /// <summary>
        /// 应用固定步长
        /// </summary>
        private IterFdRst ApplyFixedStep(double[] currentRgb, double[] step, double[] currentXylv, GammaBundle bundle)
        {
            // 计算目标值（从bundle获取）
            double targetLv = bundle.Dest;
            
            // 根据Lv误差决定步长方向
            double lvError = targetLv - currentXylv[2];
            double stepDirection = Math.Sign(lvError);
            
            // 如果误差很小，使用默认方向
            if (Math.Abs(lvError) < 0.1)
            {
                stepDirection = 1; // 默认增加RGB
            }
            
            // 计算新RGB值，使用智能步长
            double[] newRgb = new double[3];
            for (int i = 0; i < 3; i++)
            {
                // 使用带方向的步长
                double actualStep = stepDirection * step[i];
                newRgb[i] = currentRgb[i] + actualStep;
                newRgb[i] = Math.Max(0, Math.Min(_config.MaxRGB, newRgb[i]));
            }
            
            // 格式化RGB值
            int formatR = FormatRGB(newRgb[0]);
            int formatG = FormatRGB(newRgb[1]);
            int formatB = FormatRGB(newRgb[2]);
            
            Log.Trace($"固定步长计算: 目标Lv={targetLv:F3}, 当前Lv={currentXylv[2]:F3}, 误差={lvError:F3}, 方向={stepDirection}");
            Log.Trace($"步长设置: [{step[0]},{step[1]},{step[2]}]");
            Log.Trace($"实际步长: [{stepDirection * step[0]:F1},{stepDirection * step[1]:F1},{stepDirection * step[2]:F1}]");
            Log.Trace($"计算得RGB：{newRgb[0]:F1},{newRgb[1]:F1},{newRgb[2]:F1}");
            Log.Trace($"格式化得RGB：{formatR},{formatG},{formatB}");
            
            // 计算步长变化
            double[] stepChange = { newRgb[0] - currentRgb[0], newRgb[1] - currentRgb[1], newRgb[2] - currentRgb[2] };
            
            // 输出总结数据
            string logInfo = $"总结数据：[{_iterTimes,4}][{_lastDebugType}=>Lv],得：Step:{stepChange[1]:F1},RGB:[{currentRgb[0]:F0},{currentRgb[1]:F0},{currentRgb[2]:F0}]=>[{formatR},{formatG},{formatB}]";
            Log.Trace(logInfo);
            
            // 更新状态
            _lastLvGrayInfo = new GrayInfo(_gray, formatR, formatG, formatB);
            _lastDebugType = DebugType.Lv;
            
            GrayInfo gi = new GrayInfo(_gray, formatR, formatG, formatB);
            return new IterFdRst(IterRstType_enum.Continue_Lv, gi);
        }









        /// <summary>
        /// 计算加权雅可比矩阵
        /// </summary>
        private double[,] ComputeWeightedJacobian(double[,] jacobian)
        {
            if (_normalizationFactors == null)
                throw new InvalidOperationException("归一化因子未设置，请先调用SetTargetAndTolerances或SetTargetFromBundle");
            
            double[,] weightedJacobian = new double[3, 3];
            
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    double weight = _normalizeErrors ? _weights[i] / _normalizationFactors[i] : _weights[i];
                    weightedJacobian[i, j] = weight * jacobian[i, j];
                }
            }
            
            return weightedJacobian;
        }

        /// <summary>
        /// 高斯牛顿步长计算
        /// </summary>
        private double[] GaussNewtonStep(double[,] weightedJacobian, double[] weightedError, double maxStepSize = -1)
        {
            try
            {
                Console.WriteLine($" 开始高斯牛顿步长计算:");
                Console.WriteLine($"   加权误差: [X={weightedError[0]:F6}, Y={weightedError[1]:F6}, Lv={weightedError[2]:F3}]");
                
                // 计算 J^T * J
                double[,] JTJ = MatrixMultiply(MatrixTranspose(weightedJacobian), weightedJacobian);
                Console.WriteLine($" J^T * J 矩阵:");
                Console.WriteLine($"   JTJ = [");
                for (int i = 0; i < 3; i++)
                {
                    Console.WriteLine($"        [{JTJ[i, 0]:F6}, {JTJ[i, 1]:F6}, {JTJ[i, 2]:F6}]");
                }
                Console.WriteLine($"       ]");
                
                // 计算 J^T * e
                double[] JTe = MatrixVectorMultiply(MatrixTranspose(weightedJacobian), weightedError);
                Console.WriteLine($" J^T * e 向量: [{JTe[0]:F6}, {JTe[1]:F6}, {JTe[2]:F6}]");
                
                // 求解 (J^T * J) * delta = J^T * e
                Console.WriteLine($" 求解线性方程组: (J^T * J) * delta = J^T * e");
                double[] deltaRgb = SolveLinearSystem(JTJ, JTe);
                Console.WriteLine($"   原始解: [ΔR={deltaRgb[0]:F6}, ΔG={deltaRgb[1]:F6}, ΔB={deltaRgb[2]:F6}]");
                
                // 应用学习率
                for (int i = 0; i < 3; i++)
                {
                    deltaRgb[i] *= _learningRate;
                }
                Console.WriteLine($"   应用学习率({_learningRate}): [ΔR={deltaRgb[0]:F6}, ΔG={deltaRgb[1]:F6}, ΔB={deltaRgb[2]:F6}]");
                
                // 步长限制
                double effectiveMaxStepSize = maxStepSize > 0 ? maxStepSize : _maxStepSize;
                double stepNorm = Math.Sqrt(deltaRgb[0] * deltaRgb[0] + deltaRgb[1] * deltaRgb[1] + deltaRgb[2] * deltaRgb[2]);
                Console.WriteLine($"   步长模长: {stepNorm:F6} (最大允许: {effectiveMaxStepSize})");
                if (stepNorm > effectiveMaxStepSize)
                {
                    double scale = effectiveMaxStepSize / stepNorm;
                    for (int i = 0; i < 3; i++)
                    {
                        deltaRgb[i] *= scale;
                    }
                    Console.WriteLine($"   步长限制缩放({scale:F6}): [ΔR={deltaRgb[0]:F6}, ΔG={deltaRgb[1]:F6}, ΔB={deltaRgb[2]:F6}]");
                }
                
                Console.WriteLine($" 最终deltargb: [ΔR={deltaRgb[0]:F6}, ΔG={deltaRgb[1]:F6}, ΔB={deltaRgb[2]:F6}]");
                
                // 简洁记录步长信息
                double finalStepNorm = Math.Sqrt(deltaRgb[0] * deltaRgb[0] + deltaRgb[1] * deltaRgb[1] + deltaRgb[2] * deltaRgb[2]);
                Log.Trace($"步长计算: 模长={stepNorm:F1} 限制={effectiveMaxStepSize:F0} 缩放={stepNorm > effectiveMaxStepSize}");
                
                return deltaRgb;
            }
            catch (Exception ex)
            {
                Log.Error($"高斯牛顿步长计算失败: {ex.Message}");
                // 返回零步长
                return new double[3] { 0, 0, 0 };
            }
        }

        /// <summary>
        /// 矩阵转置
        /// </summary>
        private double[,] MatrixTranspose(double[,] matrix)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            double[,] result = new double[cols, rows];
            
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    result[j, i] = matrix[i, j];
                }
            }
            
            return result;
        }

        /// <summary>
        /// 矩阵乘法
        /// </summary>
        private double[,] MatrixMultiply(double[,] a, double[,] b)
        {
            int rows = a.GetLength(0);
            int cols = b.GetLength(1);
            int common = a.GetLength(1);
            double[,] result = new double[rows, cols];
            
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < common; k++)
                    {
                        sum += a[i, k] * b[k, j];
                    }
                    result[i, j] = sum;
                }
            }
            
            return result;
        }

        /// <summary>
        /// 矩阵向量乘法
        /// </summary>
        private double[] MatrixVectorMultiply(double[,] matrix, double[] vector)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            double[] result = new double[rows];
            
            for (int i = 0; i < rows; i++)
            {
                double sum = 0;
                for (int j = 0; j < cols; j++)
                {
                    sum += matrix[i, j] * vector[j];
                }
                result[i] = sum;
            }
            
            return result;
        }

        /// <summary>
        /// 求解线性方程组（使用高斯消元法）
        /// </summary>
        private double[] SolveLinearSystem(double[,] A, double[] b)
        {
            int n = b.Length;
            double[,] augmented = new double[n, n + 1];
            
            // 构建增广矩阵
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    augmented[i, j] = A[i, j];
                }
                augmented[i, n] = b[i];
            }
            
            // 高斯消元
            for (int i = 0; i < n; i++)
            {
                // 寻找主元
                int maxRow = i;
                for (int k = i + 1; k < n; k++)
                {
                    if (Math.Abs(augmented[k, i]) > Math.Abs(augmented[maxRow, i]))
                        maxRow = k;
                }
                
                // 交换行
                for (int k = i; k <= n; k++)
                {
                    double temp = augmented[maxRow, k];
                    augmented[maxRow, k] = augmented[i, k];
                    augmented[i, k] = temp;
                }
                
                // 消元
                for (int k = i + 1; k < n; k++)
                {
                    double factor = augmented[k, i] / augmented[i, i];
                    for (int j = i; j <= n; j++)
                    {
                        augmented[k, j] -= factor * augmented[i, j];
                    }
                }
            }
            
            // 回代
            double[] x = new double[n];
            for (int i = n - 1; i >= 0; i--)
            {
                x[i] = augmented[i, n];
                for (int j = i + 1; j < n; j++)
                {
                    x[i] -= augmented[i, j] * x[j];
                }
                x[i] /= augmented[i, i];
            }
            
            return x;
        }

        /// <summary>
        /// 保留原有方法以保持接口兼容性，但内部使用高斯牛顿法
        /// </summary>
        IterFdRst CalcNewRGBByLv(GammaBundle bundle, double lv, double x, double y)
        {
            // 直接调用高斯牛顿法迭代
            return DoGaussNewtonIteration(bundle, lv, x, y);
        }


        /// <summary>
        /// 保留原有方法以保持接口兼容性，但内部使用高斯牛顿法
        /// </summary>
        IterFdRst CalcNewRGBByXY(GammaBundle bundle, double x, double y)
        {
            // 对于XY调整，也使用高斯牛顿法
            // 使用bundle.Dest作为目标Lv，这是通过GammaServices.GetLv(_param.GammaBasic, _param.Gray)计算出的确定值
            double currentLv = bundle.Dest;
            return DoGaussNewtonIteration(bundle, currentLv, x, y);
        }

        /// <summary>
        /// 保留原有辅助方法以保持接口兼容性
        /// </summary>
        private int FindMainCause(GammaBundle bundle, double x, double y)
        {
            double midX = (bundle.XRange.Item1 + bundle.XRange.Item2) / 2;
            double midY = (bundle.YRange.Item1 + bundle.YRange.Item2) / 2;

            double dx = x - midX;
            double dy = y - midY;

            return Math.Abs(dx) > Math.Abs(dy) ? 0 : 1;
        }

        int CompareRange(double v, (double, double) range)
        {
            if (v > range.Item2)
            {
                return 1;
            }
            else if (v < range.Item1)
            {
                return -1;
            }
            else
            {
                return 0;
            }
        }

        bool CanIter()
        {
            return ++_iterTimes <= MAX_IterTimes;
        }

        int FormatRGB(double v)
        {
            if (v < 0)
            {
                return 0;
            }
            if (v > _config.MaxRGB)
            {
                return _config.MaxRGB;
            }
            return (int)Math.Round(v);
        }


        /// <summary>
        /// 上一次的调整内容
        /// </summary>
        enum DebugType
        {
            Init,
            Lv,
            XY,
        }
    }
}
