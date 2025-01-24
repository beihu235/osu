// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Screens;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Online.Rooms;
using osu.Game.Overlays;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Menu;
using osu.Game.Screens.OnlinePlay.Match.Components;
using osu.Game.Screens.OnlinePlay.Multiplayer;
using osu.Game.Utils;
using Container = osu.Framework.Graphics.Containers.Container;

namespace osu.Game.Screens.OnlinePlay.Match
{
    [Cached(typeof(IPreviewTrackOwner))]
    public abstract partial class RoomSubScreen : OnlinePlaySubScreen, IPreviewTrackOwner
    {
        public readonly Bindable<PlaylistItem?> SelectedItem = new Bindable<PlaylistItem?>();

        public override bool? ApplyModTrackAdjustments => true;

        protected override BackgroundScreen CreateBackground() => new RoomBackgroundScreen(Room.Playlist.FirstOrDefault())
        {
            SelectedItem = { BindTarget = SelectedItem }
        };

        public override bool DisallowExternalBeatmapRulesetChanges => true;

        /// <summary>
        /// A container that provides controls for selection of user mods.
        /// This will be shown/hidden automatically when applicable.
        /// </summary>
        protected Drawable? UserModsSection;

        /// <summary>
        /// A container that provides controls for selection of the user style.
        /// This will be shown/hidden automatically when applicable.
        /// </summary>
        protected Drawable? UserStyleSection;

        /// <summary>
        /// A container that will display the user's style.
        /// </summary>
        protected Container<DrawableRoomPlaylistItem>? UserStyleDisplayContainer;

        private Sample? sampleStart;

        /// <summary>
        /// Any mods applied by/to the local user.
        /// </summary>
        protected readonly Bindable<IReadOnlyList<Mod>> UserMods = new Bindable<IReadOnlyList<Mod>>(Array.Empty<Mod>());

        /// <summary>
        /// When players are freely allowed to select their own gameplay style (selected item has a non-null beatmapset id),
        /// a non-null value indicates a local beatmap selection from the same beatmapset as the selected item.
        /// </summary>
        public readonly Bindable<BeatmapInfo?> UserBeatmap = new Bindable<BeatmapInfo?>();

        /// <summary>
        /// When players are freely allowed to select their own gameplay style (selected item has a non-null beatmapset id),
        /// a non-null value indicates a local ruleset selection.
        /// </summary>
        public readonly Bindable<RulesetInfo?> UserRuleset = new Bindable<RulesetInfo?>();

        [Resolved(CanBeNull = true)]
        private IOverlayManager? overlayManager { get; set; }

        [Resolved]
        private MusicController music { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        protected RulesetStore Rulesets { get; private set; } = null!;

        [Resolved]
        protected IAPIProvider API { get; private set; } = null!;

        [Resolved(canBeNull: true)]
        protected OnlinePlayScreen? ParentScreen { get; private set; }

        [Resolved]
        private PreviewTrackManager previewTrackManager { get; set; } = null!;

        [Resolved(canBeNull: true)]
        protected IDialogOverlay? DialogOverlay { get; private set; }

        [Cached]
        private readonly OnlinePlayBeatmapAvailabilityTracker beatmapAvailabilityTracker = new OnlinePlayBeatmapAvailabilityTracker();

        protected IBindable<BeatmapAvailability> BeatmapAvailability => beatmapAvailabilityTracker.Availability;

        public readonly Room Room;
        private readonly bool allowEdit;

        internal ModSelectOverlay UserModsSelectOverlay { get; private set; } = null!;

        private IDisposable? userModsSelectOverlayRegistration;
        private RoomSettingsOverlay settingsOverlay = null!;
        private Drawable mainContent = null!;

        /// <summary>
        /// Creates a new <see cref="RoomSubScreen"/>.
        /// </summary>
        /// <param name="room">The <see cref="Room"/>.</param>
        /// <param name="allowEdit">Whether to allow editing room settings post-creation.</param>
        protected RoomSubScreen(Room room, bool allowEdit = true)
        {
            Room = room;
            this.allowEdit = allowEdit;

            Padding = new MarginPadding { Top = Header.HEIGHT };
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            sampleStart = audio.Samples.Get(@"SongSelect/confirm-selection");

            InternalChild = new PopoverContainer
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    beatmapAvailabilityTracker,
                    new MultiplayerRoomSounds(),
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        RowDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension(GridSizeMode.Absolute, 50)
                        },
                        Content = new[]
                        {
                            // Padded main content (drawable room + main content)
                            new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding
                                    {
                                        Horizontal = WaveOverlayContainer.WIDTH_PADDING,
                                        Bottom = 30
                                    },
                                    Children = new[]
                                    {
                                        mainContent = new GridContainer
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            RowDimensions = new[]
                                            {
                                                new Dimension(GridSizeMode.AutoSize),
                                                new Dimension(GridSizeMode.Absolute, 10)
                                            },
                                            Content = new[]
                                            {
                                                new Drawable[]
                                                {
                                                    new DrawableMatchRoom(Room, allowEdit)
                                                    {
                                                        OnEdit = () => settingsOverlay.Show(),
                                                        SelectedItem = SelectedItem
                                                    }
                                                },
                                                null,
                                                new Drawable[]
                                                {
                                                    new Container
                                                    {
                                                        RelativeSizeAxes = Axes.Both,
                                                        Children = new[]
                                                        {
                                                            new Container
                                                            {
                                                                RelativeSizeAxes = Axes.Both,
                                                                Masking = true,
                                                                CornerRadius = 10,
                                                                Child = new Box
                                                                {
                                                                    RelativeSizeAxes = Axes.Both,
                                                                    Colour = Color4Extensions.FromHex(@"3e3a44") // Temporary.
                                                                },
                                                            },
                                                            new Container
                                                            {
                                                                RelativeSizeAxes = Axes.Both,
                                                                Padding = new MarginPadding(20),
                                                                Child = CreateMainContent(),
                                                            },
                                                            new Container
                                                            {
                                                                Anchor = Anchor.BottomLeft,
                                                                Origin = Anchor.BottomLeft,
                                                                RelativeSizeAxes = Axes.X,
                                                                AutoSizeAxes = Axes.Y,
                                                            },
                                                        }
                                                    }
                                                }
                                            }
                                        },
                                        new Container
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            // Resolves 1px masking errors between the settings overlay and the room panel.
                                            Padding = new MarginPadding(-1),
                                            Child = settingsOverlay = CreateRoomSettingsOverlay(Room)
                                        }
                                    },
                                },
                            },
                            // Footer
                            new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Children = new Drawable[]
                                    {
                                        new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Colour = Color4Extensions.FromHex(@"28242d") // Temporary.
                                        },
                                        new Container
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Padding = new MarginPadding(5),
                                            Child = CreateFooter()
                                        },
                                    }
                                }
                            }
                        }
                    }
                }
            };

            LoadComponent(UserModsSelectOverlay = new RoomModSelectOverlay
            {
                SelectedItem = { BindTarget = SelectedItem },
                SelectedMods = { BindTarget = UserMods },
                IsValidMod = _ => false
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            SelectedItem.BindValueChanged(_ => Scheduler.AddOnce(OnSelectedItemChanged));
            UserMods.BindValueChanged(_ => Scheduler.AddOnce(OnSelectedItemChanged));
            UserBeatmap.BindValueChanged(_ => Scheduler.AddOnce(OnSelectedItemChanged));
            UserRuleset.BindValueChanged(_ => Scheduler.AddOnce(OnSelectedItemChanged));

            beatmapAvailabilityTracker.SelectedItem.BindTo(SelectedItem);
            beatmapAvailabilityTracker.Availability.BindValueChanged(_ => updateBeatmap());

            userModsSelectOverlayRegistration = overlayManager?.RegisterBlockingOverlay(UserModsSelectOverlay);

            Room.PropertyChanged += onRoomPropertyChanged;
            updateSetupState();
        }

        private void onRoomPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Room.RoomID))
                updateSetupState();
        }

        private void updateSetupState()
        {
            if (Room.RoomID == null)
            {
                // A new room is being created.
                // The main content should be hidden until the settings overlay is hidden, signaling the room is ready to be displayed.
                mainContent.Hide();
                settingsOverlay.Show();
            }
            else
            {
                mainContent.Show();
                settingsOverlay.Hide();
            }
        }

        protected virtual bool IsConnected => API.State.Value == APIState.Online;

        public override bool OnBackButton()
        {
            if (Room.RoomID == null)
            {
                if (!ensureExitConfirmed())
                    return true;

                settingsOverlay.Hide();
                return base.OnBackButton();
            }

            if (UserModsSelectOverlay.State.Value == Visibility.Visible)
            {
                UserModsSelectOverlay.Hide();
                return true;
            }

            if (settingsOverlay.State.Value == Visibility.Visible)
            {
                settingsOverlay.Hide();
                return true;
            }

            return base.OnBackButton();
        }

        protected void ShowUserModSelect() => UserModsSelectOverlay.Show();

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);
            beginHandlingTrack();
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            // Should be a noop in most cases, but let's ensure beyond doubt that the beatmap is in a correct state.
            updateBeatmap();

            onLeaving();
            base.OnSuspending(e);
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            base.OnResuming(e);

            updateBeatmap();
            updateSpecifics();

            beginHandlingTrack();
        }

        protected bool ExitConfirmed { get; private set; }

        public override bool OnExiting(ScreenExitEvent e)
        {
            if (!ensureExitConfirmed())
                return true;

            RoomManager?.PartRoom();
            Mods.Value = Array.Empty<Mod>();

            onLeaving();

            return base.OnExiting(e);
        }

        private bool ensureExitConfirmed()
        {
            if (ExitConfirmed)
                return true;

            if (!IsConnected)
                return true;

            bool hasUnsavedChanges = Room.RoomID == null && Room.Playlist.Count > 0;

            if (DialogOverlay == null || !hasUnsavedChanges)
                return true;

            // if the dialog is already displayed, block exiting until the user explicitly makes a decision.
            if (DialogOverlay.CurrentDialog is ConfirmDiscardChangesDialog discardChangesDialog)
            {
                discardChangesDialog.Flash();
                return false;
            }

            DialogOverlay.Push(new ConfirmDiscardChangesDialog(() =>
            {
                ExitConfirmed = true;
                settingsOverlay.Hide();
                this.Exit();
            }));

            return false;
        }

        protected void StartPlay()
        {
            if (SelectedItem.Value is not PlaylistItem item)
                return;

            item = item.With(
                ruleset: GetGameplayRuleset().OnlineID,
                beatmap: new Optional<IBeatmapInfo>(GetGameplayBeatmap()));

            // User may be at song select or otherwise when the host starts gameplay.
            // Ensure that they first return to this screen, else global bindables (beatmap etc.) may be in a bad state.
            if (!this.IsCurrentScreen())
            {
                this.MakeCurrent();

                Schedule(StartPlay);
                return;
            }

            sampleStart?.Play();

            // fallback is to allow this class to operate when there is no parent OnlineScreen (testing purposes).
            var targetScreen = (Screen?)ParentScreen ?? this;

            targetScreen.Push(CreateGameplayScreen(item));
        }

        /// <summary>
        /// Creates the gameplay screen to be entered.
        /// </summary>
        /// <param name="selectedItem">The playlist item about to be played.</param>
        /// <returns>The screen to enter.</returns>
        protected abstract Screen CreateGameplayScreen(PlaylistItem selectedItem);

        protected void OnSelectedItemChanged()
        {
            if (!this.IsCurrentScreen() || SelectedItem.Value is not PlaylistItem item)
                return;

            updateUserMods();
            updateBeatmap();
            updateSpecifics();

            if (!item.AllowedMods.Any())
            {
                UserModsSection?.Hide();
                UserModsSelectOverlay.Hide();
                UserModsSelectOverlay.IsValidMod = _ => false;
            }
            else
            {
                UserModsSection?.Show();

                var rulesetInstance = GetGameplayRuleset().CreateInstance();
                var allowedMods = item.AllowedMods.Select(m => m.ToMod(rulesetInstance));
                UserModsSelectOverlay.IsValidMod = m => allowedMods.Any(a => a.GetType() == m.GetType());
            }

            if (item.FreeStyle)
                UserStyleSection?.Show();
            else
                UserStyleSection?.Hide();
        }

        private void updateUserMods()
        {
            if (!this.IsCurrentScreen() || SelectedItem.Value is not PlaylistItem item)
                return;

            // Remove any user mods that are no longer allowed.
            Ruleset rulesetInstance = GetGameplayRuleset().CreateInstance();
            Mod[] allowedMods = item.AllowedMods.Select(m => m.ToMod(rulesetInstance)).ToArray();
            Mod[] newUserMods = UserMods.Value.Where(m => allowedMods.Any(a => m.GetType() == a.GetType())).ToArray();

            if (newUserMods.SequenceEqual(UserMods.Value))
                return;

            UserMods.Value = UserMods.Value.Where(m => allowedMods.Any(a => m.GetType() == a.GetType())).ToList();
        }

        private void updateBeatmap()
        {
            if (!this.IsCurrentScreen() || SelectedItem.Value is not PlaylistItem)
                return;

            // Retrieve the corresponding local beatmap, since we can't directly use the playlist's beatmap info
            int beatmapId = GetGameplayBeatmap().OnlineID;
            var localBeatmap = beatmapManager.QueryBeatmap(b => b.OnlineID == beatmapId);
            Beatmap.Value = beatmapManager.GetWorkingBeatmap(localBeatmap);
            UserModsSelectOverlay.Beatmap.Value = Beatmap.Value;
        }

        private void updateSpecifics()
        {
            if (!this.IsCurrentScreen() || SelectedItem.Value is not PlaylistItem)
                return;

            var rulesetInstance = GetGameplayRuleset().CreateInstance();
            Mods.Value = GetGameplayMods().Select(m => m.ToMod(rulesetInstance)).ToArray();

            Ruleset.Value = GetGameplayRuleset();

            if (UserStyleDisplayContainer != null)
            {
                PlaylistItem gameplayItem = SelectedItem.Value.With(ruleset: GetGameplayRuleset().OnlineID, beatmap: new Optional<IBeatmapInfo>(GetGameplayBeatmap()));
                PlaylistItem? currentItem = UserStyleDisplayContainer.SingleOrDefault()?.Item;

                if (gameplayItem.Equals(currentItem))
                    return;

                UserStyleDisplayContainer.Child = new DrawableRoomPlaylistItem(gameplayItem)
                {
                    AllowReordering = false,
                    AllowEditing = true,
                    RequestEdit = _ => OpenStyleSelection()
                };
            }
        }

        protected virtual APIMod[] GetGameplayMods()
            => UserMods.Value.Select(m => new APIMod(m)).Concat(SelectedItem.Value!.RequiredMods).ToArray();

        protected virtual RulesetInfo GetGameplayRuleset()
            => Rulesets.GetRuleset(UserRuleset.Value?.OnlineID ?? SelectedItem.Value!.RulesetID)!;

        protected virtual IBeatmapInfo GetGameplayBeatmap()
            => UserBeatmap.Value ?? SelectedItem.Value!.Beatmap;

        protected abstract void OpenStyleSelection();

        private void beginHandlingTrack()
        {
            Beatmap.BindValueChanged(applyLoopingToTrack, true);
        }

        private void onLeaving()
        {
            UserModsSelectOverlay.Hide();
            endHandlingTrack();

            previewTrackManager.StopAnyPlaying(this);
        }

        private void endHandlingTrack()
        {
            Beatmap.ValueChanged -= applyLoopingToTrack;
            cancelTrackLooping();
        }

        private void applyLoopingToTrack(ValueChangedEvent<WorkingBeatmap>? _ = null)
        {
            if (!this.IsCurrentScreen())
                return;

            var track = Beatmap.Value?.Track;

            if (track != null)
            {
                Beatmap.Value!.PrepareTrackForPreview(true);
                music.EnsurePlayingSomething();
            }
        }

        private void cancelTrackLooping()
        {
            var track = Beatmap.Value?.Track;

            if (track != null)
                track.Looping = false;
        }

        /// <summary>
        /// Creates the main centred content.
        /// </summary>
        protected abstract Drawable CreateMainContent();

        /// <summary>
        /// Creates the footer content.
        /// </summary>
        protected abstract Drawable CreateFooter();

        /// <summary>
        /// Creates the room settings overlay.
        /// </summary>
        /// <param name="room">The room to change the settings of.</param>
        protected abstract RoomSettingsOverlay CreateRoomSettingsOverlay(Room room);

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            userModsSelectOverlayRegistration?.Dispose();
            Room.PropertyChanged -= onRoomPropertyChanged;
        }
    }
}
