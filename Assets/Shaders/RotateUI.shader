// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "RotateUI"
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
		_Vanish("Vanish", Range( -1 , 1)) = -1
		_Progress("Progress", Range( 0 , 1)) = 0
		_Rotation("Rotation", 2D) = "black" {}
		_RotationMask("Rotation Mask", 2D) = "black" {}
		_RotationPosScale("Rotation Pos Scale", Vector) = (0,0,1,1)
		_RotationTint("Rotation Tint", Color) = (0,0.3379312,1,1)
		[HideInInspector] _texcoord( "", 2D ) = "white" {}

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
			
			#include "UnityShaderVariables.cginc"

			
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
			uniform float4 _MainTex_ST;
			uniform sampler2D _Rotation;
			uniform float4 _RotationPosScale;
			uniform sampler2D _RotationMask;
			uniform float4 _RotationTint;
			uniform float _Progress;
			uniform float _Vanish;
					float2 voronoihash27( float2 p )
					{
						
						p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
						return frac( sin( p ) *43758.5453);
					}
			
					float voronoi27( float2 v, float time, inout float2 id, inout float2 mr, float smoothness )
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
						 		float2 o = voronoihash27( n + g );
								o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
								float d = 0.5 * ( abs(r.x) + abs(r.y) );
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
				
				
				OUT.worldPosition.xyz +=  float3( 0, 0, 0 ) ;
				OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

				OUT.texcoord = IN.texcoord;
				
				OUT.color = IN.color * _Color;
				return OUT;
			}

			fixed4 frag(v2f IN  ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				float2 uv_MainTex = IN.texcoord.xy * _MainTex_ST.xy + _MainTex_ST.zw;
				float4 temp_output_192_0_g586 = ( tex2D( _MainTex, uv_MainTex ) * _Color );
				float4 temp_output_57_0_g586 = _RotationPosScale;
				float2 temp_output_2_0_g586 = (temp_output_57_0_g586).zw;
				float2 temp_cast_0 = (1.0).xx;
				float2 temp_output_13_0_g586 = ( ( ( IN.texcoord.xy + (temp_output_57_0_g586).xy ) * temp_output_2_0_g586 ) + -( ( temp_output_2_0_g586 - temp_cast_0 ) * 0.5 ) );
				float TimeVar197_g586 = _Time.y;
				float cos17_g586 = cos( TimeVar197_g586 );
				float sin17_g586 = sin( TimeVar197_g586 );
				float2 rotator17_g586 = mul( temp_output_13_0_g586 - float2( 0.5,0.5 ) , float2x2( cos17_g586 , -sin17_g586 , sin17_g586 , cos17_g586 )) + float2( 0.5,0.5 );
				float4 tex2DNode97_g586 = tex2D( _Rotation, rotator17_g586 );
				float4 break15 = ( temp_output_192_0_g586 + ( ( ( tex2DNode97_g586 * tex2D( _RotationMask, IN.texcoord.xy ).g * tex2DNode97_g586.a ) * _RotationTint ) * (temp_output_192_0_g586).a ) );
				float2 texCoord10 = IN.texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float lerpResult14 = lerp( 1.0 , 0.2 , step( _Progress , texCoord10.y ));
				float time27 = 0.0;
				float2 coords27 = IN.texcoord.xy * 10.17;
				float2 id27 = 0;
				float2 uv27 = 0;
				float voroi27 = voronoi27( coords27, time27, id27, uv27, 0 );
				float smoothstepResult26 = smoothstep( _Vanish , ( _Vanish + 0.1 ) , voroi27);
				float4 appendResult17 = (float4(break15.r , break15.g , break15.b , ( ( break15.a * lerpResult14 ) * smoothstepResult26 )));
				
				half4 color = appendResult17;
				
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
	CustomEditor "ASEMaterialInspector"
	
	
}
/*ASEBEGIN
Version=18900
268;73;1030;644;271.9438;358.5419;1.629593;True;False
Node;AmplifyShaderEditor.TemplateShaderPropertyNode;1;-1450.904,-146.9988;Inherit;True;0;0;_MainTex;Shader;False;0;5;SAMPLER2D;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;3;-1210.123,-151.0984;Inherit;True;Property;_TextureSample4;Texture Sample 4;26;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TemplateShaderPropertyNode;2;-1064.257,37.76482;Inherit;False;0;0;_Color;Shader;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;6;-886.2742,91.93604;Float;False;Property;_RotationTint;Rotation Tint;12;0;Create;True;0;0;0;False;0;False;0,0.3379312,1,1;0,0.3379312,1,1;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode;10;-461.192,725.4427;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TexturePropertyNode;5;-894.333,253.8423;Float;True;Property;_Rotation;Rotation;9;0;Create;True;0;0;0;False;0;False;None;a89fe7b6f57d9e44d926a5e95093db52;False;black;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.RangedFloatNode;11;-486.6282,621.4952;Inherit;False;Property;_Progress;Progress;8;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;4;-806.1582,-57.42568;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.Vector4Node;7;-642.5916,323.2122;Float;False;Property;_RotationPosScale;Rotation Pos Scale;11;0;Create;True;0;0;0;False;0;False;0,0,1,1;0,-0.06,1.63,4.74;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TexturePropertyNode;8;-902.1857,447.4778;Float;True;Property;_RotationMask;Rotation Mask;10;0;Create;True;0;0;0;False;0;False;None;b1af5036b3a86514ea33dcb0049e111b;False;black;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.FunctionNode;9;-519.7957,48.22108;Inherit;False;UI-Sprite Effect Layer;0;;586;789bf62641c5cfe4ab7126850acc22b8;18,74,2,204,2,191,1,225,0,242,0,237,0,249,0,186,0,177,0,182,0,229,0,92,1,98,1,234,0,126,0,129,0,130,1,31,2;18;192;COLOR;0,0,0,1;False;39;COLOR;1,1,1,1;False;37;SAMPLER2D;;False;218;FLOAT2;0,0;False;239;FLOAT2;0,0;False;181;FLOAT2;0,0;False;75;SAMPLER2D;;False;80;FLOAT;1;False;183;FLOAT2;0,0;False;188;SAMPLER2D;;False;33;SAMPLER2D;;False;248;FLOAT2;0,0;False;233;SAMPLER2D;;False;101;SAMPLER2D;;False;57;FLOAT4;0,0,0,0;False;40;FLOAT;0;False;231;FLOAT;1;False;30;FLOAT;1;False;2;COLOR;0;FLOAT2;172
Node;AmplifyShaderEditor.RangedFloatNode;13;-72.42758,555.4955;Inherit;False;Constant;_Alpha;Alpha ;6;0;Create;True;0;0;0;False;0;False;0.2;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.StepOpNode;12;-205.027,701.0955;Inherit;True;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;25;513.5927,516.4388;Inherit;False;Property;_Vanish;Vanish;7;0;Create;True;0;0;0;False;0;False;-1;0;-1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;15;-178.9725,56.61078;Inherit;False;COLOR;1;0;COLOR;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.LerpOp;14;71.96856,677.7574;Inherit;True;3;0;FLOAT;1;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;28;731.7006,622.9678;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.1;False;1;FLOAT;0
Node;AmplifyShaderEditor.VoronoiNode;27;628.8375,373.3119;Inherit;False;0;2;1;0;1;False;1;False;False;4;0;FLOAT2;0,0;False;1;FLOAT;0;False;2;FLOAT;10.17;False;3;FLOAT;0;False;3;FLOAT;0;FLOAT2;1;FLOAT2;2
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;16;-17.96208,315.8522;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SmoothstepOpNode;26;852.7785,538.9581;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;29;799.4914,319.7159;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;17;922.6857,41.87817;Inherit;False;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;0;1346.302,37.98168;Float;False;True;-1;2;ASEMaterialInspector;0;4;RotateUI;5056123faa0c79b47ab6ad7e8bf059a4;True;Default;0;0;Default;2;False;True;2;5;False;-1;10;False;-1;0;1;False;-1;0;False;-1;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;-1;False;True;True;True;True;True;0;True;-9;False;False;False;False;False;False;False;True;True;0;True;-5;255;True;-8;255;True;-7;0;True;-4;0;True;-6;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;False;True;2;False;-1;True;0;True;-11;False;True;5;Queue=Transparent=Queue=0;IgnoreProjector=True;RenderType=Transparent=RenderType;PreviewType=Plane;CanUseSpriteAtlas=True;False;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;0;;0;0;Standard;0;0;1;True;False;;False;0
WireConnection;3;0;1;0
WireConnection;4;0;3;0
WireConnection;4;1;2;0
WireConnection;9;192;4;0
WireConnection;9;39;6;0
WireConnection;9;37;5;0
WireConnection;9;101;8;0
WireConnection;9;57;7;0
WireConnection;12;0;11;0
WireConnection;12;1;10;2
WireConnection;15;0;9;0
WireConnection;14;1;13;0
WireConnection;14;2;12;0
WireConnection;28;0;25;0
WireConnection;16;0;15;3
WireConnection;16;1;14;0
WireConnection;26;0;27;0
WireConnection;26;1;25;0
WireConnection;26;2;28;0
WireConnection;29;0;16;0
WireConnection;29;1;26;0
WireConnection;17;0;15;0
WireConnection;17;1;15;1
WireConnection;17;2;15;2
WireConnection;17;3;29;0
WireConnection;0;0;17;0
ASEEND*/
//CHKSM=692F4508B1750F9E62B62109568CBA158B9583A3