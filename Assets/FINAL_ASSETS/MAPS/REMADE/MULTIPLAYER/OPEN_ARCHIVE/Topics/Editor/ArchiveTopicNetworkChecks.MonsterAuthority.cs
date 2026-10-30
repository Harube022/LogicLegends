using System.Collections;
using System.Linq;
using ExitGames.Client.Photon;
using LogicLegends.OpenArchive.Hunting;
using Photon.Realtime;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static partial class ArchiveTopicNetworkChecks
    {
        static IEnumerator MonsterAuthorityFlow(Peer guest)
        {
            var hunt=session.hunting;var original=hunt.GetSnapshot();bool enabled=hunt.enabled;
            // Disable only AI ticking so these snapshots remain deterministic while
            // real authenticated guest events pass through the active sync adapter.
            hunt.enabled=false;
            try
            {
                int sequence=100000;
                foreach(var command in new[]{KhezuCommand.AddMistake,KhezuCommand.ForceFourMistakes,KhezuCommand.Sleep,KhezuCommand.ResetHunt,KhezuCommand.ResetRotation})
                {
                    bool wake=command==KhezuCommand.AddMistake||command==KhezuCommand.ForceFourMistakes;
                    var fixture=hunt.GetSnapshot();fixture.state=(int)(wake?KhezuHuntState.Sleeping:KhezuHuntState.Hunting);
                    fixture.mistakes=fixture.stage=wake?0:4;fixture.target=wake?0:guest.Actor;fixture.caught=0;
                    fixture.awake=!wake;fixture.pending=false;fixture.encounterPhase=0;
                    fixture.hunted=new[]{guest.Actor};fixture.resolvedOutcome=0;fixture.barrierTimerSerial=0;
                    hunt.ApplySnapshot(fixture,true);
                    guest.client.OpRaiseEvent(KhezuPhotonSync.CommandEvent,new object[]{hunt.GetComponent<KhezuPhotonSync>().SyncChannel,++sequence,(int)command},
                        new RaiseEventOptions{Receivers=ReceiverGroup.MasterClient},SendOptions.SendReliable);
                    yield return .2f;
                    var after=hunt.GetSnapshot();
                    Check(after.state==fixture.state&&after.mistakes==fixture.mistakes&&after.stage==fixture.stage&&after.target==fixture.target&&
                        after.awake==fixture.awake&&after.pending==fixture.pending&&after.hunted.SequenceEqual(fixture.hunted),
                        "Authenticated guest cannot issue legacy monster debug command "+command+" in Open Archive");
                }
            }
            finally{hunt.ApplySnapshot(original,true);hunt.enabled=enabled;}
        }
    }
}
