using DCL;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FavoritesSubSectionComponentView : BaseComponentView, IFavoritesSubSectionComponentView
{
    internal const string FAVORITE_CARDS_POOL_NAME = "Places_FavoriteCardsPool";
    private const int FAVORITE_CARDS_POOL_PREWARM = 20;

    private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

    [Header("Assets References")]
    [SerializeField] internal PlaceCardComponentView placeCardPrefab;
    [SerializeField] internal PlaceCardComponentView placeCardModalPrefab;

    [Header("Prefab References")]
    [SerializeField] internal ScrollRect scrollView;
    [SerializeField] internal GridContainerComponentView favorites;
    [SerializeField] internal GameObject favoritesLoading;
    [SerializeField] internal GameObject noFavoritesPrompt;
    [SerializeField] internal Color[] friendColors = null;
    [SerializeField] internal GameObject showMoreFavoritesButtonContainer;
    [SerializeField] internal ButtonComponentView showMoreFavoritesButton;

    [SerializeField] private Canvas canvas;

    internal PlaceCardComponentView placeModal;
    internal Pool favoritesCardsPool;
    private Canvas favoritesCanvas;

    public Color[] currentFriendColors => friendColors;

    public int currentFavoritePlacesPerRow => favorites.currentItemsPerRow;

    public void SetAllAsLoading() => SetFavoritesAsLoading(true);
    public void SetShowMoreButtonActive(bool isActive) => SetShowMoreFavoritesButtonActive(isActive);
    public int CurrentTilesPerRow => currentFavoritePlacesPerRow;

    public event Action OnReady;
    public event Action<PlaceCardComponentModel> OnInfoClicked;
    public event Action<HotScenesController.HotSceneInfo> OnJumpInClicked;
    public event Action<string, bool> OnFavoriteClicked;
    public event Action<FriendsHandler> OnFriendHandlerAdded;
    public event Action OnFavoriteSubSectionEnable;
    public event Action OnShowMoreFavoritesClicked;

    public override void Awake()
    {
        base.Awake();
        favoritesCanvas = favorites.GetComponent<Canvas>();
    }

    public override void Start()
    {
        placeModal = PlacesAndEventsCardsFactory.GetPlaceCardTemplateHiddenLazy(placeCardModalPrefab);

        favorites.RemoveItems();

        showMoreFavoritesButton.onClick.RemoveAllListeners();
        showMoreFavoritesButton.onClick.AddListener(() => OnShowMoreFavoritesClicked?.Invoke());

        OnReady?.Invoke();
    }

    public override void OnEnable()
    {
        OnFavoriteSubSectionEnable?.Invoke();
    }

    public override void Dispose()
    {
        base.Dispose();
        cancellationTokenSource.Cancel();

        showMoreFavoritesButton.onClick.RemoveAllListeners();

        favorites.Dispose();

        if (placeModal != null)
        {
            placeModal.Dispose();
            Destroy(placeModal.gameObject);
        }
    }

    public void ConfigurePools() =>
        favoritesCardsPool = PlacesAndEventsCardsFactory.GetCardsPoolLazy(FAVORITE_CARDS_POOL_NAME, placeCardPrefab, FAVORITE_CARDS_POOL_PREWARM);

    public override void RefreshControl() =>
        favorites.RefreshControl();

    public void SetFavorites(List<PlaceCardComponentModel> places)
    {
        SetFavoritesAsLoading(false);
        noFavoritesPrompt.gameObject.SetActive(places.Count == 0);

        favoritesCardsPool.ReleaseAll();

        this.favorites.ExtractItems();
        this.favorites.RemoveItems();

        SetFavoritesAsync(places, cancellationTokenSource.Token).Forget();
    }

    public void SetFavoritesAsLoading(bool isVisible)
    {
        favoritesCanvas.enabled = !isVisible;

        favoritesLoading.SetActive(isVisible);

        if (isVisible)
            noFavoritesPrompt.gameObject.SetActive(false);
    }

    public void AddFavorites(List<PlaceCardComponentModel> places) =>
        SetFavoritesAsync(places, cancellationTokenSource.Token).Forget();

    private async UniTask SetFavoritesAsync(List<PlaceCardComponentModel> places, CancellationToken cancellationToken)
    {
        foreach (PlaceCardComponentModel place in places)
        {
            PlaceCardComponentView placeCard = PlacesAndEventsCardsFactory.CreateConfiguredPlaceCard(favoritesCardsPool, place, OnInfoClicked, OnJumpInClicked, OnFavoriteClicked);
            OnFriendHandlerAdded?.Invoke(placeCard.friendsHandler);

            this.favorites.AddItem(placeCard);

            await UniTask.NextFrame(cancellationToken);
        }

        this.favorites.SetItemSizeForModel();
        await favoritesCardsPool.PrewarmAsync(places.Count, cancellationToken);
    }

    public void SetActive(bool isActive)
    {
        canvas.enabled = isActive;

        if (isActive)
            OnEnable();
        else
            OnDisable();
    }

    public void SetShowMoreFavoritesButtonActive(bool isActive) =>
        showMoreFavoritesButtonContainer.gameObject.SetActive(isActive);

    public void ShowPlaceModal(PlaceCardComponentModel placeInfo)
    {
        placeModal.Show();
        PlacesCardsConfigurator.Configure(placeModal, placeInfo, OnInfoClicked, OnJumpInClicked, OnFavoriteClicked);
    }

    public void HidePlaceModal()
    {
        if (placeModal != null)
            placeModal.Hide();
    }

    public void RestartScrollViewPosition() =>
        scrollView.verticalNormalizedPosition = 1;
}
