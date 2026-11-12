using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    /// <summary>
    /// A flag on a pole near the target area: it streams out with the wind of the moment (direction and strength, gusts
    /// included), so the wind can be read through the scope instead of from the panel. A few are put up for every
    /// contract, where the nest can see them and nobody walks.
    /// </summary>
    public class WindFlag : MonoBehaviour
    {
        const int Cols = 9, Rows = 4;
        const float W = 1.25f, H = 0.8f, Pole = 4.6f;
        Mesh mesh;
        Vector3[] verts;
        Transform cloth;
        float phase, yawSm;
        static Material poleMat;
        static readonly Material[] clothMats = new Material[3];

        static Material Mat(Color c, float gloss, float metal)
        {
            var sh = Shader.Find("Standard");
            var m = new Material(sh != null ? sh : Shader.Find("Diffuse")) { color = c };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            return m;
        }

        public static WindFlag Make(Transform parent, Vector3 ground, int style)
        {
            if (poleMat == null) poleMat = Mat(new Color(0.62f, 0.64f, 0.66f), 0.55f, 0.8f);
            if (clothMats[0] == null)
            {
                clothMats[0] = Mat(new Color(0.78f, 0.07f, 0.08f), 0.15f, 0f);   // red
                clothMats[1] = Mat(new Color(0.92f, 0.9f, 0.86f), 0.15f, 0f);    // white
                clothMats[2] = Mat(new Color(0.95f, 0.45f, 0.08f), 0.2f, 0f);    // orange (windsock colour)
            }
            var go = new GameObject("WindFlag");
            go.transform.SetParent(parent, false);
            go.transform.position = ground;
            var f = go.AddComponent<WindFlag>();
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            Object.Destroy(pole.GetComponent<Collider>());
            pole.transform.SetParent(go.transform, false);
            pole.transform.localPosition = new Vector3(0f, Pole * 0.5f, 0f);
            pole.transform.localScale = new Vector3(0.07f, Pole * 0.5f, 0.07f);
            pole.GetComponent<MeshRenderer>().sharedMaterial = poleMat;
            var c = new GameObject("Cloth");
            c.transform.SetParent(go.transform, false);
            c.transform.localPosition = new Vector3(0f, Pole - 0.05f, 0f);
            f.cloth = c.transform;
            f.Build(c, clothMats[Mathf.Abs(style) % clothMats.Length]);
            f.phase = Random.value * 10f;
            return f;
        }

        void Build(GameObject c, Material m)
        {
            mesh = new Mesh { name = "flag" };
            mesh.MarkDynamic();
            verts = new Vector3[Cols * Rows * 2];
            var uv = new Vector2[verts.Length];
            var tris = new List<int>();
            for (int side = 0; side < 2; side++)
                for (int r = 0; r < Rows; r++)
                    for (int k = 0; k < Cols; k++)
                    {
                        int i = side * Cols * Rows + r * Cols + k;
                        uv[i] = new Vector2(k / (Cols - 1f), r / (Rows - 1f));
                        if (k < Cols - 1 && r < Rows - 1)
                        {
                            int a = i, b = i + 1, d = i + Cols, e = i + Cols + 1;
                            if (side == 0) { tris.Add(a); tris.Add(d); tris.Add(b); tris.Add(b); tris.Add(d); tris.Add(e); }
                            else { tris.Add(a); tris.Add(b); tris.Add(d); tris.Add(b); tris.Add(e); tris.Add(d); }
                        }
                    }
            Shape(0f, 0f);
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(new Vector3(W * 0.5f, -H * 0.5f, 0f), new Vector3(W * 2f, H * 2f, W * 2f));
            c.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = c.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>The cloth along local +x from the pole: limp and hanging at no wind, flat out and rippling in a strong one.</summary>
        void Shape(float strength, float t)
        {
            float lift = Mathf.Clamp01(strength / 7f);                 // 0 hangs, 1 flies straight out
            float amp = 0.05f + 0.16f * Mathf.Clamp01(strength / 5f);
            float freq = 1.5f + strength * 0.9f;
            for (int side = 0; side < 2; side++)
                for (int r = 0; r < Rows; r++)
                    for (int k = 0; k < Cols; k++)
                    {
                        float u = k / (Cols - 1f), v = r / (Rows - 1f);
                        float len = u * W;
                        // hanging: the cloth falls down the pole; flying: straight out
                        float ang = Mathf.Lerp(-1.35f, -0.05f, lift) - 0.1f * u * (1f - lift);
                        float x = Mathf.Cos(ang) * len;
                        float y = Mathf.Sin(ang) * len - v * H * Mathf.Lerp(1f, 0.92f, lift);
                        float z = Mathf.Sin(t * freq - u * 5.5f + v * 0.6f) * amp * u;
                        verts[side * Cols * Rows + r * Cols + k] = new Vector3(x, y, z);
                    }
        }

        void Update()
        {
            var sn = Sniper.I;
            if (sn == null || mesh == null) return;
            var w = new Vector3(sn.Wind.x, 0f, sn.Wind.z);
            float s = w.magnitude;
            // the cloth points where the air goes
            if (s > 0.05f)
            {
                float want = Mathf.Atan2(w.x, w.z) * Mathf.Rad2Deg - 90f;
                yawSm = Mathf.LerpAngle(yawSm, want, 1f - Mathf.Exp(-Time.deltaTime * 2.5f));
                cloth.rotation = Quaternion.Euler(0f, yawSm, 0f);
            }
            // only while someone could be looking (close enough to see it ripple)
            var cam = sn.Cam;
            if (cam != null && (cam.transform.position - transform.position).sqrMagnitude > 700f * 700f) return;
            phase += Time.deltaTime;
            Shape(s, phase);
            mesh.vertices = verts;
            mesh.RecalculateNormals();
        }

        /// <summary>
        /// Puts up to <paramref name="count"/> flags 7-24 m around <paramref name="focus"/>: on the same ground level, in
        /// sight of the nest, clear of walls, of the marked spots and of everyone's walks.
        /// </summary>
        public static int Spawn(Transform parent, Vector3 focus, Vector3 eye, List<Vector3[]> routes, IEnumerable<Vector3> spots, int count)
        {
            var placed = new List<Vector3>();
            float a0 = Random.value * 360f;
            int style = Random.Range(0, 3);
            foreach (float r in new[] { 9f, 14f, 7f, 19f, 24f })
                for (int i = 0; i < 12 && placed.Count < count; i++)
                {
                    float a = (a0 + i * 30f + r * 7f) * Mathf.Deg2Rad;
                    var p = focus + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
                    if (!Physics.Raycast(p + Vector3.up * 4f, Vector3.down, out var g, 9f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                    if (Mathf.Abs(g.point.y - focus.y) > 2.5f || g.normal.y < 0.92f || g.point.y < Level.WaterY + 0.4f) continue;
                    var gp = g.point;
                    bool ok = true;
                    foreach (var q in placed) if ((q - gp).sqrMagnitude < 8f * 8f) { ok = false; break; }
                    if (!ok) continue;
                    if (Physics.CheckCapsule(gp + Vector3.up * 0.35f, gp + Vector3.up * (Pole + 0.2f), 0.35f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                    // the cloth needs room on every side it may swing to
                    if (Physics.CheckSphere(gp + Vector3.up * (Pole - 0.4f), W + 0.2f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                    foreach (var sp in spots) if (Flat(sp - gp) < 2.2f) { ok = false; break; }
                    if (!ok) continue;
                    if (routes != null)
                        foreach (var rt in routes)
                        {
                            if (rt == null) continue;
                            for (int k = 0; k + 1 < rt.Length && ok; k++)
                                if (SegDist(gp, rt[k], rt[k + 1]) < 1.8f) ok = false;
                            if (rt.Length == 1 && Flat(rt[0] - gp) < 1.8f) ok = false;
                            if (!ok) break;
                        }
                    if (!ok) continue;
                    // the nest must see the cloth
                    var top = gp + Vector3.up * (Pole - 0.4f);
                    if (Physics.Linecast(eye, top, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                    Make(parent, gp, style + placed.Count);
                    placed.Add(gp);
                }
            return placed.Count;
        }

        static float Flat(Vector3 d) { d.y = 0f; return d.magnitude; }

        static float SegDist(Vector3 p, Vector3 a, Vector3 b)
        {
            p.y = a.y = b.y = 0f;
            var ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }
    }
}
