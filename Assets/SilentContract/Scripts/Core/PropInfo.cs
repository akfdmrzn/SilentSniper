using UnityEngine;

namespace SilentContract
{
    /// <summary>Prop metadata baked into each prop prefab by the editor pipeline.</summary>
    public class PropInfo : MonoBehaviour
    {
        public string id;
        public string category;
        public Vector3 size;
        public string credit;
    }
}
