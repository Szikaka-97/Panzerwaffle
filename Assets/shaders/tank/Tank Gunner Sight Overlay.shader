HEADER
{
    Description = "Template Shader for S&box";
    DevShader = true;
}

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	Forward();
	Depth();
}

COMMON
{
	#include "common/shared.hlsl"
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs(VertexInput i) {
		PixelInput o = ProcessVertex(i);
		return FinalizeVertex(o);
	}
}

PS
{
	#include "common/pixel.hlsl"
	
	half3 OverlayTint < UiType(Color); Default3(1.0, 1.0, 1.0); UiGroup("Parameters,10/10"); >;
	half2 HullAtlasPosition < Range2(0.0, 0.0, 1.0, 1.0); Default2(0.0, 0.0); UiGroup("Parameters,10/30"); >;
	half2 TurretAtlasPosition < Range2(0.0, 0.0, 1.0, 1.0); Default2(0.0, 0.0); UiGroup("Parameters,10/30"); >;
	half CompassScale < Default(1.0); UiGroup("Parameters,10/50"); >;
	half RangeMonitorScale < Default(1.0); UiGroup("Parameters,10/90"); >;
	half2 CompassPosition < Range2(0.0, 0.0, 1.0, 1.0); Default2(0.0, 0.0); UiGroup("Parameters,10/60"); >;
	half2 RangeMonitorAtlasPosition < Range2(0.0, 0.0, 1.0, 1.0); Default2(0.0, 0.0); UiGroup("Parameters,10/70"); >;
	half2 RangeMonitorPosition < Range2(0.0, 0.0, 1.0, 1.0); Default2(0.0, 0.0); UiGroup("Parameters,10/80"); >;

	// Ideally, these would be in their own constant buffer
	// Unfortunately, s&box doesn't let us use constant buffers
	uint RangeMonitorReadout < Range(0, 9999); Default(1000); UiGroup( "Parameters,10/100" ); >;
	half TurretRotation < Range(-6.29, 6.29); Default(0.0); UiGroup( "Parameters,10/40" ); >;

	SamplerState g_sSampler < Filter(ANISO); AddressU(WRAP); AddressV(WRAP); >;
	CreateInputTexture2D(AtlasTexture, Linear, 1, "", "", "Parameters, 10/20", Default(0.0));
	Texture2D g_tAtlasTexture < Channel(R, Box(AtlasTexture), Linear); OutputFormat(BC7); SrgbRead(false); >;

	float2 Rotate(uniform float2 p, uniform float rotation) {
		const float sine = sin(rotation);
		const float cosine = cos(rotation);

		return float2(
			cosine * p.x - sine * p.y,
			cosine * p.y + sine * p.x
		);
	}

	float2 OffsetAndScale(
		uniform float2 uv,
		uniform float2 atlasPosition,
		uniform float2 scale,
		uniform float2 offset,
		uniform float2 boundsMin,
		uniform float2 boundsMax,
		uniform float rotation
	) {
		float2 result = uv - offset;
		result = Rotate(result, rotation);
		result /= scale;
		result = clamp(result, boundsMin, boundsMax);
		result += atlasPosition;

		return result;
	}

	half OffsetScaleAndSample(
		uniform float2 uv,
		uniform float2 atlasPosition,
		uniform float2 scale,
		uniform float2 offset,
		uniform float2 boundsMin,
		uniform float2 boundsMax,
		uniform float rotation
	) {
		float2 result = uv - offset;
		result = Rotate(result, rotation);
		result /= scale;

		if (result.x < boundsMin.x || result.y < boundsMin.y || result.x > boundsMax.x || result.y > boundsMax.y) {
			return 0;
		}

		result += atlasPosition;

		return g_tAtlasTexture.Sample(g_sSampler, result).x;
	}

	float4 MainPs(PixelInput i) : SV_Target0 {
		const float2 uv = i.vTextureCoords.xy;

		half hull = OffsetScaleAndSample(
			uv, HullAtlasPosition, CompassScale, CompassPosition, float2(-0.2, -0.26), float2(0.13, 0.40), 0
		);

		half turret = OffsetScaleAndSample(
			uv, TurretAtlasPosition, CompassScale, CompassPosition, float2(-0.12, -0.45), float2(0.12, 0.2), -TurretRotation
		);

		float color = hull + turret;

		int currentReadout = RangeMonitorReadout;
		currentReadout = clamp(currentReadout, 0, 9999);

		bool trip = false;

		if (currentReadout > 999) {
			half digit = OffsetScaleAndSample(
				uv, RangeMonitorAtlasPosition + float2(0.0625 * (currentReadout / 1000), 0), RangeMonitorScale, RangeMonitorPosition, float2(0, -0.125), float2(0.0625, -0.01), 0
			);

			color += digit;

			currentReadout %= 1000;

			trip = true;
		}
		if (currentReadout > 99 || trip) {
			half digit = OffsetScaleAndSample(
				uv, RangeMonitorAtlasPosition + float2(0.0625 * (currentReadout / 100), 0), RangeMonitorScale, RangeMonitorPosition + float2(RangeMonitorScale * 0.0625, 0), float2(0, -0.125), float2(0.0625, -0.01), 0
			);

			color += digit;

			currentReadout %= 100;

			trip = true;
		}
		if (currentReadout > 9 || trip) {
			half digit = OffsetScaleAndSample(
				uv, RangeMonitorAtlasPosition + float2(0.0625 * (currentReadout / 10), 0), RangeMonitorScale, RangeMonitorPosition + float2(RangeMonitorScale * 0.0625 * 2, 0), float2(0, -0.125), float2(0.0625, -0.01), 0
			);

			color += digit;
		}

		half digit = OffsetScaleAndSample(
			uv, RangeMonitorAtlasPosition, RangeMonitorScale, RangeMonitorPosition + float2(RangeMonitorScale * 0.0625 * 3, 0), float2(0, -0.125), float2(0.0625, -0.01), 0
		);

		color += digit;

		const half crosshairThickness = 0.002;
		const half crosshairLength = 0.05;

		half crosshair = half((abs(uv.x - 0.5) < crosshairLength && abs(uv.y - 0.5) < crosshairThickness) || (abs(uv.y - 0.5) < crosshairLength && abs(uv.x - 0.5) < crosshairThickness));

		color += crosshair;

		clip(color - 0.1);

		return float4(color * OverlayTint, 1);
	}
}
