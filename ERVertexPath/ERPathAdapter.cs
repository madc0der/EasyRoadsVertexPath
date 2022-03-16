using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ERVertexPath
{
    public class ERPathAdapter : MonoBehaviour
    {
        private float totalDistance;
        private Vector3[] positions;
        private Vector3[] directions;
        private Vector3[] normals;
        private Quaternion[] rotations;
        private float[] distances;
        private Dictionary<int, GameObject> startIndexToRoadMap;
        
        private readonly LinkedLines lastBestSection = new LinkedLines();
        private readonly PathPoint pointInstance = new PathPoint();

        public float TotalDistance => totalDistance;

        public Vector3[] Positions => positions;

        public Vector3[] Directions => directions;

        public Vector3[] Normals => normals;

        public Quaternion[] Rotations => rotations;

        public float[] Distances => distances;

        public void InitFromWrapper(ERPathToVertexPathWrapper wrapper)
        {
            InitFromData(wrapper.TotalDistance, wrapper.Positions, wrapper.Directions, wrapper.Normals, 
                wrapper.Rotations, wrapper.Distances);
        }

        public void InitFromData(float totalDistance, Vector3[] positions, Vector3[] directions, Vector3[] normals,
            Quaternion[] rotations, float[] distances,
            Dictionary<int, GameObject> startIndexToRoadMap = null)
        {
            this.totalDistance = totalDistance;
            this.positions = positions;
            this.directions = directions;
            this.normals = normals;
            this.rotations = rotations;
            this.distances = distances;
            this.startIndexToRoadMap = startIndexToRoadMap;
        }

        public virtual PathPoint GetClosestPathPoint(Vector3 p)
        {
            var closestDistance = GetClosestDistanceAlongPath(p);
            return GetPathPoint(closestDistance);
        }

        public PathPoint GetPathPoint(float distance)
        {
            var clampedDistance = clampDistance(distance);
            var i1i2 = findNeighbourIndices(clampedDistance);
            var i1 = i1i2.Key;
            var i2 = i1i2.Value;

            var d1 = distances[i1];
            var d2 = i2 == 0 ? totalDistance : distances[i2];

            var t = (clampedDistance - d1) / (d2 - d1);

            pointInstance.set(
                distance,
                Vector3.Lerp(positions[i1], positions[i2], t),
                Vector3.Lerp(directions[i1], directions[i2], t),
                Vector3.Lerp(normals[i1], normals[i2], t),
                Quaternion.Lerp(rotations[i1], rotations[i2], t),
                i1, i2
            );
            return pointInstance;
        }

        public Vector3 GetPointAtDistance(float distance)
        {
            var clampedDistance = clampDistance(distance);
            var i1i2 = findNeighbourIndices(clampedDistance);
            var i1 = i1i2.Key;
            var i2 = i1i2.Value;

            var d1 = distances[i1];
            var d2 = i2 == 0 ? totalDistance : distances[i2];

            var t = (clampedDistance - d1) / (d2 - d1);

            return Vector3.Lerp(positions[i1], positions[i2], t);
        }

        public Quaternion GetRotationAtDistance(float distance)
        {
            var clampedDistance = clampDistance(distance);
            var i1i2 = findNeighbourIndices(clampedDistance);
            var i1 = i1i2.Key;
            var i2 = i1i2.Value;
            var d1 = distances[i1];
            var d2 = i2 == 0 ? totalDistance : distances[i2];

            var t = (clampedDistance - d1) / (d2 - d1);

            return Quaternion.Lerp(rotations[i1], rotations[i2], t);
        }

        public Vector3 GetDirectionAtDistance(float distance)
        {
            var clampedDistance = clampDistance(distance);
            var i1i2 = findNeighbourIndices(clampedDistance);
            var i1 = i1i2.Key;
            var i2 = i1i2.Value;

            var d1 = distances[i1];
            var d2 = i2 == 0 ? totalDistance : distances[i2];

            var t = (clampedDistance - d1) / (d2 - d1);

            return Vector3.Lerp(directions[i1], directions[i2], t).normalized;
        }

        public Vector3 GetSideNormalAtDistance(float distance)
        {
            var clampedDistance = clampDistance(distance);
            var i1i2 = findNeighbourIndices(clampedDistance);
            var i1 = i1i2.Key;
            var i2 = i1i2.Value;

            var d1 = distances[i1];
            var d2 = i2 == 0 ? totalDistance : distances[i2];

            var t = (clampedDistance - d1) / (d2 - d1);

            return Vector3.Lerp(normals[i1], normals[i2], t).normalized;
        }

        public float GetClosestDistanceAlongPath(Vector3 p)
        {
            var bestSection = findLinkedLines(p);
            projectPointOnBestSection(bestSection, p, out var distanceOnPath, out _);
            return distanceOnPath;
        }

        public Vector3 GetClosestPointOnPath(Vector3 p, out float closestDistance)
        {
            var bestSection = findLinkedLines(p);
            return projectPointOnBestSection(bestSection, p, out closestDistance, out _);
        }

        public Vector3 GetClosestPointOnPath(Vector3 p)
        {
            return GetClosestPointOnPath(p, out _);
        }

        public void ClearPathData()
        {
            positions = null;
            directions = null;
            normals = null;
            rotations = null;
            distances = null;
        }

        public GameObject FindClosestRoadObject(int positionIndex)
        {
            var key = startIndexToRoadMap.Keys.Last(index => index <= positionIndex);
            return startIndexToRoadMap[key];
        }

        private float clampDistance(float distance)
        {
            if (distance < 0)
            {
                var clampedNegative = distance < -totalDistance
                    ? (distance / totalDistance - Mathf.Ceil(distance / totalDistance)) * totalDistance
                    : distance;
                return totalDistance + clampedNegative;
            }
            return distance > totalDistance
                    ? (distance / totalDistance - Mathf.Floor(distance / totalDistance)) * totalDistance
                    : distance;
        }
        
        private KeyValuePair<int, int> findNeighbourIndices(float clampedDistance)
        {
            var i1 = distances.ToList().FindLastIndex(d => d <= clampedDistance);
            
            var i2 = i1 < distances.Length - 1 ? i1 + 1 : 0;
            return new KeyValuePair<int, int>(i1, i2);
        }

        private Vector3 projectPointOnVector(Vector3 p1, Vector3 p2,
            Vector3 p, 
            float d1, float d2, 
            out float distanceOnPath)
        {
            var p1p2 = p2 - p1;
            var p1p = p - p1;
            var sqrMag = p1p2.sqrMagnitude;
            if (sqrMag < 0.0001f)
            {
                distanceOnPath = d1;
                return p1;
            }

            var dot = Vector3.Dot(p1p, p1p2);
            dot = Mathf.Clamp01(dot / sqrMag); //TODO: is clamp needed?

            distanceOnPath = d1 + dot * (d2 - d1);
            
            return p1 + dot * p1p2;
        }

        private Vector3 projectPointOnBestSection(LinkedLines bestSection, Vector3 p, 
            out float distanceOnPath,
            out int startIndex)
        {
            var p1p2 = bestSection.p2 - bestSection.p1;
            var p1p = p - bestSection.p1;

            if (Vector3.Dot(p1p, p1p2) > 0)
            {
                startIndex = bestSection.i1;
                return projectPointOnVector(bestSection.p1, bestSection.p2, 
                    p, bestSection.d1, bestSection.d2,
                    out distanceOnPath);
            }
            else
            {
                startIndex = bestSection.i0;
                return projectPointOnVector(bestSection.p0, bestSection.p1, 
                    p, bestSection.d0, bestSection.d1,
                    out distanceOnPath);
            }
        }

        private LinkedLines findLinkedLines(Vector3 p)
        {
            var bestSection = lastBestSection;
            var minDistance = float.MaxValue;
            
            for (var i = 0; i < positions.Length; i++)
            {
                var isFirstPoint = i == 0;
                var isLastPoint = i == positions.Length - 1;

                var i0 = isFirstPoint ? positions.Length - 1 : i - 1;
                var i1 = isLastPoint ? 0 : i + 1;

                var p1 = positions[i];

                var p1p = p - p1;
                var distance = p1p.sqrMagnitude;
                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestSection.p0 = positions[i0];
                    bestSection.p1 = p1;
                    bestSection.p2 = positions[i1];
                    
                    bestSection.d0 = distances[i0];
                    bestSection.d1 = isFirstPoint ? totalDistance + distances[i] : distances[i];
                    if (isFirstPoint)
                    {
                        bestSection.d2 = totalDistance + distances[i1];
                    } else if (isLastPoint)
                    {
                        bestSection.d2 = totalDistance + distances[i1];
                    }
                    else
                    {
                        bestSection.d2 = distances[i1];
                    }

                    bestSection.i0 = i0;
                    bestSection.i1 = i;
                    bestSection.i2 = i1;
                }
            }

            return bestSection;
        }
    }

    class LinkedLines
    {
        public Vector3 p0, p1, p2;
        public float d0, d1, d2;
        public int i0, i1, i2;
    }

    public class PathPoint
    {
        public float distance;
        public Vector3 position;
        public Vector3 direction;
        public Vector3 normal;
        public Quaternion rotation;
        public int index1;
        public int index2;

        public void set(float distance, Vector3 position, Vector3 direction, Vector3 normal, Quaternion rotation, int index1, int index2)
        {
            this.distance = distance;
            this.position = position;
            this.direction = direction;
            this.normal = normal;
            this.rotation = rotation;
            this.index1 = index1;
            this.index2 = index2;
        }

        public PathPoint copyFrom(PathPoint src)
        {
            set(src.distance, src.position, src.direction, src.normal, src.rotation, src.index1, src.index2);
            return this;
        }
    }
}