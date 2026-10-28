using TMPro;
using UnityEngine;
namespace LogicLegends.Trees
{
    public sealed class TreeNode : MonoBehaviour
    {
        public string id;
        public TMP_Text label;
        public Renderer face;
        MaterialPropertyBlock block;
        public void SetSelected(bool selected, bool target = false)
        {
            label.color=selected?StageUITheme.ReadingInk:Color.white;
            block ??=new MaterialPropertyBlock();
            var color=selected?new Color(.9f,.65f,.16f):target?new Color(.4f,.62f,.65f):new Color(.16f,.31f,.33f);
            block.SetColor("_BaseColor",color);block.SetColor("_Color",color);face.SetPropertyBlock(block);
        }
        public void ShowFeedback(bool correct)
        {
            block ??= new MaterialPropertyBlock();
            var color=correct?new Color(.25f,.95f,.35f):new Color(1,.27f,.27f);
            block.SetColor("_BaseColor",color);block.SetColor("_Color",color);face.SetPropertyBlock(block);
        }
    }
}
