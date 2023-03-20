using UnityEngine;
using UnityEngine.Rendering;
// Experimental:实验性namespace
using UnityEngine.Experimental.Rendering;
using Conditional = System.Diagnostics.ConditionalAttribute;

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

    // Render外申请内存，减少内存分配
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
            // Clear后执行，分析器中显示不嵌套Clear（原因？）
            cameraBuffer.BeginSample("Render Camera");

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

#if UNITY_EDITOR
            if (camera.cameraType == CameraType.SceneView)
            {
                ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
            }
#endif

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

        DrawDefaultPipeline(renderContext, camera);

        cameraBuffer.EndSample("Render Camera");
        // Execute结束采样的指令
        renderContext.ExecuteCommandBuffer(cameraBuffer);
        cameraBuffer.Clear();

        renderContext.Submit();
    }

    Material errorMaterial;

    // 使用错误着色器来显示其他对象
    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    void DrawDefaultPipeline(ScriptableRenderContext renderContext, Camera camera)
    {
        if (errorMaterial == null)
        {
            Shader errorShader = Shader.Find("Hidden/InternalErrorShader");
            // HideAndDontSave 不会显示在项目窗口中，且不会与所有其他资产一起保存
            errorMaterial = new Material(errorShader) { hideFlags = HideFlags.HideAndDontSave };
        }

        var drawSettings = new DrawRendererSettings(camera, new ShaderPassName("ForwardBase")); // 默认管线
        drawSettings.SetShaderPassName(1, new ShaderPassName("PrepassBase"));
        drawSettings.SetShaderPassName(2, new ShaderPassName("Always"));
        drawSettings.SetShaderPassName(3, new ShaderPassName("Vertex"));
        drawSettings.SetShaderPassName(4, new ShaderPassName("VertexLMRGBM"));
        drawSettings.SetShaderPassName(5, new ShaderPassName("VertexLM"));
        drawSettings.SetOverrideMaterial(errorMaterial, 0);
        var filterSettings = new FilterRenderersSettings(true);
        renderContext.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
    }
}
