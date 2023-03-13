#ifndef MYRP_UNLIT_INCLUDE
#define MYRP_UNLIT_INCLUDE

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

// 此处的定义是为了保证变体后shader代码共用
#define UNITY_MATRIX_M unity_ObjectToWorld

//CBUFFER_START(UnityPerMaterial)
//	float4 _Color;
//CBUFFER_END

// 由于Common.hlsl和UnityInstancing.hlsl的CBUFFER定义不一致，所以只能在此处include
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

// 如果不使用实例化，则最终等于float4 _Color，使用的话，我们最终得到一个实例数据数组
UNITY_INSTANCING_BUFFER_START(PerInstance)
	UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
UNITY_INSTANCING_BUFFER_END(PerInstance)

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

	// GPUInstance需要通过ID去索引
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

VertexOutput UnlitPassVertex(VertexInput input)
{
	VertexOutput output;
	// 这样，就使用了不同实例的input
	UNITY_SETUP_INSTANCE_ID(input);

	// 相同的ID传递给output
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	// 模型空间转入世界空间
	float4 worldPos = mul(UNITY_MATRIX_M, float4(input.pos.xyz, 1.0));	// 这样的写法比input.pos.xyzw更优化
	// 世界空间转入裁剪空间
	output.clipPos = mul(unity_MatrixVP, worldPos);
	return output;
}

float4 UnlitPassFragment(VertexOutput input) : SV_TARGET
{
	UNITY_SETUP_INSTANCE_ID(input);
	// 必须通过UNITY_ACCESS_INSTANCED_PROP宏对其进行访问，并将其传递给我们的缓冲区和属性名称
	return UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Color);
}

#endif
