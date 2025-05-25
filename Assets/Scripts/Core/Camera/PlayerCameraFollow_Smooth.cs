// Location: Core/Player/Components/PlayerCameraFollow_Smooth.cs
using UnityEngine;

namespace Core.Player.Components
{
    public class PlayerCameraFollow_Smooth : MonoBehaviour
    {
        public static PlayerCameraFollow_Smooth Instance;

        public Transform player;
        public Vector3 offset;
        public float smoothSpeed = 0.125f;
        public float zoomSpeed = 10f;
        public float minFOV = 3f;
        public float maxFOV = 5f;

        private Vector3 velocity = Vector3.zero;
        private Camera mainCamera;

        void Awake()
        {
            Instance = this;
            Debug.Log("[PlayerCameraFollow] Initialized as singleton instance");
        }

        void Start()
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("Main Camera not found!");
            }
        }

        public void SetTarget(Transform target)
        {
            player = target;
            Debug.Log($"[PlayerCameraFollow] Target set to: {player.name}");
        }

        void LateUpdate()
        {
            Debug.Log("[PlayerCameraFollow] LateUpdate called");
            if (player != null)
            {
                Vector3 targetPosition = player.position + offset;
                transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothSpeed);
            }
            else
            {
                Debug.LogWarning("[PlayerCameraFollow] Player target is not set!");
            }
        }

        void Update()
        {
            Debug.Log("[PlayerCameraFollow] Update called");
            float scroll = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                HandleZoom(scroll);
            }
        }

        void HandleZoom(float scroll)
        {
            if (mainCamera == null) return;
            
            float newSize = mainCamera.orthographicSize - scroll * zoomSpeed;
            newSize = Mathf.Clamp(newSize, minFOV, maxFOV);
            mainCamera.orthographicSize = newSize;
        }
    }
}