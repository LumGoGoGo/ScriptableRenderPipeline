using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Conditional = System.Diagnostics.ConditionalAttribute;

public class MyPipeline : RenderPipeline
{
    const int maxVisibleLights = 4;

    static int visibleLightColorId = Shader.PropertyToID("_VisibleLightColors");
    static int visibleLightDirectionId = Shader.PropertyToID("_VisibleLightDirectionsOrPositions");
    static int visibleLightAttenuationId = Shader.PropertyToID("_VisibleLightAttenuations");
    static int visibleLightSpotDirectionsId = Shader.PropertyToID("_VisibleLightSpotDirections");

    Vector4[] visibleLightColors = new Vector4[maxVisibleLights];
    Vector4[] visibleLightDirectioansOrPositions = new Vector4[maxVisibleLights];
    Vector4[] visibleLightAttenuations = new Vector4[maxVisibleLights];
    Vector4[] visibleLightSpotDirections = new Vector4[maxVisibleLights];

    CullResults cull;
    Material errorMaterial;

    // 共用的commandBuffer可以减少内存使用
    CommandBuffer cameraBuffer = new CommandBuffer { name = "Render Camera" };

    // unity的动态合批对mesh顶点数量有限制:300
    DrawRendererFlags drawFlags;

    public MyPipeline(bool dynamicBatching, bool instancing)
    {
        // 保证灯光强度是线性的，默认是gamma空间的
        GraphicsSettings.lightsUseLinearIntensity = true;
        if(dynamicBatching)
        {
            drawFlags = DrawRendererFlags.EnableDynamicBatching;
        }
        if(instancing)
        {
            drawFlags |= DrawRendererFlags.EnableInstancing;
        }
    }

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

        ConfigureLights();

        // 标记，更清晰的层次结构
        cameraBuffer.BeginSample("Render Camera");
        {
            // 复制到GPU上
            cameraBuffer.SetGlobalVectorArray(visibleLightColorId, visibleLightColors);
            cameraBuffer.SetGlobalVectorArray(visibleLightDirectionId, visibleLightDirectioansOrPositions);
            cameraBuffer.SetGlobalVectorArray(visibleLightAttenuationId, visibleLightAttenuations);
            cameraBuffer.SetGlobalVectorArray(visibleLightSpotDirectionsId, visibleLightSpotDirections);

            // 不是立即执行的，而是将buffer复制到context的内部缓冲区
            context.ExecuteCommandBuffer(cameraBuffer);
            // clear而不是release
            cameraBuffer.Clear();

            // 绘制设置和过滤器设置
            var drawSettings = new DrawRendererSettings(camera, new ShaderPassName("SRPDefaultUnlit"));

            // 只绘制离摄像机最近的物体，减少overdraw，所以需要先进行排序
            drawSettings.sorting.flags = SortFlags.CommonOpaque;

            // 动态合批
            drawSettings.flags = drawFlags;

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

    void ConfigureLights()
    {
        int i = 0;
        // 裁剪的过程，也可获得可见光
        for (; i < cull.visibleLights.Count; i++)
        {
            if(i == maxVisibleLights)
            {
                break;
            }

            VisibleLight light = cull.visibleLights[i];
            visibleLightColors[i] = light.finalColor;   // 已经乘过强度且处理过颜色空间的最终颜色
            Vector4 attenuation = Vector4.zero;
            attenuation.w = 1f;

            if (light.lightType == LightType.Directional)
            {
                Vector4 v = light.localToWorld.GetColumn(2);    // z方向向量
                v.x = -v.x;
                v.y = -v.y;
                v.z = -v.z;
                visibleLightDirectioansOrPositions[i] = v;
            }
            else
            {
                visibleLightDirectioansOrPositions[i] = light.localToWorld.GetColumn(3);
                attenuation.x = 1f / Mathf.Max(light.range * light.range, 0.00001f);

                if(light.lightType == LightType.Spot)
                {
                    Vector4 v = light.localToWorld.GetColumn(2);    // z方向向量
                    v.x = -v.x;
                    v.y = -v.y;
                    v.z = -v.z;
                    visibleLightSpotDirections[i] = v;

                    float outerRad = Mathf.Deg2Rad * 0.5f * light.spotAngle;
                    float outerCos = Mathf.Cos(outerRad);
                    float outerTan = Mathf.Tan(outerRad);
                    float innerCos =
                        Mathf.Cos(Mathf.Atan((46f / 64f) * outerTan));
                    float angleRange = Mathf.Max(innerCos - outerCos, 0.001f);
                    attenuation.z = 1f / angleRange;
                    attenuation.w = -outerCos * attenuation.z;
                }
            }

            visibleLightAttenuations[i] = attenuation;
        }

        // 当可见光的数量减少时，会发生另一件事。它们会保持可见状态，因为我们没有重置其数据。可以通过在可见光结束后继续循环遍历数组，清除所有未使用的光的颜色来解决此问题
        for (; i < maxVisibleLights; i++)
        {
            visibleLightColors[i] = Color.clear;
        }
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

        // 这些都是unity内置光照模型
        var drawSettings = new DrawRendererSettings(camera, new ShaderPassName("ForwardBase"));     // 前向
        drawSettings.SetShaderPassName(1, new ShaderPassName("PrepassBase"));   // 延迟
        drawSettings.SetShaderPassName(2, new ShaderPassName("Always"));        // 无论使用哪种路径，该Pass总是会被渲染，但不会计算任何光照
        drawSettings.SetShaderPassName(3, new ShaderPassName("Vertex"));        // 顶点
        drawSettings.SetShaderPassName(4, new ShaderPassName("VertexLMRGBM"));  // RGBM编码，PC和主机
        drawSettings.SetShaderPassName(5, new ShaderPassName("VertexLM"));      // LDR编码，移动端
        // 进行材质覆盖，第二个参数是用于渲染的材质着色器的通道索引，errorMaterial只有一个通道
        drawSettings.SetOverrideMaterial(errorMaterial, 0);

        var filterSettings = new FilterRenderersSettings(true);

        context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
    }
}