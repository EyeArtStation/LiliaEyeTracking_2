using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using XDPaint.Core;
using XDPaint.Core.Materials;
using XDPaint.Core.PaintModes;
using XDPaint.Tools;
using XDPaint.Utils;

namespace XDPaint.Controllers
{
    public class PaintController : Singleton<PaintController>
    {   
        [SerializeField] private bool overrideCamera;
        public bool OverrideCamera
        {
            get => overrideCamera;
            set => overrideCamera = value;
        }

        [SerializeField] private Camera currentCamera;
        public Camera Camera
        {
            get
            {
                if (currentCamera == null)
                {
                    currentCamera = Camera.main;
                }
                return currentCamera;
            }
            set
            {
                currentCamera = value;
            }
        }
        
        [SerializeField] private bool useSharedSettings = true;
        public bool UseSharedSettings
        {
            get => useSharedSettings;
            set
            {
                useSharedSettings = value;
                if (!initialized)
                    return;
                
                if (useSharedSettings)
                {
                    foreach (var paintManager in paintManagers)
                    {
                        if (paintManager == null)
                            continue;
                        
                        paintManager.Brush = brush;
                        paintManager.Tool = paintTool;
                        paintManager.SetPaintMode(paintModeType);
                    }
                }
                else
                {
                    foreach (var paintManager in paintManagers)
                    {
                        if (paintManager == null)
                            continue;
                        
                        paintManager.InitBrush();
                        paintManager.Tool = paintTool;
                    }
                }
            }
        }

        [SerializeField] private PaintMode paintModeType;
        public PaintMode PaintMode
        {
            get => paintModeType;
            set
            {
                var previousModeType = paintModeType;
                paintModeType = value;
                mode = GetPaintMode(paintModeType);
                if (Application.isPlaying && paintModeType != previousModeType && useSharedSettings)
                {
                    foreach (var paintManager in paintManagers)
                    {
                        if (paintManager == null)
                            continue;
                        
                        paintManager.SetPaintMode(paintModeType);
                    }
                }
            }
        }

        [SerializeField] private PaintTool paintTool;
        public PaintTool Tool
        {
            get => paintTool;
            set
            {
                paintTool = value;
                if (initialized && useSharedSettings)
                {
                    foreach (var paintManager in paintManagers)
                    {
                        if (paintManager == null)
                            continue;
                        
                        paintManager.Tool = paintTool;
                    }
                }
            }
        }

        [SerializeField] private Brush brush = new Brush();
        public Brush Brush
        {
            get => brush;
            set => brush.SetValues(value);
        }

        [SerializeField] private List<PaintManager> paintManagers;
        private List<IPaintMode> paintModes;
        private IPaintMode mode;
        private bool initialized;

        private new void Awake()
        {
            base.Awake();
            paintManagers = new List<PaintManager>();
            CreatePaintModes();
            Init();
        }

        private void CreatePaintModes()
        {
            if (paintModes == null)
            {
                paintModes = new List<IPaintMode>();
                var type = typeof(IPaintMode);
                var types = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(s => s.GetTypes())
                    .Where(p => type.IsAssignableFrom(p) && p.IsClass);
                foreach (var modeType in types)
                {
                    var paintMode = Activator.CreateInstance(modeType) as IPaintMode;
                    paintModes.Add(paintMode);
                }
            }
        }

        private void Init()
        {
            if (Application.isPlaying && !initialized)
            {
                mode = GetPaintMode(paintModeType);
                if (brush.SourceTexture == null)
                {
                    brush.SourceTexture = Settings.Instance.DefaultBrush;
                }
                
                brush.Init(mode);
                initialized = true;
#if !BURST
                if (Settings.Instance.RaycastsMethod == RaycastSystemType.JobSystem)
                {
                    Debug.LogWarning("The raycast method is set to JobSystem, but the Burst package is not installed. It is recommended " +
                                     "to use the Burst package to increase performance.\nPlease, install the Burst package from Package Manager.", Settings.Instance);
                }
#endif
            }
        }

        public IPaintMode GetPaintMode(PaintMode paintMode)
        {
            if (paintModes == null)
            {
                CreatePaintModes();
            }

            if (paintModes != null)
            {
                foreach (var paintModeInstance in paintModes)
                {
                    if (paintModeInstance.PaintMode == paintMode)
                    {
                        return paintModeInstance;
                    }
                }
            }

            return null;
        }

        public void RegisterPaintManager(PaintManager paintManager)
        {
            UnRegisterPaintManager(paintManager);
            paintManagers.Add(paintManager);
        }

        public void UnRegisterPaintManager(PaintManager paintManager)
        {
            if (paintManagers.Contains(paintManager))
            {
                paintManagers.Remove(paintManager);
            }
        }

        private async void OnDestroy()
        {
            if (useSharedSettings && paintManagers != null && paintManagers.Count > 0)
            {
                while (paintManagers.Count > 0)
                {
                    await Task.Yield();
                }
            }
            brush.DoDispose();
        }

        public PaintManager[] GetActivePaintManagers()
        {
            return paintManagers?.Where(paintManager => paintManager.IsActive()).ToArray();
        }

        public PaintManager[] GetAllPaintManagers()
        {
            return paintManagers?.Where(paintManager => paintManager != null).ToArray();
        }
                
        public IEnumerable<PaintManager> GetAllPaintManagersAsEnumerable()
        {
            return paintManagers.Where(paintManager => paintManager != null);
        }
    }
}