using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class UIRaycastDebugger : MonoBehaviour
{
    private PointerEventData _ped;
    private readonly List<RaycastResult> _results = new();

    void Update()
    {
        if (EventSystem.current == null) return;

        // Mouse potrebbe essere null su alcune piattaforme / se non è presente
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (_ped == null)
            _ped = new PointerEventData(EventSystem.current);

        // Input System: posizione mouse in pixel
        _ped.position = mouse.position.ReadValue();

        _results.Clear();
        EventSystem.current.RaycastAll(_ped, _results);

        if (_results.Count > 0)
        {
            var top = _results[0];
            //Debug.Log($"[UIRaycast] TOP = {top.gameObject.name}");
        }
        else
        {
            //Debug.Log("[UIRaycast] Nessun target UI sotto il mouse");
        }
    }
}
