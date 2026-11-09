# 决策记录：每标签页静音功能（方案 A）

日期：2026-11-08 ｜ 状态：已选定，待实施

## 需求
给 CefFlashBrowser 添加网页音量控制。项目此前完全没有音量/静音功能。

## 备选方案
- **A：CEF 原生静音**——`IBrowserHost.SetAudioMuted()`，零依赖
- **B：A + 全局音量滑块**——Windows Core Audio `ISimpleAudioVolume`（进程级）
- **C：接管音频路径**——`IAudioHandler` 捕获 PCM + NAudio 自行播放，可做播放指示与每标签页音量

## 关键技术事实
1. CEF 84 无每标签页音量 API，无「只探测播放不动音频」的接口
2. 注册 `IAudioHandler` 即改道：CEF 停止向扬声器播放，音频流交给 handler（设计如此，非 bug）
3. 捕获与 `SetAudioMuted` 互不感知：捕获造成的静音 `SetAudioMuted(false)` 解不开，`IsAudioMuted` 不反映捕获静音（include/cef_audio_handler.h 原文）→ **A/C 不能混做**
4. 捕获仅在页面有声时启动、静默约 1 秒停（kRecentlyAudibleTimeout）

## 决定：选 A
理由：
- 静音是用户最高频的需求场景，CEF 原生 API 零风险即可完整实现
- C 为了 🔊 播放指示和每标签页音量接管整条音频路径，延迟/设备切换/格式协商等维护负担与社区维护的开源项目不匹配
- B 可日后独立叠加（不经过 CEF 音频路径，与 A 无冲突）；C 可日后另立分支攻坚

## 放弃的能力
- 播放中标签页显示 🔊 指示（CEF 84 做不到不接管的探测）
- 每标签页独立音量

## UI 范围（用户已确认）
标签页右键菜单「静音网站」、网页右键菜单「静音/取消静音」、静音后标签页显示 🔇 图标；静音为会话级不持久化；不加工具栏按钮和快捷键；交互参照 Chrome。

## 后续教训（2026-11-09）

1. **嵌入过期事故**：负验证改动源码后只重建了依赖 DLL，exe 内嵌的旧副本（Costura）未更新，
   导致回归全绿而用户手上的 exe 静音无反应。对策：Debug 构建 `DisableFody=true`（exe 与磁盘
   DLL 同源），负验证后必须重建全部下游（见 AGENTS.md）。
   注意：Fody 6.8.2 不支持 FodyWeavers.Debug.xml 按配置覆盖，空配置/ExcludeAssemblies=*
   均无法关闭 Costura，最终采用官方 `DisableFody` MSBuild 属性。
2. **SVG 资源清单坑**：新增 SVG 必须登记进 csproj 的 `<Resource Include>` 列表，否则
   编译进不了 exe、SvgIcon 渲染空白且不报错（回归断言了元素存在也拦不住渲染缺失）。
   已登记 mute.svg / volume.svg，规则记录于 AGENTS.md。
