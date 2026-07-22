# SvgShow - SVG 可视化工具

基于 WPF + WebView2 的 SVG 文件可视化工具，支持矢量级缩放、多文件夹目录树、样式覆盖、几何数据导入等功能。

## 项目特性

- **矢量级渲染**：使用 SVG `viewBox` 缩放，避免放大失真
- **多文件夹目录树**：支持同时管理多个文件夹下的 SVG 文件
- **几何数据导入**：支持从 TXT（SEGMENT / ARC / Point3d）和 JSON 文件导入几何数据并注入到当前视图     ！！！注意！！！ 导入数据后可以通过“重置视图”将所有图形显示在视图中
- **样式覆盖**：可自定义线宽和文字缩放倍率，启用后自动应用 `vector-effect: non-scaling-stroke`
- **缩放时文字稳定**：视图缩放时，文字大小反向调整（视觉上保持稳定）
- **另存为 SVG**：将当前视图（含注入元素）持久化保存
- **系统文件关联**：通过 Windows 注册表将本程序设为 `.svg` 默认打开方式

## 技术栈

| 类型 | 技术 |
|------|------|
| 框架 | WPF (.NET 9.0-windows) |
| 渲染引擎 | WebView2 (Microsoft Edge Chromium) |
| 矢量处理 | SharpVectors.Wpf 1.8.5 |
| 通信 | WebView2 PostWebMessageAsJson / WebMessageReceived |
| 跨语言 | C# + JavaScript (viewer.html) |

## 目录结构

```
SvgShow/
├── SvgShow/                     # 主项目目录
│   ├── App.xaml                # 应用入口 XAML
│   ├── App.xaml.cs             # 应用启动逻辑（处理命令行参数）
│   ├── MainWindow.xaml         # 主窗口 UI（三栏布局）
│   ├── MainWindow.xaml.cs      # 主窗口业务逻辑
│   ├── SvgShow.csproj          # 项目文件
│   ├── AssemblyInfo.cs         # 程序集信息
│   │
│   ├── Models/
│   │   └── TreeItem.cs         # 目录树数据模型（TreeItem/FolderItem/SvgFileItem）
│   │
│   ├── Services/
│   │   ├── DataTool.cs         # 数据解析工具（TXT/JSON → GeometryElement）
│   │   └── SvgService.cs       # SVG 加载服务（基于 SharpVectors）
│   │
│   └── Web/
│       └── viewer.html         # SVG 渲染前端（viewBox 缩放/平移/选中/样式覆盖）
│
├── SvgShow.sln                  # 解决方案文件
├── .gitattributes
├── .gitignore
└── LICENSE.txt
```

## 核心模块说明

### 1. 主窗口（[MainWindow.xaml](file:///c:/Users/PC/Desktop/WORKCAR/SvgShow/SvgShow/MainWindow.xaml) / [MainWindow.xaml.cs](file:///c:/Users/PC/Desktop/WORKCAR/SvgShow/SvgShow/MainWindow.xaml.cs#L1-L686)）

三栏布局：
- **左侧**：SVG 文件目录树（支持多文件夹、滚轮滑动）
- **中间**：WebView2 SVG 渲染区
- **右侧**：属性面板（几何数据 / 坐标信息 / 样式配置 / 当前文件）

工具栏按钮：
- 打开SVG / 新建SVG / 导入TXT / 导入JSON / 另存为SVG
- 重置视图 / 缩放百分比
- 关联SVG（写入注册表）

### 2. 数据解析（[DataTool.cs](file:///c:/Users/PC/Desktop/WORKCAR/SvgShow/SvgShow/Services/DataTool.cs#L1-L251)）

- **`ParseTxtGeometry(string)`**：使用正则表达式解析 TXT 文本
  - `SEGMENT((x1 y1) (x2 y2))` → 黑色线段
  - `ARC(CIRCLE((cx cy)r)startAngle endAngle)` → 深蓝色弧线
  - `(x, y, z)` → 红色圆点
- **`ParseJsonGeometry(string)`**：递归遍历 JSON 节点，提取基础几何单元
  - `GeometryType=10`（含 `StartPoint` / `EndPoint`）→ 黑色线段
  - `GeometryType=14`（含 `Circle.Center` / `Radius` / `StartAngle` / `DeltaAngle` / `IsClockwise`）→ 深蓝色弧线（自动处理顺时针/逆时针）

`GeometryElement` 是统一的数据载体，可在前后端之间序列化传输。

### 3. SVG 渲染（[viewer.html](file:///c:/Users/PC/Desktop/WORKCAR/SvgShow/SvgShow/Web/viewer.html)）

- 使用 `viewBox` 实现矢量级缩放（不依赖 CSS transform）
- 鼠标交互：拖拽平移、滚轮缩放、点击选中
- 选中元素高亮，向上层（C#）上报几何属性
- 样式覆盖：保存原始 `stroke-width` / `font-size` / `vector-effect`，支持恢复

### 4. 目录树（[TreeItem.cs](file:///c:/Users/PC/Desktop/WORKCAR/SvgShow/SvgShow/Models/TreeItem.cs)）

- `TreeItem` 基类（实现 `INotifyPropertyChanged`）
- `FolderItem`：文件夹节点，包含 `Children` 集合
- `SvgFileItem`：SVG 文件节点，包含 `FullPath`
- 使用 `HierarchicalDataTemplate` 渲染，支持滚动条自动接管滚轮

## C# 与 JavaScript 通信协议

C# 发送至 JS（`action` 字段）：
| action | 说明 |
|--------|------|
| `addSvg` | 添加 SVG 图层 |
| `clearAll` | 清空所有图层 |
| `setStyleConfig` | 设置样式覆盖配置 |
| `resetView` | 重置视图 |
| `importJsonToSvg` | 导入 JSON 数据 |
| `importTxtToSvg` | 导入 TXT 数据 |
| `getSvgContent` | 获取当前 SVG 内容（用于另存为） |

JS 发送至 C#（`type` 字段）：
| type | 说明 |
|------|------|
| `mouseMove` | 鼠标移动（含 SVG 坐标和屏幕坐标） |
| `elementSelected` | 选中图形（含类型/边界/中心/宽高） |
| `selectionCleared` | 取消选中 |
| `scaleChanged` | 缩放变化 |
| `svgContent` | SVG 内容（响应 `getSvgContent`） |

## 编译与运行

环境要求：
- Windows 10/11
- .NET 9.0 SDK
- WebView2 Runtime（Windows 11 自带）

```powershell
# 还原依赖
dotnet restore SvgShow.sln

# 编译
dotnet build SvgShow.sln

# 运行
dotnet run --project SvgShow/SvgShow.csproj
```

## 关键约束与约定

- 渲染必须使用 WebView2（不再使用 WPF Canvas）
- SVG 缩放必须使用 `viewBox`（不能用 CSS transform，会像素化）
- 目录树必须支持垂直滚动（用 `PreviewMouseWheel` 接管）
- 目录树支持多个不同路径的文件夹
- 打开文件时只支持单选
- 启用样式覆盖时，文字使用 SVG 用户单位（缩放时反向调整）
- 启用样式覆盖时，所有线条应用 `vector-effect: non-scaling-stroke`
- 文字大小计算：`originalFontSize × fontScale / zoomFactor`
- 导入 JSON 必须有已打开的 SVG 视图，否则拒绝

## 使用示例

### TXT 数据格式

```
SEGMENT((0 0) (100 0))
SEGMENT((100 0) (100 100))
ARC(CIRCLE((0 0)50)0 180)
(0, 0, 0)
```

### JSON 数据格式

```json
{
  "Item1": {
    "GeometryType": 10,
    "StartPoint": { "GeometryType": 1, "X": 0, "Y": 0 },
    "EndPoint": { "GeometryType": 1, "X": 100, "Y": 0 }
  },
  "Item2": [
    {
      "GeometryType": 14,
      "Circle": {
        "Center": { "GeometryType": 1, "X": 50, "Y": 50 },
        "Radius": 30
      },
      "StartAngle": 0,
      "DeltaAngle": 3.14159,
      "IsClockwise": false
    }
  ]
}
```

## 已知限制

- 导入 JSON 时，若数据坐标范围与当前 SVG 的 `viewBox` 差异过大，可能无法在视图中显示（需缩放查看）
- TXT 解析依赖特定格式，格式不一致可能解析失败

## License

详见 [LICENSE.txt](file:///c:/Users/PC/Desktop/WORKCAR/SvgShow/LICENSE.txt)
