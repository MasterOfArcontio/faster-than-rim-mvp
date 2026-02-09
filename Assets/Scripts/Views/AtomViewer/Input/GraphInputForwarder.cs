using UnityEngine;
using UnityEngine.EventSystems;

namespace SocialViewer.UI.Graph
{
    /// <summary>
    /// Invia l'input pan/zoom da un nodo alla viewport (GraphPanZoom)
    /// così pan/zoom funziona anche quando il puntatore del mouse è sopra i nodi.
    /// LA dipendenza è "iniettata" dallo spawner/controller (per evitare l'uso deprecabile del find).
    /// </summary>
    public class GraphInputForwarder : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        [SerializeField] private GameObject viewportReceiver; // GraphViewport gameobject (come GraphPanZoom)

        public void SetViewportReceiver(GameObject receiver)
        {
            viewportReceiver = receiver;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (viewportReceiver == null) return;

            if (eventData.button == PointerEventData.InputButton.Right)
                ExecuteEvents.Execute<IBeginDragHandler>(viewportReceiver, eventData, ExecuteEvents.beginDragHandler);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (viewportReceiver == null) return;

            if (eventData.button == PointerEventData.InputButton.Right)
                ExecuteEvents.Execute<IDragHandler>(viewportReceiver, eventData, ExecuteEvents.dragHandler);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (viewportReceiver == null) return;

            if (eventData.button == PointerEventData.InputButton.Right)
                ExecuteEvents.Execute<IEndDragHandler>(viewportReceiver, eventData, ExecuteEvents.endDragHandler);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (viewportReceiver == null) return;

            ExecuteEvents.Execute<IScrollHandler>(viewportReceiver, eventData, ExecuteEvents.scrollHandler);
        }
    }
}
