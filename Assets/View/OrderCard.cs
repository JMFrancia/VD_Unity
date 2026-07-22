using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VoidDay.Core.Model;
using VoidDay.Data;

namespace VoidDay.View
{
    /// pattern.orderCard (mockup 14:2 / 15:2) — one filled order slot. The look is authored in the prefab;
    /// this binds it to an order and the player's current holdings. Requested-good chips are dynamic count,
    /// so they instantiate an authored chip template.
    ///
    /// The card renders BOTH states of a slot: a filled card, or the dashed "Refilling · 0:47" placeholder.
    /// One prefab with two authored sub-objects beats two prefabs that drift apart.
    public sealed class OrderCard : MonoBehaviour
    {
        [Header("Filled state")]
        [SerializeField] GameObject filledRoot;
        [SerializeField] Transform requestList;
        [SerializeField] OrderGoodChip chipTemplate;
        [SerializeField] Text cashText;
        [SerializeField] Text xpText;
        [SerializeField] Button fillButton;
        [SerializeField] Image fillButtonImage;
        [SerializeField] Text fillLabel;
        [SerializeField] Button skipButton;

        [Header("Refilling state")]
        [SerializeField] GameObject refillingRoot;
        [SerializeField] Text refillingText;

        [Tooltip("Gem skip. NOT skipButton above — that one discards a filled order and costs nothing.")]
        [SerializeField] Button skipTimerButton;
        [SerializeField] Text skipTimerCostText;

        [Header("State colors")]
        [SerializeField] UiThemeSO theme;

        public void BindOrder(OrderModel order, IReadOnlyDictionary<string, int> held,
            IReadOnlyDictionary<string, ResourceSO> resources, Action onFill, Action onSkip)
        {
            filledRoot.SetActive(true);
            refillingRoot.SetActive(false);

            Clear(requestList);
            bool canFill = true;
            foreach (var request in order.Requests)
            {
                held.TryGetValue(request.ResourceId, out int have);
                if (have < request.Amount) canFill = false;
                resources.TryGetValue(request.ResourceId, out var so);
                Instantiate(chipTemplate, requestList)
                    .Bind(so != null ? so.displayName : request.ResourceId, so != null ? so.icon : null, request.Amount, have);
            }

            cashText.text = order.Cash.ToString();
            xpText.gameObject.SetActive(false); // XP hidden until the level/XP milestone (M8); cash reward stays

            fillButton.onClick.RemoveAllListeners();
            fillButton.interactable = canFill;
            fillButtonImage.color = canFill ? theme.accent : theme.lockedBg;
            fillLabel.color = canFill ? theme.accentText : theme.lockedText;
            fillButton.onClick.AddListener(() => onFill());

            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(() => onSkip());
        }

        /// gemCost is what a skip costs RIGHT NOW — priced by Core and handed in, never re-derived here
        /// (CLAUDE.md: one rule, one home). Zero or less means the timer isn't skippable and the control hides.
        public void BindRefilling(float secondsRemaining, int gemCost, Action onSkipTimer)
        {
            filledRoot.SetActive(false);
            refillingRoot.SetActive(true);
            int seconds = Mathf.CeilToInt(Mathf.Max(0f, secondsRemaining));
            refillingText.text = $"Refilling · {seconds / 60}:{seconds % 60:00}";

            skipTimerButton.gameObject.SetActive(gemCost > 0);
            if (gemCost <= 0) return;
            skipTimerCostText.text = gemCost.ToString();
            skipTimerButton.onClick.RemoveAllListeners();
            skipTimerButton.onClick.AddListener(() => onSkipTimer());
        }

        static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }
    }
}
