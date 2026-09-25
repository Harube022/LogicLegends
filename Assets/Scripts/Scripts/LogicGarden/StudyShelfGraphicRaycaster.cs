using UnityEngine.UI;

// World-space GraphicRaycasters normally rank behind screen-space HUD raycasters,
// even when their Canvas sorting order is higher. Give only the bookshelf topic
// canvases an explicit EventSystem priority between the HUD and open-book reader.
public sealed class StudyShelfGraphicRaycaster : GraphicRaycaster
{
    private const int TopicPriority = 100;

    public override int sortOrderPriority => TopicPriority;
    public override int renderOrderPriority => TopicPriority;
}
