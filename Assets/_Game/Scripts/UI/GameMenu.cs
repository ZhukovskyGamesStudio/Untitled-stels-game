using Stealth.Game;
using Stealth.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// Main menu (host / join), the in-game Esc menu and the settings page (prefab UI/Menu).
    /// Buttons, sliders and toggles are pre-built in the prefab; this presenter wires them up and shows the right page.
    /// </summary>
    public class GameMenu : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        public static bool SettingsOpen { get; private set; }

        /// <summary>Test hook: opens the pause menu.</summary>
        public static void OpenPauseForTest() => IsOpen = true;

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
        public InputField Address;
        public Button Join;
        public Text NetStatus;
        public Button MainSettings;
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
        public Button PauseSettings;
        public Button Leave;

        [Header("Settings")]
        public Text SensitivityLabel;
        public Slider Sensitivity;
        public Text FovLabel;
        public Slider Fov;
        public Toggle InvertY;
        public Toggle HeadBob;
        public Toggle CrouchToggle;
        public Button Defaults;
        public Button Done;

        UiPool<FriendLobbyRowView> m_FriendRows;
        float m_NextFriendsRefresh;

        void Awake()
        {
            m_FriendRows = new UiPool<FriendLobbyRowView>(FriendRowPrefab, FriendList);
            RefreshFriends.onClick.AddListener(() => m_NextFriendsRefresh = 0f);
            HostSteam.onClick.AddListener(() => Session.Launcher.HostSteam());
            HostLocal.onClick.AddListener(() => Session.Launcher.HostLocal());
            Join.onClick.AddListener(() => Session.Launcher.JoinLocal(Address.text));
            MainSettings.onClick.AddListener(OpenSettings);
            Quit.onClick.AddListener(Application.Quit);

            Invite.onClick.AddListener(() => Session.Launcher.InviteFriends());
            Resume.onClick.AddListener(() => IsOpen = false);
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
            Defaults.onClick.AddListener(() =>
            {
                GameSettings.ResetToDefaults();
                SyncSettingsControls();
            });
            Done.onClick.AddListener(CloseSettings);
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
            else if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (SettingsOpen) CloseSettings();
                else IsOpen = !IsOpen;
            }

            bool settings = SettingsOpen && (IsOpen || !running);
            bool main = !settings && !running;
            bool pause = !settings && running && IsOpen;
            if (settings && !SettingsPage.activeSelf) SyncSettingsControls();
            Ui.Show(SettingsPage, settings);
            Ui.Show(MainPage, main);
            Ui.Show(PausePage, pause);

            if (main) UpdateMain(net);
            if (pause) UpdatePause(net);
            if (settings) UpdateSettingsLabels();
        }

        void UpdateMain(ISessionLauncher net)
        {
            SteamStatus.text = $"Steam: {net.SteamStatus}";
            HostSteam.interactable = net.SteamAvailable;
            LocalCaption.text = $"Локальная сеть / тест на одном ПК (UDP, порт {net.LocalPort}):";
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
                : !net.SteamAvailable && DevFlags.FriendLobbiesOverride == null ? "Нужен запущенный Steam"
                : "Никого из друзей нет в игре. Друг создаёт игру через Steam — она появится здесь.";
            Ui.Show(FriendsStatus, status != null);
            if (status != null) FriendsStatus.text = status;
        }

        void UpdatePause(ISessionLauncher net)
        {
            Role.text = net.IsHost ? "Ты хост" : "Ты подключён к хосту";
            Ui.Show(Invite, net.CanInviteFriends);
        }

        void SyncSettingsControls()
        {
            Sensitivity.SetValueWithoutNotify(GameSettings.Sensitivity);
            Fov.SetValueWithoutNotify(GameSettings.Fov);
            InvertY.SetIsOnWithoutNotify(GameSettings.InvertY);
            HeadBob.SetIsOnWithoutNotify(GameSettings.HeadBob);
            CrouchToggle.SetIsOnWithoutNotify(GameSettings.CrouchToggle);
        }

        void UpdateSettingsLabels()
        {
            SensitivityLabel.text = $"Чувствительность мыши: {GameSettings.Sensitivity * 10f:0.0}";
            FovLabel.text = $"Поле зрения: {GameSettings.Fov:0}°";
        }
    }
}
