using System;
using System.Collections.Generic;
using System.Linq;
using EasyRoads3Dv3;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.PlayerLoop;

namespace ERVertexPath
{
    public class ERMultiRoadPathCreator : MonoBehaviour
    {
        public ERNetworkVertexPathCreator allRoadsPathCreator;
        public string roadNameMask;
        public bool isShowPathGizmo;
        public bool cleanupSourcePaths;

        private ERPathAdapter unionAdapter;

        private bool isInitialized;

        public ERPathAdapter UnionAdapter => unionAdapter;

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

            isInitialized = true;
            
            allRoadsPathCreator.Init();
            var lowerNameMask = roadNameMask.ToLower();
            var roads = new ERRoadNetwork().GetRoads().Where(_road => _road.GetName().ToLower().Contains(lowerNameMask));

            var initialRoad = roads.First();
            var road = initialRoad;
            Assert.IsNotNull(road, "Only implicitly closed tracks are supported, can't find any roads with name contains given mask");
            const int traverseLimit = 1000;
            var traverseConnectionCount = 0;

            var vertexPathWrappers = new List<ERPathToVertexPathWrapper>();

            DebugLog($"Building union vertex path for roads with mask {roadNameMask}");

            do
            {
                vertexPathWrappers.Add(road.roadScript.GetComponent<ERPathToVertexPathWrapper>());
                
                DebugLog($"Search connections for road: {road.GetName()}");
                var endConnector = road.GetConnectionAtEnd(out _);

                if (endConnector != null)
                {
                    for (var i = 0; i < endConnector.GetConnectionCount(); i++)
                    {
                        var connectedRoad = endConnector.GetConnectedRoad(i, out _);
                        if (connectedRoad != null && connectedRoad != road)
                        {
                            road = connectedRoad;
                            break;
                        }
                    }
                }

                traverseConnectionCount++;
            } while (road != initialRoad && traverseConnectionCount < traverseLimit);

            if (traverseConnectionCount == traverseLimit)
            {
                throw new Exception($"Too much roads, limit {traverseLimit} reached");
            }
            
            BuildUnionVertexPath(vertexPathWrappers);
        }

        private void BuildUnionVertexPath(IEnumerable<ERPathToVertexPathWrapper> wrappers)
        {
            var vertexList = new List<Vector3>();
            var directionsList = new List<Vector3>();
            var normalsList = new List<Vector3>();
            var rotationsList = new List<Quaternion>();
            var distanceList = new List<float>();
            var startIndexToRoad = new Dictionary<int, GameObject>();

            var totalDistance = 0f;
            var positionIndex = 0;
            foreach (var wrapper in wrappers)
            {
                vertexList.AddRange(wrapper.Positions);
                directionsList.AddRange(wrapper.Directions);
                normalsList.AddRange(wrapper.Normals);
                rotationsList.AddRange(wrapper.Rotations);
                // ReSharper disable once AccessToModifiedClosure
                var updatedDistances = wrapper.Distances.Select(distance => distance + totalDistance);
                distanceList.AddRange(updatedDistances);
                totalDistance += wrapper.TotalDistance;
                
                startIndexToRoad.Add(positionIndex, wrapper.gameObject);
                Debug.Log($"Adding road {wrapper.gameObject.name} from index: {positionIndex}");

                if (cleanupSourcePaths)
                {
                    wrapper.ClearPathData();
                    var pathAdapter = wrapper.gameObject.GetComponent<ERPathAdapter>();
                    if (pathAdapter)
                    {
                        pathAdapter.ClearPathData();
                    }
                }

                positionIndex = vertexList.Count;
            }

            var adapters = gameObject.GetComponents<ERPathAdapter>();
            foreach (var adapter in adapters)
            {
                DestroyImmediate(adapter);
            }

            unionAdapter = gameObject.AddComponent<ERPathAdapter>();
            unionAdapter.InitFromData(totalDistance, vertexList.ToArray(), directionsList.ToArray(),
                normalsList.ToArray(), rotationsList.ToArray(), distanceList.ToArray(), startIndexToRoad);
            
            DebugLog($"Created union adapter for roads with mask = {roadNameMask}, distance = {unionAdapter.TotalDistance}, vertex count = {vertexList.Count}");
        }

        private void OnDrawGizmos()
        {
            if (!isShowPathGizmo)
            {
                return;
            }
            
            var verticalOffset = Vector3.up * 0.5f;
            for (var i = 0; i < unionAdapter.Positions.Length; i++)
            {
                var start = unionAdapter.Positions[i];
                var end = i == unionAdapter.Positions.Length - 1 ? unionAdapter.Positions[0] : unionAdapter.Positions[i + 1];
                start += verticalOffset;
                end += verticalOffset;
                Debug.DrawLine(start, end, i % 2 == 0 ? Color.white : Color.magenta);
                Debug.DrawLine(start, start + verticalOffset, Color.yellow);
                Debug.DrawLine(start, start + unionAdapter.Normals[i], Color.red);
            }
        }

        private void DebugLog(string msg)
        {
            if (!allRoadsPathCreator.logEnabled)
            {
                return;
            }
            Debug.Log(msg);
        }
    }
}