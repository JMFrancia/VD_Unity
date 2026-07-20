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

        [Header("State colors")]
        [SerializeField] UiThemeSO theme;

        public void BindOrder(OrderModel order, IReadOnlyDictionary<string, int> held,
            IReadOnlyDictionary<string, string> resourceNames, Action onFill, Action onSkip)
        {
            filledRoot.SetActive(true);
            refillingRoot.SetActive(false);

            Clear(requestList);
            bool canFill = true;
            foreach (var request in order.Requests)
            {
                held.TryGetValue(request.ResourceId, out int have);
                if (have < request.Amount) canFill = false;
                resourceNames.TryGetValue(request.ResourceId, out string name);
                Instantiate(chipTemplate, requestList).Bind(name ?? request.ResourceId, request.Amount, have);
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

        public void BindRefilling(float secondsRemaining)
        {
            filledRoot.SetActive(false);
            refillingRoot.SetActive(true);
            int seconds = Mathf.CeilToInt(Mathf.Max(0f, secondsRemaining));
            refillingText.text = $"Refilling · {seconds / 60}:{seconds % 60:00}";
        }

        static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }
    }
}
