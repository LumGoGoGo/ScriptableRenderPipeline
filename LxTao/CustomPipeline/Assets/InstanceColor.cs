using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InstanceColor : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    [SerializeField]
    Color color = Color.white;
    static MaterialPropertyBlock propertyBlock;
    static int colorID = Shader.PropertyToID("_Color");

    void Awake()
    {
        OnValidate();
    }

    void OnValidate()
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
        propertyBlock.SetColor(colorID, color);
        GetComponent<MeshRenderer>().SetPropertyBlock(propertyBlock);
    }
}
