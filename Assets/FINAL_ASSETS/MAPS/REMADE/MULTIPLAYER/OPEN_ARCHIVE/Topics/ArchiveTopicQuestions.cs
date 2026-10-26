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
                    ArchiveCurriculumQuestions.BuildProposition(q, round, seed);
                    q.instructions = "Discuss the question with your team. Walk onto A, B, C or D, tap Hand / E once, and stay inside its ring until the channel completes. Step away to cancel."; break;
                case 1:
                    int difficulty = round / 3;
                    var truthPool = ArchiveCurriculumQuestions.TruthBanks[difficulty];
                    var truth = truthPool[Permutation(truthPool.Length, unchecked(seed + 734 + difficulty * 139))[round % 3]];
                    q.activity = ArchiveActivity.Truth; q.title = ArchiveCurriculumQuestions.TierName(1, round) + " / Column " + (round % 3 + 1);
                    q.difficulty = ArchiveCurriculumQuestions.TierName(1, round); q.moduleReference = ArchiveCurriculumQuestions.TruthReference;
                    q.prompt = truth.Expression; q.items = new[] { "True", "False" }; q.reusableItems = true;
                    q.slots = new[] { "P=T, Q=T", "P=T, Q=F", "P=F, Q=T", "P=F, Q=F" };
                    q.expected = Enumerable.Range(0, 4).Select(i => truth.EvaluateRow(i) ? 0 : 1).ToArray();
                    q.instructions = "Read P and Q for each row (TT, TF, FT, FF). Take a reusable T / True or F / False tile with Hand / E, carry it to that row's output, and use Hand / E to place it. Fill four outputs, then Check table. You can replace a tile to correct a row. Teammates share the same table."; break;
                case 2: BuildInference(catalog, q, round, seed, subphase); break;
                case 3:
                    var proof = ArchiveCurriculumQuestions.Proof(catalog, round, seed); q.proof = proof; q.title = proof.title; q.prompt = proof.context;
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
                    var trial = ArchiveCurriculumQuestions.Method(catalog, round, seed);
                    q.trial=trial;q.title=trial.title;q.prompt=trial.problem;q.method=(int)trial.correctMethod;
                    q.activity=ArchiveActivity.Method;
                    if(trial.kind!=TrialProofKind.IdentifyMethod)
                    {
                        q.items=trial.tablets.Select((t,i)=>"Tablet "+(char)('A'+i)).ToArray();q.details=trial.tablets.Select(t=>t.statement+"\n"+t.justification).ToArray();
                        q.slots=Enumerable.Range(1,trial.tablets.Length).Select(i=>"Step "+i).ToArray();
                    }
                    q.instructions="Select a proof method when requested. Carry tablets into numbered proof sockets and submit at the lectern.";break;
                case 5:
                    var induction = ArchiveCurriculumQuestions.Induction(round, seed);
                    q.activity=ArchiveActivity.Induction;q.title=induction.title;q.prompt=induction.theorem;q.instructions=induction.instructions;
                    q.items=induction.choices.Select((t,i)=>"Formula "+(char)('A'+i)).ToArray();q.details=induction.choices.Select(t=>t.expression).ToArray();
                    q.slots=induction.answers.Keys.OrderBy(key=>key.StartsWith("Base:")?0:key.StartsWith("Hypothesis:")?1:2).ThenBy(key=>int.Parse(key.Split(':')[1])).ToArray();
                    q.expected=q.slots.Select(key=>Array.FindIndex(induction.choices,c=>c.id==induction.answers[key])).ToArray();break;
                case 6: BuildSets(catalog,q,round,seed);break;
                case 7:
                    var mapping = ArchiveCurriculumQuestions.Mapping(catalog, round, seed);
                    q.activity=ArchiveActivity.Mapping;q.mapping=mapping;q.title=mapping.title;q.prompt=mapping.instruction;
                    q.inputLabels=mapping.domain;q.outputLabels=mapping.codomain;
                    q.instructions="Use Hand / E at a Domain crystal, then at a Codomain crystal to connect them. Blue lasers are editable; amber lasers are fixed. Undo at Codomain removes the last editable team connection. The range contains only reached outputs. Verdicts distinguish a function, an invalid endpoint pair, and a valid relation that is not a function. Property seals include None of these.";break;
                case 8:
                    var graph=ArchiveCurriculumQuestions.Graph(catalog, round, seed);
                    q.activity=ArchiveActivity.Graph;q.graph=graph;q.title=graph.title;q.prompt=graph.objective;
                    q.nodeIds=graph.vertices.Select(v=>v.id).ToArray();
                    var boundsX=graph.vertices.Select(v=>v.position.x).ToArray();var boundsZ=graph.vertices.Select(v=>v.position.z).ToArray();float graphScale=9.6f/Mathf.Max(1,Mathf.Max(boundsX.Max()-boundsX.Min(),boundsZ.Max()-boundsZ.Min()));
                    q.nodes=graph.vertices.Select(v=>new Vector2((v.position.x-(boundsX.Min()+boundsX.Max())*.5f)*graphScale,-(v.position.z-(boundsZ.Min()+boundsZ.Max())*.5f)*graphScale)).ToArray();
                    q.instructions="Use Hand / E at the green start rune to take the route. One teammate walks it while everyone shares the result. Stay on a connected stone path, cross its middle, and follow directed arrows. Confirm while standing on the goal vertex; a cycle finishes back at its start. Retry at the start clears only the unfinished route. Degree questions use choices 0 to 6.";break;
                case 9:
                    var tree=ArchiveCurriculumQuestions.Tree(catalog, round, seed);
                    q.activity=ArchiveActivity.Tree;q.tree=tree;q.title=tree.title;q.prompt=tree.objective;
                    q.nodeIds=tree.nodes.Select(n=>n.id).ToArray();var treePoints=tree.nodes.Select(n=>new Vector2(n.position.x*4.5f,n.position.y*5)).ToArray();var treeMin=new Vector2(treePoints.Min(p=>p.x),treePoints.Min(p=>p.y));var treeMax=new Vector2(treePoints.Max(p=>p.x),treePoints.Max(p=>p.y));float treeScale=9.6f/Mathf.Max(1,Mathf.Max(treeMax.x-treeMin.x,treeMax.y-treeMin.y));q.nodes=treePoints.Select(p=>(p-(treeMin+treeMax)*.5f)*treeScale).ToArray();
                    q.instructions="Use Hand / E at a node, or select it on the live tree diagram. Build and Repair: select the parent first, then the child, then Connect. Remove accepts either endpoint order; remove an old parent before adding a new one. Spanning trees use only original branches. Clear Pick keeps all branches. Identify selects one node; Depth / Height uses choices 0 to 5. Confirm the shared answer. Reading keeps movement and the active timer running.";break;
                default: throw new ArgumentOutOfRangeException(nameof(topic));
            }
            if(topic>=3){q.difficulty=ArchiveCurriculumQuestions.TierName(topic,round);q.moduleReference=ArchiveCurriculumQuestions.References[topic];q.title=q.difficulty+" · "+q.title;}
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
            var random=new Random(unchecked(seed+round*557));var bank=ArchiveCurriculumQuestions.InferenceBanks[round];var spec=bank[random.Next(bank.Length)];
            var rule=c.inferenceRules.First(r=>r.abbreviation==spec.RuleCode);var example=rule.examples[random.Next(rule.examples.Length)];
            var source=new InferenceQuestion(rule,spec,example);q.title=ArchiveCurriculumQuestions.TierName(2,round)+" · Inference challenge "+(round+1);
            q.difficulty=ArchiveCurriculumQuestions.TierName(2,round);q.moduleReference=ArchiveCurriculumQuestions.InferenceReference;
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
            int group=round/ArchiveTopicCatalog.SetProblemsPerChallenge,problem=round%ArchiveTopicCatalog.SetProblemsPerChallenge;var bank=c.sets[group].questions;int entry=Permutation(bank.Count,unchecked(seed+group*307+101))[problem];var data=ArchiveCurriculumQuestions.SetCase(c,group,entry);
            var a=(data.a??Array.Empty<string>()).Distinct().OrderBy(value=>value,StringComparer.Ordinal).ToArray();var b=(data.b??Array.Empty<string>()).Distinct().OrderBy(value=>value,StringComparer.Ordinal).ToArray();
            q.setGroup=group;q.setEntry=entry;q.setA=a;q.setB=b;q.setName=data.setName;q.setUniverse=data.universe??Array.Empty<string>();
            q.activity=ArchiveActivity.Sets;q.regionPlacement=true;q.title="Challenge "+(group+1)+" — "+new[]{"Membership","Sets A and B","Intersection","Set differences","Complement"}[group]+" / Problem "+(problem+1)+" of "+ArchiveTopicCatalog.SetProblemsPerChallenge;
            q.prompt="A = { "+string.Join(", ",a)+" }\nB = { "+string.Join(", ",b)+" }";
            if(group<3)q.prompt=ArchiveCurriculumQuestions.SetPrompt(group,data);
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
            var items=(q.setUniverse.Length>0?q.setUniverse:a.Union(b)).Distinct().ToList();var random=new Random(unchecked(seed+round*331));
            if(group!=4)while(items.Count<10){string value=random.Next(1,100).ToString();if(!items.Contains(value))items.Add(value);}
            q.setSourceValues=Permutation(items.Count,random.Next()).Select(i=>items[i]).ToArray();
            // Logical copy IDs are bounded for Android; only available, carried and placed copies are rendered.
            q.items=group==3?Enumerable.Range(0,ArchiveSetsPresentation.CopiesPerSource).SelectMany(_=>q.setSourceValues).ToArray():q.setSourceValues.ToArray();
            if(group==4){var selected=data.setName=="B"?data.b:data.a;var inside=new HashSet<string>(selected);foreach(var value in selected.Take(2))inside.Remove(value);foreach(var value in q.setUniverse.Except(selected).Take(2))inside.Add(value);q.setInitialPlacements=q.items.Select(value=>inside.Contains(value)?0:1).ToArray();}
            q.instructions=group==3?"Fill five separate answer trays: full A, full B, A ∩ B, A − B and B − A. Each spawner pickup leaves another copy of that value; moving a placed block creates no copy. Use one physical block for each required appearance. Unused blocks stay at the supply.":group==4?"Correct the existing arrangement. Members belong inside the named circle; complement elements belong outside that circle but inside U. Use every value in U once. No duplicate copies are supplied.":"Carry every listed element into A only, A ∩ B or B only. Shared values appear once in the intersection. Unused distractors stay at the supply.";
        }

        public static bool Validate(ArchiveQuestion q,ArchiveStationState state,out string feedback)
        {
            feedback="Check the entire arrangement. Your team's work is kept.";
            switch(q.activity)
            {
                case ArchiveActivity.Choice:return state.selection==q.correctChoice;
                case ArchiveActivity.Sort:return state.placements.SequenceEqual(q.expected);
                case ArchiveActivity.Sets:
                    if(state.placements.Length!=q.items.Length||state.placements.Any(region=>region< -1||region>=q.slots.Length))return false;
                    for(int region=0;region<q.slots.Length;region++)
                        if(!state.placements.Select((r,i)=>new{r,i}).Where(v=>v.r==region).Select(v=>q.items[v.i]).OrderBy(v=>v).SequenceEqual(q.regionValues[region].OrderBy(v=>v)))return false;
                    return true;
                case ArchiveActivity.Truth:case ArchiveActivity.Induction:return state.placements.SequenceEqual(q.expected);
                case ArchiveActivity.Premises:
                    return state.placements.Length==q.expected.Length && state.placements.Select((v,i)=>v>=0&&v<q.items.Length&&InferenceQuestion.Normalize(q.items[v])==InferenceQuestion.Normalize(q.items[q.expected[i]])).All(v=>v);
                case ArchiveActivity.Order:
                    if(q.proof==null || q.proof.kind==ProofChallengeKind.MissingRule)return state.placements.SequenceEqual(q.expected);
                    int n=q.statementCount;var steps=state.placements.Take(n).ToArray();
                    if(state.placements.Length!=q.PlacementCount)return false;
                    if(!q.orders.Any(o=>o.SequenceEqual(steps)) || steps.Any(i=>i<0||i>=n))return false;
                    if(q.proof.kind==ProofChallengeKind.Formalize&&state.placements.Skip(n).Distinct().Count()!=n)return false;
                    for(int i=0;i<n;i++)
                    {
                        if(q.proof.steps[steps[i]].prerequisiteIds.Any(prior=>!steps.Take(i).Contains(prior)))return false;
                        if(q.proof.kind==ProofChallengeKind.Formalize)
                        {
                            int rule=state.placements[n+i];
                            // Equal visible justifications are interchangeable physical cards, including repeated Premise cards.
                            if(rule<n||rule>=n*2||!string.Equals(q.proof.steps[rule-n].justification.Trim(),q.proof.steps[steps[i]].justification.Trim(),StringComparison.OrdinalIgnoreCase))return false;
                        }
                    }
                    return steps[n-1]==n-1;
                case ArchiveActivity.Method:return q.trial.IsCorrect(state.selection,state.placements);
                case ArchiveActivity.Mapping:
                    if(state.links.Any(link=>link<0||link/100>=q.inputLabels.Length||link%100>=q.outputLabels.Length)||state.links.Distinct().Count()!=state.links.Count)return false;
                    var pairs=Pairs(q,state.links);
                    if(!MappingRuleEvaluator.HasValidEndpoints(pairs,q.inputLabels,q.outputLabels))return false;
                    if(q.mapping.questionKind==MappingQuestionKind.ClassifyFunction)return state.selection==(MappingRuleEvaluator.IsFunction(pairs,q.inputLabels)?0:2)&&state.selection==q.mapping.correctClassificationIndex;
                    if(q.mapping.questionKind==MappingQuestionKind.SelectProperties)return state.flags==(int)(MappingRuleEvaluator.Properties(pairs,q.inputLabels,q.outputLabels)&(MappingProperty.Injective|MappingProperty.Surjective|MappingProperty.Bijective))&&state.flags==(int)q.mapping.correctProperties;
                    return MappingRuleEvaluator.ExactPairs(pairs,q.mapping.requiredPairs)&&MappingRuleEvaluator.MeetsRules(pairs,q.mapping);
                case ArchiveActivity.Graph:
                    if(state.route.Any(i=>i<0||i>=q.nodeIds.Length))return false;
                    return GraphAnswerValidator.Validate(q.graph,state.route.Select(i=>q.nodeIds[i]).ToList(),state.selection<0?(int?)null:state.selection,state.continuous&&state.routeAtVertex,out feedback);
                case ArchiveActivity.Tree:
                    if(!ArchiveTreeRules.Decode(q,state.links,out var treeEdges))return false;
                    if(!q.tree.Editable&&!new HashSet<int>(q.tree.initial.Select(e=>ArchiveTreeRules.Encode(q,e))).SetEquals(state.links))return false;
                    return TreeAnswerValidator.Validate(q.tree,treeEdges,state.selection<0||state.selection>=q.nodeIds.Length?null:q.nodeIds[state.selection],state.flags<0?(int?)null:state.flags,out feedback);
                default:return false;
            }
        }
        public static string ProofReveal(ArchiveQuestion q,ArchiveStationState state)
        {
            if(q.proof.kind==ProofChallengeKind.SortScrolls)
                return string.Join("\n",q.items.Select((name,i)=>name+" · "+q.slots[state.placements[i]]))+"\n\nFormal proofs state explicit steps and justifications. Informal proofs explain valid reasoning in ordinary language.";
            if(q.proof.kind==ProofChallengeKind.MissingRule){string rule=q.items[state.placements[0]];return "Justification: "+rule+"\n\n"+q.prompt.Replace("[MISSING JUSTIFICATION]",rule).Replace("[MISSING]",rule).Trim();}
            return string.Join("\n",Enumerable.Range(0,q.statementCount).Select(i=>(i+1)+". "+q.proof.steps[state.placements[i]].statement+" — "+(q.proof.kind==ProofChallengeKind.Formalize?q.proof.steps[state.placements[q.statementCount+i]-q.statementCount].justification:q.proof.steps[state.placements[i]].justification)));
        }
        public static string MethodReveal(ArchiveQuestion q,ArchiveStationState state)
        {
            var method=q.trial.kind==TrialProofKind.ArrangeSteps?q.trial.station:(TrialProofMethod)state.selection;
            string name=method==TrialProofMethod.Contrapositive?"Contrapositive (indirect)":method==TrialProofMethod.Contradiction?"Contradiction (indirect)":"Direct";
            string steps=q.trial.kind==TrialProofKind.IdentifyMethod?"":string.Join("\n\n",state.placements.Select((item,i)=>(i+1)+". "+q.trial.tablets[item].statement+"\n"+q.trial.tablets[item].justification))+"\n\n";
            return "Accepted method: "+name+"\n\n"+steps+q.trial.explanation;
        }
        public static string InductionReveal(ArchiveQuestion q,ArchiveStationState state)
        {
            string proof=string.Join("\n\n",q.slots.Select((key,i)=>(key.StartsWith("Base:")?"Base case · P(1)":key.StartsWith("Hypothesis:")?"Inductive hypothesis · P(k)":"Inductive step · line "+key.Split(':')[1])+"\n"+q.details[state.placements[i]]));
            return q.prompt+"\n\n"+proof+(q.slots.Any(key=>key.StartsWith("Base:"))&&q.slots.Any(key=>key.StartsWith("Hypothesis:"))&&q.slots.Count(key=>key.StartsWith("Step:"))==4?"\n\nThe base case and P(k) → P(k + 1) establish P(n) for every integer n ≥ 1.":"");
        }
        public static int[] SetPlacementGrades(ArchiveQuestion q,ArchiveStationState state){var grades=new int[q.items.Length];var seen=Enumerable.Range(0,q.slots.Length).Select(_=>new HashSet<string>()).ToArray();for(int i=0;i<grades.Length;i++){int region=i<state.placements.Length?state.placements[i]:-1;if(region<0)continue;grades[i]=region<q.regionValues.Length&&q.regionValues[region].Contains(q.items[i])&&seen[region].Add(q.items[i])?1:-1;}return grades;}
        public static string MappingReveal(ArchiveQuestion q,ArchiveStationState state)=>q.title+"\n"+string.Join("  ·  ",Pairs(q,state.links))+"\nRange = { "+string.Join(", ",q.outputLabels.Where((label,i)=>state.links.Any(link=>link%100==i)))+" }\n"+(q.mapping.questionKind==MappingQuestionKind.ClassifyFunction?"Accepted verdict: "+ArchiveMappingPresentation.ClassificationNames[state.selection]+". A function is also a relation; each input must have exactly one output.":q.mapping.questionKind==MappingQuestionKind.SelectProperties?"Accepted properties: "+(state.flags==0?"None of these":string.Join(", ",new[]{2,4,8}.Where(flag=>(state.flags&flag)!=0).Select(flag=>ArchiveMappingPresentation.PropertyNames[Array.IndexOf(new[]{2,4,8},flag)]))):MappingConstructionSummary(q,state));
        static string MappingConstructionSummary(ArchiveQuestion q,ArchiveStationState state)
        {
            if(!q.mapping.requireFunction)return "The complete relation matches the required ordered pairs.";
            var pairs=Pairs(q,state.links);bool injective=MappingRuleEvaluator.IsInjective(pairs,q.inputLabels),onto=MappingRuleEvaluator.IsSurjective(pairs,q.outputLabels);
            return "Every input has exactly one output. "+(injective?"Different inputs reach different outputs. ":"Some inputs share an output. ")+(onto?"Every codomain output is reached.":"The range is smaller than the codomain, so this function is not onto.");
        }
        public static string GraphLabel(ArchiveQuestion q,string id)=>string.IsNullOrEmpty(q.graph.Vertex(id)?.label)?id:q.graph.Vertex(id).label;
        public static string GraphReveal(ArchiveQuestion q,ArchiveStationState state)=>q.title+"\n"+(q.graph.type==GraphChallengeType.Degree?"Degree of "+GraphLabel(q,q.graph.degreeVertex)+" = "+state.selection:"Accepted route: "+string.Join(" → ",state.route.Select(i=>GraphLabel(q,q.nodeIds[i]))))+"\n\n"+q.graph.lesson;
        public static string SetsReveal(ArchiveQuestion q)=>q.prompt+"\n\n"+string.Join("\n",q.slots.Select((name,i)=>name+" = { "+string.Join(", ",q.regionValues[i])+" }"));
        public static List<MappingPair> Pairs(ArchiveQuestion q,IEnumerable<int> links) => links.Select(l=>new MappingPair(q.inputLabels[l/100],q.outputLabels[l%100])).ToList();
        public static void Prepare(ArchiveQuestion q,ArchiveStationState state)
        {
            state.placements=Enumerable.Repeat(-1,q.PlacementCount).ToArray();state.selection=-1;state.flags=q.activity==ArchiveActivity.Tree?-1:0;state.reveal="";
            state.setGrades=Array.Empty<int>();if(q.activity==ArchiveActivity.Sets){state.setGrades=new int[q.items.Length];if(q.setInitialPlacements.Length==q.PlacementCount)state.placements=q.setInitialPlacements.ToArray();}
            state.carries.Clear();state.links.Clear();state.route.Clear();state.continuous=true;state.routeSampleValid=false;
            state.routeEdge=-1;state.routeCrossedMiddle=false;state.routeAtVertex=false;state.routeSampleAt=0;state.routeReason="";
            state.channelActor=0;state.channelChoice=-1;
            if(q.mapping!=null)state.links=q.mapping.initialPairs.Select(p=>Array.IndexOf(q.inputLabels,p.domain)*100+Array.IndexOf(q.outputLabels,p.codomain)).ToList();
            if(q.tree!=null)state.links=q.tree.initial.Select(e=>Array.IndexOf(q.nodeIds,e.source)*100+Array.IndexOf(q.nodeIds,e.destination)).ToList();
        }
    }
}
