Shader "My Pipeline/Lit"
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

            // 一个是_，一个是_SHADOWS_SOFT
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _SHADOWS_HARD

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            
            #include "../ShaderLibrary/Lit.hlsl"
            ENDHLSL
        }

        Pass
        {
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            HLSLPROGRAM

            #pragma target 3.5

            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #pragma vertex ShadowCasterPassVertex
            #pragma fragment ShadowCasterPassFragment

            #include "../ShaderLibrary/ShadowCaster.hlsl"
            ENDHLSL
        }
    }
}
