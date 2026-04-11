using UnityEngine;

namespace TinCan.Features.Environment
{
    /// <summary>
    /// Utility Component: Smoothly moves a Rigidbody in a repeating pattern using sine waves.
    /// Used for testing moving platforms and character synchronization.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SimpleOscillator : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private Vector3 _amplitude = new Vector3(5f, 2f, 0f); // How far it moves in each axis
        [SerializeField] private Vector3 _frequency = new Vector3(1f, 0.5f, 0f); // How fast it moves in each axis

        private Rigidbody _rb;
        private Vector3 _startPosition;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _startPosition = transform.position;
        }

        private void FixedUpdate()
        {
            // Move in FixedUpdate so the Rigidbody has an accurate linearVelocity for physics
            Vector3 offset = new Vector3(
                _amplitude.x > 0 ? Mathf.Sin(Time.time * _frequency.x) * _amplitude.x : 0,
                _amplitude.y > 0 ? Mathf.Sin(Time.time * _frequency.y) * _amplitude.y : 0,
                _amplitude.z > 0 ? Mathf.Sin(Time.time * _frequency.z) * _amplitude.z : 0
            );

            _rb.MovePosition(_startPosition + offset);
        }
    }
}
