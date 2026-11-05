using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UntitledPoolGame.Pool
{
    // The glowing wall on the edge of a Play Anywhere course's play area,
    // worked out from its path (PlayAnywhereHole). Only a picture: no collider,
    // a ball goes through it and is then out of bounds as usual.
    //
    // The play area is a union of stretches and round node ends, so its edge
    // is what's left of their outlines once the parts inside another stretch
    // are dropped: each outline is cut in short pieces, and a piece stays if
    // just past it is out of the play area.
    //
    // While a player aims, the wall dithers away near their camera, not to
    // hide the shot: the shader needs to know for the camera being drawn, and
    // the screen is split, so it's told per camera.
    public static class PlayAnywhereCourseWall
    {
        private const float PieceLength = 0.1f;
        private const float OutsideProbe = 0.02f;
        private static readonly int AimingId = Shader.PropertyToID("_PlayAnywhereWallAiming");
        // Each camera's player, looked up once; null for a Scene view.
        private static readonly Dictionary<Camera, LocalPoolAimController> aimOfCamera = new Dictionary<Camera, LocalPoolAimController>();

        private static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!aimOfCamera.TryGetValue(camera, out LocalPoolAimController aim))
            {
                aim = camera.GetComponentInParent<LocalPoolAimController>(true);
                aimOfCamera[camera] = aim;
            }
            Shader.SetGlobalFloat(AimingId, aim != null && aim.IsAiming ? 1f : 0f);
        }

        // The wall of this hole, made once when it's first shown. Null when
        // it has none.
        public static Transform Build(PlayAnywhereHole hole, PlayAnywhereSettings settings)
        {
            if (settings.courseWallMaterial == null || settings.courseWallHeight <= 0f || hole.PathWidth <= 0f) return null;

            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;

            var builder = new Builder(hole, settings.courseWallHeight);
            hole.GetNode(0, out Vector3 a, out float widthA);
            builder.AddCircle(a, widthA * 0.5f);
            for (int i = 1; i < hole.NodeCount; i++)
            {
                hole.GetNode(i, out Vector3 b, out float widthB);
                builder.AddCircle(b, widthB * 0.5f);
                Vector3 along = b - a;
                along.y = 0f;
                if (along.sqrMagnitude > 1e-6f)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, along.normalized);
                    builder.AddLine(a + side * (widthA * 0.5f), b + side * (widthB * 0.5f), side);
                    builder.AddLine(a - side * (widthA * 0.5f), b - side * (widthB * 0.5f), -side);
                }
                a = b;
                widthA = widthB;
            }

            var wall = new GameObject("Mur du parcours");
            wall.AddComponent<MeshFilter>().sharedMesh = builder.Build();
            var r = wall.AddComponent<MeshRenderer>();
            r.sharedMaterial = settings.courseWallMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            // Vertices are in world space; parented keeping its place: it goes
            // with the hole, whatever the hole's scale.
            wall.transform.SetParent(hole.transform, true);
            return wall.transform;
        }

        private sealed class Builder
        {
            private readonly PlayAnywhereHole hole;
            private readonly float height;
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector2> uvs = new List<Vector2>();
            private readonly List<int> triangles = new List<int>();
            private float walked;

            public Builder(PlayAnywhereHole hole, float height)
            {
                this.hole = hole;
                this.height = height;
            }

            public void AddCircle(Vector3 centre, float radius)
            {
                int pieces = Mathf.Max(12, Mathf.CeilToInt(2f * Mathf.PI * radius / PieceLength));
                Vector3 from = centre + Direction(0, pieces) * radius;
                for (int i = 0; i < pieces; i++)
                {
                    Vector3 to = centre + Direction(i + 1, pieces) * radius;
                    AddPiece(from, to, Direction(i * 2 + 1, pieces * 2));
                    from = to;
                }
            }

            private static Vector3 Direction(int index, int pieces)
            {
                float angle = index * 2f * Mathf.PI / pieces;
                return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }

            public void AddLine(Vector3 a, Vector3 b, Vector3 outward)
            {
                Vector3 flat = b - a;
                flat.y = 0f;
                int pieces = Mathf.Max(1, Mathf.CeilToInt(flat.magnitude / PieceLength));
                Vector3 from = a;
                for (int i = 1; i <= pieces; i++)
                {
                    Vector3 to = Vector3.Lerp(a, b, (float)i / pieces);
                    AddPiece(from, to, outward);
                    from = to;
                }
            }

            private void AddPiece(Vector3 from, Vector3 to, Vector3 outward)
            {
                float start = walked;
                walked += Vector3.Distance(from, to);
                if (hole.InPlayArea((from + to) * 0.5f + outward * OutsideProbe)) return;

                int i = vertices.Count;
                vertices.Add(from);
                vertices.Add(to);
                vertices.Add(to + Vector3.up * height);
                vertices.Add(from + Vector3.up * height);
                uvs.Add(new Vector2(start, 0f));
                uvs.Add(new Vector2(walked, 0f));
                uvs.Add(new Vector2(walked, 1f));
                uvs.Add(new Vector2(start, 1f));
                triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 1);
                triangles.Add(i); triangles.Add(i + 3); triangles.Add(i + 2);
            }

            public Mesh Build()
            {
                var mesh = new Mesh { name = "Mur du parcours" };
                if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
