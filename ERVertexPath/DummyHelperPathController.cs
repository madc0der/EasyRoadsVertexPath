using EasyRoads3Dv3;
using UnityEngine;
using UnityEngine.Assertions;

namespace ERVertexPath
{
    public class DummyHelperPathController : MonoBehaviour
    {
        public GameObject erPathAdapterContainer;
        
        public float speedMs;
        
        [Tooltip("Position by closest point or by closest distance")]
        public bool positionByClosestPoint;

        public bool alignToPath;

        private ERPathAdapter pathAdapter;

        private void Start()
        {
            pathAdapter = erPathAdapterContainer.GetComponent<ERPathAdapter>();
            Assert.IsNotNull(pathAdapter, $"Cant find ERPathAdapter for gameObject {erPathAdapterContainer.name}");
        }

        private void FixedUpdate()
        {
            if (!alignToPath)
            {
                return;
            }

            var p = transform.position + transform.forward * (speedMs * Time.fixedDeltaTime);

            if (positionByClosestPoint)
            {
                var pathP = pathAdapter.GetClosestPointOnPath(p, out var closestDistance);
                transform.position = pathP;
                transform.rotation = pathAdapter.GetRotationAtDistance(closestDistance);
            }
            else
            {
                var closestDistance = pathAdapter.GetClosestDistanceAlongPath(p);
                var pathP = pathAdapter.GetPointAtDistance(closestDistance);
                transform.position = pathP;
                transform.rotation = pathAdapter.GetRotationAtDistance(closestDistance);
            }
        }
    }
}