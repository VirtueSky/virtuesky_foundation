using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pancake.Monetization
{
    [Serializable]
    // avoid config name with AppLovinSettings
    public class ApplovinSettings
    {
        [SerializeField] private bool enable;
        [SerializeField] private string sdkKey;
        [SerializeField] private ApplovinBannerUnit bannerAdUnit;
        [SerializeField] private ApplovinInterstitialUnit interstitialAdUnit;
        [SerializeField] private ApplovinRewardedUnit rewardedAdUnit;
        [SerializeField] private ApplovinAppOpenUnit appOpenAdUnit;
        [SerializeField] private ApplovinRewardedInterstitialUnit rewardedInterstitialAdUnit;
        [SerializeField] private bool enableAgeRestrictedUser;
        [SerializeField] private bool enableRequestAdAfterHidden = true;
        [SerializeField] private bool enableMaxAdReview;

#if UNITY_EDITOR
        [NonSerialized] internal List<MaxNetwork> editorListNetwork = new List<MaxNetwork>();
        [NonSerialized] internal MaxNetwork editorImportingNetwork;
        
#endif

        public bool Enable => enable;
        public string SdkKey => sdkKey;
        public ApplovinBannerUnit BannerAdUnit => bannerAdUnit;
        public ApplovinInterstitialUnit InterstitialAdUnit => interstitialAdUnit;
        public ApplovinRewardedUnit RewardedAdUnit => rewardedAdUnit;
        public ApplovinAppOpenUnit AppOpenAdUnit => appOpenAdUnit;

        public ApplovinRewardedInterstitialUnit RewardedInterstitialAdUnit => rewardedInterstitialAdUnit;

        public bool EnableAgeRestrictedUser => enableAgeRestrictedUser;

        public bool EnableRequestAdAfterHidden => enableRequestAdAfterHidden;

        public bool EnableMaxAdReview => enableMaxAdReview;
    }
}