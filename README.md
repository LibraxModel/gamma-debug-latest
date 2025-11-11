# Gamma Debug Algorithm

显示器Gamma调试算法，基于高斯-牛顿法优化RGB值，实现目标亮度和色坐标的精确调节。

## 📋 项目简介

本项目是一个用于显示器Gamma调试的C#算法库，通过迭代优化RGB寄存器值，使显示器的亮度(Lv)和色坐标(x, y)达到目标规格范围。算法采用高斯-牛顿法进行非线性优化，支持全Gamma和PGamma两种调试模式。

> **生产环境使用**：只需替换 `GmmaDebug.Algorithm/GammaIter.cs` 文件，重新编译生成 dll 即可，接口保持不变。

## ✨ 主要特性

- **高斯-牛顿法优化**：基于Python版算法重写，使用数值优化方法快速收敛
- **动态范围收缩**：智能调整目标范围，提高收敛效率
- **自适应步长**：根据当前状态动态调整RGB步长
- **实验缓存**：避免重复计算，提高性能
- **低亮度优化**：针对低亮度场景的特殊处理逻辑
- **完整日志**：使用NLog记录调试过程

## 🏗️ 项目结构

```
GammaDebug/
├── GammaDebug/              # 测试程序
│   ├── Program.cs           # 主程序入口，包含手动测试示例
│   └── GammaDebug.csproj   # 项目配置文件
├── GmmaDebug.Algorithm/     # 核心算法库
│   ├── GammaOpen.cs        # 对外接口类
│   ├── GammaCore.cs        # 核心逻辑类
│   ├── GammaIter.cs        # 高斯-牛顿法迭代器（核心算法）
│   ├── GammaBundle.cs      # 数据打包类
│   ├── GammaServices.cs    # 辅助服务类
│   ├── AlgoParam.cs        # 算法参数定义
│   ├── GrayInfo.cs         # 灰阶信息类
│   └── IterFdRst.cs        # 迭代结果类
├── Logger/                  # 日志模块
│   ├── Logger.cs           # 日志封装类
│   └── Logger.csproj       # 日志项目配置
├── GammaDebug.sln          # Visual Studio解决方案文件
├── NLog.config             # NLog配置文件
└── NLog.dll               # NLog库文件
```

## 🚀 使用流程

### 生产环境部署

**重要**：在生产环境中，只需要替换 `GammaIter.cs` 文件，然后重新编译生成 dll 即可。

#### 部署步骤

1. **替换算法文件**
   - 将新的 `GmmaDebug.Algorithm/GammaIter.cs` 文件替换到现有项目中
   - 保持其他文件不变，确保接口兼容

2. **编译生成 dll**
   ```bash
   dotnet build GmmaDebug.Algorithm/GammaDebug.Algorithm.csproj --configuration Release
   ```
   或使用 Visual Studio 编译项目

3. **替换 dll 文件**
   - 将生成的 `GammaDebug.Algorithm.dll` 替换到生产环境
   - 无需修改调用代码，接口保持不变

### 测试环境

如需在测试环境验证算法：

1. **克隆仓库**
```bash
git clone https://github.com/LibraxModel/gamma-debug-latest.git
cd gamma-debug-latest
```

2. **编译项目**
```bash
dotnet build GammaDebug.sln
```

3. **运行测试程序**
```bash
dotnet run --project GammaDebug/GammaDebug.csproj
```

### 接口调用

算法接口保持不变，调用方式与之前一致：

```csharp
// 初始化
GammaOpen gammaOpen = new GammaOpen();
gammaOpen.Init(grayInfos, config);

// 开始调试
gammaOpen.StartGrayNew(param);

// 迭代获取RGB值
IterFdRst result = gammaOpen.GetNextRGB(lv, x, y);
```

## 🔧 算法说明

本算法基于**高斯-牛顿法**进行非线性优化，通过迭代调整RGB值使显示器的亮度(Lv)和色坐标(x, y)达到目标范围。

### 核心特性

- 使用数值微分计算雅可比矩阵
- 自适应步长控制，避免过度调整
- 动态范围收缩，提高收敛效率
- 实验缓存机制，避免重复计算
- 低亮度场景特殊优化

## 📊 参数说明

### 主要参数

- **GammaConfigParam**：配置参数（LvBase、LvHigh、LvLow、Mode_Enum、MaxRGB等）
- **AlgoParam**：算法参数（Gray、Gamma范围、色坐标范围、步进等）

详细参数定义请参考 `AlgoParam.cs` 和 `GammaConfigParam.cs` 文件。

### 迭代结果

`IterFdRst.RstType` 返回迭代状态：
- `Finished` - 调试完成
- `Continue_Lv/Continue_XY` - 继续迭代
- `Error_Iter_OverTimes` - 超过最大迭代次数






**注意**：本项目是显示器Gamma调试的专业算法，需要配合测量设备使用。使用前请确保理解算法原理和参数设置。

