using UnityEngine;
using UnityEngine.EventSystems;

namespace SocialViewer.UI.Overlay
{
    /// <summary>
    /// Questo script va sul Panel "ModalBlocker".
    /// Il ModalBlocker è un pannello trasparente a schermo intero con Raycast Target ON,
    /// quindi intercetta click e impedisce interazioni col grafo.
    ///
    /// Qui usiamo il click sul blocker per chiudere il menu contestuale.
    /// </summary>
    public class ModalBlockerClose : MonoBehaviour, IPointerDownHandler
    {
        [Tooltip("Riferimento al ContextMenuController da chiudere quando clicco fuori.")]
        [SerializeField] private ContextMenuController contextMenu;

        public void OnPointerDown(PointerEventData eventData)
        {
            // Appena clicchi sullo sfondo (fuori dal menu),
            // chiudiamo il menu e disattiviamo il blocker.
            if (contextMenu != null)
                contextMenu.Hide();
        }
    }
}
