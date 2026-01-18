using UnityEngine;
using FaeMaze.Visitors;

namespace FaeMaze.Props
{
    /// <summary>
    /// Prevents visitors from entering a radius around the lantern.
    /// Attached to a child object with a trigger collider.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class LanternExclusionZone : MonoBehaviour
    {
        [SerializeField] private float exclusionRadius = 1f;

        private SphereCollider _collider;

        private void Awake()
        {
            _collider = GetComponent<SphereCollider>();
            _collider.isTrigger = true;
            _collider.radius = exclusionRadius;
        }

        private void OnTriggerStay(Collider other)
        {
            // Check if this is a visitor
            var visitor = other.GetComponent<VisitorControllerBase>();
            if (visitor == null)
            {
                visitor = other.GetComponentInParent<VisitorControllerBase>();
            }

            if (visitor == null) return;

            // Push visitor away from center
            Vector3 lanternPos = transform.position;
            Vector3 visitorPos = visitor.transform.position;

            // Calculate direction away from lantern (XY plane only)
            Vector2 lanternXY = new Vector2(lanternPos.x, lanternPos.y);
            Vector2 visitorXY = new Vector2(visitorPos.x, visitorPos.y);
            Vector2 awayDir = (visitorXY - lanternXY).normalized;

            // If visitor is exactly at center, pick a random direction
            if (awayDir.sqrMagnitude < 0.01f)
            {
                float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                awayDir = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle));
            }

            // Calculate distance from lantern
            float dist = Vector2.Distance(visitorXY, lanternXY);

            // If inside exclusion radius, push to edge
            if (dist < exclusionRadius)
            {
                Vector2 newPosXY = lanternXY + awayDir * exclusionRadius;
                visitor.transform.position = new Vector3(newPosXY.x, newPosXY.y, visitorPos.z);
            }
        }
    }
}
