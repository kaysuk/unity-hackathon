using UnityEngine;
using UnityEngine.UI;

namespace Hackathon.Menu
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MainMenuBackdropEffects : MaskableGraphic
    {
        private static readonly Rect KitchenWindow = new Rect(0.452f, 0.236f, 0.030f, 0.177f);
        private static readonly Rect DoorGap = new Rect(0.755f, 0.241f, 0.011f, 0.419f);

        // Crack paths use the photographed glass as a coordinate system, independent of the screen size.
        private static readonly Vector2[][] Cracks =
        {
            new[] { new Vector2(0.10f, 0.06f), new Vector2(0.37f, 0.28f), new Vector2(0.43f, 0.44f), new Vector2(0.52f, 0.48f) },
            new[] { new Vector2(0.52f, 0.48f), new Vector2(0.68f, 0.32f), new Vector2(0.79f, 0.29f), new Vector2(0.95f, 0.13f) },
            new[] { new Vector2(0.52f, 0.48f), new Vector2(0.66f, 0.56f), new Vector2(0.84f, 0.56f), new Vector2(0.99f, 0.65f) },
            new[] { new Vector2(0.52f, 0.48f), new Vector2(0.57f, 0.63f), new Vector2(0.66f, 0.78f), new Vector2(0.69f, 0.99f) },
            new[] { new Vector2(0.52f, 0.48f), new Vector2(0.37f, 0.62f), new Vector2(0.32f, 0.76f), new Vector2(0.16f, 0.95f) },
            new[] { new Vector2(0.52f, 0.48f), new Vector2(0.32f, 0.45f), new Vector2(0.18f, 0.50f), new Vector2(0.01f, 0.46f) },
            new[] { new Vector2(0.37f, 0.28f), new Vector2(0.48f, 0.24f), new Vector2(0.59f, 0.28f), new Vector2(0.68f, 0.32f) },
            new[] { new Vector2(0.57f, 0.63f), new Vector2(0.43f, 0.69f), new Vector2(0.37f, 0.62f) },
            new[] { new Vector2(0.66f, 0.78f), new Vector2(0.81f, 0.83f), new Vector2(0.96f, 0.80f) },
        };

        private float weather;
        private float damage;
        private float rainTime;

        internal void SetFrame(float weatherBlend, float damageBlend, float time)
        {
            bool needsMesh = weatherBlend > 0f || weather != weatherBlend || damage != damageBlend;
            weather = weatherBlend;
            damage = damageBlend;
            rainTime = time;
            if (needsMesh)
                SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            if (weather > 0f)
            {
                Rain(mesh, rect, KitchenWindow, 12, 0.12f);
                Rain(mesh, rect, DoorGap, 15, 0.21f);
            }

            if (damage > 0f)
                CrackedGlass(mesh, rect);
        }

        private void Rain(VertexHelper mesh, Rect rect, Rect opening, int count, float speed)
        {
            for (int index = 0; index < count; index++)
            {
                float seed = index + 1;
                float x = Mathf.Lerp(opening.xMin, opening.xMax, Mathf.Repeat(seed * 0.618f, 1f));
                float travel = Mathf.Repeat(seed * 0.381f + rainTime * (speed + seed * 0.003f), 1f);
                float y = Mathf.Lerp(opening.yMin, opening.yMax, travel);
                float bottom = Mathf.Min(opening.yMax, y + 0.010f + Mathf.Repeat(seed * 0.0029f, 0.020f));
                var streak = new Color(0.70f, 0.81f, 0.89f, weather * weather * (0.14f + (index % 4) * 0.047f));
                Line(mesh, InImage(rect, new Vector2(x, y)), InImage(rect, new Vector2(x - 0.0007f, bottom)),
                    rect.height * 0.0009f, streak);
            }
        }

        private void CrackedGlass(VertexHelper mesh, Rect rect)
        {
            float visible = Mathf.Clamp01(damage * 4f);
            Color edge = new Color(0.89f, 0.86f, 0.80f, visible * 0.88f);
            Color shadow = new Color(0.13f, 0.12f, 0.11f, visible * 0.70f);
            float lineWidth = rect.height * 0.00145f;
            Vector2 offset = Vector2.down * lineWidth;
            foreach (Vector2[] crack in Cracks)
            {
                for (int index = 1; index < crack.Length; index++)
                {
                    Vector2 a = InImage(rect, OnGlass(crack[index - 1]));
                    Vector2 b = InImage(rect, OnGlass(crack[index]));
                    Line(mesh, a + offset, b + offset, lineWidth * 1.45f, shadow);
                    Line(mesh, a, b, lineWidth, edge);
                }
            }

            float falling = damage * damage;
            for (int index = 0; index < 5; index++)
            {
                float drift = (index - 2f) * 0.011f;
                Vector2 start = OnGlass(new Vector2(0.35f + index * 0.12f, 0.83f));
                Vector2 shard = start + new Vector2(drift * damage, falling * (0.047f + index * 0.006f));
                Vector2 a = InImage(rect, shard);
                Vector2 b = InImage(rect, shard + new Vector2(0.0048f, 0.0027f));
                Vector2 c = InImage(rect, shard + new Vector2(0.0014f, 0.0070f));
                Triangle(mesh, a, b, c, new Color(0.78f, 0.81f, 0.81f, visible * 0.52f));
                Line(mesh, a, b, lineWidth * 0.9f, edge);
            }
        }

        private static Vector2 OnGlass(Vector2 position)
        {
            Vector2 top = Vector2.Lerp(new Vector2(0.897f, 0.553f), new Vector2(0.944f, 0.564f), position.x);
            Vector2 bottom = Vector2.Lerp(new Vector2(0.882f, 0.667f), new Vector2(0.928f, 0.684f), position.x);
            return Vector2.Lerp(top, bottom, position.y);
        }

        private static Vector2 InImage(Rect rect, Vector2 point)
        {
            return new Vector2(rect.xMin + point.x * rect.width, rect.yMax - point.y * rect.height);
        }

        private static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color32 color)
        {
            Vector2 delta = b - a;
            Vector2 edge = new Vector2(-delta.y, delta.x).normalized * (width * 0.5f);
            int start = mesh.currentVertCount;
            mesh.AddVert(a + edge, color, Vector2.zero);
            mesh.AddVert(b + edge, color, Vector2.zero);
            mesh.AddVert(b - edge, color, Vector2.zero);
            mesh.AddVert(a - edge, color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        private static void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color32 color)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(a, color, Vector2.zero);
            mesh.AddVert(b, color, Vector2.zero);
            mesh.AddVert(c, color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
        }
    }
}
