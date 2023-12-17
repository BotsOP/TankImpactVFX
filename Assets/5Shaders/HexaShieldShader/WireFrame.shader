Shader "Unlit/WireframeFixedWidth"
{
    Properties
    {
        _WireframeColor("Wireframe color", color) = (1.0, 1.0, 1.0, 1.0)
        _Color("Mesh color", color) = (0.5, 0.5, 0.5, 1.0)
        _Emissive("Emissive", float) = 1
        _InsideWidth("Inside Width", Range(0,1)) = 1
        _Padding("Padding", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue"="Transparent"}
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma geometry geom
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            // We add our barycentric variables to the geometry struct.
            struct g2f {
                float4 pos : SV_POSITION;
                float3 barycentric : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            // This applies the barycentric coordinates to each vertex in a triangle.
            [maxvertexcount(3)]
            void geom(triangle v2f IN[3], inout TriangleStream<g2f> triStream) {
                g2f o;
                o.pos = IN[0].vertex;
                o.barycentric = float3(IN[0].uv.y, 0.0, 0.0);
                triStream.Append(o);
                o.pos = IN[1].vertex;
                o.barycentric = float3(0.0, IN[1].uv.y, 0.0);
                triStream.Append(o);
                o.pos = IN[2].vertex;
                o.barycentric = float3(0.0, 0.0, IN[2].uv.y);
                triStream.Append(o);
            }

            fixed4 _WireframeColor;
            fixed4 _Color;
            float _Emissive;
            float _InsideWidth;
            float _Padding;

            fixed4 frag(g2f i) : SV_Target
            {
                float closest = max(i.barycentric.x, max(i.barycentric.y, i.barycentric.z));
                // Set alpha to 1 if within the threshold, else 0.
                closest = 1 - step(_InsideWidth, (closest + _Padding) % 1);
                float4 color = lerp(_Color, _WireframeColor, closest);
                color.rgb *= _Emissive;
                return color;
            }
            ENDCG
        }
    }
}
