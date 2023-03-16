https://zhuanlan.zhihu.com/p/165140042

# 一

问题

1. 正向渲染、延迟渲染
2. Unity2018引入了轻量级管线和高清管线，区别是什么，分别可以定制什么阶段
3. Unity2018默认gamma空间，gamma和Linear的区别
4. C# 成员函数的public override前缀
5. "命令缓冲区要求资源以将其命令存储在Unity引擎的本机级别。"

Opaque 不透明

点

- Unity中可以做到通过脚本继承资产类型来实现定制（RenderPipelineAsset）
- C# 的out、ref参数
- RenderQueueRange.opaque覆盖从0到2500，RenderQueueRange.transparent从2501到5000

说明

- 用FilterRenderersSettings而不用FilterRendererSettings，可能因为写教程时没有不带s的，用2018.3.0f2没有不带s的