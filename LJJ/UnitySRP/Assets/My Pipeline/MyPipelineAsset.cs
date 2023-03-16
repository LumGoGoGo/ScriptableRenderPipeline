using UnityEngine;
using UnityEngine.Rendering;
// Experimental:实验性namespace
using UnityEngine.Experimental.Rendering;

// 管线类，负责渲染过程。RenderPipeline继承自IRenderPipeline
public class MyPipeline : RenderPipeline
{
    public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        base.Render(renderContext, cameras);

        //{
        //    // 发送渲染命令
        //    renderContext.DrawSkybox(cameras[0]);

        //    // 对上下文的命令是缓冲的，提交后执行
        //    renderContext.Submit();
        //}

        // 逐相机渲染
        foreach (var camera in cameras)
        {
            Render(renderContext, camera);
        }
    }

    CullResults cull;
    CommandBuffer cameraBuffer = new CommandBuffer { name = "Render Camara" };

    public void Render(ScriptableRenderContext renderContext, Camera camera)
    {
        // 设置摄像机视图矩阵V和投影矩阵P等
        renderContext.SetupCameraProperties(camera);

        {
            // 命令缓冲区
            //var buffer = new CommandBuffer { name = camera.name };

            // 根据camera属性，添加clear命令
            CameraClearFlags clearFlags = camera.clearFlags;
            cameraBuffer.ClearRenderTarget(
                (clearFlags & CameraClearFlags.Depth) != 0,
                (clearFlags & CameraClearFlags.Color) != 0,
                camera.backgroundColor);

            // 复制到上下文内部缓冲区
            renderContext.ExecuteCommandBuffer(cameraBuffer);
            //buffer.Release();
            cameraBuffer.Clear();
        }

        {
            // 获取剔除参数
            ScriptableCullingParameters cullingParameters;
            if (CullResults.GetCullingParameters(camera, out cullingParameters) == false)
            {
                // 创建有效参数失败
                return;
            }

            // 剔除
            CullResults.Cull(ref cullingParameters, renderContext, ref cull);

            // 绘制不透明物体。先绘制不透明的，防止overdraw
            var drawSettings = new DrawRendererSettings(camera, new ShaderPassName("SRPDefaultUnlit"));
            // 不透明从前往后绘制，减少overdraw
            drawSettings.sorting.flags = SortFlags.CommonOpaque;
            var filterSettings = new FilterRenderersSettings(true) { renderQueueRange = RenderQueueRange.opaque };
            renderContext.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);

            // 绘制天空盒
            // 仅用到相机清除标志决定是否绘制天空盒
            renderContext.DrawSkybox(camera);

            // 绘制透明物体。透明着色器通道不写入深度缓冲区，后绘制
            drawSettings.sorting.flags = SortFlags.CommonTransparent;
            // 透明物体从后往前绘制，透明效果需要叠加之前的颜色
            filterSettings.renderQueueRange = RenderQueueRange.transparent;
            renderContext.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
        }

        renderContext.Submit();
    }
}

// 管线资产的描述
[CreateAssetMenu(menuName = "Rendering/My Pipeline")]
public class MyPipelineAsset : RenderPipelineAsset
{
    // 创建管线实例
    protected override IRenderPipeline InternalCreatePipeline()
    {
        return new MyPipeline();
    }
}
