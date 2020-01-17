using System;
using System.Collections.Generic;
using BattleTech.Data;

using UnityEngine;

namespace BattleTech
{
	public class BehaviorVariableScopeManager
	{
		enum ScopeKind
		{
			Global,
			UnitRole,
			Faction,
			Personality,
			SkillBased,
		};

		struct ScopeDesc
		{
			public string Name;
			public ScopeKind ScopeKind;
			public UnitRole UnitRole;
			private string FactionID;
			private FactionValue privateFactionValue;
			public FactionValue FactionValue
			{
				get
				{
					if (privateFactionValue == null)
					{
						privateFactionValue = FactionEnumeration.GetFactionByName(FactionID);
						if (privateFactionValue == null)
						{
							privateFactionValue = FactionEnumeration.GetInvalidUnsetFactionValue();
							FactionID = privateFactionValue.Name;
						}
					}
					return privateFactionValue;
				}
				set
				{
					privateFactionValue = value;

					if(privateFactionValue == null)
						privateFactionValue = FactionEnumeration.GetInvalidUnsetFactionValue();

					FactionID = privateFactionValue.Name;
				}
			}
			public AIMood Mood;
			public AIPersonality AIPersonality;
			public AISkillID AISkillID;

			public ScopeDesc(string name, AIMood mood)
			{
				this.Name = name;
				this.ScopeKind = ScopeKind.Global;
				this.UnitRole = UnitRole.Undefined;
				this.AIPersonality = AIPersonality.Undefined;
				this.AISkillID = AISkillID.Undefined;
				this.Mood = mood;

				privateFactionValue = FactionEnumeration.GetInvalidUnsetFactionValue();
				FactionID = privateFactionValue.Name;
			}

			public ScopeDesc(string name, AIMood mood, UnitRole unitRole): this(name, mood)
			{
				this.UnitRole = unitRole;
				this.ScopeKind = ScopeKind.UnitRole;
			}

			public ScopeDesc(string name, AIMood mood, FactionValue faction): this(name, mood)
			{
				if (faction == null)
					faction = FactionEnumeration.GetInvalidUnsetFactionValue();

				privateFactionValue = faction;
				FactionID = privateFactionValue.Name;

				this.ScopeKind = ScopeKind.Faction;
			}

			public ScopeDesc(string name, AIMood mood, AIPersonality aiPersonality): this(name, mood)
			{
				this.AIPersonality = aiPersonality;
				this.ScopeKind = ScopeKind.Personality;
			}

			public ScopeDesc(string name, AIMood mood, AISkillID aiSkillID) : this(name, mood)
			{
				this.AISkillID = aiSkillID;
				this.ScopeKind = ScopeKind.SkillBased;
			}
		}

		Dictionary<int,BehaviorVariableScope> scopesByFaction;
		Dictionary<UnitRole, BehaviorVariableScope> scopesByRole;
		Dictionary<AIPersonality, BehaviorVariableScope> scopesByAIPersonality;
		Dictionary<AISkillID, BehaviorVariableScope> scopesByAISkill;
		public BehaviorVariableScope globalBehaviorVariableScope;
		List<ScopeDesc> scopeDescriptions;

		public BehaviorVariableScopeManager(GameInstance gameInstance)
		{
			scopeDescriptions = new List<ScopeDesc> {
				new ScopeDesc("global", AIMood.Undefined),
				new ScopeDesc("global_def", AIMood.Defensive),
				new ScopeDesc("global_sensorlock", AIMood.SensorLocking),
				new ScopeDesc("global_ruthless", AIMood.Ruthless),
				new ScopeDesc("role_brawler", AIMood.Undefined, UnitRole.Brawler),
				new ScopeDesc("role_brawler_def", AIMood.Defensive, UnitRole.Brawler),
				new ScopeDesc("role_ecmcarrier", AIMood.Undefined, UnitRole.EcmCarrier),
				new ScopeDesc("role_ecmcarrier_def", AIMood.Defensive, UnitRole.EcmCarrier),
				new ScopeDesc("role_ewe", AIMood.Undefined, UnitRole.Ewe),
				new ScopeDesc("role_ewe_def", AIMood.Defensive, UnitRole.Ewe),
				new ScopeDesc("role_activeprobe", AIMood.Undefined, UnitRole.ActiveProbe),
				new ScopeDesc("role_activeprobe_def", AIMood.Defensive, UnitRole.ActiveProbe),
				new ScopeDesc("role_sniper", AIMood.Undefined, UnitRole.Sniper),
				new ScopeDesc("role_sniper_def", AIMood.Defensive, UnitRole.Sniper),
				new ScopeDesc("role_scout", AIMood.Undefined, UnitRole.Scout),
				new ScopeDesc("role_scout_def", AIMood.Defensive, UnitRole.Scout),
				new ScopeDesc("role_lastmanstanding", AIMood.Undefined, UnitRole.LastManStanding),
				new ScopeDesc("role_lastmanstanding_def", AIMood.Defensive, UnitRole.LastManStanding),
				new ScopeDesc("role_meleeonly", AIMood.Undefined, UnitRole.MeleeOnly),
				new ScopeDesc("role_meleeonly_def", AIMood.Defensive, UnitRole.MeleeOnly),
				new ScopeDesc("role_noncombatant", AIMood.Undefined, UnitRole.NonCombatant),
				new ScopeDesc("role_noncombatant_def", AIMood.Defensive, UnitRole.NonCombatant),
				new ScopeDesc("role_turret", AIMood.Undefined, UnitRole.Turret),
				new ScopeDesc("role_turret_def", AIMood.Defensive, UnitRole.Turret),
				new ScopeDesc("role_vehicle", AIMood.Undefined, UnitRole.Vehicle),
				new ScopeDesc("role_vehicle_def", AIMood.Defensive, UnitRole.Vehicle),
				};

			List<FactionValue> factionList = FactionEnumeration.AIBehaviorVariableScopeList;
			for (int i = 0; i < factionList.Count; ++i)
			{
				FactionValue faction = factionList[i];
				if (faction.HasAIBehaviorVariableScope)
				{
					string undefined = string.Format("faction_{0}", faction.Name.ToLower());
					string defensive = string.Format("{0}_def", undefined);
					scopeDescriptions.Add(new ScopeDesc(undefined, AIMood.Undefined, faction));
					scopeDescriptions.Add(new ScopeDesc(defensive, AIMood.Defensive, faction));
				}
			}

			scopeDescriptions.Add(new ScopeDesc("personality_disciplined", AIMood.Undefined, AIPersonality.Disciplined));
			scopeDescriptions.Add(new ScopeDesc("personality_disciplined_def", AIMood.Defensive, AIPersonality.Disciplined));
			scopeDescriptions.Add(new ScopeDesc("personality_aggressive", AIMood.Undefined, AIPersonality.Aggressive));
			scopeDescriptions.Add(new ScopeDesc("personality_aggressive_def", AIMood.Defensive, AIPersonality.Aggressive));
			scopeDescriptions.Add(new ScopeDesc("personality_qapersonality", AIMood.Undefined, AIPersonality.QAPersonality));
			scopeDescriptions.Add(new ScopeDesc("personality_qapersonality_def", AIMood.Defensive, AIPersonality.QAPersonality));
			scopeDescriptions.Add(new ScopeDesc("skill_reckless", AIMood.Undefined, AISkillID.Reckless));
			scopeDescriptions.Add(new ScopeDesc("skill_reckless_def", AIMood.Defensive, AISkillID.Reckless));

			scopesByRole = new Dictionary<UnitRole, BehaviorVariableScope>();
			scopesByFaction = new Dictionary<int, BehaviorVariableScope>();
			scopesByAIPersonality = new Dictionary<AIPersonality, BehaviorVariableScope>();
			scopesByAISkill = new Dictionary<AISkillID, BehaviorVariableScope>();

			LoadRequest loadRequest = gameInstance.DataManager.CreateLoadRequest();

			for (int i = 0; i < scopeDescriptions.Count; ++i)
			{
				ScopeDesc scopeDescription = scopeDescriptions[i];
				loadRequest.AddLoadRequest<string>(BattleTechResourceType.BehaviorVariableScope, scopeDescription.Name, OnBehaviorVariableScopeLoaded);

				switch (scopeDescription.ScopeKind)
				{
				case ScopeKind.Global:
					if (scopeDescription.Mood == AIMood.Undefined)
					{
						globalBehaviorVariableScope = new BehaviorVariableScope();
					}
					else
					{
						globalBehaviorVariableScope.ScopesByMood[scopeDescription.Mood] = new BehaviorVariableScope();
					}
					break;
				case ScopeKind.UnitRole:
					if (scopeDescription.Mood == AIMood.Undefined)
					{
						scopesByRole[scopeDescription.UnitRole] = new BehaviorVariableScope();
					}
					else
					{
						scopesByRole[scopeDescription.UnitRole].ScopesByMood[scopeDescription.Mood] = new BehaviorVariableScope();
					}
					break;
				case ScopeKind.Faction:
					if (scopeDescription.Mood == AIMood.Undefined)
					{
						scopesByFaction[scopeDescription.FactionValue.ID] = new BehaviorVariableScope();
					}
					else
					{
						scopesByFaction[scopeDescription.FactionValue.ID].ScopesByMood[scopeDescription.Mood] = new BehaviorVariableScope();
					}
					break;
				case ScopeKind.Personality:
					if (scopeDescription.Mood == AIMood.Undefined)
					{
						scopesByAIPersonality[scopeDescription.AIPersonality] = new BehaviorVariableScope();
					}
					else
					{
						scopesByAIPersonality[scopeDescription.AIPersonality].ScopesByMood[scopeDescription.Mood] = new BehaviorVariableScope();
					}
					break;
				case ScopeKind.SkillBased:
					if (scopeDescription.Mood == AIMood.Undefined)
					{
						scopesByAISkill[scopeDescription.AISkillID] = new BehaviorVariableScope();
					}
					else
					{
						scopesByAISkill[scopeDescription.AISkillID].ScopesByMood[scopeDescription.Mood] = new BehaviorVariableScope();
					}
					break;
				}
			}

			loadRequest.ProcessRequests();
		}

		public void OnBehaviorVariableScopeLoaded(string id, string json)
		{
			ScopeDesc scopeDescription = scopeDescriptions.Find(item => item.Name == id);
			BehaviorVariableScope scope = null;

			switch (scopeDescription.ScopeKind)
			{
				case ScopeKind.Global:
					scope = globalBehaviorVariableScope;
					break;
				case ScopeKind.Faction:
					scope = scopesByFaction[scopeDescription.FactionValue.ID];
					break;
				case ScopeKind.UnitRole:
					scope = scopesByRole[scopeDescription.UnitRole];
					break;
				case ScopeKind.Personality:
					scope = scopesByAIPersonality[scopeDescription.AIPersonality];
					break;
				case ScopeKind.SkillBased:
					scope = scopesByAISkill[scopeDescription.AISkillID];
					break;
				default:
					Debug.LogError("unhandled scopeKind: " + scopeDescription.ScopeKind);
					break;
			}
			if (scopeDescription.Mood != AIMood.Undefined)
			{
				scope = scope.ScopesByMood[scopeDescription.Mood];
			}
			scope.FromJSON(json);
		}

		public BehaviorVariableScope GetScopeForFaction(FactionValue faction)
		{
			if (scopesByFaction.ContainsKey(faction.ID))
			{
				return scopesByFaction[faction.ID];
			}
			return null;
		}

		public BehaviorVariableScope GetScopeForRole(UnitRole role)
		{
			if (scopesByRole.ContainsKey(role))
			{
				return scopesByRole[role];
			}
			return null;
		}

		public BehaviorVariableScope GetScopeForAIPersonality(AIPersonality aiPersonality)
		{
			if (scopesByAIPersonality.ContainsKey(aiPersonality))
			{
				return scopesByAIPersonality[aiPersonality];
			}
			return null;
		}

		public BehaviorVariableScope GetScopeForAISkill(AISkillID aiSkillID)
		{
			if (scopesByAISkill.ContainsKey(aiSkillID))
			{
				return scopesByAISkill[aiSkillID];
			}
			return null;
		}

		public BehaviorVariableScope GetGlobalScope()
		{
			return globalBehaviorVariableScope;
		}
	}
}

