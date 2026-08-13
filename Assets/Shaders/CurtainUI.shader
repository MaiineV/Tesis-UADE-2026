// CurtainUI: fake velvet-curtain look for a flat UI quad. No vertex geometry to
// fold, so pleats are faked in the fragment shader (alternating light/dark
// vertical bands via sin+pow) with a subtle time-based sway so the fabric
// visibly "breathes" while it's on screen. Opening/closing is handled by
// sliding the RectTransform in C# (MainMenuIntroAnimation), not by this shader.
Shader "CurtainUI"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture (grano opcional)", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)

		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255

		_ColorMask ("Color Mask", Float) = 15

		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

		_BaseColor ("Velvet Base", Color) = (0.55, 0.03, 0.06, 1)
		_ShadowColor ("Velvet Fold Shadow", Color) = (0.22, 0.01, 0.03, 1)
		_FoldCount ("Fold Count", Float) = 14
		_FoldSharpness ("Fold Sharpness", Range( 1 , 8)) = 3
		_SwaySpeed ("Sway Speed", Float) = 0.6
		_SwayAmount ("Sway Amount", Range( 0 , 0.1)) = 0.02
		_VignetteStrength ("Top/Bottom Vignette", Range( 0 , 1)) = 0.4
	}

	SubShader
	{
		LOD 0

		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }

		Stencil
		{
			Ref [_Stencil]
			ReadMask [_StencilReadMask]
			WriteMask [_StencilWriteMask]
			CompFront [_StencilComp]
			PassFront [_StencilOp]
			FailFront Keep
			ZFailFront Keep
			CompBack Always
			PassBack Keep
			FailBack Keep
			ZFailBack Keep
		}

		Cull Off
		Lighting Off
		ZWrite Off
		ZTest [unity_GUIZTestMode]
		Blend SrcAlpha OneMinusSrcAlpha
		ColorMask [_ColorMask]

		Pass
		{
			Name "Default"
		CGPROGRAM

			#ifndef UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX
			#define UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input)
			#endif
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0

			#include "UnityCG.cginc"
			#include "UnityUI.cginc"

			#pragma multi_compile __ UNITY_UI_CLIP_RECT
			#pragma multi_compile __ UNITY_UI_ALPHACLIP

			struct appdata_t
			{
				float4 vertex   : POSITION;
				float4 color    : COLOR;
				float2 texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 vertex   : SV_POSITION;
				fixed4 color    : COLOR;
				half2 texcoord  : TEXCOORD0;
				float4 worldPosition : TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			uniform fixed4 _Color;
			uniform fixed4 _TextureSampleAdd;
			uniform float4 _ClipRect;
			uniform sampler2D _MainTex;
			uniform fixed4 _BaseColor;
			uniform fixed4 _ShadowColor;
			uniform float _FoldCount;
			uniform float _FoldSharpness;
			uniform float _SwaySpeed;
			uniform float _SwayAmount;
			uniform float _VignetteStrength;

			v2f vert( appdata_t IN  )
			{
				v2f OUT;
				UNITY_SETUP_INSTANCE_ID( IN );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
				UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
				OUT.worldPosition = IN.vertex;

				OUT.worldPosition.xyz += float3( 0, 0, 0 );
				OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

				OUT.texcoord = IN.texcoord;

				OUT.color = IN.color * _Color;
				return OUT;
			}

			fixed4 frag(v2f IN  ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				// Desfasaje que varia con la altura y el tiempo real — hace que la
				// fase de los pliegues "respire" en vez de quedar estatica.
				float sway = sin( IN.texcoord.y * 3 + _Time.y * _SwaySpeed ) * _SwayAmount;
				float foldPhase = ( IN.texcoord.x + sway ) * _FoldCount * 6.28318;
				float fold = pow( abs( sin( foldPhase ) ), _FoldSharpness );
				fixed3 velvet = lerp( _ShadowColor.rgb, _BaseColor.rgb, fold );

				// Viñeta arriba/abajo: sombra de la barra del telón y del piso,
				// le da profundidad a un quad plano.
				float topShadow = smoothstep( 0.0, 0.15, IN.texcoord.y );
				float bottomShadow = smoothstep( 1.0, 0.85, IN.texcoord.y );
				velvet *= lerp( 1 - _VignetteStrength, 1, topShadow ) * lerp( 1 - _VignetteStrength, 1, bottomShadow );

				fixed4 grain = tex2D( _MainTex, IN.texcoord ) * IN.color;
				fixed4 color = fixed4( velvet * grain.rgb, grain.a );

				#ifdef UNITY_UI_CLIP_RECT
				color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
				#endif

				#ifdef UNITY_UI_ALPHACLIP
				clip (color.a - 0.001);
				#endif

				return color;
			}
		ENDCG
		}
	}
}
