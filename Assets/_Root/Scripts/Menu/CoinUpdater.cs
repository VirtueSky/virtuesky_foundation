using System;
using Alchemy.Inspector;
using Pancake.Scriptable;
using TMPro;
using UnityEngine;

namespace Pancake.SceneFlow
{
    [EditorIcon("icon_default")]
    public class CoinUpdater : GameComponent
    {
        [Blockquote("Update text coin with current coin of user")] [SerializeField]
        private TextMeshProUGUI textCoin;

        [Blockquote("Update text coin with temp value")] [SerializeField]
        private ScriptableEventNoParam eventUpdateCoin;

        [SerializeField] private ScriptableEventInt updateCoinWithValue;

        private void Start() { OnNoticeUpdateCoin(); }

        private void OnEnable()
        {
            if (eventUpdateCoin != null) eventUpdateCoin.OnRaised += OnNoticeUpdateCoin;
            if (updateCoinWithValue != null) updateCoinWithValue.OnRaised += OnNoticeUpdateCoin;
        }

        private void OnDisable()
        {
            if (eventUpdateCoin != null) eventUpdateCoin.OnRaised -= OnNoticeUpdateCoin;
            if (updateCoinWithValue != null) updateCoinWithValue.OnRaised -= OnNoticeUpdateCoin;
        }

        private void OnNoticeUpdateCoin(int value)
        {
            int previousCoin = int.Parse(textCoin.text);
            textCoin.text = $"{previousCoin + value}";
        }

        private void OnNoticeUpdateCoin() { textCoin.text = UserData.GetCurrentCoin().ToString(); }
    }
}