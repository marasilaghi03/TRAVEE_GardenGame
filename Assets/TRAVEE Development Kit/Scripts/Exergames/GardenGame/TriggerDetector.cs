using UnityEngine;
using GardenGame;

public interface ISpotTool
{
    Transform SpotProbe { get; }
    void SetCurrentSpot(PlantSpot spot);
    void ClearCurrentSpot(PlantSpot spot);
}

public class TriggerDetector : MonoBehaviour
{
    [Header("Held Tools")]
    public MonoBehaviour HeldTool; 

    [Header("Detection")]
    public float ProbeRadius = 0.08f;
    public LayerMask SpotMask; 

    private ISpotTool _tool;
    private PlantSpot _current;

    void Awake()
    {
        _tool = HeldTool as ISpotTool;
    }

    void Update()
    {
        if ((HeldTool as ISpotTool) != _tool)
            _tool = HeldTool as ISpotTool;

        if (_tool == null || _tool.SpotProbe == null)
        {
            // daca nu mai tinem tool, curatam spotul curent
            if (_current != null)
            {
                _tool?.ClearCurrentSpot(_current);
                _current = null;
            }
            return;
        }

        Vector3 center = _tool.SpotProbe.position;

        Collider[] hits = (SpotMask.value != 0)
            ? Physics.OverlapSphere(center, ProbeRadius, SpotMask, QueryTriggerInteraction.Collide)
            : Physics.OverlapSphere(center, ProbeRadius, ~0, QueryTriggerInteraction.Collide);

        PlantSpot best = null;
        float bestD = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            var spot = hits[i].GetComponentInParent<PlantSpot>();
            if (spot == null) continue;

            float d = (hits[i].ClosestPoint(center) - center).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                best = spot;
            }
        }

        if (best == _current) return;

        // iesim din spot vechi
        if (_current != null)
            _tool.ClearCurrentSpot(_current);

        _current = best;

        // intram in spot nou
        if (_current != null)
            _tool.SetCurrentSpot(_current);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (_tool != null && _tool.SpotProbe != null)
        {
            Gizmos.DrawWireSphere(_tool.SpotProbe.position, ProbeRadius);
        }
    }
#endif
}
