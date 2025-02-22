using AvatarSystem;
using DCL;
using DCL.ProfanityFiltering;
using DCL.Social.Friends;
using DCl.Social.Passports;
using DCL.Social.Passports;
using DCLServices.Lambdas.LandsService;
using DCLServices.Lambdas.NamesService;
using DCLServices.WearablesCatalogService;
using SocialFeaturesAnalytics;
using UnityEngine;

public class PlayerPassportPlugin : IPlugin
{
    private readonly PlayerPassportHUDController passportController;

    public PlayerPassportPlugin()
    {
        PlayerPassportReferenceContainer referenceContainer = Object.Instantiate(Resources.Load<GameObject>("PlayerPassport")).GetComponent<PlayerPassportReferenceContainer>();
        var wearablesCatalogService = Environment.i.serviceLocator.Get<IWearablesCatalogService>();

        passportController = new PlayerPassportHUDController(
                        referenceContainer.PassportView,
                        new PassportPlayerInfoComponentController(
                            Resources.Load<StringVariable>("CurrentPlayerInfoCardId"),
                            referenceContainer.PlayerInfoView,
                            DataStore.i,
                            Environment.i.serviceLocator.Get<IProfanityFilter>(),
                            FriendsController.i,
                            new UserProfileWebInterfaceBridge(),
                            new SocialAnalytics(
                                Environment.i.platform.serviceProviders.analytics,
                                new UserProfileWebInterfaceBridge()),
                            Environment.i.platform.clipboard,
                            new WebInterfacePassportApiBridge()),
                        new PassportPlayerPreviewComponentController(
                            referenceContainer.PlayerPreviewView,
                            new SocialAnalytics(
                                Environment.i.platform.serviceProviders.analytics,
                                new UserProfileWebInterfaceBridge())),
                        new PassportNavigationComponentController(
                            referenceContainer.PassportNavigationView,
                            Environment.i.serviceLocator.Get<IProfanityFilter>(),
                            new WearableItemResolver(wearablesCatalogService),
                            wearablesCatalogService,
                            Environment.i.serviceLocator.Get<IEmotesCatalogService>(),
                            Environment.i.serviceLocator.Get<INamesService>(),
                            Environment.i.serviceLocator.Get<ILandsService>(),
                            new UserProfileWebInterfaceBridge(),
                            DataStore.i),
                        Resources.Load<StringVariable>("CurrentPlayerInfoCardId"),
                        new UserProfileWebInterfaceBridge(),
                        new WebInterfacePassportApiBridge(),
                        new SocialAnalytics(
                            Environment.i.platform.serviceProviders.analytics,
                            new UserProfileWebInterfaceBridge()),
                        DataStore.i,
                        SceneReferences.i.mouseCatcher,
                        CommonScriptableObjects.playerInfoCardVisibleState);
    }

    public void Dispose()
    {
        passportController?.Dispose();
    }
}
