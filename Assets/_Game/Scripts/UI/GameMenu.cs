using Stealth.Audio;
using Stealth.Configs;
using Stealth.Game;
using Stealth.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// Main menu (host / join), the in-game Esc menu with every control (rebindable) and the settings page (prefab UI/Menu).
    /// Buttons, sliders and toggles are pre-built in the prefab; this presenter wires them up and shows the right page.
    /// </summary>
    public class GameMenu : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        public static bool SettingsOpen { get; private set; }
        /// <summary>The controls page opened from the main menu (in a game they are on the pause page).</summary>
        public static bool ControlsOpen { get; private set; }

        /// <summary>Test hook: opens the pause menu.</summary>
        public static void OpenPauseForTest() => IsOpen = true;

        /// <summary>Test hook: closes the pause menu.</summary>
        public static void ClosePauseForTest() => IsOpen = false;

        /// <summary>Test hook: opens the pause menu with the settings page.</summary>
        public static void OpenSettingsForTest()
        {
            IsOpen = true;
            OpenSettings();
        }

        [Header("Pages")]
        public GameObject MainPage;
        public GameObject PausePage;
        public GameObject SettingsPage;

        [Header("Main")]
        public Text SteamStatus;
        public Button HostSteam;
        public Text LocalCaption;
        public Button HostLocal;
        [Tooltip("Main game (off) or the test rooms (on): small houses of 3–4 rooms with no guards, to tune rooms in.")]
        public Toggle TestRooms;
        [Tooltip("The classic generator: rooms cut first, their types guessed from size (LevelGenerator.Classic).")]
        public Toggle ClassicRooms;
        [Tooltip("The old enemies: guards on patrol, wardens, hounds, the hunter (LevelGenerator.ClassicEnemies).")]
        public Toggle ClassicEnemies;
        public InputField Address;
        public Button Join;
        public Text NetStatus;
        public Button MainSettings;
        public Button MainControls;
        public Button Quit;

        [Header("Friends in game (Steam)")]
        public Text FriendsStatus;
        public RectTransform FriendList;
        public FriendLobbyRowView FriendRowPrefab;
        public Button RefreshFriends;
        [Tooltip("Seconds between automatic refreshes while the main menu is open.")]
        public float FriendsRefreshInterval = 5f;

        [Header("Pause")]
        public Text Role;
        public Button Invite;
        public Button Resume;
        [Tooltip("\"I'm stuck\": moves the local player to the nearest free spot.")]
        public Button Unstuck;
        public Button PauseSettings;
        public Button Leave;

        [Header("Controls")]
        [Tooltip("Every control, rebindable: next to the pause buttons in a game, its own page from the main menu.")]
        public ControlsPanel KeysPanel;
        public GameObject ControlsBackdrop;
        public Button ControlsDone;

        [Header("Settings")]
        public Text SensitivityLabel;
        public Slider Sensitivity;
        public Text FovLabel;
        public Slider Fov;
        public Toggle InvertY;
        public Toggle HeadBob;
        public Toggle CrouchToggle;
        public Text VolumeLabel;
        public Slider Volume;
        public Text MusicLabel;
        public Slider Music;
        [Tooltip("Cycles the languages of the texts (Loc.All); its label names the current one.")]
        public Button Language;
        public Button Defaults;
        public Button Done;

        [Header("Voice chat")]
        [Tooltip("Cycles voice activation / push-to-talk / off.")]
        public Button VoiceMode;
        [Tooltip("Cycles the microphones (Microphone.devices); its label names the current one.")]
        public Button VoiceDevice;
        public Text MicSensitivityLabel;
        public Slider MicSensitivity;
        public Text MicLevelLabel;
        [Tooltip("Live level of the microphone (not interactive); its fill turns green above the voice activation threshold.")]
        public Slider MicLevel;
        public Toggle NoiseSuppression;
        public Text VoiceVolumeLabel;
        public Slider VoiceVolume;

        UiPool<FriendLobbyRowView> m_FriendRows;
        float m_NextFriendsRefresh;

        void Awake()
        {
            GameSettings.EnsureLoaded();
            m_FriendRows = new UiPool<FriendLobbyRowView>(FriendRowPrefab, FriendList);
            RefreshFriends.onClick.AddListener(() => m_NextFriendsRefresh = 0f);
            HostSteam.onClick.AddListener(() => Session.Launcher.HostSteam());
            HostLocal.onClick.AddListener(() => Session.Launcher.HostLocal());
            // "-testrooms" on the command line: start with the switch on (every peer passes it, like the switch itself)
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-testrooms") >= 0 && !Configs.Tuning.TestRooms)
            {
                Configs.Tuning.SetTestRooms(true);
                Level.VisualStyles.UseConfig(Configs.Tuning.Styles);
            }
            // "-classicrooms": the classic generator (every peer passes it, like the switch itself)
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-classicrooms") >= 0) Level.LevelGenerator.Classic = true;
            if (ClassicRooms != null)
            {
                ClassicRooms.SetIsOnWithoutNotify(Level.LevelGenerator.Classic);
                ClassicRooms.onValueChanged.AddListener(on => Level.LevelGenerator.Classic = on);
            }
            // "-classicenemies": the old enemies (the host's choice travels with the house to every peer)
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-classicenemies") >= 0) Level.LevelGenerator.ClassicEnemies = true;
            if (ClassicEnemies != null)
            {
                ClassicEnemies.SetIsOnWithoutNotify(Level.LevelGenerator.ClassicEnemies);
                ClassicEnemies.onValueChanged.AddListener(on => Level.LevelGenerator.ClassicEnemies = on);
            }
            if (TestRooms != null)
            {
                TestRooms.SetIsOnWithoutNotify(Configs.Tuning.TestRooms);
                TestRooms.onValueChanged.AddListener(on =>
                {
                    Configs.Tuning.SetTestRooms(on);
                    Level.VisualStyles.UseConfig(Configs.Tuning.Styles); // the test rooms: «Заброшенный комплекс» only
                });
            }
            Join.onClick.AddListener(() => Session.Launcher.JoinLocal(Address.text));
            MainSettings.onClick.AddListener(OpenSettings);
            MainControls.onClick.AddListener(() => ControlsOpen = true);
            ControlsDone.onClick.AddListener(() => ControlsOpen = false);
            Quit.onClick.AddListener(Application.Quit);

            Invite.onClick.AddListener(() => Session.Launcher.InviteFriends());
            Resume.onClick.AddListener(() => IsOpen = false);
            if (Unstuck != null)
                Unstuck.onClick.AddListener(() =>
                {
                    IsOpen = false;
                    PlayerController.Local?.RequestUnstuck();
                });
            PauseSettings.onClick.AddListener(OpenSettings);
            Leave.onClick.AddListener(() =>
            {
                IsOpen = false;
                Session.Launcher.Leave();
            });

            Sensitivity.onValueChanged.AddListener(v => GameSettings.Sensitivity = v);
            Fov.onValueChanged.AddListener(v => GameSettings.Fov = Mathf.Round(v));
            InvertY.onValueChanged.AddListener(v => GameSettings.InvertY = v);
            HeadBob.onValueChanged.AddListener(v => GameSettings.HeadBob = v);
            CrouchToggle.onValueChanged.AddListener(v => GameSettings.CrouchToggle = v);
            Volume.onValueChanged.AddListener(v => GameSettings.MasterVolume = v);
            Music.onValueChanged.AddListener(v => GameSettings.MusicVolume = v);
            Defaults.onClick.AddListener(() =>
            {
                GameSettings.ResetToDefaults();
                VoicePrefs.ResetToDefaults();
                SyncSettingsControls();
            });
            Done.onClick.AddListener(CloseSettings);
            WireVoice();
            if (Language != null) Language.onClick.AddListener(NextLanguage);
        }

        /// <summary>The next language of <see cref="Loc.All"/> (saved at once; every label follows it).</summary>
        static void NextLanguage()
        {
            int i = System.Array.IndexOf(Loc.All, Loc.Language);
            Loc.Use(Loc.All[(i + 1) % Loc.All.Length]);
        }

        static void OpenSettings()
        {
            GameSettings.EnsureLoaded();
            SettingsOpen = true;
        }

        static void CloseSettings()
        {
            GameSettings.Save();
            SettingsOpen = false;
        }

        void Update()
        {
            var net = Session.Launcher;
            if (net == null) return;
            bool running = Session.IsRunning;
            if (!running) IsOpen = false;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (ControlsPanel.Rebinding) KeysPanel.Cancel();
                else if (SettingsOpen) CloseSettings();
                else if (ControlsOpen && !running) ControlsOpen = false;
                // at a terminal, a safe or a vault console Esc leaves that first; the next one opens the menu
                else if (running && !IsOpen && Stealth.Player.PlayerController.Local != null && Stealth.Player.PlayerController.Local.LeaveModeOnEscape()) { }
                else if (running) IsOpen = !IsOpen;
            }
            if (running) ControlsOpen = false;

            bool settings = SettingsOpen && (IsOpen || !running);
            bool main = !settings && !running;
            bool pause = !settings && running && IsOpen;
            bool keys = pause || (main && ControlsOpen);
            if (settings && !SettingsPage.activeSelf) SyncSettingsControls();
            if (keys && !KeysPanel.gameObject.activeSelf) GameSettings.EnsureLoaded(); // the saved bindings
            Ui.Show(SettingsPage, settings);
            Ui.Show(MainPage, main);
            Ui.Show(PausePage, pause);
            Ui.Show(KeysPanel, keys);
            Ui.Show(ControlsBackdrop, main && ControlsOpen);
            Ui.Show(ControlsDone, main && ControlsOpen);

            if (main) UpdateMain(net);
            if (pause) UpdatePause(net);
            if (settings) UpdateSettingsLabels();
            VoiceCapture.Testing = settings;
        }

        void LateUpdate()
        {
            // Menus always get a free cursor — also right after leaving a session, when the player that locked it is gone.
            if (Session.IsRunning && !IsOpen && !SettingsOpen) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void UpdateMain(ISessionLauncher net)
        {
            SteamStatus.text = net.SteamStatus; // only our words: the technical reason is in the log
            HostSteam.interactable = net.SteamAvailable;
            LocalCaption.text = Loc.F("menu.local", net.LocalPort);
            Ui.Show(NetStatus, !string.IsNullOrEmpty(net.Status));
            NetStatus.text = net.Status;
            UpdateFriends(net);
        }

        /// <summary>Friends hosting a session: refreshed every few seconds, one click to join.</summary>
        void UpdateFriends(ISessionLauncher net)
        {
            if (net.SteamAvailable && Time.unscaledTime >= m_NextFriendsRefresh)
            {
                m_NextFriendsRefresh = Time.unscaledTime + FriendsRefreshInterval;
                net.RefreshFriendLobbies();
            }
            var lobbies = DevFlags.FriendLobbiesOverride ?? net.FriendLobbies;
            RefreshFriends.interactable = net.SteamAvailable;
            m_FriendRows.Begin();
            foreach (var lobby in lobbies) m_FriendRows.Next().Set(lobby, id => net.JoinFriendLobby(id));
            m_FriendRows.End();
            string status = lobbies.Count > 0 ? null
                : !net.SteamAvailable && DevFlags.FriendLobbiesOverride == null ? Loc.T("friends.no_steam")
                : Loc.T("friends.none");
            Ui.Show(FriendsStatus, status != null);
            if (status != null) FriendsStatus.text = status;
        }

        void UpdatePause(ISessionLauncher net)
        {
            Role.text = Loc.T(net.IsHost ? "menu.role_host" : "menu.role_client");
            Ui.Show(Invite, net.CanInviteFriends);
        }

        void SyncSettingsControls()
        {
            Sensitivity.SetValueWithoutNotify(GameSettings.Sensitivity);
            Fov.SetValueWithoutNotify(GameSettings.Fov);
            InvertY.SetIsOnWithoutNotify(GameSettings.InvertY);
            HeadBob.SetIsOnWithoutNotify(GameSettings.HeadBob);
            CrouchToggle.SetIsOnWithoutNotify(GameSettings.CrouchToggle);
            Volume.SetValueWithoutNotify(GameSettings.MasterVolume);
            Music.SetValueWithoutNotify(GameSettings.MusicVolume);
            if (MicSensitivity != null) MicSensitivity.SetValueWithoutNotify(VoicePrefs.Sensitivity);
            if (NoiseSuppression != null) NoiseSuppression.SetIsOnWithoutNotify(VoicePrefs.NoiseSuppression);
            if (VoiceVolume != null) VoiceVolume.SetValueWithoutNotify(VoicePrefs.Volume);
        }

        void WireVoice()
        {
            if (VoiceMode == null) return; // an old prefab without the voice column
            VoiceMode.onClick.AddListener(() => VoicePrefs.Mode = VoicePrefs.Next(VoicePrefs.Mode));
            VoiceDevice.onClick.AddListener(() =>
            {
                var devices = Microphone.devices;
                int i = System.Array.IndexOf(devices, VoicePrefs.Device); // -1 = the default → the first device
                VoicePrefs.Device = i + 1 < devices.Length ? devices[i + 1] : ""; // past the last: back to the default
            });
            MicSensitivity.onValueChanged.AddListener(v => VoicePrefs.Sensitivity = v);
            NoiseSuppression.onValueChanged.AddListener(v => VoicePrefs.NoiseSuppression = v);
            VoiceVolume.onValueChanged.AddListener(v => VoicePrefs.Volume = v);
        }

        void UpdateVoiceLabels()
        {
            if (VoiceMode == null) return;
            string mode = VoicePrefs.Mode switch
            {
                Stealth.Audio.VoiceMode.PushToTalk => Loc.F("menu.voice_ptt", Controls.Label(GameAction.PushToTalk)),
                Stealth.Audio.VoiceMode.Off => Loc.T("menu.voice_off"),
                _ => Loc.T("menu.voice_activity"),
            };
            SetLabel(VoiceMode, mode);
            string device = string.IsNullOrEmpty(VoicePrefs.Device) ? Loc.T("menu.voice_default_mic") : VoicePrefs.Device;
            SetLabel(VoiceDevice, Loc.F("menu.voice_device", device));
            MicSensitivityLabel.text = Loc.F("menu.voice_sensitivity", VoicePrefs.Sensitivity * 100f);
            VoiceVolumeLabel.text = Loc.F("menu.voice_volume", VoicePrefs.Volume * 100f);
            var cap = VoiceCapture.Instance;
            bool live = cap != null && cap.Running;
            MicLevelLabel.text = !VoiceNative.Available ? Loc.T("menu.voice_unavailable")
                : live ? Loc.T("menu.voice_level") : Loc.T("menu.voice_no_mic");
            float db = live ? cap.LevelDb : VoiceLevel.Silence;
            MicLevel.SetValueWithoutNotify(Mathf.InverseLerp(-70f, 0f, db));
            var fill = MicLevel.fillRect != null ? MicLevel.fillRect.GetComponent<Graphic>() : null;
            bool open = live && db >= VoiceGate.ThresholdDb(VoicePrefs.Sensitivity, Tuning.Audio.Voice);
            if (fill != null) fill.color = open ? UiPalette.MarkGreen : UiPalette.Rule;
        }

        static void SetLabel(Button button, string text)
        {
            var label = button.GetComponentInChildren<Text>();
            if (label != null && label.text != text) label.text = text;
        }

        void UpdateSettingsLabels()
        {
            SensitivityLabel.text = Loc.F("menu.sensitivity", GameSettings.Sensitivity * 10f);
            FovLabel.text = Loc.F("menu.fov", GameSettings.Fov);
            VolumeLabel.text = Loc.F("menu.volume", GameSettings.MasterVolume * 100f);
            MusicLabel.text = Loc.F("menu.music", GameSettings.MusicVolume * 100f);
            UpdateVoiceLabels();
            if (Language != null)
            {
                var label = Language.GetComponentInChildren<Text>();
                string text = Loc.F("menu.language", Loc.NameOf(Loc.Language));
                if (label != null && label.text != text) label.text = text;
            }
        }
    }
}
