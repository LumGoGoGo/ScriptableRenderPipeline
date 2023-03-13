using UnityEngine;

public class InstancedColor : MonoBehaviour
{
    [SerializeField]
    Color color = Color.white;

    static MaterialPropertyBlock propertyBlock;
    static int colorID = Shader.PropertyToID("_Color");

    // 假设在运行期颜色不变
    void Awake()
    {
        OnValidate();
    }
    // OnValidate是一种特殊的Unity消息方法。在组件加载或更改时，在编辑模式下调用它。因此，每次加载场景时以及编辑组件时调用
    void OnValidate()
    {
        if(propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
        propertyBlock.SetColor(colorID, color);
        GetComponent<MeshRenderer>().SetPropertyBlock(propertyBlock);
    }
}
