using HarmonyLib;
using UnityEngine;
using KitchenMods;
using Kitchen.Transports;
using Kitchen;
using Steamworks;
using System.Runtime.Serialization.Formatters.Binary;
using Discord;
using Kitchen.NetworkSupport;
using Steamworks.Data;
using MessagePack;
using Unity.Collections;
using Unity.Entities;
using KitchenData;
using TwitchLib.Api.Core.Models.Undocumented.ChannelExtensionData;
using TwitchLib.Api.Core.Models.Undocumented.ChannelPanels;

namespace PlateUpModTemplate;

public class PlateUpMod : IModInitializer
{
    public const string AUTHOR = "AUTHOR";
    public const string MOD_NAME = "MOD_NAME";
    public const string MOD_ID = $"com.{AUTHOR}.{MOD_NAME}";
    public static GameObject? playerPrefab;
    public static GameObject emoteTracker;

    public void PostActivate(Mod mod)
    {
        Harmony.DEBUG = true;
        Harmony harmony = new Harmony(MOD_ID);


        harmony.PatchAll();
    }

    public void PreInject()
    {

    }

    public void PostInject()
    {

    }
}

[HarmonyPatch(typeof(GameCreator), "PerformInitialSetup")]
public static class GameCreator_InitialSetupPatch
{
    [HarmonyPostfix]
    public static void Postfix(GameCreator __instance)
    {
        var fieldInfo = typeof(GameCreator).GetField("Directory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        AssetDirectory dir = (AssetDirectory)fieldInfo.GetValue(__instance);

        PlateUpMod.playerPrefab = dir.ViewPrefabs[ViewType.Player];

        if (PlateUpMod.playerPrefab is null)
        {
            Debug.Log("I'm killing myself");
            throw new NullReferenceException();
        }

        PlateUpMod.playerPrefab.AddComponent<EmoteView>();
    }
}

[HarmonyPatch]
public static class Scene_Changed_Patch
{
    [HarmonyPatch(typeof(Kitchen.MakePing), "Perform")]
    [HarmonyPostfix]
    public static void SceneChanged_Postfix(ref InteractionData data)
    {
        Debug.Log("ping detected");
        TestClass t = World.DefaultGameObjectInjectionWorld.GetExistingSystem<TestClass>();
        t.IncrementEventNumByOne(data.Interactor);
        //if (PlateUpMod.emoteTracker == null)
        //{
        //    Debug.Log("Creating emote tracker");
        //    PlateUpMod.emoteTracker = new GameObject();
        //    PlateUpMod.emoteTracker.name = "EmoteTracker";
        //    var viewthing = PlateUpMod.emoteTracker.AddComponent<EmoteView>();
        //    GameObject.DontDestroyOnLoad(PlateUpMod.emoteTracker);
        //}
        //do my stuff
        //new SyncAnimationToClientsPlate().send(/*null, */"sex", -1, 0);
    }
}
public class AddCEmoteDataToPlayers : GenericSystemBase, IModSystem
{
    private EntityQuery EmoteEnjoyers;
    protected override void Initialise()
    {
        base.Initialise();
        EmoteEnjoyers = GetEntityQuery(new QueryHelper()
                .All(typeof(CPlayer))
                .None(
                    typeof(CEmoteData)
                ));
    }

    protected override void OnUpdate()
    {
        var joyers = EmoteEnjoyers.ToEntityArray(Allocator.TempJob);
        foreach (var joyer in joyers)
        {
            EntityManager.AddComponent<CRequiresView>(joyer);
            EntityManager.SetComponentData(joyer, new CRequiresView
            {
                Type = ViewType.Player,
                ViewMode = ViewMode.World
            });
            EntityManager.AddComponent<CEmoteData>(joyer);
        }
        joyers.Dispose();
    }
}
public struct CEmoteData : IModComponent
{
    public char animation;
    public int position;
    public int eventNum;
}


public class EmoteView : UpdatableObjectView<EmoteView.ViewData>
{
    public int helpmeplease = 69;
    public class UpdateView : IncrementalViewSystemBase<ViewData>, IModSystem
    {
        private EntityQuery Views;

        protected override void Initialise()
        {
            base.Initialise();
            Views = GetEntityQuery(new QueryHelper().All(typeof(CLinkedView), typeof(CEmoteData)));
        }

        protected override void OnUpdate()
        {
            using var views = Views.ToComponentDataArray<CLinkedView>(Allocator.Temp);
            using var components = Views.ToComponentDataArray<CEmoteData>(Allocator.Temp);

            for (var i = 0; i < components.Length; i++)
            {
                var view = views[i];
                var data = components[i];

                SendUpdate(view, new ViewData
                {
                    animation = data.animation,
                    position = data.position,
                    eventNum = data.eventNum
                }, MessageType.SpecificViewUpdate);
            }
        }
    }

    // you must mark your ViewData as MessagePackObject and mark each field with a key
    // if you don't, the game will run locally but fail in multiplayer
    [MessagePackObject]
    public struct ViewData : ISpecificViewData, IViewData.ICheckForChanges<ViewData>
    {
        [Key(0)] public char animation;
        [Key(1)] public int position;
        [Key(2)] public int eventNum;

        // this tells the game how to find this subview within a prefab
        // GetSubView<T> is a cached method that looks for the requested T in the view and its children
        public IUpdatableObject GetRelevantSubview(IObjectView view) => view.GetSubView<EmoteView>();

        // this is used to determine if the data needs to be sent again
        public bool IsChangedFrom(ViewData check) => animation != check.animation ||
                                                     position != check.position ||
                                                     eventNum != check.eventNum;
    }

    // this receives the updated data from the ECS backend whenever a new update is sent
    // in general, this should update the state of the view to match the values in view_data
    // ideally ignoring all current state; it's possible that not all updates will be received so
    // you should avoid relying on previous state where possible
    protected override void UpdateData(ViewData view_data)
    {
        helpmeplease = view_data.eventNum;
        // perform the update here
        // this is a Unity MonoBehavior so we can do normal Unity things here
    }
}

//public class IncrementEmote : InteractionSystem, IModSystem
//{
//    protected override InteractionType RequiredType => InteractionType.Notify;

//    protected override bool AllowAnyMode => true;

//    private CEmoteData _data;

//    protected override bool IsPossible(ref InteractionData data)
//    {
//        bool sex = Require<CEmoteData>(data.Interactor, out _data);
//        Debug.Log($"--------------  why isn't it possible   {sex}");
//        return sex;
//    }

//    protected override void Perform(ref InteractionData data)
//    {
//        Debug.Log($"--------------  it's just not {_data}");

//        _data.eventNum++;
//        EntityManager.SetComponentData(data.Interactor, _data);
//    }
//}

public class TestClass : GenericSystemBase, IModSystem
{
    private CEmoteData _data;
    protected override void OnUpdate()
    {

    }
    public void IncrementEventNumByOne(Entity eeeeeeeeeeeeeeee)
    {
        bool sex = Require(eeeeeeeeeeeeeeee, out _data);
        if (sex)
        {
            _data.eventNum++;
            Debug.Log($"{eeeeeeeeeeeeeeee}'s eventNum is now {_data.eventNum}");
            EntityManager.SetComponentData(eeeeeeeeeeeeeeee, _data);
        }
    }
}