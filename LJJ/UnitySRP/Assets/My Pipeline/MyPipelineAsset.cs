using UnityEngine;
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

    public void Render(ScriptableRenderContext renderContext, Camera camera)
    {
        // 设置摄像机视图矩阵V和投影矩阵P等
        renderContext.SetupCameraProperties(camera);

        // 仅用到相机清除标志决定是否绘制天空盒
        renderContext.DrawSkybox(camera);

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
