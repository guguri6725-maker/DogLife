using UnityEngine;

namespace DogLife
{
    // 프리팹 Hierarchy에 배치한 빛줄기와 입자만 평가합니다. 합성 결과 지급에는 관여하지 않습니다.
    public sealed class WeaponFusionEnergy : MonoBehaviour
    {
        public LineRenderer leftStream, rightStream, spiralA, spiralB, revealRing, crown;
        public ParticleSystem embers, completionBurst;
        bool burstPlayed;
        float nextEmber;
        Color tint;

        static float Ease(float a, float b, float t) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, t));

        public void Begin(Color color)
        {
            tint = color; burstPlayed = false; nextEmber = 0;
            foreach (var particles in new[] { embers, completionBurst })
                if (particles)
                {
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    particles.Play();
                }
        }

        public void Sample(float t, float clock, float height, float radius, float size, Vector3 left, Vector3 right)
        {
            float gather = Ease(.15f, .28f, t) * (1 - Ease(.50f, .65f, t));
            float forge = Ease(.31f, .48f, t) * (1 - Ease(.77f, .88f, t));
            Vector3 center = Vector3.up * height;
            Stream(leftStream, left, center, clock, gather, 0);
            Stream(rightStream, right, center, clock, gather, Mathf.PI);
            Spiral(spiralA, center, radius, size, clock, t, forge, 0);
            Spiral(spiralB, center, radius, size, clock, t, forge, Mathf.PI);

            float reveal = Ease(.52f, .84f, t);
            Circle(revealRing, center + Vector3.up * Mathf.Lerp(-size * .59f, size * .59f, reveal),
                size * .44f, clock * 1.8f, Ease(.49f, .56f, t) * (1 - Ease(.82f, .91f, t)), false);
            // 완성 후에는 무기를 가리지 않는 얇은 후광만 남깁니다.
            Circle(crown, center + Vector3.forward * .28f, size * .67f,
                -clock * .16f, Ease(.79f, .92f, t) * .30f, true);

            if (embers && clock >= nextEmber && t > .16f && t < .82f)
            {
                nextEmber = clock + .028f;
                for (int i = 0; i < 3; i++)
                {
                    float a = clock * 8 + i * 2.0944f;
                    Vector3 offset = new Vector3(Mathf.Cos(a), .25f * Mathf.Sin(a * 2), Mathf.Sin(a)) *
                        Mathf.Lerp(radius, .25f, reveal);
                    Emit(embers, center + offset, -offset * .65f + Vector3.up * .22f,
                        Color.Lerp(tint, Color.white, .5f), .025f + i * .012f, .65f);
                }
            }
            if (!burstPlayed && t >= .80f)
            {
                burstPlayed = true;
                if (completionBurst)
                    for (int i = 0; i < 64; i++)
                    {
                        float a = i * 2.399963f, y = 1 - 2 * ((i + .5f) / 64);
                        float horizontal = Mathf.Sqrt(1 - y * y);
                        Vector3 direction = new Vector3(Mathf.Cos(a) * horizontal, y, Mathf.Sin(a) * horizontal);
                        Emit(completionBurst, center + direction * .12f, direction * (1.15f + i % 5 * .18f),
                            Color.Lerp(tint, new Color(1, .88f, .57f), (i % 4) / 3f),
                            .025f + i % 3 * .018f, .55f + i % 7 * .09f);
                    }
            }
        }

        void Style(LineRenderer line, float alpha, float width)
        {
            if (!line) return;
            line.enabled = alpha > .002f; line.useWorldSpace = false;
            line.widthMultiplier = width;
            var start = Color.Lerp(tint, Color.white, .55f); start.a = alpha;
            var end = tint; end.a = alpha * .35f;
            line.startColor = start; line.endColor = end;
        }

        void Stream(LineRenderer line, Vector3 origin, Vector3 center, float clock, float alpha, float phase)
        {
            if (!line) return;
            Style(line, alpha * .9f, .055f); line.positionCount = 49;
            for (int i = 0; i < 49; i++)
            {
                float u = i / 48f, curl = Mathf.Sin(u * Mathf.PI) * .26f;
                Vector3 p = Vector3.Lerp(origin, center, u);
                p.y += Mathf.Sin(u * Mathf.PI) * .55f + Mathf.Sin(u * 13 - clock * 8 + phase) * curl;
                p.z += Mathf.Cos(u * 13 - clock * 8 + phase) * curl;
                line.SetPosition(i, p);
            }
        }

        void Spiral(LineRenderer line, Vector3 center, float radius, float size, float clock, float t, float alpha, float phase)
        {
            if (!line) return;
            Style(line, alpha * .75f, .035f); line.positionCount = 81;
            float compression = Ease(.42f, .71f, t);
            for (int i = 0; i < 81; i++)
            {
                float u = i / 80f, a = phase + u * Mathf.PI * 4 - clock * 5;
                float r = Mathf.Lerp(radius * .75f, size * .23f, compression) * Mathf.Sin(u * Mathf.PI);
                line.SetPosition(i, center + new Vector3(Mathf.Cos(a) * r, (u - .5f) * size * 1.18f, Mathf.Sin(a) * r));
            }
        }

        void Circle(LineRenderer line, Vector3 center, float radius, float turn, float alpha, bool vertical)
        {
            if (!line) return;
            Style(line, alpha, vertical ? .022f : .038f); line.positionCount = 97;
            for (int i = 0; i < 97; i++)
            {
                float a = i * Mathf.PI * 2 / 96 + turn;
                float r = radius * (vertical && i % 8 == 0 ? 1.035f : 1);
                line.SetPosition(i, center + (vertical ?
                    new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0) :
                    new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r)));
            }
        }

        void Emit(ParticleSystem particles, Vector3 position, Vector3 velocity, Color color, float size, float life)
        {
            var emission = new ParticleSystem.EmitParams
            {
                position = transform.TransformPoint(position),
                velocity = transform.TransformDirection(velocity),
                startColor = color, startSize = size, startLifetime = life
            };
            particles.Emit(emission, 1);
        }

        public void Stop()
        {
            foreach (var line in new[] { leftStream, rightStream, spiralA, spiralB, revealRing, crown })
                if (line) line.enabled = false;
            foreach (var particles in new[] { embers, completionBurst })
                if (particles) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        void OnDisable() { Stop(); }
    }
}
