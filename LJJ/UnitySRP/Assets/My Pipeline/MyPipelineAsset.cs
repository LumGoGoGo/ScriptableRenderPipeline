using UnityEngine;
// Experimental:实验性namespace
using UnityEngine.Experimental.Rendering;

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
