using System;
using System.Linq;
using UnityEngine;

namespace AlephVault.Unity.WindRose.GabTab
{
    namespace Authoring
    {
        namespace Behaviours
        {
            namespace UI
            {
                using AlephVault.Unity.GabTab.Authoring.Behaviours;
                using AlephVault.Unity.GabTab.Authoring.Behaviours.Interactors;
                using System.Threading.Tasks;
                using Types;
                using UnityEngine.SceneManagement;
                using AlephVault.Unity.WindRose.Authoring.Behaviours.Entities.Objects;

                /// <summary>
                ///   A HUDLinker allows a <see cref="MapObject"/> to execute
                ///     interactions on the given HUD or the main-and-only
                ///     HUD in the active scene.
                /// </summary>
                [RequireComponent(typeof(MapObject))]
                public class HUDLinker : MonoBehaviour
                {
                    /// <summary>
                    ///   The <see cref="HUD"/> this object is attached to.
                    /// </summary>
                    public HUD HUD;

                    private HUD GetTheOnlyHUDInScene()
                    {
                        HUD foundHud = null;
                        foreach (HUD hud in (from obj in SceneManager.GetActiveScene().GetRootGameObjects() select obj.GetComponent<HUD>()))
                        {
                            if (hud)
                            {
                                if (foundHud)
                                {
                                    throw new Exception("A HUD was not specified to this object, and there are two/+ top-level HUDs in the scene");
                                }
                                else
                                {
                                    foundHud = hud;
                                }
                            }
                        }
                        if (!foundHud) throw new Exception("A HUD was not specified to this object, and there is no top-level HUD in the scene");
                        return foundHud;
                    }

                    /// <summary>
                    ///   Executes an interaction, as described in <see cref="UI.HUD.RunInteraction(Func{InteractorsManager, InteractiveMessage, Task})"/>.
                    ///   The HUD to consider is one being specifically added to the scene (the only one) or, even better, the one assigned to this object
                    ///     under the <see cref="UI.HUD"/> property.
                    /// </summary>
                    /// <param name="interaction">The interaction to run</param>
                    public void RunInteraction(Func<InteractorsManager, InteractiveMessage, Task> interaction)
                    {
                        HUD hud = HUD ? HUD : GetTheOnlyHUDInScene();
                        hud.RunInteraction(interaction);
                    }
                }
            }
        }
    }
}
