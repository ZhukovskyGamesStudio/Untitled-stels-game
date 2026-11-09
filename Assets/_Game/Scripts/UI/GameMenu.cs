using Stealth.Net;
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

        void Awake()
        {
            HostSteam.onClick.AddListener(() => SteamLobby.Instance.HostLobby());
            HostLocal.onClick.AddListener(() => GameNetwork.Instance.StartHostLocal());
            Join.onClick.AddListener(() => GameNetwork.Instance.StartClientLocal(Address.text));
            MainSettings.onClick.AddListener(OpenSettings);
            Quit.onClick.AddListener(Application.Quit);

            Invite.onClick.AddListener(() => SteamLobby.Instance.OpenInviteDialog());
            Resume.onClick.AddListener(() => IsOpen = false);
            PauseSettings.onClick.AddListener(OpenSettings);
            Leave.onClick.AddListener(() =>
            {
                IsOpen = false;
                GameNetwork.Instance.Leave();
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
            var net = GameNetwork.Instance;
            if (net == null) return;
            if (!net.IsRunning) IsOpen = false;
            else if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (SettingsOpen) CloseSettings();
                else IsOpen = !IsOpen;
            }

            bool settings = SettingsOpen && (IsOpen || !net.IsRunning);
            bool main = !settings && !net.IsRunning;
            bool pause = !settings && net.IsRunning && IsOpen;
            if (settings && !SettingsPage.activeSelf) SyncSettingsControls();
            Ui.Show(SettingsPage, settings);
            Ui.Show(MainPage, main);
            Ui.Show(PausePage, pause);

            if (main) UpdateMain(net);
            if (pause) UpdatePause(net);
            if (settings) UpdateSettingsLabels();
        }

        void UpdateMain(GameNetwork net)
        {
            SteamStatus.text = $"Steam: {(SteamManager.Initialized ? "подключён как " + SteamManager.PersonaName : "недоступен — " + SteamManager.Error)}";
            HostSteam.interactable = SteamManager.Initialized;
            LocalCaption.text = $"Локальная сеть / тест на одном ПК (UDP, порт {net.LocalPort}):";
            Ui.Show(NetStatus, !string.IsNullOrEmpty(net.Status));
            NetStatus.text = net.Status;
        }

        void UpdatePause(GameNetwork net)
        {
            Role.text = net.Manager.IsHost ? "Ты хост" : "Ты подключён к хосту";
            Ui.Show(Invite, net.UsingSteam && SteamLobby.Instance != null && SteamLobby.Instance.InLobby);
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
