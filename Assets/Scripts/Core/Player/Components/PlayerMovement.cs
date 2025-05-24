// Location: Core/Player/Components/PlayerMovement.cs 
// Updated version using PlayerInputHandler
using UnityEngine;
using Unity.Netcode;
using System.Collections;
using Core.Player.Input;

namespace Core.Player.Components
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Unity.Netcode.Components.NetworkTransform))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerMovement : NetworkBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float dashForce = 10f;
        [SerializeField] private float dashCooldown = 1f;
        [SerializeField] private float dashDuration = 0.2f;

        private Rigidbody2D rb;
        private Animator animator;
        private PlayerInputHandler inputHandler;
        
        private bool isDashing = false;
        private float lastDashTime = -999f;
        private string currentAnimation = "";

        public override void OnNetworkSpawn()
        {
            rb = GetComponent<Rigidbody2D>();
            animator = GetComponent<Animator>();
            inputHandler = GetComponent<PlayerInputHandler>();

            if (!IsOwner)
            {
                enabled = false;
                return;
            }
            
            // Subscribe to input events
            inputHandler.OnMovementInput += HandleMovementInput;
            inputHandler.OnDashInput += HandleDashInput;
        }
        
        public override void OnNetworkDespawn()
        {
            // Unsubscribe from input events
            if (inputHandler != null)
            {
                inputHandler.OnMovementInput -= HandleMovementInput;
                inputHandler.OnDashInput -= HandleDashInput;
            }
            
            base.OnNetworkDespawn();
        }

        private void HandleMovementInput(Vector2 input)
        {
            if (isDashing) return;
            
            // Apply movement
            rb.linearVelocity = input * moveSpeed;
            
            // Update animation
            UpdateAnimation(input);
        }

        private void HandleDashInput(Vector2 direction)
        {
            if (isDashing || Time.time < lastDashTime + dashCooldown) return;
            
            StartCoroutine(PerformDash(direction));
        }

        private IEnumerator PerformDash(Vector2 direction)
        {
            isDashing = true;
            lastDashTime = Time.time;
            
            rb.linearVelocity = direction * dashForce;
            yield return new WaitForSeconds(dashDuration);
            
            isDashing = false;
            
            // Reapply current movement input after dash ends
            if (inputHandler != null)
            {
                Vector2 currentInput = new Vector2(
                    UnityEngine.Input.GetAxis("Horizontal"),
                    UnityEngine.Input.GetAxis("Vertical")
                );
                
                if (currentInput.sqrMagnitude < 0.1f)
                {
                    rb.linearVelocity = Vector2.zero;
                }
            }
        }

        private void UpdateAnimation(Vector2 movement)
        {
            if (isDashing || animator == null) return;
            
            string newAnim = "idle";

            if (movement.magnitude > 0.1f)
            {
                newAnim = Mathf.Abs(movement.x) > Mathf.Abs(movement.y)
                    ? movement.x > 0 ? "walk_right" : "walk_left"
                    : movement.y > 0 ? "walk_up" : "walk_down";
            }
            else if (!string.IsNullOrEmpty(currentAnimation))
            {
                newAnim = currentAnimation.Replace("walk_", "idle_");
            }

            if (newAnim != currentAnimation)
            {
                currentAnimation = newAnim;
                animator.CrossFade(currentAnimation, 0.1f);
            }
        }
    }
}