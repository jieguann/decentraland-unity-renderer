using System;
using DCL.ECS7.InternalComponents;

public interface IInternalECSComponents
{
    void WriteSystemUpdate();
    IInternalECSComponent<InternalTexturizable> texturizableComponent { get; }
    IInternalECSComponent<InternalMaterial> materialComponent { get; }
    IInternalECSComponent<InternalVideoMaterial> videoMaterialComponent { get; }
    IInternalECSComponent<InternalVideoPlayer> videoPlayerComponent { get; }
    IInternalECSComponent<InternalColliders> onPointerColliderComponent { get; }
    IInternalECSComponent<InternalColliders> physicColliderComponent { get; }
    IInternalECSComponent<InternalRenderers> renderersComponent { get; }
    IInternalECSComponent<InternalVisibility> visibilityComponent { get; }
    IInternalECSComponent<InternalInputEventResults> inputEventResultsComponent { get; }
    IInternalECSComponent<InternalUiContainer> uiContainerComponent { get; }
    IInternalECSComponent<InternalUIInputResults> uiInputResultsComponent { get; }
    IInternalECSComponent<InternalSceneBoundsCheck> sceneBoundsCheckComponent { get; }
    IInternalECSComponent<InternalAudioSource> audioSourceComponent { get; }
}
