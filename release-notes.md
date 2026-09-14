拾词的第一个公开试用版，欢迎大家试用并提出意见，我会根据反馈逐步修正和完善。

## 下载与运行

下载本页附件 **Shici-v0.1.0-win-x64.zip**，完整解压后运行 `Shici.exe`。GitHub 自动生成的 Source code ZIP 是源码，不能直接运行。

需要 Windows x64 和 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)。发布包已包含本地词典，不需要配置翻译 API Key。没有安装器、没有开机自启，数据保存在程序旁的 `data` 文件夹。

## 本版功能

- 选中文字后显示「中文 / English」按钮，中英互译。
- 本地英文词典、音标、双语释义、原形、词形变化和有资料支持的词根词缀。
- 同一个词第 4 次成功查询自动加入生词本。
- 个人笔记、翻卡复习，以及 Markdown / TSV / Anki 导出。
- 托盘运行、快捷键兼容取词与查询缓存。

默认兼容取词快捷键为 Ctrl+Alt+T；若被占用，自动尝试 Ctrl+Alt+Shift+T。实际快捷键显示在主窗口底部。

## 限制与反馈

自动划词依赖目标软件提供可访问文本选区，无法保证兼容所有程序；扫描 PDF 和图片不支持直接取词。词根资料覆盖有限，未收录时会提示。联网翻译受 MyMemory 免费额度与网络影响。

已在开发者 Windows 电脑通过构建、逻辑测试和真实鼠标划词流程验证。欢迎通过 [Issues](https://github.com/walterkken/shici/issues/new/choose) 反馈，我会逐步改进；请不要提交私人词库或查询缓存。

校验压缩包：

```powershell
Get-FileHash .\Shici-v0.1.0-win-x64.zip -Algorithm SHA256
```

结果应与附件 `SHA256SUMS.txt` 一致。源码及第三方资料保留各自许可证，详见仓库说明。
