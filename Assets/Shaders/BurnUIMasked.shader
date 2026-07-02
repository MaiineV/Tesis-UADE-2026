// BurnUIMasked: burn-dissolve UI shader that respects the assigned sprite's
// own silhouette (via _MainTex alpha) instead of burning the full quad, and
// drives the Voronoi noise + edge ember texture with real elapsed time.
Shader "BurnUIMasked"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)

		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255

		_ColorMask ("Color Mask", Float) = 15

		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

		_Progress ("Progress", Range( -1 , 1)) = -1
		_VoronoiScale ("Voronoi Scale", Float) = 4.37
		_EdgeWidth ("Edge Width", Range( 0.01 , 1)) = 0.1
		_EmberTex ("Ember Texture (edge)", 2D) = "white" {}
		_EmberSpeed ("Ember Scroll Speed", Vector) = (0.15, 0.1, 0, 0)
		_EmberTiling ("Ember Tiling", Float) = 2
		_EmberColor ("Ember Tint", Color) = (1, 0.5, 0.1, 1)
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
			uniform float _Progress;
			uniform float _VoronoiScale;
			uniform float _EdgeWidth;
			uniform sampler2D _EmberTex;
			uniform float4 _EmberSpeed;
			uniform float _EmberTiling;
			uniform fixed4 _EmberColor;

			float2 voronoihash2( float2 p )
			{
				p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
				return frac( sin( p ) * 43758.5453);
			}

			float voronoi2( float2 v, float time, inout float2 id, inout float2 mr, float smoothness )
			{
				float2 n = floor( v );
				float2 f = frac( v );
				float F1 = 8.0;
				float F2 = 8.0; float2 mg = 0;
				for ( int j = -1; j <= 1; j++ )
				{
					for ( int i = -1; i <= 1; i++ )
					{
						float2 g = float2( i, j );
						float2 o = voronoihash2( n + g );
						o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
						float d = 0.5 * dot( r, r );
						if( d<F1 ) {
							F2 = F1;
							F1 = d; mg = g; mr = r; id = o;
						} else if( d<F2 ) {
							F2 = d;
						}
					}
				}
				return F1;
			}

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

				fixed4 spriteColor = tex2D( _MainTex, IN.texcoord ) * IN.color;
				float spriteAlpha = spriteColor.a; // silueta real del sprite asignado

				float2 coords = IN.texcoord.xy * _VoronoiScale;
				float2 id = 0;
				float2 mr = 0;
				float voroi = voronoi2( coords, _Time.y, id, mr, 0 );

				// 0 = quemado, 1 = intacto — banda de transicion de ancho _EdgeWidth
				float edge = smoothstep( _Progress, _Progress + _EdgeWidth, voroi );
				float burnCutoff = step( _Progress, voroi );

				float2 emberUV = IN.texcoord * _EmberTiling + _Time.y * _EmberSpeed.xy;
				fixed4 ember = tex2D( _EmberTex, emberUV ) * _EmberColor;

				fixed3 rgb = lerp( ember.rgb, spriteColor.rgb, edge );
				// nunca "sale" de la forma del sprite, sin importar el burn
				float alpha = burnCutoff * spriteAlpha;

				fixed4 color = fixed4( rgb, alpha );

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
