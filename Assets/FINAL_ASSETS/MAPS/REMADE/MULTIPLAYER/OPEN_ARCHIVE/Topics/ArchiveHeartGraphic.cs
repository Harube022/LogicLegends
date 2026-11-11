using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Brass-set ruby HUD artwork. Health remains owned by the existing HealthManager.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ArchiveHeartGraphic : MaskableGraphic
    {
        public bool empty;
        const int Segments = 64;
        static readonly Vector2[] Outline = MakeOutline();
        static readonly Vector2 HeartCenter = new Vector2(.5f, .53f);

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var rect = GetPixelAdjustedRect();
            if (empty)
            {
                // The lost-heart socket is an open outline: the world remains visible inside it.
                for (int i = 0; i < Segments; i++)
                {
                    var a = HeartCenter + (Outline[i] - HeartCenter) * .955f;
                    var b = HeartCenter + (Outline[(i + 1) % Segments] - HeartCenter) * .955f;
                    Stroke(mesh, rect, a, b, .025f, new Color(.76f, .59f, .34f, .82f));
                }
                Stroke(mesh, rect, new Vector2(.49f, .69f), new Vector2(.43f, .54f), .016f, new Color(.76f, .59f, .34f, .7f));
                Stroke(mesh, rect, new Vector2(.43f, .54f), new Vector2(.56f, .47f), .016f, new Color(.76f, .59f, .34f, .7f));
                Stroke(mesh, rect, new Vector2(.56f, .47f), new Vector2(.48f, .31f), .016f, new Color(.76f, .59f, .34f, .7f));
                return;
            }
            Fill(mesh, rect, 1.02f, new Vector2(0, -.025f), new Color(.30f, .025f, .07f, .22f), new Color(.30f, .025f, .07f, .22f), new Color(.30f, .025f, .07f, .22f));
            Fill(mesh, rect, 1, Vector2.zero, new Color(.14f, .075f, .055f), new Color(.14f, .075f, .055f), new Color(.14f, .075f, .055f));
            Fill(mesh, rect, .955f, Vector2.zero,
                empty ? new Color(.50f, .42f, .28f) : new Color(1, .85f, .54f),
                empty ? new Color(.28f, .24f, .18f) : new Color(.52f, .31f, .12f),
                empty ? new Color(.43f, .36f, .24f) : new Color(.84f, .62f, .29f));
            Fill(mesh, rect, .855f, Vector2.zero,
                empty ? new Color(.055f, .115f, .125f) : new Color(.42f, .055f, .12f),
                empty ? new Color(.025f, .065f, .075f) : new Color(.25f, .025f, .075f),
                empty ? new Color(.035f, .08f, .09f) : new Color(.44f, .045f, .12f));
            Fill(mesh, rect, .78f, new Vector2(0, .008f),
                empty ? new Color(.065f, .13f, .14f) : new Color(1, .30f, .39f),
                empty ? new Color(.035f, .075f, .085f) : new Color(.61f, .035f, .14f),
                empty ? new Color(.045f, .095f, .105f) : new Color(.94f, .13f, .25f));
            if (empty)
            {
                // Engraving, rather than a missing slot, keeps lost hearts countable.
                Stroke(mesh, rect, new Vector2(.49f, .69f), new Vector2(.43f, .54f), .018f, new Color(.32f, .40f, .39f, .7f));
                Stroke(mesh, rect, new Vector2(.43f, .54f), new Vector2(.56f, .47f), .018f, new Color(.32f, .40f, .39f, .7f));
                Stroke(mesh, rect, new Vector2(.56f, .47f), new Vector2(.48f, .31f), .018f, new Color(.32f, .40f, .39f, .7f));
            }
            else
            {
                Triangle(mesh, rect, new Vector2(.23f, .60f), new Vector2(.49f, .52f), new Vector2(.50f, .24f), new Color(1, .48f, .48f, .16f));
                Triangle(mesh, rect, new Vector2(.49f, .52f), new Vector2(.77f, .61f), new Vector2(.50f, .24f), new Color(.30f, 0, .07f, .13f));
                Vector2 previous = Bezier(0);
                for (int i = 1; i <= 16; i++)
                {
                    float t = i / 16f; var next = Bezier(t);
                    Stroke(mesh, rect, previous, next, .019f * (1 - t * .55f), new Color(1, .87f, .76f, .75f * (1 - t * .7f)));
                    previous = next;
                }
                // A second restrained glint gives the ruby depth at mobile size.
                Stroke(mesh, rect, new Vector2(.66f, .76f), new Vector2(.72f, .73f), .022f, new Color(1, .73f, .67f, .58f));
            }
        }
        static Vector2[] MakeOutline()
        {
            var points = new Vector2[Segments];
            for (int i = 0; i < Segments; i++)
            {
                float t = i * Mathf.PI * 2 / Segments, sine = Mathf.Sin(t);
                float x = 16 * sine * sine * sine;
                float y = 13 * Mathf.Cos(t) - 5 * Mathf.Cos(2 * t) - 2 * Mathf.Cos(3 * t) - Mathf.Cos(4 * t);
                points[i] = new Vector2(.5f + x / 35, .5f + (y + 2.3f) / 35);
            }
            return points;
        }
        static Vector2 Bezier(float t)
        {
            float s = 1 - t;
            return s * s * s * new Vector2(.245f, .665f) + 3 * s * s * t * new Vector2(.22f, .81f) +
                3 * s * t * t * new Vector2(.39f, .84f) + t * t * t * new Vector2(.43f, .745f);
        }
        Vector2 Pixel(Rect r, Vector2 point) => new Vector2(r.xMin + point.x * r.width, r.yMin + point.y * r.height);
        void Fill(VertexHelper mesh, Rect rect, float scale, Vector2 offset, Color top, Color bottom, Color center)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(Pixel(rect, HeartCenter + offset), center * color, Vector2.zero);
            foreach (var point in Outline)
            {
                var p = HeartCenter + (point - HeartCenter) * scale + offset;
                mesh.AddVert(Pixel(rect, p), Color.Lerp(bottom, top, Mathf.InverseLerp(.08f, .9f, point.y)) * color, Vector2.zero);
            }
            for (int i = 0; i < Segments; i++) mesh.AddTriangle(first, first + i + 1, first + (i + 1) % Segments + 1);
        }
        void Triangle(VertexHelper mesh, Rect r, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            int n = mesh.currentVertCount; tint *= color;
            mesh.AddVert(Pixel(r, a), tint, Vector2.zero); mesh.AddVert(Pixel(r, b), tint, Vector2.zero); mesh.AddVert(Pixel(r, c), tint, Vector2.zero);
            mesh.AddTriangle(n, n + 1, n + 2);
        }
        void Stroke(VertexHelper mesh, Rect r, Vector2 a, Vector2 b, float width, Color tint)
        {
            a = Pixel(r, a); b = Pixel(r, b); var line = b - a;
            var side = new Vector2(-line.y, line.x).normalized * Mathf.Min(r.width, r.height) * width / 2;
            int n = mesh.currentVertCount; tint *= color;
            mesh.AddVert(a - side, tint, Vector2.zero); mesh.AddVert(b - side, tint, Vector2.zero);
            mesh.AddVert(b + side, tint, Vector2.zero); mesh.AddVert(a + side, tint, Vector2.zero);
            mesh.AddTriangle(n, n + 1, n + 2); mesh.AddTriangle(n, n + 2, n + 3);
        }
    }
}
