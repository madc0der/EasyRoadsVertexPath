using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ERVertexPath
{
    public class ERPathAdapter : MonoBehaviour
    {
        private float roadWidth;
        private float totalDistance;
        private Vector3[] positions;
        private Vector3[] directions;
        private Vector3[] normals;
        private Quaternion[] rotations;
        private float[] distances;
        private Dictionary<int, GameObject> startIndexToRoadMap;
        
        private readonly LinkedLines lastBestSection = new LinkedLines();
        private readonly PathPoint pointInstance = new PathPoint();

        public float RoadWidth => roadWidth;

        public float TotalDistance => totalDistance;

        public Vector3[] Positions => positions;

        public Vector3[] Directions => directions;

        public Vector3[] Normals => normals;

        public Quaternion[] Rotations => rotations;

        public float[] Distances => distances;

        public Dictionary<int, GameObject> GetStartIndexToRoadMap()
        {
            return startIndexToRoadMap;
        }

        public void InitFromWrapper(ERPathToVertexPathWrapper wrapper)
        {
            InitFromData(wrapper.Width, 
                wrapper.TotalDistance, wrapper.Positions, wrapper.Directions, wrapper.Normals, 
                wrapper.Rotations, wrapper.Distances);
        }

        public void InitFromData(float roadWidth, 
            float totalDistance, Vector3[] positions, Vector3[] directions, Vector3[] normals,
            Quaternion[] rotations, float[] distances,
            Dictionary<int, GameObject> startIndexToRoadMap = null)
        {
            this.roadWidth = roadWidth;
            this.totalDistance = totalDistance;
            this.positions = positions;
            this.directions = directions;
            this.normals = normals;
            this.rotations = rotations;
            this.distances = distances;
            this.startIndexToRoadMap = startIndexToRoadMap;
        }

        //Pass prevPathPoint to avoid recalculations for same segment
        public virtual PathPoint GetClosestPathPoint(Vector3 p, PathPoint prevPathPoint = null)
        {
            float closestDistance = 0f;
            int startIndex = 0, endIndex = 0;
            if (prevPathPoint != null)
            {
                closestDistance = GetClosestDistanceAlongPath(p, prevPathPoint, out startIndex, out endIndex);
            }
            else
            {
                closestDistance = GetClosestDistanceAlongPath(p, out startIndex, out endIndex);
            }
            var clampedDistance = clampDistance(closestDistance);
            return GetPathPoint(closestDistance, clampedDistance, startIndex, endIndex);
        }

        public PathPoint GetPathPoint(float distance)
        {
            var clampedDistance = clampDistance(distance);
            var i1i2 = findNeighbourIndices(clampedDistance);
            var i1 = i1i2.Key;
            var i2 = i1i2.Value;

            return GetPathPoint(distance, clampedDistance, i1, i2);
        }

        private PathPoint GetPathPoint(float distance, float clampedDistance, int i1, int i2)
        {
            var d1 = distances[i1];
            var d2 = i2 == 0 ? totalDistance : distances[i2];

            var t = (clampedDistance - d1) / (d2 - d1);

            pointInstance.set(
                clampedDistance,
                distance,
                Vector3.Lerp(positions[i1], positions[i2], t),
                Vector3.Lerp(directions[i1], directions[i2], t),
                Vector3.Lerp(normals[i1], normals[i2], t),
                Quaternion.Lerp(rotations[i1], rotations[i2], t),
                i1, i2,
                GetHashCode()
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
            return GetClosestDistanceAlongPath(p, out _, out _);
        }

        private float GetClosestDistanceAlongPath(Vector3 p, out int startIndex, out int endIndex)
        {
            var bestSection = FindLinkedLines(p, true);
            if (bestSection == null)
            {
                bestSection = FindLinkedLines(p, false);
            }
            projectPointOnBestSection(bestSection, p, out var distanceOnPath, out startIndex, out endIndex);
            return distanceOnPath;
        }

        private float GetClosestDistanceAlongPath(Vector3 p, PathPoint prevPathPoint, out int startIndex, out int endIndex)
        {
            if (IsSameSegment(p, prevPathPoint, out var currentDistanceOnPath))
            {
                startIndex = prevPathPoint.index1;
                endIndex = prevPathPoint.index2;
                return currentDistanceOnPath;
            }
            
            var bestSection = FindLinkedLines(p, true);
            if (bestSection == null)
            {
                bestSection = FindLinkedLines(p, false);
            }
            projectPointOnBestSection(bestSection, p, out var distanceOnPath, out startIndex, out endIndex);
            
            return distanceOnPath;
        }
        
        public Vector3 GetClosestPointOnPath(Vector3 p, out float closestDistance)
        {
            var bestSection = FindLinkedLines(p, true);
            if (bestSection == null)
            {
                bestSection = FindLinkedLines(p, false);
            }
            return projectPointOnBestSection(bestSection, p, out closestDistance, out _, out _);
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
            var lastIndex = -1;
            for (var i = distances.Length - 1; i >= 0; i--)
            {
                var d = distances[i];
                if (d <= clampedDistance)
                {
                    lastIndex = i;
                    break;
                }
            }
            var i1 = lastIndex;
            
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

        private Vector3 projectPointOnBestSection(
            LinkedLines bestSection, 
            Vector3 p,
            out float distanceOnPath,
            out int startIndex,
            out int endIndex)
        {
            var p1p2 = bestSection.p2 - bestSection.p1;
            var p1p = p - bestSection.p1;

            if (Vector3.Dot(p1p, p1p2) > 0)
            {
                startIndex = bestSection.i1;
                endIndex = bestSection.i2;
                return projectPointOnVector(bestSection.p1, bestSection.p2, 
                    p, bestSection.d1, bestSection.d2,
                    out distanceOnPath);
            }
            else
            {
                startIndex = bestSection.i0;
                endIndex = bestSection.i1;
                return projectPointOnVector(bestSection.p0, bestSection.p1, 
                    p, bestSection.d0, bestSection.d1,
                    out distanceOnPath);
            }
        }

        private bool IsSameSegment(Vector3 p, PathPoint prevPathPoint, out float currentDistanceOnPath)
        {
            currentDistanceOnPath = -1f;
            
            //if ((prevPathPoint?.calculatedForAdapter?.GetHashCode() ?? -1) != GetHashCode())
            if (prevPathPoint?.calculatedForAdapterId != GetHashCode())
            {
                return false;
            }
            
            var i1 = prevPathPoint.index1;
            var i2 = prevPathPoint.index2;
            var p1 = positions[i1];
            var p2 = positions[i2];
            var p1p2 = p2 - p1;
            var p1p = p - p1;

            if (Vector3.Dot(p1p, p1p2) > 0)
            {
                if (p1p.sqrMagnitude < p1p2.sqrMagnitude)
                {
                    var sourceDistance = distances[i1];
                    var targetDistance = distances[i2];
                    if (targetDistance < sourceDistance)
                    {
                        targetDistance += totalDistance;
                    }
                    projectPointOnVector(p1, p2, p, sourceDistance, targetDistance, out currentDistanceOnPath);
                    return true;
                }
            }
            return false;
        }

        private LinkedLines FindLinkedLines(Vector3 p, bool strictHeight)
        {
            var bestSection = lastBestSection;
            var minDistance = float.MaxValue;

            //Assume any bridge will have vertical offset > 10 (i.e. 16)
            const float verticalDistanceThreshold = 10f;

            var found = false;

            for (var i = 0; i < positions.Length; i++)
            {
                var isFirstPoint = i == 0;
                var isLastPoint = i == positions.Length - 1;

                var i0 = isFirstPoint ? positions.Length - 1 : i - 1;
                var i1 = isLastPoint ? 0 : i + 1;

                var p1 = positions[i];

                var p1p = p - p1;
                var distance = p1p.sqrMagnitude;
                var verticalDistance = Mathf.Abs(p.y - p1.y);
                if (distance < minDistance && (!strictHeight || verticalDistance < verticalDistanceThreshold))
                {
                    found = true;
                    minDistance = distance;

                    bestSection.p0 = positions[i0];
                    bestSection.p1 = p1;
                    bestSection.p2 = positions[i1];
                    
                    bestSection.d0 = distances[i0];
                    if (isFirstPoint)
                    {
                        bestSection.d1 = totalDistance + distances[i];
                        bestSection.d2 = totalDistance + distances[i1];
                    } 
                    else if (isLastPoint)
                    {
                        bestSection.d1 = distances[i];
                        bestSection.d2 = totalDistance + distances[i1];
                    }
                    else
                    {
                        bestSection.d1 = distances[i];
                        bestSection.d2 = distances[i1];
                    }

                    bestSection.i0 = i0;
                    bestSection.i1 = i;
                    bestSection.i2 = i1;
                }
            }

            if (!found)
            {
                return null;
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
        public float clampedDistance;
        public float distance;
        public Vector3 position;
        public Vector3 direction;
        public Vector3 normal;
        public Quaternion rotation;
        public int index1;
        public int index2;
        public int calculatedForAdapterId;

        public void set(float clampedDistance,
            float distance, 
            Vector3 position, 
            Vector3 direction, 
            Vector3 normal, 
            Quaternion rotation, 
            int index1, 
            int index2,
            int calculatedForAdapterId)
        {
            this.clampedDistance = clampedDistance;
            this.distance = distance;
            this.position = position;
            this.direction = direction;
            this.normal = normal;
            this.rotation = rotation;
            this.index1 = index1;
            this.index2 = index2;
            this.calculatedForAdapterId = calculatedForAdapterId;
        }

        public PathPoint copyFrom(PathPoint src)
        {
            set(src.clampedDistance,
                src.distance, 
                src.position, 
                src.direction, 
                src.normal, 
                src.rotation, 
                src.index1, 
                src.index2,
                src.calculatedForAdapterId);
            return this;
        }

        public static PathPoint CopyFrom(PathPoint src)
        {
            var pathPoint = new PathPoint();
            pathPoint.copyFrom(src);
            return pathPoint;
        }
    }
}