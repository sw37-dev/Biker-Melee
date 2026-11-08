using GTA;
using GTA.Math;
using GTA.Native;
using System;
using System.IO;
using System.Media;
using System.Collections.Generic;

namespace BikerMeleeSP
{
    public class BikerMelee : Script
    {
        private string animWeapType;
        private Model bikeMeleeWpnModel;
        private Prop bikeMeleeWpnProp;
        private Prop passengerMeleeWpnProp;
        private bool holdingAttack;

        // Wanted level khi đá trúng phương tiện Emergency / Military.
        private const int WantedVehicleCooldown = 30000;
        private const int MaxWantedLevel = 5;

        // Mức giảm độ bám đường khi thời tiết ẩm ướt.
        private const int WeatherGripReductionLevel = 1;

        // Mức giảm độ bám đường khi chạy trên địa hình ngoài đường
        private const int OffRoadGripReductionLevel = 2;
        private const int TerrainCheckInterval = 500;
        private int nextTerrainCheckTime = -1;
        private int nextWeatherGripStateCheckTime = -1;
        private bool offRoadTerrainActive;
        private int currentWeatherGripLevel = -1;

        // DISTURBANCE WANTED LEVEL
        private const int DisturbanceRequiredHits = 12;     // 12 lần đá
        private const int DisturbanceWindowDuration = 40000;     // trong vòng 40 giây
        private const int DisturbanceWantedChance = 50;     // 50% tăng 1 sao truy nã
        private const int DisturbanceWantedCooldown = 60000;

        private int disturbanceHitCount;
        private int disturbanceWindowEndTime = -1;
        private int nextDisturbanceWantedTime;

        // WEATHER EFFECTS
        private const int WeatherCheckInterval = 30000;
        private const float WetWeatherKickSteerMagnitude = 0.20f;

        private int nextWeatherCheckTime = -1;

        private bool wetWeatherActive;
        private bool rainOrThunderActive;

        private Vehicle weatherGripVehicle;
        private bool weatherGripApplied;

        // FIRST KICK HELP TEXT SEQUENCE
        // The five tutorials run once, in order, after the player's first valid kick.
        private const int FirstKickHelpDuration = 10000;
        private const int FollowUpKickHelpDelay = 3000;
        private const int FollowUpKickHelpDuration = 7000;

        private const int FirstKickHelpStageInitial = 1;
        private const int FirstKickHelpStageSecondDelay = 2;
        private const int FirstKickHelpStageSecond = 3;
        private const int FirstKickHelpStageThirdDelay = 4;
        private const int FirstKickHelpStageThird = 5;
        private const int FirstKickHelpStageFourthDelay = 6;
        private const int FirstKickHelpStageFourth = 7;
        private const int FirstKickHelpStageFifthDelay = 8;
        private const int FirstKickHelpStageFifth = 9;
        private const int FirstKickHelpStageComplete = 10;

        private const string FirstKickHelpMessage =
            "On a multi-seat vehicle, press ~INPUT_CONTEXT~ to invite a teammate and pull off co-op kicks!";

        private const string CounterKickHelpMessage =
            "To counter knock-up kicks from other NPCs, press ~INPUT_MULTIPLAYER_INFO~ to " +
            "resist while riding a motorcycle or bicycle";

        private const string PassengerTyreAttackHelpMessage =
            "When riding with two people, press ~INPUT_DETONATE~ to have the passenger shoot " +
            "and puncture the tyres of a nearby target";

        private const string PassengerTransferHelpMessage =
            "Hold ~INPUT_CONTEXT~ for 3s to transfer your passenger to the nearest vacant " +
            "motorcycle or bicycle for automatic attacks";

        private const string PassengerReturnHelpMessage =
            "You can repeat this to call your teammates back to your vehicle. Also, while " +
            "the kick is active, you cannot switch vehicles";

        private bool firstKickHelpShown;
        private int firstKickHelpStage;
        private int firstKickHelpEndTime = -1;

        // DAMAGE SETTINGS
        // Keep the normal base kick at exactly 20 HP; only percentage layers below are tuned.
        private const int DefaultPedKickDamage = 20;
        private const float DefaultVehicleKickDamage = 20f;
        private const float ListedMeleeWeaponDamageMultiplier = 1.32f;

        // Chỉ các vũ khí cận chiến này mới tăng 32% sát thương cơ bản.
        // Tay không và mọi vũ khí khác giữ nguyên sát thương mặc định.
        private static readonly HashSet<uint> ListedMeleeWeaponDamageHashes = new HashSet<uint>
        {
            0x99B507EAu,
            0x678B81B1u,
            0x4E875F73u,
            0x958A4A8Fu,
            0x84BD7BFDu,
            0x440E4788u,
            0xF9E6AA4Bu,
            0x92A27487u,
            0xF9DCBF2Du,
            0xD8DF3C3Cu,
            0xDD5DF8D9u,
            0x8BB05FD7u,
            0xDFE37640u,
            0xCD274149u,
            0x94117305u,
            0x19044EE0u,
            0x3813FC08u,
            0x6589186Au,
            0xDAC00025u
        };

        private const float HighSpeedKickThreshold = 25.0f;     // ~90km/h
        private const float HighSpeedKickDamageMultiplier = 4.85f;

        // CHAIN OF FIVE HIGH-SPEED VEHICLE KICKS.
        // 25 m/s = 90 km/h. The streak accepts exactly 90 km/h and above.
        private const int HighSpeedVehicleKickStreakRequired = 5;
        private const float HighSpeedVehicleKickStreakThreshold = 25.0f;

        // A completed five-segment charge grants five Special Kick uses.
        // Each later use compounds +8.5% damage and +10% force on the previous use.
        private const int HighSpeedSpecialKickChargesPerFullCharge = 5;
        private const float HighSpeedSpecialKickDamageIncreasePerUse = 0.085f;
        private const float HighSpeedSpecialKickForceIncreasePerUse = 0.10f;

        // TEMPORARY KICK MASTERY REWARDS.
        // A qualifying hit must be made while the player's bike is moving above 50 km/h.
        private const float KickRewardHitSpeedThreshold = 13.8889f;
        private const int KickRewardAccumulationDuration = 40000;
        private const int KickRewardActiveDuration = 30000;
        private const int KickRewardTier1Hits = 8;
        private const int KickRewardTier2Hits = 15;
        private const int KickRewardTier3Hits = 30;
        private const float KickRewardTier2PassengerForceMultiplier = 1.22f;
        private const float KickRewardTier1DamageTakenMultiplier = 0.77f;
        private const float KickRewardTier3NpcDamageTakenMultiplier = 0.38f;
        private const float KickRewardTier1RecoilMultiplier = 0.80f;
        private const float KickRewardTier3CounterSteerMagnitude = 0.70f;
        private const int KickRewardTier3CounterSteerDuration = 1100;

        // INDEPENDENT KICK BUFF: 150 weighted successful hits fill the bar.
        // At 90-<110 km/h, add 7.5% per base hit-count unit; at >=110 km/h,
        // add 18% per base hit-count unit. A two-person hit therefore receives
        // twice the percentage bonus of a solo hit, as well as its base value of 2.
        private const int KickBuffHitsRequired = 150;
        private const float KickBuffMediumSpeedBonusPerHitCount = 0.075f;
        private const float KickBuffHighSpeedBonusPerHitCount = 0.18f;
        // Every completed Buff has a fixed 25-second base duration.
        // Normal charging adds 35% of the accumulation time, capped at 175 seconds.
        // If charging takes more than 500 seconds, use the 100-second fallback bonus.
        private const int KickBuffBaseActiveDuration = 25000;
        private const int KickBuffDefaultActiveDuration = 100000;
        private const int KickBuffDefaultTotalActiveDuration =
            KickBuffBaseActiveDuration + KickBuffDefaultActiveDuration;
        private const int KickBuffMaximumAccumulationDuration = 500000;
        private const int KickBuffMaximumAccumulationBonusDuration = 175000;
        private const float KickBuffAccumulationDrainRatio = 0.35f;

        // Helmet bonuses apply only to the player's Special Kick and stack on top
        // of its existing x8 damage and x4 force multipliers.
        private const float HelmetSpecialKickDamageMultiplier = 1.46f;
        private const float HelmetSpecialKickForceMultiplier = 1.12f;

        // Show the Buff explanation for ten seconds whenever the Buff bar starts draining.
        private const int KickBuffHelpDuration = 10000;

        // Hothands lasts for a fixed 20 seconds plus 8% of the Buff accumulation time.
        // If accumulation exceeds 500 seconds, the existing 100-second fallback is used.
        private const int KickBuffHothandsBaseDuration = 20000;
        private const double KickBuffHothandsAccumulationRatio = 0.08;
        private const int KickBuffDefaultHothandsDuration = 28000;

        // Player/ride invincibility lasts 23 seconds plus 9% of the accumulation time.
        // Accumulation beyond 500 seconds uses the existing 100-second fallback: 32 seconds total.
        private const int KickBuffInvincibilityBaseDuration = 23000;
        private const double KickBuffInvincibilityAccumulationRatio = 0.09;
        private const int KickBuffDefaultInvincibilityDuration = 32000;

        // BikerMelee resolves contacts itself, so the native Hothands flag is paired
        // with one owned impact explosion at each confirmed player hit.
        private const int KickBuffExplosiveImpactType = 0; // Grenade-style impact.
        private const float KickBuffExplosiveImpactDamageScale = 3.88f;
        private const float KickBuffExplosiveImpactCameraShake = 1.25f;

        private const float KickBuffDamageMultiplier = 1.96f;
        private const float KickBuffForceMultiplier = 1.96f;
        private const float KickBuffDamageTakenMultiplier = 0.72f;
        private const float KickBuffTargetDamageBonusMultiplier = 1.22f;
        private const int KickBuffRagdollChance = 10;
        private const int KickBuffVehicleComponentDamageChance = 40;
        private const int KickBuffFlashIntervalMs = 400;
        private const int KickBuffFlashMax = 5;

        // 5% chance per successful direct vehicle kick to damage one random component.
        private const int RandomVehicleComponentDamageChance = 5;

        // PLAYER VEHICLE STEERING DISTURBANCE
        private const float PlayerKickSteerBiasMagnitude = 0.16f;
        private const float PlayerKickSteerInterpolation = 0.08f;
        private const int PlayerKickSteerHoldDuration = 700;

        // Khi người chơi đá trúng chính tài xế xe máy/xe đạp, 15% sẽ chuyển
        // từ hất văng sang đánh lái mạnh theo đúng phía bị đá.
        private const int NpcVictimBikeSteerChance = 15;
        private const float NpcVictimBikeSteerMagnitude = 0.95f;
        private const int NpcVictimBikeSteerDuration = 1100;

        private const float HighSpeedKickPenaltyThreshold = 30.5556f;     // ~110km/h

        // VEHICLE KICK CAMERA SHAKE
        private const float VehicleKickCameraShakeThreshold = 25.0f;
        private const float VehicleKickStrongCameraShakeThreshold = 30.5556f;

        // Biên độ rung camera.
        private const float VehicleKickCameraShakeIntensity = 0.20f;
        private const float VehicleKickStrongCameraShakeIntensity = 0.40f;

        // Thời gian rung camera.
        private const int VehicleKickCameraShakeDuration = 750;

        private int vehicleKickCameraShakeEndTime = -1;
        private bool vehicleKickCameraShakeActive;

        // OFF-ROAD VEHICLE KICK
        private const float OffRoadVehicleKickMultiplier = 0.70f;     // Một người: giảm 30% lực đá
        private const float TwoPersonOffRoadKickMultiplier = 0.50f;   // Hai người: giảm 50% lực đá và sát thương

        // Hậu quả riêng khi có NPC ngồi ghế phụ.
        private const int TwoPersonHelmetRagdollChance = 15;
        private const int TwoPersonHelmetSelfDamage = 3;
        private const int TwoPersonNoHelmetSelfDamage = 15;

        // When the player is driving a bicycle (Vehicle Class 13), every direct
        // damage instance authored by this mod is increased by 27%.
        // This is applied after existing defense/reward/Buff modifiers and before
        // armor absorption, without changing any force or ragdoll behavior.
        private const float BicyclePlayerIncomingDamageMultiplier = 1.27f;

        // Hành vi truy đuổi có vũ khí chỉ được mở khóa khi có người ngồi ghế phụ.
        private const int ArmedVehicleChaseDuration = 60000;
        private const int ArmedVehicleAttackRefreshInterval = 1500;
        private const int ArmedVehicleChaseAccuracy = 15;
        private const int FiringPatternBurstFireDriveBy = unchecked((int)0xD31265F2);

        // Weapon pool
        private static readonly uint[] VehicleRetaliationWeaponHashes =
        {
            0x7846A318u,
            0x3AABBBAAu,
            0xEF951FBBu,
            0x12E82D3Du,
            0x05A96BA4u,
            0xBD248B55u,
            0x22D8FE39u,
            0x1BC4FDB9u,
            0x57A4368Cu,
            0xDC4DB296u,
            0x2B5EF5ECu,
            0x88374054u,
            0xBFE256D4u,
        };

        // Temporary passenger drive-by used to puncture the nearest vehicle tyre.
        private const Control PassengerTyreAttackControl = (Control)47; // INPUT_DETONATE
        private const int PassengerTyreAttackDuration = 6000;
        private const int PassengerTyreAttackCooldown = 60000;
        private const int PassengerTyreAttackTargetRefreshInterval = 500;
        private const int PassengerTyreAttackTyreHitDelay = 1200;
        private const int PassengerTyreAttackAccuracy = 85;
        private const int PassengerTyreAttackHitChance = 85;
        private const float PassengerTyreAttackTargetRadius = 70.0f;
        private const float PassengerTyreAttackForwardConeDot = 0.15f;
        private const float PassengerTyreAttackDriveByDistance = 70.0f;
        private const int PassengerTyreAttackAmmo = 9999;

        private static readonly uint[] PassengerTyreAttackWeaponHashes =
        {
            0x1B06D571u, // Pistol
            0xBFE256D4u, // Pistol Mk2
            0x5EF9FEC4u, // Combat Pistol
            0x99AEEB3Bu, // Pistol .50
            0xBFD21232u, // SNS Pistol
            0x88374054u, // SNS Pistol Mk2
            0xD205520Eu, // Heavy Pistol
            0x083839C4u, // Vintage Pistol
            0x2B5EF5ECu, // Ceramic Pistol
            0xDC4DB296u, // Marksman Pistol
            0xC1B3C3D1u, // Revolver
            0xCB96392Fu, // Revolver Mk2
            0x97EA20B8u, // Double Action Revolver
            0x917F6C8Cu, // Navy Revolver
            0x57A4368Cu, // Perico Pistol
            0x1BC4FDB9u, // WM29 Pistol
            0x22D8FE39u, // AP Pistol
        };

        private const int HighSpeedRagdollChance = 35;     // 35% ragdoll khi không đội mũ bảo hiểm
        private const int HelmetHighSpeedRagdollChance = 25;     // ragdoll 25% khi đội mũ bảo hiểm

        // Khi trời mưa hoặc giông bão.
        private const int WetWeatherRagdollChance = 55;

        // Khi trời mưa hoặc giông bão và đội mũ bảo hiểm.
        private const int HelmetWetWeatherRagdollChance = 45;

        // Xác suất người chơi mất HP và bị tăng cooldown.
        private const int HighSpeedSelfDamageChance = 30;

        // Lực bổ sung khi người chơi bị ragdoll ở tốc độ cao.
        private const float HighSpeedRagdollForwardBoost = 5.5f;

        // Lực nâng nhẹ để tạo hiệu ứng bay nhào về phía trước.
        private const float HighSpeedRagdollUpwardBoost = 3.5f;

        private const int HighSpeedSelfDamage = 10;     // 10 HP khi không đội mũ bảo hiểm
        private const int HelmetHighSpeedSelfDamage = 5;     // 5 HP khi đội mũ bảo hiểm

        private const int HighSpeedKickCooldown = 1500;     // cooldown đá 1.5 giây

        // Thời gian ragdoll.
        private const int HighSpeedRagdollDuration = 2200;

        // Thời điểm được phép thực hiện đòn đá tiếp theo.
        private int nextKickAllowedTime;

        // Special Kick progress is stored in quarter-segment units:
        // 4 units = one vehicle hit; 2 units = an on-foot NPC hit at >= 110 km/h;
        // 1 unit = an on-foot NPC hit at >= 90 and < 110 km/h.
        private const int HighSpeedVehicleKickStreakQuarterUnitsPerSegment = 4;
        private const int HighSpeedVehicleKickStreakQuarterUnitsRequired =
            HighSpeedVehicleKickStreakRequired * HighSpeedVehicleKickStreakQuarterUnitsPerSegment;
        private const int HighSpeedVehicleKickStreakVehicleHitQuarterUnits =
            HighSpeedVehicleKickStreakQuarterUnitsPerSegment;
        private const int HighSpeedVehicleKickStreakPedHitAt90QuarterUnits = 1;
        private const int HighSpeedVehicleKickStreakPedHitAt110QuarterUnits = 2;
        // Penalties now remove a randomly selected 1, 2, 3 or 4 quarter-units.
        // The stored five-use charge is unaffected.
        private int highSpeedVehicleKickStreakQuarterUnits;
        private int highSpeedKickStreakVehicleHandle;
        private bool highSpeedSpecialKickReady;
        // Stored charges are independent from the ride-session accumulation state.
        // Once filled, they survive dismounts, falls and vehicle changes.
        private int highSpeedSpecialKickChargesRemaining;

        // Chu kỳ thành tích 40 giây + thời gian hiệu lực phần thưởng 30 giây.
        private int kickRewardHitCount;
        private int kickRewardWindowEndTime = -1;
        private int kickRewardEndTime = -1;
        private int activeKickRewardLevel;

        // Kick Buff state is persistent across dismounts and independent from Special Kick.
        // Progress is 0..1; during the active phase it decreases continuously with game time.
        private float kickBuffProgress;
        private bool kickBuffActive;
        private bool kickBuffDrainStarted;
        private int kickBuffAccumulationStartTime = -1;
        // Once charging exceeds 500 seconds, stop tracking elapsed time and
        // retain the default-duration outcome until this Buff cycle completes.
        private bool kickBuffAccumulationLimitExceeded;
        private int kickBuffActiveDuration = KickBuffDefaultTotalActiveDuration;
        // Independent Hothands window; the Buff bar keeps using kickBuffActiveDuration.
        private int kickBuffHothandsDuration = KickBuffDefaultHothandsDuration;
        private int kickBuffInvincibilityDuration = KickBuffDefaultInvincibilityDuration;
        private int kickBuffHelpEndTime = -1;
        private string kickBuffHelpMessage;
        private int kickBuffLastUpdateTime = -1;
        private bool kickBuffFlashActive;
        private int kickBuffFlashCount;
        private int kickBuffFlashAlpha;
        private int kickBuffFlashLastTime = -1;

        // The timed Hothands/protection windows are anchored to the exact start
        // of the adaptive-duration drain, not to the beginning of the green flash.
        private int kickBuffDrainStartTime = -1;
        private int kickBuffNextProtectionRefreshTime = -1;

        private Ped kickBuffProtectedPlayer;
        private bool kickBuffPlayerWasInvincible;
        private bool kickBuffPlayerInvincibilityCaptured;

        private Vehicle kickBuffProtectedVehicle;
        private bool kickBuffVehicleWasInvincible;
        private bool kickBuffVehicleInvincibilityCaptured;

        // NPC lái xe máy/xe đạp tự động phản đòn khi người chơi vào tầm chân đá.
        // Cooldown độc lập theo từng NPC; không dùng cooldown đá của người chơi.
        private const float NpcBikeKickDetectionRadius = 4.0f;
        private const float NpcBikeKickContactRadius = 1.0f;
        private const int NpcBikeKickCooldown = 1500;
        private const int NpcBikeCounterAttackChance = 70;
        private const int NpcBikeKickCooldownCleanupInterval = 30000;
        // Mũ bảo hiểm giảm 33% sát thương từ cú đá phản đòn của NPC.
        private const float NpcBikeKickHelmetDamageMultiplier = 0.67f;

        // Broadphase NPC scans are intentionally throttled: 75 ms is responsive
        // enough for close-range kicks while avoiding repeated nearby-ped arrays.
        private const int NpcBikeKickDetectionInterval = 75;
        private const int VehicleChaseMaintenanceInterval = 100;
        private const int PassengerExitMaintenanceInterval = 100;
        private const int BikeWeaponPermissionRefreshInterval = 750;

        // Passenger-seat validation remains at 100 ms to preserve prompt transfers.
        private const int RecruitedPassengerMaintenanceInterval = 100;

        // Grip state is revalidated four times per second while an override is
        // active; terrain retains its original 500 ms sampling interval.
        private const int WeatherGripStateCheckInterval = 250;

        // Weapon selection is stable between polls; avoid one weapon native per frame.
        private const int OnFootWeaponSnapshotInterval = 100;
        private const int PassengerWeaponSyncCheckInterval = 100;

        // Protection flags are persistent entity state. Reassert them at 4 Hz;
        // Hothands itself remains refreshed every frame as required by the native.
        private const int KickBuffProtectionRefreshInterval = 250;

        // Validate steering ownership periodically but keep applying interpolated
        // bias every frame so the actual steering animation remains smooth.
        private const int SteeringEntityValidationInterval = 100;

        // INPUT_MULTIPLAYER_INFO: khi đang lái xe máy/xe đạp, giữ control này
        // để đỡ cú đá phản đòn bằng một cú lệch lái cực mạnh thay vì ngã xe.
        private const Control SpecialCounterKickModifierControl = (Control)20;
        private const float SpecialCounterKickSteerMagnitude = 0.95f;

        // Hash Unarmed dùng cho việc lưu/khôi phục và đồng bộ vũ khí.
        private const uint UnarmedWeaponHash = 0xA2719263u;

        // Special Kick HUD lấy đúng bố cục Player Lives trong KortzCenterHeist.
        private const float SpecialKickHudCenterX = 0.89f;
        private const float SpecialKickHudCenterY = 0.93f;
        private const float SpecialKickHudBackgroundWidth = 0.165f;
        private const float SpecialKickHudBackgroundHeight = 0.035f;
        private const float SpecialKickHudTextX = 0.82f;
        private const float SpecialKickHudTextY = 0.9188f;
        private const float SpecialKickHudBarLeft = 0.8955f;
        private const float SpecialKickHudBarWidth = 0.069f;
        private const float SpecialKickHudBarHeight = 0.021f;
        private const float SpecialKickHudSegmentGap = 0.0011f;

        // KICK BUFF HUD: same single continuous timerbar as the Kortz Loot Bag HUD.
        // It occupies the upper Loot Bag row; Special Kick remains on its own lower row.
        private const float KickBuffHudCenterX = 0.89f;
        private const float KickBuffHudCenterY = 0.890000045f;
        private const float KickBuffHudBackgroundWidth = 0.165f;
        private const float KickBuffHudBackgroundHeight = 0.03f;
        private const float KickBuffHudTextX = 0.836f;
        private const float KickBuffHudTextY = 0.878800035f;
        private const float KickBuffHudBarLeft = 0.8955f;
        private const float KickBuffHudBarWidth = 0.069f;
        private const float KickBuffHudBarHeight = 0.021f;

        private int nextNpcBikeKickScanTime = -1;
        private int nextNpcBikeKickCooldownCleanupTime = -1;
        // During the draining phase of Kick Buff, the recruited passenger attacks
        // on its own timer and is no longer driven by the player's attack inputs.
        private int nextBuffPassengerKickScanTime = -1;
        private int nextBuffPassengerKickAllowedTime = -1;
        private readonly Dictionary<int, int> npcBikeKickCooldowns = new Dictionary<int, int>();
        private readonly List<int> expiredNpcBikeKickHandles = new List<int>();

        private int nextVehicleChaseUpdateTime = -1;
        private int nextPassengerExitUpdateTime = -1;
        private int nextRecruitedPassengerUpdateTime = -1;

        private bool specialKickHudTextureRequested;
        private bool specialKickHudTextureLoaded;

        // Reused by the rare random door-damage branch to avoid allocating a
        // List<int> for every successful vehicle-component damage roll.
        private readonly int[] validVehicleDoorScratch = new int[6];

        // These values are model metadata, not per-entity state. Cache them by
        // model hash so recurring checks do not keep issuing identical natives.
        private static readonly Dictionary<int, int> VehicleClassByModelCache = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> VehicleSeatCountByModelCache = new Dictionary<int, int>();

        private uint lastBikeWeaponHash;
        private bool bikeWeaponSelectionInitialized;
        private bool bikeWeaponPermissionsApplied;
        private int nextBikeWeaponPermissionRefreshTime = -1;
        private int nextOnFootWeaponSnapshotTime = -1;
        private int nextPassengerWeaponSyncCheckTime = -1;
        private bool cachedPassengerCompanionForWeaponSync;
        private int lastSynchronizedPassengerWeaponHandle;

        // GTA may temporarily select a default firearm after the player boards a
        // motorcycle/bicycle. Preserve the actual on-foot selection and use that
        // snapshot for the whole ride instead of trusting the vehicle-forced hash.
        private uint lastOnFootWeaponHash = UnarmedWeaponHash;
        private bool onFootWeaponSnapshotInitialized;
        private uint bikeEntryWeaponHash = UnarmedWeaponHash;
        private bool bikeEntryWeaponSnapshotInitialized;
        private bool wasRidingMotorcycleOrCycle;

        private Vehicle playerKickSteerVehicle;
        private int nextPlayerKickSteerValidationTime = -1;

        private float currentPlayerKickSteerBias;
        private float targetPlayerKickSteerBias;

        private int playerKickSteerEndTime = -1;

        // Game time cooldown truy nã.
        private int nextWantedVehicleTime;

        private const int VehicleChaseDuration = 40000;     // 40 giây NPC truy đuổi
        private const int VehiclePanicFleeDuration = 20000;     // 20 giây NPC bỏ chạy
        // At most six NPCs may hold a kick-triggered chase/flee/armed-chase reaction
        // at the same time. The existing activeVehicleChases list owns their lifetimes.
        private const int MaximumActiveNpcKickReactions = 6;
        private const int VehiclePanicFleeSkidDelay = 10000;     // Bắt đầu trượt sau 10 giây
        private const int VehiclePanicFleeSkidGripLevel = 3;     // Mức giảm độ bám đường tối đa
        private const float VehiclePanicFleeSpeed = 31.94f;     // tốc độ bỏ chạy 115km/h

        // Giữ NPC của nhánh skid tiếp tục chạy nhanh sau mốc 10 giây thay vì
        // tới đích 300 m quá sớm; nhánh panic-flee cũ vẫn dùng khoảng cách gốc.
        private const float VehiclePanicFleeDistance = 300.0f;
        private const float VehiclePanicFleeSkidDistance = 1200.0f;

        // Các NPC đang trong trạng thái truy đuổi.
        private readonly List<VehicleChaseData> activeVehicleChases = new List<VehicleChaseData>();

        // Tách khỏi playerKickSteerVehicle vì hiệu ứng này tác động lên xe của NPC.
        private readonly List<NpcVictimSteeringData> activeNpcVictimSteeringEffects = new List<NpcVictimSteeringData>();

        private class VehicleChaseData
        {
            public Ped Driver;
            public Vehicle Vehicle;
            public int EndTime;
            public bool IsFleeing;

            // Chỉ áp dụng cho nhánh panic-flee thứ hai: sau 10 giây mô phỏng
            // mất phanh và giữ mức giảm bám đường tối đa đến hết 20 giây.
            public bool IsPanicSkidFlee;
            public int PanicSkidStartTime = -1;
            public bool PanicSkidStarted;

            // Trạng thái truy đuổi có vũ khí (chỉ dành cho hai người).
            public bool IsArmedChasing;
            public int LastArmedAttackAt = -1;
            public uint GrantedWeaponHash;
            public uint PreviousWeaponHash;
            public int PreviousAccuracy = -1;
        }

        // Steering tạm thời trên xe do NPC điều khiển khi cú đá rơi vào nhánh 15%.
        private class NpcVictimSteeringData
        {
            public Ped Driver;
            public Vehicle Vehicle;
            public float CurrentBias;
            public float TargetBias;
            public int EndTime = -1;
            public int NextValidationTime = -1;
        }

        // NPC được tuyển tạm thời để ngồi ghế phụ xe máy/xe đạp.
        private const float PassengerRecruitRadius = 3.0f;
        private const int PassengerEnterRetryInterval = 2500;

        // A recruited passenger can temporarily transfer to an empty nearby
        // motorcycle/bicycle and follow the player's bike as an independent rider.
        private const float PassengerTransferVehicleRadius = 8.0f;
        private const int PassengerTransferHoldDuration = 3000;
        private const int PassengerTransferVehicleScanInterval = 250;
        // The teammate uses the native vehicle-chase AI in Rushed mode. The
        // pursuit-distance override keeps the bike close without tailgating.
        private const float PassengerIndependentPursuitDistance = 10.0f;

        private enum PassengerTransferState
        {
            None = 0,
            LeavingPlayerBike = 1,
            EnteringIndependentBike = 2,
            LeavingIndependentBike = 3,
            ReenteringPlayerBike = 4
        }

        private class PassengerControlData
        {
            public Ped Passenger;
            public Vehicle Vehicle;
            public bool WasPersistent;
            public bool WasInvincible;
            public int NextEnterAttemptTime;

            // Vũ khí đang được NPC sử dụng trước khi được tuyển và các vũ khí
            // mà BikerMelee tạm cấp để đồng bộ với lựa chọn của người chơi.
            public uint PreviousWeaponHash;
            public readonly List<uint> GrantedWeaponHashes = new List<uint>();

            // Temporary six-second drive-by state. It is kept separate from the
            // normal synchronized weapon grants so ending the action cannot remove
            // weapons that the passenger already owned or that the mod synced.
            public bool TyreAttackActive;
            public int TyreAttackEndTime = -1;
            public int TyreAttackNextRefreshTime = -1;
            public int TyreAttackBurstTime = -1;
            public bool TyreAttackTaskStarted;
            public uint TyreAttackWeaponHash;
            public uint TyreAttackPreviousSelectedWeaponHash;
            public bool TyreAttackWeaponWasOwned;
            public int TyreAttackPreviousAmmo = -1;
            public int TyreAttackPreviousAccuracy = -1;
            public Vehicle TyreAttackTargetVehicle;
            public int TyreAttackWheelIndex = -1;
            public bool TyreAttackTargetHasWheel;
            public bool TyreAttackHitRollPassed;
            public bool TyreAttackImpactResolved;

            // The recruited teammate remains owned by this data object while
            // switching between the player's passenger seat and a separate bike.
            public bool IsIndependentRider;
            public Vehicle IndependentVehicle;
            public Vehicle TransferTargetVehicle;
            public PassengerTransferState TransferState;
            public int ContextHoldStartTime = -1;
            public bool RequireContextReleaseAfterTransfer;
            public int NextTransferVehicleScanTime = -1;
            public Vehicle CachedTransferVehicle;
            public int NextIndependentFollowTaskTime = -1;
            public int ReenterPlayerBikeStartTime = -1;
            public int LastFollowTargetVehicleHandle;
        }

        // Theo dõi NPC đang thực hiện task rời xe để tắt AlwaysKeepTask
        // ngay khi NPC đã xuống khỏi chiếc xe vừa được tuyển.
        private class PassengerExitData
        {
            public Ped Passenger;
            public int VehicleHandle;
            public bool WasPersistent;
            public bool WasInvincible;
        }

        private PassengerControlData recruitedPassenger;
        private readonly List<PassengerExitData> passengersLeavingVehicles = new List<PassengerExitData>();
        private int nextPassengerTyreAttackAllowedTime = -1;

        private Control leftAttackControl;
        private Control rightAttackControl;

        private SoundPlayer[] bikeHitSounds;
        private Stream[] bikeHitSoundStreams;

        private readonly Random soundRandom = new Random();
        // Keep penalty rolls independent so Special Kick losses do not alter the
        // existing randomized hit-sound sequence.
        private readonly Random specialKickPenaltyRandom = new Random(unchecked(Environment.TickCount ^ 0x6D2B79F5));

        private int[] availableSoundIndices = new int[0];
        private SoundPlayer currentPlayingSound;
        private InputMethod lastInputMethod;
        private bool inputMethodInitialized;

        public BikerMelee()
        {
            Function.Call(Native(-5184338789570016586L), "scr_solomon3");

            animWeapType = "melee_kick";

            leftAttackControl = (Control)24;
            rightAttackControl = (Control)25;

            LoadBikeHitSounds();

            Tick += OnTick;
            Aborted += OnAborted;
        }

        private static Hash Native(long value)
        {
            return (Hash)unchecked((ulong)value);
        }

        private static bool IsControlPressed(int controlGroup, Control control)
        {
            return Function.Call<bool>(Hash.IS_CONTROL_PRESSED, controlGroup, (int)control);
        }

        private static void DisableControlThisFrame(int controlGroup, Control control)
        {
            Function.Call(Hash.DISABLE_CONTROL_ACTION, controlGroup, (int)control, true);
        }

        // Kích hoạt quyền đổi vũ khí/drive-by của ped khi đang lái xe máy hoặc xe đạp.
        // Config flag 48 là BlockWeaponSwitching; false nghĩa là không khóa đổi vũ khí.
        private static void EnableBikeWeaponSelection(Ped player)
        {
            if (player == null || !player.Exists())
                return;

            Function.Call(Hash.SET_PED_CAN_SWITCH_WEAPON, player.Handle, true);
            Function.Call(Hash.SET_PED_CONFIG_FLAG, player.Handle, 48, false);

            // PLAYER native nhận Player index, không phải Ped handle.
            Function.Call(Hash.SET_PLAYER_CAN_DO_DRIVE_BY, Game.Player.Handle, true);
        }

        private static int GetGameDeadline(int duration)
        {
            return unchecked(Game.GameTime + duration);
        }

        private static bool IsGameTimeReached(int deadline)
        {
            return unchecked(Game.GameTime - deadline) >= 0;
        }

        // Kiểm tra thời tiết có gây ảnh hưởng đến độ bám đường hay không.
        private static int GetVehicleClassCached(Vehicle vehicle)
        {
            int modelHash = vehicle.Model.Hash;
            int vehicleClass;

            if (VehicleClassByModelCache.TryGetValue(modelHash, out vehicleClass))
                return vehicleClass;

            vehicleClass = Function.Call<int>(
                Hash.GET_VEHICLE_CLASS,
                vehicle.Handle);

            VehicleClassByModelCache[modelHash] = vehicleClass;
            return vehicleClass;
        }

        private static int GetVehicleSeatCountCached(Vehicle vehicle)
        {
            int modelHash = vehicle.Model.Hash;
            int seatCount;

            if (VehicleSeatCountByModelCache.TryGetValue(modelHash, out seatCount))
                return seatCount;

            seatCount = Function.Call<int>(
                Hash.GET_VEHICLE_MODEL_NUMBER_OF_SEATS,
                modelHash);

            VehicleSeatCountByModelCache[modelHash] = seatCount;
            return seatCount;
        }

        private static bool IsWetWeather(Weather weather)
        {
            switch (weather)
            {
                case Weather.Raining:
                case Weather.ThunderStorm:
                case Weather.Blizzard:
                case Weather.Snowing:
                case Weather.Snowlight:
                case Weather.Christmas:
                    return true;

                default:
                    return false;
            }
        }

        private static readonly string[] BikeHitSoundNames =
        {
            "KNIFE_STAB_3.wav",
            "SLOW_MO_MELEE_IMPACT_02.wav",
            "STOMP_KICK_3.wav"
        };

        // Kiểm tra thời tiết mưa hoặc giông bão.
        private static bool IsRainOrThunderWeather(Weather weather)
        {
            return weather == Weather.Raining || weather == Weather.ThunderStorm;
        }

        // Kiểm tra Ped có đang đội mũ bảo hiểm hay không.
        private static bool IsPedWearingHelmet(Ped ped)
        {
            if (ped == null || !ped.Exists())
                return false;

            try
            {
                return Function.Call<bool>(
                    Hash.IS_PED_WEARING_HELMET,
                    ped.Handle);
            }
            catch
            {
                return false;
            }
        }

        // Cập nhật thời tiết, địa hình và hiệu ứng giảm độ bám đường.
        private void UpdateWeatherAndBikeGrip(Ped player)
        {
            if (nextWeatherCheckTime < 0 ||
                IsGameTimeReached(nextWeatherCheckTime))
            {
                Weather currentWeather = World.Weather;

                wetWeatherActive = IsWetWeather(currentWeather);
                rainOrThunderActive = IsRainOrThunderWeather(currentWeather);

                nextWeatherCheckTime = GetGameDeadline(WeatherCheckInterval);
            }

            bool terrainCheckDue = nextTerrainCheckTime < 0 || IsGameTimeReached(nextTerrainCheckTime);

            // Nothing can change the grip state between scheduled terrain polls
            // in dry weather when no grip override is currently applied.
            if (!weatherGripApplied &&
                !wetWeatherActive &&
                !offRoadTerrainActive &&
                !terrainCheckDue)
            {
                return;
            }

            // While wet/off-road grip is active, avoid repeating vehicle, driver
            // and grip-state native checks on every rendered frame. A terrain poll
            // still bypasses this gate at its original 500 ms cadence.
            if (!terrainCheckDue &&
                nextWeatherGripStateCheckTime >= 0 &&
                !IsGameTimeReached(nextWeatherGripStateCheckTime))
            {
                return;
            }

            nextWeatherGripStateCheckTime = GetGameDeadline(
                WeatherGripStateCheckInterval);

            if (player == null ||
                !player.Exists() ||
                !player.IsInVehicle())
            {
                offRoadTerrainActive = false;
                RestoreWeatherGrip();
                nextTerrainCheckTime = GetGameDeadline(WeatherGripStateCheckInterval);
                return;
            }

            Vehicle currentBike = player.CurrentVehicle;

            if (currentBike == null ||
                !currentBike.Exists() ||
                !IsMotorcycleOrCycle(currentBike))
            {
                offRoadTerrainActive = false;
                RestoreWeatherGrip();
                nextTerrainCheckTime = GetGameDeadline(WeatherGripStateCheckInterval);
                return;
            }

            // Chỉ áp dụng khi người chơi thực sự là tài xế.
            try
            {
                Ped driver = currentBike.GetPedOnSeat(VehicleSeat.Driver);

                if (driver == null ||
                    !driver.Exists() ||
                    driver.Handle != player.Handle)
                {
                    offRoadTerrainActive = false;
                    RestoreWeatherGrip();
                    nextTerrainCheckTime = GetGameDeadline(WeatherGripStateCheckInterval);
                    return;
                }
            }
            catch
            {
                offRoadTerrainActive = false;
                RestoreWeatherGrip();
                nextTerrainCheckTime = GetGameDeadline(WeatherGripStateCheckInterval);
                return;
            }

            // Kiểm tra địa hình định kỳ.
            if (terrainCheckDue)
            {
                Weather currentWeather = World.Weather;

                if (IsDryWeatherForOffRoad(currentWeather))
                {
                    offRoadTerrainActive = !IsVehicleOnRoad(currentBike);
                }
                else
                {
                    offRoadTerrainActive = false;
                }

                nextTerrainCheckTime = GetGameDeadline(TerrainCheckInterval);
            }

            // Xác định mức giảm độ bám đường.
            int desiredGripLevel = 0;

            if (wetWeatherActive)
            {
                desiredGripLevel = WeatherGripReductionLevel;
            }
            else if (offRoadTerrainActive)
            {
                desiredGripLevel = OffRoadGripReductionLevel;
            }

            // Không cần giảm độ bám đường nếu không có điều kiện kích hoạt.
            if (desiredGripLevel <= 0)
            {
                RestoreWeatherGrip();
                return;
            }

            // Nếu đang áp dụng cho cùng một xe và cùng mức giảm
            if (weatherGripApplied &&
                weatherGripVehicle != null &&
                weatherGripVehicle.Exists() &&
                weatherGripVehicle.Handle == currentBike.Handle &&
                currentWeatherGripLevel == desiredGripLevel)
            {
                return;
            }

            // Khôi phục trạng thái cũ trước khi áp dụng trạng thái mới.
            RestoreWeatherGrip();

            try
            {
                Function.Call(
                    Hash.SET_VEHICLE_REDUCE_GRIP,
                    currentBike.Handle,
                    true);

                Function.Call(
                    Hash.SET_VEHICLE_REDUCE_GRIP_LEVEL,
                    currentBike.Handle,
                    desiredGripLevel);

                weatherGripVehicle = currentBike;
                weatherGripApplied = true;
                currentWeatherGripLevel = desiredGripLevel;
            }
            catch
            {
                RestoreWeatherGrip();
            }
        }

        private void RestoreWeatherGrip()
        {
            if (weatherGripApplied &&
                weatherGripVehicle != null &&
                weatherGripVehicle.Exists())
            {
                try
                {
                    Function.Call(
                        Hash.SET_VEHICLE_REDUCE_GRIP,
                        weatherGripVehicle.Handle,
                        false);

                    Function.Call(
                        Hash.SET_VEHICLE_REDUCE_GRIP_LEVEL,
                        weatherGripVehicle.Handle,
                        0);
                }
                catch
                {
                }
            }

            weatherGripVehicle = null;
            weatherGripApplied = false;
            currentWeatherGripLevel = -1;
        }

        // Tìm NPC gần nhất đủ điều kiện để tuyển làm người ngồi ghế phụ.
        private static Ped FindNearbyRecruitablePed(Ped player)
        {
            if (player == null || !player.Exists())
                return null;

            Ped[] nearbyPeds = World.GetNearbyPeds(player, PassengerRecruitRadius);

            if (nearbyPeds == null || nearbyPeds.Length == 0)
                return null;

            Ped closestPed = null;
            float closestDistanceSquared = PassengerRecruitRadius * PassengerRecruitRadius;
            Vector3 playerPosition = player.Position;

            foreach (Ped ped in nearbyPeds)
            {
                if (ped == null ||
                    !ped.Exists() ||
                    ped.Handle == player.Handle ||
                    !ped.IsHuman ||
                    !ped.IsAlive ||
                    ped.IsInVehicle())
                {
                    continue;
                }

                Vector3 pedPosition = ped.Position;
                float dx = pedPosition.X - playerPosition.X;
                float dy = pedPosition.Y - playerPosition.Y;
                float dz = pedPosition.Z - playerPosition.Z;
                float distanceSquared = dx * dx + dy * dy + dz * dz;

                if (distanceSquared <= closestDistanceSquared)
                {
                    closestDistanceSquared = distanceSquared;
                    closestPed = ped;
                }
            }

            return closestPed;
        }

        // Đưa NPC vào đúng ghế phụ (index 0) bằng hành vi vào xe thông thường.
        private void RequestPassengerEnterVehicle(PassengerControlData data)
        {
            if (data == null ||
                data.Passenger == null || !data.Passenger.Exists() ||
                data.Vehicle == null || !data.Vehicle.Exists())
            {
                return;
            }

            Function.Call(
                Hash.TASK_ENTER_VEHICLE,
                data.Passenger.Handle,
                data.Vehicle.Handle,
                -1,
                (int)VehicleSeat.Passenger,
                2.0f,
                1,
                0);

            data.NextEnterAttemptTime = GetGameDeadline(
                PassengerEnterRetryInterval);
        }

        // Đưa đồng đội lên ghế tài xế của chiếc xe riêng sau khi đã rời ghế phụ.
        private void RequestPassengerEnterDriverVehicle(
            PassengerControlData data,
            Vehicle targetVehicle)
        {
            if (data == null ||
                data.Passenger == null || !data.Passenger.Exists() ||
                targetVehicle == null || !targetVehicle.Exists())
            {
                return;
            }

            Function.Call(
                Hash.TASK_ENTER_VEHICLE,
                data.Passenger.Handle,
                targetVehicle.Handle,
                -1,
                (int)VehicleSeat.Driver,
                2.0f,
                1,
                0);

            data.NextEnterAttemptTime = GetGameDeadline(
                PassengerEnterRetryInterval);
        }

        // Check every model seat, including the driver seat (-1). This avoids
        // transferring the teammate into a bike that only looks unoccupied.
        private static bool IsVehicleEmptyExceptPed(Vehicle vehicle, Ped allowedPed)
        {
            if (vehicle == null || !vehicle.Exists() || vehicle.IsDead ||
                !IsMotorcycleOrCycle(vehicle))
            {
                return false;
            }

            int seatCount;
            try
            {
                seatCount = GetVehicleSeatCountCached(vehicle);
            }
            catch
            {
                return false;
            }

            if (seatCount < 1)
                return false;

            try
            {
                for (int seatIndex = -1; seatIndex < seatCount - 1; seatIndex++)
                {
                    int occupantHandle = Function.Call<int>(
                        Hash.GET_PED_IN_VEHICLE_SEAT,
                        vehicle.Handle,
                        seatIndex);

                    if (occupantHandle != 0 &&
                        (allowedPed == null ||
                         !allowedPed.Exists() ||
                         occupantHandle != allowedPed.Handle))
                    {
                        return false;
                    }
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        private Vehicle FindNearbyEmptyMotorcycleOrCycle(Ped player, Vehicle playerVehicle)
        {
            if (player == null || !player.Exists())
                return null;

            Vehicle[] nearbyVehicles = World.GetNearbyVehicles(
                player,
                PassengerTransferVehicleRadius);

            if (nearbyVehicles == null || nearbyVehicles.Length == 0)
                return null;

            Vehicle closestVehicle = null;
            float bestDistanceSquared =
                PassengerTransferVehicleRadius * PassengerTransferVehicleRadius;
            Vector3 playerPosition = player.Position;
            int playerVehicleHandle = playerVehicle != null && playerVehicle.Exists()
                ? playerVehicle.Handle
                : 0;

            foreach (Vehicle candidate in nearbyVehicles)
            {
                if (candidate == null || !candidate.Exists() || candidate.IsDead ||
                    (playerVehicleHandle != 0 &&
                     candidate.Handle == playerVehicleHandle) ||
                    !IsMotorcycleOrCycle(candidate))
                {
                    continue;
                }

                Vector3 candidatePosition = candidate.Position;
                float dx = candidatePosition.X - playerPosition.X;
                float dy = candidatePosition.Y - playerPosition.Y;
                float dz = candidatePosition.Z - playerPosition.Z;
                float distanceSquared = dx * dx + dy * dy + dz * dz;

                if (distanceSquared > bestDistanceSquared ||
                    !IsVehicleEmptyExceptPed(candidate, null))
                {
                    continue;
                }

                bestDistanceSquared = distanceSquared;
                closestVehicle = candidate;
            }

            return closestVehicle;
        }

        private void RecruitPassenger(Ped passenger, Vehicle vehicle)
        {
            if (recruitedPassenger != null ||
                passenger == null || !passenger.Exists() ||
                vehicle == null || !vehicle.Exists())
            {
                return;
            }

            if (!IsMotorcycleOrCycle(vehicle) ||
                !HasAtLeastTwoSeats(vehicle) ||
                !IsPlayerDrivingVehicle(Game.Player.Character, vehicle))
            {
                return;
            }

            // Không tuyển nếu ghế phụ đã có người.
            Ped currentPassenger = vehicle.GetPedOnSeat(VehicleSeat.Passenger);

            if (currentPassenger != null && currentPassenger.Exists())
                return;

            PassengerControlData data = new PassengerControlData();
            data.Passenger = passenger;
            data.Vehicle = vehicle;
            data.WasPersistent = passenger.IsPersistent;
            data.WasInvincible = passenger.IsInvincible;
            data.NextEnterAttemptTime = -1;
            data.TransferState = PassengerTransferState.None;
            data.IsIndependentRider = false;
            data.IndependentVehicle = null;
            data.TransferTargetVehicle = null;
            data.ContextHoldStartTime = -1;

            try
            {
                data.PreviousWeaponHash = Function.Call<uint>(
                    Hash.GET_SELECTED_PED_WEAPON,
                    passenger.Handle);
            }
            catch
            {
                data.PreviousWeaponHash = UnarmedWeaponHash;
            }

            recruitedPassenger = data;
            cachedPassengerCompanionForWeaponSync = false;
            nextPassengerWeaponSyncCheckTime = -1;
            lastSynchronizedPassengerWeaponHandle = 0;

            try
            {
                // Tạm thời giữ quyền điều khiển NPC như các NPC chase/flee.
                passenger.AlwaysKeepTask = true;
                passenger.BlockPermanentEvents = true;
                passenger.IsPersistent = true;
                passenger.IsInvincible = true;

                RequestPassengerEnterVehicle(data);
            }
            catch
            {
                ReleaseRecruitedPassenger();
            }
        }

        // INPUT_CONTEXT giữ nguyên thao tác tuyển NPC bằng một lần nhấn.
        // Khi đã có đồng đội, giữ cùng phím đủ 3 giây sẽ chuyển xe hoặc gọi về ghế phụ.
        private void HandlePassengerRecruitment(Ped player, Vehicle vehicle)
        {
            if (player == null || !player.Exists() ||
                vehicle == null || !vehicle.Exists())
            {
                if (recruitedPassenger != null)
                    recruitedPassenger.ContextHoldStartTime = -1;
                return;
            }

            if (recruitedPassenger != null)
            {
                HandlePassengerTransferInput(player, vehicle, recruitedPassenger);
                return;
            }

            // INPUT_CONTEXT is sampled first. In the normal (not pressed) case,
            // skip seat-count and driver-seat natives as well as nearby-ped scans.
            if (!IsControlPressed(0, (Control)51) &&
                !IsDisabledControlPressed(0, (Control)51))
            {
                return;
            }

            if (!HasAtLeastTwoSeats(vehicle) ||
                !IsPlayerDrivingVehicle(player, vehicle))
            {
                return;
            }

            Ped currentPassenger = vehicle.GetPedOnSeat(VehicleSeat.Passenger);
            if (currentPassenger != null && currentPassenger.Exists())
                return;

            Ped nearbyPed = FindNearbyRecruitablePed(player);
            if (nearbyPed == null)
                return;

            // Normal recruitment remains a single press; the new three-second
            // hold action is only available after a teammate has been recruited.
            DisableControlThisFrame(0, (Control)51);

            if (IsDisabledControlJustPressed(0, (Control)51))
                RecruitPassenger(nearbyPed, vehicle);
        }

        private bool IsPassengerTransferBlocked(PassengerControlData data)
        {
            if (data == null || data.TyreAttackActive || holdingAttack ||
                IsPassengerTyreAttackActive() ||
                IsKickBuffActive() || IsKickBuffDraining())
            {
                return true;
            }

            // A charged Special Kick is a protected combat state: do not interrupt
            // it by ordering the teammate to leave the player's bike.
            return highSpeedSpecialKickReady &&
                   highSpeedSpecialKickChargesRemaining > 0;
        }

        private void HandlePassengerTransferInput(
            Ped player,
            Vehicle playerVehicle,
            PassengerControlData data)
        {
            if (data == null)
                return;

            bool contextHeld = IsControlPressed(0, (Control)51) ||
                               IsDisabledControlPressed(0, (Control)51);

            if (data.TransferState != PassengerTransferState.None)
            {
                // Keep INPUT_CONTEXT consumed during the transition. Releasing
                // the key clears the latch, so one continuous hold cannot trigger
                // a second transfer immediately after the first one completes.
                if (contextHeld)
                {
                    data.RequireContextReleaseAfterTransfer = true;
                    DisableControlThisFrame(0, (Control)51);
                }
                else
                {
                    data.RequireContextReleaseAfterTransfer = false;
                }

                data.ContextHoldStartTime = -1;
                return;
            }

            if (data.RequireContextReleaseAfterTransfer)
            {
                if (contextHeld)
                {
                    DisableControlThisFrame(0, (Control)51);
                    data.ContextHoldStartTime = -1;
                    return;
                }

                data.RequireContextReleaseAfterTransfer = false;
            }

            // Avoid scanning nearby vehicles on every frame: this entire path is
            // dormant unless INPUT_CONTEXT is actually held.
            if (!contextHeld)
            {
                data.ContextHoldStartTime = -1;
                data.NextTransferVehicleScanTime = -1;
                data.CachedTransferVehicle = null;
                return;
            }

            if (IsPassengerTransferBlocked(data))
            {
                data.ContextHoldStartTime = -1;
                data.NextTransferVehicleScanTime = -1;
                data.CachedTransferVehicle = null;
                return;
            }

            if (!HasAtLeastTwoSeats(playerVehicle) ||
                !IsPlayerDrivingVehicle(player, playerVehicle))
            {
                data.ContextHoldStartTime = -1;
                return;
            }

            bool isSeatedPassenger = HasRecruitedPassengerSeated(player);
            bool isIndependentRider = IsRecruitedIndependentRiderActive(player);
            Vehicle transferVehicle = null;

            if (isSeatedPassenger)
            {
                // The nearby-vehicle broadphase and seat scan run at 4 Hz while
                // the key is held, not on every rendered frame.
                if (data.NextTransferVehicleScanTime < 0 ||
                    IsGameTimeReached(data.NextTransferVehicleScanTime))
                {
                    data.CachedTransferVehicle = FindNearbyEmptyMotorcycleOrCycle(
                        player,
                        playerVehicle);
                    data.NextTransferVehicleScanTime = GetGameDeadline(
                        PassengerTransferVehicleScanInterval);
                }

                transferVehicle = data.CachedTransferVehicle;
            }
            else if (isIndependentRider)
            {
                data.CachedTransferVehicle = null;
                data.NextTransferVehicleScanTime = -1;
                Ped playerBikePassenger = playerVehicle.GetPedOnSeat(
                    VehicleSeat.Passenger);

                // Return is possible only while the original passenger seat is free.
                if (playerBikePassenger == null || !playerBikePassenger.Exists())
                    transferVehicle = playerVehicle;
            }

            if (transferVehicle == null || !transferVehicle.Exists())
            {
                data.ContextHoldStartTime = -1;
                return;
            }

            DisableControlThisFrame(0, (Control)51);

            if (data.ContextHoldStartTime < 0)
                data.ContextHoldStartTime = Game.GameTime;

            if (unchecked(Game.GameTime - data.ContextHoldStartTime) <
                PassengerTransferHoldDuration)
            {
                return;
            }

            data.ContextHoldStartTime = -1;
            data.CachedTransferVehicle = null;
            data.NextTransferVehicleScanTime = -1;
            data.RequireContextReleaseAfterTransfer = true;

            if (isSeatedPassenger)
                BeginIndependentRiderTransfer(data, transferVehicle);
            else if (isIndependentRider)
                BeginReturnToPlayerBike(data);
        }

        private void BeginIndependentRiderTransfer(
            PassengerControlData data,
            Vehicle targetVehicle)
        {
            if (data == null || data.TransferState != PassengerTransferState.None ||
                data.IsIndependentRider ||
                data.Passenger == null || !data.Passenger.Exists() ||
                data.Vehicle == null || !data.Vehicle.Exists() ||
                targetVehicle == null || !targetVehicle.Exists() ||
                !IsVehicleEmptyExceptPed(targetVehicle, null))
            {
                return;
            }

            Ped passenger = data.Passenger;
            if (!passenger.IsInVehicle() ||
                passenger.CurrentVehicle == null ||
                !passenger.CurrentVehicle.Exists() ||
                passenger.CurrentVehicle.Handle != data.Vehicle.Handle)
            {
                return;
            }

            Ped player = Game.Player.Character;
            if (player == null || !player.Exists() ||
                player.Position.DistanceTo(targetVehicle.Position) >
                    PassengerTransferVehicleRadius)
            {
                return;
            }

            data.TransferTargetVehicle = targetVehicle;
            data.CachedTransferVehicle = null;
            data.NextTransferVehicleScanTime = -1;
            data.TransferState = PassengerTransferState.LeavingPlayerBike;
            data.NextEnterAttemptTime = -1;
            data.NextIndependentFollowTaskTime = -1;
            data.ReenterPlayerBikeStartTime = -1;
            data.LastFollowTargetVehicleHandle = 0;

            try
            {
                Function.Call(
                    Hash.TASK_LEAVE_VEHICLE,
                    passenger.Handle,
                    data.Vehicle.Handle,
                    0);
            }
            catch
            {
                data.TransferState = PassengerTransferState.None;
                data.TransferTargetVehicle = null;
            }
        }

        private void BeginReturnToPlayerBike(PassengerControlData data)
        {
            if (data == null || data.TransferState != PassengerTransferState.None ||
                !data.IsIndependentRider ||
                data.Passenger == null || !data.Passenger.Exists() ||
                data.IndependentVehicle == null || !data.IndependentVehicle.Exists() ||
                data.Vehicle == null || !data.Vehicle.Exists())
            {
                return;
            }

            Ped playerBikePassenger = data.Vehicle.GetPedOnSeat(VehicleSeat.Passenger);
            if (playerBikePassenger != null && playerBikePassenger.Exists())
                return;

            data.TransferTargetVehicle = data.IndependentVehicle;
            data.CachedTransferVehicle = null;
            data.NextTransferVehicleScanTime = -1;
            data.TransferState = PassengerTransferState.LeavingIndependentBike;
            data.NextEnterAttemptTime = -1;
            data.NextIndependentFollowTaskTime = -1;
            data.ReenterPlayerBikeStartTime = -1;

            try
            {
                Function.Call(
                    Hash.TASK_LEAVE_VEHICLE,
                    data.Passenger.Handle,
                    data.IndependentVehicle.Handle,
                    0);
            }
            catch
            {
                data.TransferState = PassengerTransferState.None;
                data.TransferTargetVehicle = null;
            }
        }

        // Xóa các vũ khí chỉ được cấp để đồng bộ và trả lại vũ khí NPC đã chọn trước đó.
        private static void RestoreRecruitedPassengerWeapons(PassengerControlData data)
        {
            if (data == null || data.Passenger == null || !data.Passenger.Exists())
                return;

            Ped passenger = data.Passenger;

            if (data.GrantedWeaponHashes != null)
            {
                for (int i = data.GrantedWeaponHashes.Count - 1; i >= 0; i--)
                {
                    uint weaponHash = data.GrantedWeaponHashes[i];

                    try
                    {
                        Function.Call(
                            Hash.REMOVE_WEAPON_FROM_PED,
                            passenger.Handle,
                            weaponHash);
                    }
                    catch
                    {
                    }
                }

                data.GrantedWeaponHashes.Clear();
            }

            try
            {
                uint previousWeapon = data.PreviousWeaponHash != 0
                    ? data.PreviousWeaponHash
                    : UnarmedWeaponHash;

                if (Function.Call<bool>(
                    Hash.HAS_PED_GOT_WEAPON,
                    passenger.Handle,
                    previousWeapon,
                    false))
                {
                    Function.Call(
                        Hash.SET_CURRENT_PED_WEAPON,
                        passenger.Handle,
                        previousWeapon,
                        true);
                }
            }
            catch
            {
            }
        }

        // Trả NPC về trạng thái bình thường và cho NPC rời xe nếu đang ngồi.
        private void ReleaseRecruitedPassenger()
        {
            PassengerControlData data = recruitedPassenger;
            recruitedPassenger = null;
            cachedPassengerCompanionForWeaponSync = false;
            nextPassengerWeaponSyncCheckTime = -1;
            nextRecruitedPassengerUpdateTime = -1;
            nextBuffPassengerKickScanTime = -1;
            nextBuffPassengerKickAllowedTime = -1;
            CleanupPassengerWeaponProp();

            if (data == null ||
                data.Passenger == null ||
                !data.Passenger.Exists())
            {
                return;
            }

            Ped passenger = data.Passenger;
            StopPassengerTyreAttack(data);
            RestoreRecruitedPassengerWeapons(data);
            lastSynchronizedPassengerWeaponHandle = 0;
            Vehicle vehicleToLeave = null;

            try
            {
                if (passenger.IsInVehicle())
                {
                    Vehicle currentVehicle = passenger.CurrentVehicle;
                    if (currentVehicle != null && currentVehicle.Exists())
                    {
                        bool isAssignedPlayerBike = data.Vehicle != null &&
                            data.Vehicle.Exists() &&
                            currentVehicle.Handle == data.Vehicle.Handle;
                        bool isIndependentBike = data.IndependentVehicle != null &&
                            data.IndependentVehicle.Exists() &&
                            currentVehicle.Handle == data.IndependentVehicle.Handle;
                        bool isTransferBike = data.TransferTargetVehicle != null &&
                            data.TransferTargetVehicle.Exists() &&
                            currentVehicle.Handle == data.TransferTargetVehicle.Handle;

                        if (isAssignedPlayerBike || isIndependentBike || isTransferBike)
                            vehicleToLeave = currentVehicle;
                    }
                }

                if (vehicleToLeave != null)
                {
                    // Keep the native exit task alive, just as the original
                    // passenger-release path does for the player's bike.
                    Function.Call(
                        Hash.TASK_LEAVE_VEHICLE,
                        passenger.Handle,
                        vehicleToLeave.Handle,
                        0);

                    passenger.BlockPermanentEvents = false;
                    passenger.AlwaysKeepTask = true;
                    passenger.IsPersistent = data.WasPersistent;
                    passenger.IsInvincible = data.WasInvincible;

                    PassengerExitData exitData = new PassengerExitData();
                    exitData.Passenger = passenger;
                    exitData.VehicleHandle = vehicleToLeave.Handle;
                    exitData.WasPersistent = data.WasPersistent;
                    exitData.WasInvincible = data.WasInvincible;
                    passengersLeavingVehicles.Add(exitData);
                    return;
                }
                else
                {
                    // NPC vẫn đang đi bộ tới xe hoặc đã rời khỏi xe.
                    passenger.Task.ClearAll();
                    passenger.BlockPermanentEvents = false;
                    passenger.AlwaysKeepTask = false;
                    passenger.IsPersistent = data.WasPersistent;
                }

                passenger.IsInvincible = data.WasInvincible;
            }
            catch
            {
                try
                {
                    passenger.BlockPermanentEvents = false;
                    passenger.AlwaysKeepTask = false;
                    passenger.IsPersistent = data.WasPersistent;
                    passenger.IsInvincible = data.WasInvincible;
                }
                catch
                {
                }
            }
        }

        // Sau khi NPC rời ghế phụ, trả task và trạng thái quản lý về mặc định.
        private void UpdatePassengerExitTasks()
        {
            if (passengersLeavingVehicles.Count == 0)
            {
                nextPassengerExitUpdateTime = -1;
                return;
            }

            if (nextPassengerExitUpdateTime >= 0 &&
                !IsGameTimeReached(nextPassengerExitUpdateTime))
            {
                return;
            }

            nextPassengerExitUpdateTime = GetGameDeadline(
                PassengerExitMaintenanceInterval);

            for (int i = passengersLeavingVehicles.Count - 1; i >= 0; i--)
            {
                PassengerExitData data = passengersLeavingVehicles[i];

                if (data == null)
                {
                    passengersLeavingVehicles.RemoveAt(i);
                    continue;
                }

                Ped passenger = data.Passenger;

                if (passenger == null || !passenger.Exists())
                {
                    passengersLeavingVehicles.RemoveAt(i);
                    continue;
                }

                bool stillInOriginalVehicle = false;

                try
                {
                    if (passenger.IsInVehicle())
                    {
                        Vehicle currentVehicle = passenger.CurrentVehicle;
                        stillInOriginalVehicle = currentVehicle != null &&
                                                 currentVehicle.Exists() &&
                                                 currentVehicle.Handle == data.VehicleHandle;
                    }
                }
                catch
                {
                    stillInOriginalVehicle = false;
                }

                if (stillInOriginalVehicle && passenger.IsAlive)
                    continue;

                try
                {
                    passenger.AlwaysKeepTask = false;
                    passenger.BlockPermanentEvents = false;
                    passenger.IsPersistent = data.WasPersistent;
                    passenger.IsInvincible = data.WasInvincible;
                }
                catch
                {
                }

                passengersLeavingVehicles.RemoveAt(i);
            }
        }

        // Script bị gỡ thì không còn Tick để theo dõi task rời xe.
        // Để task native tiếp tục nhưng không giữ NPC ở trạng thái AlwaysKeepTask.
        private void FinalizePassengerExitTasks()
        {
            for (int i = passengersLeavingVehicles.Count - 1; i >= 0; i--)
            {
                PassengerExitData data = passengersLeavingVehicles[i];

                if (data == null || data.Passenger == null || !data.Passenger.Exists())
                    continue;

                try
                {
                    data.Passenger.AlwaysKeepTask = false;
                    data.Passenger.BlockPermanentEvents = false;
                    data.Passenger.IsPersistent = data.WasPersistent;
                    data.Passenger.IsInvincible = data.WasInvincible;
                }
                catch
                {
                }
            }

            passengersLeavingVehicles.Clear();
        }

        // Keep the recruited teammate active across passenger-seat / independent-bike
        // transitions. Leaving the player's bike ends recruitment exactly as before.
        private void UpdateRecruitedPassenger(Ped player)
        {
            PassengerControlData data = recruitedPassenger;

            if (data == null)
            {
                nextRecruitedPassengerUpdateTime = -1;
                return;
            }

            if (nextRecruitedPassengerUpdateTime >= 0 &&
                !IsGameTimeReached(nextRecruitedPassengerUpdateTime))
            {
                return;
            }

            nextRecruitedPassengerUpdateTime = GetGameDeadline(
                RecruitedPassengerMaintenanceInterval);

            if (player == null ||
                !player.Exists() ||
                !player.IsInVehicle() ||
                data.Vehicle == null ||
                !data.Vehicle.Exists() ||
                player.CurrentVehicle == null ||
                !player.CurrentVehicle.Exists() ||
                player.CurrentVehicle.Handle != data.Vehicle.Handle ||
                !IsMotorcycleOrCycle(data.Vehicle) ||
                !HasAtLeastTwoSeats(data.Vehicle) ||
                !IsPlayerDrivingVehicle(player, data.Vehicle))
            {
                ReleaseRecruitedPassenger();
                return;
            }

            Ped passenger = data.Passenger;
            if (passenger == null || !passenger.Exists() || !passenger.IsAlive)
            {
                ReleaseRecruitedPassenger();
                return;
            }

            if (data.TransferState != PassengerTransferState.None)
            {
                UpdatePassengerTransfer(data);
                return;
            }

            if (data.IsIndependentRider)
            {
                if (!IsRecruitedIndependentRiderActive(player))
                {
                    // Leaving the transferred bike is treated as normal teammate
                    // dismissal: restore the ped's original state and AI ownership.
                    ReleaseRecruitedPassenger();
                    return;
                }

                UpdateIndependentRiderFollow(data, data.Vehicle);
                return;
            }

            Ped seatOccupant = data.Vehicle.GetPedOnSeat(VehicleSeat.Passenger);
            if (seatOccupant != null &&
                seatOccupant.Exists() &&
                seatOccupant.Handle == passenger.Handle)
            {
                return;
            }

            // Preserve the original safeguard: an NPC that independently enters
            // another vehicle or loses the recruited passenger seat is released.
            if (passenger.IsInVehicle() ||
                (seatOccupant != null && seatOccupant.Exists()))
            {
                ReleaseRecruitedPassenger();
                return;
            }

            if (data.NextEnterAttemptTime >= 0 &&
                !IsGameTimeReached(data.NextEnterAttemptTime))
            {
                return;
            }

            int tryingVehicleHandle = Function.Call<int>(
                Hash.GET_VEHICLE_PED_IS_TRYING_TO_ENTER,
                passenger.Handle);

            if (tryingVehicleHandle == data.Vehicle.Handle)
            {
                data.NextEnterAttemptTime = GetGameDeadline(1000);
                return;
            }

            RequestPassengerEnterVehicle(data);
        }

        private void UpdatePassengerTransfer(PassengerControlData data)
        {
            Ped passenger = data.Passenger;
            if (passenger == null || !passenger.Exists() || !passenger.IsAlive)
            {
                ReleaseRecruitedPassenger();
                return;
            }

            Vehicle currentVehicle = null;
            if (passenger.IsInVehicle())
            {
                try { currentVehicle = passenger.CurrentVehicle; } catch { }
            }

            switch (data.TransferState)
            {
                case PassengerTransferState.LeavingPlayerBike:
                    {
                        if (currentVehicle != null && currentVehicle.Exists())
                        {
                            if (data.Vehicle != null && data.Vehicle.Exists() &&
                                currentVehicle.Handle == data.Vehicle.Handle)
                            {
                                return; // Native leave task is still running.
                            }

                            if (data.TransferTargetVehicle != null &&
                                data.TransferTargetVehicle.Exists() &&
                                currentVehicle.Handle == data.TransferTargetVehicle.Handle)
                            {
                                Ped currentDriver = currentVehicle.GetPedOnSeat(
                                    VehicleSeat.Driver);
                                if (currentDriver != null && currentDriver.Exists() &&
                                    currentDriver.Handle == passenger.Handle)
                                {
                                    CompleteIndependentRiderTransfer(data, currentVehicle);
                                    return;
                                }
                            }

                            ReleaseRecruitedPassenger();
                            return;
                        }

                        if (data.TransferTargetVehicle == null ||
                            !data.TransferTargetVehicle.Exists() ||
                            !IsVehicleEmptyExceptPed(data.TransferTargetVehicle, null))
                        {
                            // The target became unavailable while the teammate was
                            // getting off. Safely send the teammate back to the player.
                            data.TransferTargetVehicle = data.Vehicle;
                            data.TransferState = PassengerTransferState.ReenteringPlayerBike;
                            data.ReenterPlayerBikeStartTime = Game.GameTime;
                            data.NextEnterAttemptTime = -1;
                            return;
                        }

                        data.TransferState = PassengerTransferState.EnteringIndependentBike;
                        data.NextEnterAttemptTime = -1;
                        RequestPassengerEnterDriverVehicle(
                            data,
                            data.TransferTargetVehicle);
                        return;
                    }

                case PassengerTransferState.EnteringIndependentBike:
                    {
                        Vehicle targetVehicle = data.TransferTargetVehicle;
                        if (currentVehicle != null && currentVehicle.Exists())
                        {
                            if (targetVehicle != null && targetVehicle.Exists() &&
                                currentVehicle.Handle == targetVehicle.Handle)
                            {
                                Ped driver = currentVehicle.GetPedOnSeat(VehicleSeat.Driver);
                                if (driver != null && driver.Exists() &&
                                    driver.Handle == passenger.Handle)
                                {
                                    CompleteIndependentRiderTransfer(data, currentVehicle);
                                }
                                else
                                {
                                    ReleaseRecruitedPassenger();
                                }
                                return;
                            }

                            if (data.Vehicle != null && data.Vehicle.Exists() &&
                                currentVehicle.Handle == data.Vehicle.Handle)
                            {
                                data.TransferState = PassengerTransferState.None;
                                data.TransferTargetVehicle = null;
                                data.NextEnterAttemptTime = -1;
                                return;
                            }

                            ReleaseRecruitedPassenger();
                            return;
                        }

                        if (targetVehicle == null ||
                            !targetVehicle.Exists() ||
                            !IsVehicleEmptyExceptPed(targetVehicle, passenger))
                        {
                            data.TransferTargetVehicle = data.Vehicle;
                            data.TransferState = PassengerTransferState.ReenteringPlayerBike;
                            data.ReenterPlayerBikeStartTime = Game.GameTime;
                            data.NextEnterAttemptTime = -1;
                            return;
                        }

                        if (data.NextEnterAttemptTime < 0 ||
                            IsGameTimeReached(data.NextEnterAttemptTime))
                        {
                            int tryingHandle = Function.Call<int>(
                                Hash.GET_VEHICLE_PED_IS_TRYING_TO_ENTER,
                                passenger.Handle);

                            if (tryingHandle != targetVehicle.Handle)
                            {
                                RequestPassengerEnterDriverVehicle(data, targetVehicle);
                            }
                            else
                            {
                                data.NextEnterAttemptTime = GetGameDeadline(1000);
                            }
                        }
                        return;
                    }

                case PassengerTransferState.LeavingIndependentBike:
                    {
                        Vehicle independentVehicle = data.IndependentVehicle;
                        if (currentVehicle != null && currentVehicle.Exists())
                        {
                            if (independentVehicle != null && independentVehicle.Exists() &&
                                currentVehicle.Handle == independentVehicle.Handle)
                            {
                                return; // Wait until the teammate has actually dismounted.
                            }

                            ReleaseRecruitedPassenger();
                            return;
                        }

                        data.IsIndependentRider = false;
                        data.TransferTargetVehicle = data.Vehicle;
                        data.TransferState = PassengerTransferState.ReenteringPlayerBike;
                        data.ReenterPlayerBikeStartTime = Game.GameTime;
                        data.NextEnterAttemptTime = -1;
                        return;
                    }

                case PassengerTransferState.ReenteringPlayerBike:
                    {
                        if (currentVehicle != null && currentVehicle.Exists())
                        {
                            if (data.Vehicle != null && data.Vehicle.Exists() &&
                                currentVehicle.Handle == data.Vehicle.Handle)
                            {
                                Ped seatOccupant = data.Vehicle.GetPedOnSeat(VehicleSeat.Passenger);
                                if (seatOccupant != null && seatOccupant.Exists() &&
                                    seatOccupant.Handle == passenger.Handle)
                                {
                                    data.IsIndependentRider = false;
                                    data.IndependentVehicle = null;
                                    data.TransferTargetVehicle = null;
                                    data.CachedTransferVehicle = null;
                                    data.NextTransferVehicleScanTime = -1;
                                    data.RequireContextReleaseAfterTransfer = true;
                                    data.TransferState = PassengerTransferState.None;
                                    data.NextEnterAttemptTime = -1;
                                    data.NextIndependentFollowTaskTime = -1;
                                    data.ReenterPlayerBikeStartTime = -1;
                                    data.LastFollowTargetVehicleHandle = 0;
                                    lastSynchronizedPassengerWeaponHandle = 0;
                                }
                                else
                                {
                                    ReleaseRecruitedPassenger();
                                }
                                return;
                            }

                            ReleaseRecruitedPassenger();
                            return;
                        }

                        Ped seatOccupantNow = data.Vehicle.GetPedOnSeat(VehicleSeat.Passenger);
                        if (seatOccupantNow != null && seatOccupantNow.Exists())
                            return; // Wait for the original passenger seat to become free.

                        if (data.ReenterPlayerBikeStartTime < 0)
                            data.ReenterPlayerBikeStartTime = Game.GameTime;

                        int reentryElapsed = unchecked(
                            Game.GameTime - data.ReenterPlayerBikeStartTime);
                        float playerBikeSpeed = data.Vehicle.Speed;
                        float teammateDistance = passenger.Position.DistanceTo(
                            data.Vehicle.Position);

                        // A pedestrian cannot reliably catch a bike that is already
                        // moving away. First allow the normal enter task; if the rider
                        // remains too far away / the player is moving quickly, use the
                        // native warp only after the teammate has left the other bike.
                        bool needsMovingBikeFallback = reentryElapsed >= 3500 &&
                            (playerBikeSpeed > 8.0f || teammateDistance > 5.0f);

                        if (needsMovingBikeFallback &&
                            (data.NextEnterAttemptTime < 0 ||
                             IsGameTimeReached(data.NextEnterAttemptTime)))
                        {
                            try
                            {
                                Function.Call(
                                    Native(unchecked((long)0x9A7D091411C5F684UL)),
                                    passenger.Handle,
                                    data.Vehicle.Handle,
                                    (int)VehicleSeat.Passenger);
                            }
                            catch
                            {
                            }

                            data.NextEnterAttemptTime = GetGameDeadline(1000);
                            return;
                        }

                        if (data.NextEnterAttemptTime < 0 ||
                            IsGameTimeReached(data.NextEnterAttemptTime))
                        {
                            int tryingHandle = Function.Call<int>(
                                Hash.GET_VEHICLE_PED_IS_TRYING_TO_ENTER,
                                passenger.Handle);

                            if (tryingHandle != data.Vehicle.Handle)
                                RequestPassengerEnterVehicle(data);
                            else
                                data.NextEnterAttemptTime = GetGameDeadline(1000);
                        }
                        return;
                    }
            }
        }

        private void CompleteIndependentRiderTransfer(
            PassengerControlData data,
            Vehicle independentVehicle)
        {
            if (data == null || independentVehicle == null ||
                !independentVehicle.Exists())
            {
                return;
            }

            data.IsIndependentRider = true;
            data.IndependentVehicle = independentVehicle;
            data.TransferTargetVehicle = null;
            data.TransferState = PassengerTransferState.None;
            data.NextEnterAttemptTime = -1;
            data.ContextHoldStartTime = -1;
            data.RequireContextReleaseAfterTransfer = true;
            data.NextTransferVehicleScanTime = -1;
            data.CachedTransferVehicle = null;
            data.NextIndependentFollowTaskTime = -1;
            data.LastFollowTargetVehicleHandle = 0;
            lastSynchronizedPassengerWeaponHandle = 0;
        }

        private void UpdateIndependentRiderFollow(
            PassengerControlData data,
            Vehicle playerVehicle)
        {
            if (data == null ||
                data.Passenger == null || !data.Passenger.Exists() ||
                data.IndependentVehicle == null || !data.IndependentVehicle.Exists() ||
                playerVehicle == null || !playerVehicle.Exists())
            {
                return;
            }

            Ped passenger = data.Passenger;
            bool targetChanged = data.LastFollowTargetVehicleHandle != playerVehicle.Handle;
            bool retryPending = data.NextIndependentFollowTaskTime >= 0;

            // A vehicle-chase task is persistent by itself. Do not restart it
            // every few seconds; only start it for a new target or retry a failed native call.
            if (retryPending)
            {
                if (!IsGameTimeReached(data.NextIndependentFollowTaskTime))
                    return;
            }
            else if (!targetChanged)
            {
                return;
            }

            try
            {
                // Use the same vehicle-chase task as NPC pursuit reactions. Rushed
                // driving lets the teammate catch up naturally; the ideal pursuit
                // distance prevents it from riding directly on the player's rear wheel.
                Function.Call(Hash.SET_PED_KEEP_TASK, passenger.Handle, true);
                Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, passenger.Handle, true);
                Function.Call(Hash.SET_DRIVER_ABILITY, passenger.Handle, 1.0f);
                Function.Call(Hash.SET_DRIVER_AGGRESSIVENESS, passenger.Handle, 1.0f);

                Function.Call(
                    Hash.TASK_VEHICLE_CHASE,
                    passenger.Handle,
                    Game.Player.Character.Handle);

                Function.Call(
                    Hash.SET_DRIVE_TASK_DRIVING_STYLE,
                    passenger.Handle,
                    (int)DrivingStyle.Rushed);

                // This native only affects an active TASK_VEHICLE_CHASE task.
                Function.Call(
                    Native(unchecked((long)0x639B642FACBE4EDDUL)),
                    passenger.Handle,
                    PassengerIndependentPursuitDistance);

                data.LastFollowTargetVehicleHandle = playerVehicle.Handle;
                data.NextIndependentFollowTaskTime = -1;
            }
            catch
            {
                // Retry after a short delay instead of restarting the chase each tick.
                data.NextIndependentFollowTaskTime = GetGameDeadline(1000);
            }
        }

        private bool HasRecruitedPassengerSeated(Ped player)
        {
            PassengerControlData data = recruitedPassenger;

            if (data == null ||
                data.Passenger == null || !data.Passenger.Exists() ||
                data.Vehicle == null || !data.Vehicle.Exists() ||
                player == null || !player.Exists() ||
                !player.IsInVehicle() ||
                player.CurrentVehicle == null ||
                !player.CurrentVehicle.Exists() ||
                player.CurrentVehicle.Handle != data.Vehicle.Handle)
            {
                return false;
            }

            Ped seatOccupant = data.Vehicle.GetPedOnSeat(VehicleSeat.Passenger);

            return seatOccupant != null &&
                   seatOccupant.Exists() &&
                   seatOccupant.Handle == data.Passenger.Handle;
        }

        // Strict seat check remains for synchronized passenger animations and
        // actions that require the player bike's passenger seat.
        private bool IsRecruitedIndependentRiderActive(Ped player)
        {
            PassengerControlData data = recruitedPassenger;
            if (data == null || !data.IsIndependentRider ||
                data.TransferState != PassengerTransferState.None ||
                data.Passenger == null || !data.Passenger.Exists() || !data.Passenger.IsAlive ||
                data.IndependentVehicle == null || !data.IndependentVehicle.Exists() ||
                player == null || !player.Exists() || !player.IsInVehicle() ||
                data.Vehicle == null || !data.Vehicle.Exists() ||
                player.CurrentVehicle == null || !player.CurrentVehicle.Exists() ||
                player.CurrentVehicle.Handle != data.Vehicle.Handle ||
                !IsPlayerDrivingVehicle(player, data.Vehicle))
            {
                return false;
            }

            Ped driver = data.IndependentVehicle.GetPedOnSeat(VehicleSeat.Driver);
            return data.Passenger.IsInVehicle() &&
                   data.Passenger.CurrentVehicle != null &&
                   data.Passenger.CurrentVehicle.Exists() &&
                   data.Passenger.CurrentVehicle.Handle == data.IndependentVehicle.Handle &&
                   driver != null && driver.Exists() &&
                   driver.Handle == data.Passenger.Handle;
        }

        // Used only for co-op damage/force and weapon synchronization. During a
        // transfer the teammate still belongs to the active two-person team.
        private bool HasRecruitedCompanion(Ped player)
        {
            PassengerControlData data = recruitedPassenger;
            if (data == null || data.Passenger == null || !data.Passenger.Exists() ||
                !data.Passenger.IsAlive || data.Vehicle == null || !data.Vehicle.Exists() ||
                player == null || !player.Exists() || !player.IsInVehicle() ||
                player.CurrentVehicle == null || !player.CurrentVehicle.Exists() ||
                player.CurrentVehicle.Handle != data.Vehicle.Handle ||
                !IsPlayerDrivingVehicle(player, data.Vehicle))
            {
                return false;
            }

            return data.TransferState != PassengerTransferState.None ||
                   HasRecruitedPassengerSeated(player) ||
                   IsRecruitedIndependentRiderActive(player);
        }

        private bool IsPassengerIndependentAttackActive(Ped player)
        {
            return IsRecruitedIndependentRiderActive(player);
        }

        private bool IsPassengerTyreAttackActive()
        {
            return recruitedPassenger != null &&
                   recruitedPassenger.TyreAttackActive;
        }

        // Resolve the nearest vehicle in front of the player's current heading.
        // The player's own bike is excluded from the target pool.
        private Vehicle FindPassengerTyreAttackTarget(Ped player, Vehicle ownVehicle)
        {
            if (player == null || !player.Exists())
                return null;

            Vehicle[] nearbyVehicles = World.GetNearbyVehicles(
                player,
                PassengerTyreAttackTargetRadius);

            if (nearbyVehicles == null || nearbyVehicles.Length == 0)
                return null;

            Vector3 origin = player.Position;
            int ownVehicleHandle = ownVehicle != null && ownVehicle.Exists()
                ? ownVehicle.Handle
                : 0;
            Vector3 forward = ownVehicleHandle != 0
                ? ownVehicle.ForwardVector
                : player.ForwardVector;

            Vehicle bestVehicle = null;
            float bestDistanceSquared =
                PassengerTyreAttackTargetRadius * PassengerTyreAttackTargetRadius;

            foreach (Vehicle candidate in nearbyVehicles)
            {
                if (candidate == null || !candidate.Exists() || candidate.IsDead ||
                    (ownVehicleHandle != 0 &&
                     candidate.Handle == ownVehicleHandle))
                {
                    continue;
                }

                Vector3 difference = candidate.Position - origin;
                float distanceSquared =
                    difference.X * difference.X +
                    difference.Y * difference.Y +
                    difference.Z * difference.Z;

                if (distanceSquared <= 0.01f ||
                    distanceSquared >= bestDistanceSquared)
                {
                    continue;
                }

                float distance = (float)Math.Sqrt(distanceSquared);
                float forwardDot = Vector3.Dot(difference, forward) / distance;

                if (forwardDot < PassengerTyreAttackForwardConeDot)
                    continue;

                bestDistanceSquared = distanceSquared;
                bestVehicle = candidate;
            }

            return bestVehicle;
        }

        // Aim at the closest tyre on the same side of the target vehicle as the
        // player. If the model exposes no usable wheel bones, aim at its body.
        private static bool TryGetPassengerTyreAttackAim(
            Vehicle target,
            Ped player,
            out int wheelIndex,
            out Vector3 aimPosition)
        {
            wheelIndex = -1;
            aimPosition = Vector3.Zero;

            if (target == null || !target.Exists() ||
                player == null || !player.Exists())
            {
                return false;
            }

            bool playerOnLeftSide = Vector3.Dot(
                player.Position - target.Position,
                target.RightVector) < 0f;

            float bestDistanceSquared = float.MaxValue;
            string firstBone = playerOnLeftSide ? "wheel_lf" : "wheel_rf";
            string secondBone = playerOnLeftSide ? "wheel_lr" : "wheel_rr";
            int firstWheelIndex = playerOnLeftSide ? 0 : 1;
            int secondWheelIndex = playerOnLeftSide ? 4 : 5;
            bool foundWheel = false;
            bool vehicleCanHaveTyres = true;

            try
            {
                int vehicleClass = GetVehicleClassCached(target);
                vehicleCanHaveTyres = vehicleClass != 14 && // Boats
                                      vehicleClass != 15 && // Helicopters
                                      vehicleClass != 16 && // Planes
                                      vehicleClass != 21;   // Trains
            }
            catch
            {
                // Fall back to checking model wheel bones if class lookup fails.
            }

            if (vehicleCanHaveTyres)
            {
                try
                {
                    int firstBoneIndex = Function.Call<int>(
                        Hash.GET_ENTITY_BONE_INDEX_BY_NAME,
                        target.Handle,
                        firstBone);

                    if (firstBoneIndex >= 0)
                    {
                        Vector3 position = Function.Call<Vector3>(
                            Hash.GET_WORLD_POSITION_OF_ENTITY_BONE,
                            target.Handle,
                            firstBoneIndex);

                        float dx = position.X - player.Position.X;
                        float dy = position.Y - player.Position.Y;
                        float dz = position.Z - player.Position.Z;
                        bestDistanceSquared = dx * dx + dy * dy + dz * dz;
                        aimPosition = position;
                        wheelIndex = firstWheelIndex;
                        foundWheel = true;
                    }

                    int secondBoneIndex = Function.Call<int>(
                        Hash.GET_ENTITY_BONE_INDEX_BY_NAME,
                        target.Handle,
                        secondBone);

                    if (secondBoneIndex >= 0)
                    {
                        Vector3 position = Function.Call<Vector3>(
                            Hash.GET_WORLD_POSITION_OF_ENTITY_BONE,
                            target.Handle,
                            secondBoneIndex);

                        float dx = position.X - player.Position.X;
                        float dy = position.Y - player.Position.Y;
                        float dz = position.Z - player.Position.Z;
                        float distanceSquared = dx * dx + dy * dy + dz * dz;

                        if (!foundWheel || distanceSquared < bestDistanceSquared)
                        {
                            bestDistanceSquared = distanceSquared;
                            aimPosition = position;
                            wheelIndex = secondWheelIndex;
                            foundWheel = true;
                        }
                    }
                }
                catch
                {
                    foundWheel = false;
                    wheelIndex = -1;
                }
            }

            if (foundWheel)
                return true;

            // Non-wheeled vehicles, or models without wheel bones, are shot at
            // the outer body panel on the player's side instead.
            Vector3 modelMin = new Vector3(-1.0f, -1.0f, -0.5f);
            Vector3 modelMax = new Vector3(1.0f, 1.0f, 0.5f);

            try
            {
                target.Model.GetDimensions(out modelMin, out modelMax);
            }
            catch
            {
            }

            float sideOffset = playerOnLeftSide
                ? modelMin.X * 0.80f
                : modelMax.X * 0.80f;

            aimPosition = target.Position +
                          target.RightVector * sideOffset +
                          target.UpVector * 0.15f;
            wheelIndex = -1;
            return false;
        }

        private void SetPassengerTyreAttackTarget(
            PassengerControlData data,
            Ped player,
            Vehicle target)
        {
            if (data == null || player == null || !player.Exists() ||
                target == null || !target.Exists() || target.IsDead)
            {
                return;
            }

            int wheelIndex;
            Vector3 aimPosition;
            bool hasWheel = TryGetPassengerTyreAttackAim(
                target,
                player,
                out wheelIndex,
                out aimPosition);

            data.TyreAttackTargetVehicle = target;
            data.TyreAttackWheelIndex = wheelIndex;
            data.TyreAttackTargetHasWheel = hasWheel;
            data.TyreAttackHitRollPassed =
                soundRandom.Next(100) < PassengerTyreAttackHitChance;
            data.TyreAttackBurstTime = GetGameDeadline(
                PassengerTyreAttackTyreHitDelay);
            data.TyreAttackImpactResolved = false;
            data.TyreAttackTaskStarted = false;
        }

        private void StartPassengerTyreAttackTask(
            PassengerControlData data,
            Ped player)
        {
            if (data == null || data.Passenger == null || !data.Passenger.Exists() ||
                player == null || !player.Exists() ||
                data.TyreAttackTargetVehicle == null ||
                !data.TyreAttackTargetVehicle.Exists() ||
                data.TyreAttackTargetVehicle.IsDead)
            {
                return;
            }

            int currentWheelIndex;
            Vector3 aimPosition;
            bool hasWheel = TryGetPassengerTyreAttackAim(
                data.TyreAttackTargetVehicle,
                player,
                out currentWheelIndex,
                out aimPosition);

            data.TyreAttackWheelIndex = currentWheelIndex;
            data.TyreAttackTargetHasWheel = hasWheel;

            try
            {
                Function.Call(
                    Hash.TASK_DRIVE_BY,
                    data.Passenger.Handle,
                    0,
                    data.TyreAttackTargetVehicle.Handle,
                    aimPosition.X,
                    aimPosition.Y,
                    aimPosition.Z,
                    PassengerTyreAttackDriveByDistance,
                    PassengerTyreAttackAccuracy,
                    true,
                    FiringPatternBurstFireDriveBy);

                data.TyreAttackTaskStarted = true;
            }
            catch
            {
                data.TyreAttackTaskStarted = false;
            }
        }

        private bool StartPassengerTyreAttack(Ped player)
        {
            PassengerControlData data = recruitedPassenger;

            if (data == null || !HasRecruitedPassengerSeated(player) ||
                data.TyreAttackActive || holdingAttack ||
                (nextPassengerTyreAttackAllowedTime >= 0 &&
                 !IsGameTimeReached(nextPassengerTyreAttackAllowedTime)) ||
                PassengerTyreAttackWeaponHashes == null ||
                PassengerTyreAttackWeaponHashes.Length == 0)
            {
                return false;
            }

            Ped passenger = data.Passenger;
            if (passenger == null || !passenger.Exists() || !passenger.IsAlive)
                return false;

            uint previousWeapon = GetSelectedWeaponHash(passenger);
            int previousAccuracy = -1;
            try
            {
                previousAccuracy = Function.Call<int>(
                    Hash.GET_PED_ACCURACY,
                    passenger.Handle);
            }
            catch
            {
            }

            // Choose uniformly from the complete user-provided weapon pool.
            // If the passenger already owns this exact pistol, its original ammo
            // is saved and restored instead of removing the weapon afterward.
            uint selectedWeapon = PassengerTyreAttackWeaponHashes[
                soundRandom.Next(PassengerTyreAttackWeaponHashes.Length)];
            bool selectedWeaponAlreadyOwned = false;

            try
            {
                selectedWeaponAlreadyOwned = Function.Call<bool>(
                    Hash.HAS_PED_GOT_WEAPON,
                    passenger.Handle,
                    selectedWeapon,
                    false);
            }
            catch
            {
                return false;
            }

            int previousAmmo = -1;
            if (selectedWeaponAlreadyOwned)
            {
                try
                {
                    previousAmmo = Function.Call<int>(
                        Hash.GET_AMMO_IN_PED_WEAPON,
                        passenger.Handle,
                        selectedWeapon);
                }
                catch
                {
                }
            }

            try
            {
                Function.Call(
                    Hash.GIVE_WEAPON_TO_PED,
                    passenger.Handle,
                    selectedWeapon,
                    PassengerTyreAttackAmmo,
                    false,
                    true);

                Function.Call(
                    Hash.SET_PED_AMMO,
                    passenger.Handle,
                    selectedWeapon,
                    PassengerTyreAttackAmmo,
                    false);

                if (!Function.Call<bool>(
                    Hash.HAS_PED_GOT_WEAPON,
                    passenger.Handle,
                    selectedWeapon,
                    false))
                {
                    if (!selectedWeaponAlreadyOwned)
                    {
                        try
                        {
                            Function.Call(
                                Hash.REMOVE_WEAPON_FROM_PED,
                                passenger.Handle,
                                selectedWeapon);
                        }
                        catch
                        {
                        }
                    }

                    if (previousAccuracy >= 0)
                    {
                        try
                        {
                            Function.Call(
                                Hash.SET_PED_ACCURACY,
                                passenger.Handle,
                                previousAccuracy);
                        }
                        catch
                        {
                        }
                    }

                    return false;
                }

                Function.Call(
                    Hash.SET_CURRENT_PED_WEAPON,
                    passenger.Handle,
                    selectedWeapon,
                    true);

                Function.Call(
                    Hash.SET_PED_ACCURACY,
                    passenger.Handle,
                    PassengerTyreAttackAccuracy);
            }
            catch
            {
                // Roll back only the weapon state touched by this attempt.
                if (!selectedWeaponAlreadyOwned)
                {
                    try
                    {
                        Function.Call(
                            Hash.REMOVE_WEAPON_FROM_PED,
                            passenger.Handle,
                            selectedWeapon);
                    }
                    catch
                    {
                    }
                }
                else if (previousAmmo >= 0)
                {
                    try
                    {
                        Function.Call(
                            Hash.SET_PED_AMMO,
                            passenger.Handle,
                            selectedWeapon,
                            previousAmmo,
                            false);
                    }
                    catch
                    {
                    }
                }

                try
                {
                    if (previousWeapon != 0 && Function.Call<bool>(
                        Hash.HAS_PED_GOT_WEAPON,
                        passenger.Handle,
                        previousWeapon,
                        false))
                    {
                        Function.Call(
                            Hash.SET_CURRENT_PED_WEAPON,
                            passenger.Handle,
                            previousWeapon,
                            true);
                    }
                    else
                    {
                        Function.Call(
                            Hash.SET_CURRENT_PED_WEAPON,
                            passenger.Handle,
                            UnarmedWeaponHash,
                            true);
                    }
                }
                catch
                {
                }

                if (previousAccuracy >= 0)
                {
                    try
                    {
                        Function.Call(Hash.SET_PED_ACCURACY, passenger.Handle, previousAccuracy);
                    }
                    catch
                    {
                    }
                }

                return false;
            }

            data.TyreAttackPreviousSelectedWeaponHash = previousWeapon;
            data.TyreAttackPreviousAccuracy = previousAccuracy;
            data.TyreAttackWeaponHash = selectedWeapon;
            data.TyreAttackWeaponWasOwned = selectedWeaponAlreadyOwned;
            data.TyreAttackPreviousAmmo = previousAmmo;
            data.TyreAttackActive = true;
            data.TyreAttackEndTime = GetGameDeadline(PassengerTyreAttackDuration);
            data.TyreAttackNextRefreshTime = -1;
            data.TyreAttackBurstTime = -1;
            data.TyreAttackTaskStarted = false;
            data.TyreAttackTargetVehicle = null;
            data.TyreAttackWheelIndex = -1;
            data.TyreAttackTargetHasWheel = false;
            data.TyreAttackHitRollPassed = false;
            data.TyreAttackImpactResolved = false;

            nextPassengerTyreAttackAllowedTime = GetGameDeadline(
                PassengerTyreAttackCooldown);

            Vehicle ownVehicle = data.Vehicle;
            Vehicle target = FindPassengerTyreAttackTarget(player, ownVehicle);
            if (target != null)
                SetPassengerTyreAttackTarget(data, player, target);

            return true;
        }

        private void ResolvePassengerTyreAttackImpact(PassengerControlData data)
        {
            if (data == null || data.TyreAttackImpactResolved)
                return;

            data.TyreAttackImpactResolved = true;

            Vehicle target = data.TyreAttackTargetVehicle;
            if (!data.TyreAttackTaskStarted ||
                !data.TyreAttackHitRollPassed ||
                target == null || !target.Exists() || target.IsDead)
            {
                return;
            }

            if (data.TyreAttackTargetHasWheel && data.TyreAttackWheelIndex >= 0)
            {
                try
                {
                    Function.Call(
                        Hash.SET_VEHICLE_TYRE_BURST,
                        target.Handle,
                        data.TyreAttackWheelIndex,
                        true,
                        1000.0f);
                }
                catch
                {
                }
            }
            else
            {
                // Fallback for boats/aircraft or models without wheel bones:
                // the passenger still fires at the vehicle body and this small
                // body-health loss guarantees that the fallback has a real effect.
                try
                {
                    target.BodyHealth = Math.Max(
                        0f,
                        target.BodyHealth - 20.0f);
                }
                catch
                {
                }
            }
        }

        private void UpdatePassengerTyreAttack(Ped player)
        {
            PassengerControlData data = recruitedPassenger;

            if (data == null)
                return;

            if (!HasRecruitedPassengerSeated(player))
            {
                if (data.TyreAttackActive)
                    StopPassengerTyreAttack(data);
                return;
            }

            // Consume INPUT_DETONATE while a recruited passenger is present so
            // the key cannot also trigger the player's normal explosive action.
            DisableControlThisFrame(0, PassengerTyreAttackControl);

            if (IsDisabledControlJustPressed(0, PassengerTyreAttackControl) &&
                !data.TyreAttackActive &&
                !holdingAttack &&
                (nextPassengerTyreAttackAllowedTime < 0 ||
                 IsGameTimeReached(nextPassengerTyreAttackAllowedTime)))
            {
                StartPassengerTyreAttack(player);
            }

            if (!data.TyreAttackActive)
                return;

            if (IsGameTimeReached(data.TyreAttackEndTime))
            {
                StopPassengerTyreAttack(data);
                return;
            }

            if (data.TyreAttackBurstTime >= 0 &&
                !data.TyreAttackImpactResolved &&
                IsGameTimeReached(data.TyreAttackBurstTime))
            {
                ResolvePassengerTyreAttackImpact(data);
            }

            if (data.TyreAttackNextRefreshTime >= 0 &&
                !IsGameTimeReached(data.TyreAttackNextRefreshTime))
            {
                return;
            }

            data.TyreAttackNextRefreshTime = GetGameDeadline(
                PassengerTyreAttackTargetRefreshInterval);

            Ped passenger = data.Passenger;
            if (passenger == null || !passenger.Exists() || !passenger.IsAlive)
            {
                StopPassengerTyreAttack(data);
                return;
            }

            Vehicle targetVehicle = data.TyreAttackTargetVehicle;
            if (targetVehicle == null || !targetVehicle.Exists() || targetVehicle.IsDead)
            {
                targetVehicle = FindPassengerTyreAttackTarget(
                    player,
                    data.Vehicle);

                if (targetVehicle != null)
                    SetPassengerTyreAttackTarget(data, player, targetVehicle);
            }

            if (targetVehicle == null || !targetVehicle.Exists() || targetVehicle.IsDead)
                return;

            if (!data.TyreAttackTaskStarted)
            {
                StartPassengerTyreAttackTask(data, player);
            }
            else
            {
                int wheelIndex;
                Vector3 aimPosition;
                bool hasWheel = TryGetPassengerTyreAttackAim(
                    targetVehicle,
                    player,
                    out wheelIndex,
                    out aimPosition);

                data.TyreAttackWheelIndex = wheelIndex;
                data.TyreAttackTargetHasWheel = hasWheel;

                try
                {
                    Function.Call(
                        Hash.SET_DRIVEBY_TASK_TARGET,
                        passenger.Handle,
                        0,
                        targetVehicle.Handle,
                        aimPosition.X,
                        aimPosition.Y,
                        aimPosition.Z);
                }
                catch
                {
                    // Re-create only the secondary drive-by task if target refresh
                    // is unsupported by the current game build.
                    data.TyreAttackTaskStarted = false;
                    StartPassengerTyreAttackTask(data, player);
                }
            }
        }

        private void StopPassengerTyreAttack(PassengerControlData data)
        {
            if (data == null ||
                (!data.TyreAttackActive && data.TyreAttackWeaponHash == 0))
            {
                return;
            }

            Ped passenger = data.Passenger;
            if (passenger != null && passenger.Exists())
            {
                try
                {
                    Function.Call(
                        Hash.CLEAR_DRIVEBY_TASK_UNDERNEATH_DRIVING_TASK,
                        passenger.Handle);
                }
                catch
                {
                }

                try
                {
                    Function.Call(
                        Hash.CLEAR_PED_SECONDARY_TASK,
                        passenger.Handle);
                }
                catch
                {
                }

                if (data.TyreAttackWeaponHash != 0)
                {
                    if (data.TyreAttackWeaponWasOwned)
                    {
                        if (data.TyreAttackPreviousAmmo >= 0)
                        {
                            try
                            {
                                // Ensure the weapon exists before restoring its old ammo.
                                if (!Function.Call<bool>(
                                    Hash.HAS_PED_GOT_WEAPON,
                                    passenger.Handle,
                                    data.TyreAttackWeaponHash,
                                    false))
                                {
                                    Function.Call(
                                        Hash.GIVE_WEAPON_TO_PED,
                                        passenger.Handle,
                                        data.TyreAttackWeaponHash,
                                        0,
                                        false,
                                        false);
                                }

                                Function.Call(
                                    Hash.SET_PED_AMMO,
                                    passenger.Handle,
                                    data.TyreAttackWeaponHash,
                                    data.TyreAttackPreviousAmmo,
                                    false);
                            }
                            catch
                            {
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            Function.Call(
                                Hash.REMOVE_WEAPON_FROM_PED,
                                passenger.Handle,
                                data.TyreAttackWeaponHash);
                        }
                        catch
                        {
                        }
                    }
                }

                uint previousWeapon = data.TyreAttackPreviousSelectedWeaponHash;
                try
                {
                    if (previousWeapon != 0 && Function.Call<bool>(
                        Hash.HAS_PED_GOT_WEAPON,
                        passenger.Handle,
                        previousWeapon,
                        false))
                    {
                        Function.Call(
                            Hash.SET_CURRENT_PED_WEAPON,
                            passenger.Handle,
                            previousWeapon,
                            true);
                    }
                    else
                    {
                        Function.Call(
                            Hash.SET_CURRENT_PED_WEAPON,
                            passenger.Handle,
                            UnarmedWeaponHash,
                            true);
                    }
                }
                catch
                {
                }

                if (data.TyreAttackPreviousAccuracy >= 0)
                {
                    try
                    {
                        Function.Call(
                            Hash.SET_PED_ACCURACY,
                            passenger.Handle,
                            data.TyreAttackPreviousAccuracy);
                    }
                    catch
                    {
                    }
                }
            }

            data.TyreAttackActive = false;
            data.TyreAttackEndTime = -1;
            data.TyreAttackNextRefreshTime = -1;
            data.TyreAttackBurstTime = -1;
            data.TyreAttackTaskStarted = false;
            data.TyreAttackWeaponHash = 0;
            data.TyreAttackPreviousSelectedWeaponHash = 0;
            data.TyreAttackWeaponWasOwned = false;
            data.TyreAttackPreviousAmmo = -1;
            data.TyreAttackPreviousAccuracy = -1;
            data.TyreAttackTargetVehicle = null;
            data.TyreAttackWheelIndex = -1;
            data.TyreAttackTargetHasWheel = false;
            data.TyreAttackHitRollPassed = false;
            data.TyreAttackImpactResolved = false;

            lastSynchronizedPassengerWeaponHandle = 0;
        }

        // Kích hoạt chuỗi ba hướng dẫn đúng một lần khi người chơi lần đầu tiên đá trúng mục tiêu.
        private void RegisterFirstKickHelp()
        {
            if (firstKickHelpShown)
                return;

            firstKickHelpShown = true;
            firstKickHelpStage = FirstKickHelpStageInitial;
            firstKickHelpEndTime = GetGameDeadline(FirstKickHelpDuration);
        }

        // Chuỗi helptext: 10 giây đầu, sau đó mỗi hướng dẫn cách nhau 3 giây
        // và hiển thị trong 7 giây; gồm đủ năm hướng dẫn theo đúng thứ tự.
        private void UpdateFirstKickHelp()
        {
            if (!firstKickHelpShown ||
                firstKickHelpEndTime < 0 ||
                firstKickHelpStage == FirstKickHelpStageComplete)
            {
                return;
            }

            if (IsGameTimeReached(firstKickHelpEndTime))
            {
                switch (firstKickHelpStage)
                {
                    case FirstKickHelpStageInitial:
                        firstKickHelpStage = FirstKickHelpStageSecondDelay;
                        firstKickHelpEndTime = GetGameDeadline(FollowUpKickHelpDelay);
                        return;

                    case FirstKickHelpStageSecondDelay:
                        firstKickHelpStage = FirstKickHelpStageSecond;
                        firstKickHelpEndTime = GetGameDeadline(FollowUpKickHelpDuration);
                        break;

                    case FirstKickHelpStageSecond:
                        firstKickHelpStage = FirstKickHelpStageThirdDelay;
                        firstKickHelpEndTime = GetGameDeadline(FollowUpKickHelpDelay);
                        return;

                    case FirstKickHelpStageThirdDelay:
                        firstKickHelpStage = FirstKickHelpStageThird;
                        firstKickHelpEndTime = GetGameDeadline(FollowUpKickHelpDuration);
                        break;

                    case FirstKickHelpStageThird:
                        firstKickHelpStage = FirstKickHelpStageFourthDelay;
                        firstKickHelpEndTime = GetGameDeadline(FollowUpKickHelpDelay);
                        return;

                    case FirstKickHelpStageFourthDelay:
                        firstKickHelpStage = FirstKickHelpStageFourth;
                        firstKickHelpEndTime = GetGameDeadline(FollowUpKickHelpDuration);
                        break;

                    case FirstKickHelpStageFourth:
                        firstKickHelpStage = FirstKickHelpStageFifthDelay;
                        firstKickHelpEndTime = GetGameDeadline(FollowUpKickHelpDelay);
                        return;

                    case FirstKickHelpStageFifthDelay:
                        firstKickHelpStage = FirstKickHelpStageFifth;
                        firstKickHelpEndTime = GetGameDeadline(FollowUpKickHelpDuration);
                        break;

                    case FirstKickHelpStageFifth:
                    default:
                        firstKickHelpStage = FirstKickHelpStageComplete;
                        firstKickHelpEndTime = -1;
                        return;
                }
            }

            switch (firstKickHelpStage)
            {
                case FirstKickHelpStageInitial:
                    GTA.UI.Screen.ShowHelpTextThisFrame(FirstKickHelpMessage);
                    break;

                case FirstKickHelpStageSecond:
                    GTA.UI.Screen.ShowHelpTextThisFrame(CounterKickHelpMessage);
                    break;

                case FirstKickHelpStageThird:
                    GTA.UI.Screen.ShowHelpTextThisFrame(PassengerTyreAttackHelpMessage);
                    break;

                case FirstKickHelpStageFourth:
                    GTA.UI.Screen.ShowHelpTextThisFrame(PassengerTransferHelpMessage);
                    break;

                case FirstKickHelpStageFifth:
                    GTA.UI.Screen.ShowHelpTextThisFrame(PassengerReturnHelpMessage);
                    break;
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            Ped player = Game.Player.Character;

            if (player == null || !player.Exists())
            {
                PenalizeAndDetachHighSpeedVehicleKickStreak();
                UpdateKickRewardCycle();
                UpdateKickBuffCycle();
                ReleaseRecruitedPassenger();
                bikeWeaponPermissionsApplied = false;
                nextBikeWeaponPermissionRefreshTime = -1;
                UpdatePassengerExitTasks();
                UpdateNpcVictimSteeringEffects();
                return;
            }

            // Phát hiện rời xe/ngã xe/chuyển xe để trừ đúng một nấc chuỗi tốc độ cao.
            UpdateHighSpeedVehicleKickStreakState(player);

            // Kết thúc cửa sổ tích lũy hoặc thời gian hiệu lực phần thưởng đúng hạn.
            UpdateKickRewardCycle();

            // Buff giảm theo thời gian thật của game, kể cả khi người chơi đã rời xe.
            UpdateKickBuffCycle();

            // Hiển thị hướng dẫn tuyển NPC ghế phụ nếu đang trong thời gian.
            UpdateFirstKickHelp();

            // Cập nhật độ lệch bánh lái sau khi đá trúng phương tiện.
            UpdatePlayerKickSteeringEffect();

            // Duy trì hiệu ứng đánh lái tạm thời trên những xe do NPC điều khiển.
            UpdateNpcVictimSteeringEffects();

            // Cập nhật và tự động kết thúc camera shake.
            UpdateVehicleKickCameraShake();

            // Cập nhật trạng thái truy đuổi NPC.
            UpdateVehicleChases();

            // Hoàn tất khôi phục AI cho NPC đã rời ghế phụ ở frame trước.
            UpdatePassengerExitTasks();

            // Quản lý NPC ghế phụ; NPC được trả về ngay khi người chơi rời xe.
            UpdateRecruitedPassenger(player);

            // INPUT_DETONATE starts/updates the passenger's temporary six-second tyre drive-by.
            UpdatePassengerTyreAttack(player);

            // NPC đang lái xe máy/xe đạp có thể tự động đá ngược người chơi ở cự ly gần.
            UpdateNpcBikeCounterAttacks(player);

            // During the Buff drain, the recruited passenger independently scans
            // for nearby Peds/vehicles and performs its own kick without player input.
            UpdateBuffPassengerCounterAttacks(player);

            // Kiểm tra thời tiết và điều chỉnh độ bám đường.
            UpdateWeatherAndBikeGrip(player);

            Vehicle currentPlayerVehicle = null;
            bool ridingBike = false;

            if (player.IsInVehicle())
            {
                currentPlayerVehicle = player.CurrentVehicle;

                if (currentPlayerVehicle != null && currentPlayerVehicle.Exists())
                {
                    ridingBike = IsMotorcycleOrCycle(currentPlayerVehicle);
                }
            }

            UpdateBikeWeaponEntryWeaponSnapshot(player, ridingBike);

            if (ridingBike)
            {
                // The three permission natives are persistent. Refresh them every
                // 750 ms instead of issuing them on every rendered frame.
                if (!bikeWeaponPermissionsApplied ||
                    nextBikeWeaponPermissionRefreshTime < 0 ||
                    IsGameTimeReached(nextBikeWeaponPermissionRefreshTime))
                {
                    EnableBikeWeaponSelection(player);
                    bikeWeaponPermissionsApplied = true;
                    nextBikeWeaponPermissionRefreshTime = GetGameDeadline(
                        BikeWeaponPermissionRefreshInterval);
                }

                HandlePassengerRecruitment(player, currentPlayerVehicle);
                UpdateBikeWeaponSelection(player);

                if (IsPassengerTyreAttackActive())
                {
                    DisableControlThisFrame(0, leftAttackControl);
                    DisableControlThisFrame(0, rightAttackControl);

                    if (holdingAttack)
                    {
                        CleanupWeaponProp();
                        CleanupPassengerWeaponProp();
                        holdingAttack = false;
                    }
                }

                if (!IsPassengerTyreAttackActive() &&
                    IsControlPressed(0, (Control)73))
                {
                    DisableControlThisFrame(0, leftAttackControl);
                    DisableControlThisFrame(0, rightAttackControl);

                    if (IsDisabledControlPressed(0, leftAttackControl))
                    {
                        if (!holdingAttack && !IsKickCooldownActive())
                        {
                            PrepareWeaponProp(player, true);

                            PlayAnim(
                                player,
                                "anim@veh@drivebybike@dirt@front@" + animWeapType,
                                "melee_idle_l",
                                -1,
                                9);

                            if (HasRecruitedPassengerSeated(player) &&
                                !IsKickBuffDraining())
                            {
                                PreparePassengerWeaponProp(
                                    recruitedPassenger.Passenger,
                                    true);

                                PlayAnim(
                                    recruitedPassenger.Passenger,
                                    "anim@veh@drivebybike@dirt@front@" + animWeapType,
                                    "melee_idle_l",
                                    -1,
                                    9);
                            }

                            holdingAttack = true;
                        }
                    }
                    else if (
                        IsDisabledControlPressed(0, rightAttackControl) &&
                        !holdingAttack &&
                        !IsKickCooldownActive())
                    {
                        PrepareWeaponProp(player, false);

                        PlayAnim(
                            player,
                            "anim@veh@drivebybike@dirt@front@" + animWeapType,
                            "melee_idle_r",
                            -1,
                            9);

                        if (HasRecruitedPassengerSeated(player) &&
                            !IsKickBuffDraining())
                        {
                            PreparePassengerWeaponProp(
                                recruitedPassenger.Passenger,
                                false);

                            PlayAnim(
                                recruitedPassenger.Passenger,
                                "anim@veh@drivebybike@dirt@front@" + animWeapType,
                                "melee_idle_r",
                                -1,
                                9);
                        }

                        holdingAttack = true;
                    }
                }

                if (holdingAttack && !IsPassengerTyreAttackActive())
                {
                    if (IsDisabledControlJustReleased(0, leftAttackControl))
                    {
                        PlayAnim(
                            player,
                            "anim@veh@drivebybike@dirt@front@" + animWeapType,
                            "melee_l",
                            500,
                            8);

                        if (HasRecruitedPassengerSeated(player) &&
                            !IsKickBuffDraining())
                        {
                            PlayAnim(
                                recruitedPassenger.Passenger,
                                "anim@veh@drivebybike@dirt@front@" + animWeapType,
                                "melee_l",
                                500,
                                8);
                        }

                        WaitWhileDrawingKickProgressHuds(100);

                        MeleeBikeContact(player.Position + player.RightVector * -1f, -15f);

                        WaitWhileUpdatingKickSteering(500);

                        CleanupWeaponProp();
                        CleanupPassengerWeaponProp();
                        holdingAttack = false;
                    }

                    if (IsDisabledControlJustReleased(0, rightAttackControl))
                    {
                        PlayAnim(
                            player,
                            "anim@veh@drivebybike@dirt@front@" + animWeapType,
                            "melee_r",
                            500,
                            8);

                        if (HasRecruitedPassengerSeated(player) &&
                            !IsKickBuffDraining())
                        {
                            PlayAnim(
                                recruitedPassenger.Passenger,
                                "anim@veh@drivebybike@dirt@front@" + animWeapType,
                                "melee_r",
                                500,
                                8);
                        }

                        WaitWhileDrawingKickProgressHuds(100);

                        MeleeBikeContact(player.Position + player.RightVector * 1f, 15f);

                        WaitWhileUpdatingKickSteering(500);

                        CleanupWeaponProp();
                        CleanupPassengerWeaponProp();
                        holdingAttack = false;
                    }
                }
            }
            else
            {
                bikeWeaponSelectionInitialized = false;
                bikeWeaponPermissionsApplied = false;
                nextBikeWeaponPermissionRefreshTime = -1;
                lastSynchronizedPassengerWeaponHandle = 0;

                int weaponGroup = unchecked((int)player.Weapons.Current.Group);

                if (weaponGroup == -1609580060)
                {
                    animWeapType = "melee_kick";
                }
                else if (weaponGroup == -728555052)
                {
                    animWeapType = "melee_1h";

                    if (player.Weapons.CurrentWeaponObject != null &&
                        player.Weapons.CurrentWeaponObject.Exists())
                    {
                        bikeMeleeWpnModel = player.Weapons.CurrentWeaponObject.Model;
                    }
                }
                else
                {
                    animWeapType = "melee_small_wpn";

                    if (player.Weapons.CurrentWeaponObject != null &&
                        player.Weapons.CurrentWeaponObject.Exists())
                    {
                        bikeMeleeWpnModel = player.Weapons.CurrentWeaponObject.Model;
                    }
                }

                CleanupWeaponProp();
                CleanupPassengerWeaponProp();
                holdingAttack = false;
            }

            InputMethod currentInputMethod = Game.LastInputMethod;

            if (!inputMethodInitialized ||
                currentInputMethod != lastInputMethod)
            {
                lastInputMethod = currentInputMethod;
                inputMethodInitialized = true;

                if (currentInputMethod == InputMethod.MouseAndKeyboard)
                {
                    leftAttackControl = (Control)24;
                    rightAttackControl = (Control)25;
                }
                else
                {
                    leftAttackControl = (Control)37;
                    rightAttackControl = (Control)44;
                }
            }

            // Draw after processing the current tick so a hit registered during
            // this tick is reflected immediately. The wait/yield helpers below
            // keep redrawing both HUDs on every yielded frame as well.
            DrawKickProgressHuds(player);
        }

        // Kiểm tra người chơi có đang điều khiển xe máy
        // trên địa hình off-road hay không.
        //
        // Không phụ thuộc thời tiết.
        // Không thay đổi logic giảm độ bám đường hiện tại.
        private static bool IsPlayerOnOffRoadTerrain(Ped player)
        {
            if (player == null ||
                !player.Exists() ||
                !player.IsInVehicle())
            {
                return false;
            }

            try
            {
                Vehicle vehicle = player.CurrentVehicle;

                if (vehicle == null ||
                    !vehicle.Exists() ||
                    !IsMotorcycleOrCycle(vehicle))
                {
                    return false;
                }

                Ped driver = vehicle.GetPedOnSeat(VehicleSeat.Driver);

                if (driver == null ||
                    !driver.Exists() ||
                    driver.Handle != player.Handle)
                {
                    return false;
                }

                return !IsVehicleOnRoad(vehicle);
            }
            catch
            {
                return false;
            }
        }

        // Kiểm tra vị trí xe có nằm trên đường giao thông hay không.
        private static bool IsVehicleOnRoad(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists())
                return true;

            try
            {
                Vector3 position = vehicle.Position;

                return Function.Call<bool>(
                    Hash.IS_POINT_ON_ROAD,
                    position.X,
                    position.Y,
                    position.Z,
                    vehicle.Handle);
            }
            catch
            {
                return true;
            }
        }

        // Các thời tiết khô được phép kích hoạt hiệu ứng off-road.
        private static bool IsDryWeatherForOffRoad(Weather weather)
        {
            switch (weather)
            {
                case Weather.ExtraSunny:
                case Weather.Clear:
                case Weather.Clearing:
                case Weather.Clouds:
                case Weather.Overcast:
                case Weather.Smog:
                    return true;

                default:
                    return false;
            }
        }

        // Kiểm tra vũ khí đang được chọn thực tế, không dựa vào animation/prop.
        private uint GetEffectiveWeaponHash(Ped ped)
        {
            if (ped == null || !ped.Exists())
                return 0;

            try
            {
                Ped player = Game.Player.Character;

                // While riding, use the weapon captured before boarding. GTA can
                // report its temporary/default vehicle weapon instead of the
                // weapon that the player actually selected on foot.
                if (player != null && player.Exists() &&
                    ped.Handle == player.Handle &&
                    bikeEntryWeaponSnapshotInitialized &&
                    ped.IsInVehicle())
                {
                    return bikeEntryWeaponHash;
                }
            }
            catch
            {
            }

            return GetSelectedWeaponHash(ped);
        }

        private bool HasListedMeleeDamageWeapon(Ped ped)
        {
            if (ped == null || !ped.Exists())
                return false;

            uint selectedWeaponHash = GetEffectiveWeaponHash(ped);
            return ListedMeleeWeaponDamageHashes.Contains(selectedWeaponHash);
        }

        // Hai người cùng đá chỉ nhận một lần bonus +32%, không cộng thành +64%.
        // NPC ghế phụ thường đã được đồng bộ vũ khí từ người chơi; vẫn kiểm tra cả hai.
        private float GetListedMeleeWeaponDamageMultiplier(
            Ped player,
            Ped passenger)
        {
            return HasListedMeleeDamageWeapon(player) ||
                   HasListedMeleeDamageWeapon(passenger)
                ? ListedMeleeWeaponDamageMultiplier
                : 1.0f;
        }

        // Sát thương cố định x8 cho cú đá đặc biệt; không cộng dồn hệ số tốc độ 4.85
        // hoặc x1.96 hành khách. Bonus vũ khí chỉ thay đổi sát thương nền trước x8.
        private float GetSpecialTargetPedKickDamage(
            Ped target,
            Ped player,
            Ped passenger,
            float specialKickDamageMultiplier = 1.0f)
        {
            float damage = DefaultPedKickDamage * 8.0f *
                specialKickDamageMultiplier *
                GetListedMeleeWeaponDamageMultiplier(player, passenger);

            // The player's helmet strengthens Special Kick; the target's helmet
            // reduction below remains a separate, fully preserved defense.
            if (IsPedWearingHelmet(player))
                damage *= HelmetSpecialKickDamageMultiplier;

            if (IsPedWearingHelmet(target))
                damage *= 0.48f;

            return damage;
        }

        // Tính sát thương cú đá lên NPC.
        // NPC đội mũ bảo hiểm chỉ nhận 48% sát thương.
        private int GetTargetPedKickDamage(
            Ped target,
            Ped player,
            Ped passenger)
        {
            int damage = GetPedKickDamage(player, passenger);

            if (IsPedWearingHelmet(target))
            {
                damage = (int)Math.Round(
                    damage * 0.48f,
                    MidpointRounding.AwayFromZero);
            }

            return damage;
        }

        // Các Vehicle Class không được kích hoạt truy đuổi.
        private static bool IsExcludedFromVehicleChase(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists())
                return true;

            int vehicleClass = GetVehicleClassCached(vehicle);

            switch (vehicleClass)
            {
                case 14: // Boats
                case 15: // Helicopters
                case 16: // Planes
                case 18: // Emergency
                case 19: // Military
                case 20: // Commercial
                case 21: // Trains
                    return true;

                default:
                    return false;
            }
        }

        // Kiểm tra NPC đã được kích hoạt truy đuổi hay chưa.
        private bool IsDriverAlreadyChasing(Ped driver)
        {
            if (driver == null || !driver.Exists())
                return false;

            int driverHandle = driver.Handle;

            for (int i = activeVehicleChases.Count - 1; i >= 0; i--)
            {
                VehicleChaseData data = activeVehicleChases[i];

                if (data == null)
                    continue;

                Ped activeDriver = data.Driver;

                if (activeDriver != null &&
                    activeDriver.Handle == driverHandle)
                {
                    return true;
                }
            }

            return false;
        }

        // Cấp một vũ khí chưa có trong túi của NPC để có thể thu hồi sạch sau 60 giây.
        private bool GiveTemporaryRetaliationWeapon(Ped driver, VehicleChaseData data)
        {
            if (driver == null || !driver.Exists() || data == null ||
                VehicleRetaliationWeaponHashes == null || VehicleRetaliationWeaponHashes.Length == 0)
            {
                return false;
            }

            try
            {
                data.PreviousWeaponHash = Function.Call<uint>(
                    Hash.GET_SELECTED_PED_WEAPON,
                    driver.Handle);

                data.PreviousAccuracy = Function.Call<int>(
                    Hash.GET_PED_ACCURACY,
                    driver.Handle);

                int firstIndex = soundRandom.Next(VehicleRetaliationWeaponHashes.Length);
                uint weapon = 0;

                // Tránh ghi đè vũ khí đã có sẵn; chỉ cấp vũ khí mới để có thể xóa sau đó.
                for (int i = 0; i < VehicleRetaliationWeaponHashes.Length; i++)
                {
                    uint candidate = VehicleRetaliationWeaponHashes[
                        (firstIndex + i) % VehicleRetaliationWeaponHashes.Length];

                    bool alreadyOwned = Function.Call<bool>(
                        Hash.HAS_PED_GOT_WEAPON,
                        driver.Handle,
                        candidate,
                        false);

                    if (!alreadyOwned)
                    {
                        weapon = candidate;
                        break;
                    }
                }

                // Nếu NPC đã có toàn bộ weapon pool, không thay đổi túi vũ khí của NPC.
                if (weapon == 0)
                    return false;

                data.GrantedWeaponHash = weapon;

                Function.Call(
                    Hash.GIVE_WEAPON_TO_PED,
                    driver.Handle,
                    weapon,
                    9999,
                    true,
                    true);

                Function.Call(
                    Hash.SET_CURRENT_PED_WEAPON,
                    driver.Handle,
                    weapon,
                    true);

                Function.Call(Hash.SET_PED_ACCURACY, driver.Handle, ArmedVehicleChaseAccuracy);
                Function.Call(Hash.SET_PED_FIRING_PATTERN, driver.Handle, FiringPatternBurstFireDriveBy);
                Function.Call(Hash.SET_PED_SHOOT_RATE, driver.Handle, 100);
                Function.Call(Hash.SET_PED_COMBAT_ABILITY, driver.Handle, 2);

                return true;
            }
            catch
            {
                RestoreTemporaryRetaliationWeapon(driver, data);
                return false;
            }
        }

        // Thu hồi đúng vũ khí do mod vừa cấp và trả lại vũ khí được chọn trước đó.
        private static void RestoreTemporaryRetaliationWeapon(Ped driver, VehicleChaseData data)
        {
            if (driver == null || !driver.Exists() || data == null || data.GrantedWeaponHash == 0)
                return;

            try
            {
                Function.Call(
                    Hash.REMOVE_WEAPON_FROM_PED,
                    driver.Handle,
                    data.GrantedWeaponHash);
            }
            catch
            {
            }

            try
            {
                if (data.PreviousWeaponHash != 0)
                {
                    Function.Call(
                        Hash.SET_CURRENT_PED_WEAPON,
                        driver.Handle,
                        data.PreviousWeaponHash,
                        true);
                }

                if (data.PreviousAccuracy >= 0)
                {
                    Function.Call(
                        Hash.SET_PED_ACCURACY,
                        driver.Handle,
                        data.PreviousAccuracy);
                }
            }
            catch
            {
            }

            data.GrantedWeaponHash = 0;
        }

        // Duy trì đồng thời hành vi lái xe truy đuổi và bắn trả bằng chính tài xế.
        private void RefreshArmedVehicleChase(VehicleChaseData data, Ped player, bool force)
        {
            if (data == null || !data.IsArmedChasing ||
                data.Driver == null || !data.Driver.Exists() || !data.Driver.IsAlive ||
                data.Vehicle == null || !data.Vehicle.Exists() ||
                player == null || !player.Exists() || player.IsDead)
            {
                return;
            }

            Ped driver = data.Driver;

            if (!driver.IsInVehicle())
                return;

            Vehicle currentVehicle = driver.CurrentVehicle;

            if (currentVehicle == null || !currentVehicle.Exists() ||
                currentVehicle.Handle != data.Vehicle.Handle)
            {
                return;
            }

            int now = Game.GameTime;

            if (!force && data.LastArmedAttackAt >= 0 &&
                now - data.LastArmedAttackAt < ArmedVehicleAttackRefreshInterval)
            {
                return;
            }

            data.LastArmedAttackAt = now;

            try
            {
                Function.Call(Hash.SET_PED_KEEP_TASK, driver.Handle, true);
                Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, driver.Handle, true);
                Function.Call(Hash.SET_DRIVER_ABILITY, driver.Handle, 1.0f);
                Function.Call(Hash.SET_DRIVER_AGGRESSIVENESS, driver.Handle, 1.0f);
                Function.Call(Hash.SET_PED_ACCURACY, driver.Handle, ArmedVehicleChaseAccuracy);
                Function.Call(Hash.SET_PED_FIRING_PATTERN, driver.Handle, FiringPatternBurstFireDriveBy);
                Function.Call(Hash.SET_PED_SHOOT_RATE, driver.Handle, 100);
                Function.Call(Hash.SET_PED_COMBAT_ABILITY, driver.Handle, 2);

                Function.Call(Hash.TASK_VEHICLE_CHASE, driver.Handle, player.Handle);
                Function.Call(Hash.SET_DRIVE_TASK_DRIVING_STYLE, driver.Handle, 786603);

                // Cùng cơ chế driver vừa lái vừa bắn được dùng trong JustBusinessMission.
                Function.Call(
                    Hash.ADD_VEHICLE_SUBTASK_ATTACK_PED,
                    driver.Handle,
                    player.Handle);
            }
            catch
            {
                try
                {
                    Function.Call(Hash.TASK_VEHICLE_CHASE, driver.Handle, player.Handle);
                }
                catch
                {
                }
            }
        }

        // Kích hoạt NPC phản ứng ngẫu nhiên sau khi phương tiện bị đá.
        // Một người: giữ nguyên 50% truy đuổi / 50% bỏ chạy.
        // Hai người: bổ sung hành vi truy đuổi có vũ khí trong 60 giây.
        private void StartVehicleChase(Vehicle vehicle, bool hasPassenger)
        {
            Ped player = Game.Player.Character;

            if (player == null || !player.Exists())
                return;

            if (vehicle == null ||
                !vehicle.Exists() ||
                vehicle.IsDead)
            {
                return;
            }

            if (IsExcludedFromVehicleChase(vehicle))
                return;

            // Không kích hoạt phản ứng ngẫu nhiên đối với chiếc xe mà VehicleDelivery
            // xác nhận đang được chính NPC trộm xe điều khiển.
            // Các hiệu ứng đá xe khác trong KickVehicle vẫn tiếp tục bình thường.
            if (global::VehicleDelivery.IsVehicleBeingDrivenByTheftNpc(vehicle.Handle))
                return;

            Ped driver = vehicle.Driver;

            if (driver == null ||
                !driver.Exists() ||
                !driver.IsHuman ||
                !driver.IsAlive)
            {
                return;
            }

            // Không kích hoạt lại hoặc làm mới thời gian.
            if (IsDriverAlreadyChasing(driver))
                return;

            // Khi đã đủ sáu NPC đang thực hiện phản ứng, không gán thêm AI task,
            // không cấp vũ khí tạm và không thêm NPC mới vào danh sách quản lý.
            // Sát thương/lực đá phương tiện bên ngoài hàm này vẫn giữ nguyên.
            if (activeVehicleChases.Count >= MaximumActiveNpcKickReactions)
                return;

            // NPC phải đang thực sự điều khiển phương tiện.
            if (!driver.IsInVehicle())
                return;

            if (driver.CurrentVehicle == null ||
                !driver.CurrentVehicle.Exists() ||
                driver.CurrentVehicle.Handle != vehicle.Handle)
            {
                return;
            }

            bool armedChase = false;
            bool panicFlee;
            bool panicSkidFlee = false;

            if (hasPassenger)
            {
                // Ba hành vi có xác suất ngang nhau khi có người ngồi ghế phụ.
                int behavior = soundRandom.Next(3);
                armedChase = behavior == 2;
                panicFlee = behavior == 1;
            }
            else
            {
                // Không có người ngồi ghế phụ: giữ nguyên xác suất 50/50 hiện tại.
                panicFlee = soundRandom.Next(100) < 50;
            }

            // Nested random choice: only NPCs already assigned to panic-flee
            // receive the second 50/50 choice. The original parent probabilities
            // (one rider 50/50; two riders three equal branches) remain unchanged.
            if (panicFlee)
                panicSkidFlee = soundRandom.Next(2) == 1;

            VehicleChaseData chaseData = new VehicleChaseData();
            chaseData.Driver = driver;
            chaseData.Vehicle = vehicle;
            chaseData.IsFleeing = panicFlee;
            chaseData.IsPanicSkidFlee = panicSkidFlee;
            chaseData.PanicSkidStartTime = panicSkidFlee
                ? GetGameDeadline(VehiclePanicFleeSkidDelay)
                : -1;

            // Nếu native cấp vũ khí thất bại, tự động quay về hành vi truy đuổi cũ.
            if (armedChase && !GiveTemporaryRetaliationWeapon(driver, chaseData))
            {
                armedChase = false;
                panicFlee = false;
                chaseData.IsFleeing = false;
            }

            // Duy trì NPC trong trạng thái nhiệm vụ.
            driver.IsPersistent = true;
            driver.BlockPermanentEvents = true;
            driver.AlwaysKeepTask = true;

            Function.Call(Hash.SET_DRIVER_ABILITY, driver, 1.0f);

            Function.Call(
                Hash.SET_DRIVER_AGGRESSIVENESS,
                driver,
                panicFlee ? 0.0f : 1.0f);

            Function.Call(
                Hash.SET_DRIVE_TASK_DRIVING_STYLE,
                driver,
                786603);

            if (panicFlee)
            {
                // PANIC FLEE
                Vector3 fleeDirection = vehicle.Position - player.Position;

                fleeDirection.Z = 0f;

                float directionLength = fleeDirection.Length();

                if (directionLength > 0.01f)
                {
                    fleeDirection /= directionLength;
                }
                else
                {
                    fleeDirection = vehicle.ForwardVector;
                }

                float fleeDistance = panicSkidFlee
                    ? VehiclePanicFleeSkidDistance
                    : VehiclePanicFleeDistance;

                Vector3 fleeDestination = vehicle.Position + fleeDirection * fleeDistance;

                // Giữ độ cao ban đầu của phương tiện.
                fleeDestination.Z = vehicle.Position.Z;

                // NPC lái xe đến vị trí cách xa người chơi.
                Function.Call(
                    Hash.TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE,
                    driver.Handle,
                    vehicle.Handle,
                    fleeDestination.X,
                    fleeDestination.Y,
                    fleeDestination.Z,
                    VehiclePanicFleeSpeed,
                    786603,
                    15.0f);
            }
            else if (armedChase)
            {
                // TRUY ĐUỔI + BẮN TRẢ
                chaseData.IsArmedChasing = true;
                chaseData.LastArmedAttackAt = -1;
                RefreshArmedVehicleChase(chaseData, player, true);
            }
            else
            {
                // VEHICLE CHASE cũ
                Function.Call(
                    Hash.TASK_VEHICLE_CHASE,
                    driver,
                    player);

                Function.Call(
                    Hash.SET_DRIVE_TASK_DRIVING_STYLE,
                    driver,
                    786603);
            }

            chaseData.EndTime = GetGameDeadline(
                armedChase
                    ? ArmedVehicleChaseDuration
                    : (panicFlee ? VehiclePanicFleeDuration : VehicleChaseDuration));

            activeVehicleChases.Add(chaseData);
        }

        // Bắt đầu hiệu ứng panic-skid sau 10 giây bỏ chạy. GTA không cung cấp
        // native tắt phần cứng phanh thực sự; SET_VEHICLE_BRAKE(false) và
        // SET_VEHICLE_HANDBRAKE(false) được duy trì trong mỗi chu kỳ 50 ms để
        // mô phỏng hệ thống phanh mất tác dụng, còn reduce-grip level 3 tạo cú trượt dài.
        private static void StartPanicFleeSkid(VehicleChaseData data)
        {
            if (data == null || data.PanicSkidStarted)
                return;

            data.PanicSkidStarted = true;

            Vehicle vehicle = data.Vehicle;
            if (vehicle == null || !vehicle.Exists())
                return;

            try
            {
                Function.Call(Hash.SET_VEHICLE_BRAKE, vehicle.Handle, false);
                Function.Call(Hash.SET_VEHICLE_HANDBRAKE, vehicle.Handle, false);
                Function.Call(Hash.SET_VEHICLE_REDUCE_GRIP, vehicle.Handle, true);
                Function.Call(
                    Hash.SET_VEHICLE_REDUCE_GRIP_LEVEL,
                    vehicle.Handle,
                    VehiclePanicFleeSkidGripLevel);
            }
            catch
            {
            }
        }

        // Khôi phục độ bám đường và nhả các native phanh khi panic-skid kết thúc,
        // NPC/xe biến mất hoặc script bị gỡ.
        private static void RestorePanicFleeSkid(VehicleChaseData data)
        {
            if (data == null || !data.PanicSkidStarted ||
                data.Vehicle == null || !data.Vehicle.Exists())
            {
                return;
            }

            try
            {
                Function.Call(Hash.SET_VEHICLE_BRAKE, data.Vehicle.Handle, false);
                Function.Call(Hash.SET_VEHICLE_HANDBRAKE, data.Vehicle.Handle, false);
                Function.Call(Hash.SET_VEHICLE_REDUCE_GRIP, data.Vehicle.Handle, false);
                Function.Call(Hash.SET_VEHICLE_REDUCE_GRIP_LEVEL, data.Vehicle.Handle, 0);
            }
            catch
            {
            }

            data.PanicSkidStarted = false;
        }

        // Kiểm tra và kết thúc các nhiệm vụ truy đuổi, bỏ chạy hoặc truy đuổi có vũ khí.
        private void UpdateVehicleChases()
        {
            if (activeVehicleChases.Count == 0)
            {
                nextVehicleChaseUpdateTime = -1;
                return;
            }

            if (nextVehicleChaseUpdateTime >= 0 &&
                !IsGameTimeReached(nextVehicleChaseUpdateTime))
            {
                return;
            }

            nextVehicleChaseUpdateTime = GetGameDeadline(
                VehicleChaseMaintenanceInterval);

            for (int i = activeVehicleChases.Count - 1; i >= 0; i--)
            {
                VehicleChaseData data = activeVehicleChases[i];

                if (data == null)
                {
                    activeVehicleChases.RemoveAt(i);
                    continue;
                }

                Ped driver = data.Driver;

                if (driver == null ||
                    !driver.Exists() ||
                    !driver.IsAlive)
                {
                    RestorePanicFleeSkid(data);
                    activeVehicleChases.RemoveAt(i);
                    continue;
                }

                if (data.IsPanicSkidFlee &&
                    !data.PanicSkidStarted &&
                    IsGameTimeReached(data.PanicSkidStartTime))
                {
                    StartPanicFleeSkid(data);
                }

                // Keep forcing the brake and handbrake controls released throughout
                // the skid window so the flee AI cannot regain braking input.
                if (data.PanicSkidStarted && data.Vehicle != null && data.Vehicle.Exists())
                {
                    try
                    {
                        Function.Call(Hash.SET_VEHICLE_BRAKE, data.Vehicle.Handle, false);
                        Function.Call(Hash.SET_VEHICLE_HANDBRAKE, data.Vehicle.Handle, false);
                    }
                    catch
                    {
                    }
                }

                if (!IsGameTimeReached(data.EndTime))
                {
                    if (data.IsArmedChasing)
                    {
                        RefreshArmedVehicleChase(data, Game.Player.Character, false);
                    }

                    continue;
                }

                // Kết thúc hành vi hiện tại.
                driver.Task.ClearAll();
                RestorePanicFleeSkid(data);

                // Thu hồi vũ khí tạm thời và khôi phục lựa chọn vũ khí trước đó.
                if (data.IsArmedChasing)
                {
                    RestoreTemporaryRetaliationWeapon(driver, data);
                }

                driver.AlwaysKeepTask = false;
                driver.BlockPermanentEvents = false;
                driver.IsPersistent = false;

                Function.Call(
                    Hash.SET_DRIVER_ABILITY,
                    driver,
                    0.5f);

                Function.Call(
                    Hash.SET_DRIVER_AGGRESSIVENESS,
                    driver,
                    0.0f);

                activeVehicleChases.RemoveAt(i);
            }
        }

        // Các Vehicle Class được phép kích hoạt camera shake.
        private static bool IsVehicleKickCameraShakeClass(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists())
                return false;

            int vehicleClass = GetVehicleClassCached(vehicle);

            switch (vehicleClass)
            {
                case 10: // Industrial
                case 11: // Utility
                case 12: // Vans
                case 18: // Emergency
                case 19: // Military
                case 20: // Commercial
                case 21: // Trains
                    return true;

                default:
                    return false;
            }
        }

        // Kích hoạt camera shake khi đá phương tiện hạng nặng.
        private void ApplyVehicleKickCameraShake(
            Vehicle vehicle,
            Ped player)
        {
            if (vehicle == null ||
                !vehicle.Exists() ||
                player == null ||
                !player.Exists())
            {
                return;
            }

            if (!IsVehicleKickCameraShakeClass(vehicle))
                return;

            if (!player.IsInVehicle())
                return;

            Vehicle playerVehicle = player.CurrentVehicle;

            if (playerVehicle == null ||
                !playerVehicle.Exists())
            {
                return;
            }

            // Chỉ áp dụng khi người chơi đang điều khiển xe.
            try
            {
                Ped driver = playerVehicle.GetPedOnSeat(
                    VehicleSeat.Driver);

                if (driver == null ||
                    !driver.Exists() ||
                    driver.Handle != player.Handle)
                {
                    return;
                }
            }
            catch
            {
                return;
            }

            float speed = Math.Abs(playerVehicle.Speed);

            // Chưa đạt 90 km/h thì không rung camera.
            if (speed <= VehicleKickCameraShakeThreshold)
                return;

            float intensity;

            // Từ 110 km/h: rung mạnh hơn.
            if (speed >= VehicleKickStrongCameraShakeThreshold)
            {
                intensity = VehicleKickStrongCameraShakeIntensity;
            }
            else
            {
                intensity = VehicleKickCameraShakeIntensity;
            }

            try
            {
                Function.Call(
                    Hash.SHAKE_GAMEPLAY_CAM,
                    "ROAD_VIBRATION_SHAKE",
                    intensity);

                Function.Call(
                    Hash.SET_GAMEPLAY_CAM_SHAKE_AMPLITUDE,
                    intensity);

                vehicleKickCameraShakeActive = true;

                vehicleKickCameraShakeEndTime =
                    GetGameDeadline(VehicleKickCameraShakeDuration);
            }
            catch
            {
            }
        }

        // Tự động kết thúc camera shake.
        private void UpdateVehicleKickCameraShake()
        {
            if (!vehicleKickCameraShakeActive)
                return;

            if (vehicleKickCameraShakeEndTime >= 0 &&
                IsGameTimeReached(vehicleKickCameraShakeEndTime))
            {
                StopVehicleKickCameraShake();
            }
        }

        // Khôi phục camera sau khi kết thúc hiệu ứng.
        private void StopVehicleKickCameraShake()
        {
            if (!vehicleKickCameraShakeActive)
                return;

            try
            {
                Function.Call(
                    Hash.STOP_GAMEPLAY_CAM_SHAKING,
                    true);

                Function.Call(
                    Hash.SET_GAMEPLAY_CAM_SHAKE_AMPLITUDE,
                    0f);
            }
            catch
            {
            }

            vehicleKickCameraShakeActive = false;
            vehicleKickCameraShakeEndTime = -1;
        }

        private static bool IsHeavyOrSpecialVehicleClass(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists())
                return false;

            int vehicleClass = GetVehicleClassCached(vehicle);

            switch (vehicleClass)
            {
                case 10: // Industrial
                case 11: // Utility
                case 12: // Vans
                case 15: // Helicopters
                case 16: // Planes
                case 18: // Emergency
                case 19: // Military
                case 20: // Commercial
                case 21: // Trains
                    return true;

                default:
                    return false;
            }
        }

        private void IncreaseWantedLevelForSpecialVehicle(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists())
                return;

            int vehicleClass = GetVehicleClassCached(vehicle);

            // Chỉ áp dụng cho Emergency (18) và Military (19).
            if (vehicleClass != 18 && vehicleClass != 19)
                return;

            // Cooldown 30 giây theo thời gian game.
            if (!IsGameTimeReached(nextWantedVehicleTime) &&
                nextWantedVehicleTime != 0)
            {
                return;
            }

            int currentWantedLevel = Game.Player.WantedLevel;

            if (currentWantedLevel >= MaxWantedLevel)
                return;

            int newWantedLevel = Math.Min(
                currentWantedLevel + 1,
                MaxWantedLevel);

            Game.Player.WantedLevel = newWantedLevel;

            nextWantedVehicleTime = GetGameDeadline(WantedVehicleCooldown);
        }

        // Return the ped's real selected weapon, normalizing a zero hash to Unarmed.
        private static uint GetSelectedWeaponHash(Ped ped)
        {
            if (ped == null || !ped.Exists())
                return UnarmedWeaponHash;

            try
            {
                uint selectedWeaponHash = Function.Call<uint>(
                    Hash.GET_SELECTED_PED_WEAPON,
                    ped.Handle);

                return selectedWeaponHash == 0
                    ? UnarmedWeaponHash
                    : selectedWeaponHash;
            }
            catch
            {
                return UnarmedWeaponHash;
            }
        }

        // Cache continuously while on foot, then freeze that exact selection on
        // the transition into a motorcycle/bicycle. Do not overwrite it with the
        // default firearm GTA may expose while the player is seated on the bike.
        private void UpdateBikeWeaponEntryWeaponSnapshot(Ped player, bool ridingBike)
        {
            if (player == null || !player.Exists())
                return;

            if (!ridingBike)
            {
                bool playerInAnyVehicle = player.IsInVehicle();

                if (wasRidingMotorcycleOrCycle || playerInAnyVehicle)
                    nextOnFootWeaponSnapshotTime = -1;

                if (!playerInAnyVehicle &&
                    (nextOnFootWeaponSnapshotTime < 0 ||
                     IsGameTimeReached(nextOnFootWeaponSnapshotTime)))
                {
                    lastOnFootWeaponHash = GetSelectedWeaponHash(player);
                    onFootWeaponSnapshotInitialized = true;
                    nextOnFootWeaponSnapshotTime = GetGameDeadline(
                        OnFootWeaponSnapshotInterval);
                }

                wasRidingMotorcycleOrCycle = false;
                bikeEntryWeaponSnapshotInitialized = false;
                return;
            }

            nextOnFootWeaponSnapshotTime = -1;

            if (!wasRidingMotorcycleOrCycle)
            {
                bikeEntryWeaponHash = onFootWeaponSnapshotInitialized
                    ? lastOnFootWeaponHash
                    : GetSelectedWeaponHash(player);

                bikeEntryWeaponSnapshotInitialized = true;
                wasRidingMotorcycleOrCycle = true;
                bikeWeaponSelectionInitialized = false;
                lastSynchronizedPassengerWeaponHandle = 0;
            }
        }

        // Cập nhật hoạt ảnh/prop từ đúng hash đã chọn trước khi lên xe, đồng thời
        // đồng bộ chính vũ khí đó cho NPC ghế phụ trong chế độ hai người.
        private void UpdateBikeWeaponSelection(Ped player)
        {
            if (player == null || !player.Exists() || !player.IsInVehicle())
                return;

            uint selectedWeaponHash = bikeEntryWeaponSnapshotInitialized
                ? bikeEntryWeaponHash
                : GetSelectedWeaponHash(player);

            bool weaponChanged = !bikeWeaponSelectionInitialized ||
                                 selectedWeaponHash != lastBikeWeaponHash;

            // Không đổi hoạt ảnh hoặc prop giữa chừng khi cú đánh hiện tại đang chạy.
            // Nếu vũ khí vừa đổi, lần Tick sau khi đòn đánh kết thúc sẽ áp dụng nó.
            if (!holdingAttack && weaponChanged)
            {
                int weaponGroup = 0;

                if (selectedWeaponHash != UnarmedWeaponHash)
                {
                    try
                    {
                        weaponGroup = Function.Call<int>(
                            Hash.GET_WEAPONTYPE_GROUP,
                            selectedWeaponHash);
                    }
                    catch
                    {
                        weaponGroup = 0;
                    }
                }

                // Unarmed is the only case that keeps the original kick animation.
                // Real melee weapons and pistols use the one-handed weapon set;
                // all remaining weapon groups use the small-weapon set.
                if (selectedWeaponHash == UnarmedWeaponHash)
                {
                    animWeapType = "melee_kick";
                }
                else if (weaponGroup == -1609580060 ||
                         weaponGroup == -728555052 ||
                         ListedMeleeWeaponDamageHashes.Contains(selectedWeaponHash))
                {
                    animWeapType = "melee_1h";
                }
                else
                {
                    animWeapType = "melee_small_wpn";
                }

                try
                {
                    if (selectedWeaponHash == UnarmedWeaponHash)
                    {
                        bikeMeleeWpnModel = new Model(0);
                    }
                    else
                    {
                        // Resolve the model from the saved weapon hash, not from
                        // CurrentWeaponObject (which can be GTA's forced bike gun).
                        int weaponModelHash = Function.Call<int>(
                            Hash.GET_WEAPONTYPE_MODEL,
                            selectedWeaponHash);

                        Model selectedWeaponModel = new Model(weaponModelHash);

                        bikeMeleeWpnModel =
                            selectedWeaponModel.IsValid && selectedWeaponModel.IsInCdImage
                                ? selectedWeaponModel
                                : new Model(0);
                    }
                }
                catch
                {
                    bikeMeleeWpnModel = new Model(0);
                }

                CleanupWeaponProp();
                CleanupPassengerWeaponProp();
                lastBikeWeaponHash = selectedWeaponHash;
                bikeWeaponSelectionInitialized = true;
            }

            if (holdingAttack || IsPassengerTyreAttackActive())
                return;

            if (nextPassengerWeaponSyncCheckTime < 0 ||
                IsGameTimeReached(nextPassengerWeaponSyncCheckTime))
            {
                cachedPassengerCompanionForWeaponSync =
                    HasRecruitedCompanion(player);
                nextPassengerWeaponSyncCheckTime = GetGameDeadline(
                    PassengerWeaponSyncCheckInterval);
            }

            if (!cachedPassengerCompanionForWeaponSync ||
                recruitedPassenger == null)
            {
                return;
            }

            Ped passenger = recruitedPassenger.Passenger;

            if (passenger == null || !passenger.Exists())
            {
                cachedPassengerCompanionForWeaponSync = false;
                nextPassengerWeaponSyncCheckTime = -1;
                return;
            }

            if (weaponChanged ||
                lastSynchronizedPassengerWeaponHandle != passenger.Handle)
            {
                SynchronizePassengerWeaponWithPlayer(
                    player,
                    passenger,
                    selectedWeaponHash);

                lastSynchronizedPassengerWeaponHandle = passenger.Handle;
            }
        }

        // NPC giữ nguyên kho vũ khí cũ; chỉ cấp tạm các hash người chơi chọn mà
        // NPC chưa sở hữu. Những vũ khí được cấp sẽ được thu hồi khi NPC rời nhóm.
        private void SynchronizePassengerWeaponWithPlayer(
            Ped player,
            Ped passenger,
            uint selectedWeaponHash)
        {
            if (player == null || !player.Exists() ||
                passenger == null || !passenger.Exists() ||
                selectedWeaponHash == 0)
            {
                return;
            }

            bool needsGrant = false;
            try
            {
                if (selectedWeaponHash != UnarmedWeaponHash &&
                    !Function.Call<bool>(Hash.IS_WEAPON_VALID, selectedWeaponHash))
                {
                    return;
                }

                bool alreadyOwned = Function.Call<bool>(
                    Hash.HAS_PED_GOT_WEAPON,
                    passenger.Handle,
                    selectedWeaponHash,
                    false);

                if (!alreadyOwned && selectedWeaponHash != UnarmedWeaponHash)
                {
                    needsGrant = true;
                    int playerAmmo = 0;
                    try
                    {
                        playerAmmo = Function.Call<int>(
                            Hash.GET_AMMO_IN_PED_WEAPON,
                            player.Handle,
                            selectedWeaponHash);
                    }
                    catch
                    {
                    }

                    Function.Call(
                        Hash.GIVE_WEAPON_TO_PED,
                        passenger.Handle,
                        selectedWeaponHash,
                        Math.Max(1, playerAmmo),
                        false,
                        false);

                }

                if (needsGrant && Function.Call<bool>(
                    Hash.HAS_PED_GOT_WEAPON,
                    passenger.Handle,
                    selectedWeaponHash,
                    false))
                {
                    RecordPassengerWeaponGrant(selectedWeaponHash);
                }

                if (Function.Call<bool>(
                    Hash.HAS_PED_GOT_WEAPON,
                    passenger.Handle,
                    selectedWeaponHash,
                    false) || selectedWeaponHash == UnarmedWeaponHash)
                {
                    Function.Call(
                        Hash.SET_CURRENT_PED_WEAPON,
                        passenger.Handle,
                        selectedWeaponHash,
                        true);
                }
            }
            catch
            {
            }
        }

        // Ghi nhớ vũ khí tạm được cấp để có thể thu hồi chính xác khi giải phóng NPC.
        private void RecordPassengerWeaponGrant(uint weaponHash)
        {
            PassengerControlData data = recruitedPassenger;

            if (data == null || weaponHash == 0 ||
                data.GrantedWeaponHashes.Contains(weaponHash))
            {
                return;
            }

            data.GrantedWeaponHashes.Add(weaponHash);
        }

        // Draw theo đúng tọa độ/texture timerbars của Loot Bag và Player Lives.
        // Tiến trình lấy trực tiếp từ chuỗi Special Kick hiện có: không tạo state thứ hai.
        private void DrawSpecialKickHud(Ped player, bool visible)
        {
            if (!visible)
                return;

            try
            {
                int remainingCharges = Math.Max(
                    0,
                    Math.Min(
                        HighSpeedSpecialKickChargesPerFullCharge,
                        highSpeedSpecialKickChargesRemaining));
                bool ready = highSpeedSpecialKickReady && remainingCharges > 0;

                // During charging, display quarter-segment fills (0.25/0.5/0.75).
                // Once ready, the same five HUD segments represent remaining uses.
                int progressQuarterUnits = ready
                    ? remainingCharges * HighSpeedVehicleKickStreakQuarterUnitsPerSegment
                    : Math.Max(
                        0,
                        Math.Min(
                            HighSpeedVehicleKickStreakQuarterUnitsRequired,
                            highSpeedVehicleKickStreakQuarterUnits));
                float progress = progressQuarterUnits * 0.25f;

                int backgroundRed = ready ? 0 : 255;
                int backgroundGreen = ready || progress == 0 ? 255 : 0;
                int backgroundBlue = ready || progress == 0 ? 255 : 0;

                Function.Call(
                    Hash.DRAW_SPRITE,
                    "timerbars",
                    "all_black_bg",
                    SpecialKickHudCenterX,
                    SpecialKickHudCenterY,
                    SpecialKickHudBackgroundWidth,
                    SpecialKickHudBackgroundHeight,
                    0.0f,
                    255,
                    255,
                    255,
                    150);

                Function.Call(
                    Hash.DRAW_SPRITE,
                    "timerbars",
                    "all_white_bg",
                    SpecialKickHudCenterX,
                    SpecialKickHudCenterY,
                    SpecialKickHudBackgroundWidth,
                    SpecialKickHudBackgroundHeight,
                    0.0f,
                    backgroundRed,
                    backgroundGreen,
                    backgroundBlue,
                    150);

                Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
                Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, "SPECIAL KICK");
                Function.Call(Hash.SET_TEXT_FONT, 0);
                Function.Call(Hash.SET_TEXT_SCALE, 0.0f, 0.3f);
                // Nhãn SPECIAL KICK luôn trắng, độc lập với trạng thái tiến trình.
                Function.Call(
                    Hash.SET_TEXT_COLOUR,
                    255,
                    255,
                    255,
                    255);
                Function.Call(Hash.SET_TEXT_OUTLINE);
                Function.Call(
                    Hash.END_TEXT_COMMAND_DISPLAY_TEXT,
                    SpecialKickHudTextX,
                    SpecialKickHudTextY,
                    0);

                const float usableBarWidth = SpecialKickHudBarWidth;
                float segmentWidth =
                    (usableBarWidth - SpecialKickHudSegmentGap * (HighSpeedVehicleKickStreakRequired - 1)) /
                    HighSpeedVehicleKickStreakRequired;

                for (int i = 0; i < HighSpeedVehicleKickStreakRequired; i++)
                {
                    float segmentLeft = SpecialKickHudBarLeft +
                        i * (segmentWidth + SpecialKickHudSegmentGap);
                    float segmentX = segmentLeft + segmentWidth * 0.5f;

                    // Draw the complete gray segment first.
                    Function.Call(
                        Hash.DRAW_SPRITE,
                        "timerbars",
                        "damagebarfill_128",
                        segmentX,
                        SpecialKickHudCenterY,
                        segmentWidth,
                        SpecialKickHudBarHeight,
                        0.0f,
                        85,
                        85,
                        85,
                        255);

                    // Each HUD segment fills continuously, including its left half.
                    float segmentProgress = Math.Max(
                        0.0f,
                        Math.Min(1.0f, progress - i));

                    if (segmentProgress > 0.0f)
                    {
                        float fillWidth = segmentWidth * segmentProgress;
                        float fillX = segmentLeft + fillWidth * 0.5f;

                        Function.Call(
                            Hash.DRAW_SPRITE,
                            "timerbars",
                            "damagebarfill_128",
                            fillX,
                            SpecialKickHudCenterY,
                            fillWidth,
                            SpecialKickHudBarHeight,
                            0.0f,
                            ready ? 0 : 255,
                            ready ? 110 : 0,
                            ready ? 255 : 0,
                            255);
                    }
                }
            }
            catch
            {
            }
        }

        // Single-piece progress bar styled and positioned like the Kortz Loot Bag HUD.
        // Yellow = charging; green = active/draining. It never uses Special Kick's 5 segments.
        private void DrawKickBuffHud(Ped player, bool visible)
        {
            if (!visible)
                return;

            try
            {
                float progress = Math.Max(0.0f, Math.Min(1.0f, kickBuffProgress));
                float fillWidth = KickBuffHudBarWidth * progress;
                float fillX = KickBuffHudBarLeft + fillWidth * 0.5f;
                bool active = IsKickBuffActive();
                bool hothandsActive = active &&
                    !kickBuffFlashActive &&
                    IsKickBuffExplosiveMeleeActive();

                // The empty Buff HUD starts white, matching Special Kick.
                // Charging is yellow; the initial completion flash remains green.
                // Cyan identifies the complete calculated Hothands window;
                // the remaining drain returns to green.
                int red = 255;
                int green = 255;
                int blue = 255;

                if (hothandsActive)
                {
                    red = 0;
                    green = 255;
                    blue = 255;
                }
                else if (active)
                {
                    red = 0;
                    green = 255;
                    blue = 0;
                }
                else if (progress > 0.0f)
                {
                    red = 255;
                    green = 215;
                    blue = 0;
                }

                int fillAlpha = active && kickBuffFlashActive
                    ? kickBuffFlashAlpha
                    : 255;

                Function.Call(
                    Hash.DRAW_SPRITE,
                    "timerbars",
                    "all_black_bg",
                    KickBuffHudCenterX,
                    KickBuffHudCenterY,
                    KickBuffHudBackgroundWidth,
                    KickBuffHudBackgroundHeight,
                    0.0f,
                    255,
                    255,
                    255,
                    150);

                Function.Call(
                    Hash.DRAW_SPRITE,
                    "timerbars",
                    "all_white_bg",
                    KickBuffHudCenterX,
                    KickBuffHudCenterY,
                    KickBuffHudBackgroundWidth,
                    KickBuffHudBackgroundHeight,
                    0.0f,
                    red,
                    green,
                    blue,
                    150);

                Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
                Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, "BUFF");
                Function.Call(Hash.SET_TEXT_FONT, 0);
                Function.Call(Hash.SET_TEXT_SCALE, 0.0f, 0.3f);
                Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 255, 255);
                Function.Call(Hash.SET_TEXT_OUTLINE);
                Function.Call(
                    Hash.END_TEXT_COMMAND_DISPLAY_TEXT,
                    KickBuffHudTextX,
                    KickBuffHudTextY,
                    0);

                // Empty gray base, same width and height as Loot Bag's continuous bar.
                Function.Call(
                    Hash.DRAW_SPRITE,
                    "timerbars",
                    "damagebarfill_128",
                    KickBuffHudBarLeft + KickBuffHudBarWidth * 0.5f,
                    KickBuffHudCenterY,
                    KickBuffHudBarWidth,
                    KickBuffHudBarHeight,
                    0.0f,
                    180,
                    180,
                    180,
                    255);

                if (fillWidth > 0.0f)
                {
                    Function.Call(
                        Hash.DRAW_SPRITE,
                        "timerbars",
                        "damagebarfill_128",
                        fillX,
                        KickBuffHudCenterY,
                        fillWidth,
                        KickBuffHudBarHeight,
                        0.0f,
                        red,
                        green,
                        blue,
                        fillAlpha);
                }
            }
            catch
            {
            }
        }

        // Render both bars from their current state on every available frame.
        // This keeps progress changes live while the attack sequence is yielding.
        private void DrawKickProgressHuds(Ped player)
        {
            if (player == null || !player.Exists())
                return;

            bool visible = false;

            try
            {
                if (player.IsInVehicle())
                {
                    Vehicle currentVehicle = player.CurrentVehicle;
                    visible = currentVehicle != null &&
                              currentVehicle.Exists() &&
                              IsMotorcycleOrCycle(currentVehicle);
                }
            }
            catch
            {
                visible = false;
            }

            if (!visible)
                return;

            try
            {
                if (!specialKickHudTextureRequested)
                {
                    Function.Call(
                        Hash.REQUEST_STREAMED_TEXTURE_DICT,
                        "timerbars",
                        false);

                    specialKickHudTextureRequested = true;
                }

                if (!specialKickHudTextureLoaded)
                {
                    if (!Function.Call<bool>(
                        Hash.HAS_STREAMED_TEXTURE_DICT_LOADED,
                        "timerbars"))
                    {
                        return;
                    }

                    // The dictionary is requested once and never released by this
                    // script, so do not ask the streaming system every HUD frame.
                    specialKickHudTextureLoaded = true;
                }
            }
            catch
            {
                return;
            }

            DrawSpecialKickHud(player, true);
            DrawKickBuffHud(player, true);
        }

        // Xóa hoàn toàn tiến trình. Chỉ dùng khi cú Special Kick đã được tiêu thụ
        // hoặc khi script cần dọn trạng thái, không dùng cho một lần rời/ngã xe.
        private void ResetHighSpeedVehicleKickStreak()
        {
            highSpeedVehicleKickStreakQuarterUnits = 0;
            highSpeedKickStreakVehicleHandle = 0;
            highSpeedSpecialKickReady = false;
            highSpeedSpecialKickChargesRemaining = 0;
        }

        // During accumulation, a dismount/fall/vehicle change randomly removes
        // 0.25, 0.5, 0.75 or 1.0 HUD segment. Quarter-unit storage renders each
        // result accurately without changing the existing HUD layout.
        // Stored Special Kick charges are protected by the guard inside this method.
        private void ApplyRandomHighSpeedVehicleKickStreakPenalty()
        {
            // A completed charge is a bank of five uses, not a ride streak.
            // Never remove a stored Special Kick because of a dismount/fall/swap.
            if (highSpeedSpecialKickReady &&
                highSpeedSpecialKickChargesRemaining > 0)
            {
                return;
            }

            if (highSpeedVehicleKickStreakQuarterUnits <= 0)
            {
                ResetHighSpeedVehicleKickStreak();
                return;
            }

            // One uniform RNG roll selects 1..4 quarter-units (0.25..1.0 segment).
            int penaltyQuarterUnits = specialKickPenaltyRandom.Next(1, 5);

            highSpeedVehicleKickStreakQuarterUnits = Math.Max(
                0,
                highSpeedVehicleKickStreakQuarterUnits - penaltyQuarterUnits);

            // Any accumulation penalty removes the uninterrupted 5/5 readiness.
            highSpeedSpecialKickReady = false;

            // Clamp overshoot to zero, then use the existing reset path so the next
            // successful qualifying hit starts a fresh accumulation cycle.
            if (highSpeedVehicleKickStreakQuarterUnits <= 0)
                ResetHighSpeedVehicleKickStreak();
        }

        // Ghi nhận một lần gián đoạn ride session và tháo liên kết với xe cũ.
        // Handle = 0 ngăn việc trừ lặp ở mọi Tick khi người chơi vẫn đang đi bộ.
        private void PenalizeAndDetachHighSpeedVehicleKickStreak()
        {
            if (highSpeedKickStreakVehicleHandle == 0)
                return;

            ApplyRandomHighSpeedVehicleKickStreakPenalty();
            highSpeedKickStreakVehicleHandle = 0;
        }

        // The incomplete accumulation streak is tied to a ride session. Once the
        // bar is fully charged, the five stored uses are independent of the ride.
        private void UpdateHighSpeedVehicleKickStreakState(Ped player)
        {
            // Stored charges are intentionally ride-independent. Do not run the
            // dismount/vehicle-change penalty path until all five uses are spent.
            if (highSpeedSpecialKickReady &&
                highSpeedSpecialKickChargesRemaining > 0)
            {
                return;
            }

            if (highSpeedVehicleKickStreakQuarterUnits <= 0 &&
                !highSpeedSpecialKickReady)
            {
                return;
            }

            if (player == null || !player.Exists() || !player.IsInVehicle())
            {
                PenalizeAndDetachHighSpeedVehicleKickStreak();
                return;
            }

            Vehicle currentVehicle = null;

            try
            {
                currentVehicle = player.CurrentVehicle;
            }
            catch
            {
            }

            if (currentVehicle == null ||
                !currentVehicle.Exists() ||
                !IsMotorcycleOrCycle(currentVehicle) ||
                !IsPlayerDrivingVehicle(player, currentVehicle))
            {
                PenalizeAndDetachHighSpeedVehicleKickStreak();
                return;
            }

            // Sau khi đã bị trừ nấc và tháo liên kết, lần đầu quay lại xe chỉ
            // gắn chuỗi với xe mới, không tiếp tục trừ thêm.
            if (highSpeedKickStreakVehicleHandle == 0)
            {
                highSpeedKickStreakVehicleHandle = currentVehicle.Handle;
                return;
            }

            if (highSpeedKickStreakVehicleHandle != currentVehicle.Handle)
            {
                ApplyRandomHighSpeedVehicleKickStreakPenalty();

                if (highSpeedVehicleKickStreakQuarterUnits > 0)
                    highSpeedKickStreakVehicleHandle = currentVehicle.Handle;
            }
        }

        // A direct vehicle hit at >= 90 km/h contributes one full HUD segment.
        private void RegisterHighSpeedVehicleKick(Ped player, bool isSpecialKick)
        {
            RegisterHighSpeedKickProgress(
                player,
                isSpecialKick,
                HighSpeedVehicleKickStreakThreshold,
                HighSpeedVehicleKickStreakVehicleHitQuarterUnits,
                float.MaxValue,
                0);
        }

        // On-foot NPCs award 0.25 segment at >=90 and <110 km/h, and retain
        // the existing 0.5 segment at >=110 km/h. NPC riders use the vehicle branch.
        private void RegisterHighSpeedNpcKick(Ped player, bool isSpecialKick)
        {
            RegisterHighSpeedKickProgress(
                player,
                isSpecialKick,
                HighSpeedKickThreshold,
                HighSpeedVehicleKickStreakPedHitAt90QuarterUnits,
                HighSpeedKickPenaltyThreshold,
                HighSpeedVehicleKickStreakPedHitAt110QuarterUnits);
        }

        // Shared ride-session validation and capped progress accumulation.
        // Both speed thresholds are inclusive: exactly 90/110 km/h qualifies.
        private void RegisterHighSpeedKickProgress(
            Ped player,
            bool isSpecialKick,
            float speedThreshold,
            int addedQuarterUnits,
            float increasedRewardSpeedThreshold,
            int increasedRewardQuarterUnits)
        {
            if (isSpecialKick ||
                highSpeedSpecialKickChargesRemaining > 0 ||
                highSpeedSpecialKickReady ||
                player == null || !player.Exists() ||
                !player.IsInVehicle())
            {
                return;
            }

            Vehicle playerVehicle = null;

            try
            {
                playerVehicle = player.CurrentVehicle;
            }
            catch
            {
            }

            if (playerVehicle == null ||
                !playerVehicle.Exists() ||
                !IsMotorcycleOrCycle(playerVehicle) ||
                !IsPlayerDrivingVehicle(player, playerVehicle))
            {
                return;
            }

            float playerVehicleSpeed = Math.Abs(playerVehicle.Speed);
            if (playerVehicleSpeed < speedThreshold)
                return;

            if (increasedRewardQuarterUnits > 0 &&
                playerVehicleSpeed >= increasedRewardSpeedThreshold)
            {
                addedQuarterUnits = increasedRewardQuarterUnits;
            }

            if (highSpeedKickStreakVehicleHandle != 0 &&
                highSpeedKickStreakVehicleHandle != playerVehicle.Handle)
            {
                ApplyRandomHighSpeedVehicleKickStreakPenalty();

                if (highSpeedVehicleKickStreakQuarterUnits > 0)
                    highSpeedKickStreakVehicleHandle = playerVehicle.Handle;
            }

            if (highSpeedSpecialKickReady)
                return;

            highSpeedKickStreakVehicleHandle = playerVehicle.Handle;
            highSpeedVehicleKickStreakQuarterUnits = Math.Min(
                HighSpeedVehicleKickStreakQuarterUnitsRequired,
                highSpeedVehicleKickStreakQuarterUnits + addedQuarterUnits);

            if (highSpeedVehicleKickStreakQuarterUnits >=
                HighSpeedVehicleKickStreakQuarterUnitsRequired)
            {
                highSpeedVehicleKickStreakQuarterUnits =
                    HighSpeedVehicleKickStreakQuarterUnitsRequired;
                highSpeedSpecialKickChargesRemaining =
                    HighSpeedSpecialKickChargesPerFullCharge;
                highSpeedSpecialKickReady = true;
            }

            DrawKickProgressHuds(player);
        }

        // Cú đánh hợp lệ ở tốc độ > 50 km/h được đưa vào chu kỳ thành tích 40 giây.
        // Có thể là NPC đi bộ, NPC đang đi xe hoặc một phương tiện trống.
        private void RegisterKickRewardHit(Ped player)
        {
            if (player == null || !player.Exists() || !player.IsInVehicle())
                return;

            Vehicle playerVehicle = null;

            try
            {
                playerVehicle = player.CurrentVehicle;
            }
            catch
            {
            }

            if (playerVehicle == null ||
                !playerVehicle.Exists() ||
                !IsMotorcycleOrCycle(playerVehicle) ||
                !IsPlayerDrivingVehicle(player, playerVehicle) ||
                Math.Abs(playerVehicle.Speed) <= KickRewardHitSpeedThreshold)
            {
                return;
            }

            UpdateKickRewardCycle();

            // Trong 30 giây nhận phần thưởng, không tích lũy cho chu kỳ kế tiếp.
            if (kickRewardEndTime >= 0)
                return;

            if (kickRewardWindowEndTime < 0)
            {
                kickRewardHitCount = 0;
                kickRewardWindowEndTime = GetGameDeadline(
                    KickRewardAccumulationDuration);
            }

            kickRewardHitCount++;
        }

        // Hết 40 giây mới xét mốc 8/15/30; sau đó khóa tích lũy 30 giây.
        // Kể cả khi chưa đạt cấp nào, chu kỳ vẫn có pha nghỉ 30 giây như yêu cầu.
        private void UpdateKickRewardCycle()
        {
            if (kickRewardEndTime >= 0)
            {
                if (!IsGameTimeReached(kickRewardEndTime))
                    return;

                activeKickRewardLevel = 0;
                kickRewardHitCount = 0;
                kickRewardEndTime = -1;

                // Mở cửa sổ 40 giây kế tiếp ngay khi 30 giây hiệu lực kết thúc.
                kickRewardWindowEndTime = GetGameDeadline(
                    KickRewardAccumulationDuration);
                return;
            }

            if (kickRewardWindowEndTime < 0 ||
                !IsGameTimeReached(kickRewardWindowEndTime))
            {
                return;
            }

            if (kickRewardHitCount >= KickRewardTier3Hits)
                activeKickRewardLevel = 3;
            else if (kickRewardHitCount >= KickRewardTier2Hits)
                activeKickRewardLevel = 2;
            else if (kickRewardHitCount >= KickRewardTier1Hits)
                activeKickRewardLevel = 1;
            else
                activeKickRewardLevel = 0;

            kickRewardWindowEndTime = -1;
            kickRewardEndTime = GetGameDeadline(KickRewardActiveDuration);
        }

        private int GetActiveKickRewardLevel()
        {
            if (kickRewardEndTime < 0 || IsGameTimeReached(kickRewardEndTime))
                return 0;

            return activeKickRewardLevel;
        }

        private bool IsKickBuffActive()
        {
            return kickBuffActive && kickBuffProgress > 0.0f;
        }

        // Passenger/player attack desynchronization is limited to the actual
        // green-bar drain. The initial completion flash keeps the original co-op sync.
        private bool IsKickBuffDraining()
        {
            return kickBuffActive &&
                   kickBuffDrainStarted &&
                   kickBuffProgress > 0.0f;
        }

        // Hothands is available for the adaptive duration calculated when the
        // 150 weighted hits complete. Passenger attacks never call the impact helper.
        private bool IsKickBuffExplosiveMeleeActive()
        {
            if (!IsKickBuffDraining() || kickBuffDrainStartTime < 0)
                return false;

            int elapsed = unchecked(Game.GameTime - kickBuffDrainStartTime);
            return elapsed >= 0 && elapsed < kickBuffHothandsDuration;
        }

        // Snapshot and protect the player and the vehicle he is using at the
        // moment the Buff bar begins its adaptive-duration drain.
        private void BeginKickBuffTimedEffects()
        {
            kickBuffDrainStartTime = Game.GameTime;

            Ped player = Game.Player.Character;
            if (player != null && player.Exists())
            {
                try
                {
                    kickBuffProtectedPlayer = player;
                    kickBuffPlayerWasInvincible = player.IsInvincible;
                    kickBuffPlayerInvincibilityCaptured = true;
                    player.IsInvincible = true;
                }
                catch
                {
                    kickBuffPlayerInvincibilityCaptured = false;
                    kickBuffProtectedPlayer = null;
                }

                if (player.IsInVehicle())
                {
                    try
                    {
                        Vehicle currentVehicle = player.CurrentVehicle;
                        if (currentVehicle != null && currentVehicle.Exists())
                        {
                            kickBuffProtectedVehicle = currentVehicle;
                            kickBuffVehicleWasInvincible = currentVehicle.IsInvincible;
                            kickBuffVehicleInvincibilityCaptured = true;
                            currentVehicle.IsInvincible = true;
                        }
                    }
                    catch
                    {
                        kickBuffVehicleInvincibilityCaptured = false;
                        kickBuffProtectedVehicle = null;
                    }
                }
            }

            // Schedule the first periodic protection refresh; the initial
            // invincibility assignments above have already taken effect.
            kickBuffNextProtectionRefreshTime = GetGameDeadline(
                KickBuffProtectionRefreshInterval);

            // Start the per-frame Hothands native immediately on the first drain frame.
            UpdateKickBuffTimedEffects();
        }

        // SET_EXPLOSIVE_MELEE_THIS_FRAME must be refreshed every game frame, just
        // like Pumpkin.cs refreshes explosive/fire ammo natives every frame.
        private void UpdateKickBuffTimedEffects()
        {
            if (!IsKickBuffDraining() || kickBuffDrainStartTime < 0)
            {
                RestoreKickBuffTemporaryInvincibility();
                return;
            }

            int elapsed = unchecked(Game.GameTime - kickBuffDrainStartTime);
            if (elapsed < 0)
                elapsed = 0;

            if (elapsed < kickBuffHothandsDuration)
            {
                try
                {
                    // HOTHANDS / SET_EXPLOSIVE_MELEE_THIS_FRAME.
                    // The argument is the Player index, not the Ped handle.
                    Function.Call((Hash)0xFF1BED81BFDC0FE0UL, Game.Player.Handle);
                }
                catch
                {
                    // Retry on the next frame while the adaptive Hothands window is active.
                }
            }

            if (elapsed < kickBuffInvincibilityDuration)
            {
                if (kickBuffNextProtectionRefreshTime < 0 ||
                    IsGameTimeReached(kickBuffNextProtectionRefreshTime))
                {
                    try
                    {
                        if (kickBuffPlayerInvincibilityCaptured &&
                            kickBuffProtectedPlayer != null &&
                            kickBuffProtectedPlayer.Exists())
                        {
                            kickBuffProtectedPlayer.IsInvincible = true;
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        if (kickBuffVehicleInvincibilityCaptured &&
                            kickBuffProtectedVehicle != null &&
                            kickBuffProtectedVehicle.Exists())
                        {
                            kickBuffProtectedVehicle.IsInvincible = true;
                        }
                    }
                    catch
                    {
                    }

                    kickBuffNextProtectionRefreshTime = GetGameDeadline(
                        KickBuffProtectionRefreshInterval);
                }
            }
            else
            {
                // Restore the exact pre-Buff state as soon as the calculated
                // protection window expires; the remaining Buff keeps running.
                RestoreKickBuffTemporaryInvincibility();
            }
        }

        private void RestoreKickBuffTemporaryInvincibility()
        {
            if (!kickBuffPlayerInvincibilityCaptured &&
                !kickBuffVehicleInvincibilityCaptured)
            {
                kickBuffNextProtectionRefreshTime = -1;
                return;
            }

            try
            {
                if (kickBuffPlayerInvincibilityCaptured &&
                    kickBuffProtectedPlayer != null &&
                    kickBuffProtectedPlayer.Exists())
                {
                    kickBuffProtectedPlayer.IsInvincible = kickBuffPlayerWasInvincible;
                }
            }
            catch
            {
            }

            try
            {
                if (kickBuffVehicleInvincibilityCaptured &&
                    kickBuffProtectedVehicle != null &&
                    kickBuffProtectedVehicle.Exists())
                {
                    kickBuffProtectedVehicle.IsInvincible = kickBuffVehicleWasInvincible;
                }
            }
            catch
            {
            }

            kickBuffProtectedPlayer = null;
            kickBuffPlayerWasInvincible = false;
            kickBuffPlayerInvincibilityCaptured = false;

            kickBuffProtectedVehicle = null;
            kickBuffVehicleWasInvincible = false;
            kickBuffVehicleInvincibilityCaptured = false;
            kickBuffNextProtectionRefreshTime = -1;
        }

        // The custom bike-kick contacts are resolved manually rather than solely
        // through GTA's default melee collision. Create one Hothands-style owned
        // explosion at the confirmed target, attributed to the player.
        private void ApplyKickBuffExplosiveMeleeImpact(Ped player, Vector3 position)
        {
            if (!IsKickBuffExplosiveMeleeActive() ||
                player == null ||
                !player.Exists())
            {
                return;
            }

            try
            {
                Function.Call(
                    Hash.ADD_OWNED_EXPLOSION,
                    player.Handle,
                    position.X,
                    position.Y,
                    position.Z,
                    KickBuffExplosiveImpactType,
                    KickBuffExplosiveImpactDamageScale,
                    true,
                    false,
                    KickBuffExplosiveImpactCameraShake);
            }
            catch
            {
                // A failed impact must not interrupt the existing kick logic.
            }
        }

        // Called only after the code has confirmed a real successful melee contact.
        // Hits at every speed count; leaving/falling off the bike never penalizes this bar.
        // A direct player kick while the recruited passenger is seated can pass 2.
        private void RegisterKickBuffHit(int successfulHitCount = 1)
        {
            UpdateKickBuffCycle();

            // Do not refill or extend the Buff while its active bar is draining.
            if (IsKickBuffActive())
                return;

            successfulHitCount = Math.Max(1, successfulHitCount);

            // Read the player's actual motorcycle/bicycle speed at the confirmed hit.
            // Below 90 km/h the original 1/2-point values remain unchanged.
            // At 90-<110 km/h use +7.5% per base hit-count unit; at >=110 use +18%.
            // Since the percentage itself scales with the 1/2 base count, this gives:
            // solo 1.075 / 1.18 points; two-person 2.3 / 2.72 points.
            float speedBonusPerHitCount = 0.0f;
            try
            {
                Ped buffPlayer = Game.Player.Character;
                if (buffPlayer != null && buffPlayer.Exists() && buffPlayer.IsInVehicle())
                {
                    Vehicle buffVehicle = buffPlayer.CurrentVehicle;
                    if (buffVehicle != null &&
                        buffVehicle.Exists() &&
                        IsMotorcycleOrCycle(buffVehicle) &&
                        IsPlayerDrivingVehicle(buffPlayer, buffVehicle))
                    {
                        float buffKickSpeed = Math.Abs(buffVehicle.Speed);
                        if (buffKickSpeed >= HighSpeedKickPenaltyThreshold)
                        {
                            speedBonusPerHitCount = KickBuffHighSpeedBonusPerHitCount;
                        }
                        else if (buffKickSpeed >= HighSpeedKickThreshold)
                        {
                            speedBonusPerHitCount = KickBuffMediumSpeedBonusPerHitCount;
                        }
                    }
                }
            }
            catch
            {
                // A speed-read failure falls back to the original unweighted hit value.
            }

            float bonusPercentage = speedBonusPerHitCount * successfulHitCount;
            double weightedSuccessfulHitCount =
                successfulHitCount * (1.0 + bonusPercentage);

            // Start the charge timer on the first confirmed hit of this Buff cycle.
            // If the 500-second limit was already exceeded, keep the fallback state.
            if (kickBuffAccumulationStartTime < 0 &&
                !kickBuffAccumulationLimitExceeded)
            {
                kickBuffAccumulationStartTime = Game.GameTime;
            }

            kickBuffProgress = (float)Math.Min(
                1.0,
                kickBuffProgress +
                    (weightedSuccessfulHitCount / KickBuffHitsRequired));

            // Avoid a fractional-float rounding residue preventing a mathematically
            // complete 150-point charge from entering the existing completion path.
            if (kickBuffProgress >= 0.999999f)
                kickBuffProgress = 1.0f;

            if (kickBuffProgress >= 1.0f)
            {
                kickBuffProgress = 1.0f;

                int accumulationElapsed = 0;
                bool useDefaultDuration = kickBuffAccumulationLimitExceeded;

                if (!useDefaultDuration && kickBuffAccumulationStartTime >= 0)
                {
                    accumulationElapsed = unchecked(
                        Game.GameTime - kickBuffAccumulationStartTime);

                    if (accumulationElapsed < 0)
                        accumulationElapsed = 0;

                    // Keep a defensive boundary check in case the completion hit
                    // arrives on the same frame the accumulation timer crosses 500s.
                    useDefaultDuration =
                        accumulationElapsed > KickBuffMaximumAccumulationDuration;
                }

                int accumulationBonusDuration;
                if (useDefaultDuration)
                {
                    // More than 500 seconds: fixed 25 seconds + 100-second fallback.
                    accumulationBonusDuration = KickBuffDefaultActiveDuration;
                }
                else
                {
                    accumulationBonusDuration = Math.Max(
                        0,
                        Math.Min(
                            KickBuffMaximumAccumulationBonusDuration,
                            (int)Math.Round(
                                accumulationElapsed * KickBuffAccumulationDrainRatio,
                                MidpointRounding.AwayFromZero)));
                }

                // Hothands has its own timer: 20 seconds + 8% of accumulation time.
                // Beyond 500 seconds, calculate from the existing 100-second fallback.
                int hothandsAccumulationDuration = useDefaultDuration
                    ? KickBuffDefaultActiveDuration
                    : accumulationElapsed;

                int hothandsBonusDuration = Math.Max(
                    0,
                    (int)Math.Round(
                        hothandsAccumulationDuration * KickBuffHothandsAccumulationRatio,
                        MidpointRounding.AwayFromZero));

                kickBuffHothandsDuration =
                    KickBuffHothandsBaseDuration + hothandsBonusDuration;

                // Invincibility has a separate timer: 23 seconds + 9% of accumulation time.
                // Round the final duration to the nearest whole second. The 100-second
                // fallback therefore produces 23 + 9 = 32 seconds.
                int invincibilityAccumulationDuration = useDefaultDuration
                    ? KickBuffDefaultActiveDuration
                    : accumulationElapsed;

                int invincibilityBonusSeconds = Math.Max(
                    0,
                    (int)Math.Round(
                        (invincibilityAccumulationDuration / 1000.0) *
                        KickBuffInvincibilityAccumulationRatio,
                        MidpointRounding.AwayFromZero));

                kickBuffInvincibilityDuration =
                    KickBuffInvincibilityBaseDuration +
                    (invincibilityBonusSeconds * 1000);

                // Total drain duration = unchanged fixed 25-second base + adaptive/fallback bonus.
                kickBuffActiveDuration =
                    KickBuffBaseActiveDuration + accumulationBonusDuration;

                kickBuffAccumulationStartTime = -1;
                kickBuffAccumulationLimitExceeded = false;
                kickBuffActive = true;
                kickBuffDrainStarted = false;
                kickBuffLastUpdateTime = -1;
                kickBuffDrainStartTime = -1;
                RestoreKickBuffTemporaryInvincibility();

                // Same flash rhythm as the Loot Bag HUD: 5 green flashes, 400 ms steps.
                // The bar stays full during this flash; draining begins only afterward.
                kickBuffFlashActive = true;
                kickBuffFlashCount = 0;
                kickBuffFlashAlpha = 0;
                kickBuffFlashLastTime = Game.GameTime;
            }

            DrawKickProgressHuds(Game.Player.Character);
        }

        // Smoothly drains the Buff bar over the entire active duration.
        // This method is called independently of the current riding state.
        private void UpdateKickBuffCycle()
        {
            if (!kickBuffActive)
            {
                // Stop the elapsed-time counter once charging goes beyond 500s.
                // The hit progress can continue normally; only the fallback duration
                // state is retained until the 150 weighted hits are completed.
                UpdateKickBuffAccumulationTimer();

                RestoreKickBuffTemporaryInvincibility();
                kickBuffDrainStartTime = -1;
                UpdateKickBuffHelp();
                return;
            }

            // First finish the Loot Bag-style green flash while the bar remains full.
            // The adaptive smooth drain begins only after the flash sequence completes.
            if (!kickBuffDrainStarted)
            {
                UpdateKickBuffFlash();
                if (kickBuffDrainStarted)
                    UpdateKickBuffTimedEffects();

                UpdateKickBuffHelp();
                return;
            }

            // Refresh the Hothands native each frame and enforce the calculated
            // player/vehicle protection window independently of attack input.
            UpdateKickBuffTimedEffects();

            int now = Game.GameTime;

            if (kickBuffLastUpdateTime < 0)
            {
                kickBuffLastUpdateTime = now;
            }
            else
            {
                int elapsed = unchecked(now - kickBuffLastUpdateTime);

                if (elapsed > 0)
                {
                    kickBuffProgress -= elapsed / (float)Math.Max(
                        1,
                        kickBuffActiveDuration);
                    kickBuffLastUpdateTime = now;
                }
            }

            if (kickBuffProgress <= 0.0f)
            {
                kickBuffProgress = 0.0f;
                kickBuffActive = false;
                kickBuffDrainStarted = false;
                kickBuffLastUpdateTime = -1;
                kickBuffFlashActive = false;
                kickBuffFlashCount = 0;
                kickBuffFlashAlpha = 0;
                kickBuffFlashLastTime = -1;
                RestoreKickBuffTemporaryInvincibility();
                kickBuffDrainStartTime = -1;
                kickBuffActiveDuration = KickBuffDefaultTotalActiveDuration;
                kickBuffHothandsDuration = KickBuffDefaultHothandsDuration;
                kickBuffInvincibilityDuration = KickBuffDefaultInvincibilityDuration;
                kickBuffAccumulationStartTime = -1;
                kickBuffAccumulationLimitExceeded = false;
            }

            UpdateKickBuffHelp();
        }

        // Stop measuring the accumulation duration as soon as it exceeds 500s.
        // This runs from the existing Buff update path and performs no extra scans.
        private void UpdateKickBuffAccumulationTimer()
        {
            if (kickBuffAccumulationLimitExceeded ||
                kickBuffAccumulationStartTime < 0 ||
                kickBuffProgress <= 0.0f)
            {
                return;
            }

            int elapsed = unchecked(
                Game.GameTime - kickBuffAccumulationStartTime);

            if (elapsed > KickBuffMaximumAccumulationDuration)
            {
                kickBuffAccumulationStartTime = -1;
                kickBuffAccumulationLimitExceeded = true;
            }
        }

        // Build the explanation once per Buff activation so the displayed values
        // always match the two independently calculated effect durations.
        private string BuildKickBuffHelpMessage()
        {
            string hothandsSeconds =
                (kickBuffHothandsDuration / 1000.0).ToString(
                    "0.#",
                    System.Globalization.CultureInfo.InvariantCulture);

            int invincibilitySeconds = kickBuffInvincibilityDuration / 1000;

            return "While Buff is active, Hothands explosive attacks last " +
                   hothandsSeconds + "s; you and current vehicle are invincible for " +
                   invincibilitySeconds + "s.";
        }

        // The explanation is shown continuously for ten seconds from the exact
        // frame on which the Buff bar begins draining.
        private void UpdateKickBuffHelp()
        {
            if (kickBuffHelpEndTime < 0)
                return;

            if (IsGameTimeReached(kickBuffHelpEndTime))
            {
                kickBuffHelpEndTime = -1;
                kickBuffHelpMessage = null;
                return;
            }

            if (string.IsNullOrEmpty(kickBuffHelpMessage))
                kickBuffHelpMessage = BuildKickBuffHelpMessage();

            GTA.UI.Screen.ShowHelpTextThisFrame(kickBuffHelpMessage);
        }

        private void UpdateKickBuffFlash()
        {
            if (!kickBuffFlashActive)
                return;

            int now = Game.GameTime;

            if (kickBuffFlashLastTime < 0)
            {
                kickBuffFlashLastTime = now;
                return;
            }

            if (unchecked(now - kickBuffFlashLastTime) < KickBuffFlashIntervalMs)
                return;

            kickBuffFlashAlpha = kickBuffFlashAlpha == 0 ? 255 : 0;
            kickBuffFlashLastTime = now;

            if (kickBuffFlashAlpha == 255)
            {
                kickBuffFlashCount++;

                if (kickBuffFlashCount >= KickBuffFlashMax)
                {
                    kickBuffFlashActive = false;
                    kickBuffFlashAlpha = 255;
                    kickBuffDrainStarted = true;
                    kickBuffLastUpdateTime = now;
                    kickBuffHelpMessage = BuildKickBuffHelpMessage();
                    kickBuffHelpEndTime = GetGameDeadline(KickBuffHelpDuration);
                    BeginKickBuffTimedEffects();
                }
            }
        }

        private int GetCurrentHighSpeedSpecialKickTier()
        {
            if (!highSpeedSpecialKickReady ||
                highSpeedSpecialKickChargesRemaining <= 0)
            {
                return 1;
            }

            // 5 remaining = tier 1; 1 remaining = tier 5.
            return Math.Max(
                1,
                Math.Min(
                    HighSpeedSpecialKickChargesPerFullCharge,
                    HighSpeedSpecialKickChargesPerFullCharge -
                    highSpeedSpecialKickChargesRemaining + 1));
        }

        private void CompleteHighSpeedSpecialKick()
        {
            if (!highSpeedSpecialKickReady ||
                highSpeedSpecialKickChargesRemaining <= 0)
            {
                return;
            }

            // Consume a charge only after the kick has confirmed a target hit.
            highSpeedSpecialKickChargesRemaining = Math.Max(
                0,
                highSpeedSpecialKickChargesRemaining - 1);

            if (highSpeedSpecialKickChargesRemaining <= 0)
            {
                // Begin a fresh accumulation cycle only after all five uses are spent.
                ResetHighSpeedVehicleKickStreak();
            }
            else
            {
                // Keep the ready state and remaining uses across dismounts.
                highSpeedSpecialKickReady = true;
                DrawKickProgressHuds(Game.Player.Character);
            }
        }

        // Ghi nhận mỗi lần người chơi đá trúng một mục tiêu.
        // Bao gồm NPC và phương tiện.
        private void RegisterDisturbance()
        {
            // Bắt đầu một đợt quấy rầy mới nếu đợt cũ đã hết hạn.
            if (disturbanceWindowEndTime < 0 ||
                IsGameTimeReached(disturbanceWindowEndTime))
            {
                disturbanceHitCount = 0;

                disturbanceWindowEndTime =
                    GetGameDeadline(DisturbanceWindowDuration);
            }

            disturbanceHitCount++;

            if (disturbanceHitCount < DisturbanceRequiredHits)
                return;

            // Đã đủ số lần quấy rầy.
            // Giữ bộ đếm ở ngưỡng trong thời gian cooldown,
            // tránh mất lượt khi người chơi tiếp tục quấy rầy.
            if (Game.Player.WantedLevel >= MaxWantedLevel)
            {
                disturbanceHitCount = 0;
                disturbanceWindowEndTime = -1;
                return;
            }

            // Chỉ kiểm tra xác suất khi cooldown đã kết thúc.
            if (nextDisturbanceWantedTime != 0 &&
                !IsGameTimeReached(nextDisturbanceWantedTime))
            {
                return;
            }

            // Mỗi đợt chỉ được thử tăng sao một lần.
            disturbanceHitCount = 0;
            disturbanceWindowEndTime = -1;

            // Xác suất 50%.
            if (soundRandom.Next(100) >= DisturbanceWantedChance)
                return;

            int currentWantedLevel = Game.Player.WantedLevel;

            if (currentWantedLevel >= MaxWantedLevel)
                return;

            Game.Player.WantedLevel = Math.Min(
                currentWantedLevel + 1,
                MaxWantedLevel);

            // Chỉ bắt đầu cooldown khi thực sự tăng được sao.
            nextDisturbanceWantedTime =
                GetGameDeadline(DisturbanceWantedCooldown);
        }

        private void LoadBikeHitSounds()
        {
            int soundCount = BikeHitSoundNames.Length;

            bikeHitSounds = new SoundPlayer[soundCount];
            bikeHitSoundStreams = new Stream[soundCount];

            var assembly = typeof(BikerMelee).Assembly;
            string[] resourceNames = assembly.GetManifestResourceNames();

            int[] loadedIndices = new int[soundCount];
            int loadedCount = 0;

            // Tìm resource một lần thay vì duyệt toàn bộ danh sách
            // riêng biệt cho từng âm thanh.
            string[] matchedResources = new string[soundCount];

            for (int i = 0; i < resourceNames.Length; i++)
            {
                string resourceName = resourceNames[i];

                for (int j = 0; j < soundCount; j++)
                {
                    if (matchedResources[j] != null)
                        continue;

                    string suffix = ".Audio." + BikeHitSoundNames[j];

                    if (resourceName.EndsWith(
                        suffix,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        matchedResources[j] = resourceName;
                    }
                }
            }

            for (int i = 0; i < soundCount; i++)
            {
                try
                {
                    if (matchedResources[i] == null)
                        continue;

                    Stream soundStream =
                        assembly.GetManifestResourceStream(matchedResources[i]);

                    if (soundStream == null)
                        continue;

                    SoundPlayer soundPlayer = new SoundPlayer(soundStream);

                    soundPlayer.Load();

                    bikeHitSoundStreams[i] = soundStream;
                    bikeHitSounds[i] = soundPlayer;

                    loadedIndices[loadedCount++] = i;
                }
                catch
                {
                    if (bikeHitSounds[i] != null)
                    {
                        bikeHitSounds[i].Dispose();
                        bikeHitSounds[i] = null;
                    }

                    if (bikeHitSoundStreams[i] != null)
                    {
                        bikeHitSoundStreams[i].Dispose();
                        bikeHitSoundStreams[i] = null;
                    }
                }
            }

            Array.Resize(ref loadedIndices, loadedCount);
            availableSoundIndices = loadedIndices;
        }

        private void PlayRandomBikeHitSound()
        {
            if (availableSoundIndices == null ||
                availableSoundIndices.Length == 0)
            {
                return;
            }

            int selectedIndex = availableSoundIndices[
                soundRandom.Next(availableSoundIndices.Length)];

            if (currentPlayingSound != null)
                currentPlayingSound.Stop();

            currentPlayingSound = bikeHitSounds[selectedIndex];

            if (currentPlayingSound != null)
                currentPlayingSound.Play();
        }

        private void PlayVehicleKickSound()
        {
            if (bikeHitSounds == null ||
                bikeHitSounds.Length <= 2 ||
                bikeHitSounds[2] == null)
            {
                return;
            }

            if (currentPlayingSound != null)
                currentPlayingSound.Stop();

            currentPlayingSound = bikeHitSounds[2];

            currentPlayingSound.Play();
        }

        private static bool IsDisabledControlPressed(int controlGroup, Control control)
        {
            return Function.Call<bool>(Hash.IS_DISABLED_CONTROL_PRESSED, controlGroup, (int)control);
        }

        private static bool IsDisabledControlJustPressed(int controlGroup, Control control)
        {
            return Function.Call<bool>(Hash.IS_DISABLED_CONTROL_JUST_PRESSED, controlGroup, (int)control);
        }

        // Chỉ áp dụng bonus sát thương nhận vào khi người chơi thực sự lái xe đạp.
        // Vehicle Class 13 = Cycles; class 8 (Motorcycles) không được áp dụng.
        private static bool IsPlayerDrivingBicycle(Ped player)
        {
            if (player == null || !player.Exists() || !player.IsInVehicle())
                return false;

            try
            {
                Vehicle vehicle = player.CurrentVehicle;

                return vehicle != null &&
                       vehicle.Exists() &&
                       GetVehicleClassCached(vehicle) == 13 &&
                       IsPlayerDrivingVehicle(player, vehicle);
            }
            catch
            {
                return false;
            }
        }

        // Chỉ nhận diện theo Vehicle Class: 8 = Motorcycles, 13 = Cycles.
        private static bool IsMotorcycleOrCycle(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists())
                return false;

            try
            {
                int vehicleClass = GetVehicleClassCached(vehicle);

                return vehicleClass == 8 || vehicleClass == 13;
            }
            catch
            {
                return false;
            }
        }

        // GET_VEHICLE_MODEL_NUMBER_OF_SEATS tính cả ghế tài xế.
        private static bool HasAtLeastTwoSeats(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists())
                return false;

            try
            {
                int seatCount = GetVehicleSeatCountCached(vehicle);

                return seatCount >= 2;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPlayerDrivingVehicle(Ped player, Vehicle vehicle)
        {
            if (player == null || !player.Exists() ||
                vehicle == null || !vehicle.Exists())
            {
                return false;
            }

            try
            {
                Ped driver = vehicle.GetPedOnSeat(VehicleSeat.Driver);

                return driver != null &&
                       driver.Exists() &&
                       driver.Handle == player.Handle;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPedDrivingVehicle(Ped ped, Vehicle vehicle)
        {
            if (ped == null || !ped.Exists() ||
                vehicle == null || !vehicle.Exists())
            {
                return false;
            }

            try
            {
                Ped driver = vehicle.GetPedOnSeat(VehicleSeat.Driver);

                return driver != null &&
                       driver.Exists() &&
                       driver.Handle == ped.Handle;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsDisabledControlJustReleased(int controlGroup, Control control)
        {
            return Function.Call<bool>(Hash.IS_DISABLED_CONTROL_JUST_RELEASED, controlGroup, (int)control);
        }

        private void PrepareWeaponProp(Ped player, bool leftHand)
        {
            CleanupWeaponProp();

            if (animWeapType != "melee_small_wpn" &&
                animWeapType != "melee_1h")
            {
                return;
            }

            // Keep bikeMeleeWpnModel from the pre-entry weapon hash. Do not read
            // CurrentWeaponObject here: on a bike it may be GTA's forced firearm.
            // The selected hash/model was already resolved in UpdateBikeWeaponSelection.
            if (!bikeMeleeWpnModel.IsValid)
                return;

            bikeMeleeWpnProp = World.CreateProp(bikeMeleeWpnModel, player.Position, false, false);

            if (bikeMeleeWpnProp == null || !bikeMeleeWpnProp.Exists())
                return;

            int boneId = leftHand ? 18905 : 57005;

            int boneIndex = Function.Call<int>(
                Hash.GET_PED_BONE_INDEX,
                player,
                boneId);

            bikeMeleeWpnProp.AttachTo(player.Bones[boneIndex],
                new Vector3(0.15f, 0.0f, 0.0f),
                new Vector3(-60f, 0.0f, 0.0f));
        }

        // Tạo bản sao vũ khí trên tay NPC ghế phụ để đồng bộ cả đòn đánh có vũ khí.
        private void PreparePassengerWeaponProp(Ped passenger, bool leftHand)
        {
            CleanupPassengerWeaponProp();

            if (passenger == null || !passenger.Exists() ||
                (animWeapType != "melee_small_wpn" &&
                 animWeapType != "melee_1h") ||
                !bikeMeleeWpnModel.IsValid)
            {
                return;
            }

            passengerMeleeWpnProp = World.CreateProp(
                bikeMeleeWpnModel,
                passenger.Position,
                false,
                false);

            if (passengerMeleeWpnProp == null ||
                !passengerMeleeWpnProp.Exists())
            {
                passengerMeleeWpnProp = null;
                return;
            }

            int boneId = leftHand ? 18905 : 57005;
            int boneIndex = Function.Call<int>(
                Hash.GET_PED_BONE_INDEX,
                passenger,
                boneId);

            passengerMeleeWpnProp.AttachTo(
                passenger.Bones[boneIndex],
                new Vector3(0.15f, 0.0f, 0.0f),
                new Vector3(-60f, 0.0f, 0.0f));
        }

        private void CleanupPassengerWeaponProp()
        {
            Prop prop = passengerMeleeWpnProp;

            if (prop == null)
                return;

            passengerMeleeWpnProp = null;

            if (prop.Exists())
                prop.Delete();
        }

        private Vehicle FindNearbyTargetVehicle(Ped player, Vector3 attackPos)
        {
            if (player == null || !player.Exists())
                return null;

            int currentVehicleHandle = 0;

            if (player.IsInVehicle())
            {
                Vehicle currentVehicle = player.CurrentVehicle;

                if (currentVehicle != null && currentVehicle.Exists())
                    currentVehicleHandle = currentVehicle.Handle;
            }

            Vehicle[] vehicles = World.GetNearbyVehicles(player, 5f);

            if (vehicles == null || vehicles.Length == 0)
                return null;

            Vehicle bestVehicle = null;
            float bestDistance = float.MaxValue;

            const float margin = 0.35f;

            foreach (Vehicle vehicle in vehicles)
            {
                if (vehicle == null || !vehicle.Exists())
                    continue;

                int handle = vehicle.Handle;

                if (currentVehicleHandle != 0 &&
                    handle == currentVehicleHandle)
                {
                    continue;
                }

                if (vehicle.IsDead)
                    continue;

                Vector3 vehiclePosition = vehicle.Position;
                Vector3 difference = attackPos - vehiclePosition;

                Vector3 right = vehicle.RightVector;
                Vector3 forward = vehicle.ForwardVector;
                Vector3 up = vehicle.UpVector;

                float localX = Vector3.Dot(difference, right);
                float localY = Vector3.Dot(difference, forward);
                float localZ = Vector3.Dot(difference, up);

                Vector3 min;
                Vector3 max;

                vehicle.Model.GetDimensions(out min, out max);

                if (localX < min.X - margin ||
                    localX > max.X + margin ||
                    localY < min.Y - margin ||
                    localY > max.Y + margin ||
                    localZ < min.Z - margin ||
                    localZ > max.Z + margin)
                {
                    continue;
                }

                // Dùng khoảng cách bình phương để không phải
                // tính căn bậc hai cho từng phương tiện.
                float dx = attackPos.X - vehiclePosition.X;
                float dy = attackPos.Y - vehiclePosition.Y;
                float dz = attackPos.Z - vehiclePosition.Z;

                float distanceSquared =
                    dx * dx + dy * dy + dz * dz;

                if (distanceSquared < bestDistance)
                {
                    bestDistance = distanceSquared;
                    bestVehicle = vehicle;
                }
            }

            return bestVehicle;
        }

        // Xác định chân đá của NPC dựa trên phía người chơi đang đứng.
        // Đồng thời tái sử dụng đúng hình học tiếp xúc của cú đá hiện tại:
        // vị trí chân lệch 1m sang trái/phải và bán kính tiếp xúc 1m.
        private static bool TryGetNpcBikeKickContact(
            Ped attacker,
            Ped target,
            out float force,
            out float distanceSquared)
        {
            force = 0f;
            distanceSquared = float.MaxValue;

            if (attacker == null || !attacker.Exists() ||
                target == null || !target.Exists())
            {
                return false;
            }

            return TryGetNpcBikeKickContactCore(
                attacker,
                target,
                out force,
                out distanceSquared);
        }

        // Hot-loop variant: callers must have checked both entities immediately
        // before calling. This avoids repeating Exists() natives for every nearby Ped.
        private static bool TryGetNpcBikeKickContactCore(
            Ped attacker,
            Ped target,
            out float force,
            out float distanceSquared)
        {
            force = 0f;
            distanceSquared = float.MaxValue;

            Vector3 attackerPosition = attacker.Position;
            Vector3 targetPosition = target.Position;
            Vector3 attackerRight = attacker.RightVector;

            Vector3 difference = targetPosition - attackerPosition;
            float side = Vector3.Dot(difference, attackerRight);

            // Người chơi bên trái NPC: chân trái; bên phải: chân phải.
            force = side < 0f ? -15f : 15f;

            float sideOffset = force < 0f ? -1.0f : 1.0f;
            Vector3 attackPosition =
                attackerPosition + attackerRight * sideOffset;

            float dx = targetPosition.X - attackPosition.X;
            float dy = targetPosition.Y - attackPosition.Y;
            float dz = targetPosition.Z - attackPosition.Z;

            distanceSquared = dx * dx + dy * dy + dz * dz;

            return distanceSquared <=
                   NpcBikeKickContactRadius * NpcBikeKickContactRadius;
        }

        // Resolve a vehicle against either of the passenger's kick-foot positions.
        // Uses the same oriented model-bound test and 0.35 m margin as the player's
        // existing FindNearbyTargetVehicle method, but performs only one nearby scan.
        private Vehicle FindPassengerBuffKickTargetVehicle(
            Ped passenger,
            out float force)
        {
            force = 0f;

            if (passenger == null || !passenger.Exists())
                return null;

            int playerVehicleHandle = recruitedPassenger != null &&
                                      recruitedPassenger.Vehicle != null &&
                                      recruitedPassenger.Vehicle.Exists()
                ? recruitedPassenger.Vehicle.Handle
                : 0;
            int ownVehicleHandle = recruitedPassenger != null &&
                                   recruitedPassenger.IndependentVehicle != null &&
                                   recruitedPassenger.IndependentVehicle.Exists()
                ? recruitedPassenger.IndependentVehicle.Handle
                : 0;

            Vehicle[] vehicles = World.GetNearbyVehicles(passenger, 5f);
            if (vehicles == null || vehicles.Length == 0)
                return null;

            Vector3 attackerPosition = passenger.Position;
            Vector3 attackerRight = passenger.RightVector;
            Vehicle bestVehicle = null;
            float bestDistance = float.MaxValue;
            float bestForce = 0f;
            const float margin = 0.35f;

            foreach (Vehicle vehicle in vehicles)
            {
                if (vehicle == null || !vehicle.Exists() || vehicle.IsDead ||
                    (ownVehicleHandle != 0 && vehicle.Handle == ownVehicleHandle) ||
                    (playerVehicleHandle != 0 && vehicle.Handle == playerVehicleHandle))
                {
                    continue;
                }

                Vector3 modelMin;
                Vector3 modelMax;
                vehicle.Model.GetDimensions(out modelMin, out modelMax);

                Vector3 vehiclePosition = vehicle.Position;
                Vector3 vehicleRight = vehicle.RightVector;
                Vector3 vehicleForward = vehicle.ForwardVector;
                Vector3 vehicleUp = vehicle.UpVector;

                // Check both feet independently so the passenger can attack to either side.
                for (int sideIndex = 0; sideIndex < 2; sideIndex++)
                {
                    float candidateForce = sideIndex == 0 ? -15f : 15f;
                    float sideOffset = candidateForce < 0f ? -1.0f : 1.0f;
                    Vector3 attackPosition =
                        attackerPosition + attackerRight * sideOffset;
                    Vector3 difference = attackPosition - vehiclePosition;

                    float localX = Vector3.Dot(difference, vehicleRight);
                    float localY = Vector3.Dot(difference, vehicleForward);
                    float localZ = Vector3.Dot(difference, vehicleUp);

                    if (localX < modelMin.X - margin ||
                        localX > modelMax.X + margin ||
                        localY < modelMin.Y - margin ||
                        localY > modelMax.Y + margin ||
                        localZ < modelMin.Z - margin ||
                        localZ > modelMax.Z + margin)
                    {
                        continue;
                    }

                    float dx = attackPosition.X - vehiclePosition.X;
                    float dy = attackPosition.Y - vehiclePosition.Y;
                    float dz = attackPosition.Z - vehiclePosition.Z;
                    float distanceSquared = dx * dx + dy * dy + dz * dz;

                    if (distanceSquared < bestDistance)
                    {
                        bestDistance = distanceSquared;
                        bestVehicle = vehicle;
                        bestForce = candidateForce;
                    }
                }
            }

            force = bestForce;
            return bestVehicle;
        }

        // Dọn cooldown đã hết hạn định kỳ để không giữ handle NPC cũ vô thời hạn.
        private void CleanupNpcBikeKickCooldowns()
        {
            if (nextNpcBikeKickCooldownCleanupTime >= 0 &&
                !IsGameTimeReached(nextNpcBikeKickCooldownCleanupTime))
            {
                return;
            }

            expiredNpcBikeKickHandles.Clear();

            foreach (KeyValuePair<int, int> entry in npcBikeKickCooldowns)
            {
                if (IsGameTimeReached(entry.Value))
                    expiredNpcBikeKickHandles.Add(entry.Key);
            }

            for (int i = 0; i < expiredNpcBikeKickHandles.Count; i++)
                npcBikeKickCooldowns.Remove(expiredNpcBikeKickHandles[i]);

            expiredNpcBikeKickHandles.Clear();

            nextNpcBikeKickCooldownCleanupTime =
                GetGameDeadline(NpcBikeKickCooldownCleanupInterval);
        }

        // Áp dụng đúng kiểu ragdoll và lực Ped đang nhận khi bị cú đá xe máy đánh trúng.
        // Chỉ dùng cho người chơi đi bộ hoặc đang ngồi xe máy/xe đạp để hất khỏi yên.
        // Không nhân lực theo tốc độ hoặc trạng thái hai người của NPC tấn công.
        private static void ApplyNpcBikeKickImpact(Ped target, Vector3 impactForce)
        {
            if (target == null || !target.Exists() || !target.IsAlive)
                return;

            GiveNMMessage(
                target,
                target.IsInVehicle() ? 526 : 715,
                2000);

            target.ApplyForce(impactForce);
        }

        // Khi người chơi ngồi trong ô tô/phương tiện khác, cú đá tác động lên thân xe
        // theo cùng hệ số lực đang được KickVehicle sử dụng.
        // Class thông thường nhận lực đầy đủ; class hạng nặng/đặc biệt chỉ nhận lực nhẹ.
        private void ApplyNpcBikeKickVehicleImpact(
            Ped attacker,
            Ped player,
            Vehicle targetVehicle,
            float force)
        {
            if (attacker == null || !attacker.Exists() ||
                player == null || !player.Exists() ||
                targetVehicle == null || !targetVehicle.Exists() ||
                targetVehicle.IsDead)
            {
                return;
            }

            try
            {
                bool lightKick = IsHeavyOrSpecialVehicleClass(targetVehicle);
                float kickStrength = lightKick ? 0.025f : 0.22f;

                Vector3 lateralForce =
                    attacker.RightVector * (force * kickStrength);

                Vector3 upwardForce =
                    Vector3.WorldUp * (lightKick ? 0.15f : 1.5f);

                // Chọn điểm tiếp xúc lệch tâm giống KickVehicle để xe có mô-men xoay.
                Vector3 worldDifference = attacker.Position - targetVehicle.Position;
                Vector3 vehicleRight = targetVehicle.RightVector;
                Vector3 vehicleForward = targetVehicle.ForwardVector;
                Vector3 vehicleUp = targetVehicle.UpVector;

                Vector3 vehicleOffset = new Vector3(
                    Vector3.Dot(worldDifference, vehicleRight),
                    Vector3.Dot(worldDifference, vehicleForward),
                    Vector3.Dot(worldDifference, vehicleUp));

                Vector3 modelMin;
                Vector3 modelMax;
                targetVehicle.Model.GetDimensions(out modelMin, out modelMax);

                float contactX = vehicleOffset.X >= 0f
                    ? modelMax.X * 0.85f
                    : modelMin.X * 0.85f;

                float contactY = Math.Max(
                    modelMin.Y * 0.70f,
                    Math.Min(modelMax.Y * 0.70f, vehicleOffset.Y));

                float contactZ = modelMin.Z +
                    (modelMax.Z - modelMin.Z) * 0.35f;

                Vector3 contactOffset = new Vector3(
                    contactX,
                    contactY,
                    contactZ);

                targetVehicle.ApplyForce(
                    lateralForce + upwardForce,
                    contactOffset,
                    ForceType.ExternalImpulse);

                // Dùng đúng điều kiện camera shake theo class và tốc độ hiện có.
                // Ví dụ Emergency (class 18) sẽ rung camera khi đạt ngưỡng tốc độ.
                ApplyVehicleKickCameraShake(targetVehicle, player);
            }
            catch
            {
            }
        }

        // NPC đá người chơi gây sát thương cơ bản; đội mũ chỉ còn nhận 67%.
        // Không gọi GetPedKickDamage vì hàm đó cố ý phụ thuộc tốc độ xe người chơi.
        private void ApplyNpcBikeKickDamage(Ped target)
        {
            if (target == null || !target.Exists() || !target.IsAlive)
                return;

            int damage = DefaultPedKickDamage;
            int rewardLevel = GetActiveKickRewardLevel();

            // Cấp 1/2: giảm 23%; cấp 3: giảm 62% sát thương từ cú đá NPC.
            float damageTakenMultiplier = rewardLevel >= 3
                ? KickRewardTier3NpcDamageTakenMultiplier
                : (rewardLevel >= 1 ? KickRewardTier1DamageTakenMultiplier : 1.0f);

            if (IsKickBuffActive())
                damageTakenMultiplier *= KickBuffDamageTakenMultiplier;

            damage = (int)Math.Round(
                damage * damageTakenMultiplier,
                MidpointRounding.AwayFromZero);

            if (IsPedWearingHelmet(target))
            {
                damage = (int)Math.Round(
                    damage * NpcBikeKickHelmetDamageMultiplier,
                    MidpointRounding.AwayFromZero);
            }

            // The player loses armor first; any remainder is applied to health.
            ApplyPedDamageArmorFirst(target, damage);
        }

        // Áp dụng sát thương qua giáp trước. Nếu sát thương vượt lượng giáp còn lại,
        // phần dư mới trừ vào máu; nếu không có giáp thì trừ toàn bộ vào máu.
        // Khi mục tiêu chính là người chơi đang lái xe đạp (class 13), cộng thêm
        // 27% lên tổng sát thương cuối cùng trước khi trừ giáp/máu.
        private static void ApplyPedDamageArmorFirst(Ped target, int damage)
        {
            if (target == null || !target.Exists() || damage <= 0)
                return;

            try
            {
                Ped player = Game.Player.Character;

                if (player != null &&
                    player.Exists() &&
                    player.Handle == target.Handle &&
                    IsPlayerDrivingBicycle(player))
                {
                    damage = (int)Math.Round(
                        damage * BicyclePlayerIncomingDamageMultiplier,
                        MidpointRounding.AwayFromZero);
                }
            }
            catch
            {
                // If the bicycle/player check fails, preserve the original damage.
            }

            int remainingDamage = damage;

            try
            {
                int currentArmor = Math.Max(0, target.Armor);

                if (currentArmor > 0)
                {
                    int absorbedDamage = Math.Min(currentArmor, remainingDamage);
                    target.Armor = currentArmor - absorbedDamage;
                    remainingDamage -= absorbedDamage;
                }
            }
            catch
            {
                // If armor cannot be read or changed, do not discard the damage;
                // apply the full remaining amount to health below.
            }

            if (remainingDamage <= 0)
                return;

            try
            {
                target.Health = Math.Max(0, target.Health - remainingDamage);
            }
            catch
            {
            }
        }

        // Tìm NPC đang thực sự lái xe máy/xe đạp trong phạm vi người chơi.
        // NPC tự chọn trái/phải, hoạt ảnh, ragdoll và lực; không kích hoạt
        // các hậu quả tốc độ cao, steering, truy nã hoặc truy đuổi vốn thuộc
        // luồng người chơi chủ động đá.
        // Cấp 3: thay cú hất người chơi khỏi xe bằng một cú đánh lái rất mạnh.
        // force < 0 là hướng trái của NPC, force > 0 là hướng phải của NPC.
        private void ApplyNpcBikeKickSteeringEffect(
            Ped player,
            Vehicle playerBike,
            float npcKickForce)
        {
            if (player == null || !player.Exists() ||
                playerBike == null || !playerBike.Exists() ||
                !player.IsInVehicle() ||
                player.CurrentVehicle == null ||
                !player.CurrentVehicle.Exists() ||
                player.CurrentVehicle.Handle != playerBike.Handle ||
                !IsMotorcycleOrCycle(playerBike) ||
                !IsPlayerDrivingVehicle(player, playerBike))
            {
                return;
            }

            if (playerKickSteerVehicle != null &&
                (!playerKickSteerVehicle.Exists() ||
                 playerKickSteerVehicle.Handle != playerBike.Handle))
            {
                ClearPlayerKickSteeringEffect();
            }

            playerKickSteerVehicle = playerBike;
            nextPlayerKickSteerValidationTime = -1;

            // SET_VEHICLE_STEER_BIAS: + = trái, - = phải.
            // Đảo dấu theo hướng lực của NPC để đầu xe lệch về đúng phía cú đá.
            targetPlayerKickSteerBias = npcKickForce < 0f
                ? KickRewardTier3CounterSteerMagnitude
                : -KickRewardTier3CounterSteerMagnitude;

            playerKickSteerEndTime = GetGameDeadline(
                KickRewardTier3CounterSteerDuration);
        }

        // Phản đòn đặc biệt khi người chơi giữ INPUT_MULTIPLAYER_INFO.
        // Giá trị được gán trực tiếp (không cộng vào hiệu ứng cấp 3 hoặc steering cũ).
        private void ApplyNpcBikeKickSpecialSteeringEffect(
            Ped player,
            Vehicle playerBike,
            float npcKickForce)
        {
            if (player == null || !player.Exists() ||
                playerBike == null || !playerBike.Exists() ||
                !player.IsInVehicle() ||
                player.CurrentVehicle == null ||
                !player.CurrentVehicle.Exists() ||
                player.CurrentVehicle.Handle != playerBike.Handle ||
                !IsMotorcycleOrCycle(playerBike) ||
                !IsPlayerDrivingVehicle(player, playerBike))
            {
                return;
            }

            if (playerKickSteerVehicle != null &&
                (!playerKickSteerVehicle.Exists() ||
                 playerKickSteerVehicle.Handle != playerBike.Handle))
            {
                ClearPlayerKickSteeringEffect();
            }

            playerKickSteerVehicle = playerBike;
            nextPlayerKickSteerValidationTime = -1;

            // Bỏ phần dư của hiệu ứng trước, sau đó đặt một hướng lệch duy nhất.
            currentPlayerKickSteerBias = 0f;
            targetPlayerKickSteerBias = npcKickForce < 0f
                ? SpecialCounterKickSteerMagnitude
                : -SpecialCounterKickSteerMagnitude;

            playerKickSteerEndTime = GetGameDeadline(
                KickRewardTier3CounterSteerDuration);
        }

        private void UpdateNpcBikeCounterAttacks(Ped player)
        {
            // Schedule before validating the player so an invalid/dead player
            // does not trigger repeated entity checks on every rendered frame.
            if (nextNpcBikeKickScanTime >= 0 &&
                !IsGameTimeReached(nextNpcBikeKickScanTime))
            {
                return;
            }

            nextNpcBikeKickScanTime = GetGameDeadline(
                NpcBikeKickDetectionInterval);

            if (player == null || !player.Exists() || !player.IsAlive)
                return;

            // The nearby-ped broadphase runs at the scheduled 75 ms cadence.

            CleanupNpcBikeKickCooldowns();

            Ped[] nearbyPeds = World.GetNearbyPeds(
                player,
                NpcBikeKickDetectionRadius);

            if (nearbyPeds == null || nearbyPeds.Length == 0)
                return;

            Ped selectedAttacker = null;
            float selectedForce = 0f;
            float bestDistanceSquared =
                NpcBikeKickContactRadius * NpcBikeKickContactRadius;

            // The player's current vehicle cannot change during this scan;
            // resolve it once instead of issuing the same entity queries for
            // every nearby pedestrian.
            int playerVehicleHandle = 0;
            int recruitedPassengerHandle = 0;
            PassengerControlData passengerData = recruitedPassenger;

            if (passengerData != null && passengerData.Passenger != null &&
                passengerData.Passenger.Exists())
            {
                recruitedPassengerHandle = passengerData.Passenger.Handle;
            }

            if (player.IsInVehicle())
            {
                Vehicle playerVehicle = player.CurrentVehicle;

                if (playerVehicle != null && playerVehicle.Exists())
                    playerVehicleHandle = playerVehicle.Handle;
            }

            foreach (Ped candidate in nearbyPeds)
            {
                if (candidate == null ||
                    !candidate.Exists() ||
                    candidate.Handle == player.Handle ||
                    (recruitedPassengerHandle != 0 &&
                     candidate.Handle == recruitedPassengerHandle) ||
                    !candidate.IsHuman ||
                    !candidate.IsAlive ||
                    !candidate.IsInVehicle())
                {
                    continue;
                }

                Vehicle npcBike = candidate.CurrentVehicle;

                if (npcBike == null ||
                    !npcBike.Exists() ||
                    !IsMotorcycleOrCycle(npcBike))
                {
                    continue;
                }

                // Chỉ tài xế mới được phép tung cú đá; bỏ qua NPC ngồi ghế phụ.
                Ped driver = npcBike.GetPedOnSeat(VehicleSeat.Driver);

                if (driver == null ||
                    !driver.Exists() ||
                    driver.Handle != candidate.Handle)
                {
                    continue;
                }

                // Không phản đòn nếu người chơi đang ngồi cùng chính chiếc xe này.
                if (playerVehicleHandle != 0 &&
                    playerVehicleHandle == npcBike.Handle)
                {
                    continue;
                }

                int nextAllowedTime;

                if (npcBikeKickCooldowns.TryGetValue(
                        candidate.Handle,
                        out nextAllowedTime) &&
                    !IsGameTimeReached(nextAllowedTime))
                {
                    continue;
                }

                float candidateForce;
                float candidateDistanceSquared;

                if (!TryGetNpcBikeKickContactCore(
                        candidate,
                        player,
                        out candidateForce,
                        out candidateDistanceSquared))
                {
                    continue;
                }

                if (candidateDistanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = candidateDistanceSquared;
                    selectedAttacker = candidate;
                    selectedForce = candidateForce;
                }
            }

            if (selectedAttacker == null || !selectedAttacker.Exists())
                return;

            // Đặt cooldown trước hoạt ảnh/yield để một NPC không thể đánh lặp.
            npcBikeKickCooldowns[selectedAttacker.Handle] =
                GetGameDeadline(NpcBikeKickCooldown);

            // Every eligible 1.5-second cycle gets a fresh 70/30 roll.
            // A failed roll consumes this NPC's cooldown but causes no kick,
            // animation, damage, steering effect or other counter-attack action.
            if (soundRandom.Next(100) >= NpcBikeCounterAttackChance)
                return;

            string animDict =
                "anim@veh@drivebybike@dirt@front@melee_kick";
            string animFile = selectedForce < 0f
                ? "melee_l"
                : "melee_r";

            PlayAnim(selectedAttacker, animDict, animFile, 500, 8);
            WaitWhileDrawingKickProgressHuds(100);
            UpdateKickBuffCycle();

            // Kiểm tra lại sau thời gian ra đòn để tránh đánh trúng mục tiêu đã rời tầm.
            if (player == null || !player.Exists() || !player.IsAlive ||
                selectedAttacker == null ||
                !selectedAttacker.Exists() ||
                !selectedAttacker.IsAlive ||
                !selectedAttacker.IsInVehicle())
            {
                return;
            }

            Vehicle currentNpcBike = selectedAttacker.CurrentVehicle;

            if (currentNpcBike == null ||
                !currentNpcBike.Exists() ||
                !IsMotorcycleOrCycle(currentNpcBike))
            {
                return;
            }

            Ped currentDriver = currentNpcBike.GetPedOnSeat(VehicleSeat.Driver);

            if (currentDriver == null ||
                !currentDriver.Exists() ||
                currentDriver.Handle != selectedAttacker.Handle)
            {
                return;
            }

            float finalForce;
            float finalDistanceSquared;

            if (!TryGetNpcBikeKickContactCore(
                    selectedAttacker,
                    player,
                    out finalForce,
                    out finalDistanceSquared))
            {
                return;
            }

            // Chỉ dùng lực cơ bản một người: NPC không được hưởng hệ số x1.36,
            // nhân sát thương theo tốc độ hoặc các hiệu ứng off-road của người chơi.
            Vector3 impactForce =
                selectedAttacker.RightVector * finalForce +
                Vector3.WorldUp * 3f;

            PlayRandomBikeHitSound();

            // Phân loại phương tiện mà người chơi đang ngồi:
            // - Xe máy/xe đạp: tác động trực tiếp lên Ped để hất khỏi xe.
            // - Phương tiện khác: tác động lên thân xe theo hệ số class của KickVehicle.
            // - Đi bộ hoặc không lấy được phương tiện hợp lệ: giữ lực Ped hiện tại.
            Vehicle playerTargetVehicle = null;
            if (player.IsInVehicle())
            {
                try { playerTargetVehicle = player.CurrentVehicle; } catch { }
            }

            bool playerDrivingBike =
                playerTargetVehicle != null &&
                playerTargetVehicle.Exists() &&
                IsMotorcycleOrCycle(playerTargetVehicle) &&
                IsPlayerDrivingVehicle(player, playerTargetVehicle);

            bool specialCounterSteering =
                playerDrivingBike &&
                (IsControlPressed(0, SpecialCounterKickModifierControl) ||
                 IsDisabledControlPressed(0, SpecialCounterKickModifierControl));

            bool tier3CounterSteering =
                GetActiveKickRewardLevel() >= 3 &&
                playerDrivingBike;

            if (playerTargetVehicle != null &&
                playerTargetVehicle.Exists() &&
                !IsMotorcycleOrCycle(playerTargetVehicle))
            {
                ApplyNpcBikeKickVehicleImpact(
                    selectedAttacker,
                    player,
                    playerTargetVehicle,
                    finalForce);
            }
            else if (specialCounterSteering)
            {
                // INPUT_MULTIPLAYER_INFO thay thế hoàn toàn nhánh ragdoll/cấp 3:
                // chỉ áp dụng một hướng lệch lái cực mạnh, không cộng hai hiệu ứng.
                ApplyNpcBikeKickSpecialSteeringEffect(
                    player,
                    playerTargetVehicle,
                    finalForce);
            }
            else if (tier3CounterSteering)
            {
                // Giữ nguyên hiệu ứng lệch lái cấp 3 đang có.
                ApplyNpcBikeKickSteeringEffect(
                    player,
                    playerTargetVehicle,
                    finalForce);
            }
            else if (playerDrivingBike &&
                     IsKickBuffActive() &&
                     soundRandom.Next(100) >= KickBuffRagdollChance)
            {
                // During Buff, only the fixed 10% failed-resistance roll knocks
                // the rider off the bike; the other 90% avoids the ragdoll impact.
            }
            else
            {
                // Cú phản đòn làm người chơi ngã xe chỉ làm mất một nấc chuỗi.
                PenalizeAndDetachHighSpeedVehicleKickStreak();
                ApplyNpcBikeKickImpact(player, impactForce);
            }

            ApplyNpcBikeKickDamage(player);

            // Nếu người chơi đang chở NPC ghế phụ do mod tuyển, người ngồi cùng
            // cũng nhận cú hất vật lý; không thay đổi trạng thái invincible của NPC này.
            if (HasRecruitedPassengerSeated(player))
            {
                Ped passenger = recruitedPassenger.Passenger;

                if (passenger != null && passenger.Exists() && passenger.IsAlive)
                {
                    // The recruited passenger receives the same fixed 10% fall chance
                    // while Buff is active; otherwise preserve the original impact.
                    if (!IsKickBuffActive() ||
                        soundRandom.Next(100) < KickBuffRagdollChance)
                    {
                        ApplyNpcBikeKickImpact(passenger, impactForce);
                    }
                }
            }
        }

        // During Kick Buff's draining phase, the recruited passenger behaves as
        // an independent rider: its scan, cooldown, animation, target and impact do
        // not depend on whether the player presses either kick control.
        private void UpdateBuffPassengerCounterAttacks(Ped player)
        {
            if (IsPassengerTyreAttackActive())
                return;

            // While an attack is cooling down, neither rider-state checks nor a
            // nearby-target scan can produce an attack. Skip those natives first.
            if (nextBuffPassengerKickAllowedTime >= 0 &&
                !IsGameTimeReached(nextBuffPassengerKickAllowedTime))
            {
                return;
            }

            bool independentRider = IsPassengerIndependentAttackActive(player);
            bool buffPassenger = !independentRider &&
                                 IsKickBuffDraining() &&
                                 HasRecruitedPassengerSeated(player);

            // A transferred teammate becomes an independent rider at all times;
            // the original passenger keeps its existing independent attack only
            // during the active Buff drain.
            if (!independentRider && !buffPassenger)
            {
                nextBuffPassengerKickScanTime = -1;
                nextBuffPassengerKickAllowedTime = -1;
                return;
            }

            if (nextBuffPassengerKickScanTime >= 0 &&
                !IsGameTimeReached(nextBuffPassengerKickScanTime))
            {
                return;
            }

            nextBuffPassengerKickScanTime = GetGameDeadline(
                NpcBikeKickDetectionInterval);

            Ped passenger = recruitedPassenger.Passenger;
            Vehicle attackerVehicle = independentRider
                ? recruitedPassenger.IndependentVehicle
                : recruitedPassenger.Vehicle;

            if (passenger == null || !passenger.Exists() || !passenger.IsAlive ||
                attackerVehicle == null || !attackerVehicle.Exists() ||
                !passenger.IsInVehicle() ||
                passenger.CurrentVehicle == null ||
                !passenger.CurrentVehicle.Exists() ||
                passenger.CurrentVehicle.Handle != attackerVehicle.Handle)
            {
                return;
            }

            if (independentRider)
            {
                Ped riderDriver = attackerVehicle.GetPedOnSeat(VehicleSeat.Driver);
                if (riderDriver == null || !riderDriver.Exists() ||
                    riderDriver.Handle != passenger.Handle)
                {
                    return;
                }
            }

            Ped selectedTargetPed = null;
            Vehicle selectedTargetVehicle = null;
            float selectedForce = 0f;
            float bestDistanceSquared =
                NpcBikeKickContactRadius * NpcBikeKickContactRadius;

            // Find a Ped target around the passenger, excluding the player and
            // the passenger itself. Use the same one-metre kick-contact test as
            // the existing NPC motorcycle counter-attack system.
            Ped[] nearbyPeds = World.GetNearbyPeds(
                passenger,
                NpcBikeKickDetectionRadius);

            if (nearbyPeds != null)
            {
                foreach (Ped candidate in nearbyPeds)
                {
                    if (candidate == null ||
                        !candidate.Exists() ||
                        !candidate.IsAlive ||
                        candidate.Handle == player.Handle ||
                        candidate.Handle == passenger.Handle)
                    {
                        continue;
                    }

                    // Occupants of ordinary cars are handled as a vehicle target;
                    // occupants of bikes remain Ped targets so the current knockback,
                    // steering and helmet-damage logic is preserved.
                    if (candidate.IsInVehicle())
                    {
                        Vehicle occupiedVehicle = candidate.CurrentVehicle;
                        if (occupiedVehicle == null ||
                            !occupiedVehicle.Exists() ||
                            !IsMotorcycleOrCycle(occupiedVehicle))
                        {
                            continue;
                        }
                    }

                    float candidateForce;
                    float candidateDistanceSquared;
                    if (TryGetNpcBikeKickContactCore(
                            passenger,
                            candidate,
                            out candidateForce,
                            out candidateDistanceSquared) &&
                        candidateDistanceSquared < bestDistanceSquared)
                    {
                        bestDistanceSquared = candidateDistanceSquared;
                        selectedTargetPed = candidate;
                        selectedTargetVehicle = null;
                        selectedForce = candidateForce;
                    }
                }
            }

            // Match the player's oriented vehicle-contact geometry when no Ped
            // is in range. A single nearby-vehicle scan checks both kick sides.
            if (selectedTargetPed == null)
            {
                selectedTargetVehicle = FindPassengerBuffKickTargetVehicle(
                    passenger,
                    out selectedForce);
            }

            if ((selectedTargetPed == null && selectedTargetVehicle == null) ||
                (!independentRider && !IsKickBuffDraining()))
            {
                return;
            }

            // Independent cooldown: it is not tied to nextKickAllowedTime or
            // to the player's current holdingAttack state.
            nextBuffPassengerKickAllowedTime = GetGameDeadline(
                NpcBikeKickCooldown);

            bool leftKick = selectedForce < 0f;
            PreparePassengerWeaponProp(passenger, leftKick);

            string animDict =
                "anim@veh@drivebybike@dirt@front@" + animWeapType;
            string animFile = leftKick ? "melee_l" : "melee_r";
            PlayAnim(passenger, animDict, animFile, 500, 8);
            WaitWhileDrawingKickProgressHuds(100);
            UpdateKickBuffCycle();

            // Abort cleanly if Buff ended, the passenger left the seat, or the
            // selected target disappeared while the attack animation was playing.
            bool attackStateStillValid = independentRider
                ? IsRecruitedIndependentRiderActive(player)
                : (IsKickBuffDraining() && HasRecruitedPassengerSeated(player));

            if (!attackStateStillValid ||
                passenger == null || !passenger.Exists() || !passenger.IsAlive)
            {
                CleanupPassengerWeaponProp();
                return;
            }

            float finalForce = 0f;
            float finalDistanceSquared = float.MaxValue;
            bool hasPedContact = selectedTargetPed != null &&
                selectedTargetPed.Exists() &&
                selectedTargetPed.IsAlive &&
                TryGetNpcBikeKickContactCore(
                    passenger,
                    selectedTargetPed,
                    out finalForce,
                    out finalDistanceSquared);

            if (hasPedContact)
            {
                ApplyBuffPassengerPedKick(
                    player,
                    passenger,
                    selectedTargetPed,
                    finalForce);
            }
            else if (selectedTargetVehicle != null &&
                     selectedTargetVehicle.Exists() &&
                     !selectedTargetVehicle.IsDead)
            {
                float vehicleFinalForce;
                Vehicle currentContactVehicle =
                    FindPassengerBuffKickTargetVehicle(
                        passenger,
                        out vehicleFinalForce);

                if (currentContactVehicle != null &&
                    currentContactVehicle.Handle == selectedTargetVehicle.Handle)
                {
                    ApplyBuffPassengerVehicleKick(
                        player,
                        passenger,
                        currentContactVehicle,
                        vehicleFinalForce);
                }
            }
            else if (selectedTargetPed != null &&
                     selectedTargetPed.Exists() &&
                     selectedTargetPed.IsAlive &&
                     selectedTargetPed.IsInVehicle())
            {
                // A target that switched into a vehicle during the animation is
                // resolved against the same direct-vehicle contact test.
                Vehicle occupiedVehicle = selectedTargetPed.CurrentVehicle;
                if (occupiedVehicle != null &&
                    occupiedVehicle.Exists() &&
                    !occupiedVehicle.IsDead &&
                    occupiedVehicle.Handle != recruitedPassenger.Vehicle.Handle &&
                    (recruitedPassenger.IndependentVehicle == null ||
                     !recruitedPassenger.IndependentVehicle.Exists() ||
                     occupiedVehicle.Handle != recruitedPassenger.IndependentVehicle.Handle))
                {
                    float vehicleFinalForce;
                    Vehicle currentContactVehicle =
                        FindPassengerBuffKickTargetVehicle(
                            passenger,
                            out vehicleFinalForce);

                    if (currentContactVehicle != null &&
                        currentContactVehicle.Handle == occupiedVehicle.Handle)
                    {
                        ApplyBuffPassengerVehicleKick(
                            player,
                            passenger,
                            currentContactVehicle,
                            vehicleFinalForce);
                    }
                }
            }

            CleanupPassengerWeaponProp();
        }

        // Keep the existing two-rider damage/force stack, Buff bonus, helmet
        // reduction, steering chance and hit effects; only the attacker is now
        // the passenger Ped rather than an animation synchronized to the player.
        private void ApplyBuffPassengerPedKick(
            Ped player,
            Ped passenger,
            Ped target,
            float force)
        {
            if (player == null || !player.Exists() ||
                passenger == null || !passenger.Exists() ||
                target == null || !target.Exists() || !target.IsAlive)
            {
                return;
            }

            bool hasPassenger = HasRecruitedCompanion(player);
            bool twoPersonOffRoad = hasPassenger && IsPlayerOnOffRoadTerrain(player);
            // Two riders retain a small cooperative bonus, tuned from x2 to x1.96.
            float damageMultiplier = hasPassenger && !twoPersonOffRoad ? 1.96f : 1.0f;
            float forceMultiplier = hasPassenger ? 1.36f : 1.0f;

            if (hasPassenger && GetActiveKickRewardLevel() >= 2)
                forceMultiplier *= KickRewardTier2PassengerForceMultiplier;

            float targetPedForceMultiplier = forceMultiplier *
                (twoPersonOffRoad ? TwoPersonOffRoadKickMultiplier : 1.0f);

            // Passenger's independent attack is not the player's Special Kick;
            // the active Buff remains an additional layer exactly as before.
            bool kickBuffActive = IsKickBuffActive();
            if (kickBuffActive)
            {
                damageMultiplier *= KickBuffDamageMultiplier;
                forceMultiplier *= KickBuffForceMultiplier;
                targetPedForceMultiplier *= KickBuffForceMultiplier;
            }

            RegisterFirstKickHelp();
            PlayRandomBikeHitSound();

            if (target.IsInVehicle())
            {
                Vehicle targetVehicle = target.CurrentVehicle;
                if (targetVehicle == null ||
                    !targetVehicle.Exists() ||
                    !IsMotorcycleOrCycle(targetVehicle))
                {
                    return;
                }

                bool steerNpcInsteadOfKnockback =
                    soundRandom.Next(100) < NpcVictimBikeSteerChance &&
                    StartNpcVictimBikeSteeringEffect(
                        target,
                        targetVehicle,
                        force);

                if (!steerNpcInsteadOfKnockback)
                    GiveNMMessage(target, 526, 2000);

                ApplyVehicleKickDamage(
                    targetVehicle,
                    player,
                    damageMultiplier,
                    false,
                    passenger);

                ApplyPlayerKickSteeringEffect(
                    player,
                    targetVehicle,
                    force,
                    hasPassenger);

                if (!steerNpcInsteadOfKnockback)
                {
                    target.ApplyForce(
                        (passenger.RightVector * force + Vector3.WorldUp * 3f) *
                        targetPedForceMultiplier);
                }

                float targetDamage = GetTargetPedKickDamage(
                    target,
                    player,
                    passenger) * damageMultiplier;

                if (kickBuffActive)
                    targetDamage *= KickBuffTargetDamageBonusMultiplier;

                int finalTargetDamage = (int)Math.Round(
                    targetDamage,
                    MidpointRounding.AwayFromZero);

                target.Health = Math.Max(0, target.Health - finalTargetDamage);
                RegisterKickBuffHit();
                RegisterKickRewardHit(player);
                RegisterDisturbance();
                ApplyBuffPassengerWeaponHitEffect(target);

                if (!steerNpcInsteadOfKnockback)
                    target.Task.FightAgainst(passenger);

                return;
            }

            GiveNMMessage(target, 715, 2000);
            target.ApplyForce(
                (passenger.RightVector * force + Vector3.WorldUp * 3f) *
                targetPedForceMultiplier);

            float pedestrianDamage = GetTargetPedKickDamage(
                target,
                player,
                passenger) * damageMultiplier;

            if (kickBuffActive)
                pedestrianDamage *= KickBuffTargetDamageBonusMultiplier;

            int finalPedestrianDamage = (int)Math.Round(
                pedestrianDamage,
                MidpointRounding.AwayFromZero);

            target.Health = Math.Max(0, target.Health - finalPedestrianDamage);
            RegisterKickBuffHit();
            RegisterKickRewardHit(player);
            RegisterDisturbance();
            ApplyBuffPassengerWeaponHitEffect(target);
            target.Task.FightAgainst(passenger);
        }

        private void ApplyBuffPassengerWeaponHitEffect(Ped target)
        {
            if (target == null || !target.Exists() ||
                (animWeapType != "melee_1h" &&
                 animWeapType != "melee_small_wpn"))
            {
                return;
            }

            try
            {
                Function.Call(
                    Native(7798175403732277905L),
                    "scr_solomon3");

                Function.Call(
                    Native(1044398152630765081L),
                    "scr_trev4_747_blood_impact",
                    target,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    31086,
                    0.125f,
                    false,
                    false,
                    false);
            }
            catch
            {
            }
        }

        private void ApplyBuffPassengerVehicleKick(
            Ped player,
            Ped passenger,
            Vehicle targetVehicle,
            float force)
        {
            if (player == null || !player.Exists() ||
                passenger == null || !passenger.Exists() ||
                targetVehicle == null || !targetVehicle.Exists() ||
                targetVehicle.IsDead)
            {
                return;
            }

            bool hasPassenger = HasRecruitedCompanion(player);
            bool twoPersonOffRoad = hasPassenger && IsPlayerOnOffRoadTerrain(player);
            float damageMultiplier = hasPassenger && !twoPersonOffRoad ? 1.96f : 1.0f;
            float forceMultiplier = hasPassenger ? 1.36f : 1.0f;

            if (hasPassenger && GetActiveKickRewardLevel() >= 2)
                forceMultiplier *= KickRewardTier2PassengerForceMultiplier;

            if (IsKickBuffActive())
            {
                damageMultiplier *= KickBuffDamageMultiplier;
                forceMultiplier *= KickBuffForceMultiplier;
            }

            KickVehicle(
                targetVehicle,
                force,
                damageMultiplier,
                forceMultiplier,
                hasPassenger,
                false,
                passenger,
                true);
        }

        private void MeleeBikeContact(Vector3 attackPos, float force)
        {
            Ped player = Game.Player.Character;

            if (player == null || !player.Exists())
                return;

            UpdateKickBuffCycle();
            UpdateHighSpeedVehicleKickStreakState(player);
            bool specialKick = highSpeedSpecialKickReady &&
                               highSpeedSpecialKickChargesRemaining > 0;
            int specialKickTier = specialKick
                ? GetCurrentHighSpeedSpecialKickTier()
                : 0;
            float specialKickDamageMultiplier = specialKick
                ? (float)Math.Pow(
                    1.0 + HighSpeedSpecialKickDamageIncreasePerUse,
                    specialKickTier - 1)
                : 1.0f;
            float specialKickForceScale = specialKick
                ? (float)Math.Pow(
                    1.0 + HighSpeedSpecialKickForceIncreasePerUse,
                    specialKickTier - 1)
                : 1.0f;

            // Tách hệ số sát thương và lực vật lý.
            // Cú đặc biệt thay thế hoàn toàn x1.96/x1.36 của chế độ hai người.
            bool hasPassenger = HasRecruitedCompanion(player);
            Ped passengerKicker = hasPassenger && recruitedPassenger != null
                ? recruitedPassenger.Passenger
                : null;
            bool twoPersonOffRoad = hasPassenger && IsPlayerOnOffRoadTerrain(player);

            float damageMultiplier = hasPassenger && !twoPersonOffRoad ? 1.96f : 1.0f;
            float forceMultiplier = hasPassenger ? 1.36f : 1.0f;

            // Cấp 2: tăng thêm 22% lực đá khi có NPC ngồi ghế phụ.
            if (!specialKick && hasPassenger && GetActiveKickRewardLevel() >= 2)
                forceMultiplier *= KickRewardTier2PassengerForceMultiplier;

            float targetPedForceMultiplier = forceMultiplier *
                (twoPersonOffRoad ? TwoPersonOffRoadKickMultiplier : 1.0f);

            if (specialKick)
            {
                // Cú đặc biệt luôn dùng đúng x8 sát thương/x4 lực, không nhân đôi
                // theo hành khách và không bị giảm bởi hệ số off-road hai người.
                damageMultiplier = 1.0f;
                forceMultiplier = 4.0f * specialKickForceScale;
                targetPedForceMultiplier = 4.0f * specialKickForceScale;

                // A helmet adds a further x1.12 to both physical-force paths.
                if (IsPedWearingHelmet(player))
                {
                    forceMultiplier *= HelmetSpecialKickForceMultiplier;
                    targetPedForceMultiplier *= HelmetSpecialKickForceMultiplier;
                }
            }

            // Buff is an additional layer: it stacks after co-op, off-road and Special Kick.
            bool kickBuffActive = IsKickBuffActive();

            if (kickBuffActive)
            {
                damageMultiplier *= KickBuffDamageMultiplier;
                forceMultiplier *= KickBuffForceMultiplier;
                targetPedForceMultiplier *= KickBuffForceMultiplier;
            }

            // Giữ nguyên logic tìm và xử lý NPC.
            Ped[] nearbyPeds = World.GetNearbyPeds(player, 4f);

            if (nearbyPeds != null && nearbyPeds.Length > 0)
            {
                Ped target = null;

                foreach (Ped nearbyPed in nearbyPeds)
                {
                    if (nearbyPed == null || !nearbyPed.Exists())
                        continue;

                    // Không để đòn đánh đồng bộ tự chọn chính NPC ghế phụ.
                    if (recruitedPassenger != null &&
                        recruitedPassenger.Passenger != null &&
                        recruitedPassenger.Passenger.Exists() &&
                        nearbyPed.Handle == recruitedPassenger.Passenger.Handle)
                    {
                        continue;
                    }

                    target = nearbyPed;
                    break;
                }

                if (target != null &&
                    target.Position.DistanceTo(attackPos) <= 1.0f &&
                    target.IsAlive)
                {
                    RegisterFirstKickHelp();

                    if (target.IsInVehicle())
                    {
                        Vehicle targetVehicle = target.CurrentVehicle;

                        if (targetVehicle == null ||
                            !targetVehicle.Exists())
                        {
                            return;
                        }

                        if (IsMotorcycleOrCycle(targetVehicle))
                        {
                            PlayRandomBikeHitSound();

                            // Nếu NPC bị đá đang ngồi trên xe có tài xế hợp lệ,
                            // nhánh 15% sẽ đánh lái cả chiếc xe thay cho ragdoll/lực hất.
                            bool steerNpcInsteadOfKnockback =
                                soundRandom.Next(100) < NpcVictimBikeSteerChance &&
                                StartNpcVictimBikeSteeringEffect(
                                    target,
                                    targetVehicle,
                                    force);

                            if (!steerNpcInsteadOfKnockback)
                                GiveNMMessage(target, 526, 2000);

                            // Phương tiện chở NPC cũng nhận sát thương.
                            ApplyVehicleKickDamage(
                                targetVehicle,
                                player,
                                damageMultiplier,
                                specialKick,
                                passengerKicker);

                            // Xe người chơi bị lệch bánh lái theo hướng chân đá.
                            ApplyPlayerKickSteeringEffect(player, targetVehicle, force, hasPassenger);

                            // Nhánh 15% giữ NPC trên yên và chỉ đánh lái chiếc xe.
                            // 85% còn lại dùng nguyên ragdoll và lực hất hiện tại.
                            if (!steerNpcInsteadOfKnockback)
                            {
                                target.ApplyForce(
                                    (player.RightVector * force + Vector3.WorldUp * 3f) *
                                    targetPedForceMultiplier);
                            }

                            float targetDamage = specialKick
                                ? GetSpecialTargetPedKickDamage(
                                    target,
                                    player,
                                    passengerKicker,
                                    specialKickDamageMultiplier) * damageMultiplier
                                : GetTargetPedKickDamage(
                                    target,
                                    player,
                                    passengerKicker) * damageMultiplier;

                            if (kickBuffActive)
                                targetDamage *= KickBuffTargetDamageBonusMultiplier;

                            int finalTargetDamage = (int)Math.Round(
                                targetDamage,
                                MidpointRounding.AwayFromZero);

                            target.Health = Math.Max(0, target.Health - finalTargetDamage);

                            // A player hit with the recruited passenger seated contributes two.
                            RegisterKickBuffHit(hasPassenger ? 2 : 1);

                            // Bộ đếm phần thưởng nhận mọi mục tiêu; chuỗi 5 chỉ nhận xe.
                            RegisterKickRewardHit(player);
                            ApplyHighSpeedKickConsequences(player, hasPassenger);
                            RegisterHighSpeedVehicleKick(player, specialKick);

                            if (animWeapType == "melee_1h" ||
                                animWeapType == "melee_small_wpn")
                            {
                                Function.Call(
                                    Native(7798175403732277905L),
                                    "scr_solomon3");

                                Function.Call(
                                    Native(1044398152630765081L),
                                    "scr_trev4_747_blood_impact",
                                    target,
                                    0.0,
                                    0.0,
                                    0.0,
                                    0.0,
                                    0.0,
                                    0.0,
                                    31086,
                                    0.125f,
                                    false,
                                    false,
                                    false);
                            }

                            // Ghi nhận một lần quấy rầy thành công.
                            RegisterDisturbance();

                            if (specialKick)
                                CompleteHighSpeedSpecialKick();

                            // Khi nhánh steering trúng, giữ task lái xe hiện tại của NPC.
                            if (!steerNpcInsteadOfKnockback)
                                target.Task.FightAgainst(player);

                            // Hothands applies only to the player-triggered kick.
                            ApplyKickBuffExplosiveMeleeImpact(player, target.Position);
                            return;
                        }
                    }
                    else
                    {
                        RegisterFirstKickHelp();

                        PlayRandomBikeHitSound();

                        GiveNMMessage(target, 715, 2000);

                        // Vị trí thứ hai: NPC đang đứng bên ngoài phương tiện.
                        target.ApplyForce(
                            (player.RightVector * force +
                             Vector3.WorldUp * 3f) * targetPedForceMultiplier);

                        float targetDamage = specialKick
                            ? GetSpecialTargetPedKickDamage(
                                target,
                                player,
                                passengerKicker,
                                specialKickDamageMultiplier) * damageMultiplier
                            : GetTargetPedKickDamage(
                                target,
                                player,
                                passengerKicker) * damageMultiplier;

                        if (kickBuffActive)
                            targetDamage *= KickBuffTargetDamageBonusMultiplier;

                        int finalTargetDamage = (int)Math.Round(
                            targetDamage,
                            MidpointRounding.AwayFromZero);

                        target.Health = Math.Max(0, target.Health - finalTargetDamage);

                        // A player hit with the recruited passenger seated contributes two.
                        RegisterKickBuffHit(hasPassenger ? 2 : 1);

                        // NPC đi bộ cũng được tính vào thành tích 40 giây nếu tốc độ > 50 km/h.
                        RegisterKickRewardHit(player);

                        // At >= 110 km/h, a successful hit on an on-foot NPC
                        // adds half a Special Kick segment (motorcycle/cycle riders
                        // are handled by the separate branch above).
                        RegisterHighSpeedNpcKick(player, specialKick);

                        ApplyHighSpeedKickConsequences(player, hasPassenger);

                        if (animWeapType == "melee_1h" ||
                            animWeapType == "melee_small_wpn")
                        {
                            Function.Call(
                                Native(7798175403732277905L),
                                "scr_solomon3");

                            Function.Call(
                                Native(1044398152630765081L),
                                "scr_trev4_747_blood_impact",
                                target,
                                0.0,
                                0.0,
                                0.0,
                                0.0,
                                0.0,
                                0.0,
                                31086,
                                0.125f,
                                false,
                                false,
                                false);
                        }

                        // Ghi nhận một lần quấy rầy thành công.
                        RegisterDisturbance();

                        if (specialKick)
                            CompleteHighSpeedSpecialKick();

                        target.Task.FightAgainst(player);

                        // Hothands applies only to the player-triggered kick.
                        ApplyKickBuffExplosiveMeleeImpact(player, target.Position);
                        return;
                    }
                }
            }

            // Chỉ xử lý phương tiện khi không có NPC bị trúng.
            Vehicle nearbyTargetVehicle = FindNearbyTargetVehicle(player, attackPos);

            if (nearbyTargetVehicle == null || !nearbyTargetVehicle.Exists())
                return;

            KickVehicle(
                nearbyTargetVehicle,
                force,
                damageMultiplier,
                forceMultiplier,
                hasPassenger,
                specialKick,
                null,
                false,
                specialKickDamageMultiplier);
        }

        // Kiểm tra xe người chơi có chạy trên 90 km/h hay không.
        private static bool IsPlayerAboveKickDamageSpeed(Ped player)
        {
            if (player == null ||
                !player.Exists() ||
                !player.IsInVehicle())
            {
                return false;
            }

            try
            {
                Vehicle playerVehicle = player.CurrentVehicle;

                if (playerVehicle == null || !playerVehicle.Exists())
                    return false;

                // Vehicle.Speed sử dụng đơn vị mét/giây.
                return playerVehicle.Speed > HighSpeedKickThreshold;
            }
            catch
            {
                return false;
            }
        }

        // Kiểm tra người chơi đang chạy trên 110 km/h.
        private static bool IsPlayerAboveHighSpeedPenaltyThreshold(Ped player)
        {
            if (player == null ||
                !player.Exists() ||
                !player.IsInVehicle())
            {
                return false;
            }

            try
            {
                Vehicle playerVehicle = player.CurrentVehicle;

                if (playerVehicle == null ||
                    !playerVehicle.Exists() ||
                    !IsMotorcycleOrCycle(playerVehicle))
                {
                    return false;
                }

                return playerVehicle.Speed > HighSpeedKickPenaltyThreshold;
            }
            catch
            {
                return false;
            }
        }

        // Kiểm tra thời gian hồi chiêu đá.
        private bool IsKickCooldownActive()
        {
            return !IsGameTimeReached(nextKickAllowedTime);
        }

        // Kích hoạt ragdoll khi người chơi đang chạy xe tốc độ cao.
        // Đồng thời truyền vận tốc của xe sang người chơi,
        // kết hợp lực đẩy về phía trước và lực nâng nhẹ.
        private void ApplyHighSpeedForwardRagdoll(Ped player)
        {
            if (player == null || !player.Exists())
                return;

            if (!player.IsInVehicle())
                return;

            Vehicle vehicle = player.CurrentVehicle;

            if (vehicle == null ||
                !vehicle.Exists() ||
                !IsMotorcycleOrCycle(vehicle))
            {
                return;
            }

            try
            {
                // Lấy vận tốc thực tế của phương tiện trước khi ragdoll.
                Vector3 vehicleVelocity = vehicle.Velocity;

                // Hướng chuyển động thực tế của xe.
                Vector3 forwardDirection = vehicleVelocity;

                float velocityLength = forwardDirection.Length();

                if (velocityLength > 0.1f)
                {
                    forwardDirection /= velocityLength;
                }
                else
                {
                    forwardDirection = vehicle.ForwardVector;
                }

                // Kích hoạt ragdoll.
                player.CanRagdoll = true;

                Function.Call(
                    Hash.SET_PED_TO_RAGDOLL,
                    player.Handle,
                    HighSpeedRagdollDuration,
                    HighSpeedRagdollDuration,
                    0,
                    false,
                    false,
                    false);

                // Giữ lại vận tốc của xe để tránh cảm giác
                // người chơi đột ngột mất toàn bộ động lượng.
                Vector3 launchVelocity = vehicleVelocity;

                // Cấp 1 trở lên giảm 20% lực phản chấn bổ sung khi ngã ở tốc độ cao.
                float recoilMultiplier = GetActiveKickRewardLevel() >= 1
                    ? KickRewardTier1RecoilMultiplier
                    : 1.0f;

                // Tạo vận tốc bay về phía trước.
                launchVelocity +=
                    forwardDirection * HighSpeedRagdollForwardBoost * recoilMultiplier;

                // Nâng người chơi lên một chút để tạo hiệu ứng văng.
                launchVelocity +=
                    Vector3.WorldUp * HighSpeedRagdollUpwardBoost * recoilMultiplier;

                // Truyền vận tốc mới cho người chơi.
                Function.Call(
                    Hash.SET_ENTITY_VELOCITY,
                    player.Handle,
                    launchVelocity.X,
                    launchVelocity.Y,
                    launchVelocity.Z);
            }
            catch
            {
                // Nếu không thể áp dụng vận tốc bổ sung,
                // vẫn duy trì ragdoll cơ bản.
                try
                {
                    player.CanRagdoll = true;

                    Function.Call(
                        Hash.SET_PED_TO_RAGDOLL,
                        player.Handle,
                        HighSpeedRagdollDuration,
                        HighSpeedRagdollDuration,
                        0,
                        false,
                        false,
                        false);
                }
                catch
                {
                }
            }
        }

        // Xử lý hậu quả khi người chơi đá trúng mục tiêu
        // trong lúc phương tiện đang chạy trên 110 km/h.
        private void ApplyHighSpeedKickConsequences(Ped player, bool hasPassenger)
        {
            if (!IsPlayerAboveHighSpeedPenaltyThreshold(player))
                return;

            bool wearingHelmet = IsPedWearingHelmet(player);

            // Hai người + có mũ: xác suất ngã xe cố định 15%.
            // Hai người + không mũ và trường hợp một người giữ nguyên xác suất cũ.
            int ragdollChance;

            if (hasPassenger && wearingHelmet)
            {
                ragdollChance = TwoPersonHelmetRagdollChance;
            }
            else if (rainOrThunderActive)
            {
                ragdollChance = wearingHelmet
                    ? HelmetWetWeatherRagdollChance
                    : WetWeatherRagdollChance;
            }
            else
            {
                ragdollChance = wearingHelmet
                    ? HelmetHighSpeedRagdollChance
                    : HighSpeedRagdollChance;
            }

            // Buff changes the high-speed fall probability to a fixed 10%.
            if (IsKickBuffActive())
                ragdollChance = KickBuffRagdollChance;

            if (soundRandom.Next(100) < ragdollChance)
            {
                // Một lần ngã xe trong chuỗi chỉ làm mất một nấc tiến trình.
                PenalizeAndDetachHighSpeedVehicleKickStreak();
                ApplyHighSpeedForwardRagdoll(player);
            }

            // Xác suất mất HP vẫn giữ nguyên 30%.
            if (soundRandom.Next(100) < HighSpeedSelfDamageChance)
            {
                int selfDamage;

                if (hasPassenger)
                {
                    selfDamage = wearingHelmet
                        ? TwoPersonHelmetSelfDamage
                        : TwoPersonNoHelmetSelfDamage;
                }
                else
                {
                    // Người đi một mình giữ nguyên 5 HP / 10 HP.
                    selfDamage = wearingHelmet
                        ? HelmetHighSpeedSelfDamage
                        : HighSpeedSelfDamage;
                }

                if (GetActiveKickRewardLevel() >= 1)
                {
                    selfDamage = (int)Math.Round(
                        selfDamage * KickRewardTier1DamageTakenMultiplier,
                        MidpointRounding.AwayFromZero);
                }

                if (IsKickBuffActive())
                {
                    selfDamage = (int)Math.Round(
                        selfDamage * KickBuffDamageTakenMultiplier,
                        MidpointRounding.AwayFromZero);
                }

                // Self-damage also consumes armor before reducing player health.
                ApplyPedDamageArmorFirst(player, selfDamage);

                nextKickAllowedTime = GetGameDeadline(
                    HighSpeedKickCooldown);
            }
        }

        // Sát thương nền lên NPC; dùng hệ số 4.85 trên 90 km/h.
        // Bonus 32% chỉ áp dụng khi player hoặc NPC ghế phụ chọn đúng weapon hash.
        private int GetPedKickDamage(Ped player, Ped passenger)
        {
            float damage = DefaultPedKickDamage;

            if (IsPlayerAboveKickDamageSpeed(player))
                damage *= HighSpeedKickDamageMultiplier;

            damage *= GetListedMeleeWeaponDamageMultiplier(player, passenger);

            return (int)Math.Round(
                damage,
                MidpointRounding.AwayFromZero);
        }

        // Các Vehicle Class được giảm một nửa sát thương.
        // Không áp dụng tăng sát thương theo tốc độ cho những class này.
        private static bool IsReducedKickDamageVehicleClass(int vehicleClass)
        {
            switch (vehicleClass)
            {
                case 14: // Boats
                case 15: // Helicopters
                case 16: // Planes
                case 18: // Emergency
                case 19: // Military
                case 20: // Commercial
                case 21: // Trains
                    return true;

                default:
                    return false;
            }
        }

        // Boats, aircraft, helicopters and trains do not use ordinary road-wheel
        // damage behavior. They receive an additional 22% body-health damage.
        private static bool IsNonWheeledVehicleClass(int vehicleClass)
        {
            switch (vehicleClass)
            {
                case 14: // Boats
                case 15: // Helicopters
                case 16: // Planes
                    return true;

                default:
                    return false;
            }
        }

        // Gây sát thương trực tiếp lên thân phương tiện.
        private void ApplyVehicleKickDamage(
            Vehicle vehicle,
            Ped player,
            float damageMultiplier = 1.0f,
            bool specialKick = false,
            Ped passenger = null,
            float specialKickDamageMultiplier = 1.0f)
        {
            if (vehicle == null ||
                !vehicle.Exists() ||
                vehicle.IsDead)
            {
                return;
            }

            if (player == null || !player.Exists())
                return;

            // Không gây sát thương lên phương tiện người chơi đang sử dụng.
            if (player.IsInVehicle())
            {
                Vehicle playerVehicle = player.CurrentVehicle;

                if (playerVehicle != null &&
                    playerVehicle.Exists() &&
                    playerVehicle.Handle == vehicle.Handle)
                {
                    return;
                }
            }

            int vehicleClass;

            try
            {
                vehicleClass = GetVehicleClassCached(vehicle);
            }
            catch
            {
                return;
            }

            float damage;

            if (specialKick)
            {
                // Special Kick keeps its existing x8 base; Buff's x1.96 is supplied
                // through damageMultiplier after the special-kick override.
                damage = DefaultVehicleKickDamage * 8.0f *
                         specialKickDamageMultiplier *
                         Math.Max(1.0f, damageMultiplier);

                // Wearing a helmet adds +46% on top of the existing x8 Special Kick.
                if (IsPedWearingHelmet(player))
                    damage *= HelmetSpecialKickDamageMultiplier;
            }
            else
            {
                damage = DefaultVehicleKickDamage;

                // Class đặc biệt: chỉ nhận 50% sát thương mặc định.
                // Tuyệt đối không nhân thêm sát thương theo tốc độ.
                if (IsReducedKickDamageVehicleClass(vehicleClass))
                {
                    damage *= 0.5f;
                }
                // Phương tiện thông thường: nếu trên 90 km/h thì nhân sát thương x4.85.
                else if (IsPlayerAboveKickDamageSpeed(player))
                {
                    damage *= HighSpeedKickDamageMultiplier;
                }

                // Existing co-op multiplier and any independent Buff multiplier.
                damage *= Math.Max(1.0f, damageMultiplier);
            }

            // Weapon bonus modifies the base vehicle damage too, before Buff's bonus.
            damage *= GetListedMeleeWeaponDamageMultiplier(player, passenger);

            // A Buffed hit deals a further 22% damage to the struck vehicle.
            if (IsKickBuffActive())
                damage *= KickBuffTargetDamageBonusMultiplier;

            // Non-road-wheel vehicles (boats/aircraft/helicopters/trains) take
            // another 22% body-health damage on top of all existing multipliers.
            if (IsNonWheeledVehicleClass(vehicleClass))
                damage *= 1.22f;

            try
            {
                float currentBodyHealth = vehicle.BodyHealth;

                if (currentBodyHealth <= 0f)
                    return;

                vehicle.BodyHealth = Math.Max(
                    0f,
                    currentBodyHealth - damage);
            }
            catch
            {
            }
        }

        // Mỗi lần đá trực tiếp vào phương tiện có 5% cơ hội làm hỏng ngẫu nhiên
        // một bộ phận: nổ lốp, làm rụng một cửa hoặc gây hư hại cụm gương bên.
        // Không gọi hàm này trong nhánh đá Ped (kể cả Ped đang ngồi trên xe).
        private void ApplyRandomVehicleComponentDamage(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists() || vehicle.IsDead)
                return;

            int componentDamageChance = IsKickBuffActive()
                ? KickBuffVehicleComponentDamageChance
                : RandomVehicleComponentDamageChance;

            if (soundRandom.Next(100) >= componentDamageChance)
                return;

            int damageType = soundRandom.Next(3);

            try
            {
                if (damageType == 0)
                {
                    // Chỉ chọn các chỉ số bánh phổ biến hợp lệ cho xe máy/xe đạp
                    // hoặc ô tô 4 bánh để tránh gọi vào vị trí bánh không tồn tại.
                    bool isBike = IsMotorcycleOrCycle(vehicle);
                    int tyreRoll = soundRandom.Next(isBike ? 2 : 4);
                    int tyreIndex;

                    if (isBike)
                    {
                        tyreIndex = tyreRoll == 0 ? 0 : 4;
                    }
                    else
                    {
                        switch (tyreRoll)
                        {
                            case 0: tyreIndex = 0; break;
                            case 1: tyreIndex = 1; break;
                            case 2: tyreIndex = 4; break;
                            default: tyreIndex = 5; break;
                        }
                    }

                    Function.Call(
                        Native(unchecked((long)0xEC6A202EE4960385UL)),
                        vehicle.Handle,
                        tyreIndex,
                        true,
                        1000.0f);

                    return;
                }

                if (damageType == 1)
                {
                    int doorCount = Function.Call<int>(
                        Native(unchecked((long)0x92922A607497B14DUL)),
                        vehicle.Handle);

                    int validDoorCount = 0;
                    int maxDoorIndex = Math.Min(doorCount, validVehicleDoorScratch.Length);

                    for (int doorIndex = 0; doorIndex < maxDoorIndex; doorIndex++)
                    {
                        bool isDoorValid = Function.Call<bool>(
                            Native(unchecked((long)0x645F4B6E8499F632UL)),
                            vehicle.Handle,
                            doorIndex);

                        if (isDoorValid)
                            validVehicleDoorScratch[validDoorCount++] = doorIndex;
                    }

                    if (validDoorCount > 0)
                    {
                        int doorIndex = validVehicleDoorScratch[
                            soundRandom.Next(validDoorCount)];

                        Function.Call(
                            Native(unchecked((long)0xD4D4F6A4AB575A33UL)),
                            vehicle.Handle,
                            doorIndex,
                            true);

                        return;
                    }

                    // Xe không có cửa (ví dụ xe máy): chuyển sang nổ một lốp.
                    int fallbackTyre = IsMotorcycleOrCycle(vehicle) ? 4 : 0;
                    Function.Call(
                        Native(unchecked((long)0xEC6A202EE4960385UL)),
                        vehicle.Handle,
                        fallbackTyre,
                        true,
                        1000.0f);
                    return;
                }

                // Tác động tập trung tại vị trí gương chiếu hậu trái/phải.
                // Với model có component gương detachable, damage native sẽ làm gương gãy/rời.
                Vector3 modelMin;
                Vector3 modelMax;
                vehicle.Model.GetDimensions(out modelMin, out modelMax);

                float mirrorSide = soundRandom.Next(2) == 0 ? -1.0f : 1.0f;
                float mirrorX = mirrorSide * Math.Max(
                    Math.Abs(modelMin.X),
                    Math.Abs(modelMax.X)) * 0.92f;
                float mirrorY = modelMax.Y * 0.48f;
                float mirrorZ = modelMin.Z +
                    (modelMax.Z - modelMin.Z) * 0.68f;

                Function.Call(
                    Native(unchecked((long)0xA1DD317EA8FD4F29UL)),
                    vehicle.Handle,
                    mirrorX,
                    mirrorY,
                    mirrorZ,
                    1000.0f,
                    0.20f,
                    true);
            }
            catch
            {
                // Nếu model không hỗ trợ cửa/gương, vẫn cố gắng làm thủng lốp.
                try
                {
                    int fallbackTyre = IsMotorcycleOrCycle(vehicle) ? 4 : 0;
                    Function.Call(
                        Native(unchecked((long)0xEC6A202EE4960385UL)),
                        vehicle.Handle,
                        fallbackTyre,
                        true,
                        1000.0f);
                }
                catch
                {
                }
            }
        }

        // Kích hoạt cú đánh lái mạnh lên xe của NPC thay cho cú hất văng.
        // SET_VEHICLE_STEER_BIAS: +1 = trái, -1 = phải.
        private bool StartNpcVictimBikeSteeringEffect(
            Ped target,
            Vehicle bike,
            float kickForce)
        {
            if (target == null || !target.Exists() || !target.IsAlive ||
                !target.IsInVehicle() ||
                bike == null || !bike.Exists() || bike.IsDead ||
                !IsMotorcycleOrCycle(bike) ||
                target.CurrentVehicle == null ||
                !target.CurrentVehicle.Exists() ||
                target.CurrentVehicle.Handle != bike.Handle)
            {
                return false;
            }

            // Có thể mục tiêu bị đá là tài xế hoặc người ngồi sau; steering
            // luôn được gán cho tài xế thực sự của chiếc xe đó.
            Ped driver = bike.GetPedOnSeat(VehicleSeat.Driver);

            if (driver == null || !driver.Exists() || !driver.IsAlive ||
                !driver.IsInVehicle() ||
                driver.CurrentVehicle == null ||
                !driver.CurrentVehicle.Exists() ||
                driver.CurrentVehicle.Handle != bike.Handle ||
                !IsPedDrivingVehicle(driver, bike))
            {
                return false;
            }

            float targetBias = kickForce < 0f
                ? NpcVictimBikeSteerMagnitude
                : -NpcVictimBikeSteerMagnitude;

            // Nếu cùng NPC/xe đã có hiệu ứng, thay hướng và làm mới thời gian.
            for (int i = activeNpcVictimSteeringEffects.Count - 1; i >= 0; i--)
            {
                NpcVictimSteeringData existing = activeNpcVictimSteeringEffects[i];

                if (existing == null || existing.Vehicle == null ||
                    !existing.Vehicle.Exists())
                {
                    activeNpcVictimSteeringEffects.RemoveAt(i);
                    continue;
                }

                if (existing.Vehicle.Handle == bike.Handle)
                {
                    existing.Driver = driver;
                    existing.CurrentBias = 0f;
                    existing.TargetBias = targetBias;
                    existing.EndTime = GetGameDeadline(
                        NpcVictimBikeSteerDuration);
                    existing.NextValidationTime = -1;
                    return true;
                }
            }

            NpcVictimSteeringData data = new NpcVictimSteeringData();
            data.Driver = driver;
            data.Vehicle = bike;
            data.CurrentBias = 0f;
            data.TargetBias = targetBias;
            data.EndTime = GetGameDeadline(NpcVictimBikeSteerDuration);
            data.NextValidationTime = -1;
            activeNpcVictimSteeringEffects.Add(data);

            return true;
        }

        // Duy trì steering từng frame, tương tự hiệu ứng INPUT_MULTIPLAYER_INFO
        // của người chơi. Khi hết thời gian, bias giảm mượt về 0 rồi giải phóng.
        private void UpdateNpcVictimSteeringEffects()
        {
            for (int i = activeNpcVictimSteeringEffects.Count - 1; i >= 0; i--)
            {
                NpcVictimSteeringData data = activeNpcVictimSteeringEffects[i];

                if (data == null)
                {
                    activeNpcVictimSteeringEffects.RemoveAt(i);
                    continue;
                }

                Ped driver = data.Driver;
                Vehicle bike = data.Vehicle;

                // Entity/seat ownership checks are the expensive part; perform
                // them every 100 ms while applying the interpolated bias each frame.
                if (data.NextValidationTime < 0 ||
                    IsGameTimeReached(data.NextValidationTime))
                {
                    data.NextValidationTime = GetGameDeadline(
                        SteeringEntityValidationInterval);

                    if (driver == null || !driver.Exists() || !driver.IsAlive ||
                        bike == null || !bike.Exists())
                    {
                        if (bike != null && bike.Exists())
                            ResetNpcVictimVehicleSteering(bike);

                        activeNpcVictimSteeringEffects.RemoveAt(i);
                        continue;
                    }

                    bool stillDrivingOriginalBike = false;

                    try
                    {
                        stillDrivingOriginalBike = driver.IsInVehicle() &&
                            driver.CurrentVehicle != null &&
                            driver.CurrentVehicle.Exists() &&
                            driver.CurrentVehicle.Handle == bike.Handle &&
                            IsPedDrivingVehicle(driver, bike);
                    }
                    catch
                    {
                        stillDrivingOriginalBike = false;
                    }

                    if (!stillDrivingOriginalBike)
                    {
                        ResetNpcVictimVehicleSteering(bike);
                        activeNpcVictimSteeringEffects.RemoveAt(i);
                        continue;
                    }
                }

                if (data.EndTime >= 0 && IsGameTimeReached(data.EndTime))
                    data.TargetBias = 0f;

                data.CurrentBias +=
                    (data.TargetBias - data.CurrentBias) * PlayerKickSteerInterpolation;

                if (data.TargetBias == 0f && Math.Abs(data.CurrentBias) < 0.001f)
                {
                    ResetNpcVictimVehicleSteering(bike);
                    activeNpcVictimSteeringEffects.RemoveAt(i);
                    continue;
                }

                try
                {
                    Function.Call(
                        Hash.SET_VEHICLE_STEER_BIAS,
                        bike.Handle,
                        data.CurrentBias);
                }
                catch
                {
                }
            }
        }

        private static void ResetNpcVictimVehicleSteering(Vehicle bike)
        {
            if (bike == null || !bike.Exists())
                return;

            try
            {
                Function.Call(
                    Hash.SET_VEHICLE_STEER_BIAS,
                    bike.Handle,
                    0.0f);
            }
            catch
            {
            }
        }

        private void ClearNpcVictimSteeringEffects()
        {
            for (int i = activeNpcVictimSteeringEffects.Count - 1; i >= 0; i--)
            {
                NpcVictimSteeringData data = activeNpcVictimSteeringEffects[i];

                if (data != null && data.Vehicle != null && data.Vehicle.Exists())
                    ResetNpcVictimVehicleSteering(data.Vehicle);
            }

            activeNpcVictimSteeringEffects.Clear();
        }

        // Bắt đầu hiệu ứng lệch bánh lái cho xe người chơi.
        //
        // force < 0: đá chân trái -> lệch phải.
        // force > 0: đá chân phải -> lệch trái.
        private void ApplyPlayerKickSteeringEffect(
            Ped player,
            Vehicle hitVehicle,
            float force,
            bool hasPassenger)
        {
            if (player == null ||
                !player.Exists() ||
                hitVehicle == null ||
                !hitVehicle.Exists())
            {
                return;
            }

            if (!player.IsInVehicle())
                return;

            Vehicle playerVehicle = player.CurrentVehicle;

            if (playerVehicle == null ||
                !playerVehicle.Exists())
            {
                return;
            }

            // Không tác động lên xe đang bị người chơi điều khiển.
            if (playerVehicle.Handle == hitVehicle.Handle)
                return;

            // Chỉ áp dụng khi người chơi thực sự là tài xế.
            try
            {
                Ped driver = playerVehicle.GetPedOnSeat(VehicleSeat.Driver);

                if (driver == null ||
                    !driver.Exists() ||
                    driver.Handle != player.Handle)
                {
                    return;
                }
            }
            catch
            {
                return;
            }

            // Nếu chuyển sang một phương tiện khác,
            // bỏ trạng thái điều khiển của phương tiện cũ.
            if (playerKickSteerVehicle != null &&
                (!playerKickSteerVehicle.Exists() ||
                 playerKickSteerVehicle.Handle != playerVehicle.Handle))
            {
                ClearPlayerKickSteeringEffect();
            }

            playerKickSteerVehicle = playerVehicle;
            nextPlayerKickSteerValidationTime = -1;

            // SET_VEHICLE_STEER_BIAS:
            // -1 = lệch phải.
            // +1 = lệch trái.
            float steerMagnitude = rainOrThunderActive
                ? WetWeatherKickSteerMagnitude
                : PlayerKickSteerBiasMagnitude;

            // Off-road: một người giảm 30%; hai người giảm 50%.
            if (IsPlayerOnOffRoadTerrain(player))
            {
                steerMagnitude *= hasPassenger
                    ? TwoPersonOffRoadKickMultiplier
                    : OffRoadVehicleKickMultiplier;
            }

            // Cấp 1 trở lên giảm 20% phản chấn lái khi người chơi đá ở >= 90 km/h.
            if (GetActiveKickRewardLevel() >= 1 &&
                IsPlayerAboveKickDamageSpeed(player))
            {
                steerMagnitude *= KickRewardTier1RecoilMultiplier;
            }

            targetPlayerKickSteerBias = force < 0f
                ? -steerMagnitude
                : steerMagnitude;

            // Duy trì hiệu ứng trong một khoảng thời gian cố định.
            playerKickSteerEndTime = GetGameDeadline(PlayerKickSteerHoldDuration);
        }

        // Cập nhật hiệu ứng lệch bánh lái từng frame.
        private void UpdatePlayerKickSteeringEffect()
        {
            Vehicle steeringVehicle = playerKickSteerVehicle;

            if (steeringVehicle == null)
                return;

            // Revalidate the driver/vehicle relationship at 10 Hz. The steering
            // native and interpolation below still run every rendered frame.
            if (nextPlayerKickSteerValidationTime < 0 ||
                IsGameTimeReached(nextPlayerKickSteerValidationTime))
            {
                nextPlayerKickSteerValidationTime = GetGameDeadline(
                    SteeringEntityValidationInterval);

                if (!steeringVehicle.Exists())
                {
                    ClearPlayerKickSteeringEffect();
                    return;
                }

                Ped player = Game.Player.Character;

                if (player == null ||
                    !player.Exists() ||
                    !player.IsInVehicle())
                {
                    ClearPlayerKickSteeringEffect();
                    return;
                }

                Vehicle currentVehicle = player.CurrentVehicle;

                if (currentVehicle == null ||
                    !currentVehicle.Exists() ||
                    currentVehicle.Handle != steeringVehicle.Handle)
                {
                    ClearPlayerKickSteeringEffect();
                    return;
                }

                try
                {
                    Ped driver = currentVehicle.GetPedOnSeat(VehicleSeat.Driver);

                    if (driver == null ||
                        !driver.Exists() ||
                        driver.Handle != player.Handle)
                    {
                        ClearPlayerKickSteeringEffect();
                        return;
                    }
                }
                catch
                {
                    ClearPlayerKickSteeringEffect();
                    return;
                }
            }

            if (playerKickSteerEndTime >= 0 &&
                IsGameTimeReached(playerKickSteerEndTime))
            {
                targetPlayerKickSteerBias = 0f;
            }

            currentPlayerKickSteerBias +=
                (targetPlayerKickSteerBias -
                 currentPlayerKickSteerBias) *
                PlayerKickSteerInterpolation;

            if (targetPlayerKickSteerBias == 0f &&
                Math.Abs(currentPlayerKickSteerBias) < 0.001f)
            {
                ClearPlayerKickSteeringEffect();
                return;
            }

            try
            {
                Function.Call(
                    Hash.SET_VEHICLE_STEER_BIAS,
                    steeringVehicle.Handle,
                    currentPlayerKickSteerBias);
            }
            catch
            {
            }
        }

        // Dọn trạng thái nội bộ.
        // Native sẽ tự giải phóng steering khi không còn được gọi.
        private void ClearPlayerKickSteeringEffect()
        {
            playerKickSteerVehicle = null;
            nextPlayerKickSteerValidationTime = -1;

            currentPlayerKickSteerBias = 0f;
            targetPlayerKickSteerBias = 0f;

            playerKickSteerEndTime = -1;
        }

        // Keep both progress bars rendered during short waits without changing
        // any of the existing steering, streak, or reward update behavior.
        private void WaitWhileDrawingKickProgressHuds(int duration)
        {
            int deadline = GetGameDeadline(duration);

            while (!IsGameTimeReached(deadline))
            {
                UpdateKickBuffCycle();
                DrawKickProgressHuds(Game.Player.Character);
                Wait(0);
            }
        }

        // Wait nhưng vẫn duy trì việc cập nhật steering từng frame.
        // Cần thiết vì OnTick hiện tại sử dụng Wait(500) sau khi đá.
        private void WaitWhileUpdatingKickSteering(int duration)
        {
            int deadline = GetGameDeadline(duration);

            while (!IsGameTimeReached(deadline))
            {
                UpdatePlayerKickSteeringEffect();
                UpdateNpcVictimSteeringEffects();
                UpdateHighSpeedVehicleKickStreakState(Game.Player.Character);
                UpdateKickRewardCycle();
                UpdateKickBuffCycle();
                DrawKickProgressHuds(Game.Player.Character);

                Wait(0);
            }
        }

        private void KickVehicle(
            Vehicle vehicle,
            float force,
            float damageMultiplier,
            float forceMultiplier,
            bool hasPassenger,
            bool specialKick,
            Ped kickSource = null,
            bool independentPassengerKick = false,
            float specialKickDamageMultiplier = 1.0f)
        {
            Ped player = Game.Player.Character;
            Ped attacker = kickSource != null && kickSource.Exists()
                ? kickSource
                : player;

            if (player == null || !player.Exists() ||
                attacker == null || !attacker.Exists() ||
                vehicle == null || !vehicle.Exists())
            {
                return;
            }

            if (vehicle.IsDead)
                return;

            // Đã đá trúng phương tiện hợp lệ.
            RegisterFirstKickHelp();

            // Không tác động lên phương tiện người chơi đang sử dụng.
            if (player.IsInVehicle())
            {
                Vehicle currentVehicle = player.CurrentVehicle;

                if (currentVehicle != null &&
                    currentVehicle.Exists() &&
                    currentVehicle.Handle == vehicle.Handle)
                {
                    return;
                }
            }

            // Âm thanh vẫn được phát cho tất cả các phương tiện.
            PlayVehicleKickSound();

            // Camera shake khi đá phương tiện hạng nặng
            // ở tốc độ trên 90 km/h.
            ApplyVehicleKickCameraShake(vehicle, player);

            // Ghi nhận một lần quấy rầy thành công.
            RegisterDisturbance();

            // Chỉ cú đá trực tiếp của người chơi mới tăng sao khi trúng
            // phương tiện Emergency / Military. Cú đá độc lập của NPC ghế phụ
            // vẫn đi qua RegisterDisturbance() ở trên như logic hiện tại.
            if (!independentPassengerKick)
                IncreaseWantedLevelForSpecialVehicle(vehicle);

            // Kích hoạt NPC lái xe truy đuổi người chơi.
            // Các Vehicle Class bị loại trừ sẽ không bị ảnh hưởng.
            StartVehicleChase(vehicle, hasPassenger);

            // Gây sát thương lên phương tiện bị đá.
            Ped passengerKicker = independentPassengerKick
                ? attacker
                : (hasPassenger && recruitedPassenger != null
                    ? recruitedPassenger.Passenger
                    : null);
            ApplyVehicleKickDamage(
                vehicle,
                player,
                damageMultiplier,
                specialKick,
                passengerKicker,
                specialKickDamageMultiplier);

            // Chỉ đá trực tiếp phương tiện mới có 5% cơ hội làm hỏng lốp/cửa/gương.
            ApplyRandomVehicleComponentDamage(vehicle);

            // Đồng thời tạo hiệu ứng lệch bánh lái
            // trên phương tiện người chơi đang điều khiển.
            ApplyPlayerKickSteeringEffect(player, vehicle, force, hasPassenger);

            Vector3 worldDifference = attacker.Position - vehicle.Position;

            Vector3 vehicleRight = vehicle.RightVector;
            Vector3 vehicleForward = vehicle.ForwardVector;
            Vector3 vehicleUp = vehicle.UpVector;

            Vector3 playerOffset = new Vector3(
                Vector3.Dot(worldDifference, vehicleRight),
                Vector3.Dot(worldDifference, vehicleForward),
                Vector3.Dot(worldDifference, vehicleUp));

            Vector3 modelMin;
            Vector3 modelMax;

            vehicle.Model.GetDimensions(out modelMin, out modelMax);

            float contactX = playerOffset.X >= 0f
                ? modelMax.X * 0.85f
                : modelMin.X * 0.85f;

            float contactY = Math.Max(modelMin.Y * 0.70f, Math.Min(modelMax.Y * 0.70f, playerOffset.Y));
            float contactZ = modelMin.Z + (modelMax.Z - modelMin.Z) * 0.35f;

            Vector3 contactOffset = new Vector3(contactX, contactY, contactZ);

            // Phân biệt lực tác động theo Vehicle Class.
            bool lightKick = IsHeavyOrSpecialVehicleClass(vehicle);

            // Giữ nguyên lực đá mặc định.
            float kickStrength = lightKick ? 0.025f : 0.22f;

            // Giữ nguyên lực ngang.
            Vector3 lateralForce = attacker.RightVector * (force * kickStrength);

            // Bình thường: NPC ghế phụ tăng lực vật lý lên 1.36 lần.
            // Cú đặc biệt đã nhận forceMultiplier = 4 và không cộng thêm hệ số nào.
            lateralForce *= forceMultiplier;

            Vector3 upwardForce = Vector3.WorldUp * (lightKick ? 0.15f : 1.5f);
            upwardForce *= forceMultiplier;

            // Kiểm tra địa hình off-road.
            bool offRoadKick = IsPlayerOnOffRoadTerrain(player);

            // Một người off-road: giảm 30% lực như hiện tại.
            // Hai người off-road: giảm 50% lực trên cả hai trục vật lý.
            if (offRoadKick && !specialKick)
            {
                float offRoadForceMultiplier = hasPassenger
                    ? TwoPersonOffRoadKickMultiplier
                    : OffRoadVehicleKickMultiplier;

                lateralForce *= offRoadForceMultiplier;
                upwardForce *= offRoadForceMultiplier;
            }

            // Giữ nguyên cơ chế tác động lệch tâm tạo mô-men xoay.
            vehicle.ApplyForce(lateralForce + upwardForce, contactOffset, ForceType.ExternalImpulse);

            // The passenger's autonomous Buff attacks pass independentPassengerKick=true,
            // so only the player's direct kick can create the explosive impact.
            if (!independentPassengerKick)
            {
                ApplyKickBuffExplosiveMeleeImpact(
                    player,
                    vehicle.Position + Vector3.WorldUp * 0.5f);
            }

            // A direct player vehicle kick counts twice when the recruited passenger
            // is actually seated. Autonomous passenger attacks retain one hit.
            RegisterKickBuffHit(
                independentPassengerKick ? 1 : (hasPassenger ? 2 : 1));

            // Ghi nhận thành tích trước hậu quả tự gây ra (có thể khiến người chơi ngã xe).
            RegisterKickRewardHit(player);

            // High-speed self-penalty and the Special-Kick streak belong to
            // the player-triggered action, not the passenger's independent kick.
            if (!independentPassengerKick)
            {
                ApplyHighSpeedKickConsequences(player, hasPassenger);
                RegisterHighSpeedVehicleKick(player, specialKick);

                if (specialKick)
                    CompleteHighSpeedSpecialKick();
            }
        }

        private void PlayAnim(Ped ped, string animDict, string animFile, int duration, int animFlag)
        {
            Function.Call(
                Native(-3189321952077349130L),
                animDict);

            int timeout = GetGameDeadline(1000);

            while (!Function.Call<bool>(Native(-3444786327252301684L), animDict))
            {
                if (IsGameTimeReached(timeout))
                    return;

                DrawKickProgressHuds(Game.Player.Character);
                Yield();
            }

            Function.Call(
                Native(-1565002832890405996L),
                ped,
                animDict,
                animFile,
                8f,
                -4f,
                duration,
                animFlag,
                0.0f,
                false,
                false,
                false);
        }

        private static void GiveNMMessage(Ped ped, int nmMessage, int duration)
        {
            ped.CanRagdoll = true;

            Function.Call(
                Native(-5865380420870110134L),
                ped,
                duration,
                duration,
                1,
                false,
                false,
                false);

            Function.Call(Native(4723979835631036037L), 1, nmMessage);
            Function.Call(Native(-5667534060467102629L), ped);
        }

        private void CleanupWeaponProp()
        {
            Prop prop = bikeMeleeWpnProp;

            if (prop == null)
                return;

            bikeMeleeWpnProp = null;

            if (prop.Exists())
                prop.Delete();
        }

        private void OnAborted(object sender, EventArgs e)
        {
            // Never leave the player or his vehicle invincible after script unload.
            RestoreKickBuffTemporaryInvincibility();
            kickBuffDrainStartTime = -1;

            ClearPlayerKickSteeringEffect();
            ClearNpcVictimSteeringEffects();

            // Khôi phục camera nếu script bị dừng giữa hiệu ứng.
            StopVehicleKickCameraShake();

            // Khôi phục độ bám đường khi script bị dừng.
            RestoreWeatherGrip();

            CleanupWeaponProp();
            CleanupPassengerWeaponProp();

            // Trả NPC ghế phụ về AI bình thường khi script bị dừng/gỡ.
            ReleaseRecruitedPassenger();
            FinalizePassengerExitTasks();

            // Khôi phục các NPC đang trong trạng thái truy đuổi
            // khi script bị dừng hoặc bị gỡ khỏi game.
            foreach (VehicleChaseData data in activeVehicleChases)
            {
                if (data == null)
                    continue;

                // Restore vehicle handling even if its driver already died or
                // disappeared before the script was aborted.
                RestorePanicFleeSkid(data);

                if (data.Driver == null || !data.Driver.Exists())
                    continue;

                Ped driver = data.Driver;

                driver.Task.ClearAll();

                if (data.IsArmedChasing)
                {
                    RestoreTemporaryRetaliationWeapon(driver, data);
                }

                driver.AlwaysKeepTask = false;
                driver.BlockPermanentEvents = false;
                driver.IsPersistent = false;

                Function.Call(Hash.SET_DRIVER_ABILITY, driver, 0.5f);
                Function.Call(Hash.SET_DRIVER_AGGRESSIVENESS, driver, 0.0f);
            }

            activeVehicleChases.Clear();

            ResetHighSpeedVehicleKickStreak();
            kickRewardHitCount = 0;
            kickRewardWindowEndTime = -1;
            kickRewardEndTime = -1;
            activeKickRewardLevel = 0;

            kickBuffProgress = 0.0f;
            kickBuffActive = false;
            kickBuffDrainStarted = false;
            kickBuffAccumulationStartTime = -1;
            kickBuffAccumulationLimitExceeded = false;
            kickBuffActiveDuration = KickBuffDefaultTotalActiveDuration;
            kickBuffHothandsDuration = KickBuffDefaultHothandsDuration;
            kickBuffInvincibilityDuration = KickBuffDefaultInvincibilityDuration;
            kickBuffHelpEndTime = -1;
            kickBuffHelpMessage = null;
            kickBuffLastUpdateTime = -1;
            kickBuffDrainStartTime = -1;
            kickBuffFlashActive = false;
            kickBuffFlashCount = 0;
            kickBuffFlashAlpha = 0;
            kickBuffFlashLastTime = -1;

            nextNpcBikeKickScanTime = -1;
            nextNpcBikeKickCooldownCleanupTime = -1;
            nextBuffPassengerKickScanTime = -1;
            nextBuffPassengerKickAllowedTime = -1;
            bikeWeaponSelectionInitialized = false;
            bikeWeaponPermissionsApplied = false;
            nextBikeWeaponPermissionRefreshTime = -1;
            nextOnFootWeaponSnapshotTime = -1;
            nextPassengerWeaponSyncCheckTime = -1;
            cachedPassengerCompanionForWeaponSync = false;
            lastSynchronizedPassengerWeaponHandle = 0;
            lastOnFootWeaponHash = UnarmedWeaponHash;
            onFootWeaponSnapshotInitialized = false;
            bikeEntryWeaponHash = UnarmedWeaponHash;
            bikeEntryWeaponSnapshotInitialized = false;
            wasRidingMotorcycleOrCycle = false;
            specialKickHudTextureRequested = false;
            specialKickHudTextureLoaded = false;
            nextWeatherGripStateCheckTime = -1;
            nextRecruitedPassengerUpdateTime = -1;
            nextPlayerKickSteerValidationTime = -1;
            kickBuffNextProtectionRefreshTime = -1;
            nextVehicleChaseUpdateTime = -1;
            nextPassengerExitUpdateTime = -1;
            nextPassengerTyreAttackAllowedTime = -1;
            npcBikeKickCooldowns.Clear();
            expiredNpcBikeKickHandles.Clear();

            currentPlayingSound = null;

            if (bikeHitSounds != null)
            {
                for (int i = 0; i < bikeHitSounds.Length; i++)
                {
                    if (bikeHitSounds[i] != null)
                    {
                        try
                        {
                            bikeHitSounds[i].Stop();
                            bikeHitSounds[i].Dispose();
                        }
                        catch
                        {
                        }

                        bikeHitSounds[i] = null;
                    }
                }
            }

            availableSoundIndices = Array.Empty<int>();

            if (bikeHitSoundStreams != null)
            {
                for (int i = 0; i < bikeHitSoundStreams.Length; i++)
                {
                    if (bikeHitSoundStreams[i] != null)
                    {
                        bikeHitSoundStreams[i].Dispose();
                        bikeHitSoundStreams[i] = null;
                    }
                }
            }

            bikeHitSounds = null;
            bikeHitSoundStreams = null;
        }
    }
}