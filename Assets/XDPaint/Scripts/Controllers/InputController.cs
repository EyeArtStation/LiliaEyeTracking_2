// #define XDPAINT_VR_ENABLE

#if XDPAINT_VR_ENABLE
using System.Collections.Generic;
using UnityEngine.XR;
using InputDevice = UnityEngine.XR.InputDevice;
using CommonUsages = UnityEngine.XR.CommonUsages;
#endif

using System;
using System.Collections.Generic;
using UnityEngine;
using XDPaint.Core;
using XDPaint.Tools;
using XDPaint.Utils;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
#endif

namespace XDPaint.Controllers
{
    public class InputController : Singleton<InputController>
    {
        [Header("General")]
        [SerializeField, Min(1)] private int maxTouchesCount = 10;
        [SerializeField] private InputMethods inputMethods = InputMethods.VRMode | InputMethods.Pen | InputMethods.Touch | InputMethods.Mouse;

        public bool triggerMouseClick;

        public InputMethods ActiveInputMethods
        {
            get => inputMethods;
            set => inputMethods = value;
        }

        [Header("Ignore Raycasts Settings")]
        [SerializeField] private List<Canvas> canvases = new List<Canvas>();
        [SerializeField] private bool blockRaycastsOnPress;
        [SerializeField] private GameObject[] ignoreForRaycasts;
        
        [Header("VR Settings")]
        public Transform PenTransform;
        public float MinimalDistanceToPaint = 0f;

        public event Action OnUpdate;
        public event Action<int, Vector3> OnMouseHover;
        public event Action<int, Vector3, float> OnMouseDown;
        public event Action<int, Vector3, float> OnMouseButton;
        public event Action<int, Vector3> OnMouseUp;

        public int MaxTouchesCount => maxTouchesCount;
        public IList<Canvas> Canvases => canvases;
        public bool BlockRaycastsOnPress => blockRaycastsOnPress;

        public GameObject[] IgnoreForRaycasts => ignoreForRaycasts;

        private bool isVRMode;
        private bool[] isBegan;

        public int isTimedDrawing;
        
#if XDPAINT_VR_ENABLE
        private List<InputDevice> leftHandedControllers;
        private bool isPressed;
#endif
        
#if XDP_DEBUG
        public void OnUpdateCustom()
        {
            OnUpdate?.Invoke();
        }

        public void OnMouseDownCustom(int fingerId, Vector2 screenPosition, float pressure = 1f)
        {
            OnMouseDown?.Invoke(fingerId, screenPosition, pressure);
        }

        public void OnMouseButtonCustom(int fingerId, Vector2 screenPosition, float pressure = 1f)
        {
            OnMouseButton?.Invoke(fingerId, screenPosition, pressure);
        }

        public void OnMouseUpCustom(int fingerId, Vector2 screenPosition)
        {
            OnMouseUp?.Invoke(fingerId, screenPosition);
        }
#endif
        
        void Start()
        {
            isBegan = new bool[maxTouchesCount];
            isVRMode = Settings.Instance.VRModeEnabled;
            InitVR();
#if ENABLE_INPUT_SYSTEM
            if (!EnhancedTouchSupport.enabled)
            {
                EnhancedTouchSupport.Enable();
            }
#endif
        }

        private void InitVR()
        {
#if XDPAINT_VR_ENABLE
            leftHandedControllers = new List<InputDevice>();
            var desiredCharacteristics = InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller;
            InputDevices.GetDevicesWithCharacteristics(desiredCharacteristics, leftHandedControllers);
#endif
        }
        
        void Update()
        {
            //VR
            if (isVRMode && (inputMethods & InputMethods.VRMode) == InputMethods.VRMode)
            {
#if XDPAINT_VR_ENABLE
                OnUpdate?.Invoke();

                OnMouseHover?.Invoke(0, Vector3.zero);

                //VR input
                //next line can be changed for VR device input
                if (leftHandedControllers.Count > 0 && leftHandedControllers[0].TryGetFeatureValue(CommonUsages.triggerButton, out var triggerValue) && triggerValue)
                {
                    if (!isPressed)
                    {
                        isPressed = true;
                        OnMouseDown?.Invoke(0, Vector3.zero, 1f);
                    }
                    else
                    {
                        OnMouseButton?.Invoke(0, Vector3.zero, 1f);
                    }
                }
                else if (isPressed)
                {
                    isPressed = false;
                    OnMouseUp?.Invoke(0, Vector3.zero);
                }
#endif
            }
            else if ((inputMethods & InputMethods.Pen) == InputMethods.Pen)
            {
                //Pen / Touch / Mouse
#if ENABLE_INPUT_SYSTEM
                if (Pen.current != null && (Pen.current.press.isPressed || Pen.current.press.wasReleasedThisFrame))
                {
                    if (Pen.current.press.isPressed)
                    {
                        OnUpdate?.Invoke();

                        var pressure = Settings.Instance.PressureEnabled ? Pen.current.pressure.ReadValue() : 1f;
                        var position = Pen.current.position.ReadValue();

                        if (Pen.current.press.wasPressedThisFrame)
                        {
                            OnMouseDown?.Invoke(0, position, pressure);
                        }

                        if (!Pen.current.press.wasPressedThisFrame)
                        {
                            OnMouseButton?.Invoke(0, position, pressure);
                        }
                    }
                    else if (Pen.current.press.wasReleasedThisFrame)
                    {
                        var position = Pen.current.position.ReadValue();
                        OnMouseUp?.Invoke(0, position);
                    }
                }
                else if (Touchscreen.current != null && Touch.activeTouches.Count > 0 && (inputMethods & InputMethods.Touch) == InputMethods.Touch)
                {
                    foreach (var touch in Touch.activeTouches)
                    {
                        var fingerId = touch.finger.index;
                        if (fingerId >= maxTouchesCount)
                            continue;

                        OnUpdate?.Invoke();

                        var pressure = Settings.Instance.PressureEnabled ? touch.pressure : 1f;
                        if (touch.phase == TouchPhase.Began && !isBegan[fingerId])
                        {
                            isBegan[fingerId] = true;
                            OnMouseDown?.Invoke(fingerId, touch.screenPosition, pressure);
                        }

                        if (isBegan[fingerId])
                        {
                            if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                            {
                                OnMouseButton?.Invoke(fingerId, touch.screenPosition, pressure);
                            }

                            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                            {
                                isBegan[fingerId] = false;
                                OnMouseUp?.Invoke(fingerId, touch.screenPosition);
                            }
                        }
                    }
                }
                else if (Mouse.current != null && (inputMethods & InputMethods.Mouse) == InputMethods.Mouse)
                {
                    OnUpdate?.Invoke();

                    var mousePosition = Application.isFocused ? Mouse.current.position.ReadValue() : -Vector2.one;
                    OnMouseHover?.Invoke(0, mousePosition);

                    if (Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        OnMouseDown?.Invoke(0, mousePosition, 1f);
                        return;
                    }

                    if (Mouse.current.leftButton.isPressed)
                    {
                        OnMouseButton?.Invoke(0, mousePosition, 1f);
                    }

                    if (Mouse.current.leftButton.wasReleasedThisFrame)
                    {
                        OnMouseUp?.Invoke(0, mousePosition);
                    }
                }
#elif ENABLE_LEGACY_INPUT_MANAGER
                //Touch / Mouse
                if (Input.touchSupported && Input.touchCount > 0 && (inputMethods & InputMethods.Touch) == InputMethods.Touch)
                {
                    foreach (var touch in Input.touches)
                    {
                        var fingerId = touch.fingerId;
                        if (fingerId >= maxTouchesCount)
                            continue;

                        OnUpdate?.Invoke();

                        var pressure = Settings.Instance.PressureEnabled ? touch.pressure : 1f;
                        if (touch.phase == TouchPhase.Began && !isBegan[fingerId])
                        {
                            isBegan[fingerId] = true;
                            OnMouseDown?.Invoke(fingerId, touch.position, pressure);
                        }

                        if (touch.fingerId == fingerId)
                        {
                            if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                            {
                                OnMouseButton?.Invoke(fingerId, touch.position, pressure);
                            }

                            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                            {
                                isBegan[fingerId] = false;
                                OnMouseUp?.Invoke(fingerId, touch.position);
                            }
                        }
                    }
                }
                else if ((inputMethods & InputMethods.Mouse) == InputMethods.Mouse)
                {
                    OnUpdate?.Invoke();

                    OnMouseHover?.Invoke(0, Input.mousePosition);

                    if (Input.GetMouseButtonDown(0) || triggerMouseClick == true)
                    {
                        OnMouseDown?.Invoke(0, Input.mousePosition, 1f);
                        triggerMouseClick = false;
                        return;
                    }

                    if (Input.GetMouseButton(0))
                    {
                        OnMouseButton?.Invoke(0, Input.mousePosition, 1f);
                    }

                    if (Input.GetMouseButtonUp(0))
                    {
                        OnMouseUp?.Invoke(0, Input.mousePosition);
                    }
                }
#endif
            }
        }

        /*public void SetBrushstroke(bool isDrawing)
        {
            isTimedDrawing = isDrawing;
        }*/
    }
}