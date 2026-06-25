using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConfirmQueueManager : MonoBehaviour
{
    public static ConfirmQueueManager Instance { get; private set; }

    // 弹窗队列
    private Queue<Action> confirmQueue = new Queue<Action>();
    private bool isConfirmShowing = false;

    // 目标选择队列（统一管理所有选择模式）
    private Queue<Action> selectionQueue = new Queue<Action>();
    private bool isSelectionActive = false;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ========== 确认弹窗队列 ==========

    public void EnqueueConfirm(string message, Action<Action> onYes, Action<Action> onNo = null)
    {
        Action showAction = () =>
        {
            ConfirmPanel.Instance.Show(message,
                () =>
                {
                    onYes?.Invoke(() => StartCoroutine(DelayedShowNextConfirm()));
                },
                () =>
                {
                    if (onNo != null)
                        onNo?.Invoke(() => StartCoroutine(DelayedShowNextConfirm()));
                    else
                        StartCoroutine(DelayedShowNextConfirm());
                }
            );
        };

        confirmQueue.Enqueue(showAction);

        if (!isConfirmShowing && !isSelectionActive)
        {
            ShowNextConfirm();
        }
    }

    void ShowNextConfirm()
    {
        if (confirmQueue.Count > 0)
        {
            isConfirmShowing = true;
            confirmQueue.Dequeue().Invoke();
        }
        else
        {
            isConfirmShowing = false;
            TryProcessNext(); // 检查是否有选择队列在等待
        }
    }

    IEnumerator DelayedShowNextConfirm()
    {
        yield return null;
        ShowNextConfirm();
    }

    // ========== 目标选择队列 ==========

    /// <summary>
    /// 加入目标选择队列。selectionAction 接收一个 Action 参数，
    /// 选择完成后调用该 Action 通知队列继续。
    /// </summary>
    public void EnqueueSelection(Action<Action> selectionAction)
    {
        selectionQueue.Enqueue(() =>
        {
            isSelectionActive = true;
            selectionAction(() =>
            {
                // 选择完成回调
                StartCoroutine(DelayedSelectionComplete());
            });
        });

        if (!isSelectionActive && !isConfirmShowing)
        {
            StartNextSelection();
        }
    }

    void StartNextSelection()
    {
        if (selectionQueue.Count > 0)
        {
            selectionQueue.Dequeue().Invoke();
        }
    }

    IEnumerator DelayedSelectionComplete()
    {
        yield return null;
        isSelectionActive = false;

        // 先检查弹窗队列
        if (confirmQueue.Count > 0)
        {
            ShowNextConfirm();
        }
        // 再检查选择队列
        else if (selectionQueue.Count > 0)
        {
            StartNextSelection();
        }
    }

    /// <summary>
    /// 弹窗处理完毕后尝试继续处理选择队列
    /// </summary>
    void TryProcessNext()
    {
        if (selectionQueue.Count > 0 && !isSelectionActive)
        {
            StartNextSelection();
        }
    }

    // ========== 静态工具方法 ==========

    public static void EnterSelectionMode()
    {
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card != null) card.SetActive(false);
        }
        Card3DHover.allowDiscard = false;
    }
    public static void ExitSelectionMode()
    {
        foreach (GameObject card in NetworkPlayer.Local.handCards)
        {
            if (card != null) card.SetActive(true);
        }
        HandManager hm = FindObjectOfType<HandManager>();
        hm?.SetHandAreaRaycast(true);
        Card3DHover.allowDiscard = true;
    }

    public static List<GameObject> FilterHandCards(Func<CardInstance, bool> condition)
    {
        List<GameObject> validCards = new List<GameObject>();
        NetworkPlayer player = NetworkPlayer.Local;
        if (player == null) return validCards;

        foreach (GameObject card in player.handCards)
        {
            if (card == null) continue;
            CardInstance ci = card.GetComponent<CardInstance>();
            if (ci != null && condition(ci))
            {
                validCards.Add(card);
                card.SetActive(true);
            }
            else
            {
                card.SetActive(false);
            }
        }
        return validCards;
    }

    public static void RestoreAllHandCards()
    {
        NetworkPlayer player = NetworkPlayer.Local;
        if (player == null) return;
        foreach (GameObject card in player.handCards)
        {
            if (card != null) card.SetActive(true);
        }
        HandManager hm = FindObjectOfType<HandManager>();
        hm?.RefreshLayout(true);
    }

    // ========== 队列状态检查 ==========

    public bool HasPendingSelections()
    {
        return selectionQueue.Count > 0 || isSelectionActive;
    }

    public bool HasPendingConfirms()
    {
        return confirmQueue.Count > 0 || isConfirmShowing;
    }

    public bool IsBusy()
    {
        return HasPendingSelections() || HasPendingConfirms();
    }
}