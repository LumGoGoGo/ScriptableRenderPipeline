#ifndef MYRP_LIT_INCLUDE
#define MYRP_LIT_INCLUDE

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

// 常量缓冲区 cbuffer

// MVP model->view->projection 一般M和VP单独提供，因为M会变化，且可以避免M*VP运算，VP可放入pre-frame缓冲区，M放入pre-draw缓冲区

// unity默认提供的矩阵
CBUFFER_START(UnityPerFrame)
	float4x4 unity_MatrixVP;	// 投影矩阵 V:view P:projection
CBUFFER_END

CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld;
	float4 unity_LightIndicesOffsetAndCount;	// y:影响对象的灯光数量
	float4 unity_4LightIndices0, unity_4LightIndices1;	// 灯光会有重要性排序，所以主要处理0中的灯光
CBUFFER_END

// 暂时仅支持16个光源
#define MAX_VISIBLE_LIGHTS 16

// 这个buffer需要外部指定进行传入
CBUFFER_START(_LightBuffer)
	float4 _VisibleLightColors[MAX_VISIBLE_LIGHTS];
	float4 _VisibleLightDirectionsOrPositions[MAX_VISIBLE_LIGHTS];
	float4 _VisibleLightAttenuations[MAX_VISIBLE_LIGHTS];
	float4 _VisibleLightSpotDirections[MAX_VISIBLE_LIGHTS];
CBUFFER_END

CBUFFER_START(_ShadowBuffer)
	//float4x4 _WorldToShadowMatrix;
	//float _ShadowStrength;
	float4x4 _WorldToShadowMatrices[MAX_VISIBLE_LIGHTS];
	float4 _ShadowData[MAX_VISIBLE_LIGHTS];
	float4 _ShadowMapSize;
CBUFFER_END

TEXTURE2D_SHADOW(_ShadowMap);
// 采样器
SAMPLER_CMP(sampler_ShadowMap);

float HardShadowAttenuation(float4 shadowPos)
{
	return SAMPLE_TEXTURE2D_SHADOW(_ShadowMap, sampler_ShadowMap, shadowPos.xyz);
}

float SoftShadowAttenuation(float4 shadowPos)
{
	// real:自动选择float或者half，为了避免平台编译出错，使用real
	real tentWeights[9];
	real2 tentUVs[9];
	// 构建卷积核
	SampleShadow_ComputeSamples_Tent_5x5(_ShadowMapSize, shadowPos.xy, tentWeights, tentUVs);
	float attenuation = 0;
	for(int i = 0; i < 9; i++)
	{
		attenuation += tentWeights[i] * SAMPLE_TEXTURE2D_SHADOW(_ShadowMap, sampler_ShadowMap, float3(tentUVs[i].xy, shadowPos.z));
	}
	return attenuation;
}

float ShadowAttenuation(int index, float3 worldPos)
{
#if !defined(_SHADOWS_HARD) && !defined(_SHADOWS_SOFT)
	return 1.0;
#endif
	if(_ShadowData[index].x <= 0)
	{
		return 1.0;
	}
	float4 shadowPos = mul(_WorldToShadowMatrices[index], float4(worldPos, 1.0));

	// 齐次坐标转常规坐标
	shadowPos.xyz /= shadowPos.w;

	// 如果该点的位置的z小于阴影贴图的值，返回1，说明比任何投射阴影的物理离光源近
	float attenuation;

#if defined(_SHADOWS_HARD)
	#if defined(_SHADOWS_SOFT)
		// y判断软阴影
		if(_ShadowData[index].y == 0)
		{
			attenuation = HardShadowAttenuation(shadowPos);
		}
		else
		{
			attenuation = SoftShadowAttenuation(shadowPos);
		}
	#else
		attenuation = HardShadowAttenuation(shadowPos);
	#endif
#else
	attenuation = SoftShadowAttenuation(shadowPos);
#endif

	return lerp(1, attenuation, _ShadowData[index].x);
}

// 漫反射光照计算函数
float3 DiffuseLight(int index, float3 normal, float3 worldPos, float shadowAttenuation)
{
	// 灯光衰减方程网上查询即可
	float3 lightColor = _VisibleLightColors[index].rgb;
	float4 lightDirectionOrPosition = _VisibleLightDirectionsOrPositions[index];
	float4 lightAttenuation = _VisibleLightAttenuations[index];
	float3 spotDirection = _VisibleLightSpotDirections[index].xyz;
	// 方向光w = 0
	float3 lightVector = lightDirectionOrPosition.xyz - worldPos * lightDirectionOrPosition.w;
	float3 lightDirection = normalize(lightVector);
	float diffuse = saturate(dot(normal, lightDirection));

	float rangeFade = dot(lightVector, lightVector) * lightAttenuation.x;
	rangeFade = saturate(1.0 - rangeFade * rangeFade);
	rangeFade *= rangeFade;

	float spotFade = dot(spotDirection, lightDirection);
	spotFade = saturate(spotFade * lightAttenuation.z + lightAttenuation.w);
	spotFade *= spotFade;

	float distanceSqr = max(dot(lightVector, lightVector), 0.00001);	// 方向光结果是1，不影响
	diffuse *= shadowAttenuation * spotFade * rangeFade / distanceSqr;

	return diffuse * lightColor;
}

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
	float3 normal : NORMAL;
	// GPUInstance需要通过ID去索引
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOutput
{
	float4 clipPos : SV_POSITION;
	float3 normal : TEXCOORD0;	// 一般自定义的选用TEXCOORD 内置的还有COLOR0 COLOR1等
	float3 worldPos : TEXCOORD1;

	// 后四个灯光逐顶点计算
	float3 vertexLighting : TEXCOORD2;
	// GPUInstance需要通过ID去索引
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

VertexOutput LitPassVertex(VertexInput input)
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
	// 法线从模型空间转世界空间，非等比缩放需要使用逆转置
	output.normal = mul((float3x3)UNITY_MATRIX_M, input.normal);
	output.worldPos = worldPos;

	// 后四个灯光重要性不高，所以通过顶点着色器去计算，节省性能
	output.vertexLighting = 0;
	for(int i = 4; i < min(unity_LightIndicesOffsetAndCount.y, 8); i++)
	{
		int lightIndex = unity_4LightIndices1[i - 4];
		// 计算每个灯光的漫反射
		output.vertexLighting += DiffuseLight(lightIndex, output.normal, output.worldPos, 1);
	}

	return output;
}

float4 LitPassFragment(VertexOutput input) : SV_TARGET
{
	UNITY_SETUP_INSTANCE_ID(input);
	input.normal = normalize(input.normal);

	// 必须通过UNITY_ACCESS_INSTANCED_PROP宏对其进行访问，并将其传递给我们的缓冲区和属性名称
	float3 albedo = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Color).rgb;

	float3 diffuseLight = input.vertexLighting;
	for(int i = 0; i < min(unity_LightIndicesOffsetAndCount.y, 4); i++)
	{
		int lightIndex = unity_4LightIndices0[i];
		float shadowAttenuation = ShadowAttenuation(lightIndex, input.worldPos);
		// 计算每个灯光的漫反射
		diffuseLight += DiffuseLight(lightIndex, input.normal, input.worldPos, shadowAttenuation);
	}

	float3 color = diffuseLight * albedo;
	return float4(color, 1.0);
}

#endif
