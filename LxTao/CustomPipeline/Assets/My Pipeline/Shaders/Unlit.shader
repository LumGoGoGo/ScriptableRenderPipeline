﻿Shader "My Pipeline/Unlit"
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

			#pragma vertex UnlitPassVertex
			#pragma fragment UnlitPassFragment
			
			#include "../ShaderLibrary/Unlit.hlsl"
			
			ENDHLSL
        }
    }
}
