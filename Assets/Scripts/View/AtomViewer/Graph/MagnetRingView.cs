using UnityEngine;
using UnityEngine.UI;

namespace SocialViewer.UI.Graph
{
    /// <summary>
    /// Visualizza un anello UI attorno al nodo magnete.
    /// Richiede un Image con sprite circolare (outline o ring).
    /// L’anello è gestito in spazio UI (RectTransform), quindi segue pan/zoom del GraphContent.
    /// </summary>
    public class MagnetRingView : MonoBehaviour
    {
        [SerializeField] private RectTransform ringRect;
        [SerializeField] private Image ringImage;

        private void Awake()
        {
            if (ringRect == null) ringRect = (RectTransform)transform;
            if (ringImage == null) ringImage = GetComponent<Image>();

            Hide();
        }

        /// <summary>
        /// Mostra l’anello centrato sul magnetRect con raggio in UI units.
        /// </summary>
        public void Show(RectTransform magnetRect, float radius)
        {
            if (ringRect == null || magnetRect == null) return;

            gameObject.SetActive(true);

            // Centro anello sul magnete
            ringRect.anchoredPosition = magnetRect.anchoredPosition;

            // Diametro = 2 * raggio
            float diameter = Mathf.Max(2f, radius * 2f);
            ringRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, diameter);
            ringRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, diameter);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
