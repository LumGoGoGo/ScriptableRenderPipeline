using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Conditional = System.Diagnostics.ConditionalAttribute;

public class MyPipeline : RenderPipeline
{
    CullResults cull;
    Material errorMaterial;

    // 共用的commandBuffer可以减少内存使用
    CommandBuffer cameraBuffer = new CommandBuffer { name = "Render Camera" };
    public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        base.Render(renderContext, cameras);

        //renderContext.DrawSkybox(cameras[0]);

        //renderContext.Submit();

        // 处理全部摄像机
        foreach(var camera in cameras)
        {
            Render(renderContext, camera);
        }
    }

    void Render(ScriptableRenderContext context, Camera camera)
    {
        // 剔除
        ScriptableCullingParameters cullingParameters;
        // 使用默认方式剔除创建参数，检查是否能返回有效参数
        if (!CullResults.GetCullingParameters(camera, out cullingParameters))
        {
            return;
        }

        // 仅在编辑期有效
#if UNITY_EDITOR
        // 仅在渲染场景窗口时
        if (camera.cameraType == CameraType.SceneView)
        {
            // 以当前相机为参数添加UI
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
        }
#endif

        // 对于大对象，尽量使用ref
        CullResults.Cull(ref cullingParameters, context, ref cull);

        // 将摄像机属性应用于context，例如设置矩阵等等
        context.SetupCameraProperties(camera);

        // 清除RT缓冲，并使用相机配置
        CameraClearFlags clearFlags = camera.clearFlags;

        cameraBuffer.ClearRenderTarget(
            (clearFlags & CameraClearFlags.Depth) != 0,
            (clearFlags & CameraClearFlags.Color) != 0,
            Color.clear);

        // 标记，更清晰的层次结构
        cameraBuffer.BeginSample("Render Camera");
        {
            // 不是立即执行的，而是将buffer复制到context的内部缓冲区
            context.ExecuteCommandBuffer(cameraBuffer);
            // clear而不是release
            cameraBuffer.Clear();

            // 绘制设置和过滤器设置
            var drawSettings = new DrawRendererSettings(camera, new ShaderPassName("SRPDefaultUnlit"));
            // 只绘制离摄像机最近的物体，减少overdraw，所以需要先进行排序
            drawSettings.sorting.flags = SortFlags.CommonOpaque;

            var filterSettings = new FilterRenderersSettings(true);

            // 先绘制非透明物体
            filterSettings.renderQueueRange = RenderQueueRange.opaque;

            // 用可见的渲染器进行绘制，通常是MeshRenderer
            context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);

            // 绘制天空盒，此函数只是用来确定是否要绘制天空盒
            // 内部已经执行了天空盒绘制command
            context.DrawSkybox(camera);

            // 半透明排序
            drawSettings.sorting.flags = SortFlags.CommonTransparent;

            // 再绘制半透明物体，2501->5000
            filterSettings.renderQueueRange = RenderQueueRange.transparent;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);

            // 最后画所有错误材质物体
            DrawDefaultPipeline(context, camera);
        }
        cameraBuffer.EndSample("Render Camera");
        context.ExecuteCommandBuffer(cameraBuffer);
        cameraBuffer.Clear();

        context.Submit();
    }

    // 开发版本中调用、执行编辑器编译时调用
    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    void DrawDefaultPipeline(ScriptableRenderContext context, Camera camera)
    {
        if(errorMaterial == null)
        {
            Shader errorShader = Shader.Find("Hidden/InternalErrorShader");
            errorMaterial = new Material(errorShader)
            {
                // 不显示在项目窗口中且不会进行保存
                hideFlags = HideFlags.HideAndDontSave
            };
        }
        var drawSettings = new DrawRendererSettings(camera, new ShaderPassName("ForwardBase"));
        drawSettings.SetShaderPassName(1, new ShaderPassName("PrepassBase"));
        drawSettings.SetShaderPassName(2, new ShaderPassName("Always"));
        drawSettings.SetShaderPassName(3, new ShaderPassName("Vertex"));
        drawSettings.SetShaderPassName(4, new ShaderPassName("VertexLMRGBM"));
        drawSettings.SetShaderPassName(5, new ShaderPassName("VertexLM"));
        // 进行材质覆盖，第二个参数是用于渲染的材质着色器的通道索引，errorMaterial只有一个通道
        drawSettings.SetOverrideMaterial(errorMaterial, 0);

        var filterSettings = new FilterRenderersSettings(true);

        context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
    }
}