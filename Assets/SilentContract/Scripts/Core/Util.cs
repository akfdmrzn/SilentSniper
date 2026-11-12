using System.Collections.Generic;
using UnityEngine;

namespace SilentContract
{
    public static class Layers
    {
        public const int Default = 0;
        public const int IgnoreRaycast = 2;
        public const int Water = 4;
        public const int UI = 5;
        public const int Body = 8;        // ragdoll / hit-zone colliders of characters
        public const int ViewModel = 9;   // first person rifle (rendered by the main camera, never hit)
        public const int Reflect = 10;    // world geometry that also shows up in the water reflection (quay edge, boats, backdrop)

        /// <summary>What bullets can hit.</summary>
        public static int ShotMask => (1 << Default) | (1 << Reflect) | (1 << Body);
        /// <summary>World geometry only (line of sight, ground).</summary>
        public static int WorldMask => (1 << Default) | (1 << Reflect);
        /// <summary>Everything the environment reflection captures.</summary>
        public static int EnvMask => (1 << Default) | (1 << Reflect);
    }

    public static class Util
    {
        public static Color Hex(string hex, float a = 1f)
        {
            if (ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c)) { c.a = a; return c; }
            return Color.magenta;
        }

        public static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
        public static float EaseInOut(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayer(t.gameObject, layer);
        }

        public static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        static Mesh quad;
        /// <summary>A unit quad facing +Z (no collider, unlike GameObject.CreatePrimitive).</summary>
        public static Mesh Quad
        {
            get
            {
                if (quad != null) return quad;
                quad = new Mesh { name = "sc_quad" };
                quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0) };
                quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
                quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                quad.RecalculateNormals();
                quad.RecalculateBounds();
                return quad;
            }
        }

        /// <summary>Flat grid in the XZ plane, centred at origin. Used for water / ground decals.</summary>
        public static Mesh Grid(float sizeX, float sizeZ, int nx, int nz, float uvScale)
        {
            var m = new Mesh { name = "sc_grid" };
            if ((nx + 1) * (nz + 1) > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            var v = new Vector3[(nx + 1) * (nz + 1)];
            var uv = new Vector2[v.Length];
            for (int z = 0; z <= nz; z++)
                for (int x = 0; x <= nx; x++)
                {
                    int i = z * (nx + 1) + x;
                    float px = (x / (float)nx - 0.5f) * sizeX, pz = (z / (float)nz - 0.5f) * sizeZ;
                    v[i] = new Vector3(px, 0, pz);
                    uv[i] = new Vector2(px, pz) * uvScale;
                }
            var t = new int[nx * nz * 6];
            int k = 0;
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    int i = z * (nx + 1) + x;
                    t[k++] = i; t[k++] = i + nx + 1; t[k++] = i + 1;
                    t[k++] = i + 1; t[k++] = i + nx + 1; t[k++] = i + nx + 2;
                }
            m.vertices = v; m.uv = uv; m.triangles = t;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        public static GameObject QuadObject(string name, Transform parent, Material mat, int layer = Layers.IgnoreRaycast)
        {
            var go = new GameObject(name);
            go.layer = layer;
            if (parent != null) go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Quad;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        /// <summary>Distance-independent angular size test: is point p inside the camera's view cone (degrees)?</summary>
        public static float AngleTo(Transform cam, Vector3 p) => Vector3.Angle(cam.forward, p - cam.position);

        public static string Meters(float m) => m < 0 ? "---" : Mathf.RoundToInt(m) + " m";

        public static void Vibrate()
        {
#if UNITY_IOS || UNITY_ANDROID
            if (Save.Vibration) Handheld.Vibrate();
#endif
        }

        public static T GetOrAdd<T>(this GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        public static List<T> Shuffled<T>(IList<T> src, System.Random rnd)
        {
            var l = new List<T>(src);
            for (int i = l.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (l[i], l[j]) = (l[j], l[i]);
            }
            return l;
        }
    }
}
