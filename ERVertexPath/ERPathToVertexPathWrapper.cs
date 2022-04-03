using System.Collections.Generic;
using System.Linq;
using EasyRoads3Dv3;
using UnityEngine;

namespace ERVertexPath
{
    //Create for every ER road on scene - will approximate spline path to vertex path
    public class ERPathToVertexPathWrapper : MonoBehaviour
    {
        public float angleThreshold = 2f;
        public float scanStep = 1f;
        public float maxDistance = 50f;

        public bool isShowPathGizmo;
        
        private ERRoad road;
        private float totalDistance;
        private Vector3[] positions;
        private Vector3[] directions;
        //Side normals as Up x Direction
        private Vector3[] normals;
        private Quaternion[] rotations;
        private float[] distances;

        public float TotalDistance => totalDistance;

        public Vector3[] Positions => positions;

        public Vector3[] Directions => directions;

        public Vector3[] Normals => normals;

        public Quaternion[] Rotations => rotations;

        public float[] Distances => distances;

        public float Width => road.GetWidth();

        public void Init(ERModularRoad modularRoad)
        {
            road = new ERRoadNetwork().GetRoadByGameObject(modularRoad.gameObject);
            totalDistance = road.GetDistance();
            buildRoadVertexPath();
        }

        private void buildRoadVertexPath()
        {
            var vertexList = new List<Vector3>();
            var directionsList = new List<Vector3>();
            var normalsList = new List<Vector3>();
            var rotationsList = new List<Quaternion>();
            var distanceList = new List<float>();

            var prevPointDistance = 0f;

            var currentRoadElement = 0;
            for (var t = 0f; t < road.GetDistance(); t += scanStep)
            {
                var p = road.GetPosition(t, ref currentRoadElement);
                var d = road.GetLookatSmooth(t, currentRoadElement);
                var r = Quaternion.LookRotation(d);
                
                var isSignificantVertex = vertexList.Count == 0 
                                          || t - prevPointDistance > maxDistance
                                          || Vector3.Angle(d, directionsList.Last()) > angleThreshold;

                if (isSignificantVertex)
                {
                    vertexList.Add(p);
                    directionsList.Add(d);
                    normalsList.Add(Vector3.Cross(Vector3.up, d));
                    rotationsList.Add(r);
                    distanceList.Add(t);
                    prevPointDistance = t;
                }
            }

            positions = vertexList.ToArray();
            directions = directionsList.ToArray();
            normals = normalsList.ToArray();
            rotations = rotationsList.ToArray();
            distances = distanceList.ToArray();
        }

        public void ClearPathData()
        {
            positions = null;
            directions = null;
            normals = null;
            rotations = null;
            distances = null;
        }

        private void FixedUpdate()
        {
            if (!isShowPathGizmo)
            {
                return;
            }
            
            var verticalOffset = Vector3.up * 0.5f;
            for (var i = 0; i < positions.Length; i++)
            {
                var start = positions[i];
                var end = i == positions.Length - 1 ? positions[0] : positions[i + 1];
                start += verticalOffset;
                end += verticalOffset;
                Debug.DrawLine(start, end, i % 2 == 0 ? Color.white : Color.magenta);
                Debug.DrawLine(start, start + verticalOffset, Color.yellow);
                Debug.DrawLine(start, start + normals[i], Color.red);
            }
        }
    }
}