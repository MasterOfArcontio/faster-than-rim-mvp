using UnityEngine;

public class NPCNodeCollision : MonoBehaviour
{
    [Tooltip("Raggio collisione in unità di anchoredPosition (UI units).")]
    public float radius = 24f;

    /// <summary>
    /// RectTransform del nodo UI (la sfera). Serve per leggere/scrivere anchoredPosition.
    /// </summary>
    public RectTransform Rect { get; private set; }

    /// <summary>
    /// ID LOGICO del nodo (quello del simulatore/spawner).
    /// Deve essere stabile e coerente (es: 0..N-1 o ArcontioId).
    /// </summary>
    public int Id { get; private set; } = -1;

    private void Awake()
    {
        // Cache del RectTransform (il nodo è UI)
        Rect = GetComponent<RectTransform>();

        // NON impostiamo più Id = GetInstanceID() come valore "reale".
        // GetInstanceID() è un ID interno Unity (spesso negativo) e NON coincide con gli ID del simulatore.
        //
        // Se vuoi un fallback per debug, lo useremo solo se Id non è stato assegnato dallo spawner.
        if (Id < 0)
        {
            // Fallback (solo debug): se ti dimentichi di chiamare SetId, almeno avrai un valore.
            // Ma NON deve essere il comportamento normale del progetto.
            Id = GetInstanceID();
        }
    }

    /// <summary>
    /// Metodo da chiamare dallo spawner subito dopo Instantiate.
    /// Imposta l'ID logico del nodo.
    /// </summary>
    public void SetId(int id)
    {
        Id = id;
    }

    private void OnEnable()
    {
        NodeRegistry.Instance?.Register(this);
    }

    private void OnDisable()
    {
        NodeRegistry.Instance?.Unregister(this);
    }
}
