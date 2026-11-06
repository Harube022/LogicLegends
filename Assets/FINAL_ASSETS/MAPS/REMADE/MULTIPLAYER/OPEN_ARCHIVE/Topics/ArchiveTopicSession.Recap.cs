using System.Collections.Generic;

namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveTopicSession
    {
        public const int LearningReviewLimitPerActor=3,LearningReviewLimitPerMatch=12,LearningReviewFeedbackLimit=700;
        public bool LearningReviewAccountMatches=>!rewardIdentityBound||rewardOwnerUid==MultiplayerRewardBackendClient.UserId;

        // This trims presentation context on existing mistake receipts only.
        // It never removes a mistake, changes a count or grades an answer.
        void NormalizeLearningReviews()
        {
            if(State?.mistakes==null)return;
            var counts=new Dictionary<int,int>();var problems=new HashSet<string>();int retained=0;
            for(int i=State.mistakes.Count-1;i>=0;i--)
            {
                var mistake=State.mistakes[i];if(mistake==null)continue;
                if(string.IsNullOrEmpty(mistake.reviewFeedback))continue;
                counts.TryGetValue(mistake.actor,out int count);
                string identity=mistake.actor+":"+mistake.topic+":"+mistake.round+":"+mistake.subphase+":"+mistake.resetSerial;
                bool valid=mistake.reviewVersion==1&&mistake.actor!=0&&mistake.reason=="answer"&&
                    mistake.topic>=0&&mistake.topic<10&&mistake.round>=0&&mistake.round<ArchiveTopicCatalog.RoundCount(mistake.topic)&&
                    mistake.subphase>=0&&mistake.subphase<(mistake.topic==2?2:1)&&mistake.resetSerial>=0;
                if(!valid||count>=LearningReviewLimitPerActor||retained>=LearningReviewLimitPerMatch||!problems.Add(identity))
                {mistake.reviewFeedback=null;continue;}
                if(mistake.reviewFeedback.Length>LearningReviewFeedbackLimit)mistake.reviewFeedback=mistake.reviewFeedback.Substring(0,LearningReviewFeedbackLimit);
                counts[mistake.actor]=count+1;retained++;
            }
        }
    }
}
