using DCL;
using DCL.Social.Friends;
using ExploreV2Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static HotScenesController;
using Environment = DCL.Environment;

public class FavoritesesSubSectionComponentController : IFavoritesSubSectionComponentController, IPlacesAndEventsAPIRequester
{
    public event Action OnCloseExploreV2;

    internal const int INITIAL_NUMBER_OF_ROWS = 5;
    private const int SHOW_MORE_ROWS_INCREMENT = 3;

    internal readonly IFavoritesSubSectionComponentView view;
    internal readonly IPlacesAPIController placesAPIApiController;
    internal readonly FriendTrackerController friendsTrackerController;
    private readonly IExploreV2Analytics exploreV2Analytics;
    private readonly DataStore dataStore;

    internal readonly PlaceAndEventsCardsReloader cardsReloader;

    internal List<HotSceneInfo> favoritesFromAPI = new ();
    internal int availableUISlots;

    public FavoritesesSubSectionComponentController(IFavoritesSubSectionComponentView view, IPlacesAPIController placesAPI, IFriendsController friendsController, IExploreV2Analytics exploreV2Analytics, DataStore dataStore)
    {

        this.dataStore = dataStore;
        this.dataStore.channels.currentJoinChannelModal.OnChange += OnChannelToJoinChanged;
        this.view = view;
        this.exploreV2Analytics = exploreV2Analytics;
        placesAPIApiController = placesAPI;

        this.view.OnReady += FirstLoading;
        this.view.OnInfoClicked += ShowPlaceDetailedInfo;
        this.view.OnJumpInClicked += OnJumpInToPlace;
        this.view.OnShowMoreFavoritesClicked += ShowMoreFavorites;
        this.view.OnFriendHandlerAdded += View_OnFriendHandlerAdded;
        friendsTrackerController = new FriendTrackerController(friendsController, view.currentFriendColors);
        cardsReloader = new PlaceAndEventsCardsReloader(view, this, dataStore.exploreV2);

        view.ConfigurePools();
    }

    public void Dispose()
    {
        view.OnReady -= FirstLoading;
        view.OnInfoClicked -= ShowPlaceDetailedInfo;
        view.OnJumpInClicked -= OnJumpInToPlace;
        view.OnFavoriteSubSectionEnable -= RequestAllFavorites;
        view.OnFriendHandlerAdded -= View_OnFriendHandlerAdded;
        view.OnShowMoreFavoritesClicked -= ShowMoreFavorites;

        dataStore.channels.currentJoinChannelModal.OnChange -= OnChannelToJoinChanged;

        cardsReloader.Dispose();
    }

    private void FirstLoading()
    {
        view.OnFavoriteSubSectionEnable += RequestAllFavorites;
        cardsReloader.Initialize();
    }

    internal void RequestAllFavorites()
    {
        if (cardsReloader.CanReload())
        {
            availableUISlots = view.CurrentTilesPerRow * INITIAL_NUMBER_OF_ROWS;
            view.SetShowMoreButtonActive(false);

            cardsReloader.RequestAll();
        }
    }

    public void RequestAllFromAPI()
    {
        placesAPIApiController.GetAllFavorites(
            OnCompleted: OnRequestedEventsUpdated);
    }

    private void OnRequestedEventsUpdated(List<HotSceneInfo> placeList)
    {
        friendsTrackerController.RemoveAllHandlers();

        favoritesFromAPI = placeList;

        view.SetFavorites(PlacesAndEventsCardsFactory.CreatePlacesCards((TakeAllForAvailableSlots(placeList))));
        view.SetShowMoreFavoritesButtonActive(availableUISlots < favoritesFromAPI.Count);
    }

    internal List<HotSceneInfo> TakeAllForAvailableSlots(List<HotSceneInfo> modelsFromAPI) =>
        modelsFromAPI.Take(availableUISlots).ToList();

    internal void ShowMoreFavorites()
    {
        int numberOfExtraItemsToAdd = ((int)Mathf.Ceil((float)availableUISlots / view.currentFavoritePlacesPerRow) * view.currentFavoritePlacesPerRow) - availableUISlots;
        int numberOfItemsToAdd = (view.currentFavoritePlacesPerRow * SHOW_MORE_ROWS_INCREMENT) + numberOfExtraItemsToAdd;

        List<HotSceneInfo> placesFiltered = availableUISlots + numberOfItemsToAdd <= favoritesFromAPI.Count
            ? favoritesFromAPI.GetRange(availableUISlots, numberOfItemsToAdd)
            : favoritesFromAPI.GetRange(availableUISlots, favoritesFromAPI.Count - availableUISlots);

        view.AddFavorites(PlacesAndEventsCardsFactory.CreatePlacesCards(placesFiltered));

        availableUISlots += numberOfItemsToAdd;

        if (availableUISlots > favoritesFromAPI.Count)
            availableUISlots = favoritesFromAPI.Count;

        view.SetShowMoreFavoritesButtonActive(availableUISlots < favoritesFromAPI.Count);
    }

    internal void ShowPlaceDetailedInfo(PlaceCardComponentModel placeModel)
    {
        view.ShowPlaceModal(placeModel);
        exploreV2Analytics.SendClickOnPlaceInfo(placeModel.hotSceneInfo.id, placeModel.placeName);

        dataStore.exploreV2.currentVisibleModal.Set(ExploreV2CurrentModal.Places);
    }

    internal void OnJumpInToPlace(HotSceneInfo placeFromAPI)
    {
        view.HidePlaceModal();

        dataStore.exploreV2.currentVisibleModal.Set(ExploreV2CurrentModal.None);
        OnCloseExploreV2?.Invoke();
        //TODO define if this will be integrated and how
    }

    private void View_OnFriendHandlerAdded(FriendsHandler friendsHandler) =>
        friendsTrackerController.AddHandler(friendsHandler);

    private void OnChannelToJoinChanged(string currentChannelId, string previousChannelId)
    {
        if (!string.IsNullOrEmpty(currentChannelId))
            return;

        view.HidePlaceModal();
        dataStore.exploreV2.currentVisibleModal.Set(ExploreV2CurrentModal.None);
        OnCloseExploreV2?.Invoke();
    }
}
