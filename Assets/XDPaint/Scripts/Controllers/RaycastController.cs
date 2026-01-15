using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using XDPaint.Controllers.InputData;
using XDPaint.Core;
using XDPaint.Tools.Raycast;
using XDPaint.Tools.Raycast.Base;
using XDPaint.Tools.Raycast.Data;
using XDPaint.Utils;

namespace XDPaint.Controllers
{
    public class RaycastController : Singleton<RaycastController>
    {
        [SerializeField] private bool useDepthTexture;
        public bool UseDepthTexture
        {
            get => useDepthTexture;
            set
            {
                useDepthTexture = value;
                if (useDepthTexture)
                {
                    if (depthToWorldConverter == null)
                    {
                        InitDepthToWorldConverter();
                    }
                }
                else
                {
                    depthToWorldConverter?.DoDispose();
                    depthToWorldConverter = null;
                }
            }
        }

#if XDP_DEBUG
        [SerializeField]
#endif
        private DepthToWorldConverter depthToWorldConverter;
        public DepthToWorldConverter DepthToWorldConverter => depthToWorldConverter;

        private ulong requestID;
        private readonly List<IRaycastMeshData> meshesData = new List<IRaycastMeshData>();
        private readonly Dictionary<KeyValuePair<IPaintManager, int>, List<RaycastData>> raycastResults = new Dictionary<KeyValuePair<IPaintManager, int>, List<RaycastData>>(RaycastsResultsCapacity);
        private readonly Dictionary<int, List<RaycastData>> raycasts = new Dictionary<int, List<RaycastData>>(RaycastsCapacity);
        private readonly Dictionary<KeyValuePair<IPaintManager, int>, Dictionary<int, List<Triangle>>> lineRaycastTriangles = new Dictionary<KeyValuePair<IPaintManager, int>, Dictionary<int, List<Triangle>>>(LineRaycastTrianglesCapacity);
        private readonly Dictionary<KeyValuePair<IPaintManager, int>, InputRequest> pendingRequests = new Dictionary<KeyValuePair<IPaintManager, int>, InputRequest>(PendingRequestsCapacity);

        private const int RaycastsResultsCapacity = 64;
        private const int RaycastsCapacity = 64;
        private const int LineRaycastTrianglesCapacity = 32;
        private const int PendingRequestsCapacity = 16;
        
        private void Start()
        {
            InitDepthToWorldConverter();
        }
        
        private void LateUpdate()
        {
            if (pendingRequests.Count > 0)
            {
                raycastResults.Clear();
            }

            var processRaycast = false;
            for (var i = pendingRequests.Keys.Count - 1; i >= 0; i--)
            {
                var key = pendingRequests.Keys.ElementAt(i);
                var inputRequest = pendingRequests[key];
                if (inputRequest != null)
                {
                    ProcessRaycast(inputRequest.RequestContainer);
                    processRaycast = true;
                }
            }

            if (processRaycast)
            {
                for (var i = pendingRequests.Keys.Count - 1; i >= 0; i--)
                {
                    var key = pendingRequests.Keys.ElementAt(i);
                    var inputRequest = pendingRequests[key];
                    if (inputRequest != null)
                    {
                        var requestContainer = inputRequest.RequestContainer;
                        foreach (var callback in inputRequest.Callbacks)
                        {
                            callback?.Invoke(requestContainer);
                        }
                        inputRequest.RequestContainer = null;
                    }
                }
            }
            
            pendingRequests.Clear();
        }

        private void OnDestroy()
        {
            depthToWorldConverter?.DoDispose();
            depthToWorldConverter = null;
            DisposeRequests();
        }

        public void InitObject(IPaintManager paintManager, Component paintComponent, Component renderComponent)
        {
            DestroyMeshData(paintManager);
            
            var raycastMeshData = meshesData.FirstOrDefault(x => x.Transform == paintComponent.transform);
            if (raycastMeshData == null)
            {
                if (renderComponent is SkinnedMeshRenderer)
                {
                    raycastMeshData = new RaycastSkinnedMeshRendererData();
                }
                else if (renderComponent is MeshRenderer)
                {
                    raycastMeshData = new RaycastMeshRendererData();
                }
                else if (renderComponent is SpriteRenderer)
                {
                    raycastMeshData = new RaycastSpriteRendererData();
                }
                else if (renderComponent is RawImage)
                {
                    raycastMeshData = new RaycastCanvasRendererData();
                }

                if (raycastMeshData != null)
                {
                    raycastMeshData.Init(paintComponent, renderComponent);
                    meshesData.Add(raycastMeshData);
                }
                else
                {
                    return;
                }
            }
            
            if (paintManager.Triangles != null)
            {
                foreach (var triangle in paintManager.Triangles)
                {
                    triangle.SetRaycastMeshData(raycastMeshData, paintManager.UVChannel);
                }
            }
            
            raycastMeshData.AddPaintManager(paintManager);
        }
        
        public IList<Triangle> GetLineTriangles(IPaintManager paintManager, int fingerId)
        {
            var key = new KeyValuePair<IPaintManager, int>(paintManager, fingerId);
            if (lineRaycastTriangles.TryGetValue(key, out var trianglesDictionary))
            {
                return trianglesDictionary.TryGetValue(fingerId, out var triangles) ? triangles : null;
            }
            return null;
        }

        public Mesh GetMesh(IPaintManager paintManager)
        {
            return meshesData.Find(x => x.PaintManagers.Contains(paintManager)).Mesh;
        }

        public void DestroyMeshData(IPaintManager paintManager)
        {
            for (var i = meshesData.Count - 1; i >= 0; i--)
            {
                if (meshesData[i].PaintManagers.Count == 1 && meshesData[i].PaintManagers.ElementAt(0) == paintManager)
                {
                    meshesData[i].DoDispose();
                    meshesData.RemoveAt(i);
                    break;
                }

                if (meshesData[i].PaintManagers.Count > 1 && meshesData[i].PaintManagers.Contains(paintManager))
                {
                    meshesData[i].RemovePaintManager(paintManager);
                    break;
                }
            }
            
            DisposeRequests(paintManager);
        }

        public RaycastData RaycastLocal(IPaintManager paintManager, Ray ray, Vector3 pointerPosition, int fingerId, bool useWorld = true)
        {
            raycasts.Clear();
            foreach (var meshData in meshesData)
            {
                if (meshData == null || !meshData.PaintManagers.Contains(paintManager))
                    continue;

                var raycast = meshData.GetRaycast(paintManager, ray, pointerPosition, fingerId, useWorld, false);
                if (raycast != null)
                {
                    if (!raycasts.ContainsKey(fingerId))
                    {
                        raycasts.Add(fingerId, new List<RaycastData>());
                    }
                    raycasts[fingerId].Add(raycast);
                }
            }

            return raycasts.TryGetValue(fingerId, out var list) ? SortIntersects(list, ray.origin) : null;
        }
        
        public void AddCallbackToRequest(IPaintManager sender, int fingerId, Action callback = null)
        {
            var key = new KeyValuePair<IPaintManager, int>(sender, fingerId);
            if (!pendingRequests.ContainsKey(key))
            {
                var requestContainer = new RaycastRequestContainer
                {
                    Sender = sender,
                    FingerId = fingerId,
                    RequestID = requestID
                };
                requestID++;

                pendingRequests.Add(key, new InputRequest
                {
                    RequestContainer = requestContainer,
                    Callbacks = new List<Action<RaycastRequestContainer>>()
                });
            }

            pendingRequests[key].Callbacks.Add(_ => callback?.Invoke());
        }

        private bool TryAddRequest(IPaintManager sender, int fingerId, Action<RaycastRequestContainer> callback = null)
        {
            var successful = false;
            var key = new KeyValuePair<IPaintManager, int>(sender, fingerId);
            if (!pendingRequests.ContainsKey(key))
            {
                var requestContainer = new RaycastRequestContainer
                {
                    Sender = sender,
                    FingerId = fingerId,
                    RequestID = requestID
                };
                requestID++;

                pendingRequests.Add(key, new InputRequest
                {
                    RequestContainer = requestContainer,
                    Callbacks = new List<Action<RaycastRequestContainer>>()
                });
                
                successful = true;
            }
            
            pendingRequests[key].Callbacks.Add(callback);
            return successful;
        }

        public void RequestRaycast(IPaintManager sender, InputData.InputData inputData, InputData.InputData previousInputData, Action<RaycastRequestContainer> callback = null)
        {
            if (!TryAddRequest(sender, inputData.FingerId, callback))
                return;

            foreach (var meshData in meshesData)
            {
                if (meshData == null)
                    continue;
                
                if (!meshData.PaintManagers.Contains(sender))
                    continue;
                
                foreach (var paintManager in meshData.PaintManagers)
                {
                    if (paintManager == null)
                        continue;
                    
                    if (paintManager.IsActive() && paintManager == sender)
                    {
                        var result = meshData.RequestRaycast(requestID, paintManager, inputData, previousInputData);
                        if (result != null)
                        {
                            var key = new KeyValuePair<IPaintManager, int>(sender, inputData.FingerId);
                            pendingRequests[key].RequestContainer.RaycastRequests.Add(result);
                        }
                    }
                }
            }
        }

        private void ProcessRaycast(RaycastRequestContainer requestContainer)
        {
            if (requestContainer == null)
                return;

            var key = requestContainer.Key;
            if (!lineRaycastTriangles.ContainsKey(key))
            {
                var dictionary = new Dictionary<int, List<Triangle>> { { requestContainer.FingerId, new List<Triangle>() } };
                lineRaycastTriangles.Add(key, dictionary);
            }
            else
            {
                if (lineRaycastTriangles[key].TryGetValue(requestContainer.FingerId, out var list))
                {
                    list.Clear();
                }
                else
                {
                    lineRaycastTriangles[key] = new Dictionary<int, List<Triangle>>{ { requestContainer.FingerId, new List<Triangle>() } };
                }
            }
            
            foreach (var meshData in meshesData)
            {
                if (meshData == null)
                    continue;
                
                if (!meshData.PaintManagers.Contains(requestContainer.Sender))
                    continue;
                
                foreach (var paintManager in meshData.PaintManagers)
                {
                    if (paintManager == null)
                        continue;
                    
                    if (paintManager.IsActive())
                    {
                        var raycast = meshData.TryGetRaycastResponse(requestContainer, out var triangles);
                        if (raycast == null) 
                            continue;
                        
                        if (triangles != null)
                        {
                            lineRaycastTriangles[key][requestContainer.FingerId].AddRange(triangles);
                        }

                        if (!raycastResults.ContainsKey(key))
                        {
                            raycastResults.Add(key, new List<RaycastData>());
                        }
                        raycastResults[key].Add(raycast);
                    }
                }
            }
        }

        public RaycastData TryGetRaycast(RaycastRequestContainer requestContainer, int fingerId, Vector3 pointerPosition)
        {
            return SortIntersects(requestContainer.Sender, fingerId, pointerPosition, raycastResults);
        }

        private RaycastData SortIntersects(IPaintManager sender, int fingerId, Vector3 pointerPosition, Dictionary<KeyValuePair<IPaintManager, int>, List<RaycastData>> raycastsData)
        {
            IPaintManager paintManager = null;
            RaycastData raycastData = null;
            var currentDistance = float.MaxValue;
            foreach (var pair in raycastsData)
            {
                var key = pair.Key;
                var data = raycastsData[key];

                if (data.Count == 0 || pair.Key.Value != fingerId)
                    continue;

                foreach (var raycast in data)
                {
                    var distance = Vector3.Distance(pointerPosition, raycast.WorldHit);
                    if (distance < currentDistance)
                    {
                        currentDistance = distance;
                        paintManager = key.Key;
                        raycastData = raycast;
                    }
                }
            }

            return paintManager == sender ? raycastData : null;
        }

        private RaycastData SortIntersects(IList<RaycastData> raycastData, Vector3 pointerPosition)
        {
            if (raycastData.Count == 0)
                return null;
            
            if (raycastData.Count == 1)
                return raycastData[0];
            
            var result = raycastData[0];
            var currentDistance = Vector3.Distance(pointerPosition, result.WorldHit);
            for (var i = 1; i < raycastData.Count; i++)
            {
                var distance = Vector3.Distance(pointerPosition, raycastData[i].WorldHit);
                if (distance < currentDistance)
                {
                    currentDistance = distance;
                    result = raycastData[i];
                }
            }
            return result;
        }

        private void InitDepthToWorldConverter()
        {
            if (useDepthTexture)
            {
                if (PaintController.Instance.Camera.orthographic)
                {
                    Debug.LogWarning("Camera is orthographic, 'useDepthTexture' flag will be ignored.");
                    return;
                }
                
                var textureFloatSupports = SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat);
                var renderTextureFloatSupports = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat);
                if (textureFloatSupports && renderTextureFloatSupports)
                {
                    if ((PaintController.Instance.Camera.depthTextureMode & DepthTextureMode.Depth) != 0)
                    {
                        PaintController.Instance.Camera.depthTextureMode |= DepthTextureMode.Depth;
                    }
                    depthToWorldConverter = new DepthToWorldConverter();
                    depthToWorldConverter.Init();
                }
                else
                {
                    Debug.LogWarning("Float texture format is not supported! Set UseDepthTexture to false.");
                    useDepthTexture = false;
                }
            }
        }

        private void DisposeRequests(IPaintManager paintManager = null)
        {
            foreach (var key in pendingRequests.Keys)
            {
                var request = pendingRequests[key];
                if (request != null && request.RequestContainer != null && request.RequestContainer.RaycastRequests != null)
                {
                    if (paintManager == null || request.RequestContainer.Sender == paintManager)
                    {
                        foreach (var raycastRequest in request.RequestContainer.RaycastRequests)
                        {
                            raycastRequest.DoDispose();
                        }
                        request.RequestContainer.RaycastRequests.Clear();
                    }
                }
            }
            pendingRequests.Clear();
        }
    }
}