using System;
using DCL.Chat.Channels;
using DCL.Interface;
using DCL.ProfanityFiltering;
using NSubstitute;
using NUnit.Framework;
using SocialFeaturesAnalytics;
using UnityEngine;

namespace DCL.Chat.HUD
{
    public class ChatChannelHUDControllerShould
    {
        private const string CHANNEL_ID = "channelId";
        private const string CHANNEL_NAME = "channelName";

        private ChatChannelHUDController controller;
        private IChatChannelWindowView view;
        private IChatHUDComponentView chatView;
        private IChatController chatController;
        private DataStore dataStore;
        private ISocialAnalytics socialAnalytics;
        private IProfanityFilter profanityFilter;

        [SetUp]
        public void SetUp()
        {
            var userProfileBridge = Substitute.For<IUserProfileBridge>();
            var ownUserProfile = ScriptableObject.CreateInstance<UserProfile>();
            ownUserProfile.UpdateData(new UserProfileModel
            {
                userId = "ownUserId",
                name = "self"
            });
            userProfileBridge.GetOwn().Returns(ownUserProfile);

            chatController = Substitute.For<IChatController>();
            chatController.GetAllocatedChannel(CHANNEL_ID)
                .Returns(new Channel(CHANNEL_ID, CHANNEL_NAME, 4, 12, true, false, "desc"));

            dataStore = new DataStore();
            socialAnalytics = Substitute.For<ISocialAnalytics>();
            profanityFilter = Substitute.For<IProfanityFilter>();
            controller = new ChatChannelHUDController(dataStore,
                userProfileBridge,
                chatController,
                Substitute.For<IMouseCatcher>(),
                ScriptableObject.CreateInstance<InputAction_Trigger>(),
                socialAnalytics,
                profanityFilter);

            view = Substitute.For<IChatChannelWindowView>();
            chatView = Substitute.For<IChatHUDComponentView>();
            view.ChatHUD.Returns(chatView);

            controller.Initialize(view, false);
            controller.Setup(CHANNEL_ID);
        }

        [TearDown]
        public void TearDown()
        {
            controller.Dispose();
        }

        [Test]
        public void LeaveChannelViaChatCommand()
        {
            chatView.OnSendMessage += Raise.Event<Action<ChatMessage>>(new ChatMessage
            {
                body = "/leave",
                messageType = ChatMessage.Type.PUBLIC
            });

            Assert.AreEqual(ChannelLeaveSource.Command, dataStore.channels.channelLeaveSource.Get());
            chatController.Received(1).LeaveChannel(CHANNEL_ID);
        }

        [Test]
        public void GoBackWhenLeavingChannel()
        {
            var backCalled = false;
            controller.OnPressBack += () => backCalled = true;
            controller.SetVisibility(true);

            chatController.OnChannelLeft += Raise.Event<Action<string>>(CHANNEL_ID);

            Assert.IsTrue(backCalled);
        }

        [Test]
        public void LeaveChannelWhenViewRequests()
        {
            string channelToLeave = "";
            controller.OnOpenChannelLeave += channelId =>
            {
                channelToLeave = channelId;
            };
            view.OnLeaveChannel += Raise.Event<Action>();

            Assert.AreEqual(ChannelLeaveSource.Chat, dataStore.channels.channelLeaveSource.Get());
            Assert.AreEqual(channelToLeave, CHANNEL_ID);
        }

        [Test]
        public void MuteChannel()
        {
            view.OnMuteChanged += Raise.Event<Action<bool>>(true);

            chatController.Received(1).MuteChannel(CHANNEL_ID);
        }

        [Test]
        public void UnmuteChannel()
        {
            view.OnMuteChanged += Raise.Event<Action<bool>>(false);

            chatController.Received(1).UnmuteChannel(CHANNEL_ID);
        }

        [Test]
        public void MarkMessagesAsSeenOnlyOnceWhenReceivedManyMessages()
        {
            controller.SetVisibility(true);
            view.IsActive.Returns(true);
            chatController.ClearReceivedCalls();

            var msg1 = new ChatMessage("msg1", ChatMessage.Type.PUBLIC, "user", "hey", 100)
            {
                recipient = CHANNEL_ID
            };
            var msg2 = new ChatMessage("msg1", ChatMessage.Type.PUBLIC, "user", "hey", 100)
            {
                recipient = CHANNEL_ID
            };

            chatController.OnAddMessage += Raise.Event<Action<ChatMessage[]>>(new[] {msg1, msg2});

            chatController.Received(1).MarkChannelMessagesAsSeen(CHANNEL_ID);
        }
    }
}
