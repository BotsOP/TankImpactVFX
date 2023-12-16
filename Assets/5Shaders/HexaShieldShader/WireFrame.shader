Shader "Unlit/WireframeFixedWidth"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _WireframeColor("Wireframe color", color) = (1.0, 1.0, 1.0, 1.0)
        _Color("Mesh color", color) = (0.5, 0.5, 0.5, 1.0)
        _WireframeWidth("Wireframe width", float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque"}
        LOD 100

        Pass
        {
            // Removes the front facing triangles, this enables us to create the wireframe for those behind.
            Cull Back
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

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
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
            float _WireframeWidth;

            fixed4 frag(g2f i) : SV_Target
            {
                // Calculate the unit width based on triangle size.
                float3 unitWidth = fwidth(i.barycentric);
                // Find the barycentric coordinate closest to the edge.
                float3 edge = step(unitWidth * _WireframeWidth, i.barycentric);
                // Set alpha to 1 if within edge width, else 0.
                float alpha = 1 - min(edge.x, min(edge.y, edge.z));
                float3 color = lerp(_Color, _WireframeColor, alpha);
                // Set to our backwards facing wireframe colour.
                return fixed4(color, 0.5);
            }
            ENDCG
        }
    }
}
