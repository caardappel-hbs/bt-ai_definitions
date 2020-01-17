using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using BattleTech;
using HBS.Util;
using BattleTech.Serialization;
using System;

[SerializableEnum("BehaviorVariableName")]
public enum BehaviorVariableName
{
    INVALID_UNSET = 0,

    /*
     Description: Accuracy needed for overheat attack.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: heat threshold
    */
    Float_AccuracyNeededForOverheatAttack = 22,

    /*
     Description: Damage needed at a location for overheat attack,
         as a fraction from 0.0 (undamaged) to 1.0 (location destroyed)
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: heat threshold, Float_AccuracyNeededForOverheatAttack
    */
    Float_ExistingTargetDamageForOverheatAttack = 1,

    /*
     Description: Damage needed at a location for DFA attack,
         as a fraction from 0.0 (undamaged) to 1.0 (location destroyed)
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_ExistingTargetDamageForDFAAttack = 40,

    /*
     Description: Maximum leg damage allowed for DFA attack,
         as a fraction from 0.0 (undamaged) to 1.0 (location destroyed)
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_OwnMaxLegDamageForDFAAttack = 42,

    /*
     Description: Radius around waypoints before proceeding to next waypoint.
    Used in: Route Following
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_LanceMoveRadius
    */
    Float_LancePatrolRadius = 2,

    /*
     Description: Distance beyond allies that a unit can move while
         following a route.
    Used in: Route Following
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_LancePatrolRadius
    */
    Float_LanceMoveRadius = 3,

    /*
    Description: When attacking a priority target, do not attack
        the priority target if targeted within the recent target
        threshold number of phases.
    Used in: Priority Target
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Int_RecentTargetThresholdPhases = 6,

    /*
    Description:
    Used in: Priority Target
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferredTargetToHitThreshold = 7,

    /*
    Description: Should targeting this unit distract it from attacking a priority target?
    Used in: Priority Target
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Bool_ProhibitTargetingPriorityTargetsAfterBeingTargeted = 21,

    /*
    Description: Whether the given route should loop
    Used in: Route following
    OK to set via JSON: No
    OK to set via orders: No
    See also:
    */
    Bool_PatrolShouldLoop = 4,

    /*
    Description: GUID for the route being followed
    Used in: Route following
    OK to set via JSON: No
    OK to set via orders: No
    See also:
    */
    String_RouteGUID = 5,

    /*
    Description: Which waypoint along the route is currently being targeted. Updated internally by route logic.
    Used in: Route following
    OK to set via JSON: No
    OK to set via orders: No
    See also:
    */
    Int_RouteTargetPoint = 8,

    /*
    Description: Whether the route has been started. Updated internally by the route logic.
    Used in: Route following
    OK to set via JSON: No
    OK to set via orders: No
    See also:
    */
    Bool_RouteStarted = 9,

    /*
    Description: Whether the route has been completed. Updated internally by the route logic.
    Used in: Route following
    OK to set via JSON: No
    OK to set via orders: No
    See also:
    */
    Bool_RouteCompleted = 10,

    /*
    Description: Whether the route is being followed in a forward direction. Updated internally by the route logic.
    Used in: Route following
    OK to set via JSON: No
    OK to set via orders: No
    See also:
    */
    Bool_RouteFollowingForward = 11,

    /*
    Description: Whether the unit should sprint while following the route.
    Used in: Route following
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Bool_RouteShouldSprint = 14,

    /*
    Description: Whether the unit should stay with their lance while following the route.
    Used in: Route following
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Bool_RouteWithLance = 63,

    /*
    Description: Whether to start at the closest point of the route, versus at the first point of the route.
    Used in: Route following
    OK to set via JSON: No
    OK to set via orders: No
    See also:
    */
    Bool_RouteStartAtClosestPoint = 20,

    /*
    Description: Radius of waypoints
    Used in: Route following
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_RouteWaypointRadius = 12,

    /*
    Description: how close is too close to the nearest ally
    Used in: Route following
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PersonalSpaceRadius = 13,

    /*
    Description: Ideal distance to nearest ally
    Used in: Route following
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_OptimalAllyDistance = 36,

    /*
    Description: GUID for a destination for the lance to follow before considering attacks
    Used in: Route following
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    String_LancePreAttackDestinationGUID = 15,

    /*
    Description: GUID for a destination for the unit to follow before considering attacks
    Used in: Route following
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    String_UnitPreAttackDestinationGUID = 16,

    /*
    Description: GUID for a destination for the lance to follow after considering attacks
    Used in: Route following
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    String_LancePostAttackDestinationGUID = 17,

    /*
    Description: GUID for a destination for the unit to follow after considering attacks
    Used in: Route following
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    String_UnitPostAttackDestinationGUID = 18,

    /*
    Description: GUID of a region to stay inside
    Used in: Stay inside region
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    String_StayInsideRegionGUID = 19,

    /*
    Description: Percentage (0-100) of time to attack priority targets
    Used in: Priority attacks
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PriorityAttackPercentage = 23,

    /*
    Description: Percentage (0-100) of time to move towards priority targets
    Used in: Priority attacks
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PriorityMovePercentage = 24,

    /*
    Description: Influence Factor Weight for preferring moving less.
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferLowerMovementFactorWeight = 25,

    /*
    Description: Influence Factor Weight for preferring moving less WHEN SPRINTING.
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferLowerMovementFactorWeight = 87,

    /*
    Description: Influence Factor Weight for preferring higher locations
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferHigherPositionFactorWeight = 26,

    /*
    Description: Influence Factor Weight for preferring higher locations WHEN SPRINTING
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferHigherPositionFactorWeight = 88,

    /*
    Description: Influence Factor Weight for preferring more level (vs steep) locations.
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferLessSteepPositionFactorWeight = 33,

    /*
    Description: Influence Factor Weight for preferring more level (vs steep) locations WHEN SPRINTING.
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferLessSteepPositionFactorWeight = 89,

    /*
    Description: Influence Factor Weight to maximize expected damage to hostiles.
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferHigherExpectedDamageToHostileFactorWeight = 27,

    /*
    Description: Influence Factor Weight to maximize expected damage to hostiles WHEN SPRINTING.
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferHigherExpectedDamageToHostileFactorWeight = 90,

    /*
    Description: Influence Factor Weight to minimize expected damage from hostiles.
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferLowerExpectedDamageFromHostileFactorWeight = 59,

    /*
    Description: Influence Factor Weight to minimize expected damage from hostiles WHEN SPRINTING.
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferLowerExpectedDamageFromHostileFactorWeight = 91,

    /*
    Description: Influence Factor Weight to prefer being behind hostiles.
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferAttackFromBehindHostileFactorWeight = 28,

    /*
    Description: Influence Factor Weight to prefer being behind hostiles WHEN SPRINTING.
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferAttackFromBehindHostileFactorWeight = 92,

    /*
    Description: Influence Factor Weight to prefer being beside hostiles.
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferAttackFrom90DegreesToHostileFactorWeight = 39,

    /*
    Description: Influence Factor Weight to prefer being beside hostiles WHEN SPRINTING.
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferAttackFrom90DegreesToHostileFactorWeight = 93,

    /*
    Description: Influence Factor Weight to reject being closer
        than the minimum weapon range to hostiles. Could
        be set to a large value (10 or greater).
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferNoCloserThanMinDistToHostileFactorWeight = 29,

    /*
    Description: Influence Factor Weight to reject being closer than the
        minimum weapon range to hostiles WHEN SPRINTING. Could be set
        to a large value (10 or greater).
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferNoCloserThanMinDistToHostileFactorWeight = 94,

    /*
    Description: Influence Factor Weight to prefer facing hostiles.
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferFacingHostileFactorWeight = 32,

    /*
    Description: Influence Factor Weight to prefer facing hostiles WHEN SPRINTING.
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferFacingHostileFactorWeight = 95,

    /*
    Description: Influence Factor Weight to reject being closer
        than personal space to allies. Can be large (>10)
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_PersonalSpaceRadius
    */
    Float_PreferNoCloserThanPersonalSpaceToAllyFactorWeight = 30,

    /*
    Description: Influence Factor Weight to reject being closer
        than personal space to allies WHEN SPRINTING. Can be large (>10).
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_PersonalSpaceRadius
    */
    Float_SprintPreferNoCloserThanPersonalSpaceToAllyFactorWeight = 96,

    /*
    Description: Influence Factor Weight to prefer being far away
        from closest hostile. Useful for defensive moves.
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferHigherDistanceFromClosestHostileFactorWeight = 34,

    /*
    Description: Influence Factor Weight to prefer being far away WHEN SPRINTING
        from closest hostile. Useful for defensive moves.
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferHigherDistanceFromClosestHostileFactorWeight = 97,

    /*
    Description: Influence Factor Weight to prefer having LOS to
        fewest hostiles. Useful for defensive moves.
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferLOSToFewestHostileFactorWeight = 35,

    /*
    Description: Influence Factor Weight to prefer having LOS to WHEN SPRINTING
        fewest hostiles. Useful for defensive moves.
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferLOSToFewestHostileFactorWeight = 98,

    /*
    Description: Influence Factor Weight to prefer having LOS to
        most hostiles. Useful for spotters.
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferLOSToMostHostilesFactorWeight = 58,

    /*
    Description: Influence Factor Weight to prefer having LOS to WHEN SPRINTING
        most hostiles. Useful for spotters.
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferLOSToMostHostilesFactorWeight = 99,

    /*
    Description: Influence Factor Weight to prefer being behind braced targets.
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferBeingBehindBracedHostileFactorWeight = 69,

    /*
    Description: Influence Factor Weight to prefer being behind braced targets. WHEN SPRINTING
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferBeingBehindBracedHostileFactorWeight = 100,

    /*
    Description: Influence Factor Weight to prefer locations close
        to optimal distance to other allies.
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_OptimalAllyDistance
    */
    Float_PreferOptimalDistanceToAllyFactorWeight = 37,

    /*
    Description: Influence Factor Weight to prefer locations close WHEN SPRINTING
        to optimal distance to other allies.
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_OptimalAllyDistance
    */
    Float_SprintPreferOptimalDistanceToAllyFactorWeight = 101,

    /*
    Description: Meters between adjacent samples of influence map
        (down to a minimum of the resolution of the pathing grid)
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_InfluenceMapSampleDistance = 31,

    /*
    Description: How much heat is acceptable before being subject to "overheat" logic.
        0: no heat
        1: Heat level 1
        2: Heat level 2
        3: Max heat
    Used in: Heat Management
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_AcceptableHeatLevel = 38,

    /*
    Description: DEPRECATED Percentage chance (0-100) of how likely a unit far away from combat will sprint to catch up.
        0 - never
        100 - always
    Used in: Combat (when not able to fire a shot)
    OK to set via JSON: DEPRECATED Yes
    OK to set via orders: DEPRECATED Yes
    See also:
    */
    Float_SprintToCombatPercentage = 41,

    /*
    Description: DEPRECATED When calculating Threat, take this root of the battle value to this base (e.g. 2.0 uses a square root,
      3.0 is a cube root. 0.5 squares the value, to make battle value much more powerful. 1.0 will just linearly use
      battle value.
    Used in: DEPRECATED Combat (when determining threat ranking)
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_ThreatBattleValueRoot = 43,

    /*
    Description: When considering different kinds of attacks, multiply the shooting damage by this value to decide which
      attack to use.
    Used in: Combat (when determining which kind of attack to use)
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_ShootingDamageMultiplier = 44,

    /*
    Description: When considering different kinds of attacks, multiply the melee damage by this value to decide which
      attack to use.
    Used in: Combat (when determining which kind of attack to use)
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_MeleeDamageMultiplier = 45,

    /*
    Description:  When considering a melee attack against an unsteady target, multiply the melee damage by this value to
      decide which attack to use. This is in addition to the base melee multiplier, above.
    Used in: Combat (when determining which kind of attack to use)
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_MeleeDamageMultiplier
    */
    Float_MeleeVsUnsteadyTargetDamageMultiplier = 173,

    /*
    Description: When considering different kinds of attacks, multiply the DFA damage by this value to decide which
      attack to use.
    Used in: Combat (when determining which kind of attack to use)
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_DFADamageMultiplier = 46,

    /*
    Description: Distance (in meters) outside of which we consider ourselves ok to stop and cool down.
    Used in: Combat (heat management)
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_CoolDownRange = 47,

    /*
    Description: Influence Factor Weight to control the desire to be outside the cooldown range. Probably most useful
      in defensive mood, when attempting to dump heat.
    Used in: Combat (heat management)
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_CoolDownRange
    */
    Float_PreferOutsideCoolDownRangeFactorWeight = 48,

    /*
    Description: Influence Factor Weight to control the desire to be outside the cooldown range. Probably most useful WHEN SPRINTING
      in defensive mood, when attempting to dump heat.
    Used in: Combat (heat management)
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_CoolDownRange
    */
    Float_SprintPreferOutsideCoolDownRangeFactorWeight = 102,

    /*
    Description: Influence Factor Weight to control the desire to be inside forests.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferLessTargetableLocationFactorWeight = 49,

    /*
    Description: Influence Factor Weight to control the desire to be inside forests. WHEN SPRINTING
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferLessTargetableLocationFactorWeight = 103,

    /*
    Description: Influence Factor Weight to control the desire to be inside forests or other locations that grant guard.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferLocationsThatGrantGuardFactorWeight = 79,

    /*
    Description: Influence Factor Weight to control the desire to be inside forests or other locations that grant guard. WHEN SPRINTING
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferLocationsThatGrantGuardFactorWeight = 104,

    /*
    Description: Influence Factor Weight to control the desire to be inside water.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferHigherHeatSinkLocationsFactorWeight = 50,

    /*
    Description: Influence Factor Weight to control the desire to be inside water. WHEN SPRINTING
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferHigherHeatSinkLocationsFactorWeight = 105,

    /*
    Description: Influence Factor Weight to control the desire to be in a heat generating mask.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferHigherHeatPerTurnLocationsFactorWeight = 212,

    /*
    Description: Influence Factor Weight to control the desire to be in a heat generating mask WHEN SPRINTING.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferHigherHeatPerTurnLocationsFactorWeight = 213,


    /*
    Description: Influence Factor Weight to control the desire to be in locations with damage reduction.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferHigherDamageReductionLocationsFactorWeight = 147,

    /*
    Description: Influence Factor Weight to control the desire to be in locations with damage reduction WHEN SPRINTING.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferHigherDamageReductionLocationsFactorWeight = 148,

    /*
    Description: Influence Factor Weight to control the desire to be in locations with higher melee to-hit penalty.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferHigherMeleeToHitPenaltyLocationsFactorWeight = 149,

    /*
    Description: Influence Factor Weight to control the desire to be in locations with higher melee to-hit penalty WHEN SPRINTING.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferHigherMeleeToHitPenaltyLocationsFactorWeight = 150,

    /*
    Description: Influence Factor Weight to control the desire to be in locations with movement bonus.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferHigherMovementBonusLocationsFactorWeight = 151,

    /*
    Description: Influence Factor Weight to control the desire to be in locations with movement bonus WHEN SPRINTING.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferHigherMovementBonusLocationsFactorWeight = 152,

    /*
    Description: Influence Factor Weight to control the desire to be in locations with stability bonus.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferLowerStabilityDamageMultiplierLocationsFactorWeight = 153,

    /*
    Description: Influence Factor Weight to control the desire to be in locations with stability bonus WHEN SPRINTING.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferLowerStabilityDamageMultiplierLocationsFactorWeight = 154,

    /*
    Description: Influence Factor Weight to control the desire to be in locations with higher visibility cost.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferHigherVisibilityCostLocationsFactorWeight = 155,

    /*
    Description: Influence Factor Weight to control the desire to be in locations with higher visibility cost WHEN SPRINTING.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferHigherVisibilityCostLocationsFactorWeight = 156,

    /*
    Description: Influence Factor Weight to control the desire to be in locations with sensor range bonus.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferHigherSensorRangeMultiplierLocationsFactorWeight = 157,

    /*
    Description: Influence Factor Weight to control the desire to be in locations with sensor range bonus WHEN SPRINTING.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferHigherSensorRangeMultiplierLocationsFactorWeight = 158,

    /*
    Description: Influence Factor Weight to control the desire to be in locations with signature reduction.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferLowerSignatureMultiplierLocationsFactorWeight = 159,

    /*
    Description: Influence Factor Weight to control the desire to be in locations with signature reduction WHEN SPRINTING.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferLowerSignatureMultiplierLocationsFactorWeight = 160,

    /*
    Description: Influence Factor Weight to control the desire to be near a tagged target.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferProximityToTaggedTargetFactorWeight = 71,

    /*
    Description: Influence Factor Weight to control the desire to be near a tagged target. WHEN SPRINTING
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferProximityToTaggedTargetFactorWeight = 106,

    /*
    Description: Distance to control the desire to be near a tagged target.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferProximityToTaggedTargetFactorDistance = 72,

    /*
    Description: Tag to control the desire to be near a tagged target.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    String_PreferProximityToTaggedTargetFactorTag = 73,

    /*
    Description: Influence Factor Weight to control the desire to attack a hostile's weak armor
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferAttackingLowerArmorHostileFactorWeight = 51,

    /*
    Description: Influence Factor Weight to control the desire to attack a hostile's weak armor WHEN SPRINTING
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferAttackingLowerArmorHostileFactorWeight = 107,

    /*
    Description: Influence Factor Weight to control the desire to defend my own weak armor
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferPresentingHigherArmorToHostileFactorWeight = 52,

    /*
    Description: Influence Factor Weight to control the desire to defend my own weak armor WHEN SPRINTING
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferPresentingHigherArmorToHostileFactorWeight = 108,

    /*
    Description: ratio between expected damage and target hit points to be used for "vulnerable" threat sorting.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_VulnerableDamageRatioThreshold = 53,

    /*
    Description:  Damage multiplier when measuring vulnerability of called shot targets.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_CalledShotVulnerabilityMultiplier = 171,

    /*
    Description: ratio between hostile's expected damage and my hit points to identify the hostile as a "threat".
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_ThreatDamageRatioThreshold = 161,


    /*
    Description: How many phases to consider when looking for a melee revenge target
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Int_MeleeRevengeWindowPhaseCount = 54,

    /*
    Description: If a target is a melee revenge target, what additional damage multiplier to apply. The total multiplier
      is Float_MeleeDamageMultiplier + FloatMeleeRevengeBonus.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Int_MeleeRevengeWindowPhaseCount, Float_MeleeDamageMultiplier
    */
    Float_MeleeRevengeBonus = 55,

    /*
    Description: If a target is a melee revenge target, but the ratio of its expected melee damage to my expected melee
      damage is higher than this threshold, DO NOT MELEE! It'll all end in tears!
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_MeleeDamageRatioCap = 56,

    /*
    Description: If a unit takes this percentage damage or more in a round, consider it "major damage", and react
    accordingly (make a defensive move, maybe do a suicidal charge?)
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_MajorDamageRatio = 57,

    /*
    Description: When a weapon (e.g. flamer) generates heat, use this ratio to convert heat to "virtual" damage for AI
      calculations.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_HeatToDamageRatio = 60,


    /*
    Description: When choosing to turn the strongest armor towards the enemy, the rear armor can be presented to the
      enemy if the unit is in "offensive" mode, only if this is set to True.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Influence Factors
    */
    Bool_AllowTurningRearArmorToEnemy = 61,

    /*
    Description: A weight for an influence factor to prefer to get inside melee range. Negative values will encourage
      units to get out of melee range.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Influence Factors
    */
    Float_PreferInsideMeleeRangeFactorWeight = 62,

    /*
    Description: A weight for an influence factor to prefer to get inside melee range. Negative values will encourage WHEN SPRINTING
      units to get out of melee range.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Influence Factors
    */
    Float_SprintPreferInsideMeleeRangeFactorWeight = 109,

    /*
    Description: The number of hostiles considered when evaluating influence maps.
    Used in: Movement
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Int_HostileInfluenceCount = 64,

    /*
    Description: The number of allies considered when evaluating influence maps.
    Used in: Movement
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Int_AllyInfluenceCount = 65,

    /*
    Description: If unsteady, the chance of just deciding to switch to defensive.
    Used in: Movement
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_UnsteadyCausesDefensiveMovePercentage = 66,

    /*
    Description: An alerted unit will act on visibility information, closing, attacking. Non-alerted units will still follow patrol orders or movement orders.
    Used in: outside of combat
    OK to set via JSON: Yes*
    OK to set via orders: Yes*
    See also:
    */
    Bool_Alerted = 67,

    /*
    Description: If unsteady, the chance of just deciding to brace. Expect brawlers will do this.
    Used in: Attack selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_UnsteadyCausesBracePercentage = 68,

    /*
    Description: If my move would not result in being able to fire, should I rewrite that move as a sprint?
    Used in: Move selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Bool_RewriteNonAttackMoves = 70,

    /*
    Description: How many degrees apart should I consider different facings when choosing moves?
    Used in: Move selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_AngularSelectionResolution = 74,

    /*
    Description: Use dynamic lance roles (brawler, flanker, spotter, sniper)
    Used in: Lance Roles
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Bool_UseDynamicLanceRoles = 75,

    /*
    Description: How much of expected melee damage to add in again as bonus damage when attacking evasive targets (0 - none, 1 - 100% bonus damage)
    Used in: Attack Evaluation
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_MeleeBonusMultiplierWhenAttackingBracedTargets
    */
    Float_MeleeBonusMultiplierWhenAttackingEvasiveTargets = 76,

    /*
    Description: How much of expected melee damage to add in again as bonus damage when attacking braced targets (0 - none, 1 - 100% bonus damage)
    Used in: Attack Evaluation
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_MeleeBonusMultiplierWhenAttackingEvasiveTargets
    */
    Float_MeleeBonusMultiplierWhenAttackingBracedTargets = 77,

    /*
    Description: How many damage points one unit of unsteadiness converts to when calculating virtual damage when attacking unstable targets
    Used in: Attack Evaluation
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_MeleeBonusMultiplierWhenAttackingEvasiveTargets
    */
    Float_UnsteadinessToVirtualDamageConversionRatio = 78,

    /*
    Description: Percentage (0-100) chance to brace (pass) when overheated.
    Used in: Attack Evaluation
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_UnsteadyCausesDefensiveMovePercentage, Float_UnsteadyCausesBracePercentage
    */
    Float_BraceWhenOverheatedPercentage = 80,

    /*
    Description: Percentage (0-100) of best available move that will be accepted for bulwark skill moves.
    Used in: Move Evaluation
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_BulwarkThresholdPercentage = 81,

    /*
    Description: Whether to log influence map calculations.
    Used in: Debugging
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: String_InfluenceMapCalculationLogDirectory
    */
    Bool_LogInfluenceMapCalculations = 82,

    /*
    Description: Whether to log behavior tree logic.
    Used in: Debugging
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: String_InfluenceMapCalculationLogDirectory
    */
    Bool_LogBehaviorTreeLogic = 135,

    /*
    Description: Directory for influence map logs. Can be an absolute directory (e.g. c:\tmp, beware OS dependencies) or relative to BattleTech.
    Used in: Debugging
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Bool_LogInfluenceMapCalculations
    */
    String_InfluenceMapCalculationLogDirectory = 83,

    /*
    Description: Whether to include influence factors that have a 0 weight in the relevant JSON.
    Used in: Debugging
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Bool_LogInfluenceMapCalculations, String_InfluenceMapCalculationLogDirectory
    */
    Bool_InfluenceMapCalculationLogIncludeZeroWeightedFactors = 84,

    /*
    Description: Whether the AI should always be in defensive mood
    Used in: Debugging
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Bool_AlwaysOffensiveMood
    */
    Bool_AlwaysDefensiveMood = 85,

    /*
    Description: Whether the AI should always be in offensive mood
    Used in: Debugging
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Bool_AlwaysDefensiveMood
    */
    Bool_AlwaysOffensiveMood = 86,

    /*
    Description: factor to multiply sprint move evaluations by before comparing them to regular move evaluations
    Used in: Move evaluation
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_SprintWeightBiasAdditive
    */
    Float_SprintWeightBiasMultiplicative = 110,

    /*
    Description: factor to add to sprint move evaluations before comparing them to regular move evaluations
    Used in: Move evaluation
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_SprintWeightBiasMultiplicative
    */
    Float_SprintWeightBiasAdditive = 111,

    /*
    Description: base percentage chance (0-100) that a side will reserve
    Used in: reserve calculations
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_SprintWeightBiasMultiplicative
    */
    Float_ReserveBasePercentage = 112,

    /*
    Description: Hostile increment X, in "for every X percent of units after me, increase chance to reserve by Y percent"
    Used in: reserve calculations
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_SprintWeightBiasMultiplicative
    */
    Float_ReserveHostilePercentageIncrementX = 113,

    /*
    Description: Hostile increment Y, in "for every X percent of units after me, increase chance to reserve by Y percent"
    Used in: reserve calculations
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_SprintWeightBiasMultiplicative
    */
    Float_ReserveHostilePercentageIncrementY = 114,

    /*
    Description: UNUSED - DO NOT USE Hostile vulnerability factor - the percentage (0-100) amount that each hostile after me that is vulnerable to called shots will decrease our chance to reserve. Note: POSITIVE values reduce chance to reserve.
    Used in: reserve calculations
    OK to set via JSON: NO
    OK to set via orders: NO
    See also: Float_SprintWeightBiasMultiplicative
    */
    Float_ReserveHostileVulnerabilityPercentage = 115,

    /*
    Description: UNUSED - DO NOT USE Self vulnerability factor - the percentage (0-100) amount that each unit on my side that is vulnerable to called shots will decrease our chance to reserve. Note: POSITIVE values reduce chance to reserve.
    Used in: reserve calculations
    OK to set via JSON: NO
    OK to set via orders: NO
    See also: Float_SprintWeightBiasMultiplicative
    */
    Float_ReserveSelfVulnerabilityPercentage = 116,

    /*
    Description: Reserve Median Offset Factor - if 0, any reserves will go to the median turn number of hostile forces. Non-zero values are *added* to the median turn number, so if you want to go AFTER the median, use negative numbers.
    Used in: reserve calculations
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_SprintWeightBiasMultiplicative
    */
    Float_ReserveHostileOffset = 117,

    /*
    Description: Working data to keep track of which round a team has done reserve calculations for. Will be set during the first activation for this round, and the value will be the round number.
    Used in: reserve calculations
    OK to set via JSON: NO
    OK to set via orders: NO
    See also:
    */
    Int_ReserveCalculationsLastDoneForRoundNumber = 118,

    /*
    Description: Working data to keep track of which phase a team has done reserve calculations for. Will be set during the first activation for this phase, and the value will be the phase number.
    Used in: reserve calculations
    OK to set via JSON: NO
    OK to set via orders: NO
    See also: Int_ReserveCalculationsLastDoneForRoundNumber
    */
    Int_ReserveCalculationsLastDoneForPhaseNumber = 183,

    /*
    Description: Working data to keep track of which phase a team has decided to reserve to. Will be set to some illegal value (-1?) if the team is not reserving.
    Used in: reserve calculations
    OK to set via JSON: NO
    OK to set via orders: NO
    See also: Float_SprintWeightBiasMultiplicative
    */
    Int_ReserveToPhaseNumber = 119,

    /*
    Description: Whether to allow non-mechs to decide when to initiate reserving.
    Used in: reserve calculations
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Bool_ReserveByNonMechs = 120,

    /*
    Description: Whether to allow AI to reserve.
    Used in: reserve calculations
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Bool_ReserveEnabled = 121,

    /*
    Description: Whether to allow sprinting to locations reachable by regular (forward/reverse/jump) moves.
    Used in: movement calculations
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Bool_SprintToRegularLocations = 122,

    /*
    Description: Threshold Percentage (100.0 equals 100%) of target hit points before we start trying to multi-target.
    Used in: target selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_MultiTargetOverkillThreshold = 123,

    /*
    Description: Influence Factor Weight to avoid standing in locations where hostile fire could kill us.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_DefensiveOverkillFactor
    */
    Float_PreferNotLethalPositionFactorWeight = 124,

    /*
    Description: Influence Factor Weight to avoid sprinting to in locations where hostile fire could kill us.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_DefensiveOverkillFactor
    */
    Float_SprintPreferNotLethalPositionFactorWeight = 125,

    /*
    Description: Bias value to determine if we're conservative or aggressive when considering where people might attack us.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_PreferNotLethalPositionFactorWeight
    */
    Float_OverkillThresholdLowForLethalPositionFactor = 126,

    /*
    Description: Bias value to determine if we're conservative or aggressive when considering where people might attack us.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_PreferNotLethalPositionFactorWeight
    */
    Float_OverkillThresholdHighForLethalPositionFactor = 184,

    /*
    Description: Bias value to determine if we're conservative or aggressive when considering where people might attack us.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_PreferNotLethalPositionFactorWeight
    */
    Float_OverkillThresholdLowForRearArcPositionFactor = 190,

    /*
    Description: Bias value to determine if we're conservative or aggressive when considering where people might attack us.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_PreferNotLethalPositionFactorWeight
    */
    Float_OverkillThresholdHighForRearArcPositionFactor = 191,

    /*
    Description: If a hostile actor can do this percentage (100.0 equals 100%) of one of my unit's hit points, do not reserve.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_PreferNotLethalPositionFactorWeight
    */
    Float_OverkillFactorForReserve = 174,

    /*
    Description: Base chance to target the head when making a called shot. Not "out of" 100 or any fixed number, just out of the total target chances.
    Used in: called shot target selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_CalledShotHeadBaseChance = 127,

    /*
    Description: Base chance to target the center torso when making a called shot. Not "out of" 100 or any fixed number, just out of the total target chances.
    Used in: called shot target selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_CalledShotCenterTorsoBaseChance = 128,

    /*
    Description: Base chance to target other locations when making a called shot. Not "out of" 100 or any fixed number, just out of the total target chances.
    Used in: called shot target selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_CalledShotOtherBaseChance = 129,

    /*
    Description: Additional chance to target a location per point of expected weapon damage at that location when making a called shot. Not "out of" 100 or any fixed number, just out of the total target chances.
    Used in: called shot target selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_CalledShotWeaponDamageChance = 130,

    /*
    Description: Chance multiplier to target damaged locations when making a called shot.
    Used in: called shot target selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_CalledShotDamagedChanceMultiplier = 131,

    /*
    Description: Number of seconds per unit the AI is allowed to use.
    Used in: behavior tree evaluation
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_MaxThinkSeconds = 132,

    /*
    Description: Influence Factor Weight to prefer standing in locations where hostile fire could kill us from behind. (Probably want to invert this in JSON.)
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_DefensiveOverkillFactor
    */
    Float_PreferLethalDamageToRearArcFromHostileFactorWeight = 133,

    /*
    Description: Influence Factor Weight to prefer sprinting to in locations where hostile fire could kill us from behind.  (Probably want to invert this in JSON.)
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_DefensiveOverkillFactor
    */
    Float_SprintPreferLethalDamageToRearArcFromHostileFactorWeight = 134,

    /*
    Description: How far (in meters) a lance will allow itself to spread out when in non-interleaved mode. (TODO currently only used for sprinting to combat)
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_NonInterleavedLanceSpreadDistance = 136,

    /*
    Description: How far (in meters) a lance will allow itself to spread out when in interleaved mode. (TODO currently only used for sprinting to combat)
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_InterleavedLanceSpreadDistance = 137,

    /*
    Description: whether to take profiling data about the influence map calculations
    Used in: debugging / profiling
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Bool_ProfileInfluenceMapCalculations = 138,

    /*
    Description: Appetitive Influence Factor Weight to approach hostile's rear arc.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_AppetitivePreferApproachingRearArcOfHostileFactorWeight = 139,

    /*
    Description: Appetitive Influence Factor Weight to approach hostile's rear arc WHEN SPRINTING.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintAppetitivePreferApproachingRearArcOfHostileFactorWeight = 140,

    /*
    Description: Radius (in meters) to avoid sprinting within.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintExclusionRadius = 141,

    /*
    Description: Influence Factor Weight to be inside SprintExclusionRadius.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_SprintExclusionRadius
    */
    Float_PreferInsideSprintExclusionRadiusHostileFactorWeight = 142,

    /*
    Description: Influence Factor Weight to be inside SprintExclusionRadius WHEN SPRINTING. NB weights should be negative to get this to discourage sprinting near hostiles.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_SprintExclusionRadius
    */
    Float_SprintPreferInsideSprintExclusionRadiusHostileFactorWeight = 143,

    /*
    Description: Maximum distance (in meters) to hostile used for SprintAppetitivePreferApproachingRearArcOfHostileFactorWeight.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_SprintExclusionRadius
    */
    Float_AppetitiveBehindMaximumRadius = 144,

    /*
    Description: Appetitive Influence Factor Weight to approach ideal weapon range to hostile.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_AppetitivePreferIdealWeaponRangeToHostileFactorWeight = 145,

    /*
    Description: Appetitive Influence Factor Weight to approach ideal weapon range to hostile WHEN SPRINTING.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintAppetitivePreferIdealWeaponRangeToHostileFactorWeight = 146,

    /*
    Description: DEPRECATED Whether to use the new threat sorting that incorporates target damage output.
    Used in: target selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Bool_UseNewThreatDEPRECATED = 162,

    /*
    Description: Prefer this lance to surround hostile units.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferSurroundingHostileUnitsFactorWeight = 163,

    /*
    Description: Prefer this lance to surround hostile units WHEN SPRINTING.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferSurroundingHostileUnitsFactorWeight = 164,

    /*
    Description: Prefer not to be surrounded by hostile units.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferNotSurroundedByHostileUnitsFactorWeight = 165,

    /*
    Description: Prefer not surrounded by hostile units WHEN SPRINTING.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferNotSurroundedByHostileUnitsFactorWeight = 166,

    /*
    Description: Sprint Hysteresis Multiplier value, the value multiplies the "sprint juice level" when a sprint move happens. Values can be between 0.0 and 1.0, with larger values (closer to 1.0) leaving more "sprint juice" around for next turn.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintHysteresisMultiplier = 167,

    /*
    Description: Sprint Hysteresis Recovery Turns, the number of turns not sprinting that it would take to recover from an empty "sprint juice" reservoir.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintHysteresisRecoveryTurns = 168,

    /*
    Description: The window (0.0 - 1.0) below the calculated ideal damage differential that we thought that inspiration might get us that we'll be willing to accept on the first round of having inspire available.
    Used in: inspiration selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_InspirationBaseDamageWindow = 169,

    /*
    Description: The increment window (0.0 - 1.0) below the calculated ideal damage differential that we thought that inspiration might get us that we'll be willing to accept on successive rounds of having inspire available.
    Used in: inspiration selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_InspirationIncrementalDamageWindow = 170,

    /*
    Description: The minimum expected damage inspiration should get us before we use inspiration
    Used in: inspiration selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_MinimumInspirationDamage = 172,

    /*
    Description: weight for an influence factor that seeks to get the team to have equal "engagement" values
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferEqualizeEngagementOverTeamFactorWeight = 175,

    /*
    Description: weight for an influence factor that seeks to get the team to have equal "engagement" values when sprinting.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferEqualizeEngagementOverTeamFactorWeight = 176,

    /*
    Description: weight for an influence factor that seeks to get the lance to stay within a radius of the center of the lance
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_FenceRadius
    */
    Float_PreferStayInsideFenceNegativeLogicFactorWeight = 177,

    /*
    Description: weight for an influence factor that seeks to get the lance to stay within a radius of the center of the lance when sprinting
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferStayInsideFenceNegativeLogicFactorWeight = 178,

    /*
    Description: radius of fence to stay within
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_FenceRadius = 189,

    /*
    Description: Evasive "to hit" floor - if the to-hit is below this percentage (0.0 - 100.0), only shoot a single "conservative" shot.
    Used in: weapon selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_EvasiveToHitFloor = 179,

    /*
    Description: Number of points of damage that a sensor locking turn needs to do
    over a straight up shooting turn before deciding to sensor lock. Positive is a
    shooting bias, negative is a sensor lock bias.
    Used in: sensor locking determination
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_MinimumSensorLockQuality = 180,

    /*
    Description: Prefer to stand still if hostiles are within melee range.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferStationaryWhenHostilesInMeleeRangeFactorWeight = 181,

    /*
    Description: Prefer to stand still if hostiles are within melee range WHEN SPRINTING. Probably doesn't make sense for this to be non-zero.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferStationaryWhenHostilesInMeleeRangeFactorWeight = 182,

    /*
    Description:  Multiplier for how 'strong' each point of head armor is when looking for weak armor.
    Used in: target damage evaluation
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_HeadArmorMultiplier = 185,

    /*
    Description:  Multiplier for how 'strong' each point of front center torso armor is when looking for weak armor.
    Used in: target damage evaluation
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_CenterTorsoArmorMultiplier = 186,

    /*
    Description:  Multiplier for how 'strong' each point of rear center torso armor is when looking for weak armor.
    Used in: target damage evaluation
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_CenterTorsoRearArmorMultiplier = 187,

    /*
    Description:  Multiplier for how 'strong' each point of leg armor is when looking for weak armor on a legged mech.
    Used in: target damage evaluation
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_RemainingLegArmorMultiplier = 188,

    /*
    Description:  Multiplier for how 'strong' each point of rear center torso armor is when looking for weak armor specifically for the rear arc lethality influence factor.
    Used in: target damage evaluation
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_CenterTorsoRearArmorMultiplierForRearArc = 193,

    /*
    Description:  A percentage (0-100+) that "opportunity fire" has to be better than our designated target expected damage before preferring our own target over the designated target.
    Used in: target selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_OpportunityFireExceedsDesignatedTargetByPercentage = 192,

    /*
    Description:  A percentage (0-100+) that "opportunity fire" has to be better than our designated target expected damage before preferring our own target over the designated target.
    Used in: target selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_OpportunityFireExceedsDesignatedTargetFirepowerTakeawayByPercentage = 197,

    /*
    Description: the GUID of a sensor locked target
    Used in: sensor lock logic
    OK to set via JSON: NO
    OK to set via orders: NO
    See also:
    */
    String_SensorLockedTargetGUID = 194,

    /*
    Description: Weight for an influence factor that prefers taking firepower from enemies.
    Used in:
    OK to set via JSON: yes
    OK to set via orders: yes
    See also:
    */
    Float_PreferHigherFirepowerTakenFromHostileFactorWeight = 195,

    /*
    Description: Weight for an influence factor that prefers taking firepower from enemies WHEN SPRINTING.
    Used in:
    OK to set via JSON: yes
    OK to set via orders: yes
    See also:
    */
    Float_SprintPreferHigherFirepowerTakenFromHostileFactorWeight = 196,

    /*
    Description: Whether target designation uses firepower takeaway
    Used in:
    OK to set via JSON: yes
    OK to set via orders: yes
    See also:
    */
    Bool_TargetDesignationUsesFirepowerTakeaway = 198,

    /*
    Description: When predicting whether an attack will strip a pip, compare the attack hit probability to this threshold.
    Used in:
    OK to set via JSON: yes
    OK to set via orders: yes
    See also:
    */
    Float_PipStripAttackProbabilityThreshold = 199,

    /*
    Description: Influence Factor Weight to control the desire to be in locations with ranged to hit penalties.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferLowerRangedToHitPenaltyLocationsFactorWeight = 200,

    /*
    Description: Influence Factor Weight to control the desire to be in locations with ranged to hit penalties WHEN SPRINTING.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferLowerRangedToHitPenaltyLocationsFactorWeight = 201,

    /*
    Description: Influence Factor Weight to control the desire to be in locations with ranged defense bonus.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferHigherRangedDefenseBonusLocationsFactorWeight = 202,

    /*
    Description: Influence Factor Weight to control the desire to be in locations with ranged defense bonus WHEN SPRINTING.
    Used in: Combat
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintPreferHigherRangedDefenseBonusLocationsFactorWeight = 203,

    /*
    Description:  Multiplier applied when considering the value of a
           downed mech when calculating the centerpoint of the
           lance fence. 1.0 would be no special consideration, 2.0
           would make a downed mech as important as two other
           units. Values less than one would give the lance a
           tendency to abandon their wounded.
    Used in: Cohesion
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_DownedMechFenceContributionMultiplier = 204,

    /*
    Description: Percentage (0.0 - 100.0+) of my "critical hit points"
           (weakest armor, vital structure) above which the AI
           won't brace to get rid of instability.
    Used in: bracing
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_UnsteadyOverkillThreshold = 205,

    /*
    Description: GUID of a guard lance. Don't outrun your coverage!
    Used in: patrol route following
    OK to set via JSON: NO
    OK to set via orders: Yes
    See also:
    */
    String_GuardLanceGUID = 206,

    /*
    Description: Percentage (0-100+) of the guard lance's max speed that this lance can move
    Used in: patrol route following
    OK to set via JSON: NO
    OK to set via orders: Yes
    See also:
    */
    Float_GuardLanceSpeedPercent = 207,

	/*
    Description: Distance that an escorted unit can move from the closest member of its escorting lance
    Used in: patrol route following
    OK to set via JSON: NO
    OK to set via orders: Yes
    See also:
    */
	Float_GuardLanceTetherDistance = 227,

	/*
    Description: Fraction (0.0 - 1.0+) of a unit's heat generated in an
           attack to consider when filtering out movement
           destinations.
    Used in: movement filtering
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: DamageFracForHeatFilter
    */
	Float_HeatFracForHeatFilter = 208,

    /*
    Description: Fraction (0.0 - 1.0+) of a unit's damage generated in
           an attack to consider when filtering out movement
           destinations.
    Used in: movement filtering
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: DamageFracForHeatFilter
    */
    Float_DamageFracForHeatFilter = 209,

    /*
    Description: Whether to use Bulwark actions.
    Used in: action selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Bool_UseBulwarkActions = 210,

    /*
    Description: Percent chance to use "Reckless" skill (move after shooting) on any given turn.
    Used in: action selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_UseRecklessSkillPercentageChance = 211,

    /*
    Description: Weight for how much to prefer to be in "excluded" regions. Probably negative.
    Used in: movement selection around artillery
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_ExcludedRegionWeight = 214,

    /*
    Description: Weight for how much to prefer to be in "excluded" regions WHEN SPRINTING. Probably negative.
    Used in: movement selection around artillery
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintExcludedRegionWeight = 215,

    /*
    Description: Weight for how much to prefer to be exposed to enemy fire alone. Probably negative.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_AlonePreferenceWeight = 216,

    /*
    Description: Weight for how much to prefer to be exposed to enemy fire alone WHEN SPRINTING. Probably negative.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintAlonePreferenceWeight = 217,

    /*
    Description: Negative weight to incentivize moving to a firing location when a buddy is exposed to enemy fire. Probably negative.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_BuddyAloneFiringSolutionPreferenceWeight = 218,

    /*
    Description: Negative weight to incentivize moving to a firing location when a buddy is exposed to enemy fire WHEN SPRINTING. Probably negative.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintBuddyAloneFiringSolutionPreferenceWeight = 219,

    /*
    Description: How long is it OK to be alone exposed to enemy fire.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Int_AloneToleranceTurnCount = 222,

    /*
    Description: How long to cool off after being exposed to enemy fire.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Int_AloneCoolDownTurnCount = 223,

    /*
    Description: Last turn number when I was alone.
    Used in: movement selection
    OK to set via JSON: NO
    OK to set via orders: NO
    See also:
    */
    Int_LastAloneRoundNumber = 224,

    /*
    Description: Last turn number when I was not alone.
    Used in: movement selection
    OK to set via JSON: NO
    OK to set via orders: NO
    See also:
    */
    Int_LastNotAloneRoundNumber = 225,

    /*
    Description: Negative weight to incentivize moving to a buddy that is exposed to enemy fire. Probably negative.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_BuddyAloneMoveNearbyPreferenceWeight = 220,

    /*
    Description: Negative weight to incentivize moving to a buddy that is exposed to enemy fire WHEN SPRINTING. Probably negative.
    Used in: movement selection
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_SprintBuddyAloneMoveNearbyPreferenceWeight = 221,

    /*
    Description: Whether to allow long range pathfinding when following routes. (Defaults to True)
    Used in: patrol route logic
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Bool_AllowLongRangePathfindingWhenPatrolling = 226,

    /*
    Description: Whether to use dynamic hex-based long range pathfinding
    Used in: long range route logic
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Bool_UseDynamicLongRangePathfinding = 228,

    /*
    Description: How fast (percentage of full speed, 0% = 0, 100% = 100.0) to follow patrol routes
    Used in: patrol route following
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PatrolRouteThrottlePercentage = 229,

    /*
     Description: If we have priority targets, should we only consider priority
         targets when evaluating the "hostile" phase of influence maps?
     Used in: influence map evaluation
     OK to set via JSON: Yes
     OK to set via orders: Yes
     See also:
     */
     Bool_FilterHostileInfluenceMapsToPriorityTargets = 230,

    /*
     Description: If true, drop out of long range pathfinding if we can walk to
         a destination with LOF to a hostile. (TODO: roll this behavior out to
         all encounters.)
     Used in: pathfinding
     OK to set via JSON: Yes
     OK to set via orders: Yes
     See also:
     */
    Bool_SimpleShortRangeLOF = 231,

    /*
     Description: If true, apply "ruthless" mood to AIs that have priority
         targets and do not consider non-LOF destinations if destinations exist
         that have LOF
     Used in: pathfinding
     OK to set via JSON: Yes
     OK to set via orders: Yes
     See also:
     */
    Bool_RuthlessPriorityTargeting = 232,

    /*
     Description: If pilot has the "Vent Coolant" ability, allow the mech
         to run hotter to this threshold
     Used in: heat management
     OK to set via JSON: Yes
     OK to set via orders: Yes
     See also: Float_AcceptableHeatLevel
     */
    Float_VentCoolantHeatThreshold = 233,

    /*
     Description: If pilot has the "Vent Coolant" ability, consider using it
         when above this health threshold
     Used in: heat management
     OK to set via JSON: Yes
     OK to set via orders: Yes
     See also:
     */
    Int_VentCoolantHealthThreshold = 234,

    /*
     Description: If pilot has the "Sure Footing" ability, boost the value of
         all walk moves by this amount
     Used in: move selection
     OK to set via JSON: Yes
     OK to set via orders: Yes
     See also:
     */
    Float_SureFootingAbilityWalkBoost = 235,

    /*
     Description: If true, allow attacks, otherwise, just do move and then brace.
     Used in: attack logic
     OK to set via JSON: Yes
     OK to set via orders: Yes
     See also:
     */
    Bool_AllowAttack = 236,

    /*
    Description: If true, we are trying to navigate out of a nook
    Used in: movement logic
    OK to set via JSON: No
    OK to set via orders: No
    See also:
    */
    Bool_IsNavigatingNook = 237,

    /*
     Description: If true, we have no LOF to any hostiles
     Used in: attack logic
     OK to set via JSON: No
     OK to set via orders: No
     See also:
     */
    Bool_NoLOFToHostiles = 238,

    /*
     Description: Weight preference for friendly ECM fields
     Used in: attack logic
     OK to set via JSON: Yes
     OK to set via orders: Yes
     See also:
     */
     Float_PreferFriendlyECMFields = 239,

    /*
     Description: Weight preference for friendly ECM Fields while sprinting
     Used in: attack logic
     OK to set via JSON: Yes
     OK to set via orders: Yes
     See also:
     */
     Float_SprintPreferFriendlyECMFields = 240,

    /*
    Description: Weight preference for hostile ECM fields
    Used in: attack logic
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferHostileECMFields = 241,

    /*
     Description: Weight preference for hostile ECM Fields while sprinting
     Used in: attack logic
     OK to set via JSON: Yes
     OK to set via orders: Yes
     See also:
     */
    Float_SprintPreferHostileECMFields = 242,

    /*
    Description: Number of nearby enemy units needed to trigger active probe
    Used in: active probe determination
    OK to set via JSON: Yes
    OK to set via orders: NO
    See also:
    */
    Int_MinimumActiveProbeCount = 243,

    /*
    Description: Is true when we have active probe targets
    Used in: active probe logic
    OK to set via JSON: NO
    OK to set via orders: NO
    See also:
    */
    Bool_HasActiveProbeTargets = 244,

    /*
     Description: Weight preference for hostiles average point within ECM fields while
     carrying Active Probe.
     Used in: attack logic
     OK to set via JSON: Yes
     OK to set via orders: No
     See also:
     */
    Float_PreferActiveProbePositions = 245,

    /*
     Description: Weight sprint preference for hostiles average point within ECM fields while
     carrying Active Probe.
     Used in: attack logic
     OK to set via JSON: Yes
     OK to set via orders: No
     See also:
     */
    Float_SprintPreferActiveProbePositions = 246,

    /*
     Description: The minimum number of stealth pips we must have in order to fire under ECM
     Used in: attack logic
     OK to set via JSON: Yes
     OK to set via orders: No
     See also:
     */
    Int_MinimumECMGhostedPipsToFire = 247,

    /*
     Description: Bool to force enable or disable urban biome navigation modes
     Used in: attack logic
     OK to set via JSON: Yes
     OK to set via orders: No
     See also:
     */
    Bool_EnableUrbanBiomeNavigation = 248,

    /*
     Description: Bool to force enable or disable urban biome navigation modes outside of urban biomes
     Used in: attack logic
     OK to set via JSON: Yes
     OK to set via orders: No
     See also:
     */
    Bool_EnableUrbanBiomeNavigationEverywhere = 249,

    /*
     Description: Float to track confidence in causing structural damage. Used to gague whether we should fire our weapon
     from a ghosted state
     Used in: attack logic
     OK to set via JSON: Yes
     OK to set via orders: No
     See also:
     */
    Float_ConfidenceInSignificantDamageWhileGhostedLevel = 250,

    /*
     When calculatiing expected damage, how much to lerp between the damage / number of weapons and the damage if every hit
     was in the same location. 0 is damage divided and 1 is damage concentrated.
     Increases with Float_GhostStateHysteresisMultiplierTurnIncrease
     Used in: attack logic
     OK to set via JSON: Yes
     OK to set via orders: No
     See also:
     */
    Float_WeaponDamageSpreadLerpValue = 251,

    /*
     When getting the chance in causing significant damage, how much weight to give the amount of structural damage caused.
     Used in: attack logic
     OK to set via JSON: Yes
     OK to set via orders: No
     See also:
     */
    Float_StructuralDamagePercentageMultiplier = 252,


    /*
    When shooting in ghost state, lerp value to go from expected damage and max possible damage.
    Increases with Float_GhostStateHysteresisMultiplierTurnIncrease
    OK to set via JSON: Yes
    OK to set via orders: No
    See also:
    */
    Float_ExpectedAndMaxDamageShootingInGhostStateLerp = 253,

    /*
   For every round the ai does not shoot in ghost state, it adds to a mutiplier that increases the confidence in causing
   higher damage
   OK to set via JSON: Yes
   OK to set via orders: No
   See also:
   */
    Float_GhostStateHysteresisMultiplierTurnIncrease = 254,

    /*
    For every round the ai does not shoot in ghost state, it adds to a mutiplier that increases the confidence in causing
    higher damage
    OK to set via JSON: Yes
    OK to set via orders: No
    See also:
  */
    Bool_ExpectedDamageAccuracyIncrease = 255,

    /*
    Description: Ideal distance to nearest Hostile
    Used in: Route following
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_OptimalHostileDistance = 256,

    /*
   Description: Influence Factor Weight to prefer locations close
       to optimal distance to other hostiles.
   Used in: Influence Maps
   OK to set via JSON: Yes
   OK to set via orders: Yes
   See also: Float_OptimalHostileDistance
   */
    Float_PreferOptimalDistanceToHostileFactorWeight = 257,

    /*
    Description: Influence Factor Weight to prefer locations close WHEN SPRINTING
        to optimal distance to other Hostiles.
    Used in: Influence Maps
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also: Float_OptimalHostileDistance
    */
    Float_SprintPreferOptimalDistanceToHostileFactorWeight = 258,

    /*
     Enables the long-range pathfinding to be aware of actors blocking movement in nodes globally.
     OK to set via JSON: Yes
     OK to set via orders: No
     See also:
 */
    Bool_EnableLongRangePathfindingBeActorAware = 259,

    /*
     How close to the target region do we switch to short-range pathfinding
     OK to set via JSON: Yes
     OK to set via orders: No
     See also:
 */
    Float_LongRangeToShortRangeDistanceThreshold = 260,

    /*
    Description: Weight penalty multiplier for losing the ECM counter aura when positioning.
    If set to 1, it represents -Float_PreferHostileECMFields or -Float_SprintPreferHostileECMFields
    Used in: attack logic
    OK to set via JSON: Yes
    OK to set via orders: Yes
    See also:
    */
    Float_PreferHostileECMFieldsPenaltyMultiplier = 261,
    
    /*
    Whether when enemy is ghosted, to use sprint or walk distance as marker to calculate influence map positional factors
    Lerp is 0 - 1, Walking to Sprinting
    OK to set via JSON: Yes
    OK to set via orders: No
    See also:
    */
    Float_SignalInWeapRngWhenEnemyGhostedWithinMoveDistance = 262

    // Next value: 263
}

[SerializableContract("BehaviorVariableValue")]
public class BehaviorVariableValue : IGuid
{
    [SerializableEnum("BehaviorVariableValueBehaviorVariableType")]
    public enum BehaviorVariableType
    {
        Undefined = 0,
        Float = 1,
        Int = 2,
        Bool = 3,
        String = 4,
        EncounterObjectGameLogic = 5,
    };

    [SerializableMember(SerializationTarget.SaveGame)]
    public BehaviorVariableType type;

    [fastJSON.JsonSerialized]
    [SerializableMember(SerializationTarget.SaveGame)]
    private float floatVal;

    [fastJSON.JsonSerialized]
    [SerializableMember(SerializationTarget.SaveGame)]
    private int intVal;

    [fastJSON.JsonSerialized]
    [SerializableMember(SerializationTarget.SaveGame)]
    private bool boolVal;

    [fastJSON.JsonSerialized]
    [SerializableMember(SerializationTarget.SaveGame)]
    private string stringVal;

    //[fastJSON.JsonSerialized]
    //private EncounterObjectGameLogic encounterObjectGameLogicVal;
    [SerializableMember(SerializationTarget.SaveGame)]
    public string GUID { get; private set; }

    /// <summary>
    /// This really only exists for serialization. Do not use.
    /// </summary>
    public BehaviorVariableValue()
    {
    }

    public BehaviorVariableValue(float f)
    {
        type = BehaviorVariableType.Float;
        FloatVal = f;
    }

    public BehaviorVariableValue(int i)
    {
        type = BehaviorVariableType.Int;
        IntVal = i;
    }

    public BehaviorVariableValue(bool b)
    {
        type = BehaviorVariableType.Bool;
        BoolVal = b;
    }

    public BehaviorVariableValue(string s)
    {
        type = BehaviorVariableType.String;
        StringVal = s;
    }

    public static BehaviorVariableValue NullBehaviorVariableValueWithType(BehaviorVariableType type)
    {
        BehaviorVariableValue val = new BehaviorVariableValue();
        val.type = type;
        return val;
    }

    public void SetGuid(string newGuid)
    {
        GUID = newGuid;
    }

    [fastJSON.JsonIgnore]
    public virtual float FloatVal
    {
        get
        {
            Debug.Assert(type == BehaviorVariableType.Float);
            return floatVal;
        }

        set
        {
            Debug.Assert(type == BehaviorVariableType.Float);
            type = BehaviorVariableType.Float;
            floatVal = value;
        }
    }

    [fastJSON.JsonIgnore]
    public virtual int IntVal
    {
        get
        {
            Debug.Assert(type == BehaviorVariableType.Int);
            return intVal;
        }

        set
        {
            Debug.Assert(type == BehaviorVariableType.Int);
            type = BehaviorVariableType.Int;
            intVal = value;
        }
    }

    [fastJSON.JsonIgnore]
    public virtual bool BoolVal
    {
        get
        {
            Debug.Assert(type == BehaviorVariableType.Bool);
            return boolVal;
        }

        set
        {
            Debug.Assert(type == BehaviorVariableType.Bool);
            type = BehaviorVariableType.Bool;
            boolVal = value;
        }
    }

    [fastJSON.JsonIgnore]
    public virtual string StringVal
    {
        get
        {
            Debug.Assert(type == BehaviorVariableType.String);
            return stringVal;
        }

        set
        {
            Debug.Assert(type == BehaviorVariableType.String);
            type = BehaviorVariableType.String;
            stringVal = value;
        }
    }
}

public class DefaultBehaviorVariableValue : BehaviorVariableValue
{
    static DefaultBehaviorVariableValue singletonValue;

    public static DefaultBehaviorVariableValue GetSingleton()
    {
        if (singletonValue == null)
        {
            singletonValue = new DefaultBehaviorVariableValue();
        }
        return singletonValue;
    }

    [fastJSON.JsonIgnore]
    public override float FloatVal
    {
        get
        {
            return 0.0f;
        }
        set
        {
            Debug.LogError("Cannot assign to default behavior variable singleton");
        }
    }

    [fastJSON.JsonIgnore]
    public override int IntVal
    {
        get
        {
            return 0;
        }

        set
        {
            Debug.LogError("Cannot assign to default behavior variable singleton");
        }
    }

    [fastJSON.JsonIgnore]
    public override bool BoolVal
    {
        get
        {
            return false;
        }

        set
        {
            Debug.LogError("Cannot assign to default behavior variable singleton");
        }
    }

    [fastJSON.JsonIgnore]
    public override string StringVal
    {
        get
        {
            return "**undefined**";
        }

        set
        {
            Debug.LogError("Cannot assign to default behavior variable singleton");
        }
    }
}


/// <summary>
/// Interface: AI order endpoint - implemented by things that need to be able to receive orders. Includes lances and
/// units (e.g. mechs, vehicles). Will typically just be copying order data into behavior variables.
/// </summary>
public interface IAIOrderEndpoint
{
    void IssueAIOrder(AIOrder order);

    void ResetBehaviorVariables();

    void SetBehaviorTree(BehaviorTreeIDEnum behaviorTreeID);

    void AddTargetPriorityRecord(ITargetPriorityRecord targetPriorityRecord);
}

public interface ITargetPriorityRecord : IGuid
{
    bool GetTargetPriorityForUnit(ICombatant unit, out int priority);
}

[SerializableContract("UnitTargetPriorityRecord")]
public class UnitTargetPriorityRecord : ITargetPriorityRecord
{
    [SerializableMember(SerializationTarget.SaveGame)]
    public string GUID { get; private set; }

    [SerializableMember(SerializationTarget.SaveGame)]
    string targetGUID;
    [SerializableMember(SerializationTarget.SaveGame)]
    int priority;

    public UnitTargetPriorityRecord(string targetGUID, int priority)
    {
        this.targetGUID = targetGUID;
        this.priority = priority;
    }

    private UnitTargetPriorityRecord()
    {
    }

    public bool GetTargetPriorityForUnit(ICombatant unit, out int priority)
    {
        priority = 0;
        if (unit.GUID == targetGUID)
        {
            priority = this.priority;
            return true;
        }
        return false;
    }

    public void SetGuid(string newGuid)
    {
        GUID = newGuid;
    }
}

[SerializableContract("RegionTargetPriorityRecord")]
public class RegionTargetPriorityRecord : ITargetPriorityRecord
{
    [SerializableMember(SerializationTarget.SaveGame)]
    public string GUID { get; private set; }

    [SerializableMember(SerializationTarget.SaveGame)]
    string regionGUID;
    [SerializableMember(SerializationTarget.SaveGame)]
    int priority;

    public RegionTargetPriorityRecord(string regionGUID, int priority)
    {
        this.regionGUID = regionGUID;
        this.priority = priority;
    }

    private RegionTargetPriorityRecord()
    {
    }

    public bool GetTargetPriorityForUnit(ICombatant target, out int priority)
    {
        priority = 0;
        AbstractActor targetActor = target as AbstractActor;

        if ((targetActor != null) && (targetActor.IsInRegion(regionGUID)))
        {
            priority = this.priority;
            return true;
        }
        // TODO do we care about buildings inside regions?
        return false;
    }

    public void SetGuid(string newGuid)
    {
        GUID = newGuid;
    }
}

[SerializableContract("TagSetAndMatchAllFlag")]
public class TagSetAndMatchAllFlag : IGuid
{
    [SerializableMember(SerializationTarget.SaveGame)]
    public HBS.Collections.TagSet TagSet;

    [SerializableMember(SerializationTarget.SaveGame)]
    public bool MustMatchAll;

	[SerializableMember(SerializationTarget.SaveGame)]
	public string GUID { get; private set; }

	public void SetGuid(string newGuid)
	{
		GUID = newGuid;
	}

	public TagSetAndMatchAllFlag(HBS.Collections.TagSet tagSet, bool mustMatchAll)
    {
        TagSet = tagSet;
        MustMatchAll = mustMatchAll;
    }

	// For Serialization
	private TagSetAndMatchAllFlag()
	{
	}

	public bool MatchesUnit(ICombatant unit)
    {
        int targetTagsMatched = 0;

        for (int tagIndex = 0; tagIndex < TagSet.Count; ++tagIndex)
        {
            string tag = TagSet[tagIndex];
            if (unit.EncounterTags.Contains(tag))
            {
                ++targetTagsMatched;
            }
        }

        return ((MustMatchAll && (targetTagsMatched == TagSet.Count)) ||
            ((!MustMatchAll) && (targetTagsMatched > 0)));
    }

	[PreSerialization]
	private void PreSerialization()
	{
		if( TagSet == null )
		{
			Debug.LogError("PreSerialization has NULL TAGSET");
		}
	}

	[PostDeserialization]
	private void PostDeserialize()
	{
		if( TagSet == null )
		{
			Debug.LogError("PostDeserialize has NULL TAGSET");
		}
	}
}

[SerializableContract("TaggedUnitTargetPriorityRecord")]
public class TaggedUnitTargetPriorityRecord : ITargetPriorityRecord
{
    [SerializableMember(SerializationTarget.SaveGame)]
    public string GUID { get; private set; }

    [SerializableMember(SerializationTarget.SaveGame)]
    int priority;
    [SerializableMember(SerializationTarget.SaveGame)]
    TagSetAndMatchAllFlag tagsAndMatchFlag;

    public TaggedUnitTargetPriorityRecord(HBS.Collections.TagSet targetTagSet, int priority, bool mustHaveAll)
    {
        this.priority = priority;
        this.tagsAndMatchFlag = new TagSetAndMatchAllFlag(targetTagSet, mustHaveAll);
    }

    private TaggedUnitTargetPriorityRecord()
    {
    }

    public bool GetTargetPriorityForUnit(ICombatant target, out int priority)
    {
        priority = 0;

        if (tagsAndMatchFlag.MatchesUnit(target))
        {
            priority = this.priority;
            return true;
        }
        return false;
    }

    public void SetGuid(string newGuid)
    {
        GUID = newGuid;
    }
}

[SerializableContract("BehaviorVariableScope")]
public class BehaviorVariableScope : IJsonTemplated, IGuid
{
    // Handled in the class Hydrate/Dehydrate Method
    // by convering it to a Dictionary<int, BehaviorVariableValue>
    [fastJSON.JsonSerialized]
    Dictionary<BehaviorVariableName, BehaviorVariableValue> behaviorVariables;

    // Handled by the RefContainer in the Hydrate/Dehydrate Method
    [fastJSON.JsonIgnore]
    public List<ITargetPriorityRecord> TargetPriorityRecords { get; private set; }

    // Handled by the RefContainer in the Hydrate/Dehydrate Method
    [fastJSON.JsonIgnore]
    public List<ITargetPriorityRecord> IgnoreTargetRecords { get; private set; }

    [fastJSON.JsonIgnore]
    [SerializableMember(SerializationTarget.SaveGame)]
    public List<string> MagicKnowledgeTargetGUIDs;

    [fastJSON.JsonIgnore]
    public List<TagSetAndMatchAllFlag> MagicKnowledgeTagSets;

    // Handled in the class Hydrate/Dehydrate Method
    // by convering it to a Dictionary<int, BehaviorVariableValue>
    [fastJSON.JsonIgnore]
    public Dictionary<AIMood, BehaviorVariableScope> ScopesByMood;

    public List<BehaviorVariableName> VariableNames
    {
        get
        {
            return new List<BehaviorVariableName>(behaviorVariables.Keys);
        }
    }

    [SerializableMember(SerializationTarget.SaveGame)]
    public string GUID { get; private set; }

    public BehaviorVariableScope()
    {
        behaviorVariables = new Dictionary<BehaviorVariableName, BehaviorVariableValue>();
        TargetPriorityRecords = new List<ITargetPriorityRecord>();
        IgnoreTargetRecords = new List<ITargetPriorityRecord>();
        MagicKnowledgeTargetGUIDs = new List<string>();
        MagicKnowledgeTagSets = new List<TagSetAndMatchAllFlag>();
        ScopesByMood = new Dictionary<AIMood, BehaviorVariableScope>();
    }

    public void SetVariable(BehaviorVariableName name, BehaviorVariableValue value)
    {
        Debug.Assert(behaviorVariables != null);

        if (behaviorVariables.ContainsKey(name))
        {
            behaviorVariables[name] = value;
        }
        else
        {
            behaviorVariables.Add(name, value);
        }
    }

    public void Reset()
    {
        behaviorVariables.Clear();
        TargetPriorityRecords.Clear();
        IgnoreTargetRecords.Clear();
        MagicKnowledgeTargetGUIDs.Clear();
        MagicKnowledgeTagSets.Clear();
    }

    public BehaviorVariableValue GetVariable(BehaviorVariableName name)
    {
        if (behaviorVariables.ContainsKey(name))
        {
            return behaviorVariables[name];
        }
        return null;
    }

    public BehaviorVariableValue GetVariableWithMood(BehaviorVariableName name, AIMood mood)
    {
        if (ScopesByMood.ContainsKey(mood))
        {
            BehaviorVariableValue value = ScopesByMood[mood].GetVariable(name);
            if (value != null)
            {
                return value;
            }
        }
        if (behaviorVariables.ContainsKey(name))
        {
            return behaviorVariables[name];
        }
        return null;
    }


    public void RemoveVariable(BehaviorVariableName name)
    {
        if (behaviorVariables.ContainsKey(name))
        {
            behaviorVariables.Remove(name);
        }
    }

    public void AddMagicKnowledge(string targetGUID)
    {
        if (!MagicKnowledgeTargetGUIDs.Contains(targetGUID))
        {
            MagicKnowledgeTargetGUIDs.Add(targetGUID);
        }
    }

    public void AddMagicKnowledge(HBS.Collections.TagSet targetTagSet, bool mustMatchAll)
    {
        TagSetAndMatchAllFlag tagsAndMatch = new TagSetAndMatchAllFlag(targetTagSet, mustMatchAll);

        if (!MagicKnowledgeTagSets.Contains(tagsAndMatch))
        {
            MagicKnowledgeTagSets.Add(tagsAndMatch);
        }
    }

    public void RemoveMagicKnowledge(string targetGUID)
    {
        if (MagicKnowledgeTargetGUIDs.Contains(targetGUID))
        {
            MagicKnowledgeTargetGUIDs.Remove(targetGUID);
        }
    }

    public void RemoveMagicKnowledge(HBS.Collections.TagSet targetTagSet, bool mustMatchAll)
    {
        TagSetAndMatchAllFlag tagsAndMatch = new TagSetAndMatchAllFlag(targetTagSet, mustMatchAll);

        if (MagicKnowledgeTagSets.Contains(tagsAndMatch))
        {
            MagicKnowledgeTagSets.Remove(tagsAndMatch);
        }
    }

    public void ResetMagicKnowledge()
    {
        MagicKnowledgeTargetGUIDs.Clear();
    }

    public bool IssueAIOrder(AIOrder order)
    {
        Debug.Assert(order != null, "Order should not be null!");
        Debug.Log("handling order: " + order);

        switch (order.OrderType)
        {
            case AIOrder.OrderTypeEnum.SetPatrolRoute:
                {
                    SetPatrolRouteAIOrder patrolOrder = order as SetPatrolRouteAIOrder;
                    Debug.Assert(patrolOrder != null);
                    SetVariable(BehaviorVariableName.String_RouteGUID, new BehaviorVariableValue(patrolOrder.routeToFollowGuid));
                    SetVariable(BehaviorVariableName.Bool_RouteStarted, new BehaviorVariableValue(false));
                    SetVariable(BehaviorVariableName.Bool_RouteCompleted, new BehaviorVariableValue(false));
                    SetVariable(BehaviorVariableName.Bool_RouteFollowingForward, new BehaviorVariableValue(patrolOrder.forward));
                    SetVariable(BehaviorVariableName.Bool_RouteShouldSprint, new BehaviorVariableValue(patrolOrder.shouldSprint));
                    SetVariable(BehaviorVariableName.Bool_RouteStartAtClosestPoint, new BehaviorVariableValue(patrolOrder.startAtClosestPoint));
                    return true;
                }
            case AIOrder.OrderTypeEnum.PreferTarget:
                {
                    RegionTargetPriorityAIOrder regionTarget = order as RegionTargetPriorityAIOrder;
                    if (regionTarget != null)
                    {
                        TargetPriorityRecords.Add(new RegionTargetPriorityRecord(regionTarget.RegionGUID, regionTarget.Priority));
                        return true;
                    }

                    UnitTargetPriorityAIOrder unitTarget = order as UnitTargetPriorityAIOrder;
                    if (unitTarget != null)
                    {
                        TargetPriorityRecords.Add(new UnitTargetPriorityRecord(unitTarget.TargetUnitGUID, unitTarget.Priority));
                        return true;
                    }

                    TaggedUnitTargetPriorityAIOrder tagTarget = order as TaggedUnitTargetPriorityAIOrder;
                    if (tagTarget != null)
                    {
                        TargetPriorityRecords.Add(new TaggedUnitTargetPriorityRecord(tagTarget.TargetTagSet, tagTarget.Priority, tagTarget.MustMatchAllTags));
                        return true;
                    }
                    return false;
                }
            case AIOrder.OrderTypeEnum.IgnoreTarget:
                {
                    IgnoreTaggedTargetsAIOrder ignoreTargetOrder = order as IgnoreTaggedTargetsAIOrder;
                    if (ignoreTargetOrder != null)
                    {
                        IgnoreTargetRecords.Add(new TaggedUnitTargetPriorityRecord(ignoreTargetOrder.TargetTagSet, 0, ignoreTargetOrder.MustMatchAllTags));
                        return true;
                    }
                    return false;
                }
            case AIOrder.OrderTypeEnum.MoveToLocation:
                {
                    MoveToLocationAIOrder locationAIOrder = order as MoveToLocationAIOrder;
                    SetVariable(BehaviorVariableName.Bool_RouteShouldSprint, new BehaviorVariableValue(locationAIOrder.shouldSprint));
                    SetVariable(BehaviorVariableName.Bool_RouteWithLance, new BehaviorVariableValue(locationAIOrder.withLance));

                    if (locationAIOrder != null)
                    {
                        if (locationAIOrder.beforeAttack)
                        {
                            if (locationAIOrder.withLance)
                            {
                                SetVariable(BehaviorVariableName.String_LancePreAttackDestinationGUID, new BehaviorVariableValue(locationAIOrder.routePointGUID));
                            }
                            else
                            {
                                SetVariable(BehaviorVariableName.String_UnitPreAttackDestinationGUID, new BehaviorVariableValue(locationAIOrder.routePointGUID));
                            }
                        }
                        else
                        {
                            if (locationAIOrder.withLance)
                            {
                                SetVariable(BehaviorVariableName.String_LancePostAttackDestinationGUID, new BehaviorVariableValue(locationAIOrder.routePointGUID));
                            }
                            else
                            {
                                SetVariable(BehaviorVariableName.String_UnitPostAttackDestinationGUID, new BehaviorVariableValue(locationAIOrder.routePointGUID));
                            }
                        }
                        return true;
                    }
                    Debug.Log("error casting order " + order);
                    return false;
                }
            case AIOrder.OrderTypeEnum.StayInsideRegion:
                {
                    StayInsideRegionAIOrder stayInsideAIOrder = order as StayInsideRegionAIOrder;
                    if (stayInsideAIOrder != null)
                    {
                        if (stayInsideAIOrder.regionGUID == null || stayInsideAIOrder.regionGUID == string.Empty)
                        {
                            RemoveVariable(BehaviorVariableName.String_StayInsideRegionGUID);
                        }
                        else
                        {
                            SetVariable(BehaviorVariableName.String_StayInsideRegionGUID, new BehaviorVariableValue(stayInsideAIOrder.regionGUID));
                        }
                        return true;
                    }
                    Debug.Log("error casting order " + order);
                    return false;
                }
            case AIOrder.OrderTypeEnum.SetBehaviorValue:
                {
                    SetBehaviorVariableAIOrder setBVAIOrder = order as SetBehaviorVariableAIOrder;
                    if (setBVAIOrder != null)
                    {
                        BehaviorVariableName name = setBVAIOrder.VariableName;
                        string nameString = name.ToString();

                        int underscoreIndex = nameString.IndexOf('_');
                        if (underscoreIndex == -1)
                        {
                            Debug.LogError("don't understand variable: " + nameString);
                            return false;
                        }

                        string typePrefix = nameString.Substring(0, underscoreIndex);

                        if (typePrefix == "Bool")
                        {
                            SetVariable(name, new BehaviorVariableValue(setBVAIOrder.BoolValue));
                        }
                        else if (typePrefix == "Float")
                        {
                            SetVariable(name, new BehaviorVariableValue(setBVAIOrder.FloatValue));
                        }
                        else if (typePrefix == "Int")
                        {
                            SetVariable(name, new BehaviorVariableValue(setBVAIOrder.IntValue));
                        }
                        else if (typePrefix == "String")
                        {
                            SetVariable(name, new BehaviorVariableValue(setBVAIOrder.StringValue));
                        }
                        else
                        {
                            Debug.LogError("don't understand varaiable: " + nameString);
                        }
                        return true;
                    }
                    Debug.Log("error casting order " + order);
                    return false;
                }
            case AIOrder.OrderTypeEnum.ResetAllBehaviorValues:
                behaviorVariables.Clear();
                return true;
            case AIOrder.OrderTypeEnum.RemoveBehaviorValue:
                RemoveBehaviorVariableAIOrder removeBVAIOrder = order as RemoveBehaviorVariableAIOrder;
                if (removeBVAIOrder != null)
                {
                    BehaviorVariableName name = removeBVAIOrder.VariableName;
                    behaviorVariables.Remove(name);
                    return true;
                }
                Debug.Log("error casting order " + order);
                return false;
            case AIOrder.OrderTypeEnum.AddMagicKnowledgeByTag:
                AddMagicKnowledgeByTagAIOrder magicKnowledgeByTagOrder = order as AddMagicKnowledgeByTagAIOrder;
                if (magicKnowledgeByTagOrder != null)
                {
                    HBS.Collections.TagSet targetTagSet = magicKnowledgeByTagOrder.TargetTagSet;
                    if (magicKnowledgeByTagOrder.AddMagicKnowledge)
                    {
                        AddMagicKnowledge(targetTagSet, magicKnowledgeByTagOrder.MustMatchAllTags);
                    }
                    else
                    {
                        RemoveMagicKnowledge(targetTagSet, magicKnowledgeByTagOrder.MustMatchAllTags);
                    }
                    return true;
                }
                Debug.Log("error casting order " + order);
                return false;
            case AIOrder.OrderTypeEnum.ResetMagicKnowledge:
                ResetMagicKnowledgeAIOrder resetMagicKnowledgeOrder = order as ResetMagicKnowledgeAIOrder;
                if (resetMagicKnowledgeOrder != null)
                {
                    ResetMagicKnowledge();
                    return true;
                }
                Debug.Log("error casting order " + order);
                return false;
            case AIOrder.OrderTypeEnum.ResetAllKnowledge:
                ResetAllKnowledgeAIOrder resetAllKnowledgeOrder = order as ResetAllKnowledgeAIOrder;
                if (resetAllKnowledgeOrder != null)
                {
                    Reset();
                    SetVariable(BehaviorVariableName.Bool_Alerted, new BehaviorVariableValue(false));
                    return true;
                }
                Debug.Log("error casting order " + order);
                return false;
            case AIOrder.OrderTypeEnum.SetBehaviorTree:
                // nothing to do here, let IAIOrderEndpoint handle this order.
                return true;
            case AIOrder.OrderTypeEnum.ProximityByTag:
                ProximityByTagAIOrder proximityOrder = order as ProximityByTagAIOrder;
                if (proximityOrder != null)
                {
                    SetVariable(BehaviorVariableName.Float_PreferProximityToTaggedTargetFactorDistance, new BehaviorVariableValue(proximityOrder.Distance));
                    SetVariable(BehaviorVariableName.String_PreferProximityToTaggedTargetFactorTag, new BehaviorVariableValue(proximityOrder.TargetTagSet[0]));
                    SetVariable(BehaviorVariableName.Float_PreferProximityToTaggedTargetFactorWeight, new BehaviorVariableValue(proximityOrder.InfluenceWeight));
                    SetVariable(BehaviorVariableName.Float_SprintPreferProximityToTaggedTargetFactorWeight, new BehaviorVariableValue(proximityOrder.SprintInfluenceWeight));
                    return true;
                }
                Debug.Log("error casting order " + order);
                return false;
            default:
                Debug.Log("AI Orders: Don't know what to do with " + order.OrderType);
                break;
        }
        return false;
    }

    #region Serialization
    public void SetGuid(string guid)
    {
        GUID = guid;
    }

    public void Hydrate(BattleTech.Save.Test.SerializableReferenceContainer references)
    {
        Dictionary<int, BehaviorVariableValue> serializableBehaviorVariables =
            references.GetItemDictionary<int, BehaviorVariableValue>(this, "serializableBehaviorVariables");

        Dictionary<int, BehaviorVariableScope> serializableScopesByMood =
            references.GetItemDictionary<int, BehaviorVariableScope>(this, "serializableScopesByMood");

        TargetPriorityRecords = references.GetItemList<ITargetPriorityRecord>(this, "TargetPriorityRecords");
        IgnoreTargetRecords = references.GetItemList<ITargetPriorityRecord>(this, "IgnoreTargetRecords");

        behaviorVariables =
            new Dictionary<BehaviorVariableName, BehaviorVariableValue>(serializableBehaviorVariables.Count);

        foreach( KeyValuePair<int, BehaviorVariableValue> kvp in serializableBehaviorVariables )
        {
            BehaviorVariableName key = (BehaviorVariableName)kvp.Key;
            BehaviorVariableValue value = kvp.Value;

            behaviorVariables.Add(key, value);
        }

        ScopesByMood = new Dictionary<AIMood, BehaviorVariableScope>(serializableScopesByMood.Count);

        foreach( KeyValuePair<int, BehaviorVariableScope> kvp in serializableScopesByMood )
        {
            AIMood key = (AIMood)kvp.Key;
            BehaviorVariableScope value = kvp.Value;

            ScopesByMood.Add(key, value);
        }

		MagicKnowledgeTagSets = references.GetItemList<TagSetAndMatchAllFlag>(this, "MagicKnowledgeTagSets");
    }

    public void Dehydrate(BattleTech.Save.Test.SerializableReferenceContainer references)
    {
        Dictionary<int, BehaviorVariableValue> serializableBehaviorVariables =
            new Dictionary<int, BehaviorVariableValue>(behaviorVariables.Count);

        Dictionary<int, BehaviorVariableScope> serializableScopesByMood =
            new Dictionary<int, BehaviorVariableScope>(ScopesByMood.Count);

        foreach( KeyValuePair<BehaviorVariableName, BehaviorVariableValue> kvp in behaviorVariables )
        {
            int key = (int)kvp.Key;
            BehaviorVariableValue value = kvp.Value;

            serializableBehaviorVariables.Add(key, value);
        }

        foreach( KeyValuePair<AIMood, BehaviorVariableScope> kvp in ScopesByMood )
        {
            int key = (int)kvp.Key;
            BehaviorVariableScope value = kvp.Value;

            serializableScopesByMood.Add(key, value);
        }

        references.AddItemDictionary(this, "serializableBehaviorVariables", serializableBehaviorVariables);
        references.AddItemDictionary(this, "serializableScopesByMood", serializableScopesByMood);
        references.AddItemList(this, "TargetPriorityRecords", TargetPriorityRecords);
        references.AddItemList(this, "IgnoreTargetRecords", IgnoreTargetRecords);
		references.AddItemList(this, "MagicKnowledgeTagSets", MagicKnowledgeTagSets);
    }
    #endregion

    #region ITemplatedJSON
    public string ToJSON()
    {
        return JSONSerializationUtility.ToJSON(this);
    }

    public void FromJSON(string json)
    {
        JSONSerializationUtility.FromJSON(this, json);
    }

    public string GenerateJSONTemplate()
    {
        BehaviorVariableScope template = new BehaviorVariableScope();
        return JSONSerializationUtility.ToJSON<BehaviorVariableScope>(template);
    }
    #endregion //ITemplatedJSON
}
