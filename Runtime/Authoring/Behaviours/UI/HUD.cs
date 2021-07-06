using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameMeanMachine.Unity.GabTab.Authoring.Behaviours;
using GameMeanMachine.Unity.GabTab.Authoring.Behaviours.Interactors;
using AlephVault.Unity.Support.Utils;

namespace GameMeanMachine.Unity.WindRose.GabTab
{
    namespace Authoring
    {
        namespace Behaviours
        {
            namespace UI
            {
                using GameMeanMachine.Unity.WindRose.Authoring.Behaviours.World;
                using GameMeanMachine.Unity.WindRose.Authoring.Behaviours.Entities.Objects;
                using System.Threading.Tasks;

                /// <summary>
                ///   <para>
                ///     A Heads Up Display makes use of a <see cref="Canvas"/> and its related
                ///       camera (when using <see cref="RenderMode.ScreenSpaceCamera"/> rendering
                ///       mode in the canvas) and allows tracking an object and also starting an
                ///       interaction if the canvas contains an <see cref="InteractiveInterface"/>.
                ///       It provides a wrapper for object following and running an interaction.
                ///   </para>
                ///   <para>
                ///     This behaviour also allows pausing every map currently alive at top-level.
                ///   </para>
                /// </summary>
                public class HUD : MonoBehaviour
                {
                    /// <summary>
                    ///   Criteria to pause the map while the interaction is running: don't pause,
                    ///     pause everything but animations, or completely freeze.
                    /// </summary>
                    public enum PauseType { NO, HOLD, FREEZE }

                    /// <summary>
                    ///   The <see cref="PauseType"/> to use while interacting.
                    /// </summary>
                    [SerializeField]
                    private PauseType pauseType = PauseType.FREEZE;

                    /// <summary>
                    ///   The related <see cref="Canvas"/> to work with. If omitted,
                    ///     it will be sought as another component in the same object.
                    /// </summary>
                    [SerializeField]
                    private Canvas canvas;

                    // The interactive interface of the current canvas.
                    private InteractiveInterface interactiveInterface;

                    /// <summary>
                    ///   The object being followed, if any.
                    /// </summary>
                    /// <seealso cref="Focus(MapObject, float, bool)"/>
                    [SerializeField]
                    private MapObject target;

                    /// <summary>
                    ///   See <see cref="target"/>.
                    /// </summary>
                    public MapObject Target { get { return target; } }

                    // The remaining time of the transition.
                    private float remainingTransitioningTime = 0f;

                    /// <summary>
                    ///   Current focus status (using the specified camera):
                    ///   <list type="bullet">
                    ///     <item>
                    ///       <term>NotFocusing</term>
                    ///       <description>There is no current target being followed.</description>
                    ///     </item>
                    ///     <item>
                    ///       <term>Transitioning</term>
                    ///       <description>The camera is moving towards the target object.</description>
                    ///     </item>
                    ///     <item>
                    ///       <term>Focusing</term>
                    ///       <description>The object is following the target object.</description>
                    ///     </item>
                    ///   </list>
                    /// </summary>
                    public enum FocusStatus { NotFocusing, Transitioning, Focusing }

                    /// <summary>
                    ///   The current focus status.
                    /// </summary>
                    /// <seealso cref="FocusStatus"/>
                    public FocusStatus Status { get; private set; }

                    private void Awake()
                    {
                        if (!canvas) canvas = GetComponent<Canvas>();
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        interactiveInterface = canvas.GetComponentInChildren<InteractiveInterface>();
                        if (transform.parent) Debug.LogWarning("Warning!!! A HUD must be a root object in the scene (i.e. have no parent transform) to be properly recognized by an object looking for the default one!!!");
                    }

                    // Use this for initialization
                    private void Start()
                    {
                        canvas.worldCamera.orthographic = true;
                    }

                    /// <summary>
                    ///   <para>
                    ///     Chooses a new object to follow, specifying an optional delay and, in
                    ///       that case, the possibility to avoid waiting any current transition.
                    ///   </para>
                    ///   <para>
                    ///     If no new <paramref name="newTarget"/> is specified, <paramref name="delay"/>
                    ///       will not be considered. If <paramref name="noWait"/> is true, the current
                    ///       transition will be aborted instantly and the target will be set to null.
                    ///       If it is false, the target will be set to null after waiting the current
                    ///       transition.
                    ///   </para>
                    ///   <para>
                    ///     If a new <paramref name="newTarget"/> is specified, <paramref name="delay"/>
                    ///       will be considered to start a transition (if > 0). If <paramref name="noWait"/>
                    ///       is true, and a transition is being run, no coroutine will start. Otherwise,
                    ///       it will start a waiting & transitioning coroutine as normal.
                    ///   </para>
                    /// </summary>
                    /// <param name="newTarget">The new object to follow</param>
                    /// <param name="delay">The delay to take transitioning to the new object</param>
                    /// <param name="noWait">Tells whether waiting the current transition or not</param>
                    /// <returns>The new coroutine</returns>
                    public async Task Focus(MapObject newTarget, float delay = 0f, bool noWait = false)
                    {
                        if (canvas.worldCamera)
                        {
                            await DoFocus(newTarget, delay, noWait);
                        }
                    }

                    private async Task DoFocus(MapObject newTarget, float delay = 0f, bool noWait = false)
                    {
                        if (noWait)
                        {
                            if (newTarget && Status == FocusStatus.Transitioning)
                            {
                                return;
                            }
                        }
                        else
                        {
                            // Wait until the current object is being focused.
                            while (Status == FocusStatus.Transitioning)
                            {
                                await Tasks.Blink();
                            }
                        }

                        // Set the object and move to its position or start a new transition to it.
                        target = newTarget;
                        if (target == null)
                        {
                            Status = FocusStatus.NotFocusing;
                        }
                        else if (delay >= 0)
                        {
                            Status = FocusStatus.Transitioning;
                            remainingTransitioningTime = delay;
                        }
                        else
                        {
                            Status = FocusStatus.Focusing;
                        }
                    }

                    private void Update()
                    {
                        /**
                         * Focuses the camera on the target object position, or transitions by considering the deltaTime/remaining
                         *   fraction and the object's distance to the camera. When the transition ends, the <see cref="Status"/>
                         *   will be changed to <see cref="FocusStatus.Focusing"/>.
                         */
                        Camera camera = canvas.worldCamera;
                        if (target && camera)
                        {
                            Vector3 targetPosition = new Vector3(target.transform.position.x, target.transform.position.y, camera.transform.position.z);
                            if (Status == FocusStatus.Focusing)
                            {
                                camera.transform.position = targetPosition;
                            }
                            else // FocusStatus.Transitioning
                            {
                                float timeDelta = Time.deltaTime;
                                float timeFraction = 0f;
                                if (timeDelta >= remainingTransitioningTime)
                                {
                                    timeFraction = 1f;
                                    remainingTransitioningTime = 0;
                                    Status = FocusStatus.Focusing;
                                }
                                else
                                {
                                    timeFraction = timeDelta / remainingTransitioningTime;
                                    remainingTransitioningTime -= timeDelta;
                                }
                                camera.transform.position = Vector3.MoveTowards(camera.transform.position, targetPosition, (targetPosition - camera.transform.position).magnitude * timeFraction);
                            }
                        }
                        else
                        {
                            target = null;
                            Status = FocusStatus.NotFocusing;
                        }
                    }

                    private void OnAcquire()
                    {
                        if (pauseType != PauseType.NO)
                        {
                            bool fullFreeze = pauseType == PauseType.FREEZE;
                            foreach (Map map in (from obj in SceneManager.GetActiveScene().GetRootGameObjects() select obj.GetComponent<Map>()))
                            {
                                if (map)
                                {
                                    map.Pause(fullFreeze);
                                }
                            }
                        }
                    }

                    private void OnRelease()
                    {
                        if (pauseType != PauseType.NO)
                        {
                            foreach (Map map in (from obj in SceneManager.GetActiveScene().GetRootGameObjects() select obj.GetComponent<Map>()))
                            {
                                if (map)
                                {
                                    map.Resume();
                                }
                            }
                        }
                    }

                    /// <summary>
                    ///   Executes an interaction, as described in <see cref="InteractiveInterface.RunInteraction(Func{InteractorsManager, InteractiveMessage, Task})"/>.
                    ///     However the behaviour is wrapped in pausing/unpausing context, depending on the value of <see cref="pauseType"/>.
                    /// </summary>
                    /// <param name="interaction">The interaction to run</param>
                    public void RunInteraction(Func<InteractorsManager, InteractiveMessage, Task> interaction)
                    {
                        if (!interactiveInterface.IsRunningAnInteraction) WrappedInteraction(interaction);
                    }

                    private async void WrappedInteraction(Func<InteractorsManager, InteractiveMessage, Task> interaction)
                    {
                        try
                        {
                            OnAcquire();
                            await interactiveInterface.RunInteraction(interaction);
                        }
                        finally
                        {
                            OnRelease();
                        }
                    }
                }
            }
        }
    }
}
