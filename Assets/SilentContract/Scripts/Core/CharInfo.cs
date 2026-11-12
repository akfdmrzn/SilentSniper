using UnityEngine;

namespace SilentContract
{
    /// <summary>Character metadata baked into each character prefab by the editor pipeline.</summary>
    public class CharInfo : MonoBehaviour
    {
        public string id;
        public string gender = "m";
        public float height = 1.78f;
    }
}
