namespace SocialViewer.UI.Graph
{
    /// <summary>
    /// Stato globale minimo per coordinare input tra:
    /// - pan/zoom del grafo
    /// - drag dei nodi
    /// - menu contestuale sui nodi
    /// 
    /// È volutamente semplice: due flag.
    /// </summary>
    public static class GraphInteractionState
    {
        public static bool IsPanning;
        public static bool IsDraggingNode;
    }
}
