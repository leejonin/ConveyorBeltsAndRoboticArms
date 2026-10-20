Shader "SmartFactory/QrMaskBlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _QrMask ("QR Mask xy=center zw=halfSize", Vector) = (0.5, 0.5, 0.08, 0.08)
    }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _QrMask;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                float2 d = abs(i.uv - _QrMask.xy);
                if (d.x < _QrMask.z && d.y < _QrMask.w)
                {
                    col = fixed4(0, 0, 0, 1);
                }
                return col;
            }
            ENDCG
        }
    }
}
