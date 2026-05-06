using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deals environmental hit damage to a player when the obstacle collider touches the player's collider.
/// </summary>
[DisallowMultipleComponent]
public class ObstacleHitDealer : MonoBehaviour
{
    [Header("Damage")]
    [Tooltip("Damage applied to the player when the player touches this obstacle.")]
    [Min(0f)]
    [SerializeField] private float _damage = 10f; // Damage value sent through the hit system when a valid player touches this obstacle.

    [Tooltip("Status tag passed to HitReceiver listeners so hit reactions can distinguish obstacle hits.")]
    [SerializeField] private string _statusTag = "Obstacle"; // Hit status tag used by downstream hit reaction systems.

    [Header("Target Filter")]
    [Tooltip("Layers that can be damaged by this obstacle.")]
    [SerializeField] private LayerMask _targetLayerMask = ~0; // Layer filter that limits which collider objects can receive obstacle damage.

    [Tooltip("When enabled, only colliders with the configured target tag can receive obstacle damage.")]
    [SerializeField] private bool _requireTargetTag = true; // Whether the target tag must match before damage is applied.

    [Tooltip("Tag required for a collider to be treated as a player target.")]
    [SerializeField] private string _targetTag = "Player"; // Required player tag when target tag filtering is enabled.

    [Header("Contact Rules")]
    [Tooltip("When enabled, the obstacle can damage a player repeatedly while the player remains touching it.")]
    [SerializeField] private bool _repeatDamageWhileTouching; // Whether staying in contact can trigger additional hits after the interval.

    [Tooltip("Minimum seconds between repeated hits against the same player while contact continues.")]
    [Min(0f)]
    [SerializeField] private float _repeatDamageInterval = 1f; // Per-target cooldown for repeated obstacle hits.

    private readonly Dictionary<int, int> _contactCountsByTarget = new Dictionary<int, int>(); // Active collider contact count per target receiver instance.
    private readonly Dictionary<int, float> _nextHitTimeByTarget = new Dictionary<int, float>(); // Next allowed hit time per target receiver instance.
    private int _hitSequence; // Monotonic sequence used to build unique hit identifiers for HitReceiver duplicate protection.

    /// <summary>
    /// Clamps inspector values to safe runtime ranges.
    /// </summary>
    private void OnValidate()
    {
        _damage = Mathf.Max(0f, _damage);
        _repeatDamageInterval = Mathf.Max(0f, _repeatDamageInterval);
    }

    /// <summary>
    /// Clears cached contact state when the obstacle is disabled.
    /// </summary>
    private void OnDisable()
    {
        _contactCountsByTarget.Clear();
        _nextHitTimeByTarget.Clear();
    }

    /// <summary>
    /// Applies obstacle damage when a trigger collider first touches the player.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleContactEnter(other, other.ClosestPoint(transform.position));
    }

    /// <summary>
    /// Applies repeated obstacle damage while a trigger collider keeps touching the player.
    /// </summary>
    private void OnTriggerStay2D(Collider2D other)
    {
        HandleContactStay(other, other.ClosestPoint(transform.position));
    }

    /// <summary>
    /// Releases cached contact state when a trigger collider stops touching the player.
    /// </summary>
    private void OnTriggerExit2D(Collider2D other)
    {
        HandleContactExit(other);
    }

    /// <summary>
    /// Applies obstacle damage when a collision collider first touches the player.
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleContactEnter(collision.collider, GetCollisionPoint(collision));
    }

    /// <summary>
    /// Applies repeated obstacle damage while a collision collider keeps touching the player.
    /// </summary>
    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleContactStay(collision.collider, GetCollisionPoint(collision));
    }

    /// <summary>
    /// Releases cached contact state when a collision collider stops touching the player.
    /// </summary>
    private void OnCollisionExit2D(Collision2D collision)
    {
        HandleContactExit(collision.collider);
    }

    /// <summary>
    /// Registers a new contact and sends the first hit immediately.
    /// </summary>
    private void HandleContactEnter(Collider2D other, Vector2 hitPoint)
    {
        if (!TryResolveTarget(other, out HitReceiver receiver))
        {
            return;
        }

        int targetId = receiver.GetInstanceID(); // Stable runtime key for this receiver during the current play session.
        bool isNewTargetContact = !_contactCountsByTarget.TryGetValue(targetId, out int contactCount); // Whether this receiver had no active obstacle contacts before this enter event.
        if (isNewTargetContact)
        {
            _contactCountsByTarget.Add(targetId, 1);
        }
        else
        {
            _contactCountsByTarget[targetId] = contactCount + 1;
        }

        if (isNewTargetContact)
        {
            TrySendHit(receiver, hitPoint, true);
        }
    }

    /// <summary>
    /// Sends another hit during sustained contact when repeated damage is enabled and the target cooldown has elapsed.
    /// </summary>
    private void HandleContactStay(Collider2D other, Vector2 hitPoint)
    {
        if (!_repeatDamageWhileTouching)
        {
            return;
        }

        if (!TryResolveTarget(other, out HitReceiver receiver))
        {
            return;
        }

        TrySendHit(receiver, hitPoint, false);
    }

    /// <summary>
    /// Decreases the active contact count for a target and removes its cached state when contact fully ends.
    /// </summary>
    private void HandleContactExit(Collider2D other)
    {
        if (!TryResolveTarget(other, out HitReceiver receiver))
        {
            return;
        }

        int targetId = receiver.GetInstanceID(); // Stable runtime key for this receiver during the current play session.
        if (!_contactCountsByTarget.TryGetValue(targetId, out int contactCount))
        {
            return;
        }

        contactCount--;
        if (contactCount > 0)
        {
            _contactCountsByTarget[targetId] = contactCount;
            return;
        }

        _contactCountsByTarget.Remove(targetId);
        _nextHitTimeByTarget.Remove(targetId);
    }

    /// <summary>
    /// Resolves a collider into a valid player HitReceiver after layer, tag, and component checks.
    /// </summary>
    private bool TryResolveTarget(Collider2D other, out HitReceiver receiver)
    {
        receiver = null;
        if (other == null)
        {
            return false;
        }

        if (!IsLayerAllowed(other.gameObject.layer))
        {
            return false;
        }

        if (_requireTargetTag && !HasTargetTag(other))
        {
            return false;
        }

        receiver = other.GetComponent<HitReceiver>();
        if (receiver == null)
        {
            receiver = other.GetComponentInParent<HitReceiver>();
        }

        if (receiver == null)
        {
            receiver = other.GetComponentInChildren<HitReceiver>();
        }

        return receiver != null && receiver.gameObject != gameObject;
    }

    /// <summary>
    /// Sends a hit request to the target receiver when the damage and cooldown rules allow it.
    /// </summary>
    private bool TrySendHit(HitReceiver receiver, Vector2 hitPoint, bool ignoreCooldown)
    {
        if (receiver == null || _damage <= 0f)
        {
            return false;
        }

        int targetId = receiver.GetInstanceID(); // Stable runtime key for this receiver during the current play session.
        if (!ignoreCooldown && _nextHitTimeByTarget.TryGetValue(targetId, out float nextHitTime) && Time.time < nextHitTime)
        {
            return false;
        }

        HitRequest request = BuildHitRequest(receiver, hitPoint);
        HitResult result = receiver.ReceiveHit(request);

        if (result.IsAccepted)
        {
            _nextHitTimeByTarget[targetId] = Time.time + _repeatDamageInterval;
        }

        return result.IsAccepted;
    }

    /// <summary>
    /// Builds the obstacle hit request in the format expected by the shared hit system.
    /// </summary>
    private HitRequest BuildHitRequest(HitReceiver receiver, Vector2 hitPoint)
    {
        _hitSequence++;
        Vector3 receiverPosition = receiver.transform.position; // Target position used to calculate hit direction from obstacle to player.
        Vector3 hitDirection = (receiverPosition - transform.position).normalized; // Direction passed to hit listeners for reaction logic.

        return new HitRequest(
            hitId: $"{gameObject.GetInstanceID()}:{receiver.GetInstanceID()}:Obstacle:{_hitSequence}",
            rawDamage: _damage,
            attacker: gameObject,
            hitPoint: hitPoint,
            hitDirection: hitDirection,
            statusTag: _statusTag,
            requestTime: Time.time);
    }

    /// <summary>
    /// Checks whether a target layer is included in the configured layer mask.
    /// </summary>
    private bool IsLayerAllowed(int layer)
    {
        int layerBit = 1 << layer; // Bit flag for the candidate collider layer.
        return (_targetLayerMask.value & layerBit) != 0;
    }

    /// <summary>
    /// Checks the collider and its parent hierarchy for the configured player tag.
    /// </summary>
    private bool HasTargetTag(Collider2D other)
    {
        if (string.IsNullOrWhiteSpace(_targetTag))
        {
            return true;
        }

        if (other.CompareTag(_targetTag))
        {
            return true;
        }

        Transform parent = other.transform.parent; // Parent traversal cursor used when the player collider is on a child object.
        while (parent != null)
        {
            if (parent.CompareTag(_targetTag))
            {
                return true;
            }

            parent = parent.parent;
        }

        return false;
    }

    /// <summary>
    /// Returns the first collision contact point, or the collider position when Unity provides no contacts.
    /// </summary>
    private Vector2 GetCollisionPoint(Collision2D collision)
    {
        if (collision.contactCount <= 0)
        {
            return collision.collider.transform.position;
        }

        return collision.GetContact(0).point;
    }
}
