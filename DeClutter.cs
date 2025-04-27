using BepInEx;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.AssetsManager;
using EFT.Ballistics;
using EFT.Interactive;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using Koenigz.PerfectCulling.EFT;
using BepInEx.Logging;
using System.Runtime.Remoting.Messaging;

namespace TYR_DeClutterer
{
    [BepInPlugin("com.TYR.DeClutter", "TYR_DeClutter", "1.1.5")]
    public class DeClutter : BaseUnityPlugin
    {
        private static GameWorld gameWorld;
        public static bool MapLoaded() => Singleton<GameWorld>.Instantiated;
        private List<GameObject> allGameObjectsList = new List<GameObject>();
        public static List<GameObject> savedClutterObjects = new List<GameObject>();
        public static Player Player;
        private bool deCluttered = false;
        public static ConfigEntry<bool> declutterEnabledConfig;
        public static ConfigEntry<bool> declutterGarbageEnabledConfig;
        public static ConfigEntry<bool> declutterHeapsEnabledConfig;
        public static ConfigEntry<bool> declutterSpentCartridgesEnabledConfig;
        public static ConfigEntry<bool> declutterFakeFoodEnabledConfig;
        public static ConfigEntry<bool> declutterDecalsEnabledConfig;
        public static ConfigEntry<bool> declutterPuddlesEnabledConfig;
        public static ConfigEntry<bool> declutterShardsEnabledConfig;
        public static ConfigEntry<bool> declutterUnscrutinizedEnabledConfig;
        public static ConfigEntry<float> declutterScaleOffsetConfig;     
        public static bool applyDeclutter = false;
        public static bool defaultsoftParticles = QualitySettings.softParticles;
        public static int defaultparticleRaycastBudget = QualitySettings.particleRaycastBudget;
        public static bool defaultsoftVegetation = QualitySettings.softVegetation;
        public static bool defaultrealtimeReflectionProbes = QualitySettings.realtimeReflectionProbes;
        public static int defaultpixelLightCount = QualitySettings.pixelLightCount;
        public static ShadowQuality defaultShadows = QualitySettings.shadows;
        public static int defaultshadowCascades = QualitySettings.shadowCascades;
        public static int defaultmasterTextureLimit = QualitySettings.masterTextureLimit;
        public static float defaultlodBias = QualitySettings.lodBias;
        public static ManualLogSource LogSource;

        private void Awake()
        {
            LogSource = Logger;
            LogSource.LogInfo("Declutter plugin loaded!");

            SceneManager.sceneUnloaded += OnSceneUnloaded;
            declutterEnabledConfig = Config.Bind("A - De-Clutter Enabler", "A - De-Clutterer Enabled", true, "Enables the De-Clutterer");
            applyDeclutter = declutterEnabledConfig.Value;
            declutterScaleOffsetConfig = Config.Bind<float>("A - De-Clutter Enabler", "B - De-Clutterer Scaler", 1f, new BepInEx.Configuration.ConfigDescription("Larger Scale = Larger the Clutter Removed.", new BepInEx.Configuration.AcceptableValueRange<float>(0.5f, 2f)));
            declutterGarbageEnabledConfig = Config.Bind("B - De-Clutter Settings", "A - Garbage & Litter De-Clutter", true, "De-Clutters things labeled 'garbage' or similar. Smaller garbage piles.");
            declutterHeapsEnabledConfig = Config.Bind("B - De-Clutter Settings", "B - Heaps & Piles De-Clutter", true, "De-Clutters things labeled 'heaps' or similar. Larger garbage piles.");
            declutterSpentCartridgesEnabledConfig = Config.Bind("B - De-Clutter Settings", "C - Spent Cartridges De-Clutter", true, "De-Clutters pre-generated spent ammunition on floor.");
            declutterFakeFoodEnabledConfig = Config.Bind("B - De-Clutter Settings", "D - Fake Food De-Clutter", true, "De-Clutters fake 'food' items.");
            declutterDecalsEnabledConfig = Config.Bind("B - De-Clutter Settings", "E - Decal De-Clutter", true, "De-Clutters decals (Blood, grafiti, etc.)");
            declutterPuddlesEnabledConfig = Config.Bind("B - De-Clutter Settings", "F - Puddle De-Clutter", true, "De-Clutters fake reflective puddles.");  
            declutterShardsEnabledConfig = Config.Bind("B - De-Clutter Settings", "G - Glass & Tile Shards", true, "De-Clutters things labeled 'shards' or similar. The things you can step on that make noise.");
            declutterUnscrutinizedEnabledConfig = Config.Bind("B - De-Clutter Settings", "H - Experimental Unscrutinized Disabler", false, "De-Clutters literally everything that doesn't have a collider, doesn't chare what the name is or the group is so above enablers will have no effect. It'll disable it all. Experimental, testing however has had positive results. Massively improves FPS.");
            InitializeClutterNames();

            // Register the SettingChanged event
            declutterEnabledConfig.SettingChanged += OnApplyDeclutterSettingChanged;

        }
        private void Update()
        {
            if (!MapLoaded() || deCluttered || !declutterEnabledConfig.Value)
                return;

            gameWorld = Singleton<GameWorld>.Instance;
            if (gameWorld == null || gameWorld.MainPlayer == null || IsInHideout())
                return;

            deCluttered = true;
            DeClutterScene();
            DeClutterVisuals();
        }
        private void OnApplyDeclutterSettingChanged(object sender, EventArgs e)
        {
            applyDeclutter = declutterEnabledConfig.Value;
            if (deCluttered)
            {
                if (applyDeclutter)
                {
                    DeClutterEnabled();
                }
                else
                {
                    ReClutterEnabled();
                }
            }

        }
        private void OnSceneUnloaded(Scene scene)
        {
            allGameObjectsList.Clear();
            savedClutterObjects.Clear();
            deCluttered = false;
        }
        private bool IsInHideout()
        {
            // Check if "bunker_2" is one of the active scene names
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.name == "bunker_2")
                {
                    //EFT.UI.ConsoleScreen.LogError("bunker_2 loaded, not running de-cluttering.");
                    return true;
                }
            }
            //EFT.UI.ConsoleScreen.LogError("bunker_2 not loaded, de-cluttering.");
            return false;
        }
        private void DeClutterVisuals()
        {
                
            // QualitySettings.softParticles = defaultsoftParticles;
            // QualitySettings.particleRaycastBudget = defaultparticleRaycastBudget;
            // QualitySettings.softVegetation = defaultsoftVegetation;
            // QualitySettings.realtimeReflectionProbes = defaultrealtimeReflectionProbes;
            // QualitySettings.pixelLightCount = defaultpixelLightCount;
            // QualitySettings.shadows = defaultShadows;
            // QualitySettings.shadowCascades = defaultshadowCascades;
            // QualitySettings.masterTextureLimit = defaultmasterTextureLimit;
            // QualitySettings.lodBias = defaultlodBias;
                
        }
        private void DeClutterEnabled()
        {
            InitializeClutterNames(); // update values

            foreach (GameObject obj in savedClutterObjects)
            {
                if (obj.activeSelf == true)
                {
                    bool foundClutterName = clutterNameDictionary.Keys.Any(key => {
                        return obj.name.ToLower().Contains(key.ToLower()) && clutterNameDictionary[key];
                    });

                    if(foundClutterName)
                        obj.SetActive(false);
                    else
                        obj.SetActive(true);
                }
            }
        }
        private void ReClutterEnabled()
        {
            foreach (GameObject obj in savedClutterObjects)
            {
                if (obj.activeSelf == false)
                {
                    obj.SetActive(true);
                }
            }
        }
        private void DeClutterScene()
        {
            StaticManager.BeginCoroutine(GetAllGameObjectsInSceneCoroutine());
            StaticManager.BeginCoroutine(DeClutterGameObjects());
        }
        private IEnumerator DeClutterGameObjects()
        {
            // Loop until the coroutine has finished
            while (true)
            {
                if (allGameObjectsList != null && allGameObjectsList.Count > 0)
                {
                    // Coroutine has finished, and allGameObjectsList is populated
                    GameObject[] allGameObjectsArray = allGameObjectsList.ToArray();
                    foreach (GameObject obj in allGameObjectsArray)
                    {
                        if (obj != null && ShouldDisableObject(obj))
                        {
                            obj.SetActive(false);
                            //Logger.LogInfo("Clutter Removed " + obj.name);
                            //EFT.UI.ConsoleScreen.LogError("Clutter Removed " + obj.name);
                        }
                    }
                }
                yield break;
            }
        }
        private IEnumerator GetAllGameObjectsInSceneCoroutine()
        {
            GameObject[] gameObjects = GameObject.FindObjectsOfType<GameObject>();

            foreach (GameObject obj in gameObjects)
            {
                bool isLODGroup = obj.GetComponent<LODGroup>() != null;
                bool isStaticDeferredDecal = obj.GetComponent<StaticDeferredDecal>() != null;
                bool isParticleSystem = obj.GetComponent<ParticleSystem>() != null;
                bool isGoodThing = isLODGroup || isStaticDeferredDecal || isParticleSystem;

                if (declutterDecalsEnabledConfig.Value)
                {
                    isGoodThing = isLODGroup || isStaticDeferredDecal;
                }
                else
                {
                    isGoodThing = isLODGroup;
                }
                
                bool isTarkovContainer = obj.GetComponent<LootableContainer>() != null;
                bool isTarkovContainerGroup = obj.GetComponent<LootableContainersGroup>() != null;
                bool isTarkovObservedItem = obj.GetComponent<ObservedLootItem>() != null;
                bool isTarkovItem = obj.GetComponent<LootItem>() != null;
                bool isTarkovWeaponMod = obj.GetComponent<WeaponModPoolObject>() != null;
                bool hasRainCondensator = obj.GetComponent<RainCondensator>() != null;
                bool isLocalPlayer = obj.GetComponent<LocalPlayer>() != null;
                bool isPlayer = obj.GetComponent<Player>() != null;
                bool isBotOwner = obj.GetComponent<BotOwner>() != null;
                bool isCullingObject = obj.GetComponent<CullingObject>() != null;
                bool isCullingLightObject = obj.GetComponent<CullingLightObject>() != null;
                bool isCullingGroup = obj.GetComponent<CullingGroup>() != null;
                bool isDisablerCullingObject = obj.GetComponent<DisablerCullingObject>() != null;
                bool isObservedCullingManager = obj.GetComponent<ObservedCullingManager>() != null;
                bool isPerfectCullingCrossSceneGroup = obj.GetComponent<PerfectCullingCrossSceneGroup>() != null;
                bool isScreenDistanceSwitcher = obj.GetComponent<ScreenDistanceSwitcher>() != null;
                bool isBakedLodContent = obj.GetComponent<BakedLodContent>() != null;
                bool isGuidComponent = obj.GetComponent<GuidComponent>() != null;
                bool isOcclusionPortal = obj.GetComponent<OcclusionPortal>() != null;
                bool isMultisceneSharedOccluder = obj.GetComponent<MultisceneSharedOccluder>() != null;
                bool isWindowBreaker = obj.GetComponent<WindowBreaker>() != null;
                bool isBallisticCollider = obj.GetComponent<BallisticCollider>() != null;
                bool isBotSpawner = obj.GetComponent<BotSpawner>() != null;
                bool isBadThing = isTarkovContainer || isTarkovContainerGroup || isTarkovObservedItem || isTarkovItem || isTarkovWeaponMod || 
                                  hasRainCondensator || isLocalPlayer || isPlayer || isBotOwner || isCullingObject || isCullingLightObject || 
                                  isCullingGroup || isDisablerCullingObject || isObservedCullingManager || isPerfectCullingCrossSceneGroup || 
                                  isBakedLodContent || isScreenDistanceSwitcher || isGuidComponent || isOcclusionPortal || isBotSpawner || 
                                  isMultisceneSharedOccluder || isWindowBreaker || isBallisticCollider;

                if (isGoodThing && !isBadThing)
                {
                    allGameObjectsList.Add(obj);
                }
            }
            yield break;
        }
        private Dictionary<string, bool> clutterNameDictionary = new Dictionary<string, bool>
        {
        };
        private void InitializeClutterNames()
        {
            clutterNameDictionary["turniket_"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["tray_"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["electronic_box"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["styrofoam_"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["polyethylene_set"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["penyok_"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["kaska"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["boot_"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["garbage_stone"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["garbage_paper"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["cable"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["drawing_"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["paper_"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["_paper"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["paper1"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["paper2"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["paper3"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["paper4"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["paper5"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["paper6"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["paper7"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["paper8"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["paper9"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["pan1"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["pan2"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["pan3"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["pan4"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["pan5"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["pan6"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["pan7"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["pan8"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["pan9"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["poster1"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["poster2"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["poster3"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["poster4"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["poster5"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["poster6"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["poster7"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["poster8"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["poster9"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["_junk"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["junk_"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["_trash"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["trash_"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["cardboard_"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["_cardboard"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["sticks"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["cloth_"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["pants_"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["shirt_"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["dishes_"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["cutlery_"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["book_"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["books_"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["folder_"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["folders_"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["magazine_"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["magazines_"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["fuel_tube"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["city_garbage_"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["city_road_garbage"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["reserve_garbage_"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["reserve_road_garbage"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["garbage_parking_"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["goshan_garbage"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["package_garbage"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["wood_board"] = declutterGarbageEnabledConfig.Value;
            clutterNameDictionary["leaves_"] = declutterGarbageEnabledConfig.Value;
    
            clutterNameDictionary["trash_pile_"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["_trash_pile"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["crushed_concrete"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["crushed_concreate"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["baked_garbage"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["garbage"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["garbage_"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["_garbage"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["garbage_constructor"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["_garb"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["garb_"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["_scrap"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["scrap_"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["heap_"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["_heap"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["_pile"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["pile_"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["_stuff"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["_rubble"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["rubble_"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["scatter_"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["_scatter"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["scattered_"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["_scattered"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["_floorset"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["floorset_"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["brick_pile"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["poletelen01"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["poletelen02"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["poletelen03"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["poletelen04"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["poletelen05"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["poletelen06"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["poletelen07"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["poletelen08"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["poletelen09"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["vetky1"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["vetky2"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["vetky1_"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["vetky2_"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["vetky3_"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["vetky4_"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["vetky5_"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["vetky6_"] = declutterHeapsEnabledConfig.Value;
            clutterNameDictionary["tile_broken_"] = declutterHeapsEnabledConfig.Value;
            

            clutterNameDictionary["shotshell_"] = declutterSpentCartridgesEnabledConfig.Value;
            clutterNameDictionary["shells_"] = declutterSpentCartridgesEnabledConfig.Value;
            clutterNameDictionary["_shotshell"] = declutterSpentCartridgesEnabledConfig.Value;
            clutterNameDictionary["_shells"] = declutterSpentCartridgesEnabledConfig.Value;
            clutterNameDictionary["rifleshell_"] = declutterSpentCartridgesEnabledConfig.Value;
            clutterNameDictionary["_rifleshell"] = declutterSpentCartridgesEnabledConfig.Value;
            clutterNameDictionary["rifle_shells_"] = declutterSpentCartridgesEnabledConfig.Value;
            
            clutterNameDictionary["canned"] = declutterFakeFoodEnabledConfig.Value;
            clutterNameDictionary["canned_"] = declutterFakeFoodEnabledConfig.Value;
            clutterNameDictionary["can_"] = declutterFakeFoodEnabledConfig.Value;
            clutterNameDictionary["juice_"] = declutterFakeFoodEnabledConfig.Value;
            clutterNameDictionary["carton_"] = declutterFakeFoodEnabledConfig.Value;
            clutterNameDictionary["_creased"] = declutterFakeFoodEnabledConfig.Value;
            clutterNameDictionary["bottle"] = declutterFakeFoodEnabledConfig.Value;
            clutterNameDictionary["bottle_"] = declutterFakeFoodEnabledConfig.Value;
            clutterNameDictionary["crackers_"] = declutterFakeFoodEnabledConfig.Value;
            clutterNameDictionary["oat_flakes"] = declutterFakeFoodEnabledConfig.Value;
            clutterNameDictionary["chocolate_"] = declutterFakeFoodEnabledConfig.Value;
            clutterNameDictionary["biscuits"] = declutterFakeFoodEnabledConfig.Value;
            clutterNameDictionary["package_"] = declutterFakeFoodEnabledConfig.Value;
            clutterNameDictionary["cigarette_"] = declutterFakeFoodEnabledConfig.Value;
            clutterNameDictionary["medkit_"] = declutterFakeFoodEnabledConfig.Value;
            clutterNameDictionary["_cup"] = declutterFakeFoodEnabledConfig.Value;
            clutterNameDictionary["plasticcup_"] = declutterFakeFoodEnabledConfig.Value;
            
            clutterNameDictionary["goshan_decal"] = declutterDecalsEnabledConfig.Value;
            clutterNameDictionary["ground_decal"] = declutterDecalsEnabledConfig.Value;
            clutterNameDictionary["decalgraffiti"] = declutterDecalsEnabledConfig.Value;
            clutterNameDictionary["blood_"] = declutterDecalsEnabledConfig.Value;
            clutterNameDictionary["_blood"] = declutterDecalsEnabledConfig.Value;
            clutterNameDictionary["sand_decal"] = declutterDecalsEnabledConfig.Value;
            clutterNameDictionary["decal_dirt"] = declutterDecalsEnabledConfig.Value;
            clutterNameDictionary["decal_drip"] = declutterDecalsEnabledConfig.Value;
            clutterNameDictionary["decal_"] = declutterDecalsEnabledConfig.Value;
            clutterNameDictionary["decals_"] = declutterDecalsEnabledConfig.Value;
            
            clutterNameDictionary["puddle"] = declutterPuddlesEnabledConfig.Value;
            clutterNameDictionary["puddles_"] = declutterPuddlesEnabledConfig.Value;
            clutterNameDictionary["_puddles"] = declutterPuddlesEnabledConfig.Value;
            clutterNameDictionary["puddle group"] = declutterPuddlesEnabledConfig.Value;

            clutterNameDictionary["_glass"] = declutterShardsEnabledConfig.Value;
            clutterNameDictionary["brokenglass_"] = declutterShardsEnabledConfig.Value;
            clutterNameDictionary["glass_crush"] = declutterShardsEnabledConfig.Value;
            clutterNameDictionary["plite_crush"] = declutterShardsEnabledConfig.Value;
            clutterNameDictionary["lesa_crush"] = declutterShardsEnabledConfig.Value;
            clutterNameDictionary["shards_"] = declutterShardsEnabledConfig.Value;
            clutterNameDictionary["_shards"] = declutterShardsEnabledConfig.Value;
            
        }
        private Dictionary<string, bool> dontDisableDictionary = new Dictionary<string, bool>
        {
            { "item_", true },
            { "weapon_", true },
            { "barter_", true },
            { "mod_", true },
            { "audio", true },
            { "container", true },
            { "trigger", true },
            { "culling", true },
            { "collider", true },
            { "colider", true },
            { "group", true },
            { "manager", true },
            { "scene", true },
            { "player", true },
            { "portal", true },
            { "bakelod", true },
            { "door", true },
            { "shadow", true },
            { "mine", true }
        };
        private bool ShouldDisableObject(GameObject obj)
        {
            if (obj == null)
            {
                // Handle the case when obj is null for whatever reason.
                return false;
            }

            bool isStaticDeferredDecal = obj.GetComponent<StaticDeferredDecal>() != null;
            bool isParticleSystem = obj.GetComponent<ParticleSystem>() != null;
            bool isGoodThing = isStaticDeferredDecal || isParticleSystem;
            GameObject childGameMeshObject = null;
            GameObject childGameColliderObject = null;
            bool childHasMesh = false;
            float sizeOnY = 3f;
            bool childHasCollider = false;
            bool foundClutterName = false;
            bool dontDisableName = dontDisableDictionary.Keys.Any(key => obj.name.ToLower().Contains(key.ToLower()));
            //EFT.UI.ConsoleScreen.LogError("Found Lod Group " + obj.name);
            if (declutterUnscrutinizedEnabledConfig.Value == true)
                {
                    foundClutterName = true;
                }
            else
                {
                    foundClutterName = clutterNameDictionary.Keys.Any(key => {
                        return obj.name.ToLower().Contains(key.ToLower());
                    });
                }
                if (foundClutterName && !dontDisableName)
                {
                //EFT.UI.ConsoleScreen.LogError("Found Clutter Name" + obj.name);
                    foreach (Transform child in obj.transform)
                    {
                        childGameMeshObject = child.gameObject;
                        bool isTarkovContainer = childGameMeshObject.GetComponent<LootableContainer>() != null;
                        bool isTarkovContainerGroup = childGameMeshObject.GetComponent<LootableContainersGroup>() != null;
                        bool isTarkovObservedItem = childGameMeshObject.GetComponent<ObservedLootItem>() != null;
                        bool isTarkovItem = childGameMeshObject.GetComponent<LootItem>() != null;
                        bool isTarkovWeaponMod = childGameMeshObject.GetComponent<WeaponModPoolObject>() != null;
                        bool hasRainCondensator = childGameMeshObject.GetComponent<RainCondensator>() != null;
                        bool isLocalPlayer = childGameMeshObject.GetComponent<LocalPlayer>() != null;
                        bool isPlayer = childGameMeshObject.GetComponent<Player>() != null;
                        bool isBotOwner = childGameMeshObject.GetComponent<BotOwner>() != null;
                        bool isCullingObject = childGameMeshObject.GetComponent<CullingObject>() != null;
                        bool isCullingLightObject = childGameMeshObject.GetComponent<CullingLightObject>() != null;
                        bool isCullingGroup = childGameMeshObject.GetComponent<CullingGroup>() != null;
                        bool isDisablerCullingObject = childGameMeshObject.GetComponent<DisablerCullingObject>() != null;
                        bool isObservedCullingManager = childGameMeshObject.GetComponent<ObservedCullingManager>() != null;
                        bool isPerfectCullingCrossSceneGroup = childGameMeshObject.GetComponent<PerfectCullingCrossSceneGroup>() != null;
                        bool isScreenDistanceSwitcher = childGameMeshObject.GetComponent<ScreenDistanceSwitcher>() != null;
                        bool isBakedLodContent = childGameMeshObject.GetComponent<BakedLodContent>() != null;
                        bool isGuidComponent = childGameMeshObject.GetComponent<GuidComponent>() != null;
                        bool isOcclusionPortal = childGameMeshObject.GetComponent<OcclusionPortal>() != null;
                        bool isMultisceneSharedOccluder = childGameMeshObject.GetComponent<MultisceneSharedOccluder>() != null;
                        bool isWindowBreaker = childGameMeshObject.GetComponent<WindowBreaker>() != null;
                        bool isBotSpawner = childGameMeshObject.GetComponent<BotSpawner>() != null;
                        bool isBadThing = isTarkovContainer || isTarkovContainerGroup || isTarkovObservedItem || isTarkovItem || isTarkovWeaponMod ||
                                          hasRainCondensator || isLocalPlayer || isPlayer || isBotOwner || isCullingObject || isCullingLightObject ||
                                          isCullingGroup || isDisablerCullingObject || isObservedCullingManager || isPerfectCullingCrossSceneGroup ||
                                          isBakedLodContent || isScreenDistanceSwitcher || isGuidComponent || isOcclusionPortal || isBotSpawner ||
                                          isMultisceneSharedOccluder || isWindowBreaker;
                    if (isBadThing)
                        {
                            return false;
                        }
                    }
                    foreach (Transform child in obj.transform)
                    {
                            childGameMeshObject = child.gameObject;
                            if (child.GetComponent<MeshRenderer>() != null && !childGameMeshObject.name.ToLower().Contains("shadow") && !childGameMeshObject.name.ToLower().StartsWith("col") && !childGameMeshObject.name.ToLower().EndsWith("der"))
                            {
                                childHasMesh = true;
                                // Exit the loop since we've found what we need
                                break;
                            }
                    }
                    if (!childHasMesh && !isGoodThing)
                    {
                        return false;
                    }
                    foreach (Transform child in obj.transform)
                    {
                        if ((child.GetComponent<MeshCollider>() != null || child.GetComponent<BoxCollider>() != null) && child.GetComponent<BallisticCollider>() == null)
                        {
                            childGameColliderObject = child.gameObject;
                            if (childGameColliderObject != null && childGameColliderObject.activeSelf)
                            {
                                childHasCollider = true;
                                // Exit the loop since we've found what we need
                                break;
                            }
                        }
                    }
                if (isGoodThing)
                {
                    sizeOnY = 0.1f;
                }
                else if (childHasMesh)
                {
                    sizeOnY = GetMeshSizeOnY(childGameMeshObject);
                }
                else
                {
                    return false;
                }
                if ((childHasMesh || isGoodThing) && (!childHasCollider || isGoodThing) && sizeOnY <= 2f * declutterScaleOffsetConfig.Value)
                    {
                        savedClutterObjects.Add(obj);
                        return true;
                    }
                }
            return false;
        }
        private float GetMeshSizeOnY(GameObject childGameObject)
        {
            MeshRenderer meshRenderer = childGameObject?.GetComponent<MeshRenderer>();
            if (meshRenderer != null && meshRenderer.enabled)
            {
                Bounds bounds = meshRenderer.bounds;
                return bounds.size.y;
            }
            return 0.0f;
        }
    }
}
