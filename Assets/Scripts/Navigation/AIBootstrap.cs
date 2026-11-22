using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(GraphManager))]
public sealed class AIBootstrap : MonoBehaviour
{
    private void Start()
    {
        GraphManager graph = GetComponent<GraphManager>();
        graph.CollectNodes();
        var roots = gameObject.scene.GetRootGameObjects();
        foreach (var root in roots)
            foreach (var door in root.GetComponentsInChildren<InteractableDoor>(true))
                door.Bind(graph);

        Transform player = null;
        foreach (var candidate in GameObject.FindGameObjectsWithTag("Player"))
            if (candidate.scene == gameObject.scene) { player = candidate.transform; break; }
        if (player == null)
        {
            Debug.LogError("AI: в сцене не найден активный игрок с тегом Player.", this);
            return;
        }
        foreach (var root in roots)
            foreach (var npc in root.GetComponentsInChildren<RifleNpc>(true))
            {
                if (!npc.TryGetComponent<NPCChaser>(out var chaser)) chaser = npc.gameObject.AddComponent<NPCChaser>();
                chaser.Initialize(graph, player);
                npc.SetChaser(chaser, player);
            }
    }
}
