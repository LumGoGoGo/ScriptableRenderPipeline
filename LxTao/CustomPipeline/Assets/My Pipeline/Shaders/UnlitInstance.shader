﻿Shader "My Pipeline/UnlitInstance"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
       // Tags { "RenderType"="Opaque" }
       // LOD 100

        Pass
        {
            HLSLPROGRAM
			#pragma target 3.5

            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
			#pragma vertex UnlitPassVertex
			#pragma fragment UnlitPassFragment
			
			#include "../ShaderLibrary/Unlit.hlsl"
			
			ENDHLSL
        }
    }
}
