using DCL.Social.Friends;
using SocialFeaturesAnalytics;
using System;
using UnityEngine;
using UnityEngine.UI;

public class FriendEntry : FriendEntryBase
{
    public event Action<FriendEntry> OnWhisperClick;

    [SerializeField] internal JumpInButton jumpInButton;
    [SerializeField] internal Button whisperButton;
    [SerializeField] internal UnreadNotificationBadge unreadNotificationBadge;
    [SerializeField] private Button rowButton;

    private IChatController chatController;
    private IFriendsController friendsController;
    private ISocialAnalytics socialAnalytics;

    public override void Awake()
    {
        base.Awake();

        whisperButton.onClick.RemoveAllListeners();
        whisperButton.onClick.AddListener(() => OnWhisperClick?.Invoke(this));
        rowButton.onClick.RemoveAllListeners();
        rowButton.onClick.AddListener(() => OnWhisperClick?.Invoke(this));
    }

    public void Initialize(IChatController chatController,
        IFriendsController friendsController,
        ISocialAnalytics socialAnalytics)
    {
        this.chatController = chatController;
        this.friendsController = friendsController;
        this.socialAnalytics = socialAnalytics;
    }

    public override void Populate(FriendEntryModel model)
    {
        base.Populate(model);

        unreadNotificationBadge?.Initialize(chatController, model.userId);
        jumpInButton.Initialize(friendsController, model.userId, socialAnalytics);
    }
}
