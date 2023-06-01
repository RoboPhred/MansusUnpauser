using System;
using SecretHistories.Entities;
using SecretHistories.Fucine;
using SecretHistories.UI;
using SecretHistories.Services;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using SecretHistories.Spheres;
using System.Linq;
using SecretHistories.Events;
using SecretHistories.Tokens.Payloads;
using SecretHistories.Enums;

public class MansusUnpauser : MonoBehaviour, ISettingSubscriber, ISphereEventSubscriber
{
    private static readonly string Setting = "mansuspause_enabled";

    private bool autoUnpausedEnabled;
    private bool preMansusWasPaused = false;

    public static void Initialise()
    {
        try
        {
            var component = new GameObject().AddComponent<MansusUnpauser>();
        }
        catch (Exception e)
        {
            NoonUtility.LogException(e);
        }
    }

    public void Start()
    {
        SceneManager.sceneLoaded += new UnityAction<Scene, LoadSceneMode>(this.HandleSceneLoaded);
        SceneManager.sceneUnloaded += new UnityAction<Scene>(this.HandleSceneUnloaded);

        var setting = Watchman.Get<Compendium>().GetEntityById<Setting>(MansusUnpauser.Setting);
        setting.AddSubscriber(this);
        this.ReadSetting();
    }

    public void OnDestroy()
    {
        var setting = Watchman.Get<Compendium>().GetEntityById<Setting>(MansusUnpauser.Setting);
        setting.RemoveSubscriber(this);
    }

    void ISettingSubscriber.BeforeSettingUpdated(object newValue)
    {

    }

    void ISettingSubscriber.WhenSettingUpdated(object newValue)
    {
        var strValue = newValue as int? == 1 ? "1" : "0";
        Watchman.Get<Config>().PersistConfigValue(MansusUnpauser.Setting, strValue);
        this.autoUnpausedEnabled = newValue as int? == 1;
    }

    private void ReadSetting()
    {
        var setting = Watchman.Get<Compendium>().GetEntityById<Setting>(MansusUnpauser.Setting).CurrentValue as int?;
        this.autoUnpausedEnabled = setting.HasValue && setting.Value == 1;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "S4Tabletop")
        {
            // GetSphereByAbsolutePath doesnt seem to work, or we could use that
            var tabletop = Watchman.Get<HornedAxe>().GetSpheres().OfType<TabletopSphere>().Single();
            tabletop.Subscribe(this);
        }
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        if (scene.name == "S4Tabletop")
        {
            var tabletop = Watchman.Get<HornedAxe>().GetSpheres().OfType<TabletopSphere>().Single();
            tabletop.Unsubscribe(this);
        }
    }

    private void TryUnpause()
    {
        if (this.autoUnpausedEnabled)
        {
            Watchman.Get<LocalNexus>().UnPauseGame(false);
        }
    }

    void ISphereEventSubscriber.OnSphereChanged(SphereChangedArgs args)
    {
    }

    void ISphereEventSubscriber.OnTokensChangedForSphere(SphereContentsChangedEventArgs args)
    {
        if (args.Change != SphereContentChange.TokenRemoved)
        {
            return;
        }

        if (args.Token.Payload is not Ingress ingress)
        {
            return;
        }

        // This is a bit of a hack... When the mansus opens, the Ingress token
        // appears for a single frame on the tabletop and is immediately removed again.
        // Thankfully, during this time, it is not opened.
        // Once the mansus opens, it will be marked as exhausted, which we can check next time.
        if (!ingress.IsExhausted)
        {
            // Remember if we were paused before the mansus, so we can keep that pause
            // afterwards.
            this.preMansusWasPaused = Watchman.Get<Heart>().IsPaused();
            return;
        }

        if (!this.preMansusWasPaused)
        {
            this.TryUnpause();
        }
    }

    void ISphereEventSubscriber.OnTokenInteractionInSphere(TokenInteractionEventArgs args)
    {
    }
}