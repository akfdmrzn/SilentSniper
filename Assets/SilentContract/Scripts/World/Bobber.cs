using UnityEngine;

namespace SilentContract
{
    /// <summary>Gentle swell for floating props (applied to the model child so path movers keep control of the root).</summary>
    public class Bobber : MonoBehaviour
    {
        public float amplitude = 0.08f;
        public float period = 4.2f;
        Vector3 basePos;
        Quaternion baseRot;
        float seed;

        void Awake()
        {
            basePos = transform.localPosition;
            baseRot = transform.localRotation;
            seed = Random.value * 10f;
        }

        void Update()
        {
            float t = (Time.time + seed) * Mathf.PI * 2f / period;
            float heave = Mathf.Sin(t) * amplitude;
            float roll = Mathf.Sin(t * 0.83f + 1.3f) * amplitude * 9f;
            float pitch = Mathf.Sin(t * 0.61f + 0.4f) * amplitude * 5f;
            transform.localPosition = basePos + Vector3.up * heave;
            transform.localRotation = Quaternion.Euler(pitch, 0, roll) * baseRot;
        }
    }
}
