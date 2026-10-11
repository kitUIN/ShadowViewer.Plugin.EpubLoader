# ShadowViewer.Plugin.EpubLoader

为本地阅读器导入以图片为内容的 EPUB 漫画。纯文字 EPUB 不在支持范围内。

## 构建与本体加载

依赖 SDK 4.0.1 和本地阅读器 2.0.0。将插件仓库与 `ShadowViewer` 仓库放在同一级目录，初始化本体的子模块，然后在 Visual Studio Developer PowerShell 执行：

```powershell
msbuild ShadowViewer.Plugin.EpubLoader/ShadowViewer.Plugin.EpubLoader.csproj /restore /p:Configuration=Debug /p:Platform=x64 /p:GithubAction=false
```

默认优先引用相邻本体中的 Local/SDK 源码；不存在该路径时使用 NuGet。独立发布构建使用 `/p:Configuration=Release /p:DepsUseNuget=true`。恢复和构建必须传入相同属性。

输出插件包：`ShadowViewer.Plugin.EpubLoader/Packages/ShadowViewer.Plugin.EpubLoader-0.2.0-Debug.sdow`。在本体的插件管理器中安装该文件并重启。不要把整个 bin 目录复制进本体；插件包会排除 SDK、Local 和 EF Core 等由本体提供的程序集。

验证：

```powershell
dotnet run --project tests/Epub.Tests/Epub.Tests.csproj
```
