using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class MultiplayerUIValidation
{
    private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    private static EditorWindow view;
    private static int previousIndex=-1;
    public static void Resolution(int width,int height)
    {
        var assembly=typeof(Editor).Assembly;var type=assembly.GetType("UnityEditor.GameViewSizes");
        var sizes=type.BaseType.GetProperty("instance",Flags).GetValue(null,null);
        var group=type.GetProperty("currentGroup",Flags).GetValue(sizes,null);
        view=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        if(previousIndex<0)previousIndex=(int)view.GetType().GetProperty("selectedSizeIndex",Flags).GetValue(view);
        var ctor=assembly.GetType("UnityEditor.GameViewSize").GetConstructors().First(c=>c.GetParameters().Length==4);
        var size=ctor.Invoke(new object[]{Enum.ToObject(assembly.GetType("UnityEditor.GameViewSizeType"),1),width,height,"Multiplayer UI QA "+width+"x"+height});
        group.GetType().GetMethod("AddCustomSize",Flags).Invoke(group,new[]{size});
        int index=(int)group.GetType().GetMethod("GetTotalCount",Flags).Invoke(group,null)-1;
        view.GetType().GetMethod("SizeSelectionCallback",Flags).Invoke(view,new object[]{index,null});view.Repaint();
    }
    public static void Restore()
    {
        if(view!=null&&previousIndex>=0)view.GetType().GetMethod("SizeSelectionCallback",Flags).Invoke(view,new object[]{previousIndex,null});
        previousIndex=-1;
    }
    public static void InsideFrame(RectTransform frame)
    {
        Canvas.ForceUpdateCanvases();
        var bounds=frame.rect;var corners=new Vector3[4];
        foreach(var button in frame.GetComponentsInChildren<Button>())
        {
            ((RectTransform)button.transform).GetWorldCorners(corners);
            foreach(var corner in corners)
            {
                var point=frame.InverseTransformPoint(corner);
                if(point.x<bounds.xMin+20||point.x>bounds.xMax-20||point.y<bounds.yMin+20||point.y>bounds.yMax-20)
                    throw new InvalidOperationException(button.name+" crosses the usable frame interior.");
            }
        }
        foreach(var text in frame.GetComponentsInChildren<TMP_Text>())
        {
            if(text.name=="Value"||text.name=="Sender")continue; // Input text scrolls horizontally; names deliberately ellipsize.
            text.ForceMeshUpdate();
            if(text.isTextOverflowing)throw new InvalidOperationException("Text overflows its layout: "+text.name+" ("+text.text+")");
        }
    }
}
