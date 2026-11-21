using UnityEngine;

public class TargetFollow : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float heightOffset = 1.5f;

    private void LateUpdate()
    {
        if (player == null) return;
        
        // Follow the player's position + height, but keep our own rotation
        transform.position = player.position + Vector3.up * heightOffset;
    }
}