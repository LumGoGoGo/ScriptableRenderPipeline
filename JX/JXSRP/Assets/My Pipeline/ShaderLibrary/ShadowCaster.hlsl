#ifndef MYRP_SHADOWCASTER_INCLUDE
#define MYRP_SHADOWCASTER_INCLUDE

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

// 常量缓冲区 cbuffer

// MVP model->view->projection 一般M和VP单独提供，因为M会变化，且可以避免M*VP运算，VP可放入pre-frame缓冲区，M放入pre-draw缓冲区

// unity默认提供的矩阵
CBUFFER_START(UnityPerFrame)
	float4x4 unity_MatrixVP;	// 投影矩阵 V:view P:projection
CBUFFER_END

CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld;
CBUFFER_END

CBUFFER_START(_ShadowCasterBuffer)
	float _ShadowBias;
CBUFFER_END

// 此处的定义是为了保证变体后shader代码共用
#define UNITY_MATRIX_M unity_ObjectToWorld

// 由于Common.hlsl和UnityInstancing.hlsl的CBUFFER定义不一致，所以只能在此处include
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

// 如果针对移动端进行优化，要尽量去使用half
struct VertexInput
{
	float4 pos : POSITION;
	// GPUInstance需要通过ID去索引
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOutput
{
	float4 clipPos : SV_POSITION;
};

VertexOutput ShadowCasterPassVertex(VertexInput input)
{
	VertexOutput output;
	// 这样，就使用了不同实例的input
	UNITY_SETUP_INSTANCE_ID(input);

	// 模型空间转入世界空间
	float4 worldPos = mul(UNITY_MATRIX_M, float4(input.pos.xyz, 1.0));	// 这样的写法比input.pos.xyzw更优化
	// 世界空间转入裁剪空间
	output.clipPos = mul(unity_MatrixVP, worldPos);

	// 为了避免没在近平面前或者相交的遮挡物无法正常投影
	// 比较z和w，能保证不超出近平面，近平面z = -1，远平面 z = 1
	// OpenGL是反过来的
#if UNITY_REVERSED_Z 
	output.clipPos.z -= _ShadowBias;
	output.clipPos.z = min(output.clipPos.z, output.clipPos.w * UNITY_NEAR_CLIP_VALUE);
#else
	output.clipPos.z = max(output.clipPos.z, output.clipPos.w * UNITY_NEAR_CLIP_VALUE);
#endif

	return output;
}

float4 ShadowCasterPassFragment(VertexOutput input) : SV_TARGET
{
	return 0;
}

#endif
