using Mirror;
using UnityEngine;

/// <summary>
/// Triggers board model sync from the server to the non-host client.
/// Uses NetworkPlayer (which HAS NetworkIdentity) for the actual TargetRpc.
/// Attach to CardCanvas (same GameObject as BoardManager).
/// </summary>
public class BoardSyncManager : MonoBehaviour
{
    public static BoardSyncManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Server: read host-side slots 6-11, send to the other client via NetworkPlayer.
    /// </summary>
    public void SyncHostBoard()
    {
        if (!NetworkServer.active) return;

        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;

        string[] hostSix = new string[6];
        for (int i = 0; i < 6; i++)
        {
            BoardSlot slot = bm.GetSlot(i + 6);
            if (slot?.currentCard3D != null)
            {
                var c3d = slot.currentCard3D.GetComponent<Card3DInstance>();
                hostSix[i] = c3d?.cardInstance?.templateID ?? "";
            }
            else
            {
                hostSix[i] = "";
            }
        }

        foreach (var kv in NetworkServer.connections)
        {
            if (kv.Value != NetworkPlayer.Local?.connectionToClient)
            {
                NetworkPlayer.Local?.TargetSyncHostBoard(kv.Value, hostSix);
                return;
            }
        }
    }

    /// <summary>
    /// Client: called by NetworkPlayer.TargetSyncHostBoard TargetRpc.
    /// Rebuilds enemy slots 0-5 from the host's slot data.
    /// </summary>
    public void ApplyHostBoard(string[] hostTemplates)
    {
        BoardManager bm = FindObjectOfType<BoardManager>();
        if (bm == null) return;
        HandManager hm = FindObjectOfType<HandManager>();

        for (int i = 0; i < 6; i++)
        {
            int enemySlotID = i;
            BoardSlot slot = bm.GetSlot(enemySlotID);
            if (slot == null) continue;

            string want = (i < hostTemplates.Length) ? hostTemplates[i] : "";
            string current = "";
            if (slot.currentCard3D != null)
            {
                var c3d = slot.currentCard3D.GetComponent<Card3DInstance>();
                current = c3d?.cardInstance?.templateID ?? "";
            }

            if (current == want) continue;

            if (slot.currentCard3D != null)
            {
                Destroy(slot.currentCard3D);
                slot.SetCard(null);
            }

            if (!string.IsNullOrEmpty(want) && hm != null)
            {
                CardData template = CardDatabase.Instance?.GetTemplate(want);
                if (template?.prefab3D != null)
                {
                    Vector3 pos = hm.GetSlotWorldPosition(enemySlotID);
                    GameObject model = Instantiate(template.prefab3D, pos, Quaternion.Euler(0, 180, 0));
                    model.name = want + "_enemy";
                    Card3DInstance c3d = model.GetComponent<Card3DInstance>();
                    if (c3d != null)
                    {
                        CardInstance ci = model.AddComponent<CardInstance>();
                        ci.InitFromTemplate(template, 0);
                        c3d.cardInstance = ci;
                        c3d.UpdateValues();
                    }
                    slot.SetCard(model);
                }
            }
        }
    }
}
