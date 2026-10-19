using System;
using System.Collections.Generic;
using System.Linq;
using LogicLegends.Induction;
using LogicLegends.Inference;
using LogicLegends.Graph;
using LogicLegends.Trees;
using UnityEngine;
using Random = System.Random;

namespace LogicLegends.OpenArchive.Topics
{
    public static class ArchiveTopicQuestions
    {
        public static int[] Permutation(int count, int seed)
        {
            var result = Enumerable.Range(0, count).ToArray(); var random = new Random(seed);
            for (int i = count - 1; i > 0; i--) { int j = random.Next(i + 1); (result[i], result[j]) = (result[j], result[i]); }
            return result;
        }
        public static ArchiveQuestion Build(ArchiveTopicCatalog catalog, int topic, int round, int seed, int subphase = 0)
        {
            var random = new Random(unchecked(seed + topic * 10007 + round * 331));
            var q = new ArchiveQuestion();
            switch (topic)
            {
                case 0:
                    int bank = Permutation(catalog.propositions.Count, seed)[round];
                    var pool = catalog.propositions[bank].questions; var logic = pool[random.Next(pool.Count)];
                    var order = Permutation(logic.options.Length, random.Next());
                    q.activity = ArchiveActivity.Choice; q.title = "Proposition challenge " + (round+1); q.prompt = logic.questionText;
                    q.items = order.Select(i => logic.options[i]).ToArray(); q.correctChoice = Array.IndexOf(order, logic.correctOptionIndex);
                    q.instructions = "Read the question. Approach your answer rune and use Interact. Stay on it until the channel completes."; break;
                case 1:
                    int difficulty = round / 3;
                    var truthPool = TruthTableChallengeQuestions.Banks[difficulty];
                    var truth = truthPool[Permutation(truthPool.Length, unchecked(seed + 734 + difficulty * 139))[round % 3]];
                    q.activity = ArchiveActivity.Truth; q.title = new[] { "Easy", "Medium", "Hard" }[difficulty] + " / Column " + (round % 3 + 1);
                    q.prompt = truth.Expression; q.items = new[] { "True", "False" }; q.reusableItems = true;
                    q.slots = new[] { "P=T, Q=T", "P=T, Q=F", "P=F, Q=T", "P=F, Q=F" };
                    q.expected = Enumerable.Range(0, 4).Select(i => truth.EvaluateRow(i) ? 0 : 1).ToArray();
                    q.instructions = "Collect a True or False token, place it in a row socket, and submit all four rows. Tokens replenish at their stands."; break;
                case 2: BuildInference(catalog, q, round, seed, subphase); break;
                case 3:
                    var proofs = catalog.formalProofs.variantSets[round].variants;
                    var proof = proofs[random.Next(proofs.Length)]; q.proof = proof; q.title = proof.title; q.prompt = proof.context;
                    q.instructions = proof.instructions + " Carry a scroll to its marked altar/socket. Submit the complete arrangement.";
                    if (proof.kind == ProofChallengeKind.SortScrolls)
                    {
                        q.activity = ArchiveActivity.Sort; q.regionPlacement = true;
                        q.items = proof.scrolls.Select((s,i) => "Scroll " + (char)('A'+i)).ToArray();
                        q.details = proof.scrolls.Select(s => s.content).ToArray(); q.slots = new[] { "Formal", "Informal" };
                        q.expected = proof.scrolls.Select(s => s.isFormal ? 0 : 1).ToArray();
                    }
                    else if (proof.kind == ProofChallengeKind.MissingRule)
                    {
                        q.activity = ArchiveActivity.Order; q.items = proof.ruleOptions; q.slots = new[] { "Missing justification" };
                        q.expected = new[] { Array.IndexOf(proof.ruleOptions, proof.expectedRule) };
                        q.prompt += "\n" + string.Join("\n", proof.steps.Select(s => s.statement + " — " + s.justification));
                    }
                    else
                    {
                        q.activity = ArchiveActivity.Order; q.statementCount = proof.steps.Length;
                        q.items = proof.steps.Select((s,i)=>"Statement "+(char)('A'+i)).ToArray(); q.details = proof.steps.Select(s=>s.statement).ToArray();
                        q.slots = Enumerable.Range(1, proof.steps.Length).Select(i=>"Step "+i).ToArray();
                        q.orders = proof.acceptableOrders.Select(o=>o.stepIds).ToList();
                        if(proof.kind == ProofChallengeKind.Formalize)
                        {
                            q.items = q.items.Concat(proof.steps.Select((s,i)=>"Rule "+(char)('A'+i))).ToArray();
                            q.details = q.details.Concat(proof.steps.Select(s=>s.justification)).ToArray();
                            q.slots = q.slots.Concat(Enumerable.Range(1,proof.steps.Length).Select(i=>"Rule for step "+i)).ToArray();
                        }
                    } break;
                case 4:
                    var trials = catalog.proofMethods.variantSets[round].variants; var trial = trials[random.Next(trials.Length)];
                    q.trial=trial;q.title=trial.title;q.prompt=trial.problem;q.method=(int)trial.correctMethod;
                    q.activity=ArchiveActivity.Method;
                    if(trial.kind!=TrialProofKind.IdentifyMethod)
                    {
                        q.items=trial.tablets.Select((t,i)=>"Tablet "+(char)('A'+i)).ToArray();q.details=trial.tablets.Select(t=>t.statement+"\n"+t.justification).ToArray();
                        q.slots=Enumerable.Range(1,trial.tablets.Length).Select(i=>"Step "+i).ToArray();
                    }
                    q.instructions="Select a proof method when requested. Carry tablets into numbered proof sockets and submit at the lectern.";break;
                case 5:
                    var induction = InductionChallenges.VariantSets[round][random.Next(InductionChallenges.VariantsPerChallenge)];
                    q.activity=ArchiveActivity.Induction;q.title=induction.title;q.prompt=induction.theorem;q.instructions=induction.instructions;
                    q.items=induction.choices.Select((t,i)=>"Formula "+(char)('A'+i)).ToArray();q.details=induction.choices.Select(t=>t.expression).ToArray();
                    q.slots=induction.answers.Keys.ToArray();q.expected=induction.answers.Values.Select(id=>Array.FindIndex(induction.choices,c=>c.id==id)).ToArray();break;
                case 6: BuildSets(catalog,q,round,seed);break;
                case 7:
                    var mapping = catalog.mappings.GetVariant(round, random.Next(catalog.mappings.ChallengeGroups[round].variants.Count));
                    q.activity=ArchiveActivity.Mapping;q.mapping=mapping;q.title=mapping.title;q.prompt=mapping.instruction;
                    q.inputLabels=mapping.domain;q.outputLabels=mapping.codomain;
                    q.instructions="Select an input at Domain, then an output at Range. Undo corrects the last editable laser. Use the classification/property markers for the later questions.";break;
                case 8:
                    var graphs = catalog.graphs.variantSets[round].variants;var graph=graphs[random.Next(graphs.Count)];
                    q.activity=ArchiveActivity.Graph;q.graph=graph;q.title=graph.title;q.prompt=graph.objective;
                    q.nodeIds=graph.vertices.Select(v=>v.id).ToArray();
                    var xs=graph.vertices.Select(v=>v.position.x).ToArray();var zs=graph.vertices.Select(v=>v.position.z).ToArray();
                    q.nodes=graph.vertices.Select(v=>new Vector2(Normalize(v.position.x,xs),Normalize(v.position.z,zs))).ToArray();
                    q.instructions="The player who starts this route walks from the marked start along the ground connections. Confirm at the destination. Retry Route clears only the current attempt. Degree questions use the number markers.";break;
                case 9:
                    var trees=catalog.trees.variantSets[round].variants;var tree=trees[random.Next(trees.Count)];
                    q.activity=ArchiveActivity.Tree;q.tree=tree;q.title=tree.title;q.prompt=tree.objective;
                    q.nodeIds=tree.nodes.Select(n=>n.id).ToArray();q.nodes=tree.nodes.Select(n=>new Vector2(n.position.x*4.7f,(n.position.y-.5f)*8)).ToArray();
                    q.instructions="Use the node controls or nearby node plinths. Select a parent and child to connect/remove a branch. Select one node or a number for identification/depth. Submit the finished tree.";break;
                default: throw new ArgumentOutOfRangeException(nameof(topic));
            }
            q.itemOrder=Permutation(q.items.Length,unchecked(seed+topic*10007+round*331+subphase*772+761));
            if(q.activity==ArchiveActivity.Induction||q.activity==ArchiveActivity.Method||(q.proof!=null&&(q.proof.kind==ProofChallengeKind.OrderSteps||q.proof.kind==ProofChallengeKind.Formalize)))
                for(int i=0;i<q.items.Length;i++)
                {
                    string prefix=q.activity==ArchiveActivity.Induction?"Formula":q.activity==ArchiveActivity.Method?"Tablet":i<q.statementCount?"Statement":"Rule";
                    q.items[i]=prefix+" "+(char)('A'+Array.IndexOf(q.itemOrder,i));
                }
            return q;
        }
        static float Normalize(float value,float[] values) => Mathf.Approximately(values.Min(),values.Max())?0:Mathf.Lerp(-4.8f,4.8f,Mathf.InverseLerp(values.Min(),values.Max(),value));
        static void BuildInference(ArchiveTopicCatalog c,ArchiveQuestion q,int round,int seed,int subphase)
        {
            var random=new Random(unchecked(seed+round*557));var bank=InferenceStageQuestions.Banks[round];var spec=bank[random.Next(bank.Length)];
            var rule=c.inferenceRules.First(r=>r.abbreviation==spec.RuleCode);var example=rule.examples[random.Next(rule.examples.Length)];
            var source=new InferenceQuestion(rule,spec,example);q.title="Inference challenge "+(round+1);
            if(subphase==0)
            {
                var rows=new List<List<PuzzlePart>>();var answers=new List<string>();spec.BuildPremises(rows,answers,example);
                int blank=0;q.prompt=string.Join("\n",rows.Select(row=>string.Concat(row.Select(p=>p.IsBlank?"["+(++blank)+"]":p.Text))));
                q.activity=ArchiveActivity.Premises;q.items=Permutation(answers.Count,random.Next()).Select(i=>answers[i]).ToArray();
                q.slots=Enumerable.Range(1,answers.Count).Select(i=>"Blank "+i).ToArray();
                q.expected=answers.Select(answer=>Array.FindIndex(q.items,v=>InferenceQuestion.Normalize(v)==InferenceQuestion.Normalize(answer))).ToArray();
                q.instructions="Carry the connector-word tokens into the matching numbered premise blanks. Check the premises before searching for a Rule Crystal.";
            }
            else
            {
                q.activity=ArchiveActivity.Order;q.prompt=string.Join("\n",spec.Sentences(example));
                q.items=c.inferenceRules.Select(r=>r.ruleName).ToArray();q.slots=new[]{"Rule crystal altar"};
                q.expected=new[]{Array.IndexOf(c.inferenceRules,rule)};q.instructions="Find the crystal whose rule follows from the completed premises. Carry it to the altar and submit. The conclusion is revealed only after a correct answer.";
                q.details=c.inferenceRules.Select(r=>r.instruction).ToArray();
                q.successText="Conclusion: "+source.Conclusion;
            }
        }
        static void BuildSets(ArchiveTopicCatalog c,ArchiveQuestion q,int round,int seed)
        {
            int group=round/ArchiveTopicCatalog.SetProblemsPerChallenge,problem=round%ArchiveTopicCatalog.SetProblemsPerChallenge;var bank=c.sets[group].questions;var data=bank[Permutation(bank.Count,unchecked(seed+group*307+101))[problem]];
            var a=(data.a??Array.Empty<string>()).Distinct().OrderBy(value=>value,StringComparer.Ordinal).ToArray();var b=(data.b??Array.Empty<string>()).Distinct().OrderBy(value=>value,StringComparer.Ordinal).ToArray();
            q.activity=ArchiveActivity.Sets;q.regionPlacement=true;q.title="Challenge "+(group+1)+" — "+new[]{"Membership","Sets A and B","Intersection","Set differences","Complement"}[group]+" / Problem "+(problem+1)+" of "+ArchiveTopicCatalog.SetProblemsPerChallenge;
            q.prompt="A = { "+string.Join(", ",a)+" }\nB = { "+string.Join(", ",b)+" }";
            if(group==3)
            {
                q.slots=new[]{"Full set A","Full set B","Intersection","A minus B","B minus A"};
                q.regionValues=new[]{a.ToArray(),b.ToArray(),a.Intersect(b).ToArray(),a.Except(b).ToArray(),b.Except(a).ToArray()};
            }
            else if(group==4)
            {
                var universe=data.universe;var selected=data.setName=="B"?b:a;
                q.slots=new[]{"Inside "+data.setName,"Outside "+data.setName+", inside U"};
                q.regionValues=new[]{selected.ToArray(),universe.Except(selected).ToArray()};
                q.prompt="U = { "+string.Join(", ",universe)+" }\n"+data.setName+" = { "+string.Join(", ",selected)+" }";
            }
            else
            {
                q.slots=new[]{"A only","Intersection","B only"};
                q.regionValues=new[]{a.Except(b).ToArray(),a.Intersect(b).ToArray(),b.Except(a).ToArray()};
            }
            var items=q.regionValues.SelectMany(v=>v).ToList();var random=new Random(unchecked(seed+round*331));
            if(group!=4)while(items.Count<Math.Max(10,q.regionValues.Sum(v=>v.Length)+2)) {string value=random.Next(1,100).ToString();if(!items.Contains(value))items.Add(value);}
            q.items=Permutation(items.Count,random.Next()).Select(i=>items[i]).ToArray();
            q.instructions=group==3?"Fill all five answer regions. Use a separate block copy for every required appearance of a number. Unused blocks stay at the stand.":"Carry the needed elements into their correct regions. Unused blocks stay at the stand. Every physical block counts in exactly one region.";
        }

        public static bool Validate(ArchiveQuestion q,ArchiveStationState state,out string feedback)
        {
            feedback="Check the entire arrangement. Your team's work is kept.";
            switch(q.activity)
            {
                case ArchiveActivity.Choice:return state.selection==q.correctChoice;
                case ArchiveActivity.Sort:return state.placements.SequenceEqual(q.expected);
                case ArchiveActivity.Sets:
                    for(int region=0;region<q.slots.Length;region++)
                        if(!state.placements.Select((r,i)=>new{r,i}).Where(v=>v.r==region).Select(v=>q.items[v.i]).OrderBy(v=>v).SequenceEqual(q.regionValues[region].OrderBy(v=>v)))return false;
                    return true;
                case ArchiveActivity.Truth:case ArchiveActivity.Induction:return state.placements.SequenceEqual(q.expected);
                case ArchiveActivity.Premises:
                    return state.placements.Length==q.expected.Length && state.placements.Select((v,i)=>v>=0&&v<q.items.Length&&InferenceQuestion.Normalize(q.items[v])==InferenceQuestion.Normalize(q.items[q.expected[i]])).All(v=>v);
                case ArchiveActivity.Order:
                    if(q.proof==null || q.proof.kind==ProofChallengeKind.MissingRule)return state.placements.SequenceEqual(q.expected);
                    int n=q.statementCount;var steps=state.placements.Take(n).ToArray();
                    if(!q.orders.Any(o=>o.SequenceEqual(steps)) || steps.Any(i=>i<0||i>=n))return false;
                    for(int i=0;i<n;i++)
                    {
                        if(q.proof.steps[steps[i]].prerequisiteIds.Any(prior=>!steps.Take(i).Contains(prior)))return false;
                        if(q.proof.kind==ProofChallengeKind.Formalize && state.placements[n+i]!=n+steps[i])return false;
                    }
                    return steps[n-1]==n-1;
                case ArchiveActivity.Method:return q.trial.IsCorrect(state.selection,state.placements);
                case ArchiveActivity.Mapping:
                    var pairs=Pairs(q,state.links);
                    if(q.mapping.questionKind==MappingQuestionKind.ClassifyFunction)return state.selection==q.mapping.correctClassificationIndex;
                    if(q.mapping.questionKind==MappingQuestionKind.SelectProperties)return state.flags==(int)q.mapping.correctProperties;
                    return MappingRuleEvaluator.ExactPairs(pairs,q.mapping.requiredPairs)&&MappingRuleEvaluator.MeetsRules(pairs,q.mapping);
                case ArchiveActivity.Graph:
                    return GraphAnswerValidator.Validate(q.graph,state.route.Select(i=>q.nodeIds[i]).ToList(),state.selection<0?(int?)null:state.selection,state.continuous,out feedback);
                case ArchiveActivity.Tree:
                    return TreeAnswerValidator.Validate(q.tree,state.links.Select(l=>new TreeEdgeData(q.nodeIds[l/100],q.nodeIds[l%100])).ToList(),state.selection<0||state.selection>=q.nodeIds.Length?null:q.nodeIds[state.selection],state.flags<0?(int?)null:state.flags,out feedback);
                default:return false;
            }
        }
        public static List<MappingPair> Pairs(ArchiveQuestion q,IEnumerable<int> links) => links.Select(l=>new MappingPair(q.inputLabels[l/100],q.outputLabels[l%100])).ToList();
        public static void Prepare(ArchiveQuestion q,ArchiveStationState state)
        {
            state.placements=Enumerable.Repeat(-1,q.PlacementCount).ToArray();state.selection=-1;state.flags=q.activity==ArchiveActivity.Tree?-1:0;state.reveal="";
            state.carries.Clear();state.links.Clear();state.route.Clear();state.continuous=true;state.routeSampleValid=false;
            state.channelActor=0;state.channelChoice=-1;
            if(q.mapping!=null)state.links=q.mapping.initialPairs.Select(p=>Array.IndexOf(q.inputLabels,p.domain)*100+Array.IndexOf(q.outputLabels,p.codomain)).ToList();
            if(q.tree!=null)state.links=q.tree.initial.Select(e=>Array.IndexOf(q.nodeIds,e.source)*100+Array.IndexOf(q.nodeIds,e.destination)).ToList();
        }
    }
}
