// Location: Core/Player/Input/PlayerInputHandler.cs
using UnityEngine;
using Unity.Netcode;
using System;

namespace Core.Player.Input
{
    /// <summary>
    /// Centralized input handler for player controls
    /// </summary>
    public class PlayerInputHandler : NetworkBehaviour
    {
        // Movement input events
        public event Action<Vector2> OnMovementInput;
        public event Action<Vector2> OnDashInput;
        
        // Attack input events
        public event Action OnPrimaryAttackInput;
        public event Action OnSecondaryAttackInput;
        
        // Special ability input events
        public event Action OnAbility1Input;
        public event Action OnAbility2Input;
        
        // Input states
        private Vector2 movementInput;
        private bool isDashing;
        
        // Key bindings (could be made configurable)
        [Header("Key Bindings")]
        [SerializeField] private KeyCode dashKey = KeyCode.Space;
        [SerializeField] private KeyCode ability1Key = KeyCode.Q;
        [SerializeField] private KeyCode ability2Key = KeyCode.E;
        
        private void Update()
        {
            if (!IsOwner) return;
            
            HandleMovementInput();
            HandleActionInput();
        }
        
        private void HandleMovementInput()
        {
            // Get movement input
            movementInput = new Vector2(
                UnityEngine.Input.GetAxis("Horizontal"),
                UnityEngine.Input.GetAxis("Vertical")
            );
            
            // Broadcast movement input
            OnMovementInput?.Invoke(movementInput);
            
            // Handle dash input
            if (UnityEngine.Input.GetKeyDown(dashKey))
            {
                Vector2 dashDirection = new Vector2(
                    UnityEngine.Input.GetAxisRaw("Horizontal"),
                    UnityEngine.Input.GetAxisRaw("Vertical")
                ).normalized;
                
                if (dashDirection.sqrMagnitude > 0.1f)
                {
                    OnDashInput?.Invoke(dashDirection);
                }
            }
        }
        
        private void HandleActionInput()
        {
            // Primary attack (left mouse button)
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                OnPrimaryAttackInput?.Invoke();
            }
            
            // Secondary attack (right mouse button)
            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                OnSecondaryAttackInput?.Invoke();
            }
            
            // Ability 1
            if (UnityEngine.Input.GetKeyDown(ability1Key))
            {
                OnAbility1Input?.Invoke();
            }
            
            // Ability 2
            if (UnityEngine.Input.GetKeyDown(ability2Key))
            {
                OnAbility2Input?.Invoke();
            }
        }
        
        /// <summary>
        /// Gets the current mouse position in world coordinates
        /// </summary>
        public Vector3 GetMouseWorldPosition()
        {
            Vector3 mousePos = UnityEngine.Input.mousePosition;
            mousePos.z = -Camera.main.transform.position.z;
            return Camera.main.ScreenToWorldPoint(mousePos);
        }
        
        /// <summary>
        /// Gets the direction from player to mouse position
        /// </summary>
        public Vector3 GetMouseDirectionFromPlayer()
        {
            Vector3 mouseWorldPos = GetMouseWorldPosition();
            return (mouseWorldPos - transform.position).normalized;
        }
    }
}