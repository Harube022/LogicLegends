using TMPro;
using UnityEngine;

/// <summary>Shared world-scroll presentation. Never changes question text or interaction geometry.</summary>
public static class ProofScrollTypography
{
    static Material neon,paper;
    public static Material Neon => neon!=null?neon:neon=Resources.Load<Material>("ProofScrollText/NeonReading");
    static Material Paper => paper!=null?paper:paper=Resources.Load<Material>("ProofScrollText/Parchment");
    public static void Text(TMP_Text label,bool heading,float size)
    {
        if(label==null)return;
        label.font=StageUITheme.ReadingFont;
        if(Neon!=null)label.fontSharedMaterial=Neon;
        label.color=Color.white;label.fontStyle=heading?FontStyles.Bold:FontStyles.Normal;
        label.fontSize=size;label.enableAutoSizing=false;
        label.textWrappingMode=TextWrappingModes.Normal;label.overflowMode=TextOverflowModes.Overflow;
        label.extraPadding=true;label.raycastTarget=false;
        label.characterSpacing=0;label.wordSpacing=0;label.lineSpacing=3;label.paragraphSpacing=5;
        label.alignment=heading?TextAlignmentOptions.Center:TextAlignmentOptions.TopLeft;
    }
    public static void Pedestal(Transform root,TMP_Text heading,TMP_Text detail)
    {
        if(root==null||heading==null||detail==null)return;
        Text(heading,true,3f);Text(detail,false,2.6f);
        const float width=2.7f,bottom=1.52f;
        detail.margin=new Vector4(.12f,.06f,.12f,.06f);
        float body=Mathf.Max(.78f,detail.GetPreferredValues(detail.text,width-.24f,100).y+.14f);
        heading.margin=new Vector4(.035f,0,.035f,0);
        float titleHeight=Mathf.Max(.38f,heading.GetPreferredValues(heading.text,width-.29f,100).y+.035f);
        float height=body+titleHeight+.26f;
        heading.rectTransform.pivot=detail.rectTransform.pivot=new Vector2(.5f,.5f);
        heading.rectTransform.sizeDelta=new Vector2(width-.22f,titleHeight);
        heading.transform.localPosition=new Vector3(0,bottom+body+.2f+titleHeight*.5f,-.345f);
        detail.rectTransform.sizeDelta=new Vector2(width,body);
        detail.transform.localPosition=new Vector3(0,bottom+.12f+body*.5f,-.345f);
        bool shortLabel=detail.text.Length<38&&!detail.text.Contains("\n");
        detail.alignment=shortLabel?TextAlignmentOptions.Center:TextAlignmentOptions.TopLeft;
        detail.ForceMeshUpdate(true);
        // Justify only word-rich prose. Sparse/formula lines remain left aligned
        // so extra whitespace cannot separate mathematical terms unnaturally.
        if(!shortLabel&&detail.text.Length>110&&!detail.text.Contains("\n")&&detail.textInfo.lineCount>1)
        {
            bool dense=true;
            for(int i=0;i<detail.textInfo.lineCount-1;i++)if(detail.textInfo.lineInfo[i].wordCount<5)dense=false;
            if(dense)detail.alignment=TextAlignmentOptions.TopJustified;
        }
        var tablet=root.Find("Reading Tablet");
        if(tablet!=null)
        {
            tablet.localPosition=new Vector3(0,bottom+height*.5f,-.26f);
            tablet.localScale=new Vector3(width,height,.12f);
            if(Paper!=null)tablet.GetComponent<Renderer>().sharedMaterial=Paper;
        }
        var frame=root.Find("Tablet Frame");
        if(frame!=null){frame.localPosition=new Vector3(0,bottom+height*.5f,-.18f);frame.localScale=new Vector3(width+.1f,height+.08f,.08f);}
        foreach(bool upper in new[]{true,false})
        {
            float y=bottom+(upper?height+.07f:-.07f);string prefix=upper?"Upper":"Lower";
            var roller=root.Find(prefix+" Scroll Roller");
            if(roller!=null){roller.localPosition=new Vector3(0,y,-.34f);roller.localScale=new Vector3(.16f,(width+.48f)*.5f,.16f);}
            foreach(string side in new[]{"Left","Right"})
            {
                var cap=root.Find(prefix+" "+side+" Scroll Cap");
                if(cap!=null)cap.localPosition=new Vector3((side=="Left"?-1:1)*(width*.5f+.23f),y,-.34f);
            }
        }
        heading.UpdateMeshPadding();detail.UpdateMeshPadding();
    }
    public static void PlacedLabel(TMP_Text label)
    {
        Text(label,true,3f);label.alignment=TextAlignmentOptions.Center;
        label.enableAutoSizing=true;label.fontSizeMin=2.7f;label.fontSizeMax=3f;
        label.rectTransform.sizeDelta=new Vector2(1.65f,.6f);label.margin=new Vector4(.05f,.04f,.05f,.04f);
    }
}
