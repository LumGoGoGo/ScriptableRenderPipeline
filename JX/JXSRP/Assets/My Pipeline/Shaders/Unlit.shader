Shader "My Pipeline/Unlit"
{
    Properties
    {
        _Color("Color", Color) = (1, 1, 1, 1)
    }
    
    SubShader
    {
        Pass
        {
            HLSLPROGRAM
            
            // 直接不对openg es 2.0进行支持，默认级别为2.5
            #pragma target 3.5
            
            // 生成GPUInstance和非GPUInstance变体
            #pragma multi_compile_instancing

            // 假设等比缩放
            #pragma instancing_options assumeuniformscaling

            #pragma vertex UnlitPassVertex
            #pragma fragment UnlitPassFragment
            
            #include "../ShaderLibrary/Unlit.hlsl"
            ENDHLSL
        }
    }
}
