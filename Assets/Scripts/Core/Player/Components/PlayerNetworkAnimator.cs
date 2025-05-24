// Location: Core/Player/Components/PlayerNetworkAnimator.cs
using Unity.Netcode.Components;

namespace Core.Player.Components
{
    public class PlayerNetworkAnimator : NetworkAnimator
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false; // This is a client-side authority setup
        }
    }
}