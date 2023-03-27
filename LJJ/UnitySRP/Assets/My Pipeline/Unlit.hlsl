﻿#ifndef MYRP_UNLIT_INCLUDED
#define MYRP_UNLIT_INCLUDED

float4x4 unity_MatrixVP;
float4x4 unity_ObjectToWorld;

struct VertexInput
{
	float4 pos : POSITION;
};

struct VertexOutput
{
	float4 clipPos : SV_POSITION;
};

// 顶点程序函数
VertexOutput UnlitPassVertex(VertexInput input)
{
	VertexOutput output;
	float4 worldPos = mul(unity_ObjectToWorld, float4(input.pos.xyz, 1.0));
	output.clipPos = mul(unity_MatrixVP, worldPos);
	return output;
}

// 片段程序函数
float4 UnlitPassFragment(VertexOutput input) : SV_TARGET
{
	return 1;
}

#endif