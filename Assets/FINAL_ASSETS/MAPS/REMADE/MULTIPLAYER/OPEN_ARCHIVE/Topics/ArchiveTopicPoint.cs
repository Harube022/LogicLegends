using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    public enum ArchivePointRole { Book, Item, Slot, Choice, Input, Output, Select, Node }
    public sealed class ArchiveTopicPoint : MonoBehaviour
    {
        public ArchiveTopicStation station;
        public ArchivePointRole role;
        public int index;
        public string caption;
        public void Interact()
        {
            var session=station.session;if(session==null||!session.Ready)return;
            switch(role)
            {
                case ArchivePointRole.Book:
                    if(session.State.stations[station.topic].phase==0)session.Request(station.topic,ArchiveAction.Start);
                    ArchiveTopicUI.Current?.Open(station);break;
                case ArchivePointRole.Item:session.Request(station.topic,ArchiveAction.Pick,index);break;
                case ArchivePointRole.Slot:session.Request(station.topic,ArchiveAction.Place,index);break;
                case ArchivePointRole.Choice:session.Request(station.topic,ArchiveAction.BeginChoice,index);break;
                case ArchivePointRole.Input:session.Request(station.topic,ArchiveAction.Select,index);break;
                case ArchivePointRole.Output:
                    var actor=session.State.actors.Find(a=>a.actor==session.LocalActor);
                    if(actor!=null&&actor.mappingInput>=0)session.Request(station.topic,ArchiveAction.Connect,actor.mappingInput,index);break;
                case ArchivePointRole.Select:session.Request(station.topic,ArchiveAction.Select,index);break;
                case ArchivePointRole.Node:
                    if(station.topic==8&&session.Question(8).graph.type!=LogicLegends.Graph.GraphChallengeType.Degree&&index==ArchiveGraphTraversal.StartIndex(session.Question(8)))session.Request(8,ArchiveAction.RetryRoute);
                    else ArchiveTopicUI.Current?.PickNode(station,index);break;
            }
        }
    }
}
