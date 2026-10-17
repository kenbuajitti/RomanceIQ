using UnityEngine;
using UnityEngine.UI;

namespace RomanceIQ
{
    // A circular outlined button with a shaft-and-chevron down arrow.
    // Geometry is generated locally; no sprite import or font glyph is needed.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RomanceIQDownArrowGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = rectTransform.rect;
            Vector2 centre = bounds.center;
            float radius = Mathf.Min(bounds.width, bounds.height) * .47f;
            if (radius <= 0) return;
            const int segments = 64;
            Color32 fill = new Color(.055f, .045f, .065f, .98f);
            Color32 border = new Color(.42f, .40f, .44f, 1);
            mesh.AddVert(centre, fill, Vector2.zero);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                mesh.AddVert(centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, fill, Vector2.zero);
            }
            for (int i = 0; i < segments; i++) mesh.AddTriangle(0, i + 1, (i + 1) % segments + 1);
            float scale = radius / 20f;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                float b = (i + 1) * Mathf.PI * 2 / segments;
                Stroke(mesh, centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (radius - scale),
                    centre + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * (radius - scale), scale * .55f, border);
            }
            Stroke(mesh, centre + new Vector2(0, 9) * scale, centre + new Vector2(0, -8) * scale, scale, color);
            Stroke(mesh, centre + new Vector2(-7, -1) * scale, centre + new Vector2(0, -8) * scale, scale, color);
            Stroke(mesh, centre + new Vector2(0, -8) * scale, centre + new Vector2(7, -1) * scale, scale, color);
        }

        static void Stroke(VertexHelper mesh, Vector2 start, Vector2 end, float halfWidth, Color32 tint)
        {
            Vector2 direction = (end - start).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * halfWidth;
            int index = mesh.currentVertCount;
            mesh.AddVert(start - normal, tint, Vector2.zero);
            mesh.AddVert(start + normal, tint, Vector2.zero);
            mesh.AddVert(end + normal, tint, Vector2.zero);
            mesh.AddVert(end - normal, tint, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
