using EasyRoads3Dv3;
using UnityEngine;
using UnityEngine.Assertions;

namespace ERVertexPath
{
    public class ERNetworkVertexPathCreator : MonoBehaviour
    {
        public bool logEnabled;

        public float defaultAngleThreshold = 5f;
        public float defaultScanStep = 1f;
        public float defaultMaxDistance = 50f;

        private bool isInitialized;
        
        private void Awake()
        {
            Init();
        }

        public void Init(bool forceInit = false)
        {
            if (!forceInit && isInitialized)
            {
                return;
            }
            
            Assert.IsNotNull(GetComponent<ERModularBase>(),
                "Cant build vertex paths for all roads, ERModularBase not found");
            ScanRoadsAndAppendWrapper();
            isInitialized = true;
        }

        private void ScanRoadsAndAppendWrapper()
        {
            var roads = GetComponentsInChildren<ERModularRoad>();
            DebugLog($"Found {roads.Length} roads");

            foreach (var road in roads)
            {
                DebugLog($"Processing road {road.name}");
                var wrapper = AppendWrapper(road);
                AppendAdapter(road, wrapper);
            }
            
            DebugLog($"All roads were extended with vertex path wrappers/adapters");
        }

        private ERPathToVertexPathWrapper AppendWrapper(ERModularRoad road)
        {
            var wrapper = road.GetComponent<ERPathToVertexPathWrapper>();
            if (!wrapper)
            {
                wrapper = (ERPathToVertexPathWrapper)road.gameObject.AddComponent(typeof(ERPathToVertexPathWrapper));            
                wrapper.angleThreshold = defaultAngleThreshold;
                wrapper.scanStep = defaultScanStep;
                wrapper.maxDistance = defaultMaxDistance;
                DebugLog($"Added new wrapper to road {road.name}");
            }
            else
            {
                DebugLog($"Found existing wrapper for road {road.name}");
            }

            InitVertexPathWrapper(road, wrapper);
            return wrapper;
        }

        private void InitVertexPathWrapper(ERModularRoad road, ERPathToVertexPathWrapper wrapper)
        {
            wrapper.Init(road);
            DebugLog($"Wrapper for road {road.name} initialized with {wrapper.Positions.Length} points and {wrapper.TotalDistance} length");
        }

        private ERPathAdapter AppendAdapter(ERModularRoad road, ERPathToVertexPathWrapper wrapper)
        {
            var adapter = road.GetComponent<ERPathAdapter>();
            if (!adapter)
            {
                adapter = (ERPathAdapter) road.gameObject.AddComponent(typeof(ERPathAdapter));
                adapter.InitFromWrapper(wrapper);
                DebugLog($"Added new adapter to road {road.name}");
            }
            else
            {
                DebugLog($"Found existing adapter for road {road.name}");
            }

            adapter.InitFromWrapper(wrapper);
            return adapter;
        }

        private void DebugLog(string message)
        {
            if (!logEnabled)
            {
                return;
            }
            
            Debug.Log(message);
        }
    }
}