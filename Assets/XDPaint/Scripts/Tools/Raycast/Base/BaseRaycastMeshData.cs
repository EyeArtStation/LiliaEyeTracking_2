using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using XDPaint.Controllers;
using XDPaint.Controllers.InputData;
using XDPaint.Core;
using XDPaint.Tools.Raycast.Data;
using XDPaint.Utils;
using Component = UnityEngine.Component;
using Object = UnityEngine.Object;

namespace XDPaint.Tools.Raycast.Base
{
    public abstract class BaseRaycastMeshData : IRaycastMeshData
    {
        private readonly List<IPaintManager> paintManagers = new List<IPaintManager>();
        public IReadOnlyCollection<IPaintManager> PaintManagers => paintManagers;

        private Transform transform;
        public Transform Transform => transform;
        
        private List<Vector3> vertices = new List<Vector3>();
        public List<Vector3> Vertices => vertices;
        
        private List<Vector3> normals = new List<Vector3>();
        public List<Vector3> Normals => normals;

        private Mesh mesh;
        public Mesh Mesh => mesh;

        protected List<Vector2> UV = new List<Vector2>();
        protected Bounds MeshWorldBounds;
        protected int BakedFrame = -1;
        protected bool IsTrianglesDataUpdated;
        private Dictionary<int, UVChannelData> uvChannelsData = new Dictionary<int, UVChannelData>();
        private Dictionary<int, SubMeshTrianglesData> trianglesSubMeshData = new Dictionary<int, SubMeshTrianglesData>();
        private Dictionary<int, List<RaycastData>> raycasts = new Dictionary<int, List<RaycastData>>(RaycastsCapacity);
        private Dictionary<KeyValuePair<IPaintManager, int>, RaycastData> raycastsDict = new Dictionary<KeyValuePair<IPaintManager, int>, RaycastData>(RaycastsDictCapacity);
        private List<Triangle> lineRaycastTriangles = new List<Triangle>(LineRaycastTrianglesCapacity);
        private Dictionary<IPaintManager, RaycastTriangleData[]> outputData = new Dictionary<IPaintManager, RaycastTriangleData[]>();

        private const int RaycastsCapacity = 64;
        private const int RaycastsDictCapacity = 32;
        private const int LineRaycastTrianglesCapacity = 64;

        public virtual void Init(Component paintComponent, Component rendererComponent)
        {
            mesh = new Mesh();
            transform = paintComponent.transform;
        }

        public virtual void AddPaintManager(IPaintManager paintManager)
        {
            paintManagers.Add(paintManager);
            outputData.Add(paintManager, new RaycastTriangleData[paintManager.Triangles.Length]);
        }

        public virtual void RemovePaintManager(IPaintManager paintManager)
        {
            paintManagers.Remove(paintManager);
            outputData.Remove(paintManager);

            if (trianglesSubMeshData.ContainsKey(paintManager.SubMesh))
            {
                trianglesSubMeshData[paintManager.SubMesh].PaintManagers.Remove(paintManager);
                if (trianglesSubMeshData[paintManager.SubMesh].PaintManagers.Count == 0)
                {
                    trianglesSubMeshData.Remove(paintManager.SubMesh);
                }
            }
            
            if (uvChannelsData.ContainsKey(paintManager.UVChannel))
            {
                uvChannelsData[paintManager.UVChannel].PaintManagers.Remove(paintManager);
                if (uvChannelsData[paintManager.UVChannel].PaintManagers.Count == 0)
                {
                    uvChannelsData.Remove(paintManager.UVChannel);
                }
            }
        }
        
        public void DoDispose()
        {
            if (mesh != null)
            {
                Object.Destroy(mesh);
                mesh = null;
            }
        }
                        
        public Vector2 GetUV(int channel, int index)
        {
            return uvChannelsData[channel].UV[index];
        }

        public IRaycastRequest RequestRaycast(ulong requestId, IPaintManager sender, InputData inputData,
            InputData previousInputData, bool useWorld = true, bool useCache = true, bool raycastAll = true)
        {
            if (!sender.IsActive())
                return null;

            raycasts.Clear();
            raycastsDict.Clear();
            lineRaycastTriangles.Clear();
            lineRaycastTriangles.Capacity = LineRaycastTrianglesCapacity;
            var rayTransformed = new Ray(inputData.Ray.origin, inputData.Ray.direction);
            if (useWorld)
            {
                UpdateMeshBounds(sender);
                MeshWorldBounds.Expand(0.0001f);
                var boundsIntersect = MeshWorldBounds.IntersectRay(inputData.Ray);
                if (!boundsIntersect || (inputData.InputSource == InputSource.Screen && !IsBoundsInDepth(MeshWorldBounds, inputData.Position)))
                    return null;

                var origin = Transform.InverseTransformPoint(inputData.Ray.origin);
                var direction = Transform.InverseTransformVector(inputData.Ray.direction);
                rayTransformed = new Ray(origin, direction);
            }

            IRaycastRequest raycastRequest = null;
            var lineTriangles = RaycastController.Instance.GetLineTriangles(sender, inputData.FingerId);
            var raycastAllTriangles = raycastAll || lineTriangles == null;
            var triangles = raycastAllTriangles ? sender.Triangles : lineTriangles;
            var distance2 = Vector3.Distance(inputData.Ray.origin, transform.position);
            var hit2 = inputData.Ray.origin + inputData.Ray.direction.normalized * distance2;
            var distance1 = previousInputData.Ray.Equals(default(Ray)) ? distance2 : Vector3.Distance(previousInputData.Ray.origin, transform.position);
            var hit1 = previousInputData.Ray.Equals(default(Ray)) ? hit2 : previousInputData.Ray.origin + previousInputData.Ray.direction.normalized * distance1;
            var plane2Position = transform.InverseTransformPoint(hit1);
            var plane3Position = transform.InverseTransformPoint(hit2);
            var nearPlanePoint3 = transform.InverseTransformPoint(inputData.Ray.origin);
            var plane1Position = (plane2Position + plane3Position) / 2f;
            var plane1Normal = Vector3.Cross(plane3Position - plane2Position, nearPlanePoint3 - plane2Position);
            var plane2Normal = -Vector3.Cross(plane2Position + plane1Normal - nearPlanePoint3, plane2Position - plane1Normal - nearPlanePoint3);
            var plane3Normal = -Vector3.Cross(plane3Position + plane1Normal - nearPlanePoint3, plane3Position - plane1Normal - nearPlanePoint3);
            var skipPlaneIntersectsTriangle = plane2Position == plane3Position;
            if (Settings.Instance.RaycastsMethod == RaycastSystemType.CPU)
            {
                var raycastsList = new List<RaycastTriangleData>();
                foreach (var triangle in triangles)
                {
                    var result = new RaycastTriangleData
                    {
                        IntersectPlaneTriangleId = -1,
                        RaycastTriangleId = -1
                    };

                    if (skipPlaneIntersectsTriangle)
                    {
                        if (triangle.TryGetRaycastData(raycastAll ? rayTransformed : inputData.Ray, out var raycastData))
                        {
                            result.RaycastTriangleId = raycastData.Triangle.Id;
                            result.Hit = raycastData.Hit;
                            result.UVHit = raycastData.UVHit;
                        }

                        raycastsList.Add(result);
                        continue;
                    }

                    if (IsPlane1IntersectsTriangle(plane1Position, plane1Normal, triangle.Position0, triangle.Position1, triangle.Position2) ||
                        IsPlane2IntersectsTriangle(plane2Position, plane2Normal, triangle.Position0, triangle.Position1, triangle.Position2) ||
                        IsPlane3IntersectsTriangle(plane3Position, plane3Normal, triangle.Position0, triangle.Position1, triangle.Position2))
                    {
                        result.IntersectPlaneTriangleId = -1;
                    }
                    else
                    {
                        result.IntersectPlaneTriangleId = triangle.Id;
                        if (triangle.TryGetRaycastData(raycastAll ? rayTransformed : inputData.Ray,
                                out var raycastData))
                        {
                            result.RaycastTriangleId = raycastData.Triangle.Id;
                            result.Hit = raycastData.Hit;
                            result.UVHit = raycastData.UVHit;
                        }
                    }

                    raycastsList.Add(result);
                }

                var request = new CPURaycastRequest
                {
                    Sender = sender,
                    FingerId = inputData.FingerId,
                    PointerPosition = inputData.Ray.origin,
                    RequestId = requestId,
                    OutputList = raycastsList
                };
                raycastRequest = request;
            }
            else if (Settings.Instance.RaycastsMethod == RaycastSystemType.JobSystem)
            {
                var verticesData = trianglesSubMeshData[sender.SubMesh];
                if (!IsTrianglesDataUpdated)
                {
                    for (var i = 0; i < sender.Triangles.Length; i++)
                    {
                        var triangle = sender.Triangles[i];
                        var triangleData = new TriangleData
                        {
                            Id = triangle.Id,
                            Position0 = Vertices[triangle.I0],
                            Position1 = Vertices[triangle.I1],
                            Position2 = Vertices[triangle.I2],
                            UV0 = UV[triangle.I0],
                            UV1 = UV[triangle.I1],
                            UV2 = UV[triangle.I2]
                        };
                        verticesData.TrianglesData[i] = triangleData;
                    }
                }

                var trianglesData = new NativeArray<TriangleData>(verticesData.TrianglesData, Allocator.TempJob);
                var data = new NativeArray<RaycastTriangleData>(verticesData.TrianglesData.Length, Allocator.TempJob);
                var jobRay = raycastAll ? rayTransformed : inputData.Ray;
                var job = new RaycastJob
                {
                    Triangles = trianglesData,
                    OutputData = data,
                    RayOrigin = jobRay.origin,
                    RayDirection = jobRay.direction,
                    Plane1Position = plane1Position,
                    Plane1Normal = plane1Normal,
                    Plane2Position = plane2Position,
                    Plane2Normal = plane2Normal,
                    Plane3Position = plane3Position,
                    Plane3Normal = plane3Normal,
                    SkipPlaneIntersectsTriangle = skipPlaneIntersectsTriangle
                };

                var jobHandle = job.Schedule(verticesData.TrianglesData.Length, 32);
                var request = new JobRaycastRequest
                {
                    Sender = sender,
                    FingerId = inputData.FingerId,
                    PointerPosition = inputData.Ray.origin,
                    RequestId = requestId,
                    JobHandle = jobHandle,
                    InputNativeArray = trianglesData,
                    OutputNativeArray = data
                };
                raycastRequest = request;
            }

            return raycastRequest;
        }

        public RaycastData TryGetRaycastResponse(RaycastRequestContainer request, out IList<Triangle> triangles)
        {
            if (request.IsDisposed)
            {
                triangles = null;
                return null;
            }

            Vector3 pointerPosition = default;
            foreach (var raycastRequest in request.RaycastRequests)
            {
                if (raycastRequest.IsDisposed || request.Sender != raycastRequest.Sender || request.FingerId != raycastRequest.FingerId)
                    continue;

                if (raycastRequest is JobRaycastRequest jobRaycastRequest)
                {
                    jobRaycastRequest.JobHandle.Complete();
                    var data = outputData[request.Sender];
                    jobRaycastRequest.OutputNativeArray.CopyTo(data);
                    
                    foreach (var item in data)
                    {
                        if (item.IntersectPlaneTriangleId >= 0)
                        {
                            var triangle = request.Sender.Triangles[item.IntersectPlaneTriangleId];
                            if (!lineRaycastTriangles.Contains(triangle))
                            {
                                lineRaycastTriangles.Add(triangle);
                            }
                        }
                    
                        if (item.RaycastTriangleId >= 0)
                        {
                            var raycastData = new RaycastData(request.Sender.Triangles[item.RaycastTriangleId])
                            {
                                Hit = item.Hit,
                                UVHit = item.UVHit
                            };
                            
                            if (!raycasts.ContainsKey(raycastRequest.FingerId))
                            {
                                raycasts.Add(raycastRequest.FingerId, new List<RaycastData>());
                            }
                            raycasts[raycastRequest.FingerId].Add(raycastData);
                        }
                    }

                    if (raycasts.Count > 0)
                    {
                        pointerPosition = jobRaycastRequest.PointerPosition;
                        if (raycasts.ContainsKey(raycastRequest.FingerId))
                        {
                            var sortedIntersect = SortRaycasts(jobRaycastRequest.PointerPosition, raycasts[raycastRequest.FingerId]);
                            raycastsDict[request.Key] = sortedIntersect;
                        }
                    }

                    jobRaycastRequest.DoDispose();
                }
                else if (raycastRequest is CPURaycastRequest cpuRaycastRequest)
                {
                    foreach (var item in cpuRaycastRequest.OutputList)
                    {
                        if (item.IntersectPlaneTriangleId >= 0)
                        {
                            var triangle = request.Sender.Triangles[item.IntersectPlaneTriangleId];
                            if (!lineRaycastTriangles.Contains(triangle))
                            {
                                lineRaycastTriangles.Add(triangle);
                            }
                        }
                    
                        if (item.RaycastTriangleId >= 0)
                        {
                            var raycastData = new RaycastData(request.Sender.Triangles[item.RaycastTriangleId])
                            {
                                Hit = item.Hit,
                                UVHit = item.UVHit
                            };
                            
                            if (!raycasts.ContainsKey(raycastRequest.FingerId))
                            {
                                raycasts.Add(raycastRequest.FingerId, new List<RaycastData>());
                            }
                            raycasts[raycastRequest.FingerId].Add(raycastData);
                        }
                    }
                    
                    if (raycasts.Count > 0)
                    {
                        pointerPosition = cpuRaycastRequest.PointerPosition;
                        if (raycasts.ContainsKey(raycastRequest.FingerId))
                        {
                            var sortedIntersect = SortRaycasts(cpuRaycastRequest.PointerPosition, raycasts[raycastRequest.FingerId]);
                            raycastsDict[request.Key] = sortedIntersect;
                        }
                    }
                    
                    cpuRaycastRequest.DoDispose();
                }
            }

            request.RaycastRequests.RemoveAll(x => x.IsDisposed);
            if (request.RaycastRequests.Count == 0)
            {
                request.DoDispose();
            }
            
            triangles = lineRaycastTriangles;

            if (!IsTrianglesDataUpdated)
            {
                IsTrianglesDataUpdated = true;
            }

            if (raycastsDict.Count == 0)
                return null;
            
            KeyValuePair<KeyValuePair<IPaintManager, int>, RaycastData> closestRaycast = default;
            var minDistance = float.MaxValue;
            foreach (var raycast in raycastsDict)
            {
                if (raycast.Key.Value != request.FingerId)
                    continue;
                
                var triangle = raycast.Value;
                var distance = Vector3.Distance(pointerPosition, triangle.WorldHit);
                if (distance < minDistance)
                {
                    closestRaycast = raycast;
                    minDistance = distance;
                }
            }

            if (closestRaycast.Key.Key != request.Sender)
                return null;
            
            return closestRaycast.Value;
        }

        public RaycastData GetRaycast(IPaintManager sender, Ray ray, Vector3 pointerPosition, int fingerId, bool useWorld = true, bool raycastAll = true)
        {
            var rayTransformed = new Ray(ray.origin, ray.direction);
            if (useWorld)
            {
                UpdateMeshBounds(sender);
                MeshWorldBounds.Expand(0.0001f);
                var boundsIntersect = MeshWorldBounds.IntersectRay(ray);
                if (!boundsIntersect)
                    return null;
                
                var origin = Transform.InverseTransformPoint(ray.origin);
                var direction = Transform.InverseTransformVector(ray.direction);
                rayTransformed = new Ray(origin, direction);
            }
            
            raycasts.Clear();
            raycastsDict.Clear();
            foreach (var paintManager in PaintManagers)
            {
                if (paintManager.IsActive())
                {
                    var lineTriangles = RaycastController.Instance.GetLineTriangles(paintManager, fingerId);
                    var raycastAllTriangles = raycastAll || lineTriangles == null;
                    var triangles = raycastAllTriangles ? paintManager.Triangles : lineTriangles;
                    foreach (var triangle in triangles)
                    {
                        if (triangle.TryGetRaycastData(useWorld ? rayTransformed : ray, out var raycastData))
                        {
                            if (!raycasts.ContainsKey(fingerId))
                            {
                                raycasts.Add(fingerId, new List<RaycastData>());
                            }
                            raycasts[fingerId].Add(raycastData);
                        }
                    }

                    if (raycasts.Count > 0)
                    {
                        var sortedIntersect = SortRaycasts(pointerPosition, raycasts[fingerId]);
                        var key = new KeyValuePair<IPaintManager, int>(sender, fingerId);
                        raycastsDict[key] = sortedIntersect;
                    }
                }
            }
            
            if (!IsTrianglesDataUpdated)
            {
                IsTrianglesDataUpdated = true;
            }
            
            if (raycastsDict.Count == 0)
                return null;
            
            KeyValuePair<KeyValuePair<IPaintManager, int>, RaycastData> closestRaycast = default;
            var minDistance = float.MaxValue;
            foreach (var raycast in raycastsDict)
            {
                var triangle = raycast.Value;
                var distance = Vector3.Distance(pointerPosition, triangle.WorldHit);
                if (distance < minDistance)
                {
                    closestRaycast = raycast;
                    minDistance = distance;
                }
            }

            if (closestRaycast.Key.Key != sender)
                return null;
            
            return closestRaycast.Value;
        }

        protected abstract void UpdateMeshBounds(IPaintManager paintManager);

        protected void SetTriangles(IPaintManager paintManager, IEnumerable<Vector3> verticesList, IEnumerable<Vector3> normalsList)
        {
            if (trianglesSubMeshData.ContainsKey(paintManager.SubMesh))
            {
                trianglesSubMeshData[paintManager.SubMesh].PaintManagers.Add(paintManager);
            }
            else
            {
                trianglesSubMeshData.Add(paintManager.SubMesh, new SubMeshTrianglesData
                {
                    PaintManagers = new List<IPaintManager> { paintManager },
                    TrianglesData = new TriangleData[paintManager.Triangles.Length]
                });
            }
            
            vertices.Clear();
            vertices.AddRange(verticesList);
            normals.Clear();
            normals.AddRange(normalsList);
            var verticesData = trianglesSubMeshData[paintManager.SubMesh];
            for (var i = 0; i < paintManager.Triangles.Length; i++)
            {
                var triangle = paintManager.Triangles[i];
                var triangleData = new TriangleData
                {
                    Id = triangle.Id,
                    Position0 = Vertices[triangle.I0],
                    Position1 = Vertices[triangle.I1],
                    Position2 = Vertices[triangle.I2],
                    UV0 = UV[triangle.I0],
                    UV1 = UV[triangle.I1],
                    UV2 = UV[triangle.I2]
                };
                verticesData.TrianglesData[i] = triangleData;
            }
        }

        protected void SetUVs(IPaintManager paintManager, IEnumerable<Vector2> uvEnumerable)
        {
            if (uvChannelsData.ContainsKey(paintManager.UVChannel))
            {
                uvChannelsData[paintManager.UVChannel].PaintManagers.Add(paintManager);
            }
            else
            {
                UV.Clear();
                UV.AddRange(uvEnumerable);
                uvChannelsData.Add(paintManager.UVChannel, new UVChannelData
                {
                    PaintManagers = new List<IPaintManager> { paintManager }, UV = new List<Vector2>(uvEnumerable)
                });
            }
        }

        private RaycastData SortRaycasts(Vector3 pointerPosition, List<RaycastData> raycastsList)
        {
            if (raycastsList.Count == 0)
                return null;
            
            if (raycastsList.Count == 1)
                return raycastsList[0];
            
            var result = raycastsList[0];
            var currentDistance = Vector3.Distance(pointerPosition, result.WorldHit);
            for (var i = 1; i < raycastsList.Count; i++)
            {
                var distance = Vector3.Distance(pointerPosition, raycastsList[i].WorldHit);
                if (distance < currentDistance)
                {
                    currentDistance = distance;
                    result = raycastsList[i];
                }
            }
            return result;
        }

        private bool IsBoundsInDepth(Bounds worldBounds, Vector3? screenPosition)
        {
            if (RaycastController.Instance.DepthToWorldConverter != null && RaycastController.Instance.UseDepthTexture && screenPosition != null)
            {
                var mainCamera = PaintController.Instance.Camera;
                if (!mainCamera.orthographic)
                {
                    var screenPositionInt = new Vector2Int(Mathf.RoundToInt(screenPosition.Value.x), Mathf.RoundToInt(screenPosition.Value.y));
                    if (RaycastController.Instance.DepthToWorldConverter.IsInTextureBounds(screenPositionInt) && 
                        RaycastController.Instance.DepthToWorldConverter.TryGetPosition(screenPositionInt, out var worldPosition) && 
                        worldPosition.w > 0 && worldPosition.w > mainCamera.nearClipPlane && worldPosition.w < mainCamera.farClipPlane)
                    {
                        return worldBounds.Contains(worldPosition);
                    }
                }
            }
            return true;
        }

        private bool IsPlane1IntersectsTriangle(Vector3 planePosition, Vector3 planeNormal, Vector3 v0, Vector3 v1, Vector3 v2)
        {
            var a = Vector3.Dot(v0 - planePosition, planeNormal) >= 0f;
            if (a != Vector3.Dot(v1 - planePosition, planeNormal) >= 0f)
                return false;
            
            return a == Vector3.Dot(v2 - planePosition, planeNormal) >= 0f;
        }
        
        private bool IsPlane2IntersectsTriangle(Vector3 planePosition, Vector3 planeNormal, Vector3 v0, Vector3 v1, Vector3 v2)
        {
            return Vector3.Dot(v0 - planePosition, planeNormal) >= 0f && 
                   Vector3.Dot(v1 - planePosition, planeNormal) >= 0f && 
                   Vector3.Dot(v2 - planePosition, planeNormal) >= 0f;
        }
        
        private bool IsPlane3IntersectsTriangle(Vector3 planePosition, Vector3 planeNormal, Vector3 v0, Vector3 v1, Vector3 v2)
        {
            return Vector3.Dot(v0 - planePosition, planeNormal) < 0 && 
                   Vector3.Dot(v1 - planePosition, planeNormal) < 0 && 
                   Vector3.Dot(v2 - planePosition, planeNormal) < 0;
        }
    }
}