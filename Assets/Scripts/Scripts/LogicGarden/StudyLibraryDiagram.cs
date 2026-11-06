using TMPro;
using UnityEngine;

/// <summary>One readable diagram per page, with a separate heading and explanation.</summary>
public static class StudyLibraryDiagram
{
    static readonly Color Ink=new Color(.16f,.10f,.06f), Teal=new Color(.16f,.38f,.34f);
    public static void Build(Transform root, StudyLessonPage page)
    {
        Label(root,page.source,new Vector2(.5f,.91f),new Vector2(1,.18f),44,true);
        Label(root,page.text,new Vector2(.5f,.12f),new Vector2(1,.24f),36,false);
        switch(page.diagram)
        {
            case "relation-graph":
                Axes(root);
                foreach(var p in new[]{new Vector2(-2,1),new Vector2(-2,3),new Vector2(0,-3),new Vector2(1,4),new Vector2(3,1)})
                {var point=new Vector2(.5f+p.x*.07f,.50f+p.y*.05f);Dot(root,point,12);}
                break;
            case "relation-mapping": Mapping(root,new[]{"−2","0","1","3"},new[]{"−3","1","3","4"},new[,]{{0,1},{0,2},{1,0},{2,3},{3,1}});break;
            case "mapping-injective": Mapping(root,new[]{"1","2","3","4","5"},new[]{"3","5","9","17","33"},new[,]{{0,0},{1,1},{2,2},{3,3},{4,4}});break;
            case "mapping-many": Mapping(root,new[]{"5","6","7","8","9"},new[]{"0","1"},new[,]{{0,1},{1,0},{2,1},{3,0},{4,0}});break;
            case "mapping-not-function": Mapping(root,new[]{"7","2","1"},new[]{"11","13","17","19","23"},new[,]{{0,0},{0,1},{1,2},{1,3},{2,4}});break;
            case "vertical-function":
                Axes(root);Vector2 previous=Vector2.zero;
                for(int i=0;i<=40;i++){float x=i/40f;var p=new Vector2(.14f+x*.72f,.50f+Mathf.Sin(x*Mathf.PI*2)*.16f);if(i>0)Line(root,previous,p,4,Teal);previous=p;}
                Vertical(root,.57f);break;
            case "vertical-circle":
                Axes(root);
                for(int i=0;i<64;i++){float a=i*Mathf.PI/32,b=(i+1)*Mathf.PI/32;Line(root,new Vector2(.5f+Mathf.Cos(a)*.23f,.51f+Mathf.Sin(a)*.20f),new Vector2(.5f+Mathf.Cos(b)*.23f,.51f+Mathf.Sin(b)*.20f),4,Teal);}
                Vertical(root,.57f);break;
            case "tree-rooted":Tree(root,true);break;
            case "tree-unrooted":Tree(root,false);break;
        }
    }
    static void Mapping(Transform root,string[] left,string[] right,int[,] edges)
    {
        Label(root,"Input",new Vector2(.16f,.785f),new Vector2(.3f,.075f),32,true);
        Label(root,"Output",new Vector2(.84f,.785f),new Vector2(.3f,.075f),32,true);
        float Y(int i,int count)=>count==1?.51f:Mathf.Lerp(.70f,.31f,i/(float)(count-1));
        for(int i=0;i<edges.GetLength(0);i++)Arrow(root,new Vector2(.25f,Y(edges[i,0],left.Length)),new Vector2(.74f,Y(edges[i,1],right.Length)));
        for(int i=0;i<left.Length;i++)Label(root,left[i],new Vector2(.16f,Y(i,left.Length)),new Vector2(.16f,.085f),36,false);
        for(int i=0;i<right.Length;i++)Label(root,right[i],new Vector2(.84f,Y(i,right.Length)),new Vector2(.16f,.085f),36,false);
    }
    static void Tree(Transform root,bool rooted)
    {
        var points=rooted?new[]{new Vector2(.5f,.73f),new Vector2(.25f,.54f),new Vector2(.75f,.54f),new Vector2(.12f,.33f),new Vector2(.38f,.33f),new Vector2(.75f,.33f)}
            :new[]{new Vector2(.45f,.53f),new Vector2(.15f,.70f),new Vector2(.77f,.69f),new Vector2(.13f,.32f),new Vector2(.48f,.29f),new Vector2(.85f,.43f)};
        var links=new[,]{{0,1},{0,2},{1,3},{1,4},{2,5}};
        for(int i=0;i<5;i++)
        {
            var a=points[links[i,0]];var b=points[links[i,1]];var delta=(b-a).normalized*.045f;
            if(rooted)Arrow(root,a+delta,b-delta);else Line(root,a+delta,b-delta,3,Ink);
        }
        for(int i=0;i<points.Length;i++)Label(root,((char)('A'+i)).ToString(),points[i],new Vector2(.12f,.1f),36,rooted&&i==0);
    }
    static void Axes(Transform root)
    {
        Line(root,new Vector2(.10f,.50f),new Vector2(.90f,.50f),2,Ink);
        Line(root,new Vector2(.50f,.27f),new Vector2(.50f,.75f),2,Ink);
        Label(root,"x",new Vector2(.92f,.47f),new Vector2(.08f,.08f),32,false);
        Label(root,"y",new Vector2(.54f,.76f),new Vector2(.08f,.08f),32,false);
        for(int x=-4;x<=4;x++)if(x!=0)Label(root,x.ToString(),new Vector2(.5f+x*.07f,.45f),new Vector2(.08f,.07f),26,false);
        foreach(int y in new[]{-3,-1,1,3,4})Label(root,y.ToString(),new Vector2(.45f,.5f+y*.05f),new Vector2(.08f,.07f),26,false);
    }
    static void Vertical(Transform root,float x)=>Line(root,new Vector2(x,.28f),new Vector2(x,.75f),2,new Color(.65f,.24f,.12f));
    static void Label(Transform root,string value,Vector2 at,Vector2 size,float font,bool bold)
    {
        var t=StageUITheme.Text("Diagram Label",root,value,font,true);t.color=Ink;t.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;
        t.margin=new Vector4(6,0,6,0);
        t.enableAutoSizing=false;StageUITheme.Stretch(t.rectTransform,at-size*.5f,at+size*.5f);
    }
    static void Dot(Transform root,Vector2 at,float diameter)
    {
        var r=StageUITheme.Rect("Relation point",root);r.anchorMin=r.anchorMax=at;r.sizeDelta=Vector2.one*diameter;
        var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.sprite=HandThoughtBubble.Circle();image.color=Teal;image.raycastTarget=false;
    }
    static void Arrow(Transform root,Vector2 a,Vector2 b)
    {
        Line(root,a,b,3,Teal);var d=(b-a).normalized;var cross=new Vector2(-d.y,d.x);
        Line(root,b,b-d*.025f+cross*.015f,3,Teal);Line(root,b,b-d*.025f-cross*.015f,3,Teal);
    }
    static void Line(Transform root,Vector2 a,Vector2 b,float width,Color color)
    {
        var r=StageUITheme.Rect("Diagram edge",root);var area=(RectTransform)root;
        var start=Vector2.Scale(a,area.rect.size);var end=Vector2.Scale(b,area.rect.size);var delta=end-start;
        r.anchorMin=r.anchorMax=Vector2.zero;r.anchoredPosition=(start+end)*.5f;r.sizeDelta=new Vector2(delta.magnitude,width);
        r.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
        var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;
    }
}
