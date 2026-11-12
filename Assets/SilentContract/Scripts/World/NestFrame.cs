using UnityEngine;

namespace SilentContract
{
    /// <summary>
    /// What is round the shooter at a nest other than the main roof: at a window, the wall with its opening, the sill
    /// the rifle rests on and a curtain pulled aside; in a crane cab, its steel frame and glass edge; on a slope, a few
    /// rocks to lie behind. Built round the eye, facing the nest's direction (nothing collides: it is only what the
    /// player sees).
    /// </summary>
    public static class NestFrame
    {
        static Material wallMat, sillMat, curtainMat, steelMat, rockMat;

        static Material Mat(Color c, float gloss, float metal)
        {
            var sh = Shader.Find("Standard");
            var m = new Material(sh != null ? sh : Shader.Find("Diffuse")) { color = c };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            return m;
        }

        static void Mats()
        {
            if (wallMat != null) return;
            wallMat = Mat(new Color(0.78f, 0.74f, 0.68f), 0.1f, 0f);
            sillMat = Mat(new Color(0.42f, 0.3f, 0.2f), 0.35f, 0f);
            curtainMat = Mat(new Color(0.5f, 0.12f, 0.1f), 0.05f, 0f);
            steelMat = Mat(new Color(0.85f, 0.55f, 0.12f), 0.45f, 0.6f);
            rockMat = Mat(new Color(0.5f, 0.49f, 0.47f), 0.12f, 0f);
        }

        static GameObject Box(Transform parent, Vector3 localPos, Vector3 size, Material m, string name = "Box")
        {
            // rocks are rounded (a squashed sphere reads as stone; a cube read as a grey placeholder block)
            var b = GameObject.CreatePrimitive(name == "Rock" ? PrimitiveType.Sphere : PrimitiveType.Cube);
            b.name = name;
            Object.Destroy(b.GetComponent<Collider>());
            b.transform.SetParent(parent, false);
            b.transform.localPosition = localPos;
            b.transform.localScale = size;
            var r = b.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return b;
        }

        /// <summary>Builds the surroundings of <paramref name="n"/> under <paramref name="parent"/> (null for the main
        /// nest, which has its own roof).</summary>
        public static Transform Build(Transform parent, LayoutNest n)
        {
            if (n == null || string.IsNullOrEmpty(n.kind) || n.id == "main") return null;
            Mats();
            var root = new GameObject("NestFrame_" + n.id).transform;
            root.SetParent(parent, false);
            root.position = new Vector3(n.x, n.y, n.z);
            root.rotation = Quaternion.Euler(0f, n.yaw, 0f);
            switch (n.kind)
            {
                case "window":
                {
                    // the opening 0.6 m ahead of the eye: 1.7 wide, from the sill (0.4 below the eye) to 1.1 above it
                    const float d = 0.6f, w = 1.7f, sill = -0.42f, top = 1.1f, t = 0.3f;
                    Box(root, new Vector3(-(w / 2 + 1.6f), 0.3f, d), new Vector3(3.2f, 4.6f, t), wallMat, "WallL");
                    Box(root, new Vector3(w / 2 + 1.6f, 0.3f, d), new Vector3(3.2f, 4.6f, t), wallMat, "WallR");
                    Box(root, new Vector3(0f, top + 0.6f, d), new Vector3(w + 0.02f, 1.2f, t), wallMat, "Lintel");
                    Box(root, new Vector3(0f, sill - 0.9f, d), new Vector3(w + 0.02f, 1.8f, t), wallMat, "Under");
                    Box(root, new Vector3(0f, sill + 0.03f, d - 0.1f), new Vector3(w + 0.2f, 0.06f, 0.5f), sillMat, "Sill");
                    // side reveals back to the room (the wall's thickness), a curtain pulled to one side
                    Box(root, new Vector3(-w / 2 - 0.02f, (sill + top) / 2f, d - 0.3f), new Vector3(0.05f, top - sill, 0.6f), wallMat, "RevealL");
                    Box(root, new Vector3(w / 2 + 0.02f, (sill + top) / 2f, d - 0.3f), new Vector3(0.05f, top - sill, 0.6f), wallMat, "RevealR");
                    Box(root, new Vector3(-w / 2 + 0.18f, (sill + top) / 2f + 0.05f, d - 0.35f), new Vector3(0.3f, top - sill + 0.2f, 0.05f), curtainMat, "Curtain");
                    // the room behind (dark): ceiling and floor so a glance down or up is not into the void
                    Box(root, new Vector3(0f, top + 0.15f, d - 1.6f), new Vector3(4f, 0.05f, 3.4f), wallMat, "Ceiling");
                    Box(root, new Vector3(0f, -1.45f, d - 1.6f), new Vector3(4f, 0.05f, 3.4f), sillMat, "Floor");
                    break;
                }
                case "crane":
                {
                    // a cab of steel posts round the shooter, open to the front
                    const float hw = 1.1f, f = 1.0f;
                    foreach (var x in new[] { -hw, hw })
                    {
                        Box(root, new Vector3(x, 0.2f, f), new Vector3(0.12f, 2.6f, 0.12f), steelMat, "Post");
                        Box(root, new Vector3(x, 0.2f, -0.9f), new Vector3(0.12f, 2.6f, 0.12f), steelMat, "Post");
                    }
                    Box(root, new Vector3(0f, 1.45f, 0.05f), new Vector3(2.4f, 0.1f, 2.1f), steelMat, "Roof");
                    Box(root, new Vector3(0f, -0.55f, f), new Vector3(2.3f, 0.08f, 0.12f), steelMat, "Rail");
                    Box(root, new Vector3(0f, -1.5f, 0.05f), new Vector3(2.4f, 0.08f, 2.1f), steelMat, "Floor");
                    break;
                }
                case "ground":
                {
                    // down at the targets' level with nothing in front: a low concrete block and sandbags to kneel behind
                    // (the eye 1.2 m above the ground, the rifle resting on the block)
                    Box(root, new Vector3(0f, -0.82f, 0.95f), new Vector3(2.2f, 0.76f, 0.45f), wallMat, "Block");
                    Box(root, new Vector3(-1.45f, -0.95f, 0.7f), new Vector3(0.75f, 0.5f, 0.5f), sillMat, "Sandbag");
                    Box(root, new Vector3(1.4f, -1.0f, 0.75f), new Vector3(0.7f, 0.4f, 0.5f), sillMat, "Sandbag");
                    break;
                }
                case "slope":
                {
                    // a couple of rocks to rest the rifle on and duck behind — low and a little ahead, so they frame the
                    // bottom of the view without cutting the line down the slope (test: a rock 0.9 m ahead hid the valley)
                    Box(root, new Vector3(0.35f, -1.25f, 1.35f), new Vector3(1.3f, 0.7f, 0.8f), rockMat, "Rock").transform.localRotation = Quaternion.Euler(4f, 12f, -6f);
                    Box(root, new Vector3(-1.25f, -1.2f, 0.8f), new Vector3(0.8f, 0.6f, 0.8f), rockMat, "Rock").transform.localRotation = Quaternion.Euler(-8f, -20f, 5f);
                    break;
                }
            }
            return root;
        }
    }
}
