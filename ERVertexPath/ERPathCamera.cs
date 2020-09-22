using UnityEngine;
using UnityEngine.Assertions;

namespace ERVertexPath
{
    public class ERPathCamera : MonoBehaviour
    {
        public GameObject modularRoad;
        public float speedMs = 30f;

        private ERPathAdapter pathAdapter;
        private Camera cameraToFollow;
        private float cameraPosition;

        private void Start()
        {
            cameraToFollow = GetComponent<Camera>();
            Assert.IsNotNull(cameraToFollow, "Cant find Camera component for ERPathCamera");
            pathAdapter = modularRoad.GetComponent<ERPathAdapter>();
            Assert.IsNotNull(pathAdapter, $"Cant find ERPathAdapter for road {modularRoad.name}");
        }
        
        private void FixedUpdate()
        {
            var position = pathAdapter.GetPointAtDistance(cameraPosition);
            var lookAt = pathAdapter.GetRotationAtDistance(cameraPosition);
            
            cameraToFollow.transform.position = position + Vector3.up * 5f;
            cameraToFollow.transform.rotation = lookAt;

            cameraPosition += Time.deltaTime * speedMs;
            if (cameraPosition > pathAdapter.TotalDistance)
            {
                cameraPosition = 0f;
            }
        }
    }
}