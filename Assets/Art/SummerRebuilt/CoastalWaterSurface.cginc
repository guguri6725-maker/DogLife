// 바닥, 낙하 물, 접점 거품이 같은 색 계산을 공유합니다.
        sampler2D _MainTex;
        fixed4 _Color;
        float _FlowSpeed, _Opacity, _SurfaceMode, _WorldTiling, _GateMode, _OpenProgress, _FlowTime, _ImpactMode, _WaterLevel;
        float _FloodVisibility, _FloodLevel, _FloodFeather;
        float _FloodFloorVisibility;
        struct Input { float2 uv_MainTex; float3 worldPos; float4 color : COLOR; };
        // 수평 수면과 수직 폭포에 같은 밝기를 사용해 방향광 때문에 색이 갈라지지 않게 한다.
        half4 LightingConsistentWater(SurfaceOutput s, half3 lightDir, half atten)
        {
            return half4(s.Albedo, s.Alpha);
        }
        void surf(Input IN, inout SurfaceOutput o)
        {
            // 새 아틀라스 오른쪽 아래의 물 무늬를 아래쪽으로 흘린다.
            float clock = _FlowTime < 0 ? _Time.y : _FlowTime;
            float2 flow = float2(IN.uv_MainTex.x, IN.uv_MainTex.y * 1.7 + clock * _FlowSpeed);
            // 세로 물결 무늬를 진행축(+X)에 맞추고, 샘플 좌표를 빼서 실제 무늬도 +X로 흐르게 한다.
            if (_SurfaceMode > .5) flow = IN.worldPos.zx * _WorldTiling - float2(0, clock * _FlowSpeed * .16);
            // 반복 경계는 반 주기 떨어진 두 무늬를 섞어 가로 줄이 보이지 않게 한다.
            float2 tile = frac(flow);
            if (_SurfaceMode > .5) tile.x = 1-abs(frac(flow.x*.5)*2-1);
            float blend = sin(tile.y*3.14159); blend *= blend;
            float2 uv = float2(.5,0) + (.012 + tile * .976) * .5;
            float2 uv2 = float2(.5,0) + (.012 + float2(tile.x,frac(flow.y+.5)) * .976) * .5;
            fixed3 water = lerp(tex2D(_MainTex, uv2).rgb,tex2D(_MainTex, uv).rgb,blend);
            float edge = smoothstep(0,.07,IN.uv_MainTex.x) * smoothstep(0,.07,1-IN.uv_MainTex.x);
            if (_SurfaceMode > .5) edge = IN.color.a;
            // 수면과 만나는 수직 물막은 아래 45cm에서 부드럽게 섞인다.
            if (_SurfaceMode < .5 && _ImpactMode < .5)
                edge *= smoothstep(_WaterLevel-.08,_WaterLevel+.45,IN.worldPos.y);
            float impact = 0;
            if (_ImpactMode > .5)
            {
                float2 q = IN.uv_MainTex*2-1;
                float spread = abs(q.y);
                float fringe = .06*sin(q.x*23+clock*1.2)+.035*sin(q.x*47-clock*.8);
                float envelope = (1-smoothstep(.36,1,spread+fringe))*(1-smoothstep(.80,1,abs(q.x)));
                // 충돌 부근 거품과 바깥으로 번지는 잔물결을 투명하게 겹친다.
                float wave = pow(.5+.5*sin(spread*23-clock*2.8+q.x*.7),7);
                float grain = saturate(dot(water,float3(.25,.5,.25)));
                impact = (.24+.38*grain+.23*wave)*envelope;
                edge = impact;
                water = lerp(_Color.rgb,float3(.85,.97,.98),.6);
            }
            // 중앙 상단부터 물 공급을 끊는다. 남은 물줄기는 아래로 빠지고, 공급 중단이 양옆으로 번진다.
            if (_GateMode > .5)
            {
                float p = saturate(_OpenProgress);
                float distanceFromCentre = abs(IN.uv_MainTex.x-.5)*2;
                float stopAt = .04 + .50 * pow(distanceFromCentre,1.25);
                float drained = saturate((p-stopAt)/.42);
                float waterTop = 1.035 - drained*1.10;
                float breakup = sin(IN.uv_MainTex.x*73+clock*2)*.012*sin(drained*3.14159);
                // 닫힌 물막은 가장자리를 흐리게 하지 않고 문틀 안쪽까지 겹쳐 틈을 막는다.
                if (_ImpactMode > .5) edge *= 1-smoothstep(.72,1,drained);
                // 문 물막은 수면 아래까지 유지한다. 아래를 투명하게 지우면 낮은 시점에서 문턱이 비친다.
                else edge = 1-smoothstep(waterTop-.018,waterTop+.018,IN.uv_MainTex.y+breakup);
            }
            o.Albedo = water * _Color.rgb;
            o.Emission = 0;
            o.Alpha = _Color.a * edge * _Opacity;
            if (_SurfaceMode < .5)
                o.Alpha *= _FloodVisibility * smoothstep(_FloodLevel-_FloodFeather*.5, _FloodLevel+_FloodFeather*.5, IN.worldPos.y);
            else o.Alpha *= _FloodFloorVisibility;
        }

