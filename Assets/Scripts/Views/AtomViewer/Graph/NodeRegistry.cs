using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro centralizzato dei nodi UI (sfere) attivi.
/// 
/// - Registra NPCNodeCollision quando i GameObject vengono abilitati.
/// - Permette lookup veloce per ID logico (quello assegnato dallo spawner/simulatore).
/// - Espone API utili ai sistemi grafici (RectTransform del nodo).
/// </summary>
public class NodeRegistry : MonoBehaviour
{
    public static NodeRegistry Instance { get; private set; }

    private readonly List<NPCNodeCollision> nodes = new();
    private readonly Dictionary<int, NPCNodeCollision> byId = new();

    public event Action<NPCNodeCollision> OnNodeRegistered;
    public event Action<NPCNodeCollision> OnNodeUnregistered;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Register(NPCNodeCollision node)
    {
        if (node == null) return;

        if (!nodes.Contains(node))
            nodes.Add(node);

        // Usa l'ID logico (node.Id), non GetInstanceID
        byId[node.Id] = node;
        Debug.Log($"[NodeRegistry] Registro NodeId={node.Id}");
        OnNodeRegistered?.Invoke(node);
    }

    public void Unregister(NPCNodeCollision node)
    {
        if (node == null) return;

        nodes.Remove(node);

        // Rimuoviamo dal dizionario solo se punta ancora a questo oggetto
        int id = node.Id;
        if (byId.TryGetValue(id, out var current) && current == node)
            byId.Remove(id);

        OnNodeUnregistered?.Invoke(node);
    }

    public IReadOnlyList<NPCNodeCollision> AllNodes => nodes;

    public bool TryGetNode(int id, out NPCNodeCollision node) => byId.TryGetValue(id, out node);

    public bool TryGetNodeRect(int id, out RectTransform rect)
    {
        rect = null;
        if (!byId.TryGetValue(id, out var node) || node == null) return false;

        rect = node.Rect;
        return rect != null;
    }

    /// <summary>
    /// Riempie una lista con tutti gli ID attivi. Evita allocazioni in Update().
    /// </summary>
    public void GetAllNodeIds(List<int> output)
    {
        output.Clear();
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n == null) continue;
            output.Add(n.Id);
        }
    }
}
